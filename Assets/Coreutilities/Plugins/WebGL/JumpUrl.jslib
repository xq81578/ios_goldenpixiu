mergeInto(LibraryManager.library, {
  JumpUrl: function(url) {
  	//console.log("JumpUrl " + UTF8ToString(url));
	window.location.href = UTF8ToString(url);
  }
});