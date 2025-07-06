
using System;
using System.Collections.Generic;

namespace Rummy.Util;
#nullable enable

public static class List
{
    public static IEnumerable<T> Of<T>(params IEnumerable<T> values) => values;
    public static IEnumerable<T> Of<T>(params object?[] values) {
        foreach (var value in values) {
            if (value is T tval) yield return tval;
            else if (value.TryConvertTo(out T result)) yield return result;
            else if (value is IEnumerable<T> enumerableVal) foreach (var innerVal in enumerableVal) yield return innerVal;
        }
    }
}