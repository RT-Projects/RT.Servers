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
        private readonly Dictionary<string, Func<HttpRequest, TApi, JsonValue>> _apiFunctions;
        private readonly bool _returnExceptionMessages;
        private readonly Func<Func<JsonValue>, JsonValue> _wrapper;

        /// <summary>
        ///     Constructs a new instance of <see cref="AjaxHandler{TApi}"/>.</summary>
        /// <param name="returnExceptionMessages">
        ///     If true, exception messages contained in exceptions thrown by an AJAX method are returned to the client.</param>
        /// <param name="wrapper">
        ///     If not <c>null</c>, provides a function in which to wrap every API function call.</param>
        public AjaxHandler(bool returnExceptionMessages, Func<Func<JsonValue>, JsonValue> wrapper = null)
        {
            _apiFunctions = new Dictionary<string, Func<HttpRequest, TApi, JsonValue>>();
            _returnExceptionMessages = returnExceptionMessages;
            _wrapper = wrapper;

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

                _apiFunctions.Add(method.Name, (req, api) =>
                {
                    var json = JsonDict.Parse(req.Post["data"].Value);
                    var arr = new object[parameters.Length];
                    foreach (var setter in parameterSetters)
                        setter(json, arr);
                    return ClassifyJson.Serialize(method.ReturnType, method.Invoke(api, arr));
                });
            }
        }

        /// <summary>
        ///     Provides the handler for AJAX calls. Pass this to a <see cref="UrlPathHook"/>.</summary>
        /// <param name="req">
        ///     The incoming HTTP POST request to be handled, containing the API function name and parameters.</param>
        /// <param name="api">
        ///     The API object on which the API function is to be invoked.</param>
        public HttpResponse Handle(HttpRequest req, TApi api)
        {
            try
            {
                var apiFunction = _apiFunctions[req.Post["apiFunction"].Value];
                return HttpResponse.Json(new JsonDict
                {
                    { "result", _wrapper == null ? apiFunction(req, api) : _wrapper(() => apiFunction(req, api)) },
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
