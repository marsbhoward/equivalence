(() => {
  // ../connector/equivalence-chain.ts
  var LABEL_REFERENCE = "000643b0";
  var LABEL_USER = "000de140";
  var api = null;
  var address = "";
  var session = "";
  var KEY_DB = "equivalence-keys";
  var STORED = "equivalence.wallet";
  function idb(mode, run) {
    return new Promise((resolve, reject) => {
      const open = indexedDB.open(KEY_DB, 1);
      open.onupgradeneeded = () => open.result.createObjectStore("keys");
      open.onerror = () => reject(open.error);
      open.onsuccess = () => {
        const tx = open.result.transaction("keys", mode);
        const req = run(tx.objectStore("keys"));
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
        tx.oncomplete = () => open.result.close();
      };
    });
  }
  var keyPair = null;
  async function sessionKey() {
    if (keyPair) return keyPair;
    const kept = await idb("readonly", (s) => s.get("session")).catch(() => void 0);
    if ((kept == null ? void 0 : kept.privateKey) && kept.publicKey) return keyPair = kept;
    const made = await crypto.subtle.generateKey({ name: "ECDSA", namedCurve: "P-256" }, false, ["sign", "verify"]);
    await idb("readwrite", (s) => s.put(made, "session")).catch(() => void 0);
    return keyPair = made;
  }
  async function dropKey() {
    keyPair = null;
    await idb("readwrite", (s) => s.delete("session")).catch(() => void 0);
  }
  async function publicName() {
    const jwk = await crypto.subtle.exportKey("jwk", (await sessionKey()).publicKey);
    return `${jwk.x}.${jwk.y}`;
  }
  var b64url = (buf) => btoa(String.fromCharCode(...new Uint8Array(buf))).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  var proofNonce = "";
  var clockOffset = 0;
  async function proof(method, url, body) {
    const ts = String(Date.now() + clockOffset);
    const bodyHash = b64url(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(body)));
    const signed = `${method.toUpperCase()}
${url.pathname}${url.search}
${ts}
${proofNonce}
${bodyHash}`;
    const sig = await crypto.subtle.sign(
      { name: "ECDSA", hash: "SHA-256" },
      (await sessionKey()).privateKey,
      new TextEncoder().encode(signed)
    );
    return `${ts}.${proofNonce}.${b64url(sig)}`;
  }
  function remember(wallet, expiresAt) {
    try {
      localStorage.setItem(STORED, JSON.stringify({ wallet, address, session, expiresAt }));
    } catch {
    }
  }
  function recalled() {
    var _a2;
    try {
      const s = JSON.parse((_a2 = localStorage.getItem(STORED)) != null ? _a2 : "null");
      return s && s.session && s.address && s.expiresAt > Date.now() + 6e4 ? s : null;
    } catch {
      return null;
    }
  }
  function forget() {
    try {
      localStorage.removeItem(STORED);
    } catch {
    }
  }
  var walletKey = "";
  var _a;
  var SERVICE = (_a = window.EQUIVALENCE_SERVICE) != null ? _a : "/api";
  var CLIENT_BUILD = "1";
  async function send(path, init = {}, retried = false) {
    var _a2, _b, _c, _d;
    const url = new URL(`${SERVICE}${path}`, location.href);
    const method = ((_a2 = init.method) != null ? _a2 : "GET").toUpperCase();
    const body = typeof init.body === "string" ? init.body : "";
    const r = await fetch(url, {
      ...init,
      headers: {
        "content-type": "application/json",
        "x-client-build": CLIENT_BUILD,
        ...session ? { authorization: `Bearer ${session}`, "x-session-proof": await proof(method, url, body) } : {},
        ...(_b = init.headers) != null ? _b : {}
      }
    });
    const nonce = r.headers.get("x-proof-nonce");
    if (nonce) proofNonce = nonce;
    const date = Date.parse((_c = r.headers.get("date")) != null ? _c : "");
    if (!Number.isNaN(date)) clockOffset = date - Date.now();
    if (r.status === 401 && session && !retried) {
      const code = (_d = await r.clone().json().catch(() => ({}))) == null ? void 0 : _d.code;
      if (code === "proof_nonce" || code === "proof_clock" || code === "proof_missing") return send(path, init, true);
    }
    return r;
  }
  async function service(path, init) {
    var _a2, _b, _c, _d;
    const r = await send(path, init);
    if (!r.ok) {
      const text = await r.text().catch(() => r.statusText);
      let said = "";
      try {
        said = (_d = (_c = (_a2 = JSON.parse(text)) == null ? void 0 : _a2.message) != null ? _c : (_b = JSON.parse(text)) == null ? void 0 : _b.error) != null ? _d : "";
      } catch {
      }
      throw new Error(said && typeof said === "string" ? said : `${path} -> ${r.status} ${text}`);
    }
    return r;
  }
  var heldAt = 0;
  async function sessionHolds(forAddress) {
    const kept = recalled();
    if (!kept || !sameAddress(kept.address, forAddress)) return false;
    session = kept.session;
    if (Date.now() - heldAt < 3e4) return true;
    const r = await send("/auth/session").catch(() => null);
    if (r == null ? void 0 : r.ok) {
      heldAt = Date.now();
      return true;
    }
    heldAt = 0;
    session = "";
    if (r && r.status === 401) forget();
    return false;
  }
  var sameAddress = (a, b) => a.toLowerCase() === b.toLowerCase();
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
      walletKey = wallet;
      return address;
    },
    async resume() {
      var _a2, _b, _c;
      const kept = recalled();
      if (!kept) return "";
      const provider = (_a2 = window.cardano) == null ? void 0 : _a2[kept.wallet];
      if (!provider || typeof provider.isEnabled !== "function") return "";
      if (!await provider.isEnabled().catch(() => false)) return "";
      try {
        api = await provider.enable();
        const used = await api.getUsedAddresses();
        const unused = used.length ? [] : await api.getUnusedAddresses();
        const now = (_c = (_b = used[0]) != null ? _b : unused[0]) != null ? _c : "";
        if (!now || !sameAddress(now, kept.address)) {
          api = null;
          return "";
        }
        if (!await sessionHolds(now)) {
          api = null;
          return "";
        }
        address = now;
        walletKey = kept.wallet;
        return JSON.stringify({ wallet: kept.wallet, address });
      } catch {
        api = null;
        return "";
      }
    },
    async proveOwnership({ accountId }) {
      var _a2;
      if (!api) throw new Error("no wallet connected");
      if (await sessionHolds(accountId)) return "remembered";
      session = "";
      const ch = await service(`/auth/challenge?address=${encodeURIComponent(accountId)}&key=${encodeURIComponent(await publicName())}`);
      const { nonce, message } = await ch.json();
      const sig = await api.signData(accountId, hex(message));
      const proved = await service("/auth/prove", {
        method: "POST",
        body: JSON.stringify({ address: accountId, nonce, ...sig })
      });
      const issued = await proved.json();
      session = (_a2 = issued.session) != null ? _a2 : "";
      if (!session) throw new Error("the service issued no session");
      remember(walletKey, Number(issued.expiresAt) || Date.now());
      return sig.signature;
    },
    async disconnect() {
      api = null;
      address = "";
      session = "";
      walletKey = "";
      heldAt = 0;
      forget();
      await dropKey();
    },
    async signOutEverywhere() {
      try {
        if (session) await service("/auth/signout-everywhere", { method: "POST", body: "{}" });
      } finally {
        await connector.disconnect({});
      }
    },
    reload() {
      location.reload();
    },
    async remembered({ accountId }) {
      return await sessionHolds(accountId) ? "true" : "";
    },
    async request({ method, path, body }) {
      try {
        const r = await send(path, { method, ...body ? { body } : {} });
        return JSON.stringify({ status: r.status, body: await r.text(), service: !!r.headers.get("x-proof-nonce") });
      } catch (e) {
        return JSON.stringify({ status: 0, body: "", error: e.message });
      }
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
