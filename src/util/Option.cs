using System;

namespace Rummy.Util;

public readonly struct Option {
    public static Option<T> Some<T>(T v) {
        return Option<T>.Some(v);
    }

    public static Option<Unit> None => Option<Unit>.None;
}

public readonly struct Option<T> {
    private readonly bool _exists;
    public readonly T Value;
    
    private Option(T value, bool exists) {
        _exists = exists;
        Value = value;
    }

    public bool IsSome => _exists;
    public bool IsNone => !_exists;

    public static Option<T> Some(T v) {
        return new(v, true);
    }

    public static Option<T> None => new(default, false);
    
    public static implicit operator Option<T>(T v) => new(v, true);
    public static implicit operator Option<T>(Option<Unit> other) => new(default, other._exists);

    public R Match<R>(Func<T, R> some, Func<R> none) => IsSome ? some(Value) : none();
    public void Match(Action<T> some, Action none) => Inspect(some).InspectNone(none);

    public Option<U> AndThen<U>(Func<T, Option<U>> andThen) => IsSome ? andThen(Value) : Option.None;
    public T Or(T orVal) => IsSome ? Value : orVal;
    public Option<T> OrElse(Func<Option<T>> orElse) => IsSome ? Some(Value) : orElse();

    public Option<T> Inspect(Action<T> inspect) {
        if (IsSome) { inspect(Value); };
        return this;
    }
    public Option<T> InspectNone(Action inspect) {
        if (IsNone) { inspect(); };
        return this;
    }

    public bool IsSomeAnd(Func<T, bool> predicate) => IsSome ? predicate(Value) : false;
}