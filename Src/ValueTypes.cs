using System;
using RT.Util.ExtensionMethods;

namespace RT.Servers;

/// <summary>
///     Encapsulates a value with a Q rating, where Q is between 0 and 1. Provides a comparer such that the values with Q = 1
///     are the smallest.</summary>
[Serializable]
public struct QValue<T>(float q, T value) : IComparable<QValue<T>>
{
    private float _q = q;
    private T _value = value;

    /// <summary>Gets the Q number</summary>
    public readonly float Q => _q;

    /// <summary>Gets the value via an implicit conversion</summary>
    public static implicit operator T(QValue<T> qv) => qv._value;

    /// <summary>Compares the Q number of this Q-value to the other one.</summary>
    public int CompareTo(QValue<T> other) => -_q.CompareTo(other._q);

    /// <summary>Converts the q value to a string.</summary>
    public override readonly string ToString() => $"{_value}; q={_q:0.0}";
}

/// <summary>Encapsulates a string value that can additionally be either weak or not.</summary>
[Serializable]
public struct WValue
{
    /// <summary>Gets or sets the value.</summary>
    public string Value { get; private set; }
    /// <summary>Gets or sets whether the value is “weak”.</summary>
    public bool Weak { get; private set; }
    /// <summary>Constructs a non-weak value.</summary>
    public WValue(string value) : this() { Value = value; Weak = false; }
    /// <summary>Constructor.</summary>
    public WValue(string value, bool weak) : this() { Value = value; Weak = weak; }
    /// <summary>Override; see base.</summary>
    public override readonly string ToString() => $@"{(Weak ? "W/" : "")}""{Value.CLiteralEscape()}""";
}
