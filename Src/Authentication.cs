using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RT.TagSoup.HtmlTags;
using RT.Util.ExtensionMethods;
using RT.Util.Xml;

namespace RT.Servers
{
    /// <summary>Provides static methods for a login form and a change-password form.</summary>
    public static class Authentication
    {
        /// <summary>Returns a login form or processes a login attempt.</summary>
        /// <param name="req">Request to process.</param>
        /// <param name="usersPath">Path to the file in which usernames and passwords are stored.</param>
        /// <param name="setUsername">Action to call when login is successful. Use this to set the username in your custom session, for example.</param>
        /// <param name="defaultReturnTo">Default URL to redirect to when a login attempt is successful. This can be overridden by a "returnto" GET parameter.</param>
        /// <param name="appName">Name of the application which uses this login handler.</param>
        public static HttpResponse LoginHandler(HttpRequest req, string usersPath, Action<string> setUsername, string defaultReturnTo, string appName)
        {
            if (req.Method != HttpMethod.Post)
                return loginForm(req.Get["returnto"].Value, false, null, null, req.UrlWithoutQuery, appName);

            var username = req.Post["username"].Value;
            var password = req.Post["password"].Value;
            var returnTo = req.Post["returnto"].Value;

            if (username == null || password == null)
                return HttpResponse.Redirect(returnTo == null ? req.UrlWithoutQuery : req.UrlWithoutQuery + "?returnto=" + returnTo.UrlEscape());

            if (usersPath != null)
            {
                if (File.Exists(usersPath))
                {
                    AuthUsers users;
                    lock (_lock)
                        users = XmlClassify.LoadObjectFromXmlFile<AuthUsers>(usersPath);
                    var user = users.Users.FirstOrDefault(u => u.Username == username && verifyHash(password, u.PasswordHash));
                    if (user != null)
                    {
                        // Login successful!
                        setUsername(username);
                        return HttpResponse.Redirect(returnTo ?? defaultReturnTo);
                    }
                }
                else
                    lock (_lock)
                        XmlClassify.SaveObjectToXmlFile<AuthUsers>(new AuthUsers(), usersPath);
            }
            // Login failed.
            return loginForm(returnTo, true, username, password, req.UrlWithoutQuery, appName);
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

        private static HttpResponse loginForm(string returnto, bool failed, string username, string password, string url, string appName)
        {
            return HttpResponse.Html(
                new HTML(
                    new HEAD(
                        new TITLE("Log in"),
                        new STYLELiteral(_formCss)
                    ),
                    new BODY(
                        new FORM { method = method.post, action = url }._(
                            new DIV(
                                returnto == null ? null : new INPUT { type = itype.hidden, name = "returnto", value = returnto },
                                new P("Please log in to access ", appName, "."),
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

        /// <summary>Returns a change-password form or processes a change-password attempt.</summary>
        /// <param name="req">Request to process.</param>
        /// <param name="usersPath">Path to the file in which usernames and passwords are stored.</param>
        /// <param name="defaultReturnTo">Default URL to redirect to when a change-password attempt is successful. This can be overridden by a "returnto" GET parameter.</param>
        /// <param name="allowCreateNew">If true, existing users can create new users by "changing" the password of a non-existent user. Once created, the new user can login and change their password again.</param>
        public static HttpResponse ChangePasswordHandler(HttpRequest req, string usersPath, string defaultReturnTo, bool allowCreateNew)
        {
            if (req.Method != HttpMethod.Post)
                return changePasswordForm(req.Get["returnto"].Value, false, false, null, null, null, null, req.UrlWithoutQuery);

            var username = req.Post["username"].Value;
            var oldpassword = req.Post["password"].Value;
            var newpassword = req.Post["newpassword1"].Value;
            var newpassword2 = req.Post["newpassword2"].Value;
            var returnTo = req.Post["returnto"].Value;

            if (username == null || oldpassword == null || newpassword == null || newpassword2 == null)
                return HttpResponse.Redirect(returnTo == null ? req.UrlWithoutQuery : req.UrlWithoutQuery + "?returnto=" + returnTo.UrlEscape());

            if (usersPath != null)
            {
                lock (_lock)
                {
                    AuthUsers users;
                    try { users = XmlClassify.LoadObjectFromXmlFile<AuthUsers>(usersPath); }
                    catch { users = null; }
                    if (users != null)
                    {
                        var user = users.Users.FirstOrDefault(u => u.Username == username);
                        if ((user == null && allowCreateNew && username != "") || verifyHash(oldpassword, user.PasswordHash))
                        {
                            if (newpassword2 != newpassword)
                                return changePasswordForm(returnTo, false, true, username, oldpassword, newpassword, newpassword2, req.UrlWithoutQuery);

                            if (user == null)
                            {
                                user = new AuthUser { Username = username };
                                users.Users.Add(user);
                            }
                            user.PasswordHash = createHash(newpassword);
                            XmlClassify.SaveObjectToXmlFile<AuthUsers>(users, usersPath);
                            return HttpResponse.Redirect(returnTo ?? defaultReturnTo);
                        }
                    }
                    else
                        XmlClassify.SaveObjectToXmlFile<AuthUsers>(new AuthUsers(), usersPath);
                }
            }
            // Username or old password was wrong
            return changePasswordForm(returnTo, true, false, username, oldpassword, newpassword, newpassword2, req.UrlWithoutQuery);
        }

        private static HttpResponse changePasswordForm(string returnto, bool loginFailed, bool passwordsDiffer, string username, string oldpassword, string newpassword1, string newpassword2, string url)
        {
            return HttpResponse.Html(
                new HTML(
                    new HEAD(
                        new TITLE("Change Password"),
                        new STYLELiteral(_formCss)
                    ),
                    new BODY(
                        new FORM { method = method.post, action = url }._(
                            new DIV(
                                returnto == null ? null : new INPUT { type = itype.hidden, name = "returnto", value = returnto },
                                new P("To change your password, type your username, the old password, and then the new password twice."),
                                loginFailed ? new P("The specified username or old password is wrong.") { class_ = "error" } : null,
                                passwordsDiffer ? new P("The specified new passwords do not match. You have to type the same new password twice.") { class_ = "error" } : null,
                                HtmlTag.HtmlTable(null,
                                    new object[] { "Username:", new INPUT { name = "username", type = itype.text, size = 60, value = username } },
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

        /// <summary>
        /// Creates a new user with the specified username/password, or sets the existing user's password.
        /// </summary>
        /// <param name="usersPath">Path to the file in which usernames and passwords are stored.</param>
        /// <param name="username">Username, case sensitive, to be added or modified.</param>
        /// <param name="password">Password that the new/updated user should have.</param>
        /// <returns>True if a new user was created, false if an existing one was modified.</returns>
        public static bool AddUser(string usersPath, string username, string password)
        {
            lock (_lock)
            {
                AuthUsers users;
                try { users = XmlClassify.LoadObjectFromXmlFile<AuthUsers>(usersPath); }
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

                XmlClassify.SaveObjectToXmlFile<AuthUsers>(users, usersPath);
                return created;
            }
        }

        /// <summary>Used to ensure that the AuthUsers XML file is not read and written concurrently.</summary>
        private static object _lock = new object();
    }

    sealed class AuthUser
    {
        public string Username;
        public string PasswordHash;
    }

    sealed class AuthUsers
    {
        public List<AuthUser> Users = new List<AuthUser>();
    }
}
