using System;

namespace RT.Servers
{
    /// <summary>Specifies that a method can be called via AJAX using an <see cref="AjaxHandler{TApi}"/>.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AjaxMethodAttribute : Attribute
    {
    }

    /// <summary>
    ///     Specifies that <see cref="AjaxHandler{TApi}"/> should use this method to convert serialized values from one type
    ///     to another before calling an AJAX method.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AjaxConverterAttribute : Attribute
    {
    }
}
