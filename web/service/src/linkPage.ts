import express, { type Response } from 'express';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { pageHeaders } from './play.js';

/**
 * The DEVICE LINK page (link.ts) - what a wallet's own browser opens to sign for the game. Plain
 * HTML and one script, shipped with the service (web/service/link), under the same strict
 * Content-Security-Policy as the game: no inline script, nothing fetched but its own origin.
 */
const dir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'link');

export function linkPage(): express.RequestHandler {
  return express.static(dir, {
    index: 'index.html',
    setHeaders(res: Response, file: string) {
      res.setHeader('Cache-Control', 'no-cache');
      res.setHeader('X-Content-Type-Options', 'nosniff');
      if (file.endsWith('.html')) pageHeaders(res, file);
    },
  });
}

/** The PRIVACY page (web/service/privacy) - what the game keeps and why, linked from the title screen. */
const privacyDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'privacy');

export function privacyPage(): express.RequestHandler {
  return express.static(privacyDir, {
    index: 'index.html',
    setHeaders(res: Response, file: string) {
      res.setHeader('Cache-Control', 'no-cache');
      res.setHeader('X-Content-Type-Options', 'nosniff');
      if (file.endsWith('.html')) pageHeaders(res, file);
    },
  });
}
