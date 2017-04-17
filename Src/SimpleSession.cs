using System;
using System.Collections.Generic;
using RT.Util;

namespace RT.Servers
{
    /// <summary>
    ///     Provides functionality to track a session ID in HTTP requests using cookies.</summary>
    /// <remarks>
    ///     This class only retrieves session IDs from the session cookie and does not handle mapping IDs to session data. It also
    ///     does not automatically create a new session if the HTTP response did not contain a session cookie.</remarks>
    public class SimpleSession
    {
        private bool _deleteSession = false;
        private bool _newSession = false;
        private string _newSessionID;
        private string _newCookiePath;
        private DateTime? _newCookieExpires;

        /// <summary>The session ID retrieved from the session cookie.</summary>
        public string SessionID { get; private set; }

        /// <summary>The name of the cookie that contains the session ID.</summary>
        public string CookieName { get; private set; }

        /// <summary>The path of the retrieved session cookie.</summary>
        public string CookiePath { get; private set; }

        /// <summary>The expiry date of the retrieved session cookie. If null, the session expires when the client's browser closes.</summary>
        public DateTime? CookieExpires { get; private set; }

        /// <summary>
        ///     Constructs a new instance.</summary>
        /// <param name="cookieName">
        ///     The name of the cookie.</param>
        public SimpleSession(string cookieName = "SessionID")
        {
            CookieName = cookieName;
        }

        /// <summary>
        ///     Specifies that the session cookie should be deleted by the Handle method. Its value is false initially and right
        ///     after calling Handle().</summary>
        public void DeleteSession()
        {
            if (_newSession)
                throw new InvalidOperationException("Cannot both delete and create a session.");

            _deleteSession = true;
        }

        /// <summary>
        ///     Specifies that a new session should be created. (The <see cref="Handle"/> method adds the relevant cookie to the
        ///     HTTP response.) It also sets the current SessionID to the specified ID.</summary>
        /// <param name="sessionID">
        ///     The ID for the new session.</param>
        /// <param name="cookieExpiry">
        ///     If null, the session expires when the client's browser is closed.</param>
        /// <param name="cookiePath">
        ///     The HTTP URL path to which the cookie should apply.</param>
        public void NewSession(string sessionID, DateTime? cookieExpiry, string cookiePath = "/")
        {
            if (_deleteSession)
                throw new InvalidOperationException("Cannot both delete and create a session.");

            _newSession = true;
            _newSessionID = sessionID;
            SessionID = sessionID;
            _newCookiePath = cookiePath;
            _newCookieExpires = cookieExpiry;
        }

        /// <summary>Returns a random string of 32 characters useful as a session ID.</summary>
        public static string CreateNewSessionId()
        {
            return RndCrypto.GenerateString(32);
        }

        /// <summary>
        ///     Retrieves the session ID from the session cookie (specified by the cookie name) from the given <see
        ///     cref="HttpRequest"/> and calls the <paramref name="handler"/> function. A session cookie is added to the HTTP
        ///     response returned by the handler only if either <see cref="DeleteSession"/> or <see cref="NewSession"/> have been
        ///     called (possibly by the handler). Before the possibly augmented HTTP response is returned, the status of
        ///     DeleteSession and NewSession is reset.</summary>
        /// <param name="req">
        ///     The current HTTP request.</param>
        /// <param name="handler">
        ///     The inner request handler to execute.</param>
        /// <returns>
        ///     An HTTP response, possibly augmented with a session cookie.</returns>
        public HttpResponse Handle(HttpRequest req, Func<HttpResponse> handler)
        {
            if (req == null)
                throw new ArgumentNullException("req");
            if (handler == null)
                throw new ArgumentNullException("handler");

            if (req.Headers != null && req.Headers.Cookie != null && req.Headers.Cookie.ContainsKey(CookieName))
            {
                var cookie = req.Headers.Cookie[CookieName];
                SessionID = cookie.Value;
                CookiePath = cookie.Path;
                CookieExpires = cookie.Expires;
            }
            else
            {
                SessionID = null;
            }

            var response = handler();

            if (response != null && ((_deleteSession && SessionID != null) || _newSession))
            {
                if (response.Headers.SetCookie == null)
                    response.Headers.SetCookie = new List<Cookie>();

                response.Headers.SetCookie.Add(new Cookie
                {
                    Name = CookieName,
                    Value = _deleteSession ? "-" : _newSessionID,
                    Path = _deleteSession ? CookiePath : _newCookiePath,
                    Expires = _deleteSession ? new DateTime(1970, 1, 1) : _newCookieExpires,
                    HttpOnly = true,
                });
            }

            _deleteSession = false;
            _newSession = false;
            return response;
        }
    }
}
