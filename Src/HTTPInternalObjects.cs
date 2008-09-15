using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Servers
{
    public static class HTTPInternalObjects
    {
        public static byte[] DirectoryListingXSL = Encoding.UTF8.GetBytes(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<xsl:stylesheet 
 version=""1.0""
 xmlns:xsl=""http://www.w3.org/1999/XSL/Transform""
 xmlns:xs=""http://www.w3.org/2001/XMLSchema""
 xmlns=""http://www.w3.org/1999/xhtml"">
 
<xsl:output method=""html""/>
 
<xsl:template match=""/directory"">
    <html>
    <head>
        <title>Directory listing</title>
        <style type=""text/css"">
            body { margin: 10pt; font-family: ""Verdana""; font-size: 10pt }
            table { border-collapse: collapse }
            .topline { border-top: 2px solid #339 }
            .listheader th { padding: 3pt 0 }
            .listname { padding-right: 5pt; border-right: 1px solid #ccc }
            .listsize { padding: 0 5pt }
            .listsize.dir { text-align: center; font-style: italic; }
            .listsize.file { text-align: right; }
            .filerow:hover { background: #eef; }
            .filerow .listname a { display: block; }
        </style>
    </head>
    <body>
        <table>
            <!-- TITLE SECTION -->
            <tr class=""topline"">
                <table>
                    <tr>
                        <td rowspan=""2""><img src=""{@img}"" alt=""folder""/></td>
                        <td style=""font-size: 180%"">Directory listing</td>
                    </tr>
                    <tr>
                        <td><xsl:value-of select=""@url""/>/</td>
                    </tr>
                </table>
            </tr>

            <!-- FILE LISTING -->
            <tr class=""topline""><td colspan=""2"">
                <table style=""width: 100%"">
                    <tr class=""listheader""><th class=""listname"" colspan=""2"">Name</th><th>Size</th></tr>
                    <tr><td></td><td class=""listname""><a href=""{@url}/.."">..</a></td><td class=""listsize dir"">Folder</td></tr>
                    <xsl:apply-templates select=""dir"" />
                    <xsl:apply-templates select=""file"" />
                    <tr style=""height: 3pt""><td class=""listname"" colspan=""2""/><td/></tr>
                </table>
            </td></tr>

            <!-- FOOTER -->
            <tr class=""topline"" style=""font-size: 80%; font-style: italic; color: #888"">
                <td colspan=""2"">
                    <xsl:choose>
                        <xsl:when test=""@numdirs &gt; 0 and @numfiles &gt; 0"">
                            Directory contains <xsl:value-of select=""@numdirs""/> sub-directories and <xsl:value-of select=""@numfiles""/> files.
                        </xsl:when>
                        <xsl:when test=""@numdirs &gt; 0 and @numfiles = 0"">
                            Directory contains <xsl:value-of select=""@numdirs""/> sub-directories.
                        </xsl:when>
                        <xsl:when test=""@numdirs = 0 and @numfiles &gt; 0"">
                            Directory contains <xsl:value-of select=""@numfiles""/> files.
                        </xsl:when>
                        <xsl:otherwise>
                            Directory is empty.
                        </xsl:otherwise>
                    </xsl:choose>
                </td>
            </tr>
        </table>
    </body>
    </html>
</xsl:template>

<xsl:template match=""dir"">
    <tr>
        <td><img src=""{@img}"" alt=""folder""/></td>
        <td class=""listname""><a href=""{@link}""><xsl:value-of select="".""/></a></td>
        <td class=""listsize dir"">Folder</td>
    </tr>
</xsl:template>

<xsl:template match=""file"">
    <tr class=""filerow"">
        <td><img src=""{@img}"" alt=""file""/></td>
        <td class=""listname"" style=""min-width: 150pt;""><a href=""{@link}""><xsl:value-of select="".""/></a></td>
        <td class=""listsize file""><xsl:value-of select=""@nicesize""/></td>
    </tr>
</xsl:template>

</xsl:stylesheet>");

        public static byte[] GetDirectoryListingIcon(string Ext)
        {
            if (Ext == "folder") return Resources.folder_16;
            if (Ext == "folderbig") return Resources.folder_48;

            if (Ext == "bmp") return Resources.bmp_16;
            if (Ext == "csv") return Resources.csv_16;
            if (Ext == "doc") return Resources.doc_16;
            if (Ext == "exe") return Resources.exe_16;
            if (Ext == "faq") return Resources.faq_16;
            if (Ext == "gz") return Resources.zip_16;
            if (Ext == "jpg") return Resources.jpg_16;
            if (Ext == "pdf") return Resources.pdf_16;
            if (Ext == "pic") return Resources.pic_16;
            if (Ext == "png") return Resources.png_16;
            if (Ext == "pps") return Resources.pps_16;
            if (Ext == "ppt") return Resources.ppt_16;
            if (Ext == "txt") return Resources.txt_16;
            if (Ext == "xls") return Resources.xls_16;
            if (Ext == "zip") return Resources.zip_16;
            if (Ext == "rar") return Resources.gz_16;

            return Resources.txt_16;
        }

        public static string GetDirectoryListingIconStr(string Ext)
        {
            if (Ext == "folder") return Ext;
            if (Ext == "folderbig") return Ext;

            if (Ext == "bmp") return Ext;
            if (Ext == "csv") return Ext;
            if (Ext == "doc") return Ext;
            if (Ext == "exe") return Ext;
            if (Ext == "faq") return Ext;
            if (Ext == "gz") return "zip";
            if (Ext == "jpg") return Ext;
            if (Ext == "pdf") return Ext;
            if (Ext == "pic") return Ext;
            if (Ext == "png") return Ext;
            if (Ext == "pps") return Ext;
            if (Ext == "ppt") return Ext;
            if (Ext == "txt") return Ext;
            if (Ext == "xls") return Ext;
            if (Ext == "zip") return Ext;
            if (Ext == "rar") return Ext;

            return "txt";
        }
    }
}
