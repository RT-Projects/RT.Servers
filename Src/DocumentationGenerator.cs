using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using RT.TagSoup.HtmlTags;
using RT.Util.Collections;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;

namespace RT.Servers
{
    /// <summary>
    /// Provides an <see cref="HttpRequestHandler"/> that generates web pages from C# XML documentation.
    /// </summary>
    public class DocumentationGenerator
    {
        private SortedDictionary<string, SortedDictionary<string, Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>>> _tree;
        private SortedDictionary<string, Tuple<Type, XElement>> _typeDocumentation;
        private SortedDictionary<string, Tuple<MemberInfo, XElement>> _memberDocumentation;

        private static string _css = @"
            body { font-family: ""Verdana"", sans-serif; font-size: 9pt; margin: 0; }
            .namespace a { font-weight: bold; }
            .sidebar li.type { font-weight: bold; }
            .sidebar li.type > ul { font-weight: normal; }
            .sidebar li { padding-left: 1em; text-indent: -1em; }
            .sidebar li.Constructor { background: #dfd; }
            .sidebar li.Method { background: #ddf; }
            .sidebar li.Property { background: #fdf; }
            .sidebar li.Event { background: #fdd; }
            .sidebar li.Field { background: #ffd; }
            .sidebar ul.legend { display: block; margin: 0; padding: 0.3em 0; }
            .sidebar ul.legend li { display: inline; font-weight: bold; padding: 0.3em 0.7em; }
            .sidebar > ul { clear: left; padding-left: 2em; padding-top: 1em; }
            ul { padding-left: 1.5em; margin-bottom: 1em; }
            li.member { padding-right: 1em; }
            li.member, li.type { list-style-image: url(data:image/png;base64,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); }
            li.member.missing, li.type.missing { list-style-image: url(data:image/png;base64,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); }
            table { border-collapse: collapse; }
            table.layout { border: hidden; width: 100%; margin: 0; }
            table.layout td { border: 1px solid black; vertical-align: top; padding: 0; }
            table.layout td.sidebar { padding: 0; }
            table.layout td.content { width: 100%; padding: 1em 1em 5em 1.5em; }
            textarea { width: 100%; height: 100%; }
            table.doclist td { border: 1px solid #ccc; padding: 1em 2em; background: #eee; }
            td p:first-child { margin-top: 0; }
            td p:last-child { margin-bottom: 0; }
            span.parameter, span.member { white-space: nowrap; }
            h1 span.parameter, h1 span.member { white-space: normal; }
            pre { background: #eee; border: 1px solid #ccc; padding: 1em 2em; }
        ";

        private class memberComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                var typeX = x[0];
                var typeY = y[0];
                if (typeX == 'M' && x.Contains(".#ctor")) typeX = 'C';
                if (typeY == 'M' && y.Contains(".#ctor")) typeY = 'C';

                if (typeX == typeY)
                    return x.CompareTo(y);

                foreach (var m in "CMEPF")
                    if (typeX == m) return -1;
                    else if (typeY == m) return 1;

                return 0;
            }
        }

        /// <summary>
        /// Initialises a <see cref="DocumentationGenerator"/> instance by searching the given path for XML and DLL files.
        /// All pairs of matching <c>*.dll</c> and <c>*.docs.xml</c> files are considered for documentation. The classes are extracted
        /// from the DLLs and grouped by namespaces.
        /// </summary>
        /// <param name="path">Path containing DLL and XML files.</param>
        public DocumentationGenerator(string path)
        {
            _tree = new SortedDictionary<string, SortedDictionary<string, Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>>>();
            _typeDocumentation = new SortedDictionary<string, Tuple<Type, XElement>>();
            _memberDocumentation = new SortedDictionary<string, Tuple<MemberInfo, XElement>>();

            foreach (var f in new DirectoryInfo(path).GetFiles("*.dll").Where(f => File.Exists(f.FullName.Remove(f.FullName.Length - 3) + "docs.xml")))
            {
                Assembly a = Assembly.LoadFile(f.FullName);
                XElement e = XElement.Load(f.FullName.Remove(f.FullName.Length - 3) + "docs.xml");
                foreach (var t in a.GetExportedTypes().Where(t => shouldTypeBeDisplayed(t)))
                {
                    XElement doc = e.Element("members").Elements("member").FirstOrDefault(n => n.Attribute("name").Value == "T:" + GetTypeFullName(t));
                    _typeDocumentation.Add(GetTypeFullName(t), new Tuple<Type, XElement>(t, doc));
                    if (!_tree.ContainsKey(t.Namespace))
                        _tree[t.Namespace] = new SortedDictionary<string, Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>>();

                    _tree[t.Namespace][GetTypeFullName(t)] = new Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>(t,
                        doc, new SortedDictionary<string, Tuple<MemberInfo, XElement>>(new memberComparer()));
                    foreach (var mem in t.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .Where(m => m.DeclaringType == t && shouldMemberBeDisplayed(m)))
                    {
                        var dcmn = documentationCompatibleMemberName(mem);
                        XElement mdoc = e.Element("members").Elements("member").FirstOrDefault(n => n.Attribute("name").Value == dcmn);
                        while (_memberDocumentation.ContainsKey(dcmn)) dcmn += "|";
                        _tree[t.Namespace][GetTypeFullName(t)].E3[dcmn] = new Tuple<MemberInfo, XElement>(mem, mdoc);
                        _memberDocumentation.Add(dcmn, new Tuple<MemberInfo, XElement>(mem, mdoc));
                    }
                }
            }
        }

        private string GetTypeFullName(Type t)
        {
            if (t.IsGenericType)
                return t.GetGenericTypeDefinition().FullName;

            return t.FullName;
        }

        /// <summary>
        /// Returns the <see cref="HttpRequestHandler"/> that handles HTTP requests for the documentation.
        /// Instantiate a <see cref="HttpRequestHandlerHook"/> with this and add it to an instance of
        /// <see cref="HttpServer"/> using <see cref="HttpServer.RequestHandlerHooks"/>.
        /// </summary>
        /// <returns>An <see cref="HttpRequestHandler"/> that can be hooked to an instance of <see cref="HttpServer"/></returns>
        public HttpRequestHandler GetRequestHandler()
        {
            return req =>
            {
                if (req.RestUrl == "")
                    return HttpServer.RedirectResponse(req.BaseUrl + "/");
                if (req.RestUrl == "/css")
                    return HttpServer.StringResponse(_css, "text/css; charset=utf-8");
                else
                {
                    string ns = null;
                    Type type = null;
                    MemberInfo member = null;

                    string token = req.RestUrl.Substring(1).UrlUnescape();
                    if (_tree.ContainsKey(token))
                        ns = token;
                    else if (_typeDocumentation.ContainsKey(token))
                    {
                        type = _typeDocumentation[token].E1;
                        ns = type.Namespace;
                    }
                    else if (_memberDocumentation.ContainsKey(token))
                    {
                        member = _memberDocumentation[token].E1;
                        type = member.DeclaringType;
                        ns = type.Namespace;
                    }

                    HttpStatusCode status = ns == null && req.RestUrl.Length > 1 ? HttpStatusCode._404_NotFound : HttpStatusCode._200_OK;

                    var html = new HTML(
                        new HEAD(
                            new TITLE(
                                member != null ? (
                                    member.MemberType == MemberTypes.Constructor ? "Constructor: " :
                                    member.MemberType == MemberTypes.Event ? "Event: " :
                                    member.MemberType == MemberTypes.Field && (member as FieldInfo).IsStatic ? "Static field: " :
                                    member.MemberType == MemberTypes.Field ? "Field: " :
                                    member.MemberType == MemberTypes.Method && (member as MethodInfo).IsStatic ? "Static method: " :
                                    member.MemberType == MemberTypes.Method ? "Method: " :
                                    member.MemberType == MemberTypes.Property ? "Property: " : "Member: "
                                ) : type != null ? (
                                    type.IsEnum ? "Enum: " : type.IsValueType ? "Struct: " : typeof(Delegate).IsAssignableFrom(type) ? "Delegate: " : "Class: "
                                ) : ns != null ? "Namespace: " : null,
                                member != null && member.MemberType == MemberTypes.Constructor ? (object) friendlyTypeName(type, false) :
                                    member != null ? member.Name : type != null ? (object) friendlyTypeName(type, false) : ns != null ? ns : null,
                                member != null || type != null || ns != null ? " – " : null,
                                "XML documentation"
                            ),
                            new LINK { href = req.BaseUrl + "/css", rel = "stylesheet", type = "text/css" }
                        ),
                        new BODY(
                            new TABLE { class_ = "layout" }._(
                                new TR(
                                    new TD { class_ = "sidebar" }._(
                                        new UL { class_ = "legend" }._(
                                            new LI("Constructor") { class_ = "Constructor" },
                                            new LI("Method") { class_ = "Method" },
                                            new LI("Property") { class_ = "Property" },
                                            new LI("Event") { class_ = "Event" },
                                            new LI("Field") { class_ = "Field" }
                                        ),
                                        new UL(_tree.Select(nkvp => new LI { class_ = "namespace" }._(new A(nkvp.Key) { href = req.BaseUrl + "/" + nkvp.Key.UrlEscape() },
                                            ns == null || ns != nkvp.Key ? (object) "" :
                                            new UL(nkvp.Value.Where(tkvp => !tkvp.Value.E1.IsNested).Select(tkvp => generateTypeBullet(tkvp, type, req)))
                                        )))
                                    ),
                                    new TD { class_ = "content" }._(
                                        member != null && _memberDocumentation.ContainsKey(token) ?
                                            (object) generateMemberDocumentation(_memberDocumentation[token].E1, _memberDocumentation[token].E2, req) :
                                        type != null && _typeDocumentation.ContainsKey(token) ?
                                            (object) generateTypeDocumentation(_typeDocumentation[token].E1, _typeDocumentation[token].E2, req) :
                                        ns != null && _tree.ContainsKey(ns) ?
                                            (object) generateNamespaceDocumentation(ns, _tree[ns], req) :
                                        req.RestUrl == "/"
                                            ? new DIV("Select an item from the list on the left.") { class_ = "warning" }
                                            : new DIV("No documentation available for this item.") { class_ = "warning" }
                                    )
                                )
                            )
                        )
                    );

                    return new HttpResponse
                    {
                        Status = status,
                        Headers = new HttpResponseHeaders { ContentType = "text/html; charset=utf-8" },
                        Content = new DynamicContentStream(html.ToEnumerable(), true)
                    };
                }
            };
        }

        private IEnumerable<object> friendlyTypeName(Type t, bool includeNamespaces)
        {
            return friendlyTypeName(t, includeNamespaces, null, false);
        }
        private IEnumerable<object> friendlyTypeName(Type t, bool includeNamespaces, string baseURL, bool inclRef)
        {
            if (t.IsByRef)
            {
                if (inclRef)
                    yield return "ref ";
                t = t.GetElementType();
            }

            if (t.IsArray)
            {
                yield return friendlyTypeName(t.GetElementType(), includeNamespaces);
                yield return "[]";
                yield break;
            }

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                yield return friendlyTypeName(t.GetGenericArguments()[0], includeNamespaces);
                yield return "?";
                yield break;
            }

            // Use the C# identifier for built-in types
            if (t == typeof(int)) yield return "int";
            else if (t == typeof(uint)) yield return "uint";
            else if (t == typeof(long)) yield return "long";
            else if (t == typeof(ulong)) yield return "ulong";
            else if (t == typeof(short)) yield return "short";
            else if (t == typeof(ushort)) yield return "ushort";
            else if (t == typeof(byte)) yield return "byte";
            else if (t == typeof(sbyte)) yield return "sbyte";
            else if (t == typeof(string)) yield return "string";
            else if (t == typeof(char)) yield return "char";
            else if (t == typeof(float)) yield return "float";
            else if (t == typeof(double)) yield return "double";
            else if (t == typeof(decimal)) yield return "decimal";
            else if (t == typeof(bool)) yield return "bool";
            else if (t == typeof(void)) yield return "void";
            else if (t == typeof(object)) yield return "object";
            else
            {
                if (includeNamespaces && !t.IsGenericParameter)
                {
                    yield return t.Namespace + ".";
                    var outerTypes = new List<object>();
                    Type outT = t;
                    while (outT.IsNested)
                    {
                        outerTypes.Insert(0, ".");
                        outerTypes.Insert(0, friendlyTypeName(outT.DeclaringType, false, baseURL, false));
                        outT = outT.DeclaringType;
                    }
                    yield return outerTypes;
                }

                string ret = t.IsGenericType ? t.Name.Remove(t.Name.IndexOf('`')) : t.Name.TrimEnd('&');
                if (baseURL != null && !t.IsGenericParameter && _typeDocumentation.ContainsKey(GetTypeFullName(t).TrimEnd('&')))
                    yield return new A(ret) { href = baseURL + "/" + GetTypeFullName(t).TrimEnd('&').UrlEscape() };
                else
                    yield return ret;

                if (t.IsGenericType)
                {
                    yield return "<";
                    bool first = true;
                    foreach (var ga in t.GetGenericArguments())
                    {
                        if (!first) yield return ", ";
                        first = false;
                        yield return friendlyTypeName(ga, includeNamespaces, baseURL, inclRef);
                    }
                    yield return ">";
                }
            }
        }

        private object friendlyMemberName(MemberInfo m, bool returnType, bool containingType, bool parameterTypes, bool parameterNames, bool namespaces)
        {
            return friendlyMemberName(m, returnType, containingType, parameterTypes, parameterNames, namespaces, false, null, null);
        }
        private object friendlyMemberName(MemberInfo m, bool returnType, bool containingType, bool parameterTypes, bool parameterNames, bool namespaces, bool indent)
        {
            return friendlyMemberName(m, returnType, containingType, parameterTypes, parameterNames, namespaces, indent, null, null);
        }
        private object friendlyMemberName(MemberInfo m, bool returnType, bool containingType, bool parameterTypes, bool parameterNames, bool namespaces, bool indent, string url, string baseUrl)
        {
            if (m.MemberType == MemberTypes.Constructor || m.MemberType == MemberTypes.Method)
                return new SPAN { class_ = m.MemberType.ToString() }._(
                    friendlyMethodName(m, returnType, containingType, parameterTypes, parameterNames, namespaces, indent, url, baseUrl, false)
                );

            return new SPAN { class_ = m.MemberType.ToString() }._(
                returnType && m.MemberType == MemberTypes.Property ? new object[] { friendlyTypeName(((PropertyInfo) m).PropertyType, namespaces, baseUrl, false), " " } :
                returnType && m.MemberType == MemberTypes.Event ? new object[] { friendlyTypeName(((EventInfo) m).EventHandlerType, namespaces, baseUrl, false), " " } :
                returnType && m.MemberType == MemberTypes.Field ? new object[] { friendlyTypeName(((FieldInfo) m).FieldType, namespaces, baseUrl, false), " " } : null,
                containingType ? friendlyTypeName(m.DeclaringType, namespaces, baseUrl, false) : null,
                containingType ? "." : null,
                m.MemberType == MemberTypes.Property
                    ? (object) friendlyPropertyName((PropertyInfo) m, parameterTypes, parameterNames, namespaces, indent, url, baseUrl)
                    : new STRONG(url == null ? (object) m.Name : new A(m.Name) { href = url })
            );
        }

        private IEnumerable<object> friendlyMethodName(MemberInfo m, bool returnType, bool containingType, bool parameterTypes, bool parameterNames, bool namespaces, bool indent, string url, string baseUrl, bool isDelegate)
        {
            MethodBase mi = m as MethodBase;
            if (isDelegate) yield return "delegate ";
            if (returnType && m.MemberType != MemberTypes.Constructor)
            {
                yield return friendlyTypeName(((MethodInfo) m).ReturnType, namespaces, baseUrl, false);
                yield return " ";
            }
            if ((m.MemberType == MemberTypes.Constructor || isDelegate) && url != null)
                yield return new STRONG(new A(friendlyTypeName(mi.DeclaringType, namespaces)) { href = url });
            else if (isDelegate)
                yield return new STRONG(friendlyTypeName(mi.DeclaringType, namespaces, baseUrl, false));
            else if (containingType || m.MemberType == MemberTypes.Constructor)
                yield return friendlyTypeName(mi.DeclaringType, namespaces, baseUrl, false);
            if (!indent) yield return new WBR();
            if (m.MemberType != MemberTypes.Constructor && !isDelegate)
            {
                if (containingType) yield return ".";
                yield return new STRONG(url == null ? (object) m.Name : new A(m.Name) { href = url });
            }
            if (mi.IsGenericMethod)
            {
                if (!indent) yield return new WBR();
                yield return "<" + mi.GetGenericArguments().Select(ga => ga.Name).JoinString(", ") + ">";
            }
            if (parameterTypes || parameterNames)
            {
                yield return indent && mi.GetParameters().Any() ? "(\n    " : "(";
                if (!indent) yield return new WBR();
                bool first = true;
                foreach (var p in mi.GetParameters())
                {
                    if (!first) yield return indent ? ",\n    " : ", ";
                    first = false;
                    yield return new SPAN { class_ = "parameter" }._(
                       parameterTypes && p.IsOut ? "out " : null,
                       parameterTypes ? friendlyTypeName(p.ParameterType, namespaces, baseUrl, !p.IsOut) : null,
                       parameterTypes && parameterNames ? " " : null,
                       parameterNames ? new STRONG(p.Name) : null
                   );
                }
                yield return indent && mi.GetParameters().Any() ? "\n)" : ")";
            }
        }

        private IEnumerable<object> friendlyPropertyName(PropertyInfo property, bool parameterTypes, bool parameterNames, bool namespaces, bool indent, string url, string baseUrl)
        {
            var prms = property.GetIndexParameters();
            if (prms.Length > 0)
            {
                yield return new STRONG(url == null ? (object) "this" : new A("this") { href = url });
                yield return indent ? "[\n    " : "[";
                if (!indent) yield return new WBR();
                bool first = true;
                foreach (var p in prms)
                {
                    if (!first) yield return indent ? ",\n    " : ", ";
                    first = false;
                    yield return new SPAN { class_ = "parameter" }._(
                        parameterTypes && p.IsOut ? "out " : null,
                        parameterTypes ? friendlyTypeName(p.ParameterType, namespaces, baseUrl, !p.IsOut) : null,
                        parameterTypes && parameterNames ? " " : null,
                        parameterNames ? new STRONG(p.Name) : null
                    );
                }
                yield return indent ? "\n]" : "]";
            }
            else
                yield return new STRONG(url == null ? (object) property.Name : new A(property.Name) { href = url });
        }

        private bool shouldMemberBeDisplayed(MemberInfo m)
        {
            if (m.ReflectedType.IsEnum && m.Name == "value__")
                return false;
            if (m.MemberType == MemberTypes.Constructor)
                return !(m as ConstructorInfo).IsPrivate;
            if (m.MemberType == MemberTypes.Method)
                return !(m as MethodInfo).IsPrivate && !isPropertyGetterOrSetter(m) && !isEventAdderOrRemover(m);
            if (m.MemberType == MemberTypes.Event)
                return true;
            if (m.MemberType == MemberTypes.Field)
                return !(m as FieldInfo).IsPrivate && !isEventField(m);
            if (m.MemberType == MemberTypes.NestedType)
                return shouldTypeBeDisplayed((Type) m);

            return true;
        }

        private bool shouldTypeBeDisplayed(Type t)
        {
            return !t.IsNested || !t.IsNestedPrivate;
        }

        private string documentationCompatibleMemberName(MemberInfo m)
        {
            StringBuilder sb = new StringBuilder();
            if (m.MemberType == MemberTypes.Method || m.MemberType == MemberTypes.Constructor)
            {
                MethodBase mi = m as MethodBase;
                sb.Append("M:");
                sb.Append(mi.DeclaringType.FullName);
                sb.Append(m.MemberType == MemberTypes.Method ? "." + mi.Name : ".#ctor");
                if (mi.IsGenericMethod)
                {
                    sb.Append("``");
                    sb.Append(mi.GetGenericArguments().Count());
                }
                bool first = true;
                foreach (var p in mi.GetParameters())
                {
                    sb.Append(first ? "(" : ",");
                    first = false;
                    sb.Append(stringifyParameterType(p.ParameterType, mi, m.ReflectedType));
                }
                if (!first) sb.Append(")");
            }
            else if (m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property || m.MemberType == MemberTypes.Event)
            {
                sb.Append(m.MemberType == MemberTypes.Field ? "F:" : m.MemberType == MemberTypes.Property ? "P:" : "E:");
                sb.Append(m.DeclaringType.FullName);
                sb.Append(".");
                sb.Append(m.Name);
            }
            else if (m.MemberType == MemberTypes.NestedType)
            {
                sb.Append(m.ToString());
            }
            else
            {
                sb.Append(m.ToString());
                sb.Append(" (Unknown member type: " + m.MemberType + ")");
            }
            return sb.ToString();
        }

        private string stringifyParameterType(Type parameterType, MethodBase method, Type type)
        {
            if (parameterType.IsByRef)
                return stringifyParameterType(parameterType.GetElementType(), method, type) + "@";

            if (parameterType.IsArray)
                return stringifyParameterType(parameterType.GetElementType(), method, type) + "[]";

            if (!parameterType.IsGenericType && !parameterType.IsGenericParameter)
                return parameterType.FullName;

            if (parameterType.IsGenericParameter)
            {
                int i = 0;
                if (method.IsGenericMethodDefinition)
                {
                    foreach (var p in method.GetGenericArguments())
                    {
                        if (p == parameterType)
                            return "``" + i;
                        i++;
                    }
                }
                if (type.IsGenericTypeDefinition)
                {
                    i = 0;
                    foreach (var p in type.GetGenericArguments())
                    {
                        if (p == parameterType)
                            return "`" + i;
                        i++;
                    }
                }
                throw new Exception("Parameter type is a generic type, but its generic argument is neither in the class nor method/constructor definition.");
            }

            if (parameterType.IsGenericType)
            {
                string fullName = parameterType.GetGenericTypeDefinition().FullName;
                fullName = fullName.Remove(fullName.LastIndexOf('`'));
                while (fullName.EndsWith("`")) fullName = fullName.Remove(fullName.Length - 1);
                return fullName + "{" + parameterType.GetGenericArguments().Select(ga => stringifyParameterType(ga, method, type)).JoinString(",") + "}";
            }

            throw new Exception("I totally don't know what to do with this parameter type.");
        }

        private bool isPropertyGetterOrSetter(MemberInfo member)
        {
            if (member.MemberType != MemberTypes.Method)
                return false;
            if (!member.Name.StartsWith("get_") && !member.Name.StartsWith("set_"))
                return false;
            string partName = member.Name.Substring(4);
            return member.ReflectedType.GetMembers().Any(m => m.MemberType == MemberTypes.Property && m.Name == partName);
        }

        private bool isEventAdderOrRemover(MemberInfo member)
        {
            if (member.MemberType != MemberTypes.Method)
                return false;
            if (!member.Name.StartsWith("add_") && !member.Name.StartsWith("remove_"))
                return false;
            string partName = member.Name.Substring(member.Name.StartsWith("add_") ? 4 : 7);
            return member.ReflectedType.GetMembers().Any(m => m.MemberType == MemberTypes.Event && m.Name == partName);
        }

        private bool isEventField(MemberInfo member)
        {
            if (member.MemberType != MemberTypes.Field)
                return false;
            return member.ReflectedType.GetMembers().Any(m => m.MemberType == MemberTypes.Event && m.Name == member.Name);
        }

        private object generateTypeBullet(KeyValuePair<string, Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>> tkvp, Type type, HttpRequest req)
        {
            string cssClass = "type";
            if (tkvp.Value.E2 == null) cssClass += " missing";
            return new LI { class_ = cssClass }._(new A(friendlyTypeName(tkvp.Value.E1, false)) { href = req.BaseUrl + "/" + tkvp.Key.UrlEscape() },
                type == null || !isNestedTypeOf(type, tkvp.Value.E1) || typeof(Delegate).IsAssignableFrom(tkvp.Value.E1) ? (object) null :
                new UL(tkvp.Value.E3.Select(mkvp =>
                {
                    string css = mkvp.Value.E1.MemberType.ToString() + " member";
                    if (mkvp.Value.E2 == null) css += " missing";
                    return mkvp.Value.E1.MemberType != MemberTypes.NestedType
                        ? new LI { class_ = css }._(new A(friendlyMemberName(mkvp.Value.E1, false, false, true, false, false)) { href = req.BaseUrl + "/" + mkvp.Key.UrlEscape() })
                        : generateTypeBullet(_tree[tkvp.Value.E1.Namespace].First(kvp => kvp.Key == ((Type) mkvp.Value.E1).FullName), type, req);
                }))
            );
        }

        private bool isNestedTypeOf(Type nestedType, Type containingType)
        {
            if (nestedType == containingType) return true;
            if (!nestedType.IsNested) return false;
            return isNestedTypeOf(nestedType.DeclaringType, containingType);
        }

        private IEnumerable<object> generateNamespaceDocumentation(string namespaceName, SortedDictionary<string, Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>> namespaceInfo, HttpRequest req)
        {
            yield return new H1("Namespace: ", namespaceName);

            foreach (var gr in namespaceInfo.GroupBy(kvp => kvp.Value.E1.IsEnum).OrderBy(gr => gr.Key))
            {
                yield return new H2(gr.Key ? "Enums in this namespace" : "Classes and structs in this namespace");
                yield return new TABLE { class_ = "doclist" }._(
                    gr.Select(kvp => new TR(
                        new TD(new A(friendlyTypeName(kvp.Value.E1, false)) { href = req.BaseUrl + "/" + kvp.Value.E1.FullName.UrlEscape() }),
                        new TD(kvp.Value.E2 == null || kvp.Value.E2.Element("summary") == null
                            ? (object) new EM("No documentation available.")
                            : interpretBlock(kvp.Value.E2.Element("summary").Nodes(), req))
                    ))
                );
            }
        }

        private IEnumerable<object> generateMemberDocumentation(MemberInfo member, XElement document, HttpRequest req)
        {
            yield return new H1(
                member.MemberType == MemberTypes.Constructor ? "Constructor: " :
                member.MemberType == MemberTypes.Event ? "Event: " :
                member.MemberType == MemberTypes.Field && (member as FieldInfo).IsStatic ? "Static field: " :
                member.MemberType == MemberTypes.Field ? "Field: " :
                member.MemberType == MemberTypes.Method && (member as MethodInfo).IsStatic ? "Static method: " :
                member.MemberType == MemberTypes.Method ? "Method: " :
                member.MemberType == MemberTypes.Property ? "Property: " : "Member: ",
                friendlyMemberName(member, true, false, true, false, false)
            );
            yield return new H2("Full definition");
            yield return new PRE(friendlyMemberName(member, true, true, true, true, true, true, null, req.BaseUrl));

            var summary = document == null ? null : document.Element("summary");
            if (summary != null)
            {
                yield return new H2("Summary");
                yield return interpretBlock(summary.Nodes(), req);
            }

            if ((member.MemberType == MemberTypes.Constructor || member.MemberType == MemberTypes.Method)
                    && (member as MethodBase).GetParameters().Any())
            {
                yield return new H2("Parameters");
                yield return new TABLE { class_ = "doclist" }._(
                    (member as MethodBase).GetParameters().Select(pi =>
                    {
                        var docElem = document == null ? null : document.Elements("param")
                            .Where(xe => xe.Attribute("name") != null && xe.Attribute("name").Value == pi.Name).FirstOrDefault();
                        return new TR(
                            new TD { class_ = "item" }._(
                                pi.IsOut ? "out " : null,
                                friendlyTypeName(pi.ParameterType, false, req.BaseUrl, !pi.IsOut),
                                " ",
                                new STRONG(pi.Name)
                            ),
                            new TD(docElem == null
                                ? (object) new EM("No documentation available.")
                                : interpretBlock(docElem.Nodes(), req))
                        );
                    })
                );
            }
        }

        private IEnumerable<object> generateTypeDocumentation(Type type, XElement document, HttpRequest req)
        {
            if (typeof(Delegate).IsAssignableFrom(type))
            {
                yield return generateDelegateDocumentation(type, document, req);
                yield break;
            }

            yield return new H1(
                type.IsEnum ? "Enum: " : type.IsValueType ? "Struct: " : "Class: ",
                friendlyTypeName(type, true)
            );
            if (document == null)
                yield return new DIV("No documentation available for this class.") { class_ = "warning" };
            else
            {
                var summary = document.Element("summary");
                if (summary != null)
                {
                    yield return new H2("Summary");
                    yield return interpretBlock(summary.Nodes(), req);
                }
            }

            foreach (var gr in _tree[type.Namespace][type.FullName].E3.GroupBy(kvp => new Tuple<MemberTypes, bool>(kvp.Value.E1.MemberType,
                kvp.Value.E1.MemberType == MemberTypes.Method && ((MethodInfo) kvp.Value.E1).IsStatic)))
            {
                yield return new H2(
                    gr.Key.E1 == MemberTypes.Constructor ? "Constructors" :
                    gr.Key.E1 == MemberTypes.Event ? "Events" :
                    gr.Key.E1 == MemberTypes.Field ? "Fields" :
                    gr.Key.E1 == MemberTypes.Method && gr.Key.E2 ? "Static methods" :
                    gr.Key.E1 == MemberTypes.Method ? "Non-static methods" :
                    gr.Key.E1 == MemberTypes.Property ? "Properties" : "Additional members"
                );
                yield return new TABLE { class_ = "doclist" }._(
                    gr.Select(kvp => new TR(
                        new TD { class_ = "item" }._(friendlyMemberName(kvp.Value.E1, true, false, true, true, false, false, req.BaseUrl + "/" + kvp.Key.UrlEscape(), req.BaseUrl)),
                        new TD(kvp.Value.E2 == null || kvp.Value.E2.Element("summary") == null
                            ? (object) new EM("No documentation available.")
                            : interpretBlock(kvp.Value.E2.Element("summary").Nodes(), req))
                    ))
                );
            }
        }

        private IEnumerable<object> generateDelegateDocumentation(Type type, XElement document, HttpRequest req)
        {
            yield return new H1("Delegate: ", friendlyTypeName(type, true));

            MethodInfo m = type.GetMethod("Invoke");
            yield return new H2("Full definition");
            yield return new PRE(friendlyMethodName(m, true, false, true, true, true, true, null, req.BaseUrl, true));
        }

        private IEnumerable<object> interpretBlock(IEnumerable<XNode> nodes, HttpRequest req)
        {
            var en = nodes.GetEnumerator();
            if (!en.MoveNext()) yield break;
            if (en.Current is XText)
            {
                yield return new P(interpretInline(new XElement("para", nodes), req));
                yield break;
            }

            foreach (var node in nodes)
            {
                var elem = node is XElement ? (XElement) node : new XElement("para", node);

                if (elem.Name == "para")
                    yield return new P(interpretInline(elem, req));
                else if (elem.Name == "list" && elem.Attribute("type") != null && elem.Attribute("type").Value == "bullet")
                    yield return new UL(new Func<IEnumerable<object>>(() =>
                    {
                        return elem.Elements("item").Select(e =>
                            e.Elements("term").Any()
                                ? (object) new LI(new STRONG(interpretInline(e.Element("term"), req)),
                                    e.Elements("description").Any() ? new BLOCKQUOTE(interpretInline(e.Element("description"), req)) : null)
                                : e.Elements("description").Any()
                                    ? (object) new LI(interpretInline(e.Element("description"), req))
                                    : null);
                    }));
                else if (elem.Name == "code")
                    yield return new PRE(elem.Value);
                else
                    yield return "Unknown element name: " + elem.Name;
            }
        }

        private IEnumerable<object> interpretInline(XElement elem, HttpRequest req)
        {
            foreach (var node in elem.Nodes())
            {
                if (node is XText)
                    yield return ((XText) node).Value;
                else
                {
                    var inElem = node as XElement;
                    if (inElem.Name == "see" && inElem.Attribute("cref") != null)
                    {
                        string token = inElem.Attribute("cref").Value;
                        if (token.StartsWith("T:") && _typeDocumentation.ContainsKey(token.Substring(2)))
                        {
                            token = token.Substring(2);
                            yield return new A(friendlyTypeName(_typeDocumentation[token].E1, false)) { href = req.BaseUrl + "/" + token.UrlEscape() };
                        }
                        else if (_memberDocumentation.ContainsKey(token))
                            yield return new A(friendlyMemberName(_memberDocumentation[token].E1, false, false, true, false, false)) { href = req.BaseUrl + "/" + token.UrlEscape() };
                        else
                            yield return new CODE(token);
                    }
                    else if (inElem.Name == "c")
                        yield return new CODE(interpretInline(inElem, req));
                    else if (inElem.Name == "paramref" && inElem.Attribute("name") != null)
                        yield return new SPAN(new STRONG(inElem.Attribute("name").Value)) { class_ = "parameter" };
                    else
                        yield return interpretInline(inElem, req);
                }
            }
        }
    }
}
