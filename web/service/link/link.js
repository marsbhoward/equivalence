/*
 * THE DEVICE LINK PAGE (web/service/src/link.ts). Opened inside a wallet app's own browser - or on
 * a computer with a wallet extension - to sign for a game running somewhere the wallet cannot reach.
 * It makes exactly one wallet call per visit: signData to sign the game in, or signTx (partial) to
 * approve a transaction the service built. Whatever it earns goes to the service, never to this page.
 */
(function () {
  'use strict';

  var $ = function (id) { return document.getElementById(id); };
  var WHAT = {
    'open-boxes': 'Open boxes',
    'pfp': 'Take the portrait',
    'pfp-renew': 'Renew the portrait',
    'dyes': 'Deposit dyes',
    'break-boxes': 'Break boxes',
  };

  function say(title, lead, note, bad) {
    $('title').textContent = title;
    $('lead').textContent = lead || '';
    $('note').textContent = note || '';
    $('note').classList.toggle('bad', !!bad);
  }

  var shown = function (code) { return code.slice(0, 3) + '-' + code.slice(3); };

  function api(path, init) {
    init = init || {};
    return fetch(path, {
      method: init.method || 'GET',
      headers: { 'content-type': 'application/json' },
      body: init.body ? JSON.stringify(init.body) : undefined,
    }).then(function (r) {
      return r.json().catch(function () { return {}; }).then(function (j) {
        if (!r.ok) throw new Error(j.error || ('the service answered ' + r.status));
        return j;
      });
    });
  }

  var hex = function (s) {
    return Array.from(new TextEncoder().encode(s), function (b) { return b.toString(16).padStart(2, '0'); }).join('');
  };

  /** CIP-30 wallets on this page - an in-app browser can inject a moment after load. */
  function wallets() {
    var found = function () {
      var c = window.cardano || {};
      return Object.keys(c).filter(function (k) { return c[k] && typeof c[k].enable === 'function'; })
        .map(function (k) { return { key: k, name: c[k].name || k, provider: c[k] }; });
    };
    return new Promise(function (resolve) {
      var tries = 0;
      (function look() {
        var w = found();
        if (w.length || ++tries > 10) resolve(w);
        else setTimeout(look, 200);
      })();
    });
  }

  function noWallet() {
    $('note').textContent = '';
    $('lead').textContent = 'No Cardano wallet on this page. Open this link in your wallet app\'s browser ' +
      '(Eternl, VESPR and others have one), or on a computer with a wallet extension.';
    $('copy').hidden = false;
  }

  $('copy').addEventListener('click', function () {
    var done = function () { $('copy').textContent = 'Link copied'; };
    if (navigator.clipboard) navigator.clipboard.writeText(location.href).then(done, function () { });
  });

  function buttons(list, label, act) {
    var box = $('wallets');
    box.textContent = '';
    list.forEach(function (w, i) {
      var b = document.createElement('button');
      b.type = 'button';
      if (i === 0) b.className = 'primary';
      b.textContent = label + ' ' + w.name.charAt(0).toUpperCase() + w.name.slice(1);
      b.addEventListener('click', function () {
        Array.prototype.forEach.call(box.children, function (x) { x.disabled = true; });
        act(w).catch(function (e) {
          $('note').textContent = (e && (e.info || e.message)) || 'The wallet said no.';
          $('note').classList.add('bad');
          Array.prototype.forEach.call(box.children, function (x) { x.disabled = false; });
        });
      });
      box.appendChild(b);
    });
  }

  function firstAddress(cip30) {
    return cip30.getUsedAddresses().then(function (used) {
      if (used.length) return used[0];
      return cip30.getUnusedAddresses().then(function (unused) { return unused[0] || ''; });
    });
  }

  function allAddresses(cip30) {
    return Promise.all([
      cip30.getUsedAddresses().catch(function () { return []; }),
      cip30.getUnusedAddresses().catch(function () { return []; }),
      cip30.getChangeAddress().then(function (a) { return [a]; }).catch(function () { return []; }),
    ]).then(function (lists) {
      return [].concat.apply([], lists).map(function (a) { return String(a).toLowerCase(); });
    });
  }

  function finished(title) {
    $('wallets').textContent = '';
    say(title, 'Go back to the game - it carries on by itself. You can close this page.');
  }

  function signIn(code, list) {
    say('Sign in to your game',
      'Only continue if this exact code is on your own game screen right now. Signing is free and is not a transaction.');
    buttons(list, 'Sign in with', function (w) {
      $('note').textContent = 'Check your wallet...';
      $('note').classList.remove('bad');
      return w.provider.enable().then(function (cip30) {
        return firstAddress(cip30).then(function (address) {
          if (!address) throw new Error('The wallet shared no address.');
          return api('/link/challenge?code=' + encodeURIComponent(code) + '&address=' + encodeURIComponent(address))
            .then(function (ch) {
              return cip30.signData(address, hex(ch.message)).then(function (sig) {
                return api('/link/prove', { method: 'POST', body: {
                  code: code, address: address, nonce: ch.nonce, signature: sig.signature, key: sig.key } });
              });
            });
        });
      }).then(function () { finished('Signed in'); });
    });
  }

  function signTx(code, req, list) {
    say(WHAT[req.what] || 'Approve',
      'Your game asks your wallet to approve this transaction. The wallet shows exactly what it moves.');
    buttons(list, 'Review in', function (w) {
      $('note').textContent = 'Check your wallet...';
      $('note').classList.remove('bad');
      return w.provider.enable().then(function (cip30) {
        return allAddresses(cip30).then(function (mine) {
          if (mine.indexOf(String(req.address).toLowerCase()) < 0)
            throw new Error('This wallet is not the one signed in to the game. Open this page in that wallet.');
          return cip30.signTx(req.cbor, true).then(function (witness) {
            return api('/link/witness', { method: 'POST', body: { code: code, witness: witness } });
          }, function (e) {
            // Declined in the wallet: the game hears it at once rather than waiting out the code.
            return api('/link/witness', { method: 'POST', body: { code: code, declined: true } })
              .catch(function () { }).then(function () { throw e; });
          });
        });
      }).then(function () { finished('Approved'); });
    });
  }

  function open(code) {
    $('enter').hidden = true;
    say('Wallet', 'Looking up the code...');
    api('/link/req?code=' + encodeURIComponent(code)).then(function (req) {
      $('code-shown').textContent = shown(req.code);
      $('code-shown').hidden = false;
      if (req.done) { finished('Already done'); return; }
      return wallets().then(function (list) {
        if (req.kind === 'signin') signIn(req.code, list); else signTx(req.code, req, list);
        if (!list.length) noWallet();
      });
    }).catch(function (e) {
      say('Wallet', '', e.message, true);
      $('enter').hidden = false;
    });
  }

  $('enter').addEventListener('submit', function (e) {
    e.preventDefault();
    var code = $('code').value.toUpperCase().replace(/[^A-Z0-9]/g, '');
    if (code.length !== 6) { say('Wallet', '', 'The code is six letters and numbers.', true); return; }
    history.replaceState(null, '', '?c=' + code);
    open(code);
  });

  var code = (new URLSearchParams(location.search).get('c') || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
  if (code) open(code);
  else {
    say('Connect your wallet', 'Type the code your game is showing.');
    $('enter').hidden = false;
  }
})();
