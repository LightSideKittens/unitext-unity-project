using UnityEditor;
using UnityEngine.UIElements;

namespace LightSide.Benchmark
{
    /// <summary>
    /// Inspector for <see cref="BenchmarkRunner"/>: one launch button per suite found in the loaded
    /// scenes, discovered rather than named, so a project's own suites appear without touching this.
    /// </summary>
    [SingleObjectEditor]
    [CustomEditor(typeof(BenchmarkRunner))]
    internal sealed class BenchmarkRunnerEditor : FullWidthEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            var root = InspectorVisuals.CreateRoot();
            root.Add(SerializedPropertyField.Create(serializedObject, "submodulePath"));

            var section = InspectorVisuals.CreateSection("Suites");
            root.Add(section);

            var suites = BenchmarkRunner.DiscoverSuites();
            if (suites.Count == 0)
            {
                section.Add(new HelpBox(
                    "No benchmark suite component is present in the loaded scenes.",
                    HelpBoxMessageType.Info));
                return root;
            }

            if (!EditorApplication.isPlaying)
                section.Add(new HelpBox(
                    "A run is a coroutine: enter Play Mode to start one.",
                    HelpBoxMessageType.Info));

            var runner = (BenchmarkRunner)target;
            var buttons = InspectorVisuals.CreateStack();
            section.Add(buttons);

            buttons.Add(new Button(runner.RunFromMenu) { text = "Run All" });
            foreach (var suite in suites)
            {
                var id = suite.SuiteId;
                buttons.Add(new Button(() => runner.RunOnly(id)) { text = $"Run {id}" });
            }

            buttons.SetEnabled(EditorApplication.isPlaying);
            return root;
        }
    }
}
