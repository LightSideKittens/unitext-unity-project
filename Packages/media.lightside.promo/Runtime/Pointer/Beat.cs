using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>What a pointer does next.</summary>
    public enum BeatKind
    {
        /// <summary>Travel to a point.</summary>
        To,

        /// <summary>Hold still.</summary>
        Wait,

        /// <summary>Press and release where the pointer already is.</summary>
        Click,

        /// <summary>Press here, travel with the button held, release at the destination.</summary>
        Drag,

        /// <summary>A keystroke, shown as a chip above the pointer.</summary>
        Key
    }

    /// <summary>
    /// One entry in the ordered list a <see cref="PointerTimeline"/> compiles.
    /// </summary>
    /// <remarks>
    /// A scene declares beats and nothing else. Both the pointer's path and every UI reaction are read back out of
    /// the compiled timeline, so the two cannot disagree about when something happened — authoring them as two
    /// lists is how a pointer ends up clicking before it arrives.
    /// </remarks>
    public readonly struct Beat
    {
        private Beat(BeatKind kind, Vector2 point, float seconds, string id, string label, float targetWidth,
            bool settles)
        {
            Kind = kind;
            Point = point;
            Seconds = seconds;
            Id = id;
            Label = label;
            TargetWidth = targetWidth;
            Settles = settles;
        }

        public BeatKind Kind { get; }

        public Vector2 Point { get; }

        public float Seconds { get; }

        /// <summary>Name this beat's span is looked up by, or null for an unnamed beat.</summary>
        public string Id { get; }

        public string Label { get; }

        /// <summary>Width of the thing being aimed at, which is half of what sets the travel time.</summary>
        public float TargetWidth { get; }

        /// <summary>Whether the click changes the interface, which earns a longer hold afterwards.</summary>
        public bool Settles { get; }


        public static Beat To(Vector2 point, string id = null, float targetWidth = 0f) =>
            new Beat(BeatKind.To, point, 0f, id, null, targetWidth, false);

        public static Beat Wait(float seconds, string id = null) =>
            new Beat(BeatKind.Wait, default, seconds, id, null, 0f, false);

        public static Beat Click(string id = null, bool settles = false) =>
            new Beat(BeatKind.Click, default, 0f, id, null, 0f, settles);

        public static Beat Drag(Vector2 point, string id, float targetWidth = 0f) =>
            new Beat(BeatKind.Drag, point, 0f, id, null, targetWidth, false);

        public static Beat Key(string label, string id = null) =>
            new Beat(BeatKind.Key, default, 0f, id, label, 0f, false);
    }
}
