// The perf-test build, served to a phone on the LAN (Assets/Editor/WebBuild.cs "Build for Web (perf test)",
// Assets/Scripts/Core/PerfTest.cs). No packages.
//
//   node Tools/perf-serve/serve.mjs            (from the project root; PORT=8080 by default)
//
// - Serves Builds/WebGLPerf, sending the gzip header Unity's loader expects for *.gz (no fallback decompressor).
// - POST /devlog: the game's reports, one JSON line each, appended to Builds/perf-logs/perf-<date>.jsonl and
//   summarised here as they arrive.
// Nothing is cached (each rebuild is picked up on reload). Stop it with Ctrl-C.

import http from 'node:http';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const root = path.resolve(process.argv[2] ?? 'Builds/WebGLPerf');
const logDir = path.resolve('Builds/perf-logs');
const port = Number(process.env.PORT ?? 8080);

if (!fs.existsSync(path.join(root, 'index.html'))) {
  console.error(`No build at ${root} - run Convergence > Build for Web (perf test) first.`);
  process.exit(1);
}
fs.mkdirSync(logDir, { recursive: true });
const logFile = path.join(logDir, `perf-${new Date().toISOString().slice(0, 10)}.jsonl`);

const types = {
  '.html': 'text/html; charset=utf-8', '.js': 'application/javascript', '.wasm': 'application/wasm',
  '.data': 'application/octet-stream', '.json': 'application/json', '.css': 'text/css', '.png': 'image/png',
  '.webmanifest': 'application/manifest+json', '.ico': 'image/x-icon', '.svg': 'image/svg+xml',
};

function summary(r) {
  if (r.kind === 'hello') return `hello  ${r.device} | ${r.os} | ${r.gpu} | ${r.screen} | ${r.memMB} MB`;
  const p = r.poses ?? {}, m = r.ms ?? {};
  return `${r.kind.padEnd(9)} ${String(r.state).padEnd(8)} floor ${String(r.floor).padStart(2)}  ` +
    `${r.fps} fps  ms median ${m.median} p95 ${m.p95} max ${m.max} (>33: ${m.over33})  moving ${r.movingShare}  ` +
    `gc ${r.gcRuns}  poses new ${p.new} dropped ${p.dropped} swing ${p.swingFrames} cache ${p.cache}/${p.cap} ${p.gpuMB}+${p.cpuMB} MB`;
}

const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://x');
  if (req.method === 'POST' && url.pathname === '/devlog') {
    let body = '';
    req.on('data', (c) => { body += c; if (body.length > 65536) req.destroy(); });
    req.on('end', () => {
      let report;
      try { report = JSON.parse(body); } catch { report = { unparsed: body.slice(0, 2000) }; }
      fs.appendFileSync(logFile, JSON.stringify({ at: new Date().toISOString(), from: req.socket.remoteAddress, report }) + '\n');
      console.log(report.unparsed ? `devlog (unparsed): ${report.unparsed.slice(0, 200)}` : summary(report));
      res.writeHead(204).end();
    });
    return;
  }
  if (req.method !== 'GET' && req.method !== 'HEAD') { res.writeHead(405).end(); return; }

  let rel = decodeURIComponent(url.pathname);
  if (rel.endsWith('/')) rel += 'index.html';
  const file = path.join(root, rel);
  if (!file.startsWith(root) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
    res.writeHead(404, { 'Content-Type': 'application/json' }).end('{"error":"not here - this is the perf-test server"}');
    return;
  }
  const gz = file.endsWith('.gz');
  const headers = {
    'Content-Type': types[path.extname(gz ? file.slice(0, -3) : file)] ?? 'application/octet-stream',
    'Cache-Control': 'no-store',
  };
  if (gz) headers['Content-Encoding'] = 'gzip';
  res.writeHead(200, headers);
  if (req.method === 'HEAD') { res.end(); return; }
  fs.createReadStream(file).pipe(res);
});

server.listen(port, '0.0.0.0', () => {
  const lan = Object.values(os.networkInterfaces()).flat().filter((a) => a && a.family === 'IPv4' && !a.internal).map((a) => a.address);
  console.log(`Serving ${root}`);
  for (const ip of lan) console.log(`  on the phone: http://${ip}:${port}/`);
  console.log(`Reports -> ${logFile}`);
});
