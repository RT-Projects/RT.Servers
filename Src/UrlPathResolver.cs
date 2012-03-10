using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>Allows you to specify “hooks” that map a URL path to an HTTP handler.</summary>
    /// <remarks>Maintains a collection of <see cref="UrlPathHook"/> objects, making sure that they are always enumerated
    /// in a sensible order, ensuring that there are no duplicate hooks added in error. This class is thread-safe except for
    /// enumeration; to enumerate the hooks safely, you must hold a lock on the instance until the enumeration is completed.</remarks>
    public sealed class UrlPathResolver : ICollection<UrlPathHook>
    {
        /// <summary>Constructs an empty <see cref="UrlPathResolver"/> with no hooks. Use <see cref="Add"/> or <see cref="AddRange"/> to add hooks.</summary>
        public UrlPathResolver() { }
        /// <summary>Initializes this <see cref="UrlPathResolver"/> with the specified collection of hooks.</summary>
        public UrlPathResolver(IEnumerable<UrlPathHook> hooks) { AddRange(hooks); }
        /// <summary>Initializes this <see cref="UrlPathResolver"/> with the specified collection of hooks.</summary>
        public UrlPathResolver(params UrlPathHook[] hooks) { AddRange(hooks); }

        /// <summary>Handles an HTTP request by delegating it to the appropriate hook according to the request’s URL.</summary>
        /// <param name="req">Incoming HTTP request.</param>
        /// <returns>The HTTP response that was returned by the first applicable hook.</returns>
        /// <remarks>Assign this method to <see cref="HttpServer.Handler"/>.</remarks>
        public HttpResponse Handle(HttpRequest req)
        {
            string host = req.Headers.Host;
            int port = 80;
            if (host.Contains(":"))
            {
                int pos = host.IndexOf(":");
                if (!int.TryParse(host.Substring(pos + 1), out port))
                    port = 80;
                host = host.Remove(pos);
            }
            host = host.TrimEnd('.');

            string url = req.UrlWithoutQuery;

            Func<HttpResponse>[] applicableHandlers;
            lock (_hooks)
            {
                applicableHandlers = _hooks.Where(hk => (hk.Port == null || hk.Port.Value == port) &&
                        (hk.Domain == null || hk.Domain == host || (!hk.SpecificDomain && host.EndsWith("." + hk.Domain))) &&
                        (hk.Path == null || hk.Path == req.UrlWithoutQuery || (!hk.SpecificPath && req.Url.StartsWith(hk.Path + "/"))))
                    .Select(hook => Ut.Lambda(() =>
                    {
                        var response = hook.Handler(new UrlPathRequest(
                            copyFrom: req,
                            baseUrl: hook.Path == null ? "" : hook.Path,
                            restUrl: hook.Path == null ? req.Url : req.Url.Substring(hook.Path.Length),
                            baseDomain: hook.Domain == null ? "" : hook.Domain,
                            restDomain: hook.Domain == null ? host : host.Remove(host.Length - hook.Domain.Length)
                        ));
                        if (response == null && !hook.Skippable)
                            throw new InvalidOperationException("The handler of a non-skippable hook returned null. Hook: {0}".Fmt(hook));
                        return response;
                    }))
                    .ToArray();
            }
            foreach (var handler in applicableHandlers)
            {
                var response = handler();
                if (response != null)
                    return response;
            }
            return HttpResponse.Error(HttpStatusCode._404_NotFound, headers: new HttpResponseHeaders { Connection = HttpConnection.Close });
        }

        private List<UrlPathHook> _hooks = new List<UrlPathHook>();

        /// <summary>Gets a value indicating the number of hooks in this collection.</summary>
        public int Count
        {
            get
            {
                lock (this)
                    return _hooks.Count;
            }
        }

        /// <summary>Determines whether a hook with the same match specification is present in this collection.</summary>
        public bool Contains(UrlPathHook item)
        {
            lock (this)
                return _hooks.BinarySearch(item) >= 0;
        }

        /// <summary>Enumerates the hooks. To maintain thread safety, you must hold a lock on the instance until the enumeration is finished.</summary>
        public IEnumerator<UrlPathHook> GetEnumerator() { return _hooks.GetEnumerator(); }

        /// <summary>Enumerates the hooks. To maintain thread safety, you must hold a lock on the instance until the enumeration is finished.</summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }

        /// <summary>Throws a "not implemented" exception.</summary>
        public void CopyTo(UrlPathHook[] array, int arrayIndex) { throw new NotImplementedException(); }

        /// <summary>Returns false.</summary>
        public bool IsReadOnly { get { return false; } }

        /// <summary>Removes all hooks from this collection.</summary>
        public void Clear()
        {
            lock (this)
                _hooks.Clear();
        }

        /// <summary>Adds the specified hook to this collection.</summary>
        public void Add(UrlPathHook item)
        {
            lock (this)
            {
                int i = _hooks.BinarySearch(item);
                if (i >= 0 && !item.Skippable) // skippable hooks are never considered duplicates
                {
                    // Need to check for duplicates both up and down from here, because CompareTo == 0 doesn't imply equality.
                    if (item.Equals(_hooks[i]))
                        throw newDuplicateHookException(item);
                    for (int k = i - 1; k >= 0 && _hooks[k].CompareTo(item) == 0; k--)
                        if (item.Equals(_hooks[k]))
                            throw newDuplicateHookException(item);
                    for (i++; i < _hooks.Count && _hooks[i].CompareTo(item) == 0; i++)
                        if (item.Equals(_hooks[i]))
                            throw newDuplicateHookException(item);
                }
                else
                    i = ~i;
                _hooks.Insert(i, item);
            }
        }

        /// <summary>Efficiently adds multiple hooks to this collection. Use this method whenever adding multiple hooks at once.</summary>
        public void AddRange(IEnumerable<UrlPathHook> items)
        {
            lock (this)
            {
                var hooks = _hooks.Concat(items).ToList();
                if (hooks.Count == _hooks.Count)
                    return;
                hooks.Sort();
                var curEqual = 0;
                for (int i = 1; i < hooks.Count; i++)
                {
                    if (hooks[i].CompareTo(hooks[curEqual]) != 0)
                        curEqual = i;
                    if (!hooks[i].Skippable) // skippable hooks are never considered duplicates
                        for (int j = curEqual; j < i; j++)
                            if (hooks[i].Equals(hooks[j]))
                                throw newDuplicateHookException(hooks[i]);
                }
                _hooks = hooks;
            }
        }

        private Exception newDuplicateHookException(UrlPathHook hook)
        {
            return new InvalidOperationException("There is already a request handler hook with the same match specification; the new one would always be ignored. Hook: " + hook.ToString());
        }

        /// <summary>Removes a hook with the given match specification.</summary>
        public bool Remove(UrlPathHook item)
        {
            lock (this)
                return _hooks.Remove(item);
        }
    }
}
