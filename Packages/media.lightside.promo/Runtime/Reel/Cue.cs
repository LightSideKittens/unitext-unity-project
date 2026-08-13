namespace LightSide.Promo
{
    /// <summary>A named moment in a reel: a sound to place, a marker to cut on.</summary>
    /// <remarks>
    /// Frame-stepped capture cannot record audio — it steps time faster than a clock runs — so the reel does not
    /// try. It emits the times instead, and the sound is laid against them outside Unity. Because the cue and the
    /// thing it marks are both derived from the same timeline, they cannot drift apart the way a hand-placed sound
    /// does.
    /// </remarks>
    public readonly struct Cue
    {
        public Cue(string name, float at)
        {
            Name = name;
            At = at;
        }

        /// <summary>What happens, in a vocabulary the sound designer chooses: <c>click</c>, <c>whoosh</c>, <c>impact</c>.</summary>
        public string Name { get; }

        /// <summary>Seconds from the start of whatever timeline emitted it.</summary>
        public float At { get; }

        /// <summary>The same cue moved onto a later timeline.</summary>
        public Cue Offset(float seconds) => new Cue(Name, At + seconds);
    }
}
