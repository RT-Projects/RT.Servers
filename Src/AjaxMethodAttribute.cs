using System;

namespace RT.Servers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AjaxMethodAttribute : Attribute
    {
    }
}
