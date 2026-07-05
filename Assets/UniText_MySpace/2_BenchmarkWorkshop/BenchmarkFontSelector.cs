using System;
using System.Collections.Generic;
using LightSide;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns a toggle per font pair and assigns the selected pair to the UniText and TMP
/// benchmark text objects, so both glyph-rasterization benchmarks run on the same font.
/// </summary>
public class BenchmarkFontSelector : MonoBehaviour
{
    [Serializable]
    public struct BenchmarkFontPair
    {
        public UniTextFont uniTextFont;
        public TMP_FontAsset tmpFont;
    }

    public List<BenchmarkFontPair> fonts = new();
    public Toggle togglePrefab;
    public ToggleGroup content;

    void Start()
    {
        for (int i = 0; i < fonts.Count; i++)
        {
            var toggle = Instantiate(togglePrefab, content.transform);
            toggle.group = content;

            var label = toggle.GetComponentInChildren<TMP_Text>(true);
            string name = fonts[i].uniTextFont != null ? fonts[i].uniTextFont.name
                : fonts[i].tmpFont != null ? fonts[i].tmpFont.name
                : $"Font {i}";
            if (label != null) label.text = name;
            else
            {
                var legacyLabel = toggle.GetComponentInChildren<Text>(true);
                if (legacyLabel != null) legacyLabel.text = name;
            }

            int index = i;
            toggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) Apply(fonts[index]);
            });
            toggle.SetIsOnWithoutNotify(i == 0);
        }

        if (fonts.Count > 0)
            Apply(fonts[0]);
    }

    void Apply(BenchmarkFontPair pair)
    {
        if (pair.uniTextFont != null)
        {
            var uniBench = ObjectUtils.FindAny<UniText_GlyphRasterizationBenchmark>();
            if (uniBench != null)
                foreach (var text in uniBench.GetComponentsInChildren<UniText>(true))
                    text.Font = pair.uniTextFont;
        }

        if (pair.tmpFont != null)
        {
            var tmpBench = ObjectUtils.FindAny<TMP_GlyphRasterizationBenchmark>();
            if (tmpBench != null)
                foreach (var text in tmpBench.GetComponentsInChildren<TMP_Text>(true))
                    text.font = pair.tmpFont;
        }
    }
}
