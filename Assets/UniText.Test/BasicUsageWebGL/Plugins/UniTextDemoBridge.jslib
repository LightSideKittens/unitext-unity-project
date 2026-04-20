// Bridge from Unity → JS for the BasicUsageExampleWebGL sample.
//
// Forwards [DllImport("__Internal")] calls to the host page, which exposes
// `window.__uniTextDemoBridge.emit(event, payload)` while the WebGL canvas
// is mounted (see sites/unity/src/components/UnityWebGLPlayer.tsx in the
// monorepo).

mergeInto(LibraryManager.library, {

  UniTextDemo_EmitTextChanged: function(textPtr) {
    var text = UTF8ToString(textPtr);
    var bridge = (typeof window !== 'undefined') ? window.__uniTextDemoBridge : null;
    if (bridge && typeof bridge.emit === 'function') {
      bridge.emit('textChanged', text);
    }
  }

});
