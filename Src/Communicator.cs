using System;
using RT.Util.ExtensionMethods;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RT.Util.Json;
using RT.Util.Serialization;
using RT.Util;

namespace RT.Servers
{
    public static class Communicator
    {
        private static List<object> _activeObjects = new List<object>();

        private static string mangledMethodName(MethodInfo m)
        {
            return "{0}({1})".Fmt(m.Name, m.GetParameters().Select(p => p.ParameterType.AssemblyQualifiedName).JoinString("; "));
        }

        private static Dictionary<string, MethodInfo> getMethodDictionary(Type t)
        {
            return t.GetMethods(BindingFlags.Public | BindingFlags.Instance).ToDictionary(mangledMethodName);
        }

        public static Func<HttpRequest, HttpResponse> CreateSingletonHandler<T>(T instance)
        {
            var methods = getMethodDictionary(typeof(T));

            return req =>
            {
                if (req.Method != HttpMethod.Post)
                    throw new HttpException(HttpStatusCode._400_BadRequest, userMessage: "POST request expected.");

                MethodInfo method;
                if (!methods.TryGetValue(req.Post["Method"].Value, out method))
                    throw new HttpException(HttpStatusCode._400_BadRequest, userMessage: "The method does not exist.");

                var parameters = method.GetParameters();
                object[] arguments = new object[parameters.Length];
                var argumentsRaw = JsonList.Parse(req.Post["Arguments"].Value);
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].IsOut)
                        continue;
                    var remoteId = argumentsRaw[i].Safe[":remoteid"].GetIntSafe();
                    if (remoteId != null)
                    {
                        lock (_activeObjects)
                            arguments[i] = _activeObjects[remoteId.Value];
                    }
                    else
                        arguments[i] = ClassifyJson.Deserialize(parameters[i].ParameterType.IsByRef ? parameters[i].ParameterType.GetElementType() : parameters[i].ParameterType, argumentsRaw[i]);
                }

                var returnValue = method.Invoke(instance, arguments);

                var process = Ut.Lambda((ParameterInfo param, object value) =>
                {
                    if (!param.IsDefined<RemoteAttribute>())
                        return ClassifyJson.Serialize(param.ParameterType, value);

                    lock (_activeObjects)
                    {
                        var remoteId = _activeObjects.IndexOf(value);
                        if (remoteId == -1)
                        {
                            remoteId = _activeObjects.Count;
                            _activeObjects.Add(value);
                        }
                        return new JsonDict { { ":remoteid", remoteId } };
                    }
                });

                var returnValueRaw = process(method.ReturnParameter, returnValue);
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType.IsByRef)
                        argumentsRaw[i] = process(parameters[i], arguments[i]);
                    else
                        argumentsRaw[i] = null;
                }

                return HttpResponse.Json(ClassifyJson.Serialize(new CommunicatorResultReturn(returnValueRaw, argumentsRaw)));
            };
        }
    }

    public enum CommunicatorStatus { Return, Throw, Invoke }

    public abstract class CommunicatorResult
    {
        public abstract CommunicatorStatus Status { get; }
    }
    public sealed class CommunicatorResultReturn : CommunicatorResult
    {
        public override CommunicatorStatus Status { get { return CommunicatorStatus.Return; } }
        public JsonValue ReturnValue { get; private set; }
        public JsonList RefOutArguments { get; private set; }
        public CommunicatorResultReturn(JsonValue returnValue, JsonList refOutArgs)
        {
            ReturnValue = returnValue;
            RefOutArguments = refOutArgs;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false)]
    public sealed class RemoteAttribute : Attribute { }
}
