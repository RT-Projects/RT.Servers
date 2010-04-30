using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RT.Util.ExtensionMethods;

namespace RT.TagSoup.XhtmlTags
{
    /// <summary>Abstract base class for XHTML tags.</summary>
    public abstract class XhtmlTag : Tag
    {
        /// <summary>Constructs an XHTML tag.</summary>
        /// <param name="contents">Contents of the tag.</param>
        public XhtmlTag(params object[] contents) { TagContents = new List<object>(contents); }
        /// <summary>Returns true.</summary>
        public override bool AllowXhtmlEmpty { get { return true; } }
    }

    /// <summary>Special class to help construct an XHTML <c>&lt;table&gt;</c> element
    /// without needing to instantiate all intermediate row and cell tags.</summary>
    public sealed class XhtmlTable : table
    {
        /// <summary>If set to a value other than null, causes all rows and cells within the generated table to have the specified CSS class.</summary>
        public string _AllClasses;

        /// <summary>Constructs an XHTML table in which all rows and cells have the same CSS class.</summary>
        /// <param name="classOnAllTags">Optional. If non-null, all rows and cells within the generated table have the specified CSS class.</param>
        /// <param name="rows">Rows (arrays of cell contents).</param>
        public XhtmlTable(string classOnAllTags, params object[][] rows)
        {
            if (classOnAllTags != null)
                class_ = classOnAllTags;
            List<object> rowTags = new List<object>();
            foreach (object[] row in rows)
            {
                List<object> cellTags = new List<object>();
                foreach (object cell in row)
                    cellTags.Add(classOnAllTags == null ? new td(cell) : new td(cell) { class_ = classOnAllTags });
                rowTags.Add(classOnAllTags == null ? new tr(cellTags.ToArray()) : new tr(cellTags.ToArray()) { class_ = classOnAllTags });
            }
            TagContents = rowTags;
        }
    }

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

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

    public sealed class a : XhtmlTag
    {
        public a(params object[] contents) : base(contents) { }
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
    public sealed class abbr : XhtmlTag
    {
        public abbr(params object[] contents) : base(contents) { }
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
    public sealed class acronym : XhtmlTag
    {
        public acronym(params object[] contents) : base(contents) { }
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
    public sealed class address : XhtmlTag
    {
        public address(params object[] contents) : base(contents) { }
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
    public sealed class area : XhtmlTag
    {
        public area(params object[] contents) : base(contents) { }
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
    public sealed class b : XhtmlTag
    {
        public b(params object[] contents) : base(contents) { }
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
    public sealed class base_ : XhtmlTag
    {
        public base_(params object[] contents) : base(contents) { }
        public override string TagName { get { return "base"; } }
        public string href;
        public string id;
    }
    public sealed class bdo : XhtmlTag
    {
        public bdo(params object[] contents) : base(contents) { }
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
    public sealed class big : XhtmlTag
    {
        public big(params object[] contents) : base(contents) { }
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
    public sealed class blockquote : XhtmlTag
    {
        public blockquote(params object[] contents) : base(contents) { }
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
    public sealed class body : XhtmlTag
    {
        public body(params object[] contents) : base(contents) { }
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
    public sealed class br : XhtmlTag
    {
        public br(params object[] contents) : base(contents) { }
        public override string TagName { get { return "br"; } }
        public string class_;
        public string id;
        public string style;
        public string title;
    }
    public sealed class button : XhtmlTag
    {
        public button(params object[] contents) : base(contents) { }
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
    public sealed class caption : XhtmlTag
    {
        public caption(params object[] contents) : base(contents) { }
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
    public sealed class cite : XhtmlTag
    {
        public cite(params object[] contents) : base(contents) { }
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
    public sealed class code : XhtmlTag
    {
        public code(params object[] contents) : base(contents) { }
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
    public sealed class col : XhtmlTag
    {
        public col(params object[] contents) : base(contents) { }
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
    public sealed class colgroup : XhtmlTag
    {
        public colgroup(params object[] contents) : base(contents) { }
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
    public sealed class dd : XhtmlTag
    {
        public dd(params object[] contents) : base(contents) { }
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
    public sealed class del : XhtmlTag
    {
        public del(params object[] contents) : base(contents) { }
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
    public sealed class dfn : XhtmlTag
    {
        public dfn(params object[] contents) : base(contents) { }
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
    public sealed class div : XhtmlTag
    {
        public div(params object[] contents) : base(contents) { }
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
    public sealed class dl : XhtmlTag
    {
        public dl(params object[] contents) : base(contents) { }
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
    public sealed class dt : XhtmlTag
    {
        public dt(params object[] contents) : base(contents) { }
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
    public sealed class em : XhtmlTag
    {
        public em(params object[] contents) : base(contents) { }
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
    public sealed class fieldset : XhtmlTag
    {
        public fieldset(params object[] contents) : base(contents) { }
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
    public sealed class form : XhtmlTag
    {
        public form(params object[] contents) : base(contents) { }
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
    public sealed class h1 : XhtmlTag
    {
        public h1(params object[] contents) : base(contents) { }
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
    public sealed class h2 : XhtmlTag
    {
        public h2(params object[] contents) : base(contents) { }
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
    public sealed class h3 : XhtmlTag
    {
        public h3(params object[] contents) : base(contents) { }
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
    public sealed class h4 : XhtmlTag
    {
        public h4(params object[] contents) : base(contents) { }
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
    public sealed class h5 : XhtmlTag
    {
        public h5(params object[] contents) : base(contents) { }
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
    public sealed class h6 : XhtmlTag
    {
        public h6(params object[] contents) : base(contents) { }
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
    public sealed class head : XhtmlTag
    {
        public head(params object[] contents) : base(contents) { }
        public override string TagName { get { return "head"; } }
        public dir dir;
        public string id;
        public string lang;
        public string profile;
        public string xmlLang;
    }
    public sealed class hr : XhtmlTag
    {
        public hr(params object[] contents) : base(contents) { }
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
    public sealed class html : XhtmlTag
    {
        public html(params object[] contents) : base(contents) { }
        public override string TagName { get { return "html"; } }
        public override string DocType { get { return @"<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Strict//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd"">"; } }
        public dir dir;
        public string id;
        public string lang;
        public string xmlLang;
        public string xmlns = "http://www.w3.org/1999/xhtml";
    }
    public sealed class i : XhtmlTag
    {
        public i(params object[] contents) : base(contents) { }
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
    public sealed class img : XhtmlTag
    {
        public img(params object[] contents) : base(contents) { }
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
    public sealed class input : XhtmlTag
    {
        public input(params object[] contents) : base(contents) { }
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
    public sealed class ins : XhtmlTag
    {
        public ins(params object[] contents) : base(contents) { }
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
    public sealed class kbd : XhtmlTag
    {
        public kbd(params object[] contents) : base(contents) { }
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
    public sealed class label : XhtmlTag
    {
        public label(params object[] contents) : base(contents) { }
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
    public sealed class legend : XhtmlTag
    {
        public legend(params object[] contents) : base(contents) { }
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
    public sealed class li : XhtmlTag
    {
        public li(params object[] contents) : base(contents) { }
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
    public sealed class link : XhtmlTag
    {
        public link(params object[] contents) : base(contents) { }
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
    public sealed class map : XhtmlTag
    {
        public map(params object[] contents) : base(contents) { }
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
    public sealed class meta : XhtmlTag
    {
        public meta(params object[] contents) : base(contents) { }
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
    public sealed class noscript : XhtmlTag
    {
        public noscript(params object[] contents) : base(contents) { }
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
    public sealed class object_ : XhtmlTag
    {
        public object_(params object[] contents) : base(contents) { }
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
    public sealed class ol : XhtmlTag
    {
        public ol(params object[] contents) : base(contents) { }
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
    public sealed class optgroup : XhtmlTag
    {
        public optgroup(params object[] contents) : base(contents) { }
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
    public sealed class option : XhtmlTag
    {
        public option(params object[] contents) : base(contents) { }
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
    public sealed class p : XhtmlTag
    {
        public p(params object[] contents) : base(contents) { }
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
    public sealed class param : XhtmlTag
    {
        public param(params object[] contents) : base(contents) { }
        public override string TagName { get { return "param"; } }
        public string id;
        public string name;
        public string type;
        public string value;
        public valuetype valuetype;
    }
    public sealed class pre : XhtmlTag
    {
        public pre(params object[] contents) : base(contents) { }
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
    public sealed class q : XhtmlTag
    {
        public q(params object[] contents) : base(contents) { }
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
    public sealed class samp : XhtmlTag
    {
        public samp(params object[] contents) : base(contents) { }
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
    public sealed class script : XhtmlTag
    {
        public script(params object[] contents) : base(contents) { }
        public override string TagName { get { return "script"; } }
        public string charset;
        public bool defer;
        public string id;
        public string src;
        public string type;
    }
    public sealed class select : XhtmlTag
    {
        public select(params object[] contents) : base(contents) { }
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
    public sealed class small : XhtmlTag
    {
        public small(params object[] contents) : base(contents) { }
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
    public sealed class span : XhtmlTag
    {
        public span(params object[] contents) : base(contents) { }
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
    public sealed class strong : XhtmlTag
    {
        public strong(params object[] contents) : base(contents) { }
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
    public sealed class style : XhtmlTag
    {
        public style(params object[] contents) : base(contents) { }
        public override string TagName { get { return "style"; } }
        public dir dir;
        public string id;
        public string lang;
        public string media;
        public string title;
        public string type;
        public string xmlLang;
    }
    public sealed class styleImport : XhtmlTag
    {
        public styleImport(params object[] contents) : base(contents) { }
        public override string TagName { get { return "style"; } }
        public string ImportFrom;
        public string media;
        public override IEnumerable<string> ToEnumerable()
        {
            yield return @"<style type=""text/css"" media=""";
            yield return media.HtmlEscape();
            yield return @""">/*<![CDATA[*/ @import """;
            yield return ImportFrom;
            yield return @"""; /*]]>*/</style>";
        }
    }
    public sealed class sub : XhtmlTag
    {
        public sub(params object[] contents) : base(contents) { }
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
    public sealed class sup : XhtmlTag
    {
        public sup(params object[] contents) : base(contents) { }
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
    public class table : XhtmlTag
    {
        public table(params object[] contents) : base(contents) { }
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
    public sealed class tbody : XhtmlTag
    {
        public tbody(params object[] contents) : base(contents) { }
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
    public sealed class td : XhtmlTag
    {
        public td(params object[] contents) : base(contents) { }
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
    public sealed class textarea : XhtmlTag
    {
        public textarea(params object[] contents) : base(contents) { }
        public override string TagName { get { return "textarea"; } }
        public string accesskey;
        public string class_;
        public int cols;
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
        public int rows;
        public string style;
        public string tabindex;
        public string title;
        public string xmlLang;
    }
    public sealed class tfoot : XhtmlTag
    {
        public tfoot(params object[] contents) : base(contents) { }
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
    public sealed class th : XhtmlTag
    {
        public th(params object[] contents) : base(contents) { }
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
    public sealed class thead : XhtmlTag
    {
        public thead(params object[] contents) : base(contents) { }
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
    public sealed class title : XhtmlTag
    {
        public title(params object[] contents) : base(contents) { }
        public override string TagName { get { return "title"; } }
        public dir dir;
        public string id;
        public string lang;
        public string xmlLang;
    }
    public sealed class tr : XhtmlTag
    {
        public tr(params object[] contents) : base(contents) { }
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
    public sealed class tt : XhtmlTag
    {
        public tt(params object[] contents) : base(contents) { }
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
    public sealed class ul : XhtmlTag
    {
        public ul(params object[] contents) : base(contents) { }
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
    public sealed class var_ : XhtmlTag
    {
        public var_(params object[] contents) : base(contents) { }
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

#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

}
