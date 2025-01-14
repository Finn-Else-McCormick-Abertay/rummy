
using System;
using System.Collections.Generic;

namespace Rummy.Util;

public static class GetOrAddExtensions
{
    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue valDefault)
    {
        if (!dict.TryGetValue(key, out TValue val)) {
            val = valDefault;
            dict.Add(key, val);
        }
        return val;
    }

    public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) where TValue : new()
    {
        if (!dict.TryGetValue(key, out TValue val)) {
            val = new TValue();
            dict.Add(key, val);
        }
        return val;
    }
    
    public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, Func<TValue> createNew)
    {
        if (!dict.TryGetValue(key, out TValue val)) {
            val = createNew();
            dict.Add(key, val);
        }
        return val;
    }
}