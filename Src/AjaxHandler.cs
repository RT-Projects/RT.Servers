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
        private readonly AjaxHandlerOptions _options;

        /// <summary>
        ///     Constructs a new instance of <see cref="AjaxHandler{TApi}"/>.</summary>
        /// <param name="options">
        ///     Specifies <see cref="AjaxHandler{TApi}"/>’s exception behaviour.</param>
        public AjaxHandler(AjaxHandlerOptions options = AjaxHandlerOptions.ReturnExceptionsWithoutMessages)
        {
            _apiFunctions = new Dictionary<string, Func<HttpRequest, TApi, JsonValue>>();
            _options = options;

            var typeContainingAjaxMethods = typeof(TApi);
            foreach (var method in typeContainingAjaxMethods.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.IsDefined<AjaxMethodAttribute>()))
            {
                var parameters = method.GetParameters();
                var returnType = method.ReturnType;

                _apiFunctions.Add(method.Name, (req, api) =>
                {
                    JsonDict json;
                    var rawJson = req.Post["data"].Value;
                    try { json = JsonDict.Parse(rawJson); }
                    catch (Exception e) { throw new AjaxInvalidParameterDataException(rawJson, e); }
                    var arr = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramName = parameters[i].Name;
                        try { arr[i] = ClassifyJson.Deserialize(parameters[i].ParameterType, json[paramName]); }
                        catch (Exception e) { throw new AjaxInvalidParameterException(paramName, e); }
                    }

                    object result;
                    try { result = method.Invoke(api, arr); }
                    catch (Exception e) { throw new AjaxException("Error invoking the AJAX method.", e); }

                    try { return ClassifyJson.Serialize(returnType, result); }
                    catch (Exception e) { throw new AjaxInvalidReturnValueException(result, returnType, e); }
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
            if (_options == AjaxHandlerOptions.PropagateExceptions)
                return callApiFunction(req, api);

            try
            {
                return callApiFunction(req, api);
            }
            catch (Exception e)
            {
                while (e is TargetInvocationException)
                    e = e.InnerException;

                var error = new JsonDict { { "status", "error" } };
                if (_options == AjaxHandlerOptions.ReturnExceptionsWithMessages)
                    error.Add("message", "{0} ({1})".Fmt(e.Message, e.GetType().Name));
                return HttpResponse.Json(error);
            }
        }

        private HttpResponse callApiFunction(HttpRequest req, TApi api)
        {
            var apiFunctionName = req.Post["apiFunction"].Value;
            if (apiFunctionName == null || !_apiFunctions.ContainsKey(apiFunctionName))
                throw new AjaxMethodNotFoundException(apiFunctionName);
            var result = _apiFunctions[apiFunctionName](req, api);
            return HttpResponse.Json(new JsonDict
            {
                { "result", result },
                { "status", "ok" }
            });
        }
    }

    /// <summary>Specifies the exception behaviour for <see cref="AjaxHandler{TApi}"/>.</summary>
    public enum AjaxHandlerOptions
    {
        /// <summary>
        ///     Exceptions thrown by an AJAX method (or the wrapper) are returned to the client as simply <c>{ "status": "error"
        ///     }</c>.</summary>
        ReturnExceptionsWithoutMessages,
        /// <summary>
        ///     Exceptions thrown by an AJAX method (or the wrapper) are returned to the client including their exception messages
        ///     as <c>{ "status": "error", "message": "{0} ({1})" }</c>, where <c>{0}</c> is the exception message and <c>{1}</c>
        ///     the exception type.</summary>
        ReturnExceptionsWithMessages,
        /// <summary>
        ///     <see cref="AjaxHandler{TApi}"/> does not catch exceptions thrown by the API functions. If you do not catch the
        ///     exceptions either, they will bring down the server.</summary>
        PropagateExceptions
    }

    public class AjaxException : Exception
    {
        public AjaxException() { }
        public AjaxException(string message) : base(message) { }
        public AjaxException(string message, Exception inner) : base(message, inner) { }
    }

    public class AjaxMethodNotFoundException : Exception
    {
        public string AjaxMethodName { get; private set; }

        public AjaxMethodNotFoundException(string ajaxMethodName) : this(ajaxMethodName, "The specified AJAX method {0} does not exist.".Fmt(ajaxMethodName), null) { }
        public AjaxMethodNotFoundException(string ajaxMethodName, string message) : this(ajaxMethodName, message, null) { }
        public AjaxMethodNotFoundException(string ajaxMethodName, string message, Exception inner)
            : base(message, inner)
        {
            AjaxMethodName = ajaxMethodName;
        }
    }

    public class AjaxInvalidParameterException : Exception
    {
        public string ParameterName { get; private set; }

        public AjaxInvalidParameterException(string parameterName) : this(parameterName, (Exception) null) { }
        public AjaxInvalidParameterException(string parameterName, string message) : this(parameterName, message, null) { }
        public AjaxInvalidParameterException(string parameterName, Exception inner) : this(parameterName, "The parameter {0} could not be deseralized.".Fmt(parameterName), inner) { }
        public AjaxInvalidParameterException(string parameterName, string message, Exception inner)
            : base(message, inner)
        {
            ParameterName = parameterName;
        }
    }

    public class AjaxInvalidParameterDataException : Exception
    {
        public string ParameterData { get; private set; }

        public AjaxInvalidParameterDataException(string parameterData) : this(parameterData, (Exception) null) { }
        public AjaxInvalidParameterDataException(string parameterData, string message) : this(parameterData, message, null) { }
        public AjaxInvalidParameterDataException(string parameterData, Exception inner) : this(parameterData, "The provided JSON for the method paramters is not a valid JSON dictionary.", inner) { }
        public AjaxInvalidParameterDataException(string parameterData, string message, Exception inner)
            : base(message, inner)
        {
            ParameterData = parameterData;
        }
    }

    public class AjaxInvalidReturnValueException : Exception
    {
        public object ReturnValue { get; private set; }
        public Type ReturnType { get; private set; }

        public AjaxInvalidReturnValueException(object returnValue, Type returnType) : this(returnValue, returnType, (Exception) null) { }
        public AjaxInvalidReturnValueException(object returnValue, Type returnType, string message) : this(returnValue, returnType, message, null) { }
        public AjaxInvalidReturnValueException(object returnValue, Type returnType, Exception inner) : this(returnValue, returnType, "The return value of the AJAX call could not be serialized.", inner) { }
        public AjaxInvalidReturnValueException(object returnValue, Type returnType, string message, Exception inner)
            : base(message, inner)
        {
            ReturnValue = returnValue;
            ReturnType = returnType;
        }
    }
}
