import type { NextFunction, Request, Response } from 'express';

/**
 * Cross-origin access, from an explicit allowlist.
 *
 * THIS IS REQUIRED, not hardening. The game is a browser build and the service is a separate
 * deployment, so every call the connector makes is cross-origin - without this the browser refuses
 * all of them and the whole chain layer silently does nothing. `window.COALESCENCE_SERVICE` in the
 * WebGL template defaults to the same-origin `/api`, which only works if something is proxying;
 * pointed at a Railway URL it is cross-origin and needs this.
 *
 * DEFAULT CLOSED. With `ALLOWED_ORIGINS` unset nothing cross-origin is permitted - same-origin
 * still works, so a proxied deployment needs no configuration and an open deployment has to say so
 * out loud. A service that signs transactions with a funded wallet is not something to leave
 * answering `*` by default.
 *
 * Exact matches only, and the origin is ECHOED rather than reflected blindly - reflecting whatever
 * arrives is the same as `*` while looking like it is not.
 */
export function cors(req: Request, res: Response, next: NextFunction) {
  const allowed = (process.env.ALLOWED_ORIGINS ?? '')
    .split(',')
    .map(s => s.trim())
    .filter(Boolean);

  const origin = req.headers.origin;
  if (origin && allowed.includes(origin)) {
    res.setHeader('Access-Control-Allow-Origin', origin);
    // Tells caches the response varies by origin. Without it a proxy can serve one origin's
    // allow-header to another, which is a hole that only appears once something sits in front.
    res.setHeader('Vary', 'Origin');
    res.setHeader('Access-Control-Allow-Methods', 'GET,POST,HEAD,OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    res.setHeader('Access-Control-Max-Age', '600');
  }

  if (req.method === 'OPTIONS') {
    res.sendStatus(origin && allowed.includes(origin) ? 204 : 403);
    return;
  }
  next();
}
