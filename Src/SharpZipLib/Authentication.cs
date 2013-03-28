using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RT.TagSoup;
using RT.Util.ExtensionMethods;
using RT.Util.Xml;

namespace RT.Servers
{
    /// <summary>Provides functionality for a login system.</summary>
    public sealed class Authenticator
    {
        private string _usersPath;
        private string _defaultReturnTo;
        private string _appName;

        /// <summary>Used to ensure that the AuthUsers XML file is not read and written concurrently.</summary>
        /// <remarks>static in case multiple instances of <see cref="Authenticator"/> use the same XML file.</remarks>
        private static object _lock = new object();

        /// <summary>Constructor.</summary>
        /// <param name="usersFilePath">Specifies the path and filename of an XML file containing the users and passwords.</param>
        /// <param name="defaultReturnTo">Default URL to redirect to when a login attempt is successful. This can be overridden by a "returnto" GET parameter.</param>
        /// <param name="appName">Name of the application which uses this authentication handler.</param>
        public Authenticator(string usersFilePath, string defaultReturnTo, string appName)
        {
            if (usersFilePath == null)
                throw new ArgumentNullException("usersFilePath");
            if (defaultReturnTo == null)
                throw new ArgumentNullException("defaultReturnTo");
            if (appName == null)
                throw new ArgumentNullException("appName");
            _usersPath = usersFilePath;
            _defaultReturnTo = defaultReturnTo;
            _appName = appName;
        }

        /// <summary>Handles a request.</summary>
        /// <param name="request">Request to handle.</param>
        /// <param name="setUsername">Action to call when login is successful. Typically used to set the username in a session.</param>
        /// <param name="loggedInUser">Username of the user currently logged in (typically read from a session).</param>
        public HttpResponse Handle(HttpRequest request, string loggedInUser, Action<string> setUsername)
        {
            return new UrlPathResolver(
                new UrlPathHook(path: "/login", handler: req => loginHandler(req, setUsername)),
                new UrlPathHook(path: "/changepassword", handler: req => changePasswordHandler(req, loggedInUser)),
                new UrlPathHook(path: "/createuser", handler: req => createUserHandler(req, loggedInUser)),
                new UrlPathHook(path: "/logout", handler: req => logoutHandler(req, setUsername))
            ).Handle(request);
        }

        private HttpResponse logoutHandler(HttpRequest req, Action<string> setUsername)
        {
            setUsername(null);
            return HttpResponse.Redirect(req.Url.WithPathParent().WithPathOnly("/login"));
        }

        private HttpResponse loginHandler(HttpRequest req, Action<string> setUsername)
        {
            if (req.Method != HttpMethod.Post)
                return loginForm(req.Url["returnto"], false, null, null, req.Url.WithoutQuery("returnto"));

            var username = req.Post["username"].Value;
            var password = req.Post["password"].Value;
            var returnTo = req.Post["returnto"].Value;

            if (username == null || password == null)
                return HttpResponse.Redirect(req.Url.WithQuery("returnto", returnTo));

            if (File.Exists(_usersPath))
            {
                AuthUsers users;
                lock (_lock)
                    users = XmlClassify.LoadObjectFromXmlFile<AuthUsers>(_usersPath);
                var user = users.Users.FirstOrDefault(u => u.Username == username && verifyHash(password, u.PasswordHash));
                if (user != null)
                {
                    // Login successful!
                    setUsername(user.Username);
                    return HttpResponse.Redirect(returnTo ?? _defaultReturnTo);
                }
            }
            else
                lock (_lock)
                    XmlClassify.SaveObjectToXmlFile<AuthUsers>(new AuthUsers(), _usersPath);

            // Login failed.
            return loginForm(returnTo, true, username, password, req.Url);
        }

        private static string createHash(string password)
        {
            var salt = new byte[8];
            new RNGCryptoServiceProvider().GetBytes(salt);
            var saltstr = salt.ToHex().ToLowerInvariant(); // just in case we ever change ToHex to return uppercase
            return saltstr + ":" + SHA1.Create().ComputeHash((saltstr + password).ToUtf8()).ToHex();
        }

        private static bool verifyHash(string password, string hash)
        {
            var parts = hash.Split(':');
            if (parts.Length != 2)
                return false;
            var expected = SHA1.Create().ComputeHash((parts[0].ToLowerInvariant() + password).ToUtf8()).ToHex();
            return string.Equals(parts[1], expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string _formCss = @"
            * { font-family: ""Calibri"", ""Verdana"", ""Arial"", sans-serif; }
            div { border: 1px solid black; -moz-border-radius: 1.4em 1.4em 1.4em 1.4em; width: auto; margin: 5em auto; display: inline-block; background: #def; padding: 0.3em 2em; }
            p { background: #bdf; padding: 0.3em 0.7em; -moz-border-radius: 0.7em 0.7em 0.7em 0.7em; }
            p.error { background: #fdb; border: 1px solid red; }
            form { text-align: center; }
            table { width: auto; margin: 0 auto; border-collapse: collapse; }
            td { padding: 0.3em 0.7em; text-align: left; }
            input[type=submit] { font-size: 125%; }
        ";

        private HttpResponse loginForm(string returnto, bool failed, string username, string password, IHttpUrl formSubmitUrl)
        {
            return HttpResponse.Html(
                new HTML(
                    new HEAD(
                        new TITLE("Log in"),
                        new STYLELiteral(_formCss)
                    ),
                    new BODY(
                        new FORM { method = method.post, action = formSubmitUrl.ToHref() }._(
                            new DIV(
                                returnto == null ? null : new INPUT { type = itype.hidden, name = "returnto", value = returnto },
                                new P("Please log in to access ", _appName, "."),
                                failed ? new P("The specified username and/or password has not been recognised.") { class_ = "error" } : null,
                                HtmlTag.HtmlTable(null,
                                    new object[] { "Username:", new INPUT { name = "username", type = itype.text, size = 60, value = username } },
                                    new object[] { "Password:", new INPUT { name = "password", type = itype.password, size = 60, value = password } },
                                    new[] { null, new INPUT { value = "Log in", type = itype.submit } }
                                )
                            )
                        )
                    )
                )
            );
        }

        private HttpResponse changePasswordHandler(HttpRequest req, string loggedInUser)
        {
            if (loggedInUser == null)
                return HttpResponse.Redirect(req.Url.WithPathOnly("/login").WithQuery("returnto", req.Url.ToHref()));

            if (req.Method != HttpMethod.Post)
                return changePasswordForm(loggedInUser, req.Url["returnto"], false, false, null, null, null, req.Url.WithoutQuery("returnto"));

            var oldpassword = req.Post["password"].Value;
            var newpassword = req.Post["newpassword1"].Value;
            var newpassword2 = req.Post["newpassword2"].Value;
            var returnTo = req.Post["returnto"].Value;

            if (loggedInUser == null || oldpassword == null || newpassword == null || newpassword2 == null)
                return HttpResponse.Redirect(returnTo == null ? req.Url : req.Url.WithQuery("returnto", returnTo));

            lock (_lock)
            {
                AuthUsers users;
                try { users = XmlClassify.LoadObjectFromXmlFile<AuthUsers>(_usersPath); }
                catch { users = null; }
                if (users == null)
                    users = new AuthUsers();

                var user = users.Users.FirstOrDefault(u => u.Username == loggedInUser);
                if (user == null || !verifyHash(oldpassword, user.PasswordHash))
                    return changePasswordForm(loggedInUser, returnTo, true, false, oldpassword, newpassword, newpassword2, req.Url.WithoutQuery("returnto"));

                if (newpassword2 != newpassword)
                    return changePasswordForm(loggedInUser, returnTo, false, true, oldpassword, newpassword, newpassword2, req.Url.WithoutQuery("returnto"));

                user.PasswordHash = createHash(newpassword);
                XmlClassify.SaveObjectToXmlFile<AuthUsers>(users, _usersPath);
                return HttpResponse.Redirect(returnTo ?? _defaultReturnTo);
            }
        }

        private static HttpResponse changePasswordForm(string loggedInUser, string returnTo, bool loginFailed, bool passwordsDiffer, string oldpassword, string newpassword1, string newpassword2, IHttpUrl formSubmitUrl)
        {
            return HttpResponse.Html(
                new HTML(
                    new HEAD(
                        new TITLE("Change Password"),
                        new STYLELiteral(_formCss)
                    ),
                    new BODY(
                        new FORM { method = method.post, action = formSubmitUrl.ToHref() }._(
                            new DIV(
                                returnTo == null ? null : new INPUT { type = itype.hidden, name = "returnto", value = returnTo },
                                new P("To change your password, type your old password, and then the new password twice."),
                                loginFailed ? new P("The specified old password is wrong.") { class_ = "error" } : null,
                                passwordsDiffer ? new P("The specified new passwords do not match. You have to type the same new password twice.") { class_ = "error" } : null,
                                HtmlTag.HtmlTable(null,
                                    new object[] { "Username:", new STRONG(loggedInUser) },
                                    new object[] { "Old password:", new INPUT { name = "password", type = itype.password, size = 60, value = oldpassword } },
                                    new object[] { "New password (1):", new INPUT { name = "newpassword1", type = itype.password, size = 60, value = newpassword1 } },
                                    new object[] { "New password (2):", new INPUT { name = "newpassword2", type = itype.password, size = 60, value = newpassword2 } },
                                    new[] { null, new INPUT { value = "Change password", type = itype.submit } }
                                )
                            )
                        )
                    )
                )
            );
        }

        private HttpResponse createUserHandler(HttpRequest req, string loggedInUserName)
        {
            AuthUsers users;
            lock (_lock)
            {
                try { users = XmlClassify.LoadObjectFromXmlFile<AuthUsers>(_usersPath); }
                catch { users = null; }
                if (users == null)
                    users = new AuthUsers();

                var loggedInUser = users.Users.FirstOrDefault(u => u.Username == loggedInUserName);
                if (loggedInUser == null || !loggedInUser.CanCreateUsers)
                    throw new HttpException(HttpStatusCode._401_Unauthorized);

                if (req.Method != HttpMethod.Post)
                    return createUserForm(req.Url["returnto"], false, false, null, null, null, req.Url.WithoutQuery("returnto"));

                var username = req.Post["username"].Value;
                var newpassword = req.Post["newpassword1"].Value;
                var newpassword2 = req.Post["newpassword2"].Value;
                var returnTo = req.Post["returnto"].Value;

                if (username == null || newpassword == null || newpassword2 == null)
                    // if returnTo is null, this removes the query parameter
                    return HttpResponse.Redirect(req.Url.WithQuery("returnto", returnTo));

                var user = users.Users.FirstOrDefault(u => u.Username == username);
                if (user != null)
                    return createUserForm(returnTo, true, false, username, newpassword, newpassword2, req.Url.WithoutQuery("returnto"));

                if (newpassword2 != newpassword)
                    return createUserForm(returnTo, false, true, username, newpassword, newpassword2, req.Url.WithoutQuery("returnto"));

                users.Users.Add(new AuthUser { Username = username, PasswordHash = createHash(newpassword) });
                XmlClassify.SaveObjectToXmlFile<AuthUsers>(users, _usersPath);
                return HttpResponse.Redirect(returnTo ?? _defaultReturnTo);
            }
        }

        /// <summary>Creates a new user. (Throws if the <paramref name="username"/> is already in use.)</summary>
        /// <param name="username">Username of the new user.</param>
        /// <param name="password">Password for the new user.</param>
        /// <exception cref="InvalidOperationException">The specified username is already in use.</exception>
        public void CreateUser(string username, string password)
        {
            if (username == null)
                throw new ArgumentNullException("username");
            if (password == null)
                throw new ArgumentNullException("password");

            lock (_lock)
            {
                AuthUsers users;
                try { users = XmlClassify.LoadObjectFromXmlFile<AuthUsers>(_usersPath); }
                catch { users = null; }
                if (users == null)
                    users = new AuthUsers();

                var user = users.Users.FirstOrDefault(u => u.Username == username);
                if (user != null)
                    throw new InvalidOperationException("The specified user already exists.");

                users.Users.Add(new AuthUser { Username = username, PasswordHash = createHash(password) });
                XmlClassify.SaveObjectToXmlFile<AuthUsers>(users, _usersPath);
            }
        }

        private static HttpResponse createUserForm(string returnTo, bool userAlreadyExists, bool passwordsDiffer, string username, string newpassword1, string newpassword2, IHttpUrl formSubmitUrl)
        {
            return HttpResponse.Html(
                new HTML(
                    new HEAD(
                        new TITLE("Create user"),
                        new STYLELiteral(_formCss)
                    ),
                    new BODY(
                        new FORM { method = method.post, action = formSubmitUrl.ToHref() }._(
                            new DIV(
                                returnTo == null ? null : new INPUT { type = itype.hidden, name = "returnto", value = returnTo },
                                new P("To create a new user, type the desired username and the new password twice."),
                                userAlreadyExists ? new P("The specified username is already in use.") { class_ = "error" } : null,
                                passwordsDiffer ? new P("The specified new passwords do not match. You have to type the same new password twice.") { class_ = "error" } : null,
                                HtmlTag.HtmlTable(null,
                                    new object[] { "Username:", new INPUT { name = "username", type = itype.text, size = 60, value = username } },
                                    new object[] { "New password (1):", new INPUT { name = "newpassword1", type = itype.password, size = 60, value = newpassword1 } },
                                    new object[] { "New password (2):", new INPUT { name = "newpassword2", type = itype.password, size = 60, value = newpassword2 } },
                                    new[] { null, new INPUT { value = "Create user", type = itype.submit } }
                                )
                            )
                        )
                    )
                )
            );
        }

        private bool addUser(string username, string password)
        {
            lock (_lock)
            {
                AuthUsers users;
                try { users = XmlClassify.LoadObjectFromXmlFile<AuthUsers>(_usersPath); }
                catch (FileNotFoundException) { users = new AuthUsers(); }

                bool created = false;
                var user = users.Users.FirstOrDefault(usr => usr.Username == username);
                if (user == null)
                {
                    user = new AuthUser { Username = username };
                    users.Users.Add(user);
                    created = true;
                }
                user.PasswordHash = createHash(password);

                XmlClassify.SaveObjectToXmlFile<AuthUsers>(users, _usersPath);
                return created;
            }
        }
    }

    sealed class AuthUser
    {
        public string Username;
        public string PasswordHash;
        public bool CanCreateUsers;
        public override string ToString() { return Username + (CanCreateUsers ? " (admin)" : ""); }
    }

    sealed class AuthUsers
    {
        public List<AuthUser> Users = new List<AuthUser>();
    }
}
