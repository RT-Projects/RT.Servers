using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Xml;

namespace RT.Servers
{
    /// <summary>Contains values to specify what should happen to a session when it is closed.</summary>
    public enum SessionCloseAction
    {
        /// <summary>The updated session is saved to the session store.</summary>
        Save,
        /// <summary>The updates to the session are discarded. The previous state of the session is retained.</summary>
        Discard,
        /// <summary>The session is deleted.</summary>
        Delete
    };

    /// <summary>Provides functionality to track user sessions in HTTP requests using cookies. See remarks for usage guidelines.</summary>
    /// <remarks>
    ///    <para>Intended use is as follows:</para>
    ///    <list type="bullet">
    ///        <item><description>Declare a class containing one or more fields which constitute your session data.</description></item>
    ///        <item><description>Derive that class from <see cref="FileSession"/>, or implement your own alternative to <see cref="FileSession"/> and derive from that.</description></item>
    ///        <item><description>
    ///            <para>You can now augment any HTTP request handler that looks like this:</para>
    ///            <code>req => { /* code to handle request, no session support */ }</code>
    ///            <para>by changing it into this:</para>
    ///            <code>req => Session.Enable&lt;TSession&gt;(req, session => { /* code to handle request with session variable available */ })</code>
    ///            <para>(replace "TSession" with the name of your derived class; see <see cref="Enable"/>).</para>
    ///        </description></item>
    ///        <item><description>Within your request handler, you can make arbitrary changes to the session object, which will be persisted automatically.</description></item>
    ///        <item><description>You can set session.<see cref="CloseAction"/> to a non-default value to suppress automatic session saving.</description></item>
    ///    </list>
    /// </remarks>
    public abstract class Session
    {
        /// <summary>Specifies how to proceed when the session object is disposed.</summary>
        [XmlIgnore]
        public SessionCloseAction CloseAction = SessionCloseAction.Save;

        /// <summary>Contains the session ID (the cookie value).</summary>
        [XmlIgnore]
        public string SessionID { get; protected set; }

        [XmlIgnore]
        private bool _isNew;

        /// <summary>When overridden in a derived class, gets the name of the cookie that contains the user’s session ID.</summary>
        /// <remarks>The default implementation returns <c>this.GetType().Name</c>.</remarks>
        protected virtual string CookieName { get { return this.GetType().Name; } }

        /// <summary>When overridden in a derived class, gets the applicable cookie Path for the cookie that contains the user’s session ID.</summary>
        /// <remarks>The default implementation returns <c>"/"</c>.</remarks>
        protected virtual string CookiePath { get { return "/"; } }

        /// <summary>When overridden in a derived class, gets the expiry date/time of the cookie that contains the user’s session ID.</summary>
        /// <remarks>The default implementation returns <c>DateTime.MaxValue</c>.</remarks>
        protected virtual DateTime CookieExpires { get { return DateTime.MaxValue; } }

        /// <summary>Returns a string representation of this session object.</summary>
        public override string ToString() { return "{0} [{1}] ({2})".Fmt(SessionID, CookieName, CloseAction); }

        /// <summary>Initialises this instance so that it represents a new, unique session.</summary>
        protected virtual void createSession()
        {
            var characters = "abcdefghijklmnopqrstuvwxyz0123456789-_";
            SessionID = new string(Enumerable.Range(1, 32).Select(i => characters[Rnd.Next(characters.Length)]).ToArray());
        }

        /// <summary>When overridden in a derived class, attempts to retrieve an existing session (identified by <see cref="SessionID"/>) from the session store and initialises this instance with the relevant data.
        /// If no such session as identified by <see cref="SessionID"/> is found in the session store, returns false.</summary>
        /// <remarks>Do not call <see cref="createSession"/> in your override. Simply return false, and <see cref="createSession"/> will be called automatically.</remarks>
        protected abstract bool readSession();
        /// <summary>When overridden in a derived class, saves this instance to the session store.</summary>
        protected abstract void saveSession();
        /// <summary>When overridden in a derived class, deletes this session (identified by <see cref="SessionID"/>) from the session store.</summary>
        protected abstract void deleteSession();

        /// <summary>Generates a <see cref="Cookie"/> object that identifies this session.</summary>
        private Cookie createCookie()
        {
            return new Cookie
            {
                Name = CookieName,
                Value = SessionID,
                Path = CookiePath,
                Expires = CookieExpires,
                HttpOnly = true,
            };
        }

        /// <summary>Initialises this session instance from the specified request. Only use this if you are not using <see cref="Enable{TSession}"/>, as that already calls it.</summary>
        /// <param name="req">Request containing the cookie information from which to initialise the session.</param>
        public void InitialiseFromRequest(HttpRequest req)
        {
            _isNew = false;

            // Try to read in an existing session
            bool done = false;
            try
            {
                if (req.Headers != null && req.Headers.Cookie != null && req.Headers.Cookie.ContainsKey(CookieName))
                {
                    SessionID = req.Headers.Cookie[CookieName].Value;
                    done = readSession();
                }
            }
            catch { }

            // No existing session found or reading failed, so create a new session
            if (!done)
            {
                createSession();
                _isNew = true;
            }
        }

        private void setCookie(HttpResponseHeaders headers)
        {
            if ((_isNew && CloseAction == SessionCloseAction.Save) || (!_isNew && CloseAction == SessionCloseAction.Delete))
            {
                if (headers.SetCookie == null)
                    headers.SetCookie = new List<Cookie>();
                var cookie = createCookie();
                if (CloseAction == SessionCloseAction.Delete)
                    cookie.Expires = DateTime.Today - TimeSpan.FromDays(300);
                headers.SetCookie.Add(cookie);
            }
        }

        private void close()
        {
            try
            {
                if (CloseAction == SessionCloseAction.Save)
                    saveSession();
                else if (CloseAction == SessionCloseAction.Delete)
                    deleteSession();
            }
            catch { }
        }

        /// <summary>Enables the use of sessions in an HTTP request handler.</summary>
        /// <typeparam name="TSession">The type of session to be used.</typeparam>
        /// <param name="req">The HTTP request for which to enable session support.</param>
        /// <param name="handler">HTTP request handler code that can make free use of a session variable.</param>
        /// <remarks>See the remarks section in the <see cref="Session"/> documentation for usage guidelines.</remarks>
        public static HttpResponse Enable<TSession>(HttpRequest req, Func<TSession, HttpResponse> handler) where TSession : Session, new()
        {
            var session = new TSession();
            session.InitialiseFromRequest(req);
            var response = handler(session);
            session.CleanUp(response);
            return response;
        }

        /// <summary>Saves/deletes the session and/or sets the session cookie, as appropriate. Only use this if you are not using <see cref="Enable{TSession}"/>, as that already calls it.</summary>
        /// <param name="response">Response to add cookie information to.</param>
        public void CleanUp(HttpResponse response)
        {
            setCookie(response.Headers);
            close();
        }
    }

    /// <summary>Provides functionality to track user sessions in HTTP requests using cookies and to store such user sessions in the local file system.</summary>
    /// <remarks>In order to use this class, you must create a class that derives from it. See the remarks section in the <see cref="Session"/> documentation for usage guidelines.</remarks>
    public abstract class FileSession : Session
    {
        /// <summary>Used to prevent concurrent read/write access to session files.</summary>
        private static object _lock = new object();

        /// <summary>Gets the folder in which session data should be stored.</summary>
        /// <remarks>The default implementation returns <c>Path.Combine(Path.GetTempPath(), "sessions")</c>.</remarks>
        protected virtual string SessionPath { get { return Path.Combine(Path.GetTempPath(), "sessions"); } }

        /// <summary>Initialises this instance so that it represents a new, unique session.</summary>
        protected override sealed void createSession() { base.createSession(); }

        /// <summary>Retrieves an existing session from the file system and initialises this instance with the relevant data.</summary>
        protected override sealed bool readSession()
        {
            var file = Path.Combine(SessionPath, SessionID);
            lock (_lock)
            {
                if (!File.Exists(file))
                    return false;
                XmlClassify.ReadXmlFileIntoObject(file, this);
            }
            return true;
        }

        /// <summary>Saves this instance to the file system.</summary>
        protected override sealed void saveSession()
        {
            var sessionPath = SessionPath;
            // Directory.CreateDirectory() does nothing if the directory already exists.
            Directory.CreateDirectory(sessionPath);
            lock (_lock)
                XmlClassify.SaveObjectToXmlFile(this, Path.Combine(sessionPath, SessionID));
        }

        /// <summary>Deletes the file containing the data for this session from the file system.</summary>
        protected override sealed void deleteSession()
        {
            var path = Path.Combine(Path.GetTempPath(), "sessions");
            var sessionPath = Path.Combine(path, SessionID);
            lock (_lock)
            {
                if (File.Exists(sessionPath))
                    File.Delete(sessionPath);
            }
        }
    }
}
