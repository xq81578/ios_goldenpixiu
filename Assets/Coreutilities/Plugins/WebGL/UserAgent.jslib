mergeInto(LibraryManager.library, {
    GetUserAgent: function () {
        var ua = navigator.userAgent;
        var lengthBytes = lengthBytesUTF8(ua) + 1;
        var stringOnWasmHeap = _malloc(lengthBytes);
        stringToUTF8(ua, stringOnWasmHeap, lengthBytes);
        return stringOnWasmHeap;
    }
});
