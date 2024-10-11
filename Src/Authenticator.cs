using System;
using System.Security.Cryptography;
using RT.TagSoup;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    ///     Provides functionality for a login system.</summary>
    /// <param name="defaultReturnTo">
    ///     Default URL to redirect to when a login attempt is successful. This can be overridden by a "returnto" GET
    ///     parameter.</param>
    /// <param name="appName">
    ///     Name of the application which uses this authentication handler.</param>
    public abstract class Authenticator(Func<IHttpUrl, string> defaultReturnTo, string appName)
    {
        private readonly Func<IHttpUrl, string> _defaultReturnTo = defaultReturnTo ?? throw new ArgumentNullException("defaultReturnTo");
        private readonly string _appName = appName ?? throw new ArgumentNullException("appName");

        /// <summary>
        ///     Handles a request.</summary>
        /// <param name="request">
        ///     Request to handle.</param>
        /// <param name="setUsername">
        ///     Action to call when login is successful. Typically used to set the username in a session.</param>
        /// <param name="loggedInUser">
        ///     Username of the user currently logged in (typically read from a session).</param>
        public HttpResponse Handle(HttpRequest request, string loggedInUser, Action<string> setUsername)
        {
            return new UrlResolver(
                new UrlMapping(path: "/login", handler: req => loginHandler(req, setUsername)),
                new UrlMapping(path: "/changepassword", handler: req => changePasswordHandler(req, loggedInUser)),
                new UrlMapping(path: "/createuser", handler: req => createUserHandler(req, loggedInUser)),
                new UrlMapping(path: "/logout", handler: req => logoutHandler(req, setUsername))
            ).Handle(request);
        }

        /// <summary>
        ///     Creates a new user. (Throws if the <paramref name="username"/> is already in use.)</summary>
        /// <param name="username">
        ///     Username of the new user.</param>
        /// <param name="password">
        ///     Password for the new user.</param>
        /// <param name="canCreateUsers">
        ///     Indicates whether the new user should have the right to create new users.</param>
        /// <exception cref="InvalidOperationException">
        ///     The specified username is already in use.</exception>
        public void CreateUser(string username, string password, bool canCreateUsers = false)
        {
            if (username == null)
                throw new ArgumentNullException("username");
            if (password == null)
                throw new ArgumentNullException("password");

            if (!createUser(username, createHash(password), canCreateUsers))
                throw new InvalidOperationException("The specified user already exists.");
        }

        /// <summary>
        ///     When overridden in a derived class, retrieves the username and password hash of a user.</summary>
        /// <param name="username">
        ///     Identifies the user to be verified, and passes out the user’s correct username. (For example, if usernames are
        ///     case-insensitive, the name is changed to the correct capitalization.)</param>
        /// <param name="passwordHash">
        ///     Receives the password hash for the user.</param>
        /// <param name="canCreateUsers">
        ///     Receives a value indicating whether this user has the right to create new users.</param>
        /// <returns>
        ///     <c>true</c> if the user exists; <c>false</c> otherwise.</returns>
        /// <remarks>
        ///     If the user does not exist, the overridden method must return <c>false</c> and not modify <paramref
        ///     name="username"/>.</remarks>
        protected abstract bool getUser(ref string username, out string passwordHash, out bool canCreateUsers);

        /// <summary>
        ///     When overridden in a derived class, attempts to change a user’s password.</summary>
        /// <param name="username">
        ///     The user whose password is to be changed.</param>
        /// <param name="newPasswordHash">
        ///     The new password hash for the user.</param>
        /// <param name="verifyOldPasswordHash">
        ///     A function which, if not <c>null</c>, must be called to verify that the old password is valid.</param>
        /// <returns>
        ///     <c>true</c> if the password was successfully changed; <c>false</c> if the specified user does not exist or
        ///     <paramref name="verifyOldPasswordHash"/> returned <c>false</c> indicating that the old password hash turned
        ///     out to be invalid.</returns>
        protected abstract bool changePassword(string username, string newPasswordHash, Func<string, bool> verifyOldPasswordHash);

        /// <summary>
        ///     When overridden in a derived class, creates a new user.</summary>
        /// <param name="username">
        ///     Username for the new user.</param>
        /// <param name="passwordHash">
        ///     Hashed password for the new user.</param>
        /// <param name="canCreateUsers">
        ///     Specifies whether the new user has the right to create new users.</param>
        /// <returns>
        ///     <c>true</c> if a new user was created; <c>false</c> if <paramref name="username"/> is already taken.</returns>
        protected abstract bool createUser(string username, string passwordHash, bool canCreateUsers);

        private HttpResponse logoutHandler(HttpRequest req, Action<string> setUsername)
        {
            setUsername(null);
            return HttpResponse.Redirect(req.Url.WithParent("login"));
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

            if (getUser(ref username, out var passwordHash, out var canCreateUsers) && verifyHash(password, passwordHash))
            {
                // Login successful!
                setUsername(username);
                return HttpResponse.Redirect(returnTo ?? _defaultReturnTo(req.Url));
            }

            // Login failed.
            return loginForm(returnTo, true, username, password, req.Url);
        }

        private HttpResponse changePasswordHandler(HttpRequest req, string loggedInUser)
        {
            if (loggedInUser == null)
                return HttpResponse.Redirect(req.Url.WithPathParent().WithPathOnly("/login").WithQuery("returnto", req.Url.ToHref()));

            if (req.Method != HttpMethod.Post)
                return changePasswordForm(loggedInUser, req.Url["returnto"], false, false, null, null, null, req.Url.WithoutQuery("returnto"));

            var oldpassword = req.Post["password"].Value;
            var newpassword = req.Post["newpassword1"].Value;
            var newpassword2 = req.Post["newpassword2"].Value;
            var returnTo = req.Post["returnto"].Value;

            if (loggedInUser == null || oldpassword == null || newpassword == null || newpassword2 == null)
                return HttpResponse.Redirect(returnTo == null ? req.Url : req.Url.WithQuery("returnto", returnTo));

            if (newpassword2 != newpassword)
                return changePasswordForm(loggedInUser, returnTo, false, true, oldpassword, newpassword, newpassword2, req.Url.WithoutQuery("returnto"));

            if (!changePassword(loggedInUser, createHash(newpassword), h => verifyHash(oldpassword, h)))
                return changePasswordForm(loggedInUser, req.Url["returnto"], true, false, oldpassword, newpassword, newpassword2, req.Url.WithoutQuery("returnto"));

            return HttpResponse.Redirect(returnTo ?? _defaultReturnTo(req.Url));
        }

        private HttpResponse createUserHandler(HttpRequest req, string loggedInUserName)
        {
            if (req.Method != HttpMethod.Post)
                return createUserForm(req.Url["returnto"], false, false, null, null, null, req.Url.WithoutQuery("returnto"));

            var username = req.Post["username"].Value;
            var newpassword = req.Post["newpassword1"].Value;
            var newpassword2 = req.Post["newpassword2"].Value;
            var returnTo = req.Post["returnto"].Value;

            if (username == null || newpassword == null || newpassword2 == null)
                // if returnTo is null, this removes the query parameter
                return HttpResponse.Redirect(req.Url.WithQuery("returnto", returnTo));

            if (!getUser(ref loggedInUserName, out var passwordHash, out var canCreateUsers) || !canCreateUsers)
                throw new HttpException(HttpStatusCode._401_Unauthorized);

            if (newpassword2 != newpassword)
                // Passwords don’t match.
                return createUserForm(returnTo, false, true, username, newpassword, newpassword2, req.Url.WithoutQuery("returnto"));

            if (!createUser(username, createHash(newpassword), false))
                // The user already exists.
                return createUserForm(returnTo, true, false, username, newpassword, newpassword2, req.Url.WithoutQuery("returnto"));

            // Success.
            return HttpResponse.Redirect(returnTo ?? _defaultReturnTo(req.Url));
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
            if (hash.Length == 0)
                return password.Length == 0;
            var parts = hash.Split(':');
            if (parts.Length != 2)
                return false;
            var expected = SHA1.Create().ComputeHash((parts[0].ToLowerInvariant() + password).ToUtf8()).ToHex();
            return string.Equals(parts[1], expected, StringComparison.OrdinalIgnoreCase);
        }

        private HttpResponse loginForm(string returnto, bool failed, string username, string password, IHttpUrl formSubmitUrl)
        {
            return HttpResponse.Html(
                new HTML(
                    new HEAD(
                        new TITLE("Log in"),
                        new STYLELiteral(_formCss),
                        new META { name = "viewport", content = "width=device-width,initial-scale=1.0" }
                    ),
                    new BODY(
                        new FORM { method = method.post, action = formSubmitUrl.ToHref() }._(
                            new DIV(
                                returnto == null ? null : new INPUT { type = itype.hidden, name = "returnto", value = returnto },
                                new P("Please log in to access ", _appName, "."),
                                failed ? new P("The specified username and/or password has not been recognised.") { class_ = "error" } : null,
                                Tag.HtmlTable(null,
                                    ["Username:", new INPUT { name = "username", type = itype.text, size = 60, value = username }],
                                    ["Password:", new INPUT { name = "password", type = itype.password, size = 60, value = password }],
                                    [null, new INPUT { value = "Log in", type = itype.submit }]
                                )
                            )
                        )
                    )
                )
            );
        }

        private HttpResponse createUserForm(string returnTo, bool userAlreadyExists, bool passwordsDiffer, string username, string newpassword1, string newpassword2, IHttpUrl formSubmitUrl)
        {
            return HttpResponse.Html(
                new HTML(
                    new HEAD(
                        new TITLE("Create user"),
                        new STYLELiteral(_formCss),
                        new META { name = "viewport", content = "width=device-width,initial-scale=1.0" }
                    ),
                    new BODY(
                        new FORM { method = method.post, action = formSubmitUrl.ToHref() }._(
                            new DIV(
                                returnTo == null ? null : new INPUT { type = itype.hidden, name = "returnto", value = returnTo },
                                new P("To create a new user, type the desired username and the new password twice."),
                                userAlreadyExists ? new P("The specified username is already in use.") { class_ = "error" } : null,
                                passwordsDiffer ? new P("The specified new passwords do not match. You have to type the same new password twice.") { class_ = "error" } : null,
                                Tag.HtmlTable(null,
                                    ["Username:", new INPUT { name = "username", type = itype.text, size = 60, value = username }],
                                    ["New password (1):", new INPUT { name = "newpassword1", type = itype.password, size = 60, value = newpassword1 }],
                                    ["New password (2):", new INPUT { name = "newpassword2", type = itype.password, size = 60, value = newpassword2 }],
                                    [null, new INPUT { value = "Create user", type = itype.submit }]
                                )
                            )
                        )
                    )
                )
            );
        }

        private HttpResponse changePasswordForm(string loggedInUser, string returnTo, bool loginFailed, bool passwordsDiffer, string oldpassword, string newpassword1, string newpassword2, IHttpUrl formSubmitUrl) => HttpResponse.Html(
            new HTML(
                new HEAD(
                    new TITLE("Change Password"),
                    new STYLELiteral(_formCss),
                    new META { name = "viewport", content = "width=device-width,initial-scale=1.0" }),
                new BODY(
                    new FORM { method = method.post, action = formSubmitUrl.ToHref() }._(
                        new DIV(
                            returnTo == null ? null : new INPUT { type = itype.hidden, name = "returnto", value = returnTo },
                            new P("To change your password, type your old password, and then the new password twice."),
                            loginFailed ? new P("The specified old password is wrong.") { class_ = "error" } : null,
                            passwordsDiffer ? new P("The specified new passwords do not match. You have to type the same new password twice.") { class_ = "error" } : null,
                            Tag.HtmlTable(null,
                                ["Username:", new STRONG(loggedInUser)],
                                ["Old password:", new INPUT { name = "password", type = itype.password, size = 60, value = oldpassword }],
                                ["New password (1):", new INPUT { name = "newpassword1", type = itype.password, size = 60, value = newpassword1 }],
                                ["New password (2):", new INPUT { name = "newpassword2", type = itype.password, size = 60, value = newpassword2 }],
                                [null, new INPUT { value = "Change password", type = itype.submit }]))))));

        private static readonly string _formCss = @"
            * { font-family: ""Calibri"", ""Verdana"", ""Arial"", sans-serif; }
            div { border: 1px solid black; -moz-border-radius: 1.4em 1.4em 1.4em 1.4em; width: auto; margin: 5em auto; display: inline-block; background: #def; padding: 0.3em 2em; }
            p { background: #bdf; padding: 0.3em 0.7em; -moz-border-radius: 0.7em 0.7em 0.7em 0.7em; }
            p.error { background: #fdb; border: 1px solid red; }
            form { text-align: center; }
            table { width: auto; margin: 0 auto; border-collapse: collapse; }
            td { padding: 0.3em 0.7em; text-align: left; }
            input[type=submit] { font-size: 125%; }
        ";
    }
}
