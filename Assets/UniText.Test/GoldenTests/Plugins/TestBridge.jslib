mergeInto(LibraryManager.library, {
    ReportTestResults: function(jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        var results = JSON.parse(json);

        window.unityTestResults = {
            xml: results.xml,
            allPassed: results.allPassed,
            total: results.total,
            passed: results.passed,
            failed: results.failed
        };
        window.unityTestsComplete = true;

        console.log('[UniText Tests] Complete:', results.passed + '/' + results.total + ' passed');

        if (!results.allPassed) {
            console.error('[UniText Tests] Some tests failed!');
        }
    },

    AddTestScreenshot: function(namePtr, base64Ptr) {
        var name = UTF8ToString(namePtr);
        var base64 = UTF8ToString(base64Ptr);

        window.unityTestScreenshots = window.unityTestScreenshots || [];
        window.unityTestScreenshots.push({ name: name, data: base64 });

        console.log('[UniText Tests] Screenshot added:', name);
    }
});
