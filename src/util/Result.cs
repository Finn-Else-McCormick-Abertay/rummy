using System;

namespace Rummy.Util;

// Adapted from implementation by Eesaa Philips (https://dev.to/ephilips/better-error-handling-in-c-with-result-types-4aan)

public readonly struct Result {
    public static Result<Unit, Unit> Ok() {
        return Result<Unit, Unit>.Ok(Unit.unit);
    }

    public static Result<T, Unit> Ok<T>(T v) {
        return Result<T, Unit>.Ok(v);
    }

    public static Result<Unit, E> Err<E>(E e) {
        return Result<Unit, E>.Err(e);
    }
}

public readonly struct Result<T, E> {
    private readonly bool _success;
    public readonly T Value;
    public readonly E Error;
    
    private Result(T value, E error, bool success) {
        _success = success;
        Value = value;
        Error = error;
    }

    public bool IsOk => _success;
    public bool IsErr => !_success;

    public static Result<T, E> Ok(T v) {
        return new(v, default, true);
    }

    public static Result<T, E> Err(E e) {
        return new(default, e, false);
    }
    
    public static implicit operator Result<T, E>(T v) => new(v, default, true);
    public static implicit operator Result<T, E>(E e) => new(default, e, false);
    
    public static implicit operator Result<T, E>(Result<T, Unit> other) => new(other.Value, default, other._success);
    public static implicit operator Result<T, E>(Result<Unit, E> other) => new(default, other.Error, other._success);

    public R Match<R>(Func<T, R> success, Func<E, R> failure) => IsOk ? success(Value) : failure(Error);
    public void Match(Action<T> success, Action<E> failure) => Inspect(success).InspectErr(failure);

    public Result<U, E> And<U>(U andVal) => IsOk ? Result.Ok(andVal) : Result.Err(Error);
    public Result<U, E> AndThen<U>(Func<T, Result<U, E>> andThen) => IsOk ? andThen(Value) : Result.Err(Error);
    public Result<T, F> OrElse<F>(Func<E, Result<T, F>> orElse) => IsOk ? Result.Ok(Value) : orElse(Error);

    public Result<T,E> Inspect(Action<T> inspect) {
        if (IsOk) { inspect(Value); };
        return this;
    }
    public Result<T,E> InspectErr(Action<E> inspectErr) {
        if (!IsOk) { inspectErr(Error); };
        return this;
    }

    public bool IsOkAnd(Func<T, bool> predicate) => IsOk ? predicate(Value) : false;
    public bool IsErrAnd(Func<E, bool> predicate) => IsErr ? predicate(Error) : false;
}