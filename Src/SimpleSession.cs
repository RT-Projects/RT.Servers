using RT.Servers;
using RT.Util;
using RT.Util.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RT.Servers
{
    /// <summary>
    ///     Provides functionality to track a session ID in HTTP requests using cookies.</summary>
    /// <remarks>
    ///     This class only retrieves session IDs from the session cookie and does not handle mapping IDs to session data. 
    ///     It also does not automatically create a new session if the HTTP response did not contain a session cookie.</remarks>
    public class SimpleSession
    {


        private Func<SimpleSession, HttpResponse> _handler;

        private bool _deleteSession = false;
        private bool _newSession = false;
        private string _newSessionID;
        private string _newCookiePath;
        private DateTime? _newCookieExpires;

        /// <summary>
        /// The session ID retrieved from the session cookie.
        /// </summary>
        public string SessionID { get; private set; }

        /// <summary>
        /// The name of the cookie that contains the session ID.
        /// </summary>
        public string CookieName { get; private set; }

        /// <summary>
        /// The path of the retrieved session cookie.
        /// </summary>
        public string CookiePath { get; private set; }

        /// <summary>
        /// The expiry date of the retrieved session cookie. If null, the session expires when the client's browser closes.
        /// </summary>
        public DateTime? CookieExpires { get; private set; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="handler">A function that handles the HTTP response and is called by the Handle method. 
        /// The first parameter of the function is instantiated to the session ID, if a session exists, or null otherwise.</param>
        public SimpleSession(Func<SimpleSession, HttpResponse> handler, string cookieName = "SessionID")
        {
            this._handler = handler;
            this.CookieName = cookieName;
        }

        /// <summary>
        /// Specifies that the session cookie should be deleted by the Handle method. Its value is false initially and right after calling Handle().
        /// </summary>
        public void DeleteSession()
        {
            if (_newSession)
            {
                throw new InvalidOperationException("Cannot both delete and create a session.");
            }
            _deleteSession = true;
        }

        /// <summary>
        /// Specifies that a new session should be created by the Handle method. 
        /// </summary>
        /// <param name="cookieExpiry">If null, the session expires when the client's browser is closed.</param>
        public void NewSession(string sessionID, DateTime? cookieExpiry, string cookiePath = "/")
        {
            if (_deleteSession)
            {
                throw new InvalidOperationException("Cannot both delete and create a session.");
            }
            _newSession = true;
            _newSessionID = sessionID;
            _newCookiePath = cookiePath;
            _newCookieExpires = cookieExpiry;
        }


        /// <param name="sessionID">Returns a random base 64 and URL encoded string of 21 bytes.</param>
        public static string CreateNewSessionId()
        {
            return RndCrypto.NextBytes(21).Base64UrlEncode();
        }



        /// <summary>
        /// This method retrieves the session ID from the session cookie (specified by the cookie name) from the given <see cref="HttpRequest"/> 
        /// and calls the handler function (specified in the constructor) on the ID and the request. 
        /// A session cookie is added to the HTTP response returned by the handler only if either DeleteSession() or NewSession() have been called (possibly by the handler). 
        /// Before the possibly augmented HTTP response is returned, the status of DeleteSession and NewSession is reset.
        /// </summary>
        /// <param name="req"></param>
        /// <returns>An HTTP response, possibly augmented with a session cookie.</returns>
        public HttpResponse Handle(HttpRequest req)
        {
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

            var response = _handler(this);

            if ((_deleteSession && SessionID != null) || _newSession)
            {
                if (response.Headers.SetCookie == null)
                {
                    response.Headers.SetCookie = new List<Cookie>();
                }

                Cookie cookie;
                if (_deleteSession)
                {
                    cookie = new Cookie
                    {
                        Name = CookieName,
                        Value = "-",
                        Path = CookiePath,
                        Expires = new DateTime(1970, 1, 1),
                        HttpOnly = true,
                    };
                }
                else // _newSession is true
                {
                    cookie = new Cookie
                    {
                        Name = CookieName,
                        Value = _newSessionID,
                        Path = _newCookiePath,
                        Expires =_newCookieExpires,
                        HttpOnly = true,
                    };
                }

                response.Headers.SetCookie.Add(cookie);

            }
            _deleteSession = false;
            _newSession = false;
            return response;
        }


    }

}
