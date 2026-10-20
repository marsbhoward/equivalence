import type { NextFunction, Request, Response } from 'express';

/**
 * RATE LIMITS - token buckets, per IP or per identity, in front of every public and player route.
 *
 * Why: several routes cost something an attacker can spend for us - Blockfrost's daily quota
 * (/components, /read/health, /boxes), the IPFS quota (/art), the service wallet's ADA (a mint),
 * memory (login challenges), storage (saves). With no limit, one script takes the chain features
 * down for everyone, or fills the database. A limit an honest player never meets is the point:
 * every number below is sized from what the game really sends (CONTINUATION.md - a heartbeat
 * every second at most while a clock runs, a few snapshots a floor) with room to spare.
 *
 * A bucket holds `burst` tokens and refills at `perMinute`; each request takes one, and a request
 * finding the bucket empty is answered 429 with `Retry-After`. Two kinds of key:
 *
 *   per IP        mounted BEFORE authentication, so a flood is refused before any signature
 *                 check or database read is spent on it. Generous - a household or a school
 *                 shares one address.
 *   per identity  mounted AFTER it (the wallet address, or a guest's owner key), so one player
 *                 cannot exhaust a route by spreading over addresses.
 *
 * IN MEMORY, PER REPLICA (as the guest limiter in identity.ts): correct while the service runs as
 * one instance; a scaled-out service needs a shared store (Postgres or Redis) here. The proxy's
 * address is not the player's - `trust proxy` (index.ts) makes req.ip the player's.
 *
 * `RATE_LIMIT_SCALE` multiplies every limit (a load test, a busy launch). It cannot switch limits
 * off: it is clamped between 0.1x and 100x.
 */
const SCALE = Math.min(100, Math.max(0.1, Number(process.env.RATE_LIMIT_SCALE ?? 1) || 1));

/** Keys kept per limiter before idle ones are swept - a brake on memory, never a reset of a live key. */
const MAX_KEYS = 50_000;

interface Bucket {
  tokens: number;
  at: number;
}

export interface Limit {
  /** Requests allowed at once. */
  burst: number;
  /** Requests a minute, sustained. */
  perMinute: number;
}

type KeyOf = (req: Request, res: Response) => string | null;

export class Buckets {
  private readonly map = new Map<string, Bucket>();
  private readonly burst: number;
  private readonly perMs: number;

  constructor(limit: Limit) {
    this.burst = Math.max(1, Math.round(limit.burst * SCALE));
    this.perMs = (limit.perMinute * SCALE) / 60_000;
  }

  /** Takes a token for `key`: 0 if allowed, else the seconds until one is free. */
  take(key: string, now = Date.now()): number {
    let b = this.map.get(key);
    if (b) {
      b.tokens = Math.min(this.burst, b.tokens + (now - b.at) * this.perMs);
      b.at = now;
    } else {
      if (this.map.size >= MAX_KEYS) this.sweep(now);
      b = { tokens: this.burst, at: now };
      this.map.set(key, b);
    }
    if (b.tokens >= 1) {
      b.tokens -= 1;
      return 0;
    }
    return Math.max(1, Math.ceil((1 - b.tokens) / this.perMs / 1000));
  }

  /** Drops every key that has refilled to full - it would start full anyway. */
  private sweep(now: number) {
    const full = this.burst / this.perMs;
    for (const [k, b] of this.map) if (now - b.at >= full) this.map.delete(k);
    // Still full of live keys (a flood from many addresses): drop the oldest half rather than grow.
    if (this.map.size >= MAX_KEYS) {
      let n = this.map.size / 2;
      for (const k of this.map.keys()) {
        if (n-- <= 0) break;
        this.map.delete(k);
      }
    }
  }
}

function limiter(name: string, limit: Limit, keyOf: KeyOf) {
  const buckets = new Buckets(limit);
  return function rateLimit(req: Request, res: Response, next: NextFunction) {
    const key = keyOf(req, res);
    if (key === null) return next();
    const wait = buckets.take(key);
    if (wait === 0) return next();
    res.setHeader('Retry-After', String(wait));
    res.status(429).json({ error: 'rate_limited', limit: name, retryAfter: wait });
  };
}

const ipOf: KeyOf = (req) => req.ip ?? 'unknown';
/** The identity an auth middleware put on res.locals: a save's owner, or a session's address. */
const identityOf: KeyOf = (_req, res) =>
  (res.locals.owner as string | undefined) ?? (res.locals.address as string | undefined) ?? null;

/** Per IP - mount BEFORE authentication. */
export const perIp = (name: string, limit: Limit) => limiter(name, limit, ipOf);

/** Per identity - mount AFTER authentication; passes a request with no identity through. */
export const perIdentity = (name: string, limit: Limit) => limiter(name, limit, identityOf);

/** One bucket for everyone - a ceiling on what a route may spend in total (Blockfrost's quota). */
export const overall = (name: string, limit: Limit) => limiter(name, limit, () => 'all');

/**
 * The limits, in one place. Sized against the game's real traffic:
 *
 *   saves      a heartbeat a second at most (only while a Labyrinth or Collapsing Rift clock
 *              runs; every 5 s otherwise), a few snapshots a floor, a save at run start and
 *              end - well under 120 a minute for one player.
 *   writes     begin-run once a run, commit-run once (and a retry or two), opening boxes in
 *              ones and tens.
 *   public     the game reads /components when the wallet connects and on a refresh; /art per
 *              picture shown.
 */
export const LIMITS = {
  // ---- before authentication (per IP)
  auth: { burst: 10, perMinute: 30 },
  publicRead: { burst: 20, perMinute: 60 },
  components: { burst: 10, perMinute: 20 },
  art: { burst: 60, perMinute: 120 },
  writeIp: { burst: 30, perMinute: 60 },
  savesIp: { burst: 300, perMinute: 900 },
  // ---- after authentication (per identity)
  saves: { burst: 120, perMinute: 240 },
  write: { burst: 20, perMinute: 30 },
  commit: { burst: 3, perMinute: 6 },
  open: { burst: 6, perMinute: 12 },
  boxes: { burst: 10, perMinute: 30 },
  // ---- in total
  /** Unauthenticated routes that call Blockfrost, all callers together. */
  blockfrostPublic: { burst: 60, perMinute: 300 },
} satisfies Record<string, Limit>;
