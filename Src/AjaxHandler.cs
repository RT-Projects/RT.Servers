using System;
using RT.Util.ExtensionMethods;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RT.Util.Json;
using RT.Util;

namespace RT.Servers
{
    public sealed class AjaxHandler<TSession> where TSession : Session, new()
    {
        private readonly Dictionary<string, apiFunctionInfo> _apiFunctions;
        private bool _returnExceptionMessages;

        public AjaxHandler(Type typeContainingAjaxMethods, bool returnExceptionMessages)
        {
            _apiFunctions = new Dictionary<string, apiFunctionInfo>();
            _returnExceptionMessages = returnExceptionMessages;

            foreach (var method in typeContainingAjaxMethods.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).Where(m => m.IsDefined<AjaxMethodAttribute>()))
            {
                if (!method.IsStatic)
                    throw new InvalidOperationException("API function {0} is not static.".Fmt(method.Name));
                if (!typeof(JsonValue).IsAssignableFrom(method.ReturnType))
                    throw new InvalidOperationException("API function {0} has an unsupported return type ({1}). Only JsonValue (or a derived type) is supported.".Fmt(method.Name, method.ReturnType.FullName));

                var parameterSetters = new List<Action<JsonDict, TSession, object[]>>();
                var parameters = method.GetParameters();
                var requireSession = false;

                for (int iFor = 0; iFor < parameters.Length; iFor++)
                {
                    var i = iFor;
                    var paramName = parameters[i].Name;
                    if (parameters[i].ParameterType == typeof(int))
                        parameterSetters.Add((dict, session, arr) => { arr[i] = dict[paramName].GetInt(); });
                    else if (parameters[i].ParameterType == typeof(double))
                        parameterSetters.Add((dict, session, arr) => { arr[i] = dict[paramName].GetDouble(); });
                    else if (parameters[i].ParameterType == typeof(string))
                        parameterSetters.Add((dict, session, arr) => { arr[i] = dict[paramName].GetString(); });
                    else if (parameters[i].ParameterType == typeof(bool))
                        parameterSetters.Add((dict, session, arr) => { arr[i] = dict[paramName].GetBool(); });
                    else if (parameters[i].ParameterType == typeof(TSession))
                    {
                        requireSession = true;
                        parameterSetters.Add((dict, session, arr) => { arr[i] = session; });
                    }
                    else
                        throw new InvalidOperationException("The AJAX method {0} has a parameter {1} of type {2}, which is not supported.".Fmt(method.Name, parameters[i], parameters[i].ParameterType.FullName));
                }

                _apiFunctions.Add(method.Name, new apiFunctionInfo(requireSession, (TSession session, HttpRequest req) =>
                {
                    var json = JsonDict.Parse(req.Post["data"].Value);
                    var arr = new object[parameters.Length];
                    foreach (var setter in parameterSetters)
                        setter(json, session, arr);
                    return (JsonValue) method.Invoke(null, arr);
                }));
            }
        }

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
                return info.RequiresSession ? Session.Enable<TSession>(req, handler) : handler(null);
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
