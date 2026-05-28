mergeInto(LibraryManager.library, {
  ReloadPage: function() {
    window.location.reload();
  },
  GoBackPage: function() {
    try {
      if (window.history && window.history.length > 1) {
        window.history.back();
        return;
      }
    } catch (e) {
      console.warn("GoBackPage failed, reload current page instead.", e);
    }

    window.location.reload();
  }
});
