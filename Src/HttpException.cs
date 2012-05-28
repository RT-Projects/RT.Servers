using System;

namespace RT.Servers
{
    /// <summary>Provides an exception that carries an HTTP status code.</summary>
    public class HttpException : Exception
    {
        /// <summary>The status code associated with this exception.</summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>An error message which may be revealed to the user.</summary>
        public string UserMessage { get; private set; }

        /// <summary>Constructor.</summary>
        /// <param name="statusCode">The status code associated with this exception.</param>
        /// <param name="message">An optional exception message. If omitted, the <paramref name="userMessage"/> will be used.</param>
        /// <param name="userMessage">An optional error message which may be revealed to the user. If omitted, a default status code description will be used.</param>
        public HttpException(HttpStatusCode statusCode, string message = null, string userMessage = null)
            : base(message ?? userMessage ?? statusCode.ToText())
        {
            StatusCode = statusCode;
            UserMessage = userMessage ?? statusCode.ToText();
        }
    }

    /// <summary>Provides an exception that indicates that a resource was not found.</summary>
    public class HttpNotFoundException : HttpException
    {
        /// <summary>A string describing the resource that was not found. May be null.</summary>
        public string Location { get; private set; }

        /// <summary>Constructor.</summary>
        /// <param name="location">An optional string describing the resource that was not found. This will be revealed to the user.</param>
        public HttpNotFoundException(string location = null)
            : base(HttpStatusCode._404_NotFound, null, location == null ? null : ("“" + location + "” does not exist."))
        {
            Location = location;
        }
    }

    /// <summary>Indicates that an error has occurred while parsing a request. This usually indicates that the request was malformed in some way.</summary>
    public class HttpRequestParseException : HttpException
    {
        /// <summary>Constructor.</summary>
        internal HttpRequestParseException(HttpStatusCode statusCode, string userMessage = null)
            : base(statusCode, null, userMessage) { }
    }
}
