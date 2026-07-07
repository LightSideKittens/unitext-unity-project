using System.Text.RegularExpressions;
using LightSide;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UniText.Tests
{
    /// <summary>
    /// Contract tests for the v2.x → v3 serialized-data migrations, driven through real Unity YAML shapes
    /// captured from released scenes. Every migration must also be a byte-exact no-op on non-matching input.
    /// </summary>
    public class AssetMigrationTests
    {
        static string Run(IMigration migration, string yaml)
        {
            var documents = UnityYaml.ParseDocuments(yaml);
            var edit = new YamlEdit(yaml);
            migration.Migrate(new MigrationContext("Assets/Test.unity", documents, edit, null));
            return edit.Apply();
        }

        const string Header = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n";

        const string ComponentDefaultHighlighter = Header +
@"--- !u!114 &628271374
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: beaa34cb0e58d624bb3a264b28600785, type: 3}
  text: hello
  highlighter:
    rid: 6670084077323812928
  references:
    version: 2
    RefIds:
    - rid: 6670084077323812928
      type: {class: DefaultTextHighlighter, ns: LightSide, asm: LightSide.UniText}
      data:
        clickColor: {r: 0.2, g: 0.5, b: 1, a: 0.6}
        fadeDuration: 0.25
        hoverColor: {r: 0.2, g: 0.5, b: 1, a: 0.1}
";

        [Test]
        public void Highlighter_ComponentLevel_Default_IsRemoved()
        {
            var result = Run(new HighlighterToStylerMigration(), ComponentDefaultHighlighter);

            StringAssert.DoesNotContain("highlighter", result);
            StringAssert.DoesNotContain("DefaultTextHighlighter", result);
            StringAssert.DoesNotContain("clickColor", result);
            StringAssert.Contains("text: hello", result);
        }

        [Test]
        public void Highlighter_ComponentLevel_Custom_IsRemovedAndLogged()
        {
            var yaml = ComponentDefaultHighlighter.Replace("a: 0.6", "a: 0.9");

            LogAssert.Expect(LogType.Warning, new Regex("custom colours"));
            var result = Run(new HighlighterToStylerMigration(), yaml);

            StringAssert.DoesNotContain("DefaultTextHighlighter", result);
        }

        const string ModifierHighlighter = Header +
@"--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: beaa34cb0e58d624bb3a264b28600785, type: 3}
  styles:
    items:
    - rid: 100
  references:
    version: 2
    RefIds:
    - rid: 100
      type: {class: LinkModifier, ns: LightSide, asm: LightSide.UniText}
      data:
        autoOpenUrl: 1
        highlighter:
          rid: 200
    - rid: 200
      type: {class: DefaultTextHighlighter, ns: LightSide, asm: LightSide.UniText}
      data:
        clickColor: {r: 1, g: 0, b: 0, a: 0.5}
        fadeDuration: 0.5
        hoverColor: {r: 0, g: 1, b: 0, a: 0.2}
";

        [Test]
        public void Highlighter_OnModifier_Custom_BecomesStylerWithMappedColours()
        {
            var result = Run(new HighlighterToStylerMigration(), ModifierHighlighter);

            StringAssert.Contains("styler:", result);
            StringAssert.DoesNotContain("highlighter", result);
            StringAssert.Contains("class: StateHighlightStyler", result);
            StringAssert.Contains("pressed:", result);
            StringAssert.Contains("color: {r: 255, g: 0, b: 0, a: 128}", result);
            StringAssert.Contains("color: {r: 0, g: 255, b: 0, a: 51}", result);
            StringAssert.Contains("activatedFlash: 0.5", result);
            StringAssert.Contains("rid: 200", result);
        }

        [Test]
        public void Highlighter_OnModifier_Default_BecomesStylerWithDefaultData()
        {
            var yaml = ModifierHighlighter
                .Replace("clickColor: {r: 1, g: 0, b: 0, a: 0.5}", "clickColor: {r: 0.2, g: 0.5, b: 1, a: 0.6}")
                .Replace("fadeDuration: 0.5", "fadeDuration: 0.25")
                .Replace("hoverColor: {r: 0, g: 1, b: 0, a: 0.2}", "hoverColor: {r: 0.2, g: 0.5, b: 1, a: 0.1}");

            var result = Run(new HighlighterToStylerMigration(), yaml);

            StringAssert.Contains("class: StateHighlightStyler", result);
            StringAssert.Contains("styler:", result);
            StringAssert.DoesNotContain("clickColor", result);
            StringAssert.DoesNotContain("pressed:", result);
        }

        [Test]
        public void Highlighter_Migration_IsIdempotent()
        {
            var once = Run(new HighlighterToStylerMigration(), ModifierHighlighter);
            var twice = Run(new HighlighterToStylerMigration(), once);
            Assert.AreEqual(once, twice);
        }

        [Test]
        public void Highlighter_NonMatchingDocument_IsUntouched()
        {
            var yaml = ModifierHighlighter.Replace("DefaultTextHighlighter", "UserHighlighter");
            var result = Run(new HighlighterToStylerMigration(), yaml);
            Assert.AreEqual(yaml, result);
        }

        const string InlineProvider = Header +
@"--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: beaa34cb0e58d624bb3a264b28600785, type: 3}
  references:
    version: 2
    RefIds:
    - rid: 300
      type: {class: InlineGradientProvider, ns: LightSide, asm: LightSide.UniText}
      data:
        entries:
          items:
          - name: rainbow
            gradient:
              serializedVersion: 2
              key0: {r: 1, g: 0, b: 0, a: 1}
              key1: {r: 0, g: 0, b: 1, a: 1}
              key2: {r: 0, g: 0, b: 0, a: 0}
              key3: {r: 0, g: 0, b: 0, a: 0}
              key4: {r: 0, g: 0, b: 0, a: 0}
              key5: {r: 0, g: 0, b: 0, a: 0}
              key6: {r: 0, g: 0, b: 0, a: 0}
              key7: {r: 0, g: 0, b: 0, a: 0}
              ctime0: 0
              ctime1: 65535
              ctime2: 0
              ctime3: 0
              ctime4: 0
              ctime5: 0
              ctime6: 0
              ctime7: 0
              atime0: 0
              atime1: 65535
              atime2: 0
              atime3: 0
              atime4: 0
              atime5: 0
              atime6: 0
              atime7: 0
              m_Mode: 0
              m_NumColorKeys: 2
              m_NumAlphaKeys: 2
";

        [Test]
        public void InlineGradientProvider_BecomesInlinePaintProviderWithSwatches()
        {
            var result = Run(new InlineGradientProviderMigration(), InlineProvider);

            StringAssert.Contains("class: InlinePaintProvider", result);
            StringAssert.DoesNotContain("InlineGradientProvider", result);
            StringAssert.DoesNotContain("key0", result);
            StringAssert.Contains("- name: rainbow", result);
            StringAssert.Contains("kind: 1", result);
            StringAssert.Contains("stops:", result);
            StringAssert.Contains("- time: 0", result);
            StringAssert.Contains("- time: 1", result);
            StringAssert.Contains("color: {r: 1, g: 0, b: 0, a: 1}", result);
            StringAssert.Contains("color: {r: 0, g: 0, b: 1, a: 1}", result);
            StringAssert.Contains("interpolation: 0", result);
        }

        [Test]
        public void InlineGradientProvider_Migration_IsIdempotent()
        {
            var once = Run(new InlineGradientProviderMigration(), InlineProvider);
            var twice = Run(new InlineGradientProviderMigration(), once);
            Assert.AreEqual(once, twice);
        }

        const string MultilineQuotedText = Header +
@"--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: beaa34cb0e58d624bb3a264b28600785, type: 3}
  text: 'Figma

    Converter''s

    Unity'
  wrapped: ""line \""one\""

    line two""
  highlighter:
    rid: 200
  references:
    version: 2
    RefIds:
    - rid: 200
      type: {class: DefaultTextHighlighter, ns: LightSide, asm: LightSide.UniText}
      data:
        clickColor: {r: 0.2, g: 0.5, b: 1, a: 0.6}
        fadeDuration: 0.25
        hoverColor: {r: 0.2, g: 0.5, b: 1, a: 0.1}
";

        [Test]
        public void Parser_MultilineQuotedScalars_DoNotDerailTheDocument()
        {
            var result = Run(new HighlighterToStylerMigration(), MultilineQuotedText);

            StringAssert.DoesNotContain("DefaultTextHighlighter", result);
            StringAssert.DoesNotContain("highlighter", result);
            StringAssert.Contains("Converter''s", result);
            StringAssert.Contains("line two", result);
        }

        [Test]
        public void GradientProviders_AreRenamedInPlace()
        {
            var yaml = Header +
@"--- !u!114 &1
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: beaa34cb0e58d624bb3a264b28600785, type: 3}
  references:
    version: 2
    RefIds:
    - rid: 400
      type: {class: GlobalSettingsGradientProvider, ns: LightSide, asm: LightSide.UniText}
      data:
";
            var result = Run(new GlobalGradientToPaintProviderMigration(), yaml);
            StringAssert.Contains("class: GlobalSettingsPaintProvider", result);
            StringAssert.DoesNotContain("GradientProvider", result);
        }
    }
}
