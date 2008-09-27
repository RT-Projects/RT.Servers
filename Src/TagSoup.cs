using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RT.Util.ExtensionMethods;

namespace RT
{
    /// <summary>
    /// Abstract base class for an HTML or XHTML tag.
    /// </summary>
    public abstract class TagSoup
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
        public abstract bool AllowXHTMLEmpty { get; }

        /// <summary>Sets the contents of the tag. Any objects are allowed.</summary>
        /// <param name="Contents"></param>
        /// <returns></returns>
        /// <remarks>
        ///     <para>Special support exists for the following types:</para>
        ///     <list type="bullet">
        ///         <item><description><c>TagSoup</c> - outputs that tag and its contents</description></item>
        ///         <item><description><c>IEnumerable&lt;string&gt;</c> - concatenates all contained strings</description></item>
        ///         <item><description><c>Func&lt;string&gt;</c> - calls the function and outputs the returned string</description></item>
        ///         <item><description><c>Func&lt;IEnumerable&lt;string&gt;&gt;</c> - calls the function and concatenates all strings returned</description></item>
        ///         <item><description><c>Func&lt;TagSoup&gt;</c> - calls the function and outputs the returned tag</description></item>
        ///         <item><description><c>Func&lt;IEnumerable&lt;TagSoup&gt;&gt;</c> - calls the function and concatenates all tags returned</description></item>
        ///     </list>
        ///     <para>Using objects of type <c>Func&lt;...&gt;</c> is a convenient way to defer execution to ensure maximally responsive output.</para>
        /// </remarks>
        public TagSoup _(params object[] Contents)
        {
            if (TagContents == null)
                TagContents = new List<object>(Contents);
            else
                TagContents.AddRange(Contents);
            return this;
        }

        /// <summary>Adds stuff at the end of the contents of this tag (a string, a tag, a collection of strings or of tags).</summary>
        /// <param name="Content">The stuff to add.</param>
        public void Add(object Content) { TagContents.Add(Content); }

        /// <summary>Outputs this tag and all its contents.</summary>
        /// <returns>A collection of strings which, when concatenated, represent this tag and all its contents.</returns>
        public virtual IEnumerable<string> ToEnumerable()
        {
            if (DocType != null)
                yield return DocType;

            if (StartTag)
                yield return "<" + TagName;
            bool TagPrinted = StartTag;

            foreach (var Field in this.GetType().GetFields())
            {
                if (Field.Name.StartsWith("_"))
                    continue;
                object Val = Field.GetValue(this);
                if (Val == null) continue;
                if (Field.FieldType.IsEnum && Val.ToString() == "_")
                    continue;
                if (Val is bool && !((bool) Val))
                    continue;

                if (!TagPrinted)
                {
                    yield return "<" + TagName;
                    TagPrinted = true;
                }

                if (Field.FieldType.IsEnum)
                    yield return " " + FixFieldName(Field.Name) + "=\"" + FixFieldName(Val.ToString()) + "\"";
                else if (Val is bool)
                {
                    string s = FixFieldName(Field.Name);
                    yield return " " + s + "=\"" + s + "\"";
                }
                else
                    yield return " " + FixFieldName(Field.Name) + "=\"" + Val.ToString().HTMLEscape() + "\"";
            }
            if (TagPrinted && AllowXHTMLEmpty && (TagContents == null || TagContents.Count == 0))
            {
                yield return "/>";
                yield break;
            }
            if (TagPrinted)
                yield return ">";
            foreach (object Content in TagContents)
            {
                if (Content == null)
                    continue;
                if (Content is TagSoup)
                    foreach (string s in ((TagSoup) Content).ToEnumerable())
                        yield return s;
                else if (Content is IEnumerable<string>)
                    foreach (string s in (IEnumerable<string>) Content)
                        yield return s;
                else if (Content is Func<string>)
                    yield return ((Func<string>) Content)();
                else if (Content is Func<IEnumerable<string>>)
                    foreach (string s in ((Func<IEnumerable<string>>) Content)())
                        yield return s;
                else if (Content is Func<TagSoup>)
                    foreach (string s in (((Func<TagSoup>) Content)()).ToEnumerable())
                        yield return s;
                else if (Content is Func<IEnumerable<TagSoup>>)
                    foreach (TagSoup t in ((Func<IEnumerable<TagSoup>>) Content)())
                        foreach (string s in ((TagSoup) t).ToEnumerable())
                            yield return s;
                else
                    yield return Content.ToString().HTMLEscape();
            }
            if (EndTag)
                yield return "</" + TagName + ">";
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

        /// <summary>Converts a C#-compatible field name into an HTML/XHTML-compatible one.</summary>
        /// <example>
        ///     <list type="bullet">
        ///         <item><description><c>class_</c> is converted to <c>"class"</c></description></item>
        ///         <item><description><c>accept_charset</c> is converted to <c>"accept-charset"</c></description></item>
        ///         <item><description><c>xmlLang</c> is converted to <c>"xml:lang"</c></description></item>
        ///         <item><description><c>_</c> would be converted to the empty string, but <see cref="ToEnumerable"/>() already skips those.</description></item>
        ///     </list>
        /// </example>
        /// <param name="fn">Field name to convert.</param>
        /// <returns>Converted field name.</returns>
        private static string FixFieldName(string fn)
        {
            Match m;
            while ((m = Regex.Match(fn, @"^(.*)([A-Z])(.*)$")).Success)
                fn = m.Groups[1].Value + ":" + m.Groups[2].Value.ToLowerInvariant() + m.Groups[3].Value;
            if (fn.EndsWith("_"))
                fn = fn.Remove(fn.Length - 1);
            fn = fn.Replace('_', '-');
            return fn;
        }
    }
}
