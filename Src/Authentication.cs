using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RT.Util.ExtensionMethods;
using RT.Util.Xml;
using RT.TagSoup.HtmlTags;

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
                return HttpServer.RedirectResponse(returnTo == null ? req.UrlWithoutQuery : req.UrlWithoutQuery + "?returnto=" + returnTo.UrlEscape());

            if (usersPath != null)
            {
                if (File.Exists(usersPath))
                {
                    var hash = getHash(username, password); ;
                    var users = XmlClassify.LoadObjectFromXmlFile<AuthUsers>(usersPath);
                    var user = users.Users.FirstOrDefault(u => u.Username == username && u.PasswordHash == hash);
                    if (user != null)
                    {
                        // Login successful!
                        setUsername(username);
                        return HttpServer.RedirectResponse(returnTo ?? defaultReturnTo);
                    }
                }
                else
                    XmlClassify.SaveObjectToXmlFile<AuthUsers>(new AuthUsers(), usersPath);
            }
            // Login failed.
            return loginForm(returnTo, true, username, password, req.UrlWithoutQuery, appName);
        }

        private static string getHash(string username, string password)
        {
            // SHA1 ( username + SHA1 ( password ) )
            return SHA1.Create().ComputeHash((username + SHA1.Create().ComputeHash(password.ToUtf8()).ToHex()).ToUtf8()).ToHex();
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
            return new HttpResponse(
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
                                new HtmlTable(null,
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
                return HttpServer.RedirectResponse(returnTo == null ? req.UrlWithoutQuery : req.UrlWithoutQuery + "?returnto=" + returnTo.UrlEscape());

            if (usersPath != null)
            {
                if (File.Exists(usersPath))
                {
                    var hash = getHash(username, oldpassword); ;
                    var users = XmlClassify.LoadObjectFromXmlFile<AuthUsers>(usersPath);
                    var user = users.Users.FirstOrDefault(u => u.Username == username);
                    if ((user == null && allowCreateNew && username != "") || user.PasswordHash == hash)
                    {
                        if (newpassword2 != newpassword)
                            return changePasswordForm(returnTo, false, true, username, oldpassword, newpassword, newpassword2, req.UrlWithoutQuery);

                        if (user == null)
                        {
                            user = new AuthUser { Username = username };
                            users.Users.Add(user);
                        }
                        user.PasswordHash = getHash(username, newpassword);
                        XmlClassify.SaveObjectToXmlFile<AuthUsers>(users, usersPath);
                        return HttpServer.RedirectResponse(returnTo ?? defaultReturnTo);
                    }
                }
                else
                    XmlClassify.SaveObjectToXmlFile<AuthUsers>(new AuthUsers(), usersPath);
            }
            // Username or old password was wrong
            return changePasswordForm(returnTo, true, false, username, oldpassword, newpassword, newpassword2, req.UrlWithoutQuery);
        }

        private static HttpResponse changePasswordForm(string returnto, bool loginFailed, bool passwordsDiffer, string username, string oldpassword, string newpassword1, string newpassword2, string url)
        {
            return new HttpResponse(
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
                                new HtmlTable(null,
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
    }

    class AuthUser
    {
        public string Username;
        public string PasswordHash;
    }

    class AuthUsers
    {
        public List<AuthUser> Users = new List<AuthUser>();
    }
}
