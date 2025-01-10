
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Rummy.Util;

public class EqualityComparer<T> : IEqualityComparer<T> where T : class
{
    private readonly Func<T, T, bool> _comparer;
    private readonly Func<T, int> _hashCodeGetter;

    public EqualityComparer(Func<T, T, bool> comparer, Func<T, int> hashCodeGetter) {
        _comparer = comparer;
        _hashCodeGetter = hashCodeGetter;
    }

    public bool Equals(T x, T y) => _comparer(x, y);
    public int GetHashCode([DisallowNull] T obj) => _hashCodeGetter(obj as T);
}