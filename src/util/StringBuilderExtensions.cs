
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Rummy.Util;
#nullable enable

using Sb = System.Text.StringBuilder;
using Pr = Func<bool>;

public static class StringSbExtensions
{
    public static Sb ToBuilder(this string self) => new(self);

    public static Sb AppendIf(this Sb self, bool bl, object? val) { if (bl) self.Append(val); return self; }
    public static Sb AppendIf(this Sb self, Pr pred, object? val) { if (pred()) self.Append(val); return self; }
    public static Sb AppendIf(this Sb self, bool bl, string? val) { if (bl) self.Append(val); return self; }
    public static Sb AppendIf(this Sb self, Pr pred, string? val) { if (pred()) self.Append(val); return self; }
    public static Sb AppendIf(this Sb self, bool bl, char? val) { if (bl) self.Append(val); return self; }
    public static Sb AppendIf(this Sb self, Pr pred, char? val) { if (pred()) self.Append(val); return self; }
    public static Sb AppendIf(this Sb self, bool bl, Sb? val) { if (bl) self.Append(val); return self; }
    public static Sb AppendIf(this Sb self, Pr pred, Sb? val) { if (pred()) self.Append(val); return self; }

    public static Sb AppendLineIf(this Sb self, bool bl, string? val) { if (bl) self.AppendLine(val); return self; }
    public static Sb AppendLineIf(this Sb self, Pr pred, string? val) { if (pred()) self.AppendLine(val); return self; }
    public static Sb AppendLineIf(this Sb self, bool bl) { if (bl) self.AppendLine(); return self; }
    public static Sb AppendLineIf(this Sb self, Pr pred) { if (pred()) self.AppendLine(); return self; }

    public static Sb AppendJoinIf<T>(this Sb self, bool bl, string? separator, params IEnumerable<T> values) { if (bl) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf<T>(this Sb self, Pr pred, string? separator, params IEnumerable<T> values) { if (pred()) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf<T>(this Sb self, bool bl, char separator, params IEnumerable<T> values) { if (bl) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf<T>(this Sb self, Pr pred, char separator, params IEnumerable<T> values) { if (pred()) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf(this Sb self, bool bl, string? separator, params string?[] values) { if (bl) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf(this Sb self, Pr pred, string? separator, params string?[] values) { if (pred()) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf(this Sb self, bool bl, char separator, params string?[] values) { if (bl) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf(this Sb self, Pr pred, char separator, params string?[] values) { if (pred()) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf(this Sb self, bool bl, string? separator, params object?[] values) { if (bl) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf(this Sb self, Pr pred, string? separator, params object?[] values) { if (pred()) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf(this Sb self, bool bl, char separator, params object?[] values) { if (bl) self.AppendJoin(separator, values); return self; }
    public static Sb AppendJoinIf(this Sb self, Pr pred, char separator, params object?[] values) { if (pred()) self.AppendJoin(separator, values); return self; }

    public static Sb AppendFormatIf(this Sb self, bool bl, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] args) { if (bl) self.AppendFormat(format, args); return self; }
    public static Sb AppendFormatIf(this Sb self, bool bl, IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] args) { if (bl) self.AppendFormat(provider, format, args); return self; }
    public static Sb AppendFormatIf(this Sb self, Pr pred, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] args) { if (pred()) self.AppendFormat(format, args); return self; }
    public static Sb AppendFormatIf(this Sb self, Pr pred, IFormatProvider? provider, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] args) { if (pred()) self.AppendFormat(provider, format, args); return self; }

    private static Sb AppendWrapDelimiter(this Sb self, string? delimiters, bool front) {
        if (string.IsNullOrEmpty(delimiters)) return self;
        if (front && delimiters.Length > 1) self.Append(delimiters[0..(delimiters.Length / 2)]);
        if (delimiters.Length % 2 == 1) self.Append(delimiters[delimiters.Length / 2]);
        if (!front && delimiters.Length > 1) self.Append(delimiters[(delimiters.Length / 2 + delimiters.Length % 2)..]);
        return self;
    }

    public static Sb AppendWrapped(this Sb self, char frontDelim, char backDelim, object? val) => self.Append(frontDelim).Append(val).Append(backDelim);
    public static Sb AppendWrapped(this Sb self, string? frontDelim, string? backDelim, object? val) => self.Append(frontDelim).Append(val).Append(backDelim);
    public static Sb AppendWrapped(this Sb self, string? delimiters, object? val) => self.AppendWrapDelimiter(delimiters, true).Append(val).AppendWrapDelimiter(delimiters, false);
    
    public static Sb AppendJoinWrapped(this Sb self, char frontDelim, char backDelim, char separator, params object?[] values) => self.Append(frontDelim).AppendJoin(separator, values).Append(backDelim);
    public static Sb AppendJoinWrapped(this Sb self, char frontDelim, char backDelim, string? separator, params object?[] values) => self.Append(frontDelim).AppendJoin(separator, values).Append(backDelim);
    public static Sb AppendJoinWrapped(this Sb self, string? frontDelim, string? backDelim, char separator, params object?[] values) => self.Append(frontDelim).AppendJoin(separator, values).Append(backDelim);
    public static Sb AppendJoinWrapped(this Sb self, string? frontDelim, string? backDelim, string? separator, params object?[] values) => self.Append(frontDelim).AppendJoin(separator, values).Append(backDelim);
    public static Sb AppendJoinWrapped(this Sb self, string? delimiters, char separator, params object?[] values) => self.AppendWrapDelimiter(delimiters, true).AppendJoin(separator, values).AppendWrapDelimiter(delimiters, false);
    public static Sb AppendJoinWrapped(this Sb self, string? delimiters, string? separator, params object?[] values) => self.AppendWrapDelimiter(delimiters, true).AppendJoin(separator, values).AppendWrapDelimiter(delimiters, false);
}