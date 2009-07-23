using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using RT.Util.ExtensionMethods;
using System.Linq;

namespace RT.TagSoup
{
    /// <summary>
    /// Abstract base class for an HTML or XHTML tag.
    /// </summary>
    public abstract class Tag
    {
        /// <summary>Remembers the contents of this tag.</summary>
        protected List<object> TagContents = null;

        /// <summary>Name of the tag.</summary>
        public abstract string TagName { get; }
        /// <summary>DOCTYPE that is output before the tag. Only used by the &lt;HTML&gt; HTML tag and the &lt;html&gt; XHTML tag.</summary>
        public virtual string DocType { get { return null; } }
        /// <summary>Whether the start tag should be printed. If the tag has attributes, it will be printed regardless.</summary>
        public virtual bool StartTag { get { return true; } }
        /// <summary>Whether the end tag should be printed.</summary>
        public virtual bool EndTag { get { return true; } }
        /// <summary>Whether XHTML-style &lt;/&gt; empty-tag markers are allowed.</summary>
        public abstract bool AllowXhtmlEmpty { get; }

        /// <summary>Sets the contents of the tag. Any objects are allowed.</summary>
        /// <param name="contents"></param>
        /// <returns></returns>
        /// <remarks>
        ///     <para>Special support exists for the following types:</para>
        ///     <list type="bullet">
        ///         <item><term><c>string</c></term><description>outputs that string (HTML-escaped, of course)</description></item>
        ///         <item><term><c>IEnumerable&lt;string&gt;</c></term><description>concatenates all contained strings</description></item>
        ///         <item><term><c>Func&lt;string&gt;</c></term><description>calls the function and outputs the returned string</description></item>
        ///         <item><term><c>Func&lt;IEnumerable&lt;string&gt;&gt;</c></term><description>calls the function and concatenates all strings returned</description></item>
        ///         <item><term><c>TagSoup</c></term><description>outputs that tag and its contents</description></item>
        ///         <item><term><c>IEnumerable&lt;TagSoup&gt;</c></term><description>concatenates all contained tags</description></item>
        ///         <item><term><c>Func&lt;TagSoup&gt;</c></term><description>calls the function and outputs the returned tag</description></item>
        ///         <item><term><c>Func&lt;IEnumerable&lt;TagSoup&gt;&gt;</c></term><description>calls the function and concatenates all tags returned</description></item>
        ///     </list>
        ///     <para>Using objects of type <c>Func&lt;...&gt;</c> is a convenient way to defer execution to ensure maximally responsive output.</para>
        /// </remarks>
        public Tag _(params object[] contents)
        {
            if (TagContents == null)
                TagContents = new List<object>(contents);
            else
                TagContents.AddRange(contents);
            return this;
        }

        /// <summary>Adds stuff at the end of the contents of this tag (a string, a tag, a collection of strings or of tags).</summary>
        /// <param name="content">The stuff to add.</param>
        public void Add(object content) { TagContents.Add(content); }

        /// <summary>Outputs this tag and all its contents.</summary>
        /// <returns>A collection of strings which, when concatenated, represent this tag and all its contents.</returns>
        public virtual IEnumerable<string> ToEnumerable()
        {
            if (DocType != null)
                yield return DocType;

            if (StartTag)
                yield return "<" + TagName;
            bool tagPrinted = StartTag;

            foreach (var field in this.GetType().GetFields())
            {
                if (field.Name.StartsWith("_"))
                    continue;
                object val = field.GetValue(this);
                if (val == null) continue;
                if (val is bool && !((bool) val))
                    continue;
                bool isEnum = field.FieldType.IsEnum;
                string valStr = val.ToString();
                if (isEnum && valStr == "_")
                    continue;

                if (!tagPrinted)
                {
                    yield return "<" + TagName;
                    tagPrinted = true;
                }

                if (isEnum)
                    yield return " " + fixFieldName(field.Name) + "=\"" + fixFieldName(valStr) + "\"";
                else if (val is bool)
                {
                    string s = fixFieldName(field.Name);
                    yield return " " + s + "=\"" + s + "\"";
                }
                else
                    yield return " " + fixFieldName(field.Name) + "=\"" + valStr.HtmlEscape() + "\"";
            }
            if (tagPrinted && AllowXhtmlEmpty && (TagContents == null || TagContents.Count == 0))
            {
                yield return "/>";
                yield break;
            }
            if (tagPrinted)
                yield return ">";
            Exception toThrow = null;
            foreach (object content in TagContents)
            {
                if (content == null)
                    continue;
                if (content is string)
                    yield return ((string) content).HtmlEscape();
                else
                {
                    IEnumerator<string> en = null;
                    try { en = stringify(content).GetEnumerator(); }
                    catch (Exception e) { toThrow = e; }
                    while (toThrow == null)
                    {
                        bool hasNext = false;
                        try { hasNext = en.MoveNext(); }
                        catch (Exception e) { toThrow = e; }
                        if (!hasNext)
                            break;
                        yield return en.Current;
                    }
                }
            }
            if (EndTag)
                yield return "</" + TagName + ">";
            if (toThrow != null)
                throw toThrow;
        }

        /// <summary>Converts the entire tag tree into a single string.</summary>
        /// <returns>The entire tag tree as a single string.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in ToEnumerable())
                sb.Append(s);
            return sb.ToString();
        }

        private IEnumerable<string> Empty = Enumerable.Empty<string>();

        private IEnumerable<string> stringify(object o)
        {
            if (o == null)
                return Empty;

            if (o is Tag)
                return ((Tag) o).ToEnumerable();

            if (o is IEnumerable)
                return ((IEnumerable) o).Cast<object>().SelectMany(s => stringify(s));

            if (o is string)
                return new[] { ((string) o).HtmlEscape() };

            Type t = o.GetType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Func<>))
                return stringify(t.GetMethod("Invoke").Invoke(o, new object[] { }));

            return new[] { o.ToString().HtmlEscape() };
        }

        /// <summary>Converts a C#-compatible field name into an HTML/XHTML-compatible one.</summary>
        /// <example>
        ///     <list type="bullet">
        ///         <item><c>class_</c> is converted to <c>"class"</c></item>
        ///         <item><c>accept_charset</c> is converted to <c>"accept-charset"</c></item>
        ///         <item><c>xmlLang</c> is converted to <c>"xml:lang"</c></item>
        ///         <item><c>_</c> would be converted to the empty string, but <see cref="ToEnumerable"/> already skips those.</item>
        ///     </list>
        /// </example>
        /// <param name="fn">Field name to convert.</param>
        /// <returns>Converted field name.</returns>
        private static string fixFieldName(string fn)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < fn.Length; i++)
                if (fn[i] >= 'A' && fn[i] <= 'Z')
                    sb.Append(":" + char.ToLowerInvariant(fn[i]));
                else if (fn[i] == '_' && i < fn.Length - 1)
                    sb.Append('-');
                else if (fn[i] != '_')
                    sb.Append(fn[i]);
            return sb.ToString();
        }
    }
}
