/*
 * THE HOME SCREEN HINT (2026-11-01). Safari on an iPhone gives a page no way to hide its address
 * bar - no Fullscreen API, no minimal-ui - so the only way to play without it is from a Home Screen
 * icon (manifest.webmanifest, apple-mobile-web-app-capable). The page cannot add the icon itself,
 * so iOS Safari players get a small note saying how, until they close it.
 *
 * Not shown: off iOS, already on the Home Screen (navigator.standalone), in another iOS browser or
 * a wallet's in-app browser (no "Safari/" in the agent, or CriOS/FxiOS/EdgiOS), or for a fortnight
 * after the close button. Storage can throw (private mode): the note then just shows again.
 */
(function () {
  var KEY = 'equivalence.homeHint', QUIET_DAYS = 14, FADE_AFTER_MS = 20000;
  var ua = navigator.userAgent || '';
  var ios = /iPhone|iPad|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  var safari = /Safari\//.test(ua) && !/CriOS|FxiOS|EdgiOS|OPiOS/.test(ua);
  var installed = navigator.standalone === true || matchMedia('(display-mode: standalone)').matches
                  || matchMedia('(display-mode: fullscreen)').matches;
  if (!ios || !safari || installed) return;
  try {
    var closed = +localStorage.getItem(KEY) || 0;
    if (Date.now() - closed < QUIET_DAYS * 864e5) return;
  } catch (e) { /* no storage: show it */ }

  var el = document.getElementById('home-hint');
  if (!el) return;
  el.hidden = false;
  requestAnimationFrame(function () { el.classList.add('shown'); });

  function hide() { el.classList.remove('shown'); setTimeout(function () { el.hidden = true; }, 400); }
  el.querySelector('button').addEventListener('click', function () {
    try { localStorage.setItem(KEY, String(Date.now())); } catch (e) { }
    hide();
  });
  setTimeout(hide, FADE_AFTER_MS);
})();
