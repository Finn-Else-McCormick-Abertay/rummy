
using System;
using System.Linq;

namespace Rummy.Util;

public static class Delimiter
{
    public static readonly string Comma = ", ";
    public static readonly char LineBreak = '\n';
    public static readonly char ZeroWidth = '\u200B';
}

public static class Text
{
    public static string Plural(int value, string none = null, string one = null, string two = null, string few = null, string many = null, string other = null) {
        string template = Math.Abs(value) switch {
            0 => none ?? other,
            1 => one ?? few ?? other,
            2 => two ?? few ?? other,
            3 => few ?? other,
            _ => many ?? other
        };
        var placeholderIndices = template.FindAllIndices('%').Where(i => i == 0 || template[i - 1] != '\\');
        foreach (int index in placeholderIndices) template = template.ReplaceAt(index, value.ToString());
        return template;
    }

    public static string Ordinal(int value) => Plural(value, one: "%th", two: "%nd", few: "%rd", other: "%th");
}