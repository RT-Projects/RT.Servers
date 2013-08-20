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
        ///     Provides the handler for AJAX calls. Pass this to a <see cref="UrlMapping"/>.</summary>
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

    /// <summary>Indicates that an error occurred during processing of an AJAX request.</summary>
    public class AjaxException : Exception
    {
        /// <summary>Constructor.</summary>
        public AjaxException() { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="message">
        ///     Exception message.</param>
        public AjaxException(string message) : base(message) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="message">
        ///     Exception message.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Indicates that an AJAX method was requested that does not exist.</summary>
    public class AjaxMethodNotFoundException : Exception
    {
        /// <summary>Gets the name of the requested method that does not exist.</summary>
        public string AjaxMethodName { get; private set; }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="ajaxMethodName">
        ///     Name of the requested method that does not exist.</param>
        public AjaxMethodNotFoundException(string ajaxMethodName) : this(ajaxMethodName, "The specified AJAX method {0} does not exist.".Fmt(ajaxMethodName), null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="ajaxMethodName">
        ///     Name of the requested method that does not exist.</param>
        /// <param name="message">
        ///     Exception message.</param>
        public AjaxMethodNotFoundException(string ajaxMethodName, string message) : this(ajaxMethodName, message, null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="ajaxMethodName">
        ///     Name of the requested method that does not exist.</param>
        /// <param name="message">
        ///     Exception message.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxMethodNotFoundException(string ajaxMethodName, string message, Exception inner)
            : base(message, inner)
        {
            AjaxMethodName = ajaxMethodName;
        }
    }

    /// <summary>Indicates that, during processing of an AJAX request, the value for a method parameter could not be deseralized.</summary>
    public class AjaxInvalidParameterException : Exception
    {
        /// <summary>Gets the name of the parameter that could not be deserialized.</summary>
        public string ParameterName { get; private set; }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterName">
        ///     Name of the parameter that could not be deserialized.</param>
        public AjaxInvalidParameterException(string parameterName) : this(parameterName, (Exception) null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterName">
        ///     Name of the parameter that could not be deserialized.</param>
        /// <param name="message">
        ///     Exception message.</param>
        public AjaxInvalidParameterException(string parameterName, string message) : this(parameterName, message, null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterName">
        ///     Name of the parameter that could not be deserialized.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxInvalidParameterException(string parameterName, Exception inner) : this(parameterName, "The parameter {0} could not be deseralized.".Fmt(parameterName), inner) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterName">
        ///     Name of the parameter that could not be deserialized.</param>
        /// <param name="message">
        ///     Exception message.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxInvalidParameterException(string parameterName, string message, Exception inner)
            : base(message, inner)
        {
            ParameterName = parameterName;
        }
    }

    /// <summary>Indicates that, during processing of an AJAX request, the data containing the method parameters could not be parsed.</summary>
    public class AjaxInvalidParameterDataException : Exception
    {
        /// <summary>Gets the raw data that could not be parsed.</summary>
        public string ParameterData { get; private set; }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterData">
        ///     The raw data that could not be parsed.</param>
        public AjaxInvalidParameterDataException(string parameterData) : this(parameterData, (Exception) null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterData">
        ///     The raw data that could not be parsed.</param>
        /// <param name="message">
        ///     Exception message.</param>
        public AjaxInvalidParameterDataException(string parameterData, string message) : this(parameterData, message, null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterData">
        ///     The raw data that could not be parsed.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxInvalidParameterDataException(string parameterData, Exception inner) : this(parameterData, "The provided JSON for the method paramters is not a valid JSON dictionary.", inner) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterData">
        ///     The raw data that could not be parsed.</param>
        /// <param name="message">
        ///     Exception message.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxInvalidParameterDataException(string parameterData, string message, Exception inner)
            : base(message, inner)
        {
            ParameterData = parameterData;
        }
    }

    /// <summary>Indicates that the value returned by an AJAX method could not be serialized.</summary>
    public class AjaxInvalidReturnValueException : Exception
    {
        /// <summary>Gets the return value that could not be serialized.</summary>
        public object ReturnValue { get; private set; }
        /// <summary>Gets the return type of the relevant AJAX method.</summary>
        public Type ReturnType { get; private set; }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="returnValue">
        ///     The return value that could not be serialized.</param>
        /// <param name="returnType">
        ///     The return type of the relevant AJAX method.</param>
        public AjaxInvalidReturnValueException(object returnValue, Type returnType) : this(returnValue, returnType, (Exception) null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="returnValue">
        ///     The return value that could not be serialized.</param>
        /// <param name="returnType">
        ///     The return type of the relevant AJAX method.</param>
        /// <param name="message">
        ///     Exception message.</param>
        public AjaxInvalidReturnValueException(object returnValue, Type returnType, string message) : this(returnValue, returnType, message, null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="returnValue">
        ///     The return value that could not be serialized.</param>
        /// <param name="returnType">
        ///     The return type of the relevant AJAX method.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxInvalidReturnValueException(object returnValue, Type returnType, Exception inner) : this(returnValue, returnType, "The return value of the AJAX call could not be serialized.", inner) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="returnValue">
        ///     The return value that could not be serialized.</param>
        /// <param name="returnType">
        ///     The return type of the relevant AJAX method.</param>
        /// <param name="message">
        ///     Exception message.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxInvalidReturnValueException(object returnValue, Type returnType, string message, Exception inner)
            : base(message, inner)
        {
            ReturnValue = returnValue;
            ReturnType = returnType;
        }
    }
}
