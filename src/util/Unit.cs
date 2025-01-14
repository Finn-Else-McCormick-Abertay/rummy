using System;
using System.Threading.Tasks;

namespace Rummy.Util;

public readonly struct Unit : IEquatable<Unit> {
    public static readonly Unit unit;
    public override readonly bool Equals(object obj) => obj is Unit;
    public readonly bool Equals(Unit other) => true;
    public static bool operator ==(Unit lhs, Unit rhs) => lhs.Equals(rhs);
    public static bool operator !=(Unit lhs, Unit rhs) => !(lhs == rhs);
    public override readonly int GetHashCode() => 0;
    public override readonly string ToString() => "()";
}

public static class UnitExtensions
{
    public static async Task<Unit> AsUnitTask(this Task task) {
        await task;
        return Unit.unit;
    }

    public static Func<TResult, Unit> AsFunc<TResult>(this Action<TResult> action) {
        return result => {
            action(result);
            return Unit.unit;
        };
    }

    public static Func<Unit, Unit> AsFunc(this Action action) {
        return _ => {
            action();
            return Unit.unit;
        };
    }
}