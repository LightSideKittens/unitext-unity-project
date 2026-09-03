using System;
using System.Diagnostics;
using UnityEngine.LowLevel;

namespace LightSide.Benchmark
{
    /// <summary>
    /// Stopwatch window over the player loop from the end of <c>Initialization</c> to the end of
    /// <c>PreLateUpdate</c>: the frame's script and engine work, with rendering, present and frame
    /// pacing left outside. The window must close before <c>PostLateUpdate</c> — that is where the
    /// display wait lives, and a window spanning it reads the refresh interval instead of the work,
    /// which on a paced device hides everything cheaper than one frame. It covers every participant
    /// that ticks in Update or LateUpdate; work scheduled into <c>PostLateUpdate</c> goes unseen.
    /// </summary>
    public static class BenchmarkFrameProbe
    {
        private struct StartMarker { }
        private struct EndMarker { }

        private static long startTimestamp;
        private static bool installed;

        /// <summary>Milliseconds the window of the most recently completed frame spanned.</summary>
        public static double LastMilliseconds { get; private set; }

        public static void Install()
        {
            if (installed) return;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Append(ref loop, typeof(UnityEngine.PlayerLoop.Initialization), typeof(StartMarker), BeginFrame);
            Append(ref loop, typeof(UnityEngine.PlayerLoop.PreLateUpdate), typeof(EndMarker), EndFrame);
            PlayerLoop.SetPlayerLoop(loop);
            installed = true;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Remove(ref loop, typeof(UnityEngine.PlayerLoop.Initialization), typeof(StartMarker));
            Remove(ref loop, typeof(UnityEngine.PlayerLoop.PreLateUpdate), typeof(EndMarker));
            PlayerLoop.SetPlayerLoop(loop);
            installed = false;
        }

        private static void BeginFrame() => startTimestamp = Stopwatch.GetTimestamp();

        private static void EndFrame() =>
            LastMilliseconds = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

        private static void Append(ref PlayerLoopSystem root, Type phaseType, Type markerType,
            PlayerLoopSystem.UpdateFunction callback)
        {
            var phases = root.subSystemList;
            for (var i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != phaseType) continue;
                var children = phases[i].subSystemList ?? Array.Empty<PlayerLoopSystem>();
                var grown = new PlayerLoopSystem[children.Length + 1];
                Array.Copy(children, grown, children.Length);
                grown[children.Length] = new PlayerLoopSystem { type = markerType, updateDelegate = callback };
                phases[i].subSystemList = grown;
                return;
            }
            throw new InvalidOperationException($"The player loop is missing its {phaseType.Name} phase.");
        }

        private static void Remove(ref PlayerLoopSystem root, Type phaseType, Type markerType)
        {
            var phases = root.subSystemList;
            for (var i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != phaseType || phases[i].subSystemList == null) continue;
                var children = phases[i].subSystemList;
                for (var j = 0; j < children.Length; j++)
                {
                    if (children[j].type != markerType) continue;
                    var shrunk = new PlayerLoopSystem[children.Length - 1];
                    Array.Copy(children, shrunk, j);
                    Array.Copy(children, j + 1, shrunk, j, children.Length - j - 1);
                    phases[i].subSystemList = shrunk;
                    break;
                }
            }
        }
    }
}
