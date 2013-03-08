using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Json;

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
                if (!typeof(JsonValue).IsAssignableFrom(method.ReturnType))
                    throw new InvalidOperationException("API function {0} has an unsupported return type ({1}). Only JsonValue (or a derived type) is supported.".Fmt(method.Name, method.ReturnType.FullName));

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
                        var converter = getConverter(parameters[i].ParameterType, method.Name, paramName);
                        parameterSetters.Add((dict, session, arr) => { arr[i] = converter(dict[paramName]); });
                    }
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

        public Func<JsonValue, object> getConverter(Type targetType, string methodName, string parameterName)
        {
            Type[] types;

            if (targetType == typeof(int))
                return val => val.GetInt();
            else if (targetType == typeof(long))
                return val => val.GetLong();
            else if (targetType == typeof(double))
                return val => val.GetDouble();
            else if (targetType == typeof(decimal))
                return val => val.GetDecimal();
            else if (targetType == typeof(bool))
                return val => val.GetBool();
            else if (targetType == typeof(string))
                return val => val.GetString();
            else if (typeof(JsonValue).IsAssignableFrom(targetType))
                return val => val;
            else if (targetType.TryGetInterfaceGenericParameters(typeof(ICollection<>), out types))
            {
                var innerType = types[0];
                var innerConverter = getConverter(innerType, methodName, parameterName);
                var constructor = targetType.GetConstructor(Type.EmptyTypes);
                if (constructor == null)
                    throw new InvalidOperationException("The AJAX method {0} has a parameter {1} which uses the type {2}, which does not have a default constructor.".Fmt(methodName, parameterName, targetType.FullName));
                var addMethod = typeof(ICollection<>).MakeGenericType(types).GetMethod("Add");
                return val =>
                {
                    var list = constructor.Invoke(null);
                    foreach (var item in val.GetList())
                        addMethod.Invoke(list, new object[] { innerConverter(item) });
                    return list;
                };
            }
            else if (targetType.IsArray || (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                var innerType = targetType.IsArray ? targetType.GetElementType() : targetType.GetGenericArguments()[0];
                var innerConverter = getConverter(innerType, methodName, parameterName);
                var castMethod = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(innerType);
                var toArrayMethod = typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(innerType);
                return val => toArrayMethod.Invoke(null, new object[] { castMethod.Invoke(null, new object[] { val.GetList().Select(innerConverter) }) });
            }
            else if (targetType.TryGetInterfaceGenericParameters(typeof(List<>), out types))
            {
                var innerType = types[0];
                var innerConverter = getConverter(innerType, methodName, parameterName);
                var castMethod = typeof(Enumerable).GetMethod("Cast").MakeGenericMethod(innerType);
                return val => castMethod.Invoke(null, new object[] { val.GetList().Select(innerConverter) });
            }
            else
                throw new InvalidOperationException("The AJAX method {0} has a parameter {1} which uses the type {2}, which is not supported.".Fmt(methodName, parameterName, targetType.FullName));
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
