mergeInto(LibraryManager.library, {
  UpdateIFrame: function () {
    console.log("UpdateIFrame() called from Unity via .jslib");

    // 你可以直接對 window 做操作
    var iframe = document.querySelector('iframe');
    if (iframe) {
      iframe.setAttribute("allow", "camera; microphone");
      console.log("iframe allow set!");
    } else {
      console.warn("iframe not found");
    }
  }
});