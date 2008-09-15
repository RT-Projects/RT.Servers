using System.IO;
using System.Text;
using System.Xml;

namespace Servers
{
    public static class HTTPInternalObjects
    {
        private static string DirectoryListingXSLString = @"<?xml version=""1.0"" encoding=""UTF-8""?>

            <xsl:stylesheet version=""1.0""
                xmlns:xsl=""http://www.w3.org/1999/XSL/Transform""
                xmlns=""http://www.w3.org/1999/xhtml"">

                <xsl:output method=""html""/>

                <xsl:template match=""/directory"">
                    <html>
                    <head>
                        <title>Directory listing</title>
                        <style type=""text/css"">" +
                            @"body{margin:10pt;font-family:""Verdana"",sans-serif;font-size:10pt}" +
                            @"table{border-collapse:collapse}" +
                            @".l{border-top:2px solid #339}" +
                            @".h th{padding:3pt 0}" +
                            @".n{padding-right:5pt;border-right:1px solid #ccc}" +
                            @".s{padding:0 5pt}" +
                            @".s.d{text-align:center;font-style:italic}" +
                            @".s.f{text-align:right}" +
                            @".r:hover{background:#eef}" +
                            @".r .n a{display:block}" +
                            @".t{font-size:80%;font-style:italic;color:#888}" +
                        @"</style>
                    </head>
                    <body>
                        <table>
                            <tr class=""l"">
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

                            <tr class=""l""><td colspan=""2"">
                                <table style=""width: 100%"">
                                    <tr class=""h""><th class=""n"" colspan=""2"">Name</th><th>Size</th></tr>
                                    <tr><td></td><td class=""n""><a href=""{@url}/.."">..</a></td><td class=""s d"">Folder</td></tr>
                                    <xsl:apply-templates select=""dir"" />
                                    <xsl:apply-templates select=""file"" />
                                    <tr style=""height: 3pt""><td class=""n"" colspan=""2""/><td/></tr>
                                </table>
                            </td></tr>

                            <tr class=""l t"">
                                <td colspan=""2"">Folder contains <xsl:value-of select=""@numdirs""/> sub-folders and <xsl:value-of select=""@numfiles""/> files.</td>
                            </tr>
                        </table>
                    </body>
                    </html>
                </xsl:template>

                <xsl:template match=""dir"">
                    <tr>
                        <td><img src=""{@img}"" alt=""folder""/></td>
                        <td class=""n""><a href=""{@link}""><xsl:value-of select="".""/></a></td>
                        <td class=""s d"">Folder</td>
                    </tr>
                </xsl:template>

                <xsl:template match=""file"">
                    <tr class=""r"">
                        <td><img src=""{@img}"" alt=""file""/></td>
                        <td class=""n"" style=""min-width: 150pt;""><a href=""{@link}""><xsl:value-of select="".""/></a></td>
                        <td class=""s f""><xsl:value-of select=""@nicesize""/></td>
                    </tr>
                </xsl:template>

            </xsl:stylesheet>
        ";
        private static byte[] DirectoryListingXSLByteArray = null;

        public static byte[] DirectoryListingXSL()
        {
            if (DirectoryListingXSLByteArray != null)
                return DirectoryListingXSLByteArray;

            // This removes all the unnecessary whitespace from the XML and outputs it as UTF-8
            XmlDocument x = new XmlDocument();
            x.LoadXml(DirectoryListingXSLString);
            DirectoryListingXSLString = null; // free some memory?
            using (MemoryStream m = new MemoryStream())
            {
                using (XmlWriter w = new XmlTextWriter(m, Encoding.UTF8))
                {
                    x.WriteTo(w);
                    w.Close();
                }
                m.Close();
                DirectoryListingXSLByteArray = m.ToArray();
            }
            return DirectoryListingXSLByteArray;
        }

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
