using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Servers.XHTMLTags
{
    public abstract class XHTMLTag
    {
        protected object[] TagContents = null;
        public abstract string TagName { get; }
        public virtual bool DocType { get { return false; } }
        public XHTMLTag(params object[] Contents) { TagContents = Contents; }
        public XHTMLTag _(params object[] Contents) { TagContents = Contents; return this; }
        public virtual IEnumerable<string> ToEnumerable()
        {
            if (DocType)
                yield return @"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01//EN"" ""http://www.w3.org/TR/html4/strict.dtd"">";
            yield return "<" + TagName;
            foreach (var Field in this.GetType().GetFields())
            {
                if (Field.Name.StartsWith("_"))
                    continue;
                object Val = Field.GetValue(this);
                if (Val == null) continue;
                if (Field.FieldType.IsEnum)
                {
                    if (Val.ToString() == "_")
                        continue;
                    yield return " " + FixFieldName(Field.Name) + "=\"" + FixFieldName(Val.ToString()) + "\"";
                }
                else if (Val is bool)
                {
                    if (!((bool) Val))
                        continue;
                    string s = FixFieldName(Field.Name);
                    yield return " " + s + "=\"" + s + "\"";
                }
                else
                    yield return " " + FixFieldName(Field.Name) + "=\"" + Val.ToString().HTMLEscape() + "\"";
            }
            if (TagContents == null || TagContents.Length == 0)
            {
                yield return "/>";
                yield break;
            }
            yield return ">";
            foreach (object Content in TagContents)
            {
                if (Content is XHTMLTag)
                    foreach (string s in ((XHTMLTag) Content).ToEnumerable())
                        yield return s;
                else if (Content is IEnumerable<string>)
                    foreach (string s in (IEnumerable<string>) Content)
                        yield return s;
                else if (Content is Func<string>)
                    yield return ((Func<string>) Content)();
                else if (Content is Func<IEnumerable<string>>)
                    foreach (string s in ((Func<IEnumerable<string>>) Content)())
                        yield return s;
                else if (Content is Func<XHTMLTag>)
                    foreach (string s in (((Func<XHTMLTag>) Content)()).ToEnumerable())
                        yield return s;
                else if (Content is Func<IEnumerable<XHTMLTag>>)
                    foreach (XHTMLTag t in ((Func<IEnumerable<XHTMLTag>>) Content)())
                        foreach (string s in ((XHTMLTag) t).ToEnumerable())
                            yield return s;
                else
                    yield return Content.ToString().HTMLEscape();
            }
            yield return "</" + TagName + ">";
        }

        private string FixFieldName(string fn)
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

    public class XHTMLTable : table
    {
        public XHTMLTable(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "table"; } }
        public string _AllClasses;
        public XHTMLTable(params object[][] Rows)
        {
            List<object> Contents = new List<object>();
            foreach (object[] Row in Rows)
                Contents.Add(_AllClasses == null ? new tr(Row) : new tr(Row) { class_ = _AllClasses });
            TagContents = Contents.ToArray();
        }
    }

    public enum align { _, left, center, right, justify, char_ }
    public enum btype { _, button, submit, reset }
    public enum dir { _, ltr, rtl }
    public enum frame { _, void_, above, below, hsides, lhs, rhs, vsides, box, border }
    public enum itype { _, text, password, checkbox, radio, submit, reset, file, hidden, image, button }
    public enum method { _, get, post }
    public enum rules { _, none, groups, rows, cols, all }
    public enum scope { _, row, col, rowgroup, colgroup }
    public enum shape { _, rect, circle, poly, default_ }
    public enum valign { _, top, middle, bottom, baseline }
    public enum valuetype { _, data, ref_, object_ }

    public class a : XHTMLTag
    {
        public a(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "a"; } }
        public string accesskey;
        public string charset;
        public string class_;
        public string coords;
        public dir dir;
        public string href;
        public string hreflang;
        public string id;
        public string lang;
        public string name;
        public string onblur;
        public string onclick;
        public string ondblclick;
        public string onfocus;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string rel;
        public string rev;
        public shape shape;
        public string style;
        public string tabindex;
        public string title;
        public string type;
        public string xmlLang;
    }
    public class abbr : XHTMLTag
    {
        public abbr(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "abbr"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class acronym : XHTMLTag
    {
        public acronym(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "acronym"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class address : XHTMLTag
    {
        public address(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "address"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class area : XHTMLTag
    {
        public area(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "area"; } }
        public string accesskey;
        public string alt;
        public string class_;
        public string coords;
        public dir dir;
        public string href;
        public string id;
        public string lang;
        public bool nohref;
        public string onblur;
        public string onclick;
        public string ondblclick;
        public string onfocus;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public shape shape;
        public string style;
        public string tabindex;
        public string title;
        public string xmlLang;
    }
    public class b : XHTMLTag
    {
        public b(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "b"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class base_ : XHTMLTag
    {
        public base_(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "base"; } }
        public string href;
        public string id;
    }
    public class bdo : XHTMLTag
    {
        public bdo(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "bdo"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class big : XHTMLTag
    {
        public big(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "big"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class blockquote : XHTMLTag
    {
        public blockquote(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "blockquote"; } }
        public string cite;
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class body : XHTMLTag
    {
        public body(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "body"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onload;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string onunload;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class br : XHTMLTag
    {
        public br(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "br"; } }
        public string class_;
        public string id;
        public string style;
        public string title;
    }
    public class button : XHTMLTag
    {
        public button(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "button"; } }
        public string accesskey;
        public string class_;
        public dir dir;
        public bool disabled;
        public string id;
        public string lang;
        public string name;
        public string onblur;
        public string onclick;
        public string ondblclick;
        public string onfocus;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string tabindex;
        public string title;
        public btype type;
        public string value;
        public string xmlLang;
    }
    public class caption : XHTMLTag
    {
        public caption(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "caption"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class cite : XHTMLTag
    {
        public cite(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "cite"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class code : XHTMLTag
    {
        public code(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "code"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class col : XHTMLTag
    {
        public col(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "col"; } }
        public align align;
        public string char_;
        public string charoff;
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string span;
        public string style;
        public string title;
        public valign valign;
        public string width;
        public string xmlLang;
    }
    public class colgroup : XHTMLTag
    {
        public colgroup(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "colgroup"; } }
        public align align;
        public string char_;
        public string charoff;
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string span;
        public string style;
        public string title;
        public valign valign;
        public string width;
        public string xmlLang;
    }
    public class dd : XHTMLTag
    {
        public dd(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "dd"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class del : XHTMLTag
    {
        public del(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "del"; } }
        public string cite;
        public string class_;
        public string datetime;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class dfn : XHTMLTag
    {
        public dfn(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "dfn"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class div : XHTMLTag
    {
        public div(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "div"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class dl : XHTMLTag
    {
        public dl(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "dl"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class dt : XHTMLTag
    {
        public dt(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "dt"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class em : XHTMLTag
    {
        public em(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "em"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class fieldset : XHTMLTag
    {
        public fieldset(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "fieldset"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class form : XHTMLTag
    {
        public form(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "form"; } }
        public string accept;
        public string accept_charset;
        public string action;
        public string class_;
        public dir dir;
        public string enctype;
        public string id;
        public string lang;
        public method method;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string onreset;
        public string onsubmit;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class h1 : XHTMLTag
    {
        public h1(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "h1"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class h2 : XHTMLTag
    {
        public h2(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "h2"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class h3 : XHTMLTag
    {
        public h3(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "h3"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class h4 : XHTMLTag
    {
        public h4(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "h4"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class h5 : XHTMLTag
    {
        public h5(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "h5"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class h6 : XHTMLTag
    {
        public h6(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "h6"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class head : XHTMLTag
    {
        public head(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "head"; } }
        public dir dir;
        public string id;
        public string lang;
        public string profile;
        public string xmlLang;
    }
    public class hr : XHTMLTag
    {
        public hr(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "hr"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class html : XHTMLTag
    {
        public html(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "html"; } }
        public override bool DocType { get { return true; } }
        public dir dir;
        public string id;
        public string lang;
        public string xmlLang;
        public string xmlns = "http://www.w3.org/1999/xhtml";
    }
    public class i : XHTMLTag
    {
        public i(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "i"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class img : XHTMLTag
    {
        public img(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "img"; } }
        public string alt;
        public string class_;
        public dir dir;
        public string height;
        public string id;
        public bool ismap;
        public string lang;
        public string longdesc;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string src;
        public string style;
        public string title;
        public string usemap;
        public string width;
        public string xmlLang;
    }
    public class input : XHTMLTag
    {
        public input(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "input"; } }
        public string accept;
        public string accesskey;
        public string alt;
        public bool checked_;
        public string class_;
        public dir dir;
        public bool disabled;
        public string file;
        public string id;
        public string lang;
        public string maxlength;
        public string name;
        public string onblur;
        public string onchange;
        public string onclick;
        public string ondblclick;
        public string onfocus;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string onselect;
        public string radio;
        public bool readonly_;
        public int size;
        public string src;
        public string style;
        public string tabindex;
        public string title;
        public itype type;
        public string usemap;
        public string value;
        public string xmlLang;
    }
    public class ins : XHTMLTag
    {
        public ins(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "ins"; } }
        public string cite;
        public string class_;
        public string datetime;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class kbd : XHTMLTag
    {
        public kbd(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "kbd"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class label : XHTMLTag
    {
        public label(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "label"; } }
        public string accesskey;
        public string class_;
        public dir dir;
        public string for_;
        public string id;
        public string lang;
        public string onblur;
        public string onclick;
        public string ondblclick;
        public string onfocus;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class legend : XHTMLTag
    {
        public legend(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "legend"; } }
        public string accesskey;
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class li : XHTMLTag
    {
        public li(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "li"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class link : XHTMLTag
    {
        public link(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "link"; } }
        public string charset;
        public string class_;
        public dir dir;
        public string href;
        public string hreflang;
        public string id;
        public string lang;
        public string media;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string rel;
        public string rev;
        public string style;
        public string title;
        public string type;
        public string xmlLang;
    }
    public class map : XHTMLTag
    {
        public map(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "map"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string name;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class meta : XHTMLTag
    {
        public meta(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "meta"; } }
        public string content;
        public dir dir;
        public string http_equiv;
        public string id;
        public string lang;
        public string name;
        public string scheme;
        public string xmlLang;
    }
    public class noscript : XHTMLTag
    {
        public noscript(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "noscript"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class object_ : XHTMLTag
    {
        public object_(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "object"; } }
        public string archive;
        public string class_;
        public string classid;
        public string codebase;
        public string codetype;
        public string data;
        public bool declare;
        public dir dir;
        public string height;
        public string id;
        public string lang;
        public string name;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string standby;
        public string style;
        public string tabindex;
        public string title;
        public string type;
        public string usemap;
        public string width;
        public string xmlLang;
    }
    public class ol : XHTMLTag
    {
        public ol(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "ol"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class optgroup : XHTMLTag
    {
        public optgroup(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "optgroup"; } }
        public string class_;
        public dir dir;
        public bool disabled;
        public string id;
        public string label;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class option : XHTMLTag
    {
        public option(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "option"; } }
        public string class_;
        public dir dir;
        public bool disabled;
        public string id;
        public string label;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public bool selected;
        public string style;
        public string title;
        public string value;
        public string xmlLang;
    }
    public class p : XHTMLTag
    {
        public p(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "p"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class param : XHTMLTag
    {
        public param(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "param"; } }
        public string id;
        public string name;
        public string type;
        public string value;
        public valuetype valuetype;
    }
    public class pre : XHTMLTag
    {
        public pre(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "pre"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class q : XHTMLTag
    {
        public q(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "q"; } }
        public string cite;
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class samp : XHTMLTag
    {
        public samp(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "samp"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class script : XHTMLTag
    {
        public script(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "script"; } }
        public string charset;
        public bool defer;
        public string id;
        public string src;
        public string type;
    }
    public class select : XHTMLTag
    {
        public select(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "select"; } }
        public string class_;
        public dir dir;
        public bool disabled;
        public string id;
        public string lang;
        public bool multiple;
        public string name;
        public string onblur;
        public string onchange;
        public string onclick;
        public string ondblclick;
        public string onfocus;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string size;
        public string style;
        public string tabindex;
        public string title;
        public string xmlLang;
    }
    public class small : XHTMLTag
    {
        public small(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "small"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class span : XHTMLTag
    {
        public span(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "span"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class strong : XHTMLTag
    {
        public strong(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "strong"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class style : XHTMLTag
    {
        public style(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "style"; } }
        public dir dir;
        public string id;
        public string lang;
        public string media;
        public string title;
        public string type;
        public string xmlLang;
    }
    public class styleImport : XHTMLTag
    {
        public styleImport(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "style"; } }
        public string ImportFrom;
        public string media;
        public override IEnumerable<string> ToEnumerable()
        {
            yield return @"<style type=""text/css"" media=""" + media.HTMLEscape() + @""">/*<![CDATA[*/ @import """ + ImportFrom + @"""; /*]]>*/</style>";
        }
    }
    public class sub : XHTMLTag
    {
        public sub(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "sub"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class sup : XHTMLTag
    {
        public sup(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "sup"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class table : XHTMLTag
    {
        public table(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "table"; } }
        public string border;
        public string cellpadding;
        public string cellspacing;
        public string class_;
        public dir dir;
        public frame frame;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public rules rules;
        public string style;
        public string summary;
        public string title;
        public string width;
        public string xmlLang;
    }
    public class tbody : XHTMLTag
    {
        public tbody(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "tbody"; } }
        public align align;
        public string char_;
        public string charoff;
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public valign valign;
        public string xmlLang;
    }
    public class td : XHTMLTag
    {
        public td(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "td"; } }
        public string abbr;
        public align align;
        public string axis;
        public string char_;
        public string charoff;
        public string class_;
        public string colspan;
        public dir dir;
        public string headers;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string rowspan;
        public scope scope;
        public string style;
        public string title;
        public valign valign;
        public string xmlLang;
    }
    public class textarea : XHTMLTag
    {
        public textarea(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "textarea"; } }
        public string accesskey;
        public string class_;
        public string cols;
        public dir dir;
        public bool disabled;
        public string id;
        public string lang;
        public string name;
        public string onblur;
        public string onchange;
        public string onclick;
        public string ondblclick;
        public string onfocus;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string onselect;
        public bool readonly_;
        public string rows;
        public string style;
        public string tabindex;
        public string title;
        public string xmlLang;
    }
    public class tfoot : XHTMLTag
    {
        public tfoot(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "tfoot"; } }
        public align align;
        public string char_;
        public string charoff;
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public valign valign;
        public string xmlLang;
    }
    public class th : XHTMLTag
    {
        public th(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "th"; } }
        public string abbr;
        public align align;
        public string axis;
        public string char_;
        public string charoff;
        public string class_;
        public string colspan;
        public dir dir;
        public string headers;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string rowspan;
        public scope scope;
        public string style;
        public string title;
        public valign valign;
        public string xmlLang;
    }
    public class thead : XHTMLTag
    {
        public thead(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "thead"; } }
        public align align;
        public string char_;
        public string charoff;
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public valign valign;
        public string xmlLang;
    }
    public class title : XHTMLTag
    {
        public title(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "title"; } }
        public dir dir;
        public string id;
        public string lang;
        public string xmlLang;
    }
    public class tr : XHTMLTag
    {
        public tr(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "tr"; } }
        public align align;
        public string char_;
        public string charoff;
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public valign valign;
        public string xmlLang;
    }
    public class tt : XHTMLTag
    {
        public tt(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "tt"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class ul : XHTMLTag
    {
        public ul(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "ul"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
    public class var_ : XHTMLTag
    {
        public var_(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "var"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string onclick;
        public string ondblclick;
        public string onkeydown;
        public string onkeypress;
        public string onkeyup;
        public string onmousedown;
        public string onmousemove;
        public string onmouseout;
        public string onmouseover;
        public string onmouseup;
        public string style;
        public string title;
        public string xmlLang;
    }
}
