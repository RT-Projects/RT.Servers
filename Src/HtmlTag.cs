using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.ExtensionMethods;

namespace RT.TagSoup.HtmlTags
{
    /// <summary>Abstract base class for HTML tags.</summary>
    public abstract class HtmlTag : Tag
    {
        /// <summary>Constructs an HTML tag.</summary>
        /// <param name="Contents">Contents of the tag.</param>
        public HtmlTag(params object[] Contents) { TagContents = new List<object>(Contents); }
        /// <summary>Returns false.</summary>
        public override bool AllowXhtmlEmpty { get { return false; } }
        /// <summary>Creates a simple HTML document from the specified elements.</summary>
        /// <param name="title">Title to use in the &lt;TITLE&gt; tag in the head.</param>
        /// <param name="bodyContent">Contents of the &lt;BODY&gt; tag.</param>
        /// <returns>An <see cref="HtmlTag"/> representing the entire HTML document.</returns>
        public static HtmlTag HtmlDocument(object title, params object[] bodyContent) { return new HTML(new HEAD(new TITLE(title)), new BODY(bodyContent)); }
    }

    /// <summary>Special class to help construct an HTML <c>&lt;TABLE&gt;</c> element
    /// without needing to instantiate all intermediate row and cell tags.</summary>
    public sealed class HtmlTable : TABLE
    {
        /// <summary>If set to a value other than null, causes all rows and cells within the generated table to have the specified CSS class.</summary>
        public string _AllClasses;

        /// <summary>Constructs an HTML table in which all rows and cells have the same CSS class.</summary>
        /// <param name="classOnAllTags">Optional. If non-null, all rows and cells within the generated table have the specified CSS class.</param>
        /// <param name="rows">Rows (arrays of cell contents).</param>
        public HtmlTable(string classOnAllTags, params object[][] rows)
        {
            if (classOnAllTags != null)
                class_ = classOnAllTags;
            List<object> rowTags = new List<object>();
            foreach (object[] row in rows)
            {
                List<object> cellTags = new List<object>();
                foreach (object cell in row)
                    cellTags.Add(classOnAllTags == null ? new TD(cell) : new TD(cell) { class_ = classOnAllTags });
                rowTags.Add(classOnAllTags == null ? new TR(cellTags.ToArray()) : new TR(cellTags.ToArray()) { class_ = classOnAllTags });
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

    public sealed class A : HtmlTag
    {
        public A(params object[] contents) : base(contents) { }
        public override string TagName { get { return "A"; } }
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
        public string target;
        public string title;
        public string type;
    }
    public sealed class ABBR : HtmlTag
    {
        public ABBR(params object[] contents) : base(contents) { }
        public override string TagName { get { return "ABBR"; } }
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
    }
    public sealed class ACRONYM : HtmlTag
    {
        public ACRONYM(params object[] contents) : base(contents) { }
        public override string TagName { get { return "ACRONYM"; } }
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
    }
    public sealed class ADDRESS : HtmlTag
    {
        public ADDRESS(params object[] contents) : base(contents) { }
        public override string TagName { get { return "ADDRESS"; } }
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
    }
    public sealed class AREA : HtmlTag
    {
        public AREA(params object[] contents) : base(contents) { }
        public override string TagName { get { return "AREA"; } }
        public override bool EndTag { get { return false; } }
        public string accesskey;
        public string alt;
        public string class_;
        public string coords;
        public dir dir;
        public string href;
        public string id;
        public string lang;
        public string nohref;
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
    }
    public sealed class B : HtmlTag
    {
        public B(params object[] contents) : base(contents) { }
        public override string TagName { get { return "B"; } }
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
    }
    public sealed class BASE : HtmlTag
    {
        public BASE(params object[] contents) : base(contents) { }
        public override string TagName { get { return "BASE"; } }
        public override bool EndTag { get { return false; } }
        public string href;
    }
    public sealed class BDO : HtmlTag
    {
        public BDO(params object[] contents) : base(contents) { }
        public override string TagName { get { return "BDO"; } }
        public string class_;
        public dir dir;
        public string id;
        public string lang;
        public string style;
        public string title;
    }
    public sealed class BIG : HtmlTag
    {
        public BIG(params object[] contents) : base(contents) { }
        public override string TagName { get { return "BIG"; } }
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
    }
    public sealed class BLOCKQUOTE : HtmlTag
    {
        public BLOCKQUOTE(params object[] contents) : base(contents) { }
        public override string TagName { get { return "BLOCKQUOTE"; } }
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
    }
    public sealed class BODY : HtmlTag
    {
        public BODY(params object[] contents) : base(contents) { }
        public override string TagName { get { return "BODY"; } }
        public override bool StartTag { get { return false; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class BR : HtmlTag
    {
        public BR(params object[] contents) : base(contents) { }
        public override string TagName { get { return "BR"; } }
        public override bool EndTag { get { return false; } }
        public string class_;
        public string id;
        public string style;
        public string title;
    }
    public sealed class BUTTON : HtmlTag
    {
        public BUTTON(params object[] contents) : base(contents) { }
        public override string TagName { get { return "BUTTON"; } }
        public string accesskey;
        public string class_;
        public dir dir;
        public string disabled;
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
        public string target;
    }
    public sealed class CAPTION : HtmlTag
    {
        public CAPTION(params object[] contents) : base(contents) { }
        public override string TagName { get { return "CAPTION"; } }
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
    }
    public sealed class CITE : HtmlTag
    {
        public CITE(params object[] contents) : base(contents) { }
        public override string TagName { get { return "CITE"; } }
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
    }
    public sealed class CODE : HtmlTag
    {
        public CODE(params object[] contents) : base(contents) { }
        public override string TagName { get { return "CODE"; } }
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
    }
    public sealed class COL : HtmlTag
    {
        public COL(params object[] contents) : base(contents) { }
        public override string TagName { get { return "COL"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class COLGROUP : HtmlTag
    {
        public COLGROUP(params object[] contents) : base(contents) { }
        public override string TagName { get { return "COLGROUP"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class DD : HtmlTag
    {
        public DD(params object[] contents) : base(contents) { }
        public override string TagName { get { return "DD"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class DEL : HtmlTag
    {
        public DEL(params object[] contents) : base(contents) { }
        public override string TagName { get { return "DEL"; } }
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
    }
    public sealed class DFN : HtmlTag
    {
        public DFN(params object[] contents) : base(contents) { }
        public override string TagName { get { return "DFN"; } }
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
    }
    public sealed class DIV : HtmlTag
    {
        public DIV(params object[] contents) : base(contents) { }
        public override string TagName { get { return "DIV"; } }
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
    }
    public sealed class DL : HtmlTag
    {
        public DL(params object[] contents) : base(contents) { }
        public override string TagName { get { return "DL"; } }
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
    }
    public sealed class DT : HtmlTag
    {
        public DT(params object[] contents) : base(contents) { }
        public override string TagName { get { return "DT"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class EM : HtmlTag
    {
        public EM(params object[] contents) : base(contents) { }
        public override string TagName { get { return "EM"; } }
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
    }
    public sealed class FIELDSET : HtmlTag
    {
        public FIELDSET(params object[] contents) : base(contents) { }
        public override string TagName { get { return "FIELDSET"; } }
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
    }
    public sealed class FORM : HtmlTag
    {
        public FORM(params object[] contents) : base(contents) { }
        public override string TagName { get { return "FORM"; } }
        public string accept;
        public string accept_charset;
        public string action;
        public string class_;
        public dir dir;
        public string enctype;
        public string id;
        public string lang;
        public method method;
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
        public string onreset;
        public string onsubmit;
        public string style;
        public string title;
        public string target;
    }
    public sealed class H1 : HtmlTag
    {
        public H1(params object[] contents) : base(contents) { }
        public override string TagName { get { return "H1"; } }
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
    }
    public sealed class H2 : HtmlTag
    {
        public H2(params object[] contents) : base(contents) { }
        public override string TagName { get { return "H2"; } }
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
    }
    public sealed class H3 : HtmlTag
    {
        public H3(params object[] contents) : base(contents) { }
        public override string TagName { get { return "H3"; } }
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
    }
    public sealed class H4 : HtmlTag
    {
        public H4(params object[] contents) : base(contents) { }
        public override string TagName { get { return "H4"; } }
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
    }
    public sealed class H5 : HtmlTag
    {
        public H5(params object[] contents) : base(contents) { }
        public override string TagName { get { return "H5"; } }
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
    }
    public sealed class H6 : HtmlTag
    {
        public H6(params object[] contents) : base(contents) { }
        public override string TagName { get { return "H6"; } }
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
    }
    public sealed class HEAD : HtmlTag
    {
        public HEAD(params object[] contents) : base(contents) { }
        public override string TagName { get { return "HEAD"; } }
        public override bool StartTag { get { return false; } }
        public override bool EndTag { get { return false; } }
        public dir dir;
        public string lang;
        public string profile;
    }
    public sealed class HR : HtmlTag
    {
        public HR(params object[] contents) : base(contents) { }
        public override string TagName { get { return "HR"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class HTML : HtmlTag
    {
        public HTML(params object[] contents) : base(contents) { }
        public override string TagName { get { return "HTML"; } }
        public override bool StartTag { get { return false; } }
        public override bool EndTag { get { return false; } }
        public override string DocType { get { return @"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01//EN"" ""http://www.w3.org/TR/html4/strict.dtd"">"; } }
        public dir dir;
        public string lang;
    }
    public sealed class I : HtmlTag
    {
        public I(params object[] contents) : base(contents) { }
        public override string TagName { get { return "I"; } }
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
    }
    public sealed class IMG : HtmlTag
    {
        public IMG(params object[] contents) : base(contents) { }
        public override string TagName { get { return "IMG"; } }
        public override bool EndTag { get { return false; } }
        public string alt;
        public string class_;
        public dir dir;
        public string height;
        public string id;
        public string ismap;
        public string lang;
        public string longdesc;
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
        public string src;
        public string style;
        public string title;
        public string usemap;
        public string width;
    }
    public sealed class INPUT : HtmlTag
    {
        public INPUT(params object[] contents) : base(contents) { }
        public override string TagName { get { return "INPUT"; } }
        public override bool EndTag { get { return false; } }
        public string PASSWORD;
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
        public itype type;
        public string value;
        public bool checked_;
        public bool disabled;
        public bool readonly_;
        public int? size;
        public int? maxlength;
        public string src;
        public string alt;
        public string usemap;
        public bool ismap;
        public int? tabindex;
        public string accesskey;
        public string onfocus;
        public string onblur;
        public string onselect;
        public string onchange;
        public string accept;
        public string target;
    }
    public sealed class INS : HtmlTag
    {
        public INS(params object[] contents) : base(contents) { }
        public override string TagName { get { return "INS"; } }
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
    }
    public sealed class KBD : HtmlTag
    {
        public KBD(params object[] contents) : base(contents) { }
        public override string TagName { get { return "KBD"; } }
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
    }
    public sealed class LABEL : HtmlTag
    {
        public LABEL(params object[] contents) : base(contents) { }
        public override string TagName { get { return "LABEL"; } }
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
    }
    public sealed class LEGEND : HtmlTag
    {
        public LEGEND(params object[] contents) : base(contents) { }
        public override string TagName { get { return "LEGEND"; } }
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
    }
    public sealed class LI : HtmlTag
    {
        public LI(params object[] contents) : base(contents) { }
        public override string TagName { get { return "LI"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class LINK : HtmlTag
    {
        public LINK(params object[] contents) : base(contents) { }
        public override string TagName { get { return "LINK"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class MAP : HtmlTag
    {
        public MAP(params object[] contents) : base(contents) { }
        public override string TagName { get { return "MAP"; } }
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
    }
    public sealed class META : HtmlTag
    {
        public META(params object[] contents) : base(contents) { }
        public override string TagName { get { return "META"; } }
        public override bool EndTag { get { return false; } }
        public string content;
        public dir dir;
        public string http_equiv;
        public string lang;
        public string name;
        public string scheme;
    }
    public sealed class NOSCRIPT : HtmlTag
    {
        public NOSCRIPT(params object[] contents) : base(contents) { }
        public override string TagName { get { return "NOSCRIPT"; } }
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
    }
    public sealed class OBJECT : HtmlTag
    {
        public OBJECT(params object[] contents) : base(contents) { }
        public override string TagName { get { return "OBJECT"; } }
        public string archive;
        public string class_;
        public string classid;
        public string codebase;
        public string codetype;
        public string data;
        public string declare;
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
    }
    public sealed class OL : HtmlTag
    {
        public OL(params object[] contents) : base(contents) { }
        public override string TagName { get { return "OL"; } }
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
    }
    public sealed class OPTGROUP : HtmlTag
    {
        public OPTGROUP(params object[] contents) : base(contents) { }
        public override string TagName { get { return "OPTGROUP"; } }
        public string class_;
        public dir dir;
        public string disabled;
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
    }
    public sealed class OPTION : HtmlTag
    {
        public OPTION(params object[] contents) : base(contents) { }
        public override string TagName { get { return "OPTION"; } }
        public override bool EndTag { get { return false; } }
        public string class_;
        public dir dir;
        public string disabled;
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
        public string selected;
        public string style;
        public string title;
        public string value;
    }
    public sealed class P : HtmlTag
    {
        public P(params object[] contents) : base(contents) { }
        public override string TagName { get { return "P"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class PARAM : HtmlTag
    {
        public PARAM(params object[] contents) : base(contents) { }
        public override string TagName { get { return "PARAM"; } }
        public override bool EndTag { get { return false; } }
        public string id;
        public string name;
        public string type;
        public string value;
        public valuetype valuetype;
    }
    public sealed class PRE : HtmlTag
    {
        public PRE(params object[] contents) : base(contents) { }
        public override string TagName { get { return "PRE"; } }
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
    }
    public sealed class Q : HtmlTag
    {
        public Q(params object[] contents) : base(contents) { }
        public override string TagName { get { return "Q"; } }
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
    }
    public sealed class SAMP : HtmlTag
    {
        public SAMP(params object[] contents) : base(contents) { }
        public override string TagName { get { return "SAMP"; } }
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
    }
    public sealed class SCRIPT : HtmlTag
    {
        public SCRIPT(params object[] contents) : base(contents) { }
        public override string TagName { get { return "SCRIPT"; } }
        public string charset;
        public string defer;
        public string event_;
        public string for_;
        public string src;
        public string type;
    }
    public sealed class SCRIPTLiteral : HtmlTag
    {
        public SCRIPTLiteral(string literal) : base() { Literal = literal; }
        public override string TagName { get { return "SCRIPT"; } }
        public string Literal;
        public override IEnumerable<string> ToEnumerable()
        {
            yield return @"<SCRIPT type=""text/javascript"">";
            yield return Literal;
            yield return @"</SCRIPT>";
        }
    }
    public sealed class SELECT : HtmlTag
    {
        public SELECT(params object[] contents) : base(contents) { }
        public override string TagName { get { return "SELECT"; } }
        public string class_;
        public dir dir;
        // accesskey is not actually allowed on SELECT elements in HTML, but don't care
        public string accesskey;
        public string disabled;
        public string id;
        public string lang;
        public string multiple;
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
    }
    public sealed class SMALL : HtmlTag
    {
        public SMALL(params object[] contents) : base(contents) { }
        public override string TagName { get { return "SMALL"; } }
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
    }
    public sealed class SPAN : HtmlTag
    {
        public SPAN(params object[] contents) : base(contents) { }
        public override string TagName { get { return "SPAN"; } }
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
    }
    public sealed class STRONG : HtmlTag
    {
        public STRONG(params object[] contents) : base(contents) { }
        public override string TagName { get { return "STRONG"; } }
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
    }
    public sealed class STYLE : HtmlTag
    {
        public STYLE(params object[] contents) : base(contents) { }
        public override string TagName { get { return "STYLE"; } }
        public dir dir;
        public string lang;
        public string media;
        public string title;
        public string type;
    }
    public sealed class STYLEImport : HtmlTag
    {
        public STYLEImport(string importFrom) : base() { ImportFrom = importFrom; }
        public override string TagName { get { return "STYLE"; } }
        public string ImportFrom;
        public string media;
        public override IEnumerable<string> ToEnumerable()
        {
            yield return @"<STYLE type=""text/css""";
            if (media != null)
                yield return @" media=""" + media.HtmlEscape() + @"""";
            yield return @">@import """;
            yield return ImportFrom;
            yield return @""";</STYLE>";
        }
    }
    public sealed class STYLELiteral : HtmlTag
    {
        public STYLELiteral(string literal) : base() { Literal = literal; }
        public override string TagName { get { return "STYLE"; } }
        public string Literal;
        public override IEnumerable<string> ToEnumerable()
        {
            yield return @"<STYLE type=""text/css"">";
            yield return Literal;
            yield return @"</STYLE>";
        }
    }
    public sealed class SUB : HtmlTag
    {
        public SUB(params object[] contents) : base(contents) { }
        public override string TagName { get { return "SUB"; } }
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
    }
    public sealed class SUP : HtmlTag
    {
        public SUP(params object[] contents) : base(contents) { }
        public override string TagName { get { return "SUP"; } }
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
    }
    public class TABLE : HtmlTag
    {
        public TABLE(params object[] contents) : base(contents) { }
        public override string TagName { get { return "TABLE"; } }
        public string border;
        public string class_;
        public dir dir;
        public frame frame;
        public string groups;
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
    }
    public sealed class TBODY : HtmlTag
    {
        public TBODY(params object[] contents) : base(contents) { }
        public override string TagName { get { return "TBODY"; } }
        public override bool StartTag { get { return false; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class TD : HtmlTag
    {
        public TD(params object[] contents) : base(contents) { }
        public override string TagName { get { return "TD"; } }
        public override bool EndTag { get { return false; } }
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
        public string width;
    }
    public sealed class TEXTAREA : HtmlTag
    {
        public TEXTAREA(params object[] contents) : base(contents) { }
        public override string TagName { get { return "TEXTAREA"; } }
        public string accesskey;
        public string class_;
        public int? cols;
        public dir dir;
        public string disabled;
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
        public string readonly_;
        public int? rows;
        public string style;
        public string tabindex;
        public string title;
    }
    public sealed class TFOOT : HtmlTag
    {
        public TFOOT(params object[] contents) : base(contents) { }
        public override string TagName { get { return "TFOOT"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class TH : HtmlTag
    {
        public TH(params object[] contents) : base(contents) { }
        public override string TagName { get { return "TH"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class THEAD : HtmlTag
    {
        public THEAD(params object[] contents) : base(contents) { }
        public override string TagName { get { return "THEAD"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class TITLE : HtmlTag
    {
        public TITLE(params object[] contents) : base(contents) { }
        public override string TagName { get { return "TITLE"; } }
    }
    public sealed class TR : HtmlTag
    {
        public TR(params object[] contents) : base(contents) { }
        public override string TagName { get { return "TR"; } }
        public override bool EndTag { get { return false; } }
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
    }
    public sealed class TT : HtmlTag
    {
        public TT(params object[] contents) : base(contents) { }
        public override string TagName { get { return "TT"; } }
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
    }
    public sealed class U : HtmlTag
    {
        public U(params object[] contents) : base(contents) { }
        public override string TagName { get { return "U"; } }
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
    }
    public sealed class UL : HtmlTag
    {
        public UL(params object[] contents) : base(contents) { }
        public override string TagName { get { return "UL"; } }
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
    }
    public class VAR : HtmlTag
    {
        public VAR(params object[] contents) : base(contents) { }
        public override string TagName { get { return "VAR"; } }
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
    }
    public sealed class WBR : HtmlTag
    {
        public WBR(params object[] contents) : base(contents) { }
        public override string TagName { get { return "WBR"; } }
        public override bool EndTag { get { return false; } }
    }

#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

}
