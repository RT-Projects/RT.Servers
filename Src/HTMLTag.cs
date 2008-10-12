using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RT.TagSoup.HTMLTags
{
    /// <summary>Abstract base class for HTML tags.</summary>
    public abstract class HTMLTag : TagSoup
    {
        /// <summary>Constructs an HTML tag.</summary>
        /// <param name="Contents">Contents of the tag.</param>
        public HTMLTag(params object[] Contents) { TagContents = new List<object>(Contents); }
        /// <summary>Returns false.</summary>
        public override bool AllowXHTMLEmpty { get { return false; } }
    }

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

    public class A : HTMLTag
    {
        public A(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "A"; } }
        public string accesskey;
        public string charset;
        public string class_;
        public string coords;
        public string dir;
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
        public string shape;
        public string style;
        public string tabindex;
        public string title;
        public string type;
    }
    public class ABBR : HTMLTag
    {
        public ABBR(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "ABBR"; } }
        public string class_;
        public string dir;
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
    public class ACRONYM : HTMLTag
    {
        public ACRONYM(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "ACRONYM"; } }
        public string class_;
        public string dir;
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
    public class ADDRESS : HTMLTag
    {
        public ADDRESS(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "ADDRESS"; } }
        public string class_;
        public string dir;
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
    public class AREA : HTMLTag
    {
        public AREA(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "AREA"; } }
        public override bool EndTag { get { return false; } }
        public string accesskey;
        public string alt;
        public string class_;
        public string coords;
        public string dir;
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
        public string shape;
        public string style;
        public string tabindex;
        public string title;
    }
    public class B : HTMLTag
    {
        public B(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "B"; } }
        public string class_;
        public string dir;
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
    public class BASE : HTMLTag
    {
        public BASE(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "BASE"; } }
        public override bool EndTag { get { return false; } }
        public string href;
    }
    public class BDO : HTMLTag
    {
        public BDO(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "BDO"; } }
        public string class_;
        public string dir;
        public string id;
        public string lang;
        public string style;
        public string title;
    }
    public class BIG : HTMLTag
    {
        public BIG(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "BIG"; } }
        public string class_;
        public string dir;
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
    public class BLOCKQUOTE : HTMLTag
    {
        public BLOCKQUOTE(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "BLOCKQUOTE"; } }
        public string cite;
        public string class_;
        public string dir;
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
    public class BODY : HTMLTag
    {
        public BODY(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "BODY"; } }
        public override bool StartTag { get { return false; } }
        public override bool EndTag { get { return false; } }
        public string class_;
        public string dir;
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
    public class BR : HTMLTag
    {
        public BR(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "BR"; } }
        public override bool EndTag { get { return false; } }
        public string class_;
        public string id;
        public string style;
        public string title;
    }
    public class BUTTON : HTMLTag
    {
        public BUTTON(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "BUTTON"; } }
        public string accesskey;
        public string class_;
        public string dir;
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
        public string type;
        public string value;
    }
    public class CAPTION : HTMLTag
    {
        public CAPTION(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "CAPTION"; } }
        public string class_;
        public string dir;
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
    public class CITE : HTMLTag
    {
        public CITE(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "CITE"; } }
        public string class_;
        public string dir;
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
    public class CODE : HTMLTag
    {
        public CODE(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "CODE"; } }
        public string class_;
        public string dir;
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
    public class COL : HTMLTag
    {
        public COL(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "COL"; } }
        public override bool EndTag { get { return false; } }
        public string align;
        public string char_;
        public string charoff;
        public string class_;
        public string dir;
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
        public string valign;
        public string width;
    }
    public class COLGROUP : HTMLTag
    {
        public COLGROUP(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "COLGROUP"; } }
        public override bool EndTag { get { return false; } }
        public string align;
        public string char_;
        public string charoff;
        public string class_;
        public string dir;
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
        public string valign;
        public string width;
    }
    public class DD : HTMLTag
    {
        public DD(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "DD"; } }
        public override bool EndTag { get { return false; } }
        public string class_;
        public string dir;
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
    public class DEL : HTMLTag
    {
        public DEL(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "DEL"; } }
        public string cite;
        public string class_;
        public string datetime;
        public string dir;
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
    public class DFN : HTMLTag
    {
        public DFN(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "DFN"; } }
        public string class_;
        public string dir;
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
    public class DIV : HTMLTag
    {
        public DIV(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "DIV"; } }
        public string class_;
        public string dir;
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
    public class DL : HTMLTag
    {
        public DL(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "DL"; } }
        public string class_;
        public string dir;
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
    public class DT : HTMLTag
    {
        public DT(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "DT"; } }
        public override bool EndTag { get { return false; } }
        public string class_;
        public string dir;
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
    public class EM : HTMLTag
    {
        public EM(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "EM"; } }
        public string class_;
        public string dir;
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
    public class FIELDSET : HTMLTag
    {
        public FIELDSET(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "FIELDSET"; } }
        public string class_;
        public string dir;
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
    public class FORM : HTMLTag
    {
        public FORM(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "FORM"; } }
        public string accept;
        public string accept_charset;
        public string action;
        public string class_;
        public string dir;
        public string enctype;
        public string id;
        public string lang;
        public string method;
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
    }
    public class H1 : HTMLTag
    {
        public H1(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "H1"; } }
        public string class_;
        public string dir;
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
    public class H2 : HTMLTag
    {
        public H2(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "H2"; } }
        public string class_;
        public string dir;
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
    public class H3 : HTMLTag
    {
        public H3(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "H3"; } }
        public string class_;
        public string dir;
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
    public class H4 : HTMLTag
    {
        public H4(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "H4"; } }
        public string class_;
        public string dir;
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
    public class H5 : HTMLTag
    {
        public H5(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "H5"; } }
        public string class_;
        public string dir;
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
    public class H6 : HTMLTag
    {
        public H6(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "H6"; } }
        public string class_;
        public string dir;
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
    public class HEAD : HTMLTag
    {
        public HEAD(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "HEAD"; } }
        public override bool StartTag { get { return false; } }
        public override bool EndTag { get { return false; } }
        public string dir;
        public string lang;
        public string profile;
    }
    public class HR : HTMLTag
    {
        public HR(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "HR"; } }
        public override bool EndTag { get { return false; } }
        public string class_;
        public string dir;
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
    public class HTML : HTMLTag
    {
        public HTML(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "HTML"; } }
        public override bool StartTag { get { return false; } }
        public override bool EndTag { get { return false; } }
        public override string DocType { get { return @"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01//EN"" ""http://www.w3.org/TR/html4/strict.dtd"">"; } }
        public string dir;
        public string lang;
    }
    public class I : HTMLTag
    {
        public I(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "I"; } }
        public string class_;
        public string dir;
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
    public class IMG : HTMLTag
    {
        public IMG(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "IMG"; } }
        public override bool EndTag { get { return false; } }
        public string alt;
        public string class_;
        public string dir;
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
    public class INPUT : HTMLTag
    {
        public INPUT(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "INPUT"; } }
        public override bool EndTag { get { return false; } }
        public string PASSWORD;
        public string class_;
        public string dir;
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
        public string type;
    }
    public class INS : HTMLTag
    {
        public INS(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "INS"; } }
        public string cite;
        public string class_;
        public string datetime;
        public string dir;
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
    public class KBD : HTMLTag
    {
        public KBD(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "KBD"; } }
        public string class_;
        public string dir;
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
    public class LABEL : HTMLTag
    {
        public LABEL(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "LABEL"; } }
        public string accesskey;
        public string class_;
        public string dir;
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
    public class LEGEND : HTMLTag
    {
        public LEGEND(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "LEGEND"; } }
        public string accesskey;
        public string class_;
        public string dir;
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
    public class LI : HTMLTag
    {
        public LI(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "LI"; } }
        public override bool EndTag { get { return false; } }
        public string class_;
        public string dir;
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
    public class LINK : HTMLTag
    {
        public LINK(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "LINK"; } }
        public override bool EndTag { get { return false; } }
        public string charset;
        public string class_;
        public string dir;
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
    public class MAP : HTMLTag
    {
        public MAP(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "MAP"; } }
        public string class_;
        public string dir;
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
    public class META : HTMLTag
    {
        public META(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "META"; } }
        public override bool EndTag { get { return false; } }
        public string content;
        public string dir;
        public string http_equiv;
        public string lang;
        public string name;
        public string scheme;
    }
    public class NOSCRIPT : HTMLTag
    {
        public NOSCRIPT(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "NOSCRIPT"; } }
        public string class_;
        public string dir;
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
    public class OBJECT : HTMLTag
    {
        public OBJECT(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "OBJECT"; } }
        public string archive;
        public string class_;
        public string classid;
        public string codebase;
        public string codetype;
        public string data;
        public string declare;
        public string dir;
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
    public class OL : HTMLTag
    {
        public OL(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "OL"; } }
        public string class_;
        public string dir;
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
    public class OPTGROUP : HTMLTag
    {
        public OPTGROUP(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "OPTGROUP"; } }
        public string class_;
        public string dir;
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
    public class OPTION : HTMLTag
    {
        public OPTION(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "OPTION"; } }
        public override bool EndTag { get { return false; } }
        public string class_;
        public string dir;
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
    public class P : HTMLTag
    {
        public P(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "P"; } }
        public override bool EndTag { get { return false; } }
        public string class_;
        public string dir;
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
    public class PARAM : HTMLTag
    {
        public PARAM(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "PARAM"; } }
        public override bool EndTag { get { return false; } }
        public string id;
        public string name;
        public string type;
        public string value;
        public string valuetype;
    }
    public class PRE : HTMLTag
    {
        public PRE(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "PRE"; } }
        public string class_;
        public string dir;
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
    public class Q : HTMLTag
    {
        public Q(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "Q"; } }
        public string cite;
        public string class_;
        public string dir;
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
    public class SAMP : HTMLTag
    {
        public SAMP(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "SAMP"; } }
        public string class_;
        public string dir;
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
    public class SCRIPT : HTMLTag
    {
        public SCRIPT(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "SCRIPT"; } }
        public string charset;
        public string defer;
        public string event_;
        public string for_;
        public string src;
        public string type;
    }
    public class SELECT : HTMLTag
    {
        public SELECT(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "SELECT"; } }
        public string class_;
        public string dir;
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
    public class SMALL : HTMLTag
    {
        public SMALL(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "SMALL"; } }
        public string class_;
        public string dir;
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
    public class SPAN : HTMLTag
    {
        public SPAN(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "SPAN"; } }
        public string class_;
        public string dir;
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
    public class STRONG : HTMLTag
    {
        public STRONG(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "STRONG"; } }
        public string class_;
        public string dir;
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
    public class STYLE : HTMLTag
    {
        public STYLE(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "STYLE"; } }
        public string dir;
        public string lang;
        public string media;
        public string title;
        public string type;
    }
    public class SUB : HTMLTag
    {
        public SUB(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "SUB"; } }
        public string class_;
        public string dir;
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
    public class SUP : HTMLTag
    {
        public SUP(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "SUP"; } }
        public string class_;
        public string dir;
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
    public class TABLE : HTMLTag
    {
        public TABLE(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "TABLE"; } }
        public string border;
        public string class_;
        public string dir;
        public string frame;
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
        public string rules;
        public string style;
        public string summary;
        public string title;
        public string width;
    }
    public class TBODY : HTMLTag
    {
        public TBODY(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "TBODY"; } }
        public override bool StartTag { get { return false; } }
        public override bool EndTag { get { return false; } }
        public string align;
        public string char_;
        public string charoff;
        public string class_;
        public string dir;
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
        public string valign;
    }
    public class TD : HTMLTag
    {
        public TD(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "TD"; } }
        public override bool EndTag { get { return false; } }
        public string abbr;
        public string align;
        public string axis;
        public string char_;
        public string charoff;
        public string class_;
        public string colspan;
        public string dir;
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
        public string scope;
        public string style;
        public string title;
        public string valign;
        public string width;
    }
    public class TEXTAREA : HTMLTag
    {
        public TEXTAREA(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "TEXTAREA"; } }
        public string accesskey;
        public string class_;
        public string cols;
        public string dir;
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
        public string rows;
        public string style;
        public string tabindex;
        public string title;
    }
    public class TFOOT : HTMLTag
    {
        public TFOOT(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "TFOOT"; } }
        public override bool EndTag { get { return false; } }
        public string align;
        public string char_;
        public string charoff;
        public string class_;
        public string dir;
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
        public string valign;
    }
    public class TH : HTMLTag
    {
        public TH(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "TH"; } }
        public override bool EndTag { get { return false; } }
        public string abbr;
        public string align;
        public string axis;
        public string char_;
        public string charoff;
        public string class_;
        public string colspan;
        public string dir;
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
        public string scope;
        public string style;
        public string title;
        public string valign;
    }
    public class THEAD : HTMLTag
    {
        public THEAD(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "THEAD"; } }
        public override bool EndTag { get { return false; } }
        public string align;
        public string char_;
        public string charoff;
        public string class_;
        public string dir;
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
        public string valign;
    }
    public class TITLE : HTMLTag
    {
        public TITLE(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "TITLE"; } }
    }
    public class TR : HTMLTag
    {
        public TR(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "TR"; } }
        public override bool EndTag { get { return false; } }
        public string align;
        public string char_;
        public string charoff;
        public string class_;
        public string dir;
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
        public string valign;
    }
    public class TT : HTMLTag
    {
        public TT(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "TT"; } }
        public string class_;
        public string dir;
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
    public class UL : HTMLTag
    {
        public UL(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "UL"; } }
        public string class_;
        public string dir;
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
    public class VAR : HTMLTag
    {
        public VAR(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "VAR"; } }
        public string class_;
        public string dir;
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
    public class WBR : HTMLTag
    {
        public WBR(params object[] Contents) : base(Contents) { }
        public override string TagName { get { return "WBR"; } }
        public override bool EndTag { get { return false; } }
    }

#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

}
