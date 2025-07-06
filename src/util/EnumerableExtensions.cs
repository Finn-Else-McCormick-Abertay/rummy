
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummy.Util;

#nullable enable
static class EnumerableExtensions
{
    public static bool None<T>(this IEnumerable<T> self) => !self.Any();
    public static bool None<T>(this IEnumerable<T> self, Func<T, bool> predicate) => !self.Any(predicate);

    // This does the same thing as Reverse, it only exists because some derivatives of IEnumerable hide reverse behind a version that returns void just to be assholes.
    public static IEnumerable<T> Reversed<T>(this IEnumerable<T> self) => self.Reverse();

    // This is a tiny thing but it just seems intuitive
    public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> self) => self.TakeLast(1);
    public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> self) => self.SkipLast(1);

    public static IEnumerable<string?> AsStrings<T>(this IEnumerable<T> self) => self.Select(x => x?.ToString());

    public static void ForEach<T>(this IEnumerable<T> self, Action<T> action) {
        ArgumentNullException.ThrowIfNull(action);
        foreach (T element in self) { action(element); }
    }

    public static void ForEach<T>(this IEnumerable<T> self, Action<int, T> action) {
        ArgumentNullException.ThrowIfNull(action);
        int index = 0;
        foreach (T element in self) { action(index++, element); }
    }

    public static T? Find<T>(this IEnumerable<T> self, Predicate<T> match) => self.FirstOrDefault(new Func<T, bool>(match));
    public static T? FindLast<T>(this IEnumerable<T> self, Predicate<T> match) => self.LastOrDefault(new Func<T, bool>(match));

    public static IEnumerable<T> FindAll<T>(this IEnumerable<T> self, Predicate<T> match) => self.Where(new Func<T, bool>(match));

    public static int FindIndex<T>(this IEnumerable<T> self, int startIndex, int count, Predicate<T> match) {
        if (match is null) throw new ArgumentNullException(nameof(match));
        if (startIndex < 0 || startIndex >= self.Count()) throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, null);
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count), count, null);
        if (startIndex + count >= self.Count()) throw new ArgumentOutOfRangeException(nameof(count), count, "Ends beyond end of enumerable");

        for (int i = startIndex; i < startIndex + count; ++i) if (match(self.ElementAt(i))) return i;
        return -1;
    }
    public static int FindIndex<T>(this IEnumerable<T> self, int startIndex, Predicate<T> match) {
        if (match is null) throw new ArgumentNullException(nameof(match));
        if (startIndex < 0 || startIndex >= self.Count()) throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, null);

        for (int i = startIndex; i < self.Count(); ++i) if (match(self.ElementAt(i))) return i;
        return -1;
    }
    public static int FindIndex<T>(this IEnumerable<T> self, Predicate<T> match) => self.FindIndex(0, match);

    public static int FindLastIndex<T>(this IEnumerable<T> self, int startIndex, int count, Predicate<T> match) {
        if (match is null) throw new ArgumentNullException(nameof(match));
        if (startIndex < 0 || startIndex >= self.Count()) throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, null);
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count), count, null);
        if (startIndex + count >= self.Count()) throw new ArgumentOutOfRangeException(nameof(count), count, "Ends beyond end of enumerable");

        for (int i = startIndex + count - 1; i >= startIndex; --i) if (match(self.ElementAt(i))) return i;
        return -1;
    }
    public static int FindLastIndex<T>(this IEnumerable<T> self, int startIndex, Predicate<T> match) {
        if (match is null) throw new ArgumentNullException(nameof(match));
        if (startIndex < 0 || startIndex >= self.Count()) throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, null);

        for (int i = self.Count() - 1; i >= startIndex; --i) if (match(self.ElementAt(i))) return i;
        return -1;
    }
    public static int FindLastIndex<T>(this IEnumerable<T> self, Predicate<T> match) => self.FindLastIndex(0, match);

    public static IEnumerable<int> FindAllIndices<T>(this IEnumerable<T> self, Predicate<T> match) =>
        self.Index().Where(pair => match(pair.Item)).Select(pair => pair.Index);

    private static Predicate<T> EqualsPredicate<T, U>(U item) => (x) => Equals(x, item);

    public static T? Find<T, U>(this IEnumerable<T> self, U item) => self.Find(EqualsPredicate<T, U>(item));
    public static T? FindLast<T, U>(this IEnumerable<T> self, U item) => self.FindLast(EqualsPredicate<T, U>(item));
    public static IEnumerable<T> FindAll<T, U>(this IEnumerable<T> self, U item) => self.FindAll(EqualsPredicate<T, U>(item));
    public static List<T> FindAll<T, U>(this List<T> self, U item) => self.FindAll(EqualsPredicate<T, U>(item));

    public static int FindIndex<T, U>(this IEnumerable<T> self, U item) => self.FindIndex(EqualsPredicate<T, U>(item));
    public static int FindIndex<T, U>(this IEnumerable<T> self, int startIndex, U item) =>
        self.FindIndex(startIndex, EqualsPredicate<T, U>(item));
    public static int FindIndex<T, U>(this IEnumerable<T> self, int startIndex, int count, U item) =>
        self.FindIndex(startIndex, count, EqualsPredicate<T, U>(item));

    public static int FindLastIndex<T, U>(this IEnumerable<T> self, U item) => self.FindLastIndex(EqualsPredicate<T, U>(item));
    public static int FindLastIndex<T, U>(this IEnumerable<T> self, int startIndex, U item) =>
        self.FindLastIndex(startIndex, EqualsPredicate<T, U>(item));
    public static int FindLastIndex<T, U>(this IEnumerable<T> self, int startIndex, int count, U item) =>
        self.FindLastIndex(startIndex, count, EqualsPredicate<T, U>(item));

    public static IEnumerable<int> FindAllIndices<T, U>(this IEnumerable<T> self, U item) =>
        self.FindAllIndices(EqualsPredicate<T, U>(item));

    // Tuple deconstruction    
    public static IEnumerable<TResult> Select<T1, T2, TResult>(this IEnumerable<(T1, T2)> source, Func<T1, T2, TResult> selector) =>
        source.Select(s => selector(s.Item1, s.Item2));
    public static IEnumerable<TResult> Select<T1, T2, T3, TResult>(this IEnumerable<(T1, T2, T3)> source, Func<T1, T2, T3, TResult> selector) =>
        source.Select(s => selector(s.Item1, s.Item2, s.Item3));
    public static IEnumerable<TResult> Select<T1, T2, T3, T4, TResult>(this IEnumerable<(T1, T2, T3, T4)> source, Func<T1, T2, T3, T4, TResult> selector) =>
        source.Select(s => selector(s.Item1, s.Item2, s.Item3, s.Item4));
    public static IEnumerable<TResult> Select<T1, T2, T3, T4, T5, TResult>(this IEnumerable<(T1, T2, T3, T4, T5)> source, Func<T1, T2, T3, T4, T5, TResult> selector) =>
        source.Select(s => selector(s.Item1, s.Item2, s.Item3, s.Item4, s.Item5));
    public static IEnumerable<TResult> Select<T1, T2, T3, T4, T5, T6, TResult>(this IEnumerable<(T1, T2, T3, T4, T5, T6)> source, Func<T1, T2, T3, T4, T5, T6, TResult> selector) =>
        source.Select(s => selector(s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6));
    public static IEnumerable<TResult> Select<T1, T2, T3, T4, T5, T6, T7, TResult>(this IEnumerable<(T1, T2, T3, T4, T5, T6, T7)> source, Func<T1, T2, T3, T4, T5, T6, T7, TResult> selector) =>
        source.Select(s => selector(s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7));
    public static IEnumerable<TResult> Select<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(this IEnumerable<(T1, T2, T3, T4, T5, T6, T7, T8)> source, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> selector) =>
        source.Select(s => selector(s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7, s.Item8));

    public static IEnumerable<TResult> SelectMany<T1, T2, TResult>(this IEnumerable<(T1, T2)> source, Func<T1, T2, IEnumerable<TResult>> selector) =>
        source.SelectMany(s => selector(s.Item1, s.Item2));
    public static IEnumerable<TResult> SelectMany<T1, T2, T3, TResult>(this IEnumerable<(T1, T2, T3)> source, Func<T1, T2, T3, IEnumerable<TResult>> selector) =>
        source.SelectMany(s => selector(s.Item1, s.Item2, s.Item3));
    public static IEnumerable<TResult> SelectMany<T1, T2, T3, T4, TResult>(this IEnumerable<(T1, T2, T3, T4)> source, Func<T1, T2, T3, T4, IEnumerable<TResult>> selector) =>
        source.SelectMany(s => selector(s.Item1, s.Item2, s.Item3, s.Item4));
    public static IEnumerable<TResult> SelectMany<T1, T2, T3, T4, T5, TResult>(this IEnumerable<(T1, T2, T3, T4, T5)> source, Func<T1, T2, T3, T4, T5, IEnumerable<TResult>> selector) =>
        source.SelectMany(s => selector(s.Item1, s.Item2, s.Item3, s.Item4, s.Item5));
    public static IEnumerable<TResult> SelectMany<T1, T2, T3, T4, T5, T6, TResult>(this IEnumerable<(T1, T2, T3, T4, T5, T6)> source, Func<T1, T2, T3, T4, T5, T6, IEnumerable<TResult>> selector) =>
        source.SelectMany(s => selector(s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6));
    public static IEnumerable<TResult> SelectMany<T1, T2, T3, T4, T5, T6, T7, TResult>(this IEnumerable<(T1, T2, T3, T4, T5, T6, T7)> source, Func<T1, T2, T3, T4, T5, T6, T7, IEnumerable<TResult>> selector) =>
        source.SelectMany(s => selector(s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7));
    public static IEnumerable<TResult> SelectMany<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(this IEnumerable<(T1, T2, T3, T4, T5, T6, T7, T8)> source, Func<T1, T2, T3, T4, T5, T6, T7, T8, IEnumerable<TResult>> selector) =>
        source.SelectMany(s => selector(s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7, s.Item8));

    // KeyValuePair deconstruction
    public static IEnumerable<TResult> Select<TKey, TValue, TResult>(this IEnumerable<KeyValuePair<TKey, TValue>> source, Func<TKey, TValue, TResult> selector) =>
        source.Select(s => selector(s.Key, s.Value));
    public static IEnumerable<TResult> SelectDecomposed<TKey, TValue, TResult>(this IEnumerable<KeyValuePair<TKey, TValue>> source, Func<TKey, TValue, TResult> selector) =>
        Select(source, selector);

    public static IEnumerable<TResult> SelectMany<TKey, TValue, TResult>(this IEnumerable<KeyValuePair<TKey, TValue>> source, Func<TKey, TValue, IEnumerable<TResult>> selector) =>
        source.SelectMany(s => selector(s.Key, s.Value));
    public static IEnumerable<TResult> SelectManyDecomposed<TKey, TValue, TResult>(this IEnumerable<KeyValuePair<TKey, TValue>> source, Func<TKey, TValue, IEnumerable<TResult>> selector) =>
        SelectMany(source, selector);
}