using System;

namespace Rummy.Util;

public static class StringManipulationExtensions
{
    public static string ReplaceAt(this string self, int index, char newValue) {
        if (index < 0 || index >= self.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return $"{self[..index]}{newValue}{self[index..]}";
    }
    public static string ReplaceAt(this string self, int index, string newValue) {
        if (index < 0 || index >= self.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return $"{self[..index]}{newValue}{self[index..]}";
    }
}