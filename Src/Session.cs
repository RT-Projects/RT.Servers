using System;
using System.Collections.Generic;
using System.IO;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Xml;

namespace RT.Servers
{
    /// <summary>
    ///     Provides functionality to track user sessions in HTTP requests using cookies. See remarks for usage
    ///     guidelines.</summary>
    /// <remarks>
    ///     <para>
    ///         Intended use is as follows:</para>
    ///     <list type="bullet">
    ///         <item><description>
    ///             Declare a class containing one or more fields which constitute your session data.</description></item>
    ///         <item><description>
    ///             Derive that class from <see cref="Session"/>, <see cref="FileSession"/>, or your own specialization of
    ///             either.</description></item>
    ///         <item><description>
    ///             <para>
    ///                 You can now augment any HTTP request handler that looks like this:</para>
    ///             <code>
    ///                 req =&gt; { /* code to handle request, no session support */ }</code>
    ///             <para>
    ///                 by changing it into this:</para>
    ///             <code>
    ///                 req =&gt; Session.EnableAutomatic&lt;TSession&gt;(req, session =&gt; {
    ///                     /* code to handle request with session variable available */
    ///                 })</code>
    ///             <para>
    ///                 (replace <c>TSession</c> with the name of your derived class; see <see cref="EnableAutomatic"/> or <see
    ///                 cref="EnableManual"/>).</para></description></item>
    ///         <item><description>
    ///             Within your request handler, you can make arbitrary changes to the session object, which will be persisted
    ///             automatically.</description></item>
    ///         <item><description>
    ///             If you use <see cref="EnableManual"/>, <see cref="SessionModified"/> must be set to <c>true</c> whenever any
    ///             value in the session object is changed.</description></item>
    ///         <item><description>
    ///             If you use <see cref="EnableAutomatic"/>, the session object must implement <see
    ///             cref="ISessionEquatable{TSession}"/>. In this case, modifications to the session object are detected by taking
    ///             a clone of the session object, then running the request handler, and then comparing the
    ///             two.</description></item>
    ///         <item><description>
    ///             You can set <see cref="Action"/> if you want the session reverted or
    ///             deleted.</description></item></list></remarks>
    public abstract class Session
    {
        /// <summary>
        ///     True if a new session was created, or any of the session variables were modified. Derived classes should set this
        ///     to true whenever a session variable is modified. Set this to false to discard all session changes (otherwise they
        ///     will be saved upon session <see cref="CleanUp"/>).</summary>
        [XmlIgnore]
        public bool SessionModified { get; set; }

        /// <summary>
        ///     True if a new session was created, or any of the cookie parameters were modified (such as <see
        ///     cref="CookieExpires"/>).</summary>
        [XmlIgnore]
        public bool CookieModified { get; set; }

        /// <summary>Controls what happens to the session data when the request handler returns.</summary>
        [XmlIgnore]
        public SessionAction Action { get; set; }

        [XmlIgnore]
        private bool _hadSession, _hadCookie;

        /// <summary>Contains the session ID, which is also the cookie value.</summary>
        [XmlIgnore]
        public string SessionID { get; private set; }

        /// <summary>
        ///     When overridden in a derived class, gets the name of the cookie that contains the user’s session ID.</summary>
        /// <remarks>
        ///     This property should always return the same value, otherwise the behaviour is undefined. The default
        ///     implementation returns <c>this.GetType().Name</c>.</remarks>
        protected virtual string CookieName { get { return this.GetType().Name; } }

        /// <summary>
        ///     When overridden in a derived class, gets the applicable cookie Path for the cookie that contains the user’s
        ///     session ID.</summary>
        /// <remarks>
        ///     This property should always return the same value, otherwise the behaviour is undefined. The default
        ///     implementation returns <c>"/"</c>.</remarks>
        protected virtual string CookiePath { get { return "/"; } }

        /// <summary>
        ///     When overridden in a derived class, gets the expiry date/time of the cookie that contains the user’s session
        ///     ID.</summary>
        /// <remarks>
        ///     <para>
        ///         If the property value is variable, ensure that <see cref="CookieModified"/> is set to true before the request
        ///         handler returns.</para>
        ///     <para>
        ///         The default implementation returns <c>DateTime.MaxValue</c>.</para></remarks>
        protected virtual DateTime CookieExpires { get { return DateTime.MaxValue; } }

        /// <summary>Returns a string representation of this session object.</summary>
        public override string ToString()
        {
            return "{0} [{1}{2}] ({3})".Fmt(SessionID, CookieName, CookieModified ? " (mod)" : "", Action);
        }

        /// <summary>
        ///     When overridden in a derived class, attempts to retrieve an existing session (identified by <see
        ///     cref="SessionID"/>) from the session store and initialises this instance with the relevant data. If no such
        ///     session as identified by <see cref="SessionID"/> is found in the session store, returns false.</summary>
        protected abstract bool ReadSession();
        /// <summary>
        ///     Initialises a new session whenever an existing one couldn't be read. Must not save the session; only initialise
        ///     any session variables. The default implementation does nothing, so derived classes don't need to call
        ///     it.</summary>
        protected virtual void NewSession() { }
        /// <summary>When overridden in a derived class, saves this instance to the session store.</summary>
        protected abstract void SaveSession();
        /// <summary>
        ///     When overridden in a derived class, deletes this session (identified by <see cref="SessionID"/>) from the session
        ///     store. This method is not called if <see cref="ReadSession"/> returned false, indicating that no session was
        ///     available.</summary>
        protected abstract void DeleteSession();

        /// <summary>
        ///     Initialises this session instance from the specified request. Only use this if you are not using <see
        ///     cref="Enable{TSession}"/>, as that already calls it.</summary>
        /// <param name="req">
        ///     Request containing the cookie information from which to initialise the session.</param>
        protected void InitialiseFromRequest(HttpRequest req)
        {
            // Try to read in an existing session
            if (req.Headers != null && req.Headers.Cookie != null && req.Headers.Cookie.ContainsKey(CookieName))
            {
                SessionID = req.Headers.Cookie[CookieName].Value;
                _hadSession = ReadSession();
                _hadCookie = true;
            }

            // Create a new session if no existing session found
            if (!_hadSession)
            {
                SessionID = RndCrypto.NextBytes(21).Base64UrlEncode();
                NewSession();
                CookieModified = true;
            }
            else
                CookieModified = false;
        }

        private void saveCookie(HttpResponse response, bool delete = false)
        {
            if (response.Headers.SetCookie == null)
                response.Headers.SetCookie = new List<Cookie>();
            response.Headers.SetCookie.Add(new Cookie
            {
                Name = CookieName,
                Value = delete ? "-" : SessionID,
                Path = CookiePath,
                Expires = delete ? DateTime.Today - TimeSpan.FromDays(300) : CookieExpires,
                HttpOnly = true,
            });
        }

        /// <summary>
        ///     Saves/deletes the session and/or sets the session cookie, as appropriate. Only use this if you are not using <see
        ///     cref="EnableManual{TSession}"/> or <see cref="EnableAutomatic{TSession}"/>, as that already calls it.</summary>
        /// <param name="response">
        ///     Response to add cookie information to.</param>
        /// <param name="wasModified">
        ///     Specifies whether the session data has changed.</param>
        public virtual void CleanUp(HttpResponse response, bool wasModified)
        {
            switch (Action)
            {
                case SessionAction.DoNothing:
                    break;

                case SessionAction.Save:
                    if (wasModified)
                        SaveSession();
                    if (CookieModified)
                        saveCookie(response);
                    break;

                case SessionAction.Delete:
                    if (_hadSession)
                        DeleteSession();
                    if (_hadCookie)
                        saveCookie(response, delete: true);
                    break;
            }
        }

        /// <summary>
        ///     Enables the use of sessions in an HTTP request handler. Use this if your session class implements <see
        ///     cref="ISessionEquatable{TSession}"/>.</summary>
        /// <typeparam name="TSession">
        ///     The type of session to be used.</typeparam>
        /// <param name="req">
        ///     The HTTP request for which to enable session support.</param>
        /// <param name="handler">
        ///     HTTP request handler code that can make free use of a session variable.</param>
        /// <remarks>
        ///     See the remarks section in the <see cref="Session"/> documentation for usage guidelines.</remarks>
        public static HttpResponse EnableAutomatic<TSession>(HttpRequest req, Func<TSession, HttpResponse> handler) where TSession : Session, ISessionEquatable<TSession>, new()
        {
            var session = new TSession();
            session.InitialiseFromRequest(req);
            var sessionCopy = session.DeepClone();
            var response = handler(session);
            session.CleanUp(response, wasModified: !sessionCopy.Equals(session));
            return response;
        }

        public static HttpResponse EnableAutomatic<TSession>(HttpRequest req, Func<TSession, HttpResponse> handler, Func<TSession>constructor) where TSession : Session, ISessionEquatable<TSession>
        {
            var session = constructor();
            session.InitialiseFromRequest(req);
            var sessionCopy = session.DeepClone();
            var response = handler(session);
            session.CleanUp(response, wasModified: !sessionCopy.Equals(session));
            return response;
        }

        /// <summary>
        ///     Enables the use of sessions in an HTTP request handler. Use this if your session class does not implement <see
        ///     cref="ISessionEquatable{TSession}"/>; your class will have to manually set <see cref="SessionModified"/> to
        ///     <c>true</c> before the request handler returns if any change was made to the session.</summary>
        /// <typeparam name="TSession">
        ///     The type of session to be used.</typeparam>
        /// <param name="req">
        ///     The HTTP request for which to enable session support.</param>
        /// <param name="handler">
        ///     HTTP request handler code that can make free use of a session variable.</param>
        /// <remarks>
        ///     See the remarks section in the <see cref="Session"/> documentation for usage guidelines.</remarks>
        public static HttpResponse EnableManual<TSession>(HttpRequest req, Func<TSession, HttpResponse> handler) where TSession : Session, new()
        {
            var session = new TSession();
            session.InitialiseFromRequest(req);
            var response = handler(session);
            session.CleanUp(response, wasModified: session.SessionModified);
            return response;
        }
    }

    /// <summary>Specifies an action to perform on the session object when the request handler returns.</summary>
    public enum SessionAction
    {
        /// <summary>Causes changes to the session to be saved.</summary>
        Save,

        /// <summary>Cause any changes to the session to be ignored. The next request will receive the previous state of the session.</summary>
        DoNothing,

        /// <summary>Cause the session and the associated cookie to be deleted. The deletion occurs in <see cref="CleanUp"/>.</summary>
        Delete
    }

    /// <summary>
    ///     Provides functionality required by <see cref="Session.EnableAutomatic"/>.</summary>
    /// <typeparam name="TSession">
    ///     The type of the session object.</typeparam>
    public interface ISessionEquatable<TSession> : IEquatable<TSession> where TSession : Session
    {
        /// <summary>Takes a deep clone of this session object.</summary>
        TSession DeepClone();
    }

    /// <summary>
    ///     Provides functionality to track user sessions in HTTP requests using cookies and to store such user sessions in the
    ///     local file system.</summary>
    /// <remarks>
    ///     In order to use this class, you must create a class that derives from it. See the remarks section in the <see
    ///     cref="Session"/> documentation for usage guidelines.</remarks>
    public abstract class FileSession : Session
    {
        /// <summary>Used to prevent concurrent read/write access to session files.</summary>
        private static object _lock = new object();

        /// <summary>
        ///     Gets the folder in which session data should be stored.</summary>
        /// <remarks>
        ///     The default implementation returns <c>Path.Combine(Path.GetTempPath(), "sessions")</c>.</remarks>
        protected virtual string SessionPath { get { return Path.Combine(Path.GetTempPath(), "sessions"); } }

        /// <summary>Retrieves an existing session from the file system and initialises this instance with the relevant data.</summary>
        protected override sealed bool ReadSession()
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
        protected override sealed void SaveSession()
        {
            var sessionPath = SessionPath;
            // Directory.CreateDirectory() does nothing if the directory already exists.
            Directory.CreateDirectory(sessionPath);
            lock (_lock)
                XmlClassify.SaveObjectToXmlFile(this, Path.Combine(sessionPath, SessionID));
        }

        /// <summary>Deletes the file containing the data for this session from the file system.</summary>
        protected override sealed void DeleteSession()
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
