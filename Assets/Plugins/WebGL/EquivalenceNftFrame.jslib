// A DONADA piece's live page over the game's canvas, for the hub's close-up view ONLY (Hub/NftFrame).
//
// One iframe at most, created on show and removed on hide. It is SANDBOXED to scripts alone (no
// allow-same-origin): the page runs in an opaque origin and cannot reach this page, its storage or
// the wallet session - and the service serves it under the same sandbox and a no-network policy
// (web/service/src/routes/nft.ts), so it holds even if this attribute were ever lost.
//
// The rect is normalised to the canvas (0..1, origin bottom-left, as Unity's screen space), so it
// follows the canvas wherever the page lays it out, and is re-placed when the window resizes.

mergeInto(LibraryManager.library, {

  EquivalenceNftShow: function (urlPtr, x, y, w, h) {
    var url = UTF8ToString(urlPtr);
    var canvas = (typeof Module !== 'undefined' && Module.canvas) ? Module.canvas : document.querySelector('canvas');
    if (!canvas) return;

    var st = window.__equivalenceNft;
    if (st && st.frame) { st.frame.remove(); window.removeEventListener('resize', st.place); }

    var f = document.createElement('iframe');
    f.setAttribute('sandbox', 'allow-scripts');
    f.setAttribute('referrerpolicy', 'no-referrer');
    f.setAttribute('title', 'The piece, live');
    f.style.position = 'fixed';
    f.style.border = '0';
    f.style.zIndex = '10';
    f.style.background = '#07060a';
    f.src = url;

    var place = function () {
      var r = canvas.getBoundingClientRect();
      f.style.left = (r.left + x * r.width) + 'px';
      f.style.top = (r.top + (1 - y - h) * r.height) + 'px';
      f.style.width = (w * r.width) + 'px';
      f.style.height = (h * r.height) + 'px';
    };
    place();
    document.body.appendChild(f);
    window.addEventListener('resize', place);
    window.__equivalenceNft = { frame: f, place: place };
  },

  EquivalenceNftHide: function () {
    var st = window.__equivalenceNft;
    if (!st) return;
    if (st.frame) st.frame.remove();
    window.removeEventListener('resize', st.place);
    window.__equivalenceNft = null;
    // Keys go back to the game - a click in the piece had taken focus into the iframe.
    var canvas = (typeof Module !== 'undefined' && Module.canvas) ? Module.canvas : document.querySelector('canvas');
    if (canvas && canvas.focus) canvas.focus();
  }
});
