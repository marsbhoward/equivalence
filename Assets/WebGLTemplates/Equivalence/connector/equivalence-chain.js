(() => {
  // ../connector/equivalence-chain.ts
  var LABEL_REFERENCE = "000643b0";
  var LABEL_USER = "000de140";
  var api = null;
  var address = "";
  var session = "";
  var _a;
  var SERVICE = (_a = window.EQUIVALENCE_SERVICE) != null ? _a : "/api";
  async function service(path, init) {
    var _a2;
    const r = await fetch(`${SERVICE}${path}`, {
      ...init,
      headers: {
        "content-type": "application/json",
        ...session ? { authorization: `Bearer ${session}` } : {},
        ...(_a2 = init == null ? void 0 : init.headers) != null ? _a2 : {}
      }
    });
    if (!r.ok) throw new Error(`${path} -> ${r.status} ${await r.text().catch(() => r.statusText)}`);
    return r;
  }
  var hex = (s) => Array.from(new TextEncoder().encode(s)).map((b) => b.toString(16).padStart(2, "0")).join("");
  var connector = {
    // ---- identity ---------------------------------------------------------------------------
    listWallets() {
      const c = window.cardano;
      if (!c) return "";
      return Object.keys(c).filter((k) => {
        var _a2;
        return typeof ((_a2 = c[k]) == null ? void 0 : _a2.enable) === "function";
      }).join(",");
    },
    async connect({ wallet }) {
      var _a2, _b, _c;
      const provider = (_a2 = window.cardano) == null ? void 0 : _a2[wallet];
      if (!provider) throw new Error(`wallet "${wallet}" is not installed`);
      api = await provider.enable();
      const used = await api.getUsedAddresses();
      const unused = used.length ? [] : await api.getUnusedAddresses();
      address = (_c = (_b = used[0]) != null ? _b : unused[0]) != null ? _c : "";
      if (!address) throw new Error("wallet authorised but exposed no address");
      return address;
    },
    async proveOwnership({ accountId }) {
      var _a2;
      if (!api) throw new Error("no wallet connected");
      const ch = await service(`/auth/challenge?address=${encodeURIComponent(accountId)}`);
      const { nonce, message } = await ch.json();
      const sig = await api.signData(accountId, hex(message));
      const proved = await service("/auth/prove", {
        method: "POST",
        body: JSON.stringify({ address: accountId, nonce, ...sig })
      });
      session = (_a2 = (await proved.json()).session) != null ? _a2 : "";
      if (!session) throw new Error("the service issued no session");
      return sig.signature;
    },
    disconnect() {
      api = null;
      address = "";
      session = "";
    },
    session() {
      return session;
    },
    // ---- what the wallet owns ---------------------------------------------------------------
    async listComponents({ accountId }) {
      if (!accountId) return { items: [] };
      const r = await service(`/components?address=${encodeURIComponent(accountId)}`);
      return r.json();
    },
    // ---- reads ------------------------------------------------------------------------------
    async currentPfp({ profileId }) {
      const r = await service(`/pfp/${encodeURIComponent(profileId)}`);
      const body = await r.text();
      return body === "null" || body === "" ? "" : body;
    },
    async pfpStatus({ profileId }) {
      const r = await service(`/pfp/${encodeURIComponent(profileId)}/status`);
      return r.text();
    },
    // ---- design boxes (tokens in the wallet; opening them is the one other thing a player signs) -
    async openBoxes({ profileId, tier, count }) {
      if (!api) throw new Error("no wallet connected");
      const built = await (await service("/write/open-boxes", {
        method: "POST",
        body: JSON.stringify({
          profileId,
          tier,
          count: Number(count) || 1
        })
      })).json();
      const witness = await api.signTx(built.cbor, true);
      const done = await service("/write/open-boxes/submit", {
        method: "POST",
        body: JSON.stringify({ openId: built.openId, witness })
      });
      return done.text();
    },
    async designBoxes({ profileId }) {
      const r = await service(`/boxes/${encodeURIComponent(profileId)}`);
      return r.text();
    },
    // ---- dyes (all else about dyes is the database's - the game reaches it over HTTP) ----------
    async withdrawDyes({ profileId, swatch, count }) {
      const r = await service("/write/dyes/withdraw", {
        method: "POST",
        body: JSON.stringify({ profileId, swatch, count: Number(count) || 1 })
      });
      return r.text();
    },
    depositDyes({ profileId, swatch, count }) {
      return burn("/write/dyes/deposit", { profileId, swatch, count: Number(count) || 1 });
    },
    breakBoxes({ profileId, tier, count }) {
      return burn("/write/break-boxes", { profileId, tier, count: Number(count) || 1 });
    },
    // ---- the ONE player-signed write ---------------------------------------------------------
    assemblePfp({ profileId, accountId, datum }) {
      return trophy("/write/pfp/mint", profileId, accountId, datum);
    },
    updatePfp({ profileId, accountId, datum }) {
      return trophy("/write/pfp/update", profileId, accountId, datum);
    },
    renewPfp({ profileId, accountId }) {
      return trophy("/write/pfp/renew", profileId, accountId, "");
    }
  };
  async function trophy(endpoint, profileId, accountId, datum) {
    if (!api) throw new Error("no wallet connected");
    const built = await service(endpoint, {
      method: "POST",
      body: JSON.stringify({ profileId, address: accountId, recipe: datum })
    });
    const { pfpId, cbor } = await built.json();
    const witness = await api.signTx(cbor, true);
    const done = await service("/write/pfp/submit", {
      method: "POST",
      body: JSON.stringify({ pfpId, witness })
    });
    const { hash } = await done.json();
    if (!hash) throw new Error("submit returned no hash");
    return hash;
  }
  async function burn(endpoint, body) {
    if (!api) throw new Error("no wallet connected");
    const built = await (await service(endpoint, { method: "POST", body: JSON.stringify(body) })).json();
    const witness = await api.signTx(built.cbor, true);
    const done = await service(`${endpoint}/submit`, {
      method: "POST",
      body: JSON.stringify({ burnId: built.burnId, witness })
    });
    return done.text();
  }
  window.EquivalenceChain = connector;
})();
