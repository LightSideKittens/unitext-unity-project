using System;
using System.Collections.Generic;

namespace LightSide.Hub
{
    /// <summary>
    /// Semantic-version ordering: numeric release parts first, then a pre-release tag, which always
    /// sorts below the release it qualifies. Unparsable parts count as zero rather than failing —
    /// a registry may serve any string, and an unrecognised version must still take a place in the list.
    /// </summary>
    internal sealed class SemVerComparer : IComparer<string>
    {
        /// <summary>The shared instance; the comparison carries no state.</summary>
        public static readonly SemVerComparer Instance = new();

        public int Compare(string a, string b)
        {
            if (a == b) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            var left = Parse(a);
            var right = Parse(b);

            for (var i = 0; i < 3; i++)
            {
                var order = left.numbers[i].CompareTo(right.numbers[i]);
                if (order != 0) return order;
            }

            if (left.pre == "" && right.pre != "") return 1;
            if (left.pre != "" && right.pre == "") return -1;
            return string.Compare(left.pre, right.pre, StringComparison.Ordinal);
        }

        /// <summary>Whether <paramref name="candidate"/> orders above <paramref name="current"/>.</summary>
        public static bool IsNewer(string candidate, string current)
            => Instance.Compare(candidate, current) > 0;

        /// <summary>Whether a version string carries a pre-release tag.</summary>
        public static bool IsPreRelease(string version)
            => !string.IsNullOrEmpty(version) && version.Contains("-");

        private static (int[] numbers, string pre) Parse(string version)
        {
            var pre = "";
            var dash = version.IndexOf('-');
            if (dash >= 0)
            {
                pre = version.Substring(dash + 1);
                version = version.Substring(0, dash);
            }
            var parts = version.Split('.');
            var numbers = new int[3];
            for (var i = 0; i < Math.Min(parts.Length, 3); i++)
                int.TryParse(parts[i], out numbers[i]);
            return (numbers, pre);
        }
    }
}
