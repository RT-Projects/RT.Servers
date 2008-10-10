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
        private Dictionary<string, List<Type>> Types = new Dictionary<string, List<Type>>();
        private Dictionary<string, XElement> Documentation = new Dictionary<string, XElement>();

        private static string CSS = @"
            body { font-family: ""Verdana"", sans-serif; font-size: 9pt; margin: 0; }
            .namespace a { font-weight: bold; }
            .sidebar li.type { font-weight: bold; }
            .sidebar li.type > ul { font-weight: normal; }
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
            li.member, li.type { list-style-image: url(data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAwAAAAMCAYAAABWdVznAAAACXBIWXMAAAsTAAALEwEAmpwYAAAKT2lDQ1BQaG90b3Nob3AgSUNDIHByb2ZpbGUAAHjanVNnVFPpFj333vRCS4iAlEtvUhUIIFJCi4AUkSYqIQkQSoghodkVUcERRUUEG8igiAOOjoCMFVEsDIoK2AfkIaKOg6OIisr74Xuja9a89+bN/rXXPues852zzwfACAyWSDNRNYAMqUIeEeCDx8TG4eQuQIEKJHAAEAizZCFz/SMBAPh+PDwrIsAHvgABeNMLCADATZvAMByH/w/qQplcAYCEAcB0kThLCIAUAEB6jkKmAEBGAYCdmCZTAKAEAGDLY2LjAFAtAGAnf+bTAICd+Jl7AQBblCEVAaCRACATZYhEAGg7AKzPVopFAFgwABRmS8Q5ANgtADBJV2ZIALC3AMDOEAuyAAgMADBRiIUpAAR7AGDIIyN4AISZABRG8lc88SuuEOcqAAB4mbI8uSQ5RYFbCC1xB1dXLh4ozkkXKxQ2YQJhmkAuwnmZGTKBNA/g88wAAKCRFRHgg/P9eM4Ors7ONo62Dl8t6r8G/yJiYuP+5c+rcEAAAOF0ftH+LC+zGoA7BoBt/qIl7gRoXgugdfeLZrIPQLUAoOnaV/Nw+H48PEWhkLnZ2eXk5NhKxEJbYcpXff5nwl/AV/1s+X48/Pf14L7iJIEyXYFHBPjgwsz0TKUcz5IJhGLc5o9H/LcL//wd0yLESWK5WCoU41EScY5EmozzMqUiiUKSKcUl0v9k4t8s+wM+3zUAsGo+AXuRLahdYwP2SycQWHTA4vcAAPK7b8HUKAgDgGiD4c93/+8//UegJQCAZkmScQAAXkQkLlTKsz/HCAAARKCBKrBBG/TBGCzABhzBBdzBC/xgNoRCJMTCQhBCCmSAHHJgKayCQiiGzbAdKmAv1EAdNMBRaIaTcA4uwlW4Dj1wD/phCJ7BKLyBCQRByAgTYSHaiAFiilgjjggXmYX4IcFIBBKLJCDJiBRRIkuRNUgxUopUIFVIHfI9cgI5h1xGupE7yAAygvyGvEcxlIGyUT3UDLVDuag3GoRGogvQZHQxmo8WoJvQcrQaPYw2oefQq2gP2o8+Q8cwwOgYBzPEbDAuxsNCsTgsCZNjy7EirAyrxhqwVqwDu4n1Y8+xdwQSgUXACTYEd0IgYR5BSFhMWE7YSKggHCQ0EdoJNwkDhFHCJyKTqEu0JroR+cQYYjIxh1hILCPWEo8TLxB7iEPENyQSiUMyJ7mQAkmxpFTSEtJG0m5SI+ksqZs0SBojk8naZGuyBzmULCAryIXkneTD5DPkG+Qh8lsKnWJAcaT4U+IoUspqShnlEOU05QZlmDJBVaOaUt2ooVQRNY9aQq2htlKvUYeoEzR1mjnNgxZJS6WtopXTGmgXaPdpr+h0uhHdlR5Ol9BX0svpR+iX6AP0dwwNhhWDx4hnKBmbGAcYZxl3GK+YTKYZ04sZx1QwNzHrmOeZD5lvVVgqtip8FZHKCpVKlSaVGyovVKmqpqreqgtV81XLVI+pXlN9rkZVM1PjqQnUlqtVqp1Q61MbU2epO6iHqmeob1Q/pH5Z/YkGWcNMw09DpFGgsV/jvMYgC2MZs3gsIWsNq4Z1gTXEJrHN2Xx2KruY/R27iz2qqaE5QzNKM1ezUvOUZj8H45hx+Jx0TgnnKKeX836K3hTvKeIpG6Y0TLkxZVxrqpaXllirSKtRq0frvTau7aedpr1Fu1n7gQ5Bx0onXCdHZ4/OBZ3nU9lT3acKpxZNPTr1ri6qa6UbobtEd79up+6Ynr5egJ5Mb6feeb3n+hx9L/1U/W36p/VHDFgGswwkBtsMzhg8xTVxbzwdL8fb8VFDXcNAQ6VhlWGX4YSRudE8o9VGjUYPjGnGXOMk423GbcajJgYmISZLTepN7ppSTbmmKaY7TDtMx83MzaLN1pk1mz0x1zLnm+eb15vft2BaeFostqi2uGVJsuRaplnutrxuhVo5WaVYVVpds0atna0l1rutu6cRp7lOk06rntZnw7Dxtsm2qbcZsOXYBtuutm22fWFnYhdnt8Wuw+6TvZN9un2N/T0HDYfZDqsdWh1+c7RyFDpWOt6azpzuP33F9JbpL2dYzxDP2DPjthPLKcRpnVOb00dnF2e5c4PziIuJS4LLLpc+Lpsbxt3IveRKdPVxXeF60vWdm7Obwu2o26/uNu5p7ofcn8w0nymeWTNz0MPIQ+BR5dE/C5+VMGvfrH5PQ0+BZ7XnIy9jL5FXrdewt6V3qvdh7xc+9j5yn+M+4zw33jLeWV/MN8C3yLfLT8Nvnl+F30N/I/9k/3r/0QCngCUBZwOJgUGBWwL7+Hp8Ib+OPzrbZfay2e1BjKC5QRVBj4KtguXBrSFoyOyQrSH355jOkc5pDoVQfujW0Adh5mGLw34MJ4WHhVeGP45wiFga0TGXNXfR3ENz30T6RJZE3ptnMU85ry1KNSo+qi5qPNo3ujS6P8YuZlnM1VidWElsSxw5LiquNm5svt/87fOH4p3iC+N7F5gvyF1weaHOwvSFpxapLhIsOpZATIhOOJTwQRAqqBaMJfITdyWOCnnCHcJnIi/RNtGI2ENcKh5O8kgqTXqS7JG8NXkkxTOlLOW5hCepkLxMDUzdmzqeFpp2IG0yPTq9MYOSkZBxQqohTZO2Z+pn5mZ2y6xlhbL+xW6Lty8elQfJa7OQrAVZLQq2QqboVFoo1yoHsmdlV2a/zYnKOZarnivN7cyzytuQN5zvn//tEsIS4ZK2pYZLVy0dWOa9rGo5sjxxedsK4xUFK4ZWBqw8uIq2Km3VT6vtV5eufr0mek1rgV7ByoLBtQFr6wtVCuWFfevc1+1dT1gvWd+1YfqGnRs+FYmKrhTbF5cVf9go3HjlG4dvyr+Z3JS0qavEuWTPZtJm6ebeLZ5bDpaql+aXDm4N2dq0Dd9WtO319kXbL5fNKNu7g7ZDuaO/PLi8ZafJzs07P1SkVPRU+lQ27tLdtWHX+G7R7ht7vPY07NXbW7z3/T7JvttVAVVN1WbVZftJ+7P3P66Jqun4lvttXa1ObXHtxwPSA/0HIw6217nU1R3SPVRSj9Yr60cOxx++/p3vdy0NNg1VjZzG4iNwRHnk6fcJ3/ceDTradox7rOEH0x92HWcdL2pCmvKaRptTmvtbYlu6T8w+0dbq3nr8R9sfD5w0PFl5SvNUyWna6YLTk2fyz4ydlZ19fi753GDborZ752PO32oPb++6EHTh0kX/i+c7vDvOXPK4dPKy2+UTV7hXmq86X23qdOo8/pPTT8e7nLuarrlca7nuer21e2b36RueN87d9L158Rb/1tWeOT3dvfN6b/fF9/XfFt1+cif9zsu72Xcn7q28T7xf9EDtQdlD3YfVP1v+3Njv3H9qwHeg89HcR/cGhYPP/pH1jw9DBY+Zj8uGDYbrnjg+OTniP3L96fynQ89kzyaeF/6i/suuFxYvfvjV69fO0ZjRoZfyl5O/bXyl/erA6xmv28bCxh6+yXgzMV70VvvtwXfcdx3vo98PT+R8IH8o/2j5sfVT0Kf7kxmTk/8EA5jz/GMzLdsAAAAgY0hSTQAAeiUAAICDAAD5/wAAgOkAAHUwAADqYAAAOpgAABdvkl/FRgAAAIpJREFUeNqUktEJhDAQRF/EBmwhLXgt2IKteL/3d5ZwlmAt2oItpIT4M4HlCCtZCDuQeZtMSMg501I9QPgEz7MAX+lX9zDQmt/A6QGjMa9aeMCufmo6HrAAUXq2GzUgCij3vmrAD8jAIT0ACdiqz6qAtpeg6R8oJ0wKZ2urhStAUrjkTQcIrV/jHgDdVxx2rpoRcwAAAABJRU5ErkJggg==); }
            li.member.missing, li.type.missing { list-style-image: url(data:image/png;base64,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); }
            table { border-collapse: collapse; border: hidden; width: 100%; margin: 0; }
            td { border: 1px solid black; vertical-align: top; padding: 0; }
            td.sidebar { padding: 0; }
            td.content { width: 100%; padding: 1em 1em 0 1.5em; }
            textarea { width: 100%; height: 100%; }
        ";

        private class MemberSorter : IComparer<Tuple<MemberTypes, string>>
        {
            public int Compare(Tuple<MemberTypes, string> x, Tuple<MemberTypes, string> y)
            {
                if (x.E1 == y.E1)
                    return x.E2.CompareTo(y.E2);

                MemberTypes[] Order = new MemberTypes[] { MemberTypes.Constructor, MemberTypes.Method, MemberTypes.Event, MemberTypes.Property, MemberTypes.Field };
                foreach (var m in Order)
                    if (x.E1 == m) return -1;
                    else if (y.E1 == m) return 1;

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
            DirectoryInfo d = new DirectoryInfo(Path);
            foreach (var f in d.GetFiles("*.dll").Where(f => File.Exists(f.FullName.Remove(f.FullName.Length - 3) + "docs.xml")))
            {
                Assembly a = Assembly.LoadFile(f.FullName);
                foreach (var t in a.GetExportedTypes())
                    Types.AddSafe(t.Namespace, t);
                XElement e = XElement.Load(f.FullName.Remove(f.FullName.Length - 3) + "docs.xml");
                foreach (var se in e.Element("members").Elements())
                    Documentation[se.Attribute("name").Value] = se;
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

        private string FriendlyTypeName(Type t)
        {
            return t.IsGenericType
                ? t.Name.Remove(t.Name.IndexOf('`')) + "<" +
                    string.Join(", ", t.GetGenericArguments().Select(s => s.Name).ToArray()) + ">"
                : t.Name;
        }

        private string FriendlyMemberName(MemberInfo m)
        {
            if (m.MemberType == MemberTypes.Constructor || m.MemberType == MemberTypes.Method)
            {
                MethodBase mi = m as MethodBase;
                StringBuilder sb = new StringBuilder();
                sb.Append(m.MemberType == MemberTypes.Constructor ? "Constructor" : m.Name);
                sb.Append("(");
                bool First = true;
                foreach (var p in mi.GetParameters())
                {
                    if (!First) sb.Append(", "); else First = false;
                    sb.Append(p.Name);
                }
                sb.Append(")");
                return sb.ToString();
            }

            return m.Name;
        }

        private bool ShouldBeDisplayed(MemberInfo m)
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
                return false;

            return true;
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
            string Namespace = Req.RestURL.Substring(1);
            string ClassName = null;
            string MemberName = null;
            if (Namespace.Contains('/'))
            {
                ClassName = Namespace.Substring(Namespace.IndexOf('/') + 1);
                Namespace = Namespace.Remove(Namespace.IndexOf('/'));
            }
            if (ClassName != null && ClassName.Contains('/'))
            {
                MemberName = ClassName.Substring(ClassName.IndexOf('/') + 1);
                ClassName = ClassName.Remove(ClassName.IndexOf('/'));
            }
            Namespace = Namespace.URLUnescape();
            if (ClassName != null) ClassName = ClassName.URLUnescape();
            if (MemberName != null) MemberName = MemberName.URLUnescape();

            Type Class = null;
            MemberInfo Member = null;

            var ret = new HTML(
                new HEAD(
                    new TITLE("XML documentation"),
                    new LINK { href = Req.BaseURL + "/css", rel = "stylesheet", type = "text/css" }
                ),
                new BODY(
                    new TABLE { border = "0" }._(
                        new TR(
                            new TD { class_ = "sidebar" }._(
                                new DIV { class_ = "legend" }._(
                                    new SPAN("Constructor") { class_ = "Constructor" },
                                    new SPAN("Method") { class_ = "Method" },
                                    new SPAN("Property") { class_ = "Property" },
                                    new SPAN("Event") { class_ = "Event" },
                                    new SPAN("Field") { class_ = "Field" }
                                ),
                                new UL(Types.OrderBy(kvp => kvp.Key).Select(kvp => new LI { class_ = "namespace" }._(new A(kvp.Key) { href = Req.BaseURL + "/" + kvp.Key.URLEscape() },
                                    Namespace != kvp.Key ? (object) "" :
                                    new UL(kvp.Value.OrderBy(t => t.Name).Select(t =>
                                    {
                                        if (t.FullName == ClassName) Class = t;
                                        string cssclass = "type" + (Documentation.ContainsKey("T:" + t.FullName) ? "" : " missing");
                                        return new LI { class_ = cssclass }._(new A(FriendlyTypeName(t)) { href = Req.BaseURL + "/" + kvp.Key.URLEscape() + "/" + t.FullName.URLEscape() },
                                            ClassName != t.FullName ? (object) "" :
                                            new UL(t.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                                .Where(m => m.DeclaringType == t)
                                                .Where(m => ShouldBeDisplayed(m))
                                                .OrderBy(m => new Tuple<MemberTypes, string>(m.MemberType, m.Name), new MemberSorter())
                                                .Select(m =>
                                                {
                                                    string dcmn = DocumentationCompatibleMemberName(m);
                                                    if (dcmn == MemberName) Member = m;
                                                    string css = m.MemberType.ToString() + " member";
                                                    if (!Documentation.ContainsKey(dcmn)) css += " missing";
                                                    return new LI { class_ = css }._(
                                                        new A(FriendlyMemberName(m)) { href = Req.BaseURL + "/" + kvp.Key.URLEscape() + "/" + t.FullName.URLEscape() + "/" + dcmn.URLEscape() }
                                                    );
                                                })
                                            )
                                        );
                                    }))
                                )))
                            ),
                            new TD { class_ = "content" }._(new Func<object>(() =>
                            {
                                return Member != null ? (object) MemberDocumentation(Member, Documentation.ContainsKey(MemberName) ? Documentation[MemberName] : null, Req) :
                                    Class != null ? (object) ClassDocumentation(Class, Documentation.ContainsKey("T:" + ClassName) ? Documentation["T:" + ClassName] : null, Req) :
                                    new DIV("No documentation available for this item.") { class_ = "warning" };
                            }))
                        )
                    )
                )
            ).ToEnumerable();

            return ret;
        }

        private IEnumerable<object> MemberDocumentation(MemberInfo Member, XElement Document, HTTPRequest Req)
        {
            yield return new H1("Member: ", Member.Name);
            if (Document == null)
            {
                yield return new DIV("No documentation available for this member.") { class_ = "warning" };
                yield break;
            }
            yield return GenerateSummary(Document, Req);
        }

        private IEnumerable<object> ClassDocumentation(Type Class, XElement Document, HTTPRequest Req)
        {
            yield return new H1("Class: ", Class.FullName);
            if (Document == null)
            {
                yield return new DIV("No documentation available for this class.") { class_ = "warning" };
                yield break;
            }
            yield return GenerateSummary(Document, Req);
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
                    if (InElem.Name == "see" && InElem.Attribute("cref") != null && Documentation.ContainsKey(InElem.Attribute("cref").Value))
                    {
                        string Token = InElem.Attribute("cref").Value;
                        if (Token.StartsWith("T:"))
                        {
                            Token = Token.Substring(2);
                            string Namespace = Token.Remove(Token.LastIndexOf('.'));
                            yield return new A(Token) { href = Req.BaseURL + "/" + Namespace.URLEscape() + "/" + Token.URLEscape() };
                        }
                        else
                        {
                            string RToken = Token.Substring(2);
                            if (RToken.Contains('(')) RToken = RToken.Remove(RToken.IndexOf('('));
                            string MemberName = RToken.Substring(RToken.LastIndexOf('.') + 1);
                            string ClassName = RToken.Remove(RToken.LastIndexOf('.'));
                            string Namespace = ClassName.Remove(ClassName.LastIndexOf('.'));
                            yield return new A(MemberName) { href = Req.BaseURL + "/" + Namespace.URLEscape() + "/" + ClassName.URLEscape() + "/" + Token.URLEscape() };
                        }
                    }
                    else if (InElem.Name == "c")
                        yield return new CODE(InterpretInline(InElem, Req));
                }
            }
        }
    }
}
