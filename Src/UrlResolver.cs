using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    ///     Allows you to specify mappings that map URLs to HTTP request handlers.</summary>
    /// <remarks>
    ///     Maintains a collection of <see cref="UrlMapping"/> objects, making sure that they are always enumerated in a
    ///     sensible order and ensuring that there are no duplicate hooks added in error. This class is thread-safe except for
    ///     enumeration; to enumerate the mappings safely, you must hold a lock on the instance until the enumeration is
    ///     completed.</remarks>
    public sealed class UrlResolver : ICollection<UrlMapping>
    {
        /// <summary>
        ///     Constructs an empty <see cref="UrlResolver"/> with no mappings. Use <see cref="Add(UrlMapping)"/>, <see
        ///     cref="Add(UrlHook, Func{HttpRequest, HttpResponse}, bool)"/> or <see cref="AddRange"/> to add mappings.</summary>
        public UrlResolver() { }
        /// <summary>Initializes this <see cref="UrlResolver"/> with the specified collection of mappings.</summary>
        public UrlResolver(IEnumerable<UrlMapping> mappings) { AddRange(mappings); }
        /// <summary>Initializes this <see cref="UrlResolver"/> with the specified collection of mappings.</summary>
        public UrlResolver(params UrlMapping[] mappings) { AddRange(mappings); }

        private object _locker = new object();

        /// <summary>Take a lock on this object to perform multiple add/remove/clear operations atomically.</summary>
        public object Locker { get { return _locker; } }

        /// <summary>
        ///     Handles an HTTP request by delegating it to the appropriate handler according to the request’s URL.</summary>
        /// <param name="req">
        ///     Incoming HTTP request.</param>
        /// <returns>
        ///     The HTTP response that was returned by the first applicable mapping.</returns>
        /// <remarks>
        ///     Assign this method to <see cref="HttpServer.Handler"/>.</remarks>
        public HttpResponse Handle(HttpRequest req)
        {
            Func<HttpResponse>[] applicableHandlers;
            lock (_locker)
            {
                applicableHandlers = _mappings.Where(mp =>
                        ((mp.Hook.Protocols.HasFlag(Protocols.Http) && !req.Url.Https) || (mp.Hook.Protocols.HasFlag(Protocols.Https) && req.Url.Https)) &&
                        (mp.Hook.Port == null || mp.Hook.Port.Value == req.Url.Port) &&
                        (mp.Hook.Domain == null || mp.Hook.Domain == req.Url.Domain || (!mp.Hook.SpecificDomain && req.Url.Domain.EndsWith("." + mp.Hook.Domain))) &&
                        (mp.Hook.Path == null || mp.Hook.Path == req.Url.Path || (!mp.Hook.SpecificPath && req.Url.Path.StartsWith(mp.Hook.Path + "/"))))
                    .Select(mapping => Ut.Lambda(() =>
                    {
                        if (mapping.Hook.Domain != null)
                        {
                            var parents = req.Url.ParentDomains;
                            req.Url.ParentDomains = new string[parents.Length + 1];
                            Array.Copy(parents, req.Url.ParentDomains, parents.Length);
                            req.Url.ParentDomains[parents.Length] = mapping.Hook.Domain;
                            req.Url.Domain = req.Url.Domain.Remove(req.Url.Domain.Length - mapping.Hook.Domain.Length);
                        }
                        if (mapping.Hook.Path != null)
                        {
                            var parents = req.Url.ParentPaths;
                            req.Url.ParentPaths = new string[parents.Length + 1];
                            Array.Copy(parents, req.Url.ParentPaths, parents.Length);
                            req.Url.ParentPaths[parents.Length] = mapping.Hook.Path;
                            req.Url.Path = req.Url.Path.Substring(mapping.Hook.Path.Length);
                        }
                        var response = mapping.Handler(req);
                        if (response == null && !mapping.Skippable)
                            throw new InvalidOperationException("The handler of a non-skippable mapping returned null. Mapping: {0}".Fmt(mapping));
                        return response;
                    }))
                    .ToArray();
            }
            var url = req.Url.ToUrl();
            foreach (var handler in applicableHandlers)
            {
                var response = handler();
                if (response != null)
                    return response;
                req.Url = url.ToUrl();
            }
            throw new HttpNotFoundException(url.ToFull());
        }

        private List<UrlMapping> _mappings = new List<UrlMapping>();

        /// <summary>Gets a value indicating the number of mappings in this collection.</summary>
        public int Count
        {
            get
            {
                lock (_locker)
                    return _mappings.Count;
            }
        }

        /// <summary>Determines whether a mapping with the same match specification is present in this collection.</summary>
        public bool Contains(UrlMapping item)
        {
            lock (_locker)
                return _mappings.BinarySearch(item) >= 0;
        }

        /// <summary>
        ///     Enumerates the mappings. To maintain thread safety, you must hold a lock on <see cref="Locker"/> until the
        ///     enumeration is finished.</summary>
        public IEnumerator<UrlMapping> GetEnumerator() { return _mappings.GetEnumerator(); }

        /// <summary>
        ///     Enumerates the mappings. To maintain thread safety, you must hold a lock on <see cref="Locker"/> until the
        ///     enumeration is finished.</summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }

        /// <summary>Throws NotImplementedException.</summary>
        public void CopyTo(UrlMapping[] array, int arrayIndex) { throw new NotImplementedException(); }

        /// <summary>Returns false.</summary>
        public bool IsReadOnly { get { return false; } }

        /// <summary>Removes all mappings from this collection.</summary>
        public void Clear()
        {
            lock (_locker)
                _mappings.Clear();
        }

        /// <summary>Adds the specified mapping to this collection.</summary>
        public void Add(UrlMapping item)
        {
            lock (_locker)
            {
                int i = _mappings.BinarySearch(item);
                if (i >= 0 && !item.Skippable) // skippable hooks are never considered duplicates
                {
                    // Need to check for duplicates both up and down from here, because CompareTo == 0 doesn't imply equality.
                    if (item.Equals(_mappings[i]))
                        throw newDuplicateMappingException(item);
                    for (int k = i - 1; k >= 0 && _mappings[k].CompareTo(item) == 0; k--)
                        if (item.Equals(_mappings[k]))
                            throw newDuplicateMappingException(item);
                    for (i++; i < _mappings.Count && _mappings[i].CompareTo(item) == 0; i++)
                        if (item.Equals(_mappings[i]))
                            throw newDuplicateMappingException(item);
                }
                else
                    i = ~i;
                _mappings.Insert(i, item);
            }
        }

        /// <summary>Adds the specified mapping to this collection.</summary>
        public void Add(UrlHook hook, Func<HttpRequest, HttpResponse> handler, bool skippable = false)
        {
            if (hook == null)
                throw new ArgumentNullException("hook");
            if (handler == null)
                throw new ArgumentNullException("handler");
            Add(new UrlMapping(hook, handler, skippable));
        }

        /// <summary>
        ///     Efficiently adds multiple mappings to this collection. Use this method whenever adding multiple mappings at
        ///     once.</summary>
        public void AddRange(IEnumerable<UrlMapping> items)
        {
            lock (_locker)
            {
                var mappings = _mappings.Concat(items).ToList();
                if (mappings.Count == _mappings.Count)
                    return;
                mappings.Sort();
                var curEqual = 0;
                for (int i = 1; i < mappings.Count; i++)
                {
                    if (mappings[i].CompareTo(mappings[curEqual]) != 0)
                        curEqual = i;
                    if (!mappings[i].Skippable) // skippable mappings are never considered duplicates
                        for (int j = curEqual; j < i; j++)
                            if (mappings[i].Equals(mappings[j]))
                                throw newDuplicateMappingException(mappings[i]);
                }
                _mappings = mappings;
            }
        }

        private Exception newDuplicateMappingException(UrlMapping mapping)
        {
            return new InvalidOperationException("There is already an equivalent URL mapping; the new mapping would always be ignored. Mapping: " + mapping.ToString());
        }

        /// <summary>
        ///     Removes a mapping that is equal to the specified mapping.</summary>
        /// <remarks>
        ///     This will remove a mapping with a different handler if everything else about it is equal. See <see
        ///     cref="UrlMapping.Equals(UrlMapping)"/>.</remarks>
        public bool Remove(UrlMapping item)
        {
            lock (_locker)
                return _mappings.Remove(item);
        }

        /// <summary>
        ///     Removes all mappings with a hook equivalent to the specified one.</summary>
        /// <returns>
        ///     The number of elements removed.</returns>
        public int Remove(UrlHook hook)
        {
            lock (_locker)
                return _mappings.RemoveAll(item => item.Hook.Equals(hook));
        }
    }
}
