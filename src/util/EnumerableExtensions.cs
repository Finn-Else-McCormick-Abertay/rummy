
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


    public static T? Find<T, U>(this List<T> self, U item)
        where T : class where U : class => self.Find(x => x == item);

    public static T? FindLast<T, U>(this List<T> self, U item)
    where T : class where U : class => self.FindLast(x => x == item);
        

    public static List<T> FindAll<T, U>(this List<T> self, U item)
    where T : class where U : class => self.FindAll(x => x == item);
    

    public static int FindIndex<T, U>(this List<T> self, U item) 
        where T : class where U : class => self.FindIndex(x => x == item);

    public static int FindIndex<T, U>(this List<T> self, int startIndex, U item)
        where T : class where U : class => self.FindIndex(startIndex, x => x == item);

    public static int FindIndex<T, U>(this List<T> self, int startIndex, int count, U item)
        where T : class where U : class => self.FindIndex(startIndex, count, x => x == item);
    

    public static int FindLastIndex<T, U>(this List<T> self, U item) 
        where T : class where U : class => self.FindLastIndex(x => x == item);

    public static int FindLastIndex<T, U>(this List<T> self, int startIndex, U item)
        where T : class where U : class => self.FindLastIndex(startIndex, x => x == item);

    public static int FindLastIndex<T, U>(this List<T> self, int startIndex, int count, U item)
        where T : class where U : class => self.FindLastIndex(startIndex, count, x => x == item);
}