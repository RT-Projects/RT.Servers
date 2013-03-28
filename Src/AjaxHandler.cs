using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Json;
using RT.Util.Serialization;

namespace RT.Servers
{
    /// <summary>
    ///     Provides a means to call methods decorated with an <see cref="AjaxMethodAttribute"/> via AJAX, using JSON as the data
    ///     interchange format.</summary>
    /// <typeparam name="TApi">
    ///     Type of the object containing the Ajax methods to use.</typeparam>
    public sealed class AjaxHandler<TApi>
    {
        private readonly Dictionary<string, Func<HttpRequest, JsonValue>> _apiFunctions;
        private readonly bool _returnExceptionMessages;

        /// <summary>
        ///     Constructs a new instance of <see cref="AjaxHandler{TApi}"/>.</summary>
        /// <param name="objContainingAjaxMethods">
        ///     The object instance of type \<c>TApi</c> on which the AJAX methods are called. Methods must be public instance methods and have the <see cref="AjaxMethodAttribute"/>
        ///     on them.</param>
        /// <param name="returnExceptionMessages">
        ///     If true, exception messages contained in exceptions thrown by an AJAX method are returned to the client.</param>
        public AjaxHandler(TApi objContainingAjaxMethods, bool returnExceptionMessages)
        {
            _apiFunctions = new Dictionary<string, Func<HttpRequest, JsonValue>>();
            _returnExceptionMessages = returnExceptionMessages;
            var typeContainingAjaxMethods = typeof(TApi);
            foreach (var method in typeContainingAjaxMethods.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.IsDefined<AjaxMethodAttribute>()))
            {
                var parameterSetters = new List<Action<JsonDict, object[]>>();
                var parameters = method.GetParameters();

                for (int iFor = 0; iFor < parameters.Length; iFor++)
                {
                    var i = iFor;
                    var paramName = parameters[i].Name;
                    parameterSetters.Add((dict, arr) => { arr[i] = ClassifyJson.Deserialize(parameters[i].ParameterType, dict[paramName]); });
                }

                _apiFunctions.Add(method.Name, req =>
                {
                    var json = JsonDict.Parse(req.Post["data"].Value);
                    var arr = new object[parameters.Length];
                    foreach (var setter in parameterSetters)
                        setter(json, arr);
                    return ClassifyJson.Serialize(method.ReturnType, method.Invoke(objContainingAjaxMethods, arr));
                });
            }
        }

        /// <summary>Provides the handler for AJAX calls. Pass this to a <see cref="UrlPathHook"/>.</summary>
        public HttpResponse Handle(HttpRequest req)
        {
            try
            {
                var apiFunction = _apiFunctions[req.Post["apiFunction"].Value];
                return HttpResponse.Json(new JsonDict
                {
                    { "result", apiFunction(req) },
                    { "status", "ok" }
                });
            }
            catch (Exception e)
            {
                while (e is TargetInvocationException)
                    e = e.InnerException;

                var error = new JsonDict { { "status", "error" } };
                if (_returnExceptionMessages)
                    error.Add("message", "{0} ({1})".Fmt(e.Message, e.GetType().Name));
                return HttpResponse.Json(error);
            }
        }
    }
}
