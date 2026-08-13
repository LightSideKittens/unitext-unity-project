using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>
    /// An ordered list of timed actions that mutate real product state, replayed so that any moment in a slide is
    /// still reproducible from its time alone.
    /// </summary>
    /// <remarks>
    /// Posing cannot express everything. Selecting a range, copying it and pasting it back are mutations of a live
    /// document, not properties of a frame — and a slide that showed a picture of them instead would be showing a
    /// picture of the feature.
    /// <para>
    /// The purity contract is kept where it matters: the same time always yields the same state. Seeking forward
    /// applies the steps in between; seeking backwards resets and replays from the start, which is exact and, for a
    /// shot a few seconds long, costs nothing. What a script must never contain is a step whose result depends on
    /// anything but the steps before it.
    /// </para>
    /// </remarks>
    public sealed class Script
    {
        private readonly struct Step
        {
            public Step(float at, string name, Action action)
            {
                At = at;
                Name = name;
                Action = action;
            }

            public float At { get; }
            public string Name { get; }
            public Action Action { get; }
        }

        private readonly List<Step> steps = new();
        private Action reset;
        private int applied;

        /// <summary>How the world is returned to the state it was in before any step ran.</summary>
        /// <remarks>
        /// Required before the first backwards seek. A script without one cannot rewind, and a reel that cannot
        /// rewind cannot be scrubbed or captured out of order.
        /// </remarks>
        public Script Rewind(Action action)
        {
            reset = action;
            return this;
        }

        /// <summary>
        /// Adds a step at <paramref name="seconds"/> into the slide. <paramref name="name"/> is also emitted as the
        /// step's sound cue.
        /// </summary>
        public Script At(float seconds, string name, Action action)
        {
            steps.Add(new Step(seconds, name, action));
            steps.Sort((a, b) => a.At.CompareTo(b.At));
            return this;
        }

        /// <summary>The steps' names and times, for a slide to declare as cues.</summary>
        public IEnumerable<Cue> Cues()
        {
            for (var i = 0; i < steps.Count; i++)
                if (!string.IsNullOrEmpty(steps[i].Name))
                    yield return new Cue(steps[i].Name, steps[i].At);
        }

        /// <summary>Brings the world to the state it should be in at <paramref name="seconds"/>.</summary>
        public void Seek(float seconds)
        {
            var target = 0;
            while (target < steps.Count && steps[target].At <= seconds) target++;

            if (target < applied)
            {
                if (reset == null)
                {
                    throw new InvalidOperationException(
                        "[Promo] This script was seeked backwards without a Rewind action. Declare one, or the " +
                        "slide cannot be scrubbed or captured out of order.");
                }

                reset();
                applied = 0;
            }

            while (applied < target) steps[applied++].Action();
        }

        /// <summary>Forgets what has been applied, for a rebuild that recreated the world from scratch.</summary>
        public void Forget() => applied = 0;

        /// <summary>When the last step runs.</summary>
        public float Length => steps.Count == 0 ? 0f : steps[steps.Count - 1].At;
    }
}
