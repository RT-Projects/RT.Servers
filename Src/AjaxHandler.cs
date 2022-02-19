using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Json;
using RT.Serialization;

namespace RT.Servers
{
    /// <summary>
    ///     Provides a means to call methods decorated with an <see cref="AjaxMethodAttribute"/> via AJAX, using JSON as the
    ///     data interchange format.</summary>
    /// <typeparam name="TApi">
    ///     Type of the object containing the Ajax methods to use.</typeparam>
    public sealed class AjaxHandler<TApi>
    {
        private readonly Dictionary<string, Func<HttpRequest, TApi, JsonValue>> _apiFunctions = new Dictionary<string, Func<HttpRequest, TApi, JsonValue>>();
        private readonly Dictionary<Type, Dictionary<string, (Type serializedType, Func<object, TApi, object> converter)>> _converterFunctions = new Dictionary<Type, Dictionary<string, (Type serializedType, Func<object, TApi, object> converter)>>();
        private readonly AjaxHandlerOptions _options;

        /// <summary>
        ///     Constructs a new instance of <see cref="AjaxHandler{TApi}"/>.</summary>
        /// <param name="options">
        ///     Specifies <see cref="AjaxHandler{TApi}"/>’s exception behaviour.</param>
        public AjaxHandler(AjaxHandlerOptions options = AjaxHandlerOptions.ReturnExceptionsWithoutMessages)
        {
            _options = options;

            var typeContainingAjaxMethods = typeof(TApi);
            foreach (var method in typeContainingAjaxMethods.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.IsDefined<AjaxMethodAttribute>())
                {
                    var parameters = method.GetParameters();
                    var returnType = method.ReturnType;

                    foreach (var parameter in parameters)
                        if (parameter.GetCustomAttribute<AjaxRequestAttribute>() != null && !parameter.ParameterType.IsAssignableFrom(typeof(HttpRequest)))
                            throw new AjaxMethodInvalidException(method.Name, $"The parameter {parameter.Name} has {nameof(AjaxRequestAttribute)} but does not accept an object of type {nameof(HttpRequest)}.");

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
                            if (parameters[i].GetCustomAttribute<AjaxRequestAttribute>() != null)
                                arr[i] = req;
                            else if (parameters[i].IsOptional && !json.ContainsKey(paramName))
                                arr[i] = parameters[i].DefaultValue;
                            else if (!json.ContainsKey(paramName))
                                throw new AjaxMissingParameterException(paramName);
                            else if (_converterFunctions.TryGetValue(parameters[i].ParameterType, out var inner) && (inner.TryGetValue(paramName, out var tup) || inner.TryGetValue("*", out tup)))
                            {
                                object deserialized;
                                try { deserialized = ClassifyJson.Deserialize(tup.serializedType, json[paramName]); }
                                catch (Exception e) { throw new AjaxInvalidParameterException(paramName, json[paramName], tup.serializedType, e); }
                                arr[i] = tup.converter(deserialized, api);
                            }
                            else
                            {
                                try { arr[i] = ClassifyJson.Deserialize(parameters[i].ParameterType, json[paramName]); }
                                catch (Exception e) { throw new AjaxInvalidParameterException(paramName, json[paramName], parameters[i].ParameterType, e); }
                            }
                        }

                        object result;
                        if (_options == AjaxHandlerOptions.PropagateExceptions)
                            result = method.InvokeDirect(api, arr);
                        else
                            try { result = method.Invoke(api, arr); }
                            catch (Exception e) { throw new AjaxException("Error invoking the AJAX method.", e); }

                        if (result is JsonValue value)
                            return value;

                        try { return ClassifyJson.Serialize(returnType, result); }
                        catch (Exception e) { throw new AjaxInvalidReturnValueException(result, returnType, e); }
                    });
                }

                var converterAttr = method.GetCustomAttribute<AjaxConverterAttribute>();
                if (converterAttr != null)
                {
                    var ps = method.GetParameters();
                    if (ps.Length != 1 || method.ReturnType == typeof(void))
                        throw new InvalidOperationException($"A method with an [AjaxConverter] attribute may have only one parameter, and must have a return type other than void. (Method: {method.DeclaringType.FullName}.{method.Name})");
                    if (!_converterFunctions.TryGetValue(method.ReturnType, out var inner))
                        inner = _converterFunctions[method.ReturnType] = new Dictionary<string, (Type serializedType, Func<object, TApi, object> converter)>();
                    if (inner.ContainsKey(ps[0].Name))
                        throw new InvalidOperationException($"There is a duplicate conversion for “{ps[0].Name}” to type “{method.ReturnType.FullName}”. (Method: {method.DeclaringType.FullName}.{method.Name})");
                    inner[ps[0].Name] = (ps[0].ParameterType, new Func<object, TApi, object>((obj, api) =>
                    {
                        if (_options == AjaxHandlerOptions.PropagateExceptions)
                            return method.InvokeDirect(api, obj);
                        try { return method.Invoke(api, new[] { obj }); }
                        catch (Exception e) { throw new AjaxException("Error invoking an AJAX conversion method.", e); }
                    }));
                }
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
                return HttpResponse.Json(error, (e as AjaxException)?.Status ?? HttpStatusCode._400_BadRequest);
            }
        }

        private HttpResponse callApiFunction(HttpRequest req, TApi api)
        {
            var apiFunctionName = req.Url.Path.SubstringSafe(1);
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
        ///     Exceptions thrown by an AJAX method (or the wrapper) are returned to the client as simply <c>{ "status":
        ///     "error" }</c>.</summary>
        ReturnExceptionsWithoutMessages,
        /// <summary>
        ///     Exceptions thrown by an AJAX method (or the wrapper) are returned to the client including their exception
        ///     messages as <c>{ "status": "error", "message": "{0} ({1})" }</c>, where <c>{0}</c> is the exception message
        ///     and <c>{1}</c> the exception type.</summary>
        ReturnExceptionsWithMessages,
        /// <summary>
        ///     <see cref="AjaxHandler{TApi}"/> does not catch exceptions thrown by the API functions. If you do not catch the
        ///     exceptions either, they will bring down the server.</summary>
        PropagateExceptions
    }

    /// <summary>Indicates that an error occurred during processing of an AJAX request.</summary>
    public class AjaxException : Exception
    {
        /// <summary>HTTP status code.</summary>
        public HttpStatusCode Status { get; private set; } = HttpStatusCode._400_BadRequest;

        /// <summary>Constructor.</summary>
        /// <param name="status">
        ///     HTTP status code.</param>
        public AjaxException(HttpStatusCode status = HttpStatusCode._400_BadRequest) { Status = status; }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="message">
        ///     Exception message.</param>
        /// <param name="status">
        ///     HTTP status code.</param>
        public AjaxException(string message, HttpStatusCode status = HttpStatusCode._400_BadRequest) : base(message) { Status = status; }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="message">
        ///     Exception message.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        /// <param name="status">
        ///     HTTP status code.</param>
        public AjaxException(string message, Exception inner, HttpStatusCode status = HttpStatusCode._400_BadRequest) : base(message, inner) { Status = status; }
    }

    /// <summary>Indicates that an AJAX method declaration is invalid.</summary>
    public class AjaxMethodInvalidException : AjaxException
    {
        /// <summary>Gets the name of the requested method that is invalid.</summary>
        public string AjaxMethodName { get; private set; }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="ajaxMethodName">
        ///     Name of the requested method that is invalid.</param>
        public AjaxMethodInvalidException(string ajaxMethodName) : this(ajaxMethodName, "The declaration of the specified AJAX method {0} is invalid.".Fmt(ajaxMethodName), null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="ajaxMethodName">
        ///     Name of the requested method that is invalid.</param>
        /// <param name="message">
        ///     Exception message.</param>
        public AjaxMethodInvalidException(string ajaxMethodName, string message) : this(ajaxMethodName, message, null) { }
        /// <summary>
        ///     Constructor.</summary>
        /// <param name="ajaxMethodName">
        ///     Name of the requested method that is invalid.</param>
        /// <param name="message">
        ///     Exception message.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxMethodInvalidException(string ajaxMethodName, string message, Exception inner)
            : base(message, inner, HttpStatusCode._500_InternalServerError)
        {
            AjaxMethodName = ajaxMethodName;
        }
    }

    /// <summary>Indicates that an AJAX method was requested that does not exist.</summary>
    public class AjaxMethodNotFoundException : AjaxException
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
            : base(message, inner, HttpStatusCode._404_NotFound)
        {
            AjaxMethodName = ajaxMethodName;
        }
    }

    /// <summary>
    ///     Indicates that, during processing of an AJAX request, the value for a method parameter could not be deseralized.</summary>
    public class AjaxInvalidParameterException : AjaxException
    {
        /// <summary>Gets the name of the parameter that could not be deserialized.</summary>
        public string ParameterName { get; private set; }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterName">
        ///     Name of the parameter that could not be deserialized.</param>
        /// <param name="targetType">
        ///     Type that the value was attempted to be deserialized as.</param>
        /// <param name="json">
        ///     The JSON value that was attempted to be deserialized.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxInvalidParameterException(string parameterName, JsonValue json, Type targetType, Exception inner)
            : base("The parameter {0} could not be deseralized into type {1}. JSON: {2}".Fmt(parameterName, targetType.FullName, JsonValue.ToString(json)), inner)
        {
            ParameterName = parameterName;
        }
    }

    /// <summary>
    ///     Indicates that, during processing of an AJAX request, a non-optional method parameter was missing from the JSON.</summary>
    public class AjaxMissingParameterException : AjaxException
    {
        /// <summary>Gets the name of the parameter that was missing.</summary>
        public string ParameterName { get; private set; }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterName">
        ///     Name of the parameter that was missing.</param>
        public AjaxMissingParameterException(string parameterName)
            : base("The parameter “{0}” is not optional and was not specified.".Fmt(parameterName))
        {
            ParameterName = parameterName;
        }
    }

    /// <summary>
    ///     Indicates that, during processing of an AJAX request, the data containing the method parameters could not be
    ///     parsed.</summary>
    public class AjaxInvalidParameterDataException : AjaxException
    {
        /// <summary>Gets the raw data that could not be parsed.</summary>
        public string ParameterData { get; private set; }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="parameterData">
        ///     The raw data that could not be parsed.</param>
        /// <param name="inner">
        ///     Inner exception.</param>
        public AjaxInvalidParameterDataException(string parameterData, Exception inner)
            : base("The provided JSON for the method parameters is not a valid JSON dictionary. JSON: {0}".Fmt(parameterData), inner)
        {
            ParameterData = parameterData;
        }
    }

    /// <summary>Indicates that the value returned by an AJAX method could not be serialized.</summary>
    public class AjaxInvalidReturnValueException : AjaxException
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
            : base(message, inner, HttpStatusCode._500_InternalServerError)
        {
            ReturnValue = returnValue;
            ReturnType = returnType;
        }
    }
}
