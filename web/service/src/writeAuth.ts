import type { NextFunction, Request, Response } from 'express';
import { createHash, timingSafeEqual } from 'node:crypto';

/**
 * Every `/write/*` route signs with the funded service wallet, and the service is on the public
 * internet - so a write must carry `Authorization: Bearer <WRITE_TOKEN>`. CORS is no guard here:
 * only browsers obey it, and a plain curl does not.
 *
 * DEFAULT CLOSED, like cors.ts: with `WRITE_TOKEN` unset every write is refused (503), so a
 * deployment that has a seed but no token cannot mint for anyone.
 *
 * This is an OPERATOR secret - for server-to-server calls and testing. It must never ship inside
 * the browser build: anything in the game's JS is public, and a token there is no token at all.
 * The game's own route to a write needs its own authority (a proven wallet, a server-issued
 * session) when that path is built.
 */
export function requireWriteToken(req: Request, res: Response, next: NextFunction) {
  const expected = process.env.WRITE_TOKEN ?? '';
  if (!expected) {
    res.status(503).json({ error: 'writes are disabled: WRITE_TOKEN is not set' });
    return;
  }

  const header = req.headers.authorization ?? '';
  const given = header.startsWith('Bearer ') ? header.slice('Bearer '.length) : '';

  // Compared as fixed-length digests so neither the content nor the LENGTH of the token leaks
  // through response timing (timingSafeEqual throws on unequal lengths).
  const digest = (s: string) => createHash('sha256').update(s).digest();
  if (!given || !timingSafeEqual(digest(given), digest(expected))) {
    res.status(401).json({ error: 'unauthorized' });
    return;
  }
  next();
}
