mergeInto(LibraryManager.library, {
  // vibratePattern: array of [vibrate, pause, vibrate, pause, ...] durations in ms
  VibrateDevice: function(vibratePattern, patternLength) {
    if (navigator && navigator.vibrate) {
      var pattern = [];
      for (var i = 0; i < patternLength; i++) {
        pattern.push(getValue(vibratePattern + i * 4, 'i32'));
      }
      navigator.vibrate(pattern);
    }
  },

  VibrateDeviceSimple: function(durationMs) {
    if (navigator && navigator.vibrate) {
      navigator.vibrate(durationMs);
    }
  },

  VibrateDeviceStop: function() {
    if (navigator && navigator.vibrate) {
      navigator.vibrate(0);
    }
  }
});
