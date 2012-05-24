using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        public HttpException(HttpStatusCode statusCode, string message = null) : base(message) { StatusCode = statusCode; }
    }
}
