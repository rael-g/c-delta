namespace System;

public struct Nullable<T> where T : struct
{
    private T _value;
    private bool _hasValue;

    public Nullable(T value)
    {
        _value = value;
        _hasValue = true;
    }

    public bool HasValue => _hasValue;

    public T Value => _value;

    public T GetValueOrDefault() => _hasValue ? _value : default;

    public T GetValueOrDefault(T defaultValue) => _hasValue ? _value : defaultValue;

    public static implicit operator T?(T value) => new T?(value);
}
