
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummy.Util;

#nullable enable
static class EnumerableExtensions
{
    public static void ForEach<T>(this IEnumerable<T> self, Action<T> action) {
        if (action is null) throw new ArgumentNullException(nameof(action));
        foreach (T element in self) { action(element); }
    }

    public static void ForEach<T>(this IEnumerable<T> self, Action<int, T> action) {
        if (action is null) throw new ArgumentNullException(nameof(action));
        int index = 0;
        foreach (T element in self) { action(index++, element); }
    }

    public static IEnumerable<T> ChainForEach<T>(this IEnumerable<T> self, Action<T> action) {
        self.ForEach(action);
        return self;
    }

    public static IEnumerable<T> ChainForEach<T>(this IEnumerable<T> self, Action<int, T> action) {
        self.ForEach(action);
        return self;
    }

    public static IEnumerable<(int index, T value)> Indexed<T>(this IEnumerable<T> self) => self.Select((x, i) => (i, x));


    public static T? Find<T>(this IEnumerable<T> self, Predicate<T> match) => self.FirstOrDefault(new Func<T,bool>(match));
    public static T? FindLast<T>(this IEnumerable<T> self, Predicate<T> match) => self.LastOrDefault(new Func<T,bool>(match));

    public static IEnumerable<T> FindAll<T>(this IEnumerable<T> self, Predicate<T> match) => self.Where(new Func<T,bool>(match));

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
        self.Indexed().Where(pair => match(pair.value)).Select(pair => pair.index);

    private static Predicate<T> EqualsPredicate<T, U> (U item) => (x) => Equals(x, item);

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
}