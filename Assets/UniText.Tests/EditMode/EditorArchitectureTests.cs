using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace LightSide.Tests
{
    /// <summary>Guards the shared serialization lifecycle required by custom editor UI.</summary>
    public sealed class EditorArchitectureTests
    {
        private static readonly string[] EditorRoots =
        {
            "LightSide.Core/Editor",
            "UniText/Editor",
            "UniShapes/Editor",
            "UniLottie/Editor"
        };

        /// <summary>Ensures inspector code cannot bypass the shared serialized-field lifecycle.</summary>
        [Test]
        public void CustomEditorCodeUsesSharedSerializedLifecycle()
        {
            var offenders = new HashSet<string>();
            var canonical = Path.GetFullPath(Path.Combine(
                Application.dataPath, "LightSide.Core/Editor/SerializedPropertyField.cs"));

            for (var i = 0; i < EditorRoots.Length; i++)
            {
                var root = Path.Combine(Application.dataPath, EditorRoots[i]);
                foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (Path.GetFullPath(file) == canonical) continue;
                    var source = File.ReadAllText(file);
                    if (source.Contains("TrackPropertyValue") ||
                        source.Contains("SerializedPropertyField.Configure"))
                        offenders.Add(file);
                    if ((source.Contains("[CustomEditor") || source.Contains("[CustomPropertyDrawer")) &&
                        source.Contains("ApplyModifiedProperties"))
                        offenders.Add(file);
                }
            }

            Assert.That(offenders, Is.Empty,
                "Custom editor fields must use SerializedPropertyField.Create, Bind, Observe, or OnChange.");
        }
    }
}
