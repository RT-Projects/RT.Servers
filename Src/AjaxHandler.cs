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
    public sealed class AjaxHandler<TSession> where TSession : Session, new()
    {
        private readonly Dictionary<string, apiFunctionInfo> _apiFunctions;
        private readonly bool _returnExceptionMessages;

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
                        parameterSetters.Add((dict, session, arr) => { arr[i] = Classify.ObjectFromElement(parameters[i].ParameterType, dict[paramName], ClassifyFormats.Json); });
                    }
                }

                _apiFunctions.Add(method.Name, new apiFunctionInfo(requireSession, (TSession session, HttpRequest req) =>
                {
                    var json = JsonDict.Parse(req.Post["data"].Value);
                    var arr = new object[parameters.Length];
                    foreach (var setter in parameterSetters)
                        setter(json, session, arr);
                    return Classify.ObjectToElement(method.ReturnType, method.Invoke(null, arr), ClassifyFormats.Json);
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
