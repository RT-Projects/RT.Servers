using System;

namespace RT.Servers
{
    /// <summary>Provides an exception that carries an HTTP status code.</summary>
    public class HttpException : Exception
    {
        /// <summary>The status code associated with this exception.</summary>
        public HttpStatusCode StatusCode { get; private set; }
        /// <summary>Constructor.</summary>
        /// <param name="statusCode">The status code associated with this exception.</param>
        /// <param name="message">An optional exception message.</param>
        public HttpException(HttpStatusCode statusCode, string message = null) : base(message ?? "") { StatusCode = statusCode; }
        /// <summary>Constructor which uses HTTP status code 500.</summary>
        /// <param name="message">An optional exception message.</param>
        public HttpException(string message = null) : base(message ?? "") { StatusCode = HttpStatusCode._500_InternalServerError; }
    }

    /// <summary>Provides an exception that indicates that a resource was not found.</summary>
    public class HttpNotFoundException : HttpException
    {
        /// <summary>A string describing the resource that was not found.</summary>
        public string Location { get; private set; }

        /// <summary>Constructor.</summary>
        /// <param name="location">A string describing the resource that was not found.</param>
        public HttpNotFoundException(string location = null)
            : base(HttpStatusCode._404_NotFound, string.IsNullOrWhiteSpace(location) ? "The requested resource does not exist." : ("“" + location + "” does not exist."))
        {
            Location = location;
        }
    }
}
