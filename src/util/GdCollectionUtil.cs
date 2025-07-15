
using System.Collections.Generic;
using Godot;

namespace Rummy.Util;

public static class GdArray
{
    public static Godot.Collections.Array<T> From<[MustBeVariant]T>(IEnumerable<T> arr) => [.. arr];
}


public static class GdDict
{
    public static Godot.Collections.Dictionary<TKey, TVal> From<[MustBeVariant] TKey, [MustBeVariant] TVal>(Dictionary<TKey, TVal> dict) => new (dict);
}