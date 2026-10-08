// The C# <-> TypeScript seam for the browser build.
//
// Unity's JS interop is SYNCHRONOUS C# -> JS and cannot return a promise, so every call is
// fire-and-forget with a request id, and the answer comes back through SendMessage. That is
// the whole reason WebChainBridge keeps a TaskCompletionSource per id: it converts this
// one-way pattern back into the `async Task` shape IProfileStore already declares, so no
// gameplay code has to know the store is now a browser round trip.
//
// The page supplies window.EquivalenceChain. See web/connector/equivalence-chain.d.ts for the
// contract; anything not implemented there fails loudly rather than silently resolving.

mergeInto(LibraryManager.library, {

  EquivalenceChainAvailable: function () {
    return (typeof window !== 'undefined' && window.EquivalenceChain) ? 1 : 0;
  },

  EquivalenceChainCall: function (reqIdPtr, methodPtr, payloadPtr) {
    var reqId   = UTF8ToString(reqIdPtr);
    var method  = UTF8ToString(methodPtr);
    var payload = UTF8ToString(payloadPtr);

    // Unity has moved SendMessage between the global scope and Module across versions, and a
    // bridge that resolves nothing is indistinguishable from a chain that never answers - so
    // check both and say so loudly if neither is there.
    function send(json) {
      var fn = (typeof SendMessage === 'function') ? SendMessage
             : (typeof Module !== 'undefined' && Module.SendMessage) ? Module.SendMessage
             : null;
      if (!fn) { console.error('[EquivalenceChain] no SendMessage available; request ' + reqId + ' will time out'); return; }
      fn('EquivalenceChainBridge', 'OnChainResult', json);
    }

    function ok(value) {
      var s;
      if (value === undefined || value === null) s = '';
      else if (typeof value === 'string') s = value;
      else s = JSON.stringify(value);           // an object result is carried as JSON, not "[object Object]"
      send(JSON.stringify({ id: reqId, ok: true, value: s }));
    }

    function fail(err) {
      var msg = (err && err.message) ? err.message : String(err);
      send(JSON.stringify({ id: reqId, ok: false, error: msg }));
    }

    try {
      var api = window.EquivalenceChain;
      if (!api) { fail('no connector: window.EquivalenceChain is undefined'); return; }

      var fn = api[method];
      if (typeof fn !== 'function') { fail('connector has no method "' + method + '"'); return; }

      var args = {};
      if (payload) {
        try { args = JSON.parse(payload); }
        catch (e) { fail('bad payload: ' + e.message); return; }
      }

      // Promise.resolve so a connector may implement any method synchronously.
      Promise.resolve(fn.call(api, args)).then(ok).catch(fail);
    } catch (e) {
      fail(e);
    }
  }
});
