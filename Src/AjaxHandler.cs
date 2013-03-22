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
    /// <typeparam name="TSession">
    ///     Type of session to use. Specify <see cref="Session"/> if you do not wish to use sessions.</typeparam>
    public sealed class AjaxHandler<TSession> where TSession : Session, ISessionEquatable<TSession>, new()
    {
        private readonly Dictionary<string, apiFunctionInfo> _apiFunctions;
        private readonly bool _returnExceptionMessages;

        /// <summary>
        ///     Constructs a new instance of <see cref="AjaxHandler{TSession}"/>.</summary>
        /// <param name="typeContainingAjaxMethods">
        ///     The type from which AJAX methods are taken. Methods must be static and have the <see cref="AjaxMethodAttribute"/>
        ///     on them.</param>
        /// <param name="returnExceptionMessages">
        ///     If true, exception messages contained in exceptions thrown by an AJAX method are returned to the client.</param>
        public AjaxHandler(Type typeContainingAjaxMethods, bool returnExceptionMessages)
        {
            _apiFunctions = new Dictionary<string, apiFunctionInfo>();
            _returnExceptionMessages = returnExceptionMessages;

            foreach (var method in typeContainingAjaxMethods.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).Where(m => m.IsDefined<AjaxMethodAttribute>()))
            {
                if (!method.IsStatic)
                    throw new InvalidOperationException("API function {0} is not static.".Fmt(method.Name));

                var parameterSetters = new List<Action<JsonDict, TSession, object[]>>();
                var parameters = method.GetParameters();
                var requireSession = false;

                for (int iFor = 0; iFor < parameters.Length; iFor++)
                {
                    var i = iFor;
                    var paramName = parameters[i].Name;
                    if (parameters[i].ParameterType == typeof(TSession))
                    {
                        requireSession = true;
                        parameterSetters.Add((dict, session, arr) => { arr[i] = session; });
                    }
                    else
                    {
                        parameterSetters.Add((dict, session, arr) => { arr[i] = ClassifyJson.Deserialize(parameters[i].ParameterType, dict[paramName]); });
                    }
                }

                _apiFunctions.Add(method.Name, new apiFunctionInfo(requireSession, (TSession session, HttpRequest req) =>
                {
                    var json = JsonDict.Parse(req.Post["data"].Value);
                    var arr = new object[parameters.Length];
                    foreach (var setter in parameterSetters)
                        setter(json, session, arr);
                    return ClassifyJson.Serialize(method.ReturnType, method.Invoke(null, arr));
                }));
            }
        }

        /// <summary>
        ///     Provides the handler for AJAX calls. Pass this to a <see cref="UrlPathHook"/>.</summary>
        public HttpResponse Handle(HttpRequest req)
        {
            try
            {
                var info = _apiFunctions[req.Post["apiFunction"].Value];
                var handler = Ut.Lambda((TSession session) => HttpResponse.Json(new JsonDict
                {
                    { "result", info.RequestHandler(session, req) },
                    { "status", "ok" }
                }));
                return info.RequiresSession ? Session.EnableAutomatic<TSession>(req, handler) : handler(null);
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

        private sealed class apiFunctionInfo
        {
            public Func<TSession, HttpRequest, JsonValue> RequestHandler { get; private set; }
            public bool RequiresSession { get; private set; }
            public apiFunctionInfo(bool requiresSession, Func<TSession, HttpRequest, JsonValue> requestHandler)
            {
                RequestHandler = requestHandler;
                RequiresSession = requiresSession;
            }
        }
    }
}
