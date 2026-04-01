using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RT.Serialization;

namespace RT.Servers;

/// <summary>
///     Provides functionality for a login system.</summary>
/// <param name="usersFilePath">
///     Specifies the path and filename of an XML file containing the users and passwords.</param>
/// <param name="defaultReturnTo">
///     Default URL to redirect to when a login attempt is successful. This can be overridden by a "returnto" GET parameter.</param>
/// <param name="appName">
///     Name of the application which uses this authentication handler.</param>
public sealed class FileAuthenticator(string usersFilePath, Func<IHttpUrl, string> defaultReturnTo, string appName) : Authenticator(defaultReturnTo, appName)
{
    private string _usersPath = usersFilePath ?? throw new ArgumentNullException("usersFilePath");

    /// <summary>
    ///     Used to ensure that the AuthUsers XML file is not read and written concurrently.</summary>
    /// <remarks>
    ///     static in case multiple instances of <see cref="FileAuthenticator"/> use the same XML file.</remarks>
    private static object _lock = new();

    /// <summary>See base.</summary>
    protected override bool getUser(ref string username, out string passwordHash, out bool canCreateUsers)
    {
        passwordHash = null;
        canCreateUsers = false;
        if (File.Exists(_usersPath))
        {
            AuthUsers users;
            lock (_lock)
                users = ClassifyXml.DeserializeFile<AuthUsers>(_usersPath);
            var un = username;  // ref parameter can’t be used in a lambda :(
            var user = users.Users.FirstOrDefault(u => u.Username == un);
            if (user == null)
                return false;
            username = user.Username;
            passwordHash = user.PasswordHash;
            canCreateUsers = user.CanCreateUsers;
            return true;
        }
        else
            lock (_lock)
                ClassifyXml.SerializeToFile(new AuthUsers(), _usersPath);
        return false;
    }

    /// <summary>See base.</summary>
    protected override bool changePassword(string username, string newPasswordHash, Func<string, bool> verifyOldPasswordHash)
    {
        lock (_lock)
        {
            AuthUsers users;
            try { users = ClassifyXml.DeserializeFile<AuthUsers>(_usersPath); }
            catch { users = null; }
            users ??= new AuthUsers();

            var user = users.Users.FirstOrDefault(u => u.Username == username);
            if (user == null || (verifyOldPasswordHash != null && !verifyOldPasswordHash(user.PasswordHash)))
                return false;

            user.PasswordHash = newPasswordHash;
            ClassifyXml.SerializeToFile<AuthUsers>(users, _usersPath);
            return true;
        }
    }

    /// <summary>See base.</summary>
    protected override bool createUser(string username, string passwordHash, bool canCreateUsers)
    {
        lock (_lock)
        {
            AuthUsers users;
            try { users = ClassifyXml.DeserializeFile<AuthUsers>(_usersPath); }
            catch { users = null; }
            users ??= new AuthUsers();

            if (users.Users.Any(u => u.Username == username))
                return false;

            users.Users.Add(new AuthUser { Username = username, PasswordHash = passwordHash, CanCreateUsers = canCreateUsers });
            ClassifyXml.SerializeToFile<AuthUsers>(users, _usersPath);
            return true;
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
    public List<AuthUser> Users = [];
}
