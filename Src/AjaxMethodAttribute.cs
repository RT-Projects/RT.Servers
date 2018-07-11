using System;

namespace RT.Servers
{
    /// <summary>Specifies that a method can be called via AJAX using an <see cref="AjaxHandler{TApi}"/>.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AjaxMethodAttribute : Attribute
    {
    }
}
