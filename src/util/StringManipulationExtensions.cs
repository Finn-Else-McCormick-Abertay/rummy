using System;

namespace Rummy.Util;

public static class StringManipulationExtensions
{
    public static string ReplaceAt(this string self, int index, char newValue) => ReplaceAt(self, index, newValue.ToString());
    public static string ReplaceAt(this string self, int index, string newValue) {
        if (index < 0 || index >= self.Length) throw new ArgumentOutOfRangeException(nameof(index));
        string before = index switch { 0 => "", _ => self[..(index - 1)] };
        string after = index switch { _ when index >= self.Length => "", _ => self[(index + 1)..] };
        return $"{before}{newValue}{after}";
    }
}