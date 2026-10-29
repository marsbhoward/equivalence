#!/usr/bin/env bash
# Ships the service and the browser build together, in the order that keeps them compatible:
# checks, the service (Railway), wait for the NEW deployment to answer, the game, then a look at
# what is live. Run from anywhere:
#
#   web/service/scripts/deploy.sh               service + game
#   web/service/scripts/deploy.sh --service     service only
#   web/service/scripts/deploy.sh --game        game only (Builds/WebGL)
#
# The game is built first, in the Editor (Convergence.EditorTools.WebBuild.BuildWeb - CHAIN.md).
# A deploy stops the old container before the new one starts (the volume), so the game sees well
# under a minute of "reconnecting"; the old one drains what is in flight first (src/lifecycle.ts).
set -euo pipefail
cd "$(dirname "$0")/.."
URL="${SERVICE_URL:-https://equivalence-service-production.up.railway.app}"
BUILD="../../Builds/WebGL"
WHAT="${1:-all}"
say() { printf '\n== %s\n' "$*"; }
die() { printf 'STOPPED: %s\n' "$*" >&2; exit 1; }

say "checks"
npm run -s typecheck || die "typecheck failed"
if [[ "$WHAT" != "--service" ]]; then
  [[ -f "$BUILD/index.html" ]] || die "no build at $BUILD - build it in the Editor first"
  n=$(ls "$BUILD/Build" | wc -l | tr -d ' ')
  [[ "$n" -ge 4 ]] || die "$BUILD/Build has $n files - the build is incomplete"
  cmp -s "$BUILD/connector/equivalence-chain.js" ../../Assets/WebGLTemplates/Equivalence/connector/equivalence-chain.js \
    || die "the build's connector differs from the template's - rebuild the game"
  [[ "$BUILD/index.html" -nt ../../Assets/WebGLTemplates/Equivalence/connector/equivalence-chain.js ]] \
    || die "the build is older than the connector - rebuild the game"
  echo "build: $(ls "$BUILD/Build" | tr '\n' ' ')"
fi

if [[ "$WHAT" != "--game" ]]; then
  before=$(curl -s "$URL/health" | sed -n 's/.*"deployment":"\([^"]*\)".*/\1/p')
  say "service (was deployment ${before:-unknown})"
  railway up --ci || die "railway up failed (its builder sometimes times out exporting - run again)"
  say "waiting for the new deployment"
  for i in $(seq 1 60); do
    now=$(curl -s --max-time 5 "$URL/health" | sed -n 's/.*"deployment":"\([^"]*\)".*/\1/p' || true)
    if [[ -n "$now" && "$now" != "$before" ]]; then echo "live: deployment $now"; break; fi
    [[ $i -eq 60 ]] && die "the new deployment did not answer in 5 minutes - railway logs"
    sleep 5
  done
fi

if [[ "$WHAT" != "--service" ]]; then
  say "game"
  node scripts/upload-play.mjs "$BUILD" || die "upload failed - the previous build is still live"
fi

say "what is live"
curl -s "$URL/status"; echo
headers=$(curl -s -D - -o /dev/null "$URL/play/")
grep -qi '^content-security-policy:' <<<"$headers" && echo "page policy: on" || echo "page policy: OFF (CSP_MODE?)"
for f in $(curl -s "$URL/play/" | grep -oE 'Build/[0-9a-f]{32}\.[a-z.]+' | sort -u); do
  code=$(curl -s -o /dev/null -w '%{http_code} %{content_type}' -I "$URL/play/$f")
  echo "$f  $code"
done
echo; echo "done. Roll the game back with: POST /op/play/rollback (WRITE_TOKEN)"
