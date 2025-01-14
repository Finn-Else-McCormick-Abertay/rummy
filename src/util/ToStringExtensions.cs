
using System.Collections.Generic;

namespace Rummy.Util;

#nullable enable
static class ToStringExtensions
{
    public static string ToJoinedString<T>(this IEnumerable<T> self, string? separator = null) => string.Join(separator, self);
}