import type { Response } from 'express';

/**
 * A failure the CLIENT is told about only in general terms. The detail - a Postgres error, a
 * Blockfrost reply, a missing setting - goes to the service's log, never to the browser: it names
 * tables, hosts and configuration a caller has no use for and an attacker does.
 *
 * Operator routes (`/op/*`, WRITE_TOKEN) keep returning their detail - the operator needs it, and
 * nobody else can reach them.
 */
export function internal(res: Response, e: unknown, where: string, status = 500, error = 'internal_error'): void {
  console.error(`[${where}]`, e instanceof Error ? e.stack ?? e.message : e);
  res.status(status).json({ error });
}
