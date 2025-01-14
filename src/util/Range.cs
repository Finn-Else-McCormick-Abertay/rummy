
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummy.Util;

// Ignore this I just got a bit distracted learning C#

class Range
{
    public static IEnumerable<int> Over<T>(IEnumerable<T> enumerable) => To(enumerable.Count());

    // Range from start to end (includes start, does not include end)
    public static IEnumerable<int> FromTo(int start, int end) => FromTo(start, end, end > start ? 1 : -1);
    
    public static IEnumerable<int> FromTo(int start, int end, int step) {
        if (start == end) { yield break; }
        if (step == 0) throw new ArgumentException("Step is zero", nameof(step));
        if (step > 0) {
            if (end - 1 < start) throw new ArgumentOutOfRangeException(nameof(end), end, "End lower than start with positive step");
            if (end - 1 > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(end), end, "End greater than int max");
            if (step > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(end), end, "Step greater than int max");
        }
        else if (step < 0) {
            if (end + 1 > start) throw new ArgumentOutOfRangeException(nameof(end), end, "End greater than start with negative step");
            if (end + 1 < int.MinValue) throw new ArgumentOutOfRangeException(nameof(end), end, "End lower than int min");
            if (step < int.MinValue) throw new ArgumentOutOfRangeException(nameof(end), end, "Step lower than int min");
        }

        int i = start;
        while (step > 0 ? i < end : i > end) { yield return i; i += step; }
    }

    // Range from 0 to count (non-inclusive)
    public static IEnumerable<int> To(int count) {
        if (count == 0) { yield break; }
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Count less than zero");
        if (count - 1 > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(count), count, "Count greater than int max");

        int i = 0;
        while (i < count) { yield return i++; }
    }

    public static IEnumerable<int> To(int end, int step) {
        if (end == 0) { yield break; }
        if (step == 0) throw new ArgumentException("Step is zero", nameof(step));
        if (step > 0) {
            if (end < 0) throw new ArgumentOutOfRangeException(nameof(end), end, "End less than zero with positive step");
            if (end - 1 > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(end), end, "End greater than int max");
            if (step > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(end), end, "Step greater than int max");
        }
        else if (step < 0) {
            if (end > 0) throw new ArgumentOutOfRangeException(nameof(end), end, "End greater than zero with negative step");
            if (end + 1 < int.MinValue) throw new ArgumentOutOfRangeException(nameof(end), end, "End lower than int min");
            if (step < int.MinValue) throw new ArgumentOutOfRangeException(nameof(end), end, "Step lower than int min");
        }

        int i = 0;
        while (step > 0 ? i < end : i > end) { yield return i; i += step; }
    }
}