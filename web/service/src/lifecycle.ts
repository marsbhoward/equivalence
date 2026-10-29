import type { NextFunction, Request, Response } from 'express';
import type { Server } from 'node:http';
import { closeDb } from './db.js';

/**
 * A CLEAN SHUTDOWN (2026-10-29). Railway stops the old deployment before starting the new one - the
 * volume cannot be mounted twice - with SIGTERM, then SIGKILL `drainingSeconds` later (railway.json).
 * Node's default on SIGTERM is to exit at once, which cut whatever was in flight - and the one
 * request where that hurts is extraction killed between submitting its mint and recording it, which
 * leaves the run marked in flight for a look by hand (routes/player.ts).
 *
 * So on SIGTERM: stop accepting connections, let what is in flight finish (up to DRAIN_MS, inside
 * Railway's window), answer anything new on a kept-alive connection with a 503 the game retries
 * (`code: 'restarting'`, ServiceClient), close the database, exit.
 */
const DRAIN_MS = 25_000;

let draining = false;
let inFlight = 0;

/** Counts requests in flight, and turns new ones away once draining. Mount first. */
export function track(_req: Request, res: Response, next: NextFunction) {
  if (draining) {
    res.setHeader('Connection', 'close');
    res.setHeader('Retry-After', '5');
    res.status(503).json({ error: 'the service is restarting - try again in a moment', code: 'restarting' });
    return;
  }
  inFlight++;
  let done = false;
  const finish = () => { if (!done) { done = true; inFlight--; } };
  res.on('finish', finish);
  res.on('close', finish);
  next();
}

export function shutDownCleanly(server: Server) {
  const stop = async (signal: string) => {
    if (draining) return;
    draining = true;
    console.log(`[lifecycle] ${signal}: draining ${inFlight} request(s)`);
    server.close();
    const until = Date.now() + DRAIN_MS;
    while (inFlight > 0 && Date.now() < until) await new Promise(r => setTimeout(r, 100));
    if (inFlight > 0) console.warn(`[lifecycle] ${inFlight} request(s) still in flight at the deadline`);
    await closeDb().catch(() => {});
    console.log('[lifecycle] stopped');
    process.exit(0);
  };
  process.on('SIGTERM', () => void stop('SIGTERM'));
  process.on('SIGINT', () => void stop('SIGINT'));
}
