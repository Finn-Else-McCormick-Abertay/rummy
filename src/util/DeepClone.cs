
using System.Collections.Generic;
using System.Linq;

namespace Rummy.Util;

static class DeepCloneExtensionMethods
{
    //public static IEnumerable<T> ShallowClone<T>(this IEnumerable<T> enumerable) => enumerable.Select(x => x);
    public static IEnumerable<T> DeepClone<T>(this IEnumerable<T> enumerable) where T : struct => enumerable.Select(x => x with {});
}