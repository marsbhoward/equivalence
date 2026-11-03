(() => {
  // ../connector/equivalence-chain.ts
  var LABEL_REFERENCE = "000643b0";
  var LABEL_USER = "000de140";
  var api = null;
  var address = "";
  var session = "";
  var KEY_DB = "equivalence-keys";
  var STORED = "equivalence.wallet";
  async function copyText(text) {
    try {
      await navigator.clipboard.writeText(text);
      return;
    } catch {
    }
    const area = document.createElement("textarea");
    area.value = text;
    area.setAttribute("readonly", "");
    area.style.position = "fixed";
    area.style.opacity = "0";
    document.body.appendChild(area);
    area.select();
    const ok = document.execCommand("copy");
    area.remove();
    if (!ok) throw new Error("The browser would not copy.");
  }
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
  var APP = "wallet-app";
  var mobile = () => /iPhone|iPad|iPod|Android/i.test(navigator.userAgent) || navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1;
  var inWalletApp = (url) => `web+cardano://browse/v1?uri=${encodeURIComponent(url)}`;
  var linkUrl = (code) => new URL(`${SERVICE}/link/?c=${code}`, location.href).href;
  var POLL_MS = 2e3;
  function linkPanel(opts, wait) {
    const style = document.createElement("style");
    style.textContent = `
    #eq-link { position: fixed; inset: 0; z-index: 20; display: flex; align-items: center; justify-content: center;
      background: rgba(5,5,8,.82); font: 15px/1.45 system-ui, -apple-system, sans-serif; color: #cfc6b4;
      padding: env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom) env(safe-area-inset-left); }
    #eq-link .p { width: min(560px, calc(100% - 32px)); max-height: 100%; overflow: auto; background: #0b0b10;
      border: 1px solid #3a3226; padding: 18px 20px; text-align: center; }
    #eq-link h2 { margin: 0 0 6px; font-size: 18px; font-weight: 600; letter-spacing: .06em; color: #e2c487; }
    #eq-link p { margin: 0 0 10px; }
    #eq-link .code { font: 600 28px/1.2 ui-monospace, Menlo, monospace; letter-spacing: .2em; color: #e2c487;
      border: 1px solid #3a3226; padding: 6px; margin: 6px auto 12px; max-width: 260px; }
    #eq-link .row { display: flex; gap: 8px; flex-wrap: wrap; justify-content: center; margin-bottom: 10px; }
    #eq-link a, #eq-link button { flex: 1 1 150px; font: inherit; letter-spacing: .05em; text-decoration: none;
      color: #cfc6b4; background: rgba(255,255,255,.03); border: 1px solid #3a3226; padding: 10px 12px; cursor: pointer; }
    #eq-link a.go { color: #0b0b10; background: #e2c487; border-color: #e2c487; font-weight: 600; }
    #eq-link .small { color: #8a8aa4; font-size: 13px; }
    #eq-link .status { min-height: 1.4em; color: #8a8aa4; font-size: 13px; margin: 0; }`;
    const root = document.createElement("div");
    root.id = "eq-link";
    const url = linkUrl(opts.code);
    root.innerHTML = `<div class="p" role="dialog" aria-modal="true">
      <h2></h2><p class="lead"></p><div class="code"></div>
      <div class="row"><a class="go">Open wallet app</a><button type="button" class="copy">Copy link</button></div>
      <p class="small"></p>
      <p class="status"></p>
      <div class="row"><button type="button" class="cancel">Cancel</button></div></div>`;
    const q = (sel) => root.querySelector(sel);
    q("h2").textContent = opts.title;
    q(".lead").textContent = opts.lead;
    q(".code").textContent = `${opts.code.slice(0, 3)}-${opts.code.slice(3)}`;
    q(".go").href = inWalletApp(url);
    q(".small").textContent = `Opens in Eternl or VESPR. Another wallet: copy the link into its browser, or go to ${new URL(url).host}/link there and type the code.`;
    q(".copy").addEventListener("click", () => {
      var _a2;
      (_a2 = navigator.clipboard) == null ? void 0 : _a2.writeText(url).then(() => {
        q(".copy").textContent = "Link copied";
      }, () => {
      });
    });
    for (const t of ["keydown", "keyup", "pointerdown", "touchstart", "wheel"]) root.addEventListener(t, (e) => e.stopPropagation());
    document.head.appendChild(style);
    document.body.appendChild(root);
    let cancelled = false;
    return new Promise((resolve, reject) => {
      q(".cancel").addEventListener("click", () => {
        cancelled = true;
        reject(new Error("cancelled"));
      });
      wait(() => cancelled, (s) => {
        q(".status").textContent = s;
      }).then(resolve, reject);
    }).finally(() => {
      root.remove();
      style.remove();
    });
  }
  function pollUntil(cancelled, expiresAt, ask) {
    return new Promise((resolve, reject) => {
      let timer = 0;
      let asking = false;
      const stop = () => {
        clearTimeout(timer);
        document.removeEventListener("visibilitychange", now);
      };
      const tick = async () => {
        if (cancelled()) {
          stop();
          return;
        }
        if (Date.now() > expiresAt) {
          stop();
          reject(new Error("the code expired - try again"));
          return;
        }
        if (asking) return;
        asking = true;
        try {
          const r = await ask();
          if (r !== void 0) {
            stop();
            resolve(r);
            return;
          }
        } catch (e) {
          stop();
          reject(e);
          return;
        } finally {
          asking = false;
        }
        timer = window.setTimeout(tick, POLL_MS);
      };
      const now = () => {
        if (document.visibilityState === "visible") {
          clearTimeout(timer);
          void tick();
        }
      };
      document.addEventListener("visibilitychange", now);
      void tick();
    });
  }
  async function linkSignIn() {
    const started = await (await service("/link/start", {
      method: "POST",
      body: JSON.stringify({ key: await publicName() })
    })).json();
    const done = JSON.parse(await linkPanel({
      title: "Connect your wallet app",
      lead: "Open the wallet app and sign in there - it checks this code. Signing is free and is not a transaction.",
      code: started.code
    }, (cancelled, status) => {
      status("Waiting for the wallet...");
      return pollUntil(cancelled, started.expiresAt, async () => {
        const r = await (await service(`/link/poll?code=${started.code}&poll=${encodeURIComponent(started.poll)}`)).json();
        if (r.state === "expired") throw new Error("the code expired - try again");
        return r.state === "done" ? JSON.stringify(r) : void 0;
      });
    }));
    session = done.session;
    if (!session) throw new Error("the service issued no session");
    return { address: done.address, expiresAt: Number(done.expiresAt) || Date.now() };
  }
  async function linkSign(cbor, what) {
    const req = await (await service("/link/sign", { method: "POST", body: JSON.stringify({ cbor, what }) })).json();
    return linkPanel({
      title: "Approve in your wallet app",
      lead: "The wallet shows exactly what this moves. Come back here when it is approved.",
      code: req.code
    }, (cancelled, status) => {
      status("Waiting for the wallet...");
      return pollUntil(cancelled, req.expiresAt, async () => {
        const r = await (await service(`/link/signed?code=${req.code}`)).json();
        if (r.state === "expired") throw new Error("the code expired - try again");
        if (r.state === "declined") throw new Error("declined in the wallet");
        return r.state === "signed" ? String(r.witness) : void 0;
      });
    });
  }
  var appApi = {
    signTx: () => Promise.reject(new Error("the wallet app signs through signTx()")),
    signData: () => Promise.reject(new Error("the wallet app signed in through its link"))
  };
  function signTx(cbor, what) {
    if (!api) throw new Error("no wallet connected");
    return walletKey === APP ? linkSign(cbor, what) : api.signTx(cbor, true);
  }
  var hex = (s) => Array.from(new TextEncoder().encode(s)).map((b) => b.toString(16).padStart(2, "0")).join("");
  var connector = {
    // ---- identity ---------------------------------------------------------------------------
    listWallets() {
      var _a2;
      const c = (_a2 = window.cardano) != null ? _a2 : {};
      const found = Object.keys(c).filter((k) => {
        var _a3;
        return typeof ((_a3 = c[k]) == null ? void 0 : _a3.enable) === "function";
      });
      if (!found.length && mobile()) found.push(APP);
      return found.join(",");
    },
    async connect({ wallet }) {
      var _a2, _b, _c;
      if (wallet === APP) {
        const linked = await linkSignIn();
        api = appApi;
        address = linked.address;
        walletKey = APP;
        heldAt = Date.now();
        remember(APP, linked.expiresAt);
        return address;
      }
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
      if (kept.wallet === APP) {
        if (!await sessionHolds(kept.address).catch(() => false)) return "";
        api = appApi;
        address = kept.address;
        walletKey = APP;
        return JSON.stringify({ wallet: APP, address });
      }
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
    async share({ text }) {
      const coarse = typeof matchMedia === "function" && matchMedia("(pointer: coarse)").matches;
      if (coarse && typeof navigator.share === "function") {
        try {
          await navigator.share({ text });
          return "shared";
        } catch (e) {
          if ((e == null ? void 0 : e.name) === "AbortError") return "cancelled";
        }
      }
      await copyText(text);
      return "copied";
    },
    async copy({ text }) {
      await copyText(text);
      return "copied";
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
    async listComponentItems({ accountId, policy, page, units }) {
      if (!accountId) return { items: [] };
      const q = units ? `units=${encodeURIComponent(units)}` : `policy=${encodeURIComponent(policy != null ? policy : "")}&page=${encodeURIComponent(page || "0")}`;
      const r = await service(`/components/items?address=${encodeURIComponent(accountId)}&${q}`);
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
      const witness = await signTx(built.cbor, "open-boxes");
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
    const witness = await signTx(cbor, endpoint.endsWith("/renew") ? "pfp-renew" : "pfp");
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
    const witness = await signTx(built.cbor, endpoint.includes("dyes") ? "dyes" : "break-boxes");
    const done = await service(`${endpoint}/submit`, {
      method: "POST",
      body: JSON.stringify({ burnId: built.burnId, witness })
    });
    return done.text();
  }
  window.EquivalenceChain = connector;
})();
