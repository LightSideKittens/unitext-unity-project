using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LightSide.Promo
{
    /// <summary>Transport and scrubber for a <see cref="Reel"/>.</summary>
    /// <remarks>
    /// Scrubbing is how a shot is authored: a timing is judged by holding the frame it lands on, not by watching it
    /// go past. The scrubber works only because <see cref="Reel.Seek"/> is a pure function of time.
    /// </remarks>
    [CustomEditor(typeof(Reel))]
    [SingleObjectEditor]
    internal sealed class ReelEditor : UnityEditor.Editor
    {
        private Reel reel;
        private InspectorSliderInt scrubber;
        private Label readout;

        public override VisualElement CreateInspectorGUI()
        {
            reel = (Reel)target;

            var root = InspectorVisuals.CreateRoot();
            root.Add(InspectorVisuals.AlignFields(BuildFields()));

            var section = InspectorVisuals.CreateRailSection("Transport", "LightSide.Promo.Transport");

            var row = InspectorVisuals.CreateRow();
            row.Add(new InspectorPillButton(() => SetPlaying(true)) { text = "Play" });
            row.Add(new InspectorPillButton(() => SetPlaying(false)) { text = "Pause" });
            row.Add(new InspectorPillButton(() => Go(0)) { text = "Start" });
            row.Add(new InspectorPillButton(Rebuild) { text = "Rebuild" });
            section.Add(row);

            scrubber = new InspectorSliderInt(0, Mathf.Max(0, reel.FrameCount - 1)) { value = reel.Frame };
            scrubber.RegisterValueChangedCallback(change => Go(change.newValue));
            section.Add(scrubber);

            readout = new Label();
            section.Add(readout);

            root.Add(section);
            root.schedule.Execute(Sync).Every(50);
            return root;
        }

        private VisualElement BuildFields()
        {
            var fields = InspectorVisuals.CreateStack();
            var property = serializedObject.GetIterator();
            for (var enter = true; property.NextVisible(enter); enter = false)
            {
                if (property.propertyPath == "m_Script") continue;
                fields.Add(new PropertyField(property.Copy()));
            }
            fields.Bind(serializedObject);
            return fields;
        }

        private void SetPlaying(bool value)
        {
            reel.Playing = value;
            EditorUtility.SetDirty(reel);
        }

        private void Go(int frame)
        {
            reel.Playing = false;
            reel.Frame = frame;
            Sync();
        }

        private void Rebuild()
        {
            reel.Rebuild();
            Sync();
        }

        private void Sync()
        {
            if (reel == null) return;

            var last = Mathf.Max(0, reel.FrameCount - 1);
            if (scrubber.highValue != last) scrubber.highValue = last;
            scrubber.SetValueWithoutNotify(reel.Frame);
            readout.text = $"frame {reel.Frame} / {last}   ·   {reel.Frame / (float)reel.Fps:0.00}s of {reel.Duration:0.00}s";
        }
    }
}
