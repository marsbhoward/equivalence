// The portrait booth's picture, handed to the player (UI/BoothScreen -> UI/PictureSaver).
//
// A phone gets the system SHARE sheet with the PNG as a file (save to photos, send it on); anything
// without a file share - a desktop browser - downloads it. Both run from Unity's frame loop a moment
// after the tap, inside the browser's user-activation window, which a share needs.

mergeInto(LibraryManager.library, {

  EquivalenceSavePicture: function (dataPtr, length, namePtr) {
    var name = UTF8ToString(namePtr);
    var bytes = HEAPU8.slice(dataPtr, dataPtr + length);
    var blob = new Blob([bytes], { type: 'image/png' });

    var download = function () {
      var url = URL.createObjectURL(blob);
      var a = document.createElement('a');
      a.href = url;
      a.download = name;
      a.style.display = 'none';
      document.body.appendChild(a);
      a.click();
      setTimeout(function () { a.remove(); URL.revokeObjectURL(url); }, 4000);
    };

    var touch = navigator.maxTouchPoints > 0 || 'ontouchstart' in window;
    try {
      var file = new File([blob], name, { type: 'image/png' });
      if (touch && navigator.canShare && navigator.canShare({ files: [file] })) {
        // A refused or cancelled share is the player's answer, not a reason to download instead.
        navigator.share({ files: [file], title: 'Equivalence' }).catch(function () {});
        return;
      }
    } catch (e) { /* no File constructor or no share: download */ }
    download();
  },
});
