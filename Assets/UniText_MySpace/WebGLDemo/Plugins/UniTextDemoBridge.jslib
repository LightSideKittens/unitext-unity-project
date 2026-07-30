// Bridge from Unity → JS for the BasicUsageExampleWebGL sample.
//
// Forwards [DllImport("__Internal")] calls to the host page, which exposes
// `window.__uniTextDemoBridge.emit(event, payload)` while the WebGL canvas
// is mounted (see sites/unity/src/components/UnityWebGLPlayer.tsx in the
// monorepo).

mergeInto(LibraryManager.library, {

  $UniTextDemoEmit: function(event, payload) {
    var bridge = (typeof window !== 'undefined') ? window.__uniTextDemoBridge : null;
    if (bridge && typeof bridge.emit === 'function') bridge.emit(event, payload);
  },

  UniTextDemo_EmitTextChanged__deps: ['$UniTextDemoEmit'],
  UniTextDemo_EmitTextChanged: function(textPtr) {
    UniTextDemoEmit('textChanged', UTF8ToString(textPtr));
  },

  UniTextDemo_EmitFontLoaded__deps: ['$UniTextDemoEmit'],
  UniTextDemo_EmitFontLoaded: function(labelPtr) {
    UniTextDemoEmit('fontLoaded', UTF8ToString(labelPtr));
  },

  UniTextDemo_EmitFontError__deps: ['$UniTextDemoEmit'],
  UniTextDemo_EmitFontError: function(messagePtr) {
    UniTextDemoEmit('fontError', UTF8ToString(messagePtr));
  }

});
