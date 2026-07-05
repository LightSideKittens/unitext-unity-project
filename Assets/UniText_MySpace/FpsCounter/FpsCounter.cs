using UnityEngine;

namespace LightSide.Samples
{
    /// <summary>
    /// Displays current frames-per-second on a target <see cref="UniTextBase"/> component.
    /// Demonstrates the zero-allocation text update path via
    /// <see cref="UniTextBase.SetText(char[], int, int)"/>.
    /// </summary>
    /// <remarks>
    /// FPS is averaged over a sliding window of length <see cref="updateInterval"/> seconds.
    /// Wall-clock time is used (<see cref="Time.unscaledDeltaTime"/>) so the readout keeps
    /// updating while the game is paused via <see cref="Time.timeScale"/>.
    /// The numeric value is formatted in-place into a pre-allocated buffer — no GC traffic
    /// even at hundreds of updates per second.
    /// </remarks>
    public class FpsCounter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Text component to drive. Falls back to GetComponent<UniTextBase>() at Awake when null.")]
        private UniTextBase target;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Refresh period of the displayed value in seconds. Smaller values are noisier; larger values are smoother but laggier.")]
        private float updateInterval = 0.25f;

        [SerializeField]
        [Tooltip("Append average frame time, e.g. \"60 FPS (16.6 ms)\".")]
        private bool showMilliseconds;

        private readonly char[] buffer = new char[32];
        private int frameCount;
        private float elapsed;

        private void Awake()
        {
            if (target == null) target = GetComponent<UniTextBase>();
        }

        private void Update()
        {
            frameCount++;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < updateInterval) return;

            var fps = frameCount / elapsed;
            var ms = elapsed * 1000f / frameCount;
            frameCount = 0;
            elapsed = 0f;

            if (target == null) return;

            var length = WriteFps(buffer, fps, showMilliseconds, ms);
            target.SetText(buffer, 0, length);
        }

        private static int WriteFps(char[] dst, float fps, bool withMs, float ms)
        {
            var i = WriteInt(dst, 0, (int)(fps + 0.5f));
            dst[i++] = ' ';
            dst[i++] = 'F';
            dst[i++] = 'P';
            dst[i++] = 'S';

            if (!withMs) return i;

            dst[i++] = ' ';
            dst[i++] = '(';
            i = WriteFixed1(dst, i, ms);
            dst[i++] = ' ';
            dst[i++] = 'm';
            dst[i++] = 's';
            dst[i++] = ')';
            return i;
        }

        private static int WriteInt(char[] dst, int offset, int value)
        {
            if (value < 0) { dst[offset++] = '-'; value = -value; }

            var start = offset;
            do
            {
                dst[offset++] = (char)('0' + value % 10);
                value /= 10;
            } while (value > 0);

            for (int a = start, b = offset - 1; a < b; a++, b--)
                (dst[a], dst[b]) = (dst[b], dst[a]);

            return offset;
        }

        private static int WriteFixed1(char[] dst, int offset, float value)
        {
            if (value < 0) { dst[offset++] = '-'; value = -value; }

            var whole = (int)value;
            var tenth = (int)((value - whole) * 10f + 0.5f);
            if (tenth >= 10) { whole++; tenth = 0; }

            offset = WriteInt(dst, offset, whole);
            dst[offset++] = '.';
            dst[offset++] = (char)('0' + tenth);
            return offset;
        }
    }
}
