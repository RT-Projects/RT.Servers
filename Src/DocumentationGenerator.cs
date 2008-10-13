using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using RT.Util;
using System.Xml.Linq;
using RT.Util.ExtensionMethods;
using RT.Servers;
using RT.Util.Streams;
using RT.TagSoup.HTMLTags;
using RT.TagSoup;

namespace RT.Servers
{
    /// <summary>
    /// Provides an <see cref="HTTPRequestHandler"/> that generates web pages from C# XML documentation.
    /// </summary>
    public class DocumentationGenerator
    {
        private SortedDictionary<string, SortedDictionary<string, Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>>> Tree;
        private SortedDictionary<string, Tuple<Type, XElement>> TypeDocumentation;
        private SortedDictionary<string, Tuple<MemberInfo, XElement>> MemberDocumentation;

        private static string CSS = @"
            body { font-family: ""Verdana"", sans-serif; font-size: 9pt; margin: 0; }
            .namespace a { font-weight: bold; }
            .sidebar li.type { font-weight: bold; }
            .sidebar li.type > ul { font-weight: normal; }
            .sidebar li { padding-left: 1em; text-indent: -1em; }
            .sidebar .Constructor { background: #dfd; }
            .sidebar .Method { background: #ddf; }
            .sidebar .Property { background: #fdf; }
            .sidebar .Event { background: #fdd; }
            .sidebar .Field { background: #ffd; }
            .legend { white-space: nowrap; padding: 0.3em 0; }
            .legend span { padding: 0.3em 0.7em; font-weight: bold; }
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
            table.doclist { border: 1px solid #888; }
            table.doclist td { border: 1px solid #ccc; padding: 1em 2em; background: #eee; }
            td p:first-child { margin-top: 0; }
            td p:last-child { margin-bottom: 0; }
            span.parameter, span.member { white-space: nowrap; }
            h1 span.parameter, h1 span.member { white-space: normal; }
        ";

        private class MemberComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                var TypeX = x[0];
                var TypeY = y[0];
                if (TypeX == 'M' && x.Contains(".#ctor")) TypeX = 'C';
                if (TypeY == 'M' && y.Contains(".#ctor")) TypeY = 'C';

                if (TypeX == TypeY)
                    return x.CompareTo(y);

                foreach (var m in "CMEPF")
                    if (TypeX == m) return -1;
                    else if (TypeY == m) return 1;

                return 0;
            }
        }

        /// <summary>
        /// Initialises a <see cref="DocumentationGenerator"/> instance by searching the given path for XML and DLL files.
        /// All pairs of matching <c>*.dll</c> and <c>*.docs.xml</c> files are considered for documentation. The classes are extracted
        /// from the DLLs and grouped by namespaces.
        /// </summary>
        /// <param name="Path">Path containing DLL and XML files.</param>
        public DocumentationGenerator(string Path)
        {
            Tree = new SortedDictionary<string, SortedDictionary<string, Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>>>();
            TypeDocumentation = new SortedDictionary<string, Tuple<Type, XElement>>();
            MemberDocumentation = new SortedDictionary<string, Tuple<MemberInfo, XElement>>();

            foreach (var f in new DirectoryInfo(Path).GetFiles("*.dll").Where(f => File.Exists(f.FullName.Remove(f.FullName.Length - 3) + "docs.xml")))
            {
                Assembly a = Assembly.LoadFile(f.FullName);
                XElement e = XElement.Load(f.FullName.Remove(f.FullName.Length - 3) + "docs.xml");
                foreach (var t in a.GetExportedTypes().Where(t => ShouldTypeBeDisplayed(t)))
                {
                    XElement doc = e.Element("members").Elements("member").FirstOrDefault(n => n.Attribute("name").Value == "T:" + t.FullName);
                    TypeDocumentation.Add(t.FullName, new Tuple<Type, XElement>(t, doc));
                    if (!Tree.ContainsKey(t.Namespace))
                        Tree[t.Namespace] = new SortedDictionary<string, Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>>();

                    Tree[t.Namespace][t.FullName] = new Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>(t,
                        doc, new SortedDictionary<string, Tuple<MemberInfo, XElement>>(new MemberComparer()));
                    foreach (var mem in t.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .Where(m => m.DeclaringType == t && ShouldMemberBeDisplayed(m)))
                    {
                        var dcmn = DocumentationCompatibleMemberName(mem);
                        XElement mdoc = e.Element("members").Elements("member").FirstOrDefault(n => n.Attribute("name").Value == dcmn);
                        while (MemberDocumentation.ContainsKey(dcmn)) dcmn += "|";
                        Tree[t.Namespace][t.FullName].E3[dcmn] = new Tuple<MemberInfo, XElement>(mem, mdoc);
                        MemberDocumentation.Add(dcmn, new Tuple<MemberInfo, XElement>(mem, mdoc));
                    }
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="HTTPRequestHandler"/> to be hooked to an instance of <see cref="HTTPServer"/> using <see cref="HTTPServer.AddHandler"/>.
        /// </summary>
        /// <returns>An <see cref="HTTPRequestHandler"/> that can be hooked to an instance of <see cref="HTTPServer"/></returns>
        public HTTPRequestHandler GetRequestHandler()
        {
            return Req =>
            {
                if (Req.RestURL == "")
                    return HTTPServer.RedirectResponse(Req.BaseURL + "/");
                if (Req.RestURL == "/css")
                    return HTTPServer.StringResponse(CSS, "text/css; charset=utf-8");
                else
                {
                    return new HTTPResponse
                    {
                        Status = HTTPStatusCode._200_OK,
                        Headers = new HTTPResponseHeaders { ContentType = "text/html; charset=utf-8" },
                        Content = new DynamicContentStream(HandleRequest(Req), true)
                    };
                }
            };
        }

        private string FriendlyTypeName(Type t, bool IncludeNamespaces)
        {
            if (t.IsArray)
                return FriendlyTypeName(t.GetElementType(), IncludeNamespaces) + "[]";

            string Ret = (IncludeNamespaces && !t.IsGenericParameter ? t.Namespace + "." : "");

            if (IncludeNamespaces && !t.IsGenericParameter)
            {
                string Prefix = "";
                Type OutT = t;
                while (OutT.IsNested)
                {
                    Prefix = FriendlyTypeName(OutT.DeclaringType, false) + "." + Prefix;
                    OutT = OutT.DeclaringType;
                }
                Ret += Prefix;
            }

            Ret +=
                (t.IsGenericType
                    ? t.Name.Remove(t.Name.IndexOf('`')) + "<" +
                        string.Join(", ", t.GetGenericArguments().Select(s => FriendlyTypeName(s, false)).ToArray()) + ">"
                    : t.Name.TrimEnd('&'));

            if (t.Name.EndsWith("&"))
                return "out " + Ret;
            return Ret;
        }

        private object FriendlyMemberName(MemberInfo m, bool ReturnType, bool ContainingType, bool ParameterTypes, bool ParameterNames, bool Namespaces)
        {
            return FriendlyMemberName(m, ReturnType, ContainingType, ParameterTypes, ParameterNames, Namespaces, null);
        }
        private object FriendlyMemberName(MemberInfo m, bool ReturnType, bool ContainingType, bool ParameterTypes, bool ParameterNames, bool Namespaces, string URL)
        {
            if (m.MemberType == MemberTypes.Constructor || m.MemberType == MemberTypes.Method)
            {
                MethodBase mi = m as MethodBase;

                return new SPAN { class_ = "method" }._(
                    ReturnType && m.MemberType != MemberTypes.Constructor ? FriendlyTypeName(((MethodInfo) m).ReturnType, Namespaces) + " " : null,
                    m.MemberType == MemberTypes.Constructor && URL != null ? (object) new STRONG(new A(FriendlyTypeName(mi.DeclaringType, Namespaces)) { href = URL }) :
                        ContainingType || m.MemberType == MemberTypes.Constructor ? FriendlyTypeName(mi.DeclaringType, Namespaces) : null,
                    new WBR(),
                    m.MemberType != MemberTypes.Constructor && ContainingType ? "." : null,
                    m.MemberType != MemberTypes.Constructor ? new STRONG(URL == null ? (object) m.Name : new A(m.Name) { href = URL }) : null,
                    mi.IsGenericMethod ? new WBR() : null,
                    mi.IsGenericMethod ? "<" + string.Join(", ", mi.GetGenericArguments().Select(ga => ga.Name).ToArray()) + ">" : null,
                    new Func<IEnumerable<object>>(() =>
                    {
                        if (!ParameterTypes && !ParameterNames) return null;
                        List<object> Lst = new List<object>();
                        Lst.Add("(");
                        bool First = true;
                        foreach (var p in mi.GetParameters())
                        {
                            if (!First) Lst.Add(", ");
                            First = false;
                            Lst.Add(new SPAN { class_ = "parameter" }._(
                                ParameterTypes ? FriendlyTypeName(p.ParameterType, Namespaces) : null,
                                ParameterTypes && ParameterNames ? " " : null,
                                ParameterNames ? new STRONG(p.Name) : null
                            ));
                        }
                        Lst.Add(")");
                        return Lst;
                    })
                );
            }

            return new SPAN { class_ = "member" }._(
                ReturnType && m.MemberType == MemberTypes.Property ? FriendlyTypeName(((PropertyInfo) m).PropertyType, Namespaces) + " " :
                ReturnType && m.MemberType == MemberTypes.Event ? FriendlyTypeName(((EventInfo) m).EventHandlerType, Namespaces) + " " :
                ReturnType && m.MemberType == MemberTypes.Field ? FriendlyTypeName(((FieldInfo) m).FieldType, Namespaces) + " " : null,
                ContainingType ? FriendlyTypeName(m.DeclaringType, Namespaces) : null,
                ContainingType ? "." : null,
                new STRONG(URL == null ? (object) m.Name : new A(m.Name) { href = URL })
            );
        }

        private bool ShouldMemberBeDisplayed(MemberInfo m)
        {
            if (m.ReflectedType.IsEnum && m.Name == "value__")
                return false;
            if (m.MemberType == MemberTypes.Constructor)
                return !(m as ConstructorInfo).IsPrivate;
            if (m.MemberType == MemberTypes.Method)
                return !(m as MethodInfo).IsPrivate && !IsPropertyGetterOrSetter(m) && !IsEventAdderOrRemover(m);
            if (m.MemberType == MemberTypes.Event)
                return true;
            if (m.MemberType == MemberTypes.Field)
                return !(m as FieldInfo).IsPrivate && !IsEventField(m);
            if (m.MemberType == MemberTypes.NestedType)
                return ShouldTypeBeDisplayed((Type) m);

            return true;
        }

        private bool ShouldTypeBeDisplayed(Type t)
        {
            return !t.IsNested || !t.IsNestedPrivate;
        }

        private string DocumentationCompatibleMemberName(MemberInfo m)
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
                bool First = true;
                foreach (var p in mi.GetParameters())
                {
                    sb.Append(First ? "(" : ",");
                    First = false;
                    sb.Append(StringifyParameterType(p.ParameterType, mi, m.ReflectedType));
                }
                if (!First) sb.Append(")");
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

        private string StringifyParameterType(Type ParameterType, MethodBase Method, Type Class)
        {
            if (ParameterType.IsArray)
                return StringifyParameterType(ParameterType.GetElementType(), Method, Class) + "[]";

            if (!ParameterType.IsGenericType && !ParameterType.IsGenericParameter)
                return ParameterType.FullName.Replace('&', '@');

            if (ParameterType.IsGenericParameter)
            {
                int i = 0;
                if (Method.IsGenericMethodDefinition)
                {
                    foreach (var p in Method.GetGenericArguments())
                    {
                        if (p == ParameterType)
                            return "``" + i;
                        i++;
                    }
                }
                if (Class.IsGenericTypeDefinition)
                {
                    i = 0;
                    foreach (var p in Class.GetGenericArguments())
                    {
                        if (p == ParameterType)
                            return "`" + i;
                        i++;
                    }
                }
                throw new Exception("Parameter type is a generic type, but its generic argument is neither in the class nor method/constructor definition.");
            }

            if (ParameterType.IsGenericType)
            {
                string FullName = ParameterType.GetGenericTypeDefinition().FullName;
                FullName = FullName.Remove(FullName.LastIndexOf('`'));
                while (FullName.EndsWith("`")) FullName = FullName.Remove(FullName.Length - 1);
                return FullName.Replace('&', '@') + "{" + string.Join(",",
                    ParameterType.GetGenericArguments()
                        .Select(ga => StringifyParameterType(ga, Method, Class))
                        .ToArray()) + "}";
            }

            throw new Exception("I totally don't know what to do with this parameter type.");
        }

        private bool IsPropertyGetterOrSetter(MemberInfo Member)
        {
            if (Member.MemberType != MemberTypes.Method)
                return false;
            if (!Member.Name.StartsWith("get_") && !Member.Name.StartsWith("set_"))
                return false;
            string PartName = Member.Name.Substring(4);
            return Member.ReflectedType.GetMembers().Any(m => m.MemberType == MemberTypes.Property && m.Name == PartName);
        }

        private bool IsEventAdderOrRemover(MemberInfo Member)
        {
            if (Member.MemberType != MemberTypes.Method)
                return false;
            if (!Member.Name.StartsWith("add_") && !Member.Name.StartsWith("remove_"))
                return false;
            string PartName = Member.Name.Substring(Member.Name.StartsWith("add_") ? 4 : 7);
            return Member.ReflectedType.GetMembers().Any(m => m.MemberType == MemberTypes.Event && m.Name == PartName);
        }

        private bool IsEventField(MemberInfo Member)
        {
            if (Member.MemberType != MemberTypes.Field)
                return false;
            return Member.ReflectedType.GetMembers().Any(m => m.MemberType == MemberTypes.Event && m.Name == Member.Name);
        }

        private IEnumerable<string> HandleRequest(HTTPRequest Req)
        {
            string Namespace = null;
            Type Class = null;
            MemberInfo Member = null;

            string Token = Req.RestURL.Substring(1).URLUnescape();
            if (Tree.ContainsKey(Token))
                Namespace = Token;
            else if (TypeDocumentation.ContainsKey(Token))
            {
                Class = TypeDocumentation[Token].E1;
                Namespace = Class.Namespace;
            }
            else if (MemberDocumentation.ContainsKey(Token))
            {
                Member = MemberDocumentation[Token].E1;
                Class = Member.DeclaringType;
                Namespace = Class.Namespace;
            }

            var ret = new HTML(
                new HEAD(
                    new TITLE("XML documentation"),
                    new LINK { href = Req.BaseURL + "/css", rel = "stylesheet", type = "text/css" }
                ),
                new BODY(
                    new TABLE { class_ = "layout" }._(
                        new TR(
                            new TD { class_ = "sidebar" }._(
                                new DIV { class_ = "legend" }._(
                                    new SPAN("Constructor") { class_ = "Constructor" },
                                    new SPAN("Method") { class_ = "Method" },
                                    new SPAN("Property") { class_ = "Property" },
                                    new SPAN("Event") { class_ = "Event" },
                                    new SPAN("Field") { class_ = "Field" }
                                ),
                                new UL(Tree.Select(nkvp => new LI { class_ = "namespace" }._(new A(nkvp.Key) { href = Req.BaseURL + "/" + nkvp.Key.URLEscape() },
                                    Namespace == null || Namespace != nkvp.Key ? (object) "" :
                                    new UL(nkvp.Value.Where(tkvp => !tkvp.Value.E1.IsNested).Select(tkvp => GenerateTypeBullet(tkvp, Class, Req)))
                                )))
                            ),
                            new TD { class_ = "content" }._(
                                Member != null && MemberDocumentation.ContainsKey(Token) ?
                                    (object) GenerateMemberDocumentation(MemberDocumentation[Token].E1, MemberDocumentation[Token].E2, Req) :
                                Class != null && TypeDocumentation.ContainsKey(Token) ?
                                    (object) GenerateTypeDocumentation(TypeDocumentation[Token].E1, TypeDocumentation[Token].E2, Req) :
                                Namespace != null && Tree.ContainsKey(Namespace) ?
                                    (object) GenerateNamespaceDocumentation(Namespace, Tree[Namespace], Req) :
                                new DIV("No documentation available for this item.") { class_ = "warning" }
                            )
                        )
                    )
                )
            ).ToEnumerable();

            return ret;
        }

        private object GenerateTypeBullet(KeyValuePair<string, Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>> tkvp, Type Class, HTTPRequest Req)
        {
            string cssclass = "type";
            if (tkvp.Value.E2 == null) cssclass += " missing";
            return new LI { class_ = cssclass }._(new A(FriendlyTypeName(tkvp.Value.E1, false)) { href = Req.BaseURL + "/" + tkvp.Key.URLEscape() },
                Class == null || !IsNestedTypeOf(Class, tkvp.Value.E1) ? (object) "" :
                new UL(tkvp.Value.E3.Select(mkvp =>
                {
                    string css = mkvp.Value.E1.MemberType.ToString() + " member";
                    if (mkvp.Value.E2 == null) css += " missing";
                    return mkvp.Value.E1.MemberType != MemberTypes.NestedType
                        ? new LI { class_ = css }._(new A(FriendlyMemberName(mkvp.Value.E1, false, false, true, false, false)) { href = Req.BaseURL + "/" + mkvp.Key.URLEscape() })
                        : GenerateTypeBullet(Tree[tkvp.Value.E1.Namespace].First(kvp => kvp.Key == ((Type) mkvp.Value.E1).FullName), Class, Req);
                }))
            );
        }

        private bool IsNestedTypeOf(Type NestedType, Type ContainingType)
        {
            if (NestedType == ContainingType) return true;
            if (!NestedType.IsNested) return false;
            return IsNestedTypeOf(NestedType.DeclaringType, ContainingType);
        }

        private IEnumerable<object> GenerateNamespaceDocumentation(string NSName, SortedDictionary<string, Tuple<Type, XElement, SortedDictionary<string, Tuple<MemberInfo, XElement>>>> NamespaceInfo, HTTPRequest Req)
        {
            yield return new H1("Namespace: ", NSName);

            foreach (var gr in NamespaceInfo.GroupBy(kvp => kvp.Value.E1.IsEnum).OrderBy(gr => gr.Key))
            {
                yield return new H2(gr.Key ? "Enums in this namespace" : "Classes and structs in this namespace");
                yield return new TABLE { class_ = "doclist" }._(
                    gr.Select(kvp => new TR(
                        new TD(new A(FriendlyTypeName(kvp.Value.E1, false)) { href = Req.BaseURL + "/" + kvp.Value.E1.FullName.URLEscape() }),
                        new TD(kvp.Value.E2 == null || kvp.Value.E2.Element("summary") == null
                            ? (object) new EM("No documentation available.")
                            : InterpretBlock(kvp.Value.E2.Element("summary").Nodes(), Req))
                    ))
                );
            }
        }

        private IEnumerable<object> GenerateMemberDocumentation(MemberInfo Member, XElement Document, HTTPRequest Req)
        {
            yield return new H1(
                Member.MemberType == MemberTypes.Constructor ? "Constructor: " :
                Member.MemberType == MemberTypes.Event ? "Event: " :
                Member.MemberType == MemberTypes.Field ? "Field: " :
                Member.MemberType == MemberTypes.Method ? "Method: " :
                Member.MemberType == MemberTypes.Property ? "Property: " : "Member: ",
                FriendlyMemberName(Member, true, true, true, true, true)
            );
            if (Document == null)
            {
                yield return new DIV("No documentation available for this member.") { class_ = "warning" };
                yield break;
            }
            yield return GenerateSummary(Document, Req);
        }

        private IEnumerable<object> GenerateTypeDocumentation(Type Class, XElement Document, HTTPRequest Req)
        {
            yield return new H1("Class: ", FriendlyTypeName(Class, true));
            if (Document == null)
                yield return new DIV("No documentation available for this class.") { class_ = "warning" };
            else
                yield return GenerateSummary(Document, Req);

            foreach (var gr in Tree[Class.Namespace][Class.FullName].E3.GroupBy(kvp => new Tuple<MemberTypes, bool>(kvp.Value.E1.MemberType,
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
                        new TD { class_ = "item" }._(FriendlyMemberName(kvp.Value.E1, true, false, true, true, false, Req.BaseURL + "/" + kvp.Key.URLEscape())),
                        new TD(kvp.Value.E2 == null || kvp.Value.E2.Element("summary") == null
                            ? (object) new EM("No documentation available.")
                            : InterpretBlock(kvp.Value.E2.Element("summary").Nodes(), Req))
                    ))
                );
            }
        }

        private IEnumerable<object> GenerateSummary(XElement Document, HTTPRequest Req)
        {
            var Elem = Document.Element("summary");
            if (Elem == null) yield break;
            yield return new H2("Summary");
            yield return InterpretBlock(Elem.Nodes(), Req);
        }

        private IEnumerable<object> InterpretBlock(IEnumerable<XNode> Nodes, HTTPRequest Req)
        {
            var en = Nodes.GetEnumerator();
            if (!en.MoveNext()) yield break;
            if (en.Current is XText)
            {
                yield return new P(InterpretInline(new XElement("para", Nodes), Req));
                yield break;
            }

            foreach (var Node in Nodes)
            {
                var Elem = Node is XElement ? (XElement) Node : new XElement("para", Node);

                if (Elem.Name == "para")
                    yield return new P(InterpretInline(Elem, Req));
                else if (Elem.Name == "list" && Elem.Attribute("type") != null && Elem.Attribute("type").Value == "bullet")
                    yield return new UL(new Func<IEnumerable<object>>(() =>
                    {
                        return Elem.Elements("item").Select(elem =>
                            elem.Elements("term").Any()
                                ? (object) new LI(new STRONG(InterpretInline(elem.Element("term"), Req)),
                                    elem.Elements("description").Any() ? new BLOCKQUOTE(InterpretInline(elem.Element("description"), Req)) : null)
                                : elem.Elements("description").Any()
                                    ? (object) new LI(InterpretInline(elem.Element("description"), Req))
                                    : null);
                    }));
                else if (Elem.Name == "code")
                    yield return new PRE(Elem.Value);
                else
                    yield return "Unknown element name: " + Elem.Name;
            }
        }

        private IEnumerable<object> InterpretInline(XElement Elem, HTTPRequest Req)
        {
            foreach (var Node in Elem.Nodes())
            {
                if (Node is XText)
                    yield return ((XText) Node).Value;
                else
                {
                    var InElem = Node as XElement;
                    if (InElem.Name == "see" && InElem.Attribute("cref") != null)
                    {
                        string Token = InElem.Attribute("cref").Value;
                        if (Token.StartsWith("T:") && TypeDocumentation.ContainsKey(Token.Substring(2)))
                        {
                            Token = Token.Substring(2);
                            string Namespace = Token.Remove(Token.LastIndexOf('.'));
                            yield return new A(FriendlyTypeName(TypeDocumentation[Token].E1, false)) { href = Req.BaseURL + "/" + Token.URLEscape() };
                        }
                        else if (MemberDocumentation.ContainsKey(Token))
                        {
                            string RToken = Token.Substring(2);
                            if (RToken.Contains('(')) RToken = RToken.Remove(RToken.IndexOf('('));
                            string MemberName = RToken.Substring(RToken.LastIndexOf('.') + 1);
                            string ClassName = RToken.Remove(RToken.LastIndexOf('.'));
                            string Namespace = ClassName.Remove(ClassName.LastIndexOf('.'));
                            yield return new A(FriendlyMemberName(MemberDocumentation[Token].E1, false, false, true, false, false)) { href = Req.BaseURL + "/" + Token.URLEscape() };
                        }
                    }
                    else if (InElem.Name == "c")
                        yield return new CODE(InterpretInline(InElem, Req));
                    else if (InElem.Name == "paramref" && InElem.Attribute("name") != null)
                        yield return new SPAN(new STRONG(InElem.Attribute("name").Value)) { class_ = "parameter" };
                    else
                        yield return InterpretInline(InElem, Req);
                }
            }
        }
    }
}
