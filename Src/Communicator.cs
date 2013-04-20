using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Json;
using RT.Util.Serialization;

namespace RT.Servers
{
    /// <summary>
    ///     Provides functionality to host objects on a remote server, and to access them from a client via a transparent proxy
    ///     object.</summary>
    public static class Communicator
    {
        private static List<object> _activeObjects = new List<object>();

        /// <summary>
        ///     Creates an HTTP request handler that responds to requests from clients to execute methods on the specified factory
        ///     object as well as any remote objects generated from it.</summary>
        /// <typeparam name="T">
        ///     The type containing the factory methods. If the client is going to access the factory methods via an interface,
        ///     this should be that interface type rather than the concrete type of <paramref
        ///     name="factoryInstance"/>.</typeparam>
        /// <param name="factoryInstance">
        ///     An instance that implements or derives from <typeparamref name="T"/> which provides the factory functionality to
        ///     be accessed by clients.</param>
        /// <returns>
        ///     An HTTP handler that can be used in <see cref="HttpServer"/> or <see cref="UrlPathResolver"/>.</returns>
        public static Func<HttpRequest, HttpResponse> CreateHandlerFromFactory<T>(T factoryInstance)
        {
            var thisType = typeof(T);
            var methods = new Dictionary<Type, Dictionary<string, MethodInfo>> { { thisType, getMethodDictionary(typeof(T)) } };

            return req =>
            {
                if (req.Method != HttpMethod.Post)
                    throw new HttpException(HttpStatusCode._400_BadRequest, userMessage: "POST request expected.");

                var objectId = req.Post["ObjectID"].Value.NullOr(Convert.ToInt32);
                var instance = objectId == null ? factoryInstance : _activeObjects[objectId.Value];
                var type = objectId == null ? thisType : instance.GetType();

                Dictionary<string, MethodInfo> methodDict;
                if (!methods.TryGetValue(type, out methodDict))
                    methods[type] = methodDict = getMethodDictionary(type);

                MethodInfo method;
                if (!methodDict.TryGetValue(req.Post["Method"].Value, out method))
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
                        // TODO: Delegates
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

                return HttpResponse.Json(ClassifyJson.Serialize<CommunicatorResult>(new CommunicatorResultReturn(returnValueRaw, argumentsRaw)));
            };
        }

        private static string mangledMethodName(MethodInfo m)
        {
            return "{0}({1})".Fmt(m.Name, m.GetParameters().Select(p => p.ParameterType.AssemblyQualifiedName).JoinString("; "));
        }

        private static Dictionary<string, MethodInfo> getMethodDictionary(Type t)
        {
            return t.GetMethods(BindingFlags.Public | BindingFlags.Instance).ToDictionary(mangledMethodName);
        }

        /// <summary>
        ///     Returns an object of the specified type which can remotely access factory methods at the specified URL.</summary>
        /// <typeparam name="T">
        ///     Type of the factory interface.</typeparam>
        /// <param name="url">
        ///     URL at which an HTTP server is handling Communicator requests for the required object.</param>
        /// <returns>
        ///     A transparent proxy for the requested remote factory object.</returns>
        public static T CreateFactory<T>(string url)
        {
            return (T) new proxy(typeof(T), url, -1).GetTransparentProxy();
        }

        private sealed class proxy : RealProxy
        {
            private string _url;
            private int _objectId;
            private HClient _client = new HClient();

            public proxy(Type t, string url, int objectId) : base(t) { _url = url; _objectId = objectId; }

            public override IMessage Invoke(IMessage rawMsg)
            {
                var msg = (IMethodCallMessage) rawMsg;
                var method = msg.MethodBase as MethodInfo;
                var args = msg.Args;

                if (method == null || args == null)
                    throw new InternalErrorException("The transparent proxy received an invalid message.");

                var parameters = method.GetParameters();

                var hArgs = new List<HArg>{
                    new HArg("Method", mangledMethodName(method)),
                    new HArg("Arguments", new JsonList(args.Select((arg, i) =>
                        parameters[i].IsOut ? null :
                        arg is Delegate ? new JsonDict { { ":delegate", true } } :
                        ClassifyJson.Serialize(parameters[i].ParameterType, arg))))
                };
                if (_objectId != -1)
                    hArgs.Add(new HArg("ObjectID", _objectId));
                var responseRaw = _client.Post(_url, hArgs.ToArray());
                var response = ClassifyJson.Deserialize<CommunicatorResult>(responseRaw.DataJson);

                var responseRet = response as CommunicatorResultReturn;
                if (responseRet != null)
                {
                    var refOut = Enumerable.Range(0, parameters.Length)
                        .Where(i => parameters[i].ParameterType.IsByRef)
                        .Select(i => translate(parameters[i].ParameterType.GetElementType(), responseRet.RefOutArguments[i]))
                        .ToArray();
                    return new ReturnMessage(
                        translate(method.ReturnType, responseRet.ReturnValue),
                        refOut,
                        refOut.Length,
                        msg.LogicalCallContext,
                        msg);
                }

                throw new NotImplementedException();
            }

            private object translate(Type targetType, JsonValue value)
            {
                var remoteId = value.Safe[":remoteid"].GetIntSafe();
                if (remoteId != null)
                    return new proxy(targetType, _url, remoteId.Value).GetTransparentProxy();
                return ClassifyJson.Deserialize(targetType, value);
            }
        }
    }

    internal enum CommunicatorStatus { Return, Throw, Invoke }

    internal abstract class CommunicatorResult
    {
        public abstract CommunicatorStatus Status { get; }
    }
    internal sealed class CommunicatorResultReturn : CommunicatorResult
    {
        public override CommunicatorStatus Status { get { return CommunicatorStatus.Return; } }
        public JsonValue ReturnValue { get; private set; }
        public JsonList RefOutArguments { get; private set; }
        public CommunicatorResultReturn(JsonValue returnValue, JsonList refOutArgs)
        {
            ReturnValue = returnValue;
            RefOutArguments = refOutArgs;
        }
        // For Classify
        private CommunicatorResultReturn() { }
    }

    /// <summary>
    ///     Indicates that the value returned by a method return value or <c>out</c> or <c>ref</c> parameter should be treated as
    ///     a new remote object managed by <see cref="Communicator"/>. (Without this attribute, the default behavior is to
    ///     serialize the object.)</summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false)]
    public sealed class RemoteAttribute : Attribute { }
}
