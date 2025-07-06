using System;

namespace Rummy.Util;

public static class Convertible
{
    public static bool IsConvertibleTo<T>(this object value) {
        try {
            T convertedValue = (T)Convert.ChangeType(value, typeof(T));
            return true;
        }
        catch (InvalidCastException) { return false; }
        catch (FormatException) { return false; }
        catch (OverflowException) { return false; }
    }

    public static bool TryConvertTo<T>(this object value, out T result) {
        if (value.IsConvertibleTo<T>()) {
            result = (T)Convert.ChangeType(value, typeof(T));
            return true;
        }
        result = default;
        return false;
    }
}