using System;
using RT.Util.ExtensionMethods;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using RT.Util.Streams;
using RT.Util;

namespace RT.Servers
{
    /// <summary>Provides a handler for <see cref="HttpServer"/> that can return files and list the contents of directories on the local file system.</summary>
    public class FileSystemHandler
    {
        private static FileSystemOptions _defaultOptions;
        /// <summary>Specifies the default options to fall back to whenever an instance of this class has no options of its own.</summary>
        public static FileSystemOptions DefaultOptions
        {
            get
            {
                if (_defaultOptions == null)
                    _defaultOptions = new FileSystemOptions();
                return _defaultOptions;
            }
        }

        /// <summary>Specifies the options associated with this instance, or null to use the <see cref="DefaultOptions"/>.</summary>
        public FileSystemOptions Options { get; set; }
        /// <summary>Specifies the directory in the local file system from which files are served and directory contents are listed.</summary>
        public string BaseDirectory
        {
            get { return _baseDirectory; }
            set
            {
                if (!value.StartsWith(@"\\") &&
                    !(value.StartsWith(@"/") && Environment.OSVersion.Platform != PlatformID.Win32NT) &&
                    !(value.Length >= 3 && value[1] == ':' && (value[2] == '\\' || value[2] == '/')))
                    throw new Exception("BaseDirectory must be absolute.");
                _baseDirectory = value;
            }
        }
        private string _baseDirectory;

        /// <summary>Initializes a new instance of <see cref="FileSystemHandler"/>.</summary>
        /// <param name="baseDir">Specifies the directory in the local file system from which files are served and directory contents are listed.</param>
        /// <param name="options">Specifies the options associated with this instance, or null to use the <see cref="DefaultOptions"/>.</param>
        public FileSystemHandler(string baseDir, FileSystemOptions options = null)
        {
            Options = options;
            BaseDirectory = baseDir;
        }

        /// <summary>Returns an <see cref="HttpResponse"/> that handles the specified request, either by delivering a file from the local file system,
        /// or by listing the contents of a directory in the local file system. The file or directory served is determined from the configured
        /// <see cref="BaseDirectory"/> and the <see cref="HttpRequest.Url"/> of the specified <paramref name="request"/>.</summary>
        /// <param name="request">HTTP request from the client.</param>
        /// <returns>An <see cref="HttpResponse"/> encapsulating the file transfer or directory listing.</returns>
        public HttpResponse Handle(HttpRequest request)
        {
            if (request.Url.Path == "/$/directory-listing/xsl")
                return HttpResponse.Create(new MemoryStream(DirectoryListingXsl), "application/xml; charset=utf-8");

            if (request.Url.Path.StartsWith("/$/directory-listing/icons/" /* watch out for the hardcoded length below */))
                return HttpResponse.Create(new MemoryStream(GetDirectoryListingIcon(request.Url.Path.Substring(27))), "image/png");

            if (request.Url.Path.StartsWith("/$/"))
                throw new HttpNotFoundException();

            string p = BaseDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()) ? BaseDirectory.Remove(BaseDirectory.Length - 1) : BaseDirectory;
            string[] urlPieces = request.Url.Path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string soFar = "";
            string soFarUrl = "";
            for (int i = 0; i < urlPieces.Length; i++)
            {
                string piece = urlPieces[i].UrlUnescape();
                if (piece == "..")
                    throw new HttpException(HttpStatusCode._403_Forbidden);
                string nextSoFar = soFar + Path.DirectorySeparatorChar + piece;
                string curPath = p + nextSoFar;

                if (File.Exists(curPath))
                {
                    DirectoryInfo parentDir = new DirectoryInfo(p + soFar);
                    foreach (var fileInf in parentDir.GetFiles(piece))
                    {
                        soFarUrl += "/" + fileInf.Name.UrlEscape();
                        break;
                    }

                    if (request.Url.Path != soFarUrl)
                        return HttpResponse.Redirect(request.Url.WithPath(soFarUrl));

                    var opts = Options ?? DefaultOptions;
                    return HttpResponse.File(curPath, opts.GetMimeType(curPath), opts.MaxAge, request.Headers.IfModifiedSince);
                }
                else if (Directory.Exists(curPath))
                {
                    DirectoryInfo parentDir = new DirectoryInfo(p + soFar);
                    foreach (var dirInfo in parentDir.GetDirectories(piece))
                    {
                        soFarUrl += "/" + dirInfo.Name.UrlEscape();
                        break;
                    }
                }
                else
                {
                    throw new HttpNotFoundException(request.Url.WithPathOnly(soFarUrl + "/" + piece).ToHref());
                }
                soFar = nextSoFar;
            }

            // If this point is reached, it’s a directory
            if (request.Url.Path != soFarUrl + "/")
                return HttpResponse.Redirect(request.Url.WithPath(soFarUrl + "/"));

            var style = (Options ?? DefaultOptions).DirectoryListingStyle;
            switch (style)
            {
                case DirectoryListingStyle.Forbidden:
                    throw new HttpException(HttpStatusCode._401_Unauthorized);
                case DirectoryListingStyle.XmlPlusXsl:
                    if (!Directory.Exists(p + soFar))
                        throw new FileNotFoundException("Directory does not exist.", p + soFar);
                    return HttpResponse.Create(generateDirectoryXml(p + soFar, request.Url, soFarUrl + "/"), "application/xml; charset=utf-8");
                default:
                    throw new InvalidOperationException("Invalid directory listing style: " + (int) style);
            }
        }

        private static IEnumerable<string> generateDirectoryXml(string localPath, IHttpUrl url, string urlPath)
        {
            List<DirectoryInfo> dirs = new List<DirectoryInfo>();
            List<FileInfo> files = new List<FileInfo>();
            DirectoryInfo dirInfo = new DirectoryInfo(localPath);
            foreach (var d in dirInfo.GetDirectories())
                dirs.Add(d);
            foreach (var f in dirInfo.GetFiles())
                files.Add(f);
            dirs.Sort((a, b) => a.Name.CompareTo(b.Name));
            files.Sort((a, b) => a.Name.CompareTo(b.Name));

            yield return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n";
            yield return "<?xml-stylesheet href=\"{0}\" type=\"text/xsl\" ?>\n".Fmt(url.WithPathOnly("/$/directory-listing/xsl").ToHref().HtmlEscape());
            yield return "<directory url=\"{0}\" unescapedurl=\"{1}\" img=\"{2}\" numdirs=\"{3}\" numfiles=\"{4}\">\n"
                .Fmt(url.ToHref().HtmlEscape(), url.ToHref().UrlUnescape().HtmlEscape(), url.WithPathOnly("/$/directory-listing/icons/folderbig").ToHref().HtmlEscape(), dirs.Count, files.Count);
            foreach (var d in dirs)
                yield return "  <dir link=\"{0}/\" img=\"{2}\">{1}</dir>\n".Fmt(d.Name.UrlEscape(), d.Name.HtmlEscape(), url.WithPathOnly("/$/directory-listing/icons/folder").ToHref().HtmlEscape());
            foreach (var f in files)
            {
                string extension = f.Name.Contains('.') ? f.Name.Substring(f.Name.LastIndexOf('.') + 1) : "";
                yield return "  <file link=\"{0}\" size=\"{1}\" nicesize=\"{2}\" img=\"{3}\">{4}</file>\n"
                    .Fmt(f.Name.UrlEscape(), f.Length, Ut.SizeToString(f.Length), url.WithPathOnly("/$/directory-listing/icons/" + GetDirectoryListingIconStr(extension)).ToHref().HtmlEscape(), f.Name.HtmlEscape());
            }

            yield return "</directory>\n";
        }

        /// <summary>
        /// XSL to use for directory listings. This will be converted to UTF-8, whitespace-optimised and cached before being output.
        /// This is the file that is returned at the URL /$/directory-listing/xsl.
        /// </summary>
        private static string _directoryListingXslString = @"<?xml version=""1.0"" encoding=""UTF-8""?>

            <xsl:stylesheet version=""1.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"" xmlns=""http://www.w3.org/1999/xhtml"">
                <xsl:output method=""html""/>
                <xsl:template match=""/directory"">
                    <html>
                    <head>
                        <title>Directory listing</title>
                        <style type=""text/css"">
                            body { margin: 10pt; font-family: ""Verdana"", sans-serif; font-size: 10pt; }
                            table { border-collapse: collapse; }
                            .line { border-top: 2px solid #339; }
                            th { padding: 3pt 0; }
                            .filename { padding-right: 5pt; border-right: 1px solid #ccc; }
                            .size { padding: 0 5pt; }
                            .size.dir { text-align: center; font-style: italic; }
                            .size.file { text-align: right; }
                            .filerow:hover { background: #eef; }
                            .filerow .filename a { display: block; }
                            .summary { font-size: 80%; font-style: italic; color: #888; }
                        </style>
                    </head>
                    <body>
                        <table>
                            <tr class=""line"">
                                <table>
                                    <tr>
                                        <td rowspan=""2""><img src=""{@img}"" alt=""folder""/></td>
                                        <td style=""font-size: 180%"">Directory listing</td>
                                    </tr>
                                    <tr>
                                        <td><span title=""{@url}""><xsl:value-of select=""@unescapedurl""/></span></td>
                                    </tr>
                                </table>
                            </tr>

                            <tr class=""line""><td colspan=""2"">
                                <table style=""width: 100%"">
                                    <tr><th class=""filename"" colspan=""2"">Name</th><th>Size</th></tr>
                                    <xsl:if test=""@url!='/'"">
                                        <tr class=""filerow""><td></td><td class=""filename""><a href="".."">..</a></td><td class=""size dir"">Folder</td></tr>
                                    </xsl:if>
                                    <xsl:apply-templates select=""dir"" />
                                    <xsl:apply-templates select=""file"" />
                                    <tr style=""height: 3pt""><td colspan=""2""/><td/></tr>
                                </table>
                            </td></tr>

                            <tr class=""line summary"">
                                <td colspan=""2"">Folder contains <xsl:value-of select=""@numdirs""/> sub-folders and <xsl:value-of select=""@numfiles""/> files.</td>
                            </tr>
                        </table>
                    </body>
                    </html>
                </xsl:template>

                <xsl:template match=""dir"">
                    <tr class=""filerow"">
                        <td><img src=""{@img}"" alt=""folder""/></td>
                        <td class=""filename""><a href=""{@link}""><xsl:value-of select="".""/></a></td>
                        <td class=""size dir"">Folder</td>
                    </tr>
                </xsl:template>

                <xsl:template match=""file"">
                    <tr class=""filerow"">
                        <td><img src=""{@img}"" alt=""file""/></td>
                        <td class=""filename""><a href=""{@link}""><xsl:value-of select="".""/></a></td>
                        <td class=""size file""><xsl:value-of select=""@nicesize""/></td>
                    </tr>
                </xsl:template>

            </xsl:stylesheet>
        ";

        /// <summary>
        /// Caches the UTF-8-encoded version of the directory-listing XSL file.
        /// </summary>
        private static byte[] _directoryListingXslByteArray = null;

        /// <summary>Returns a byte array containing the UTF-8-encoded directory-listing XSL.</summary>
        /// <returns>A byte array containing the UTF-8-encoded directory-listing XSL.</returns>
        private static byte[] DirectoryListingXsl
        {
            get
            {
                if (_directoryListingXslByteArray == null)
                {
                    _directoryListingXslByteArray = _directoryListingXslString.ToUtf8();
                    _directoryListingXslString = null; // free some memory?
                }
                return _directoryListingXslByteArray;
            }
        }

        /// <summary>
        /// Returns a byte array representing an icon in PNG format that corresponds to the specified file extension.
        /// </summary>
        /// <param name="extension">The file extension for which to retrieve an icon in PNG format.</param>
        /// <returns>A byte array representing an icon in PNG format that corresponds to the specified file extension.</returns>
        private static byte[] GetDirectoryListingIcon(string extension)
        {
            if (extension == "folder") return Resources.folder_16;
            if (extension == "folderbig") return Resources.folder_48;

            if (extension == "bmp") return Resources.bmp_16;
            if (extension == "csv") return Resources.csv_16;
            if (extension == "doc") return Resources.doc_16;
            if (extension == "exe") return Resources.exe_16;
            if (extension == "faq") return Resources.faq_16;
            if (extension == "gz") return Resources.zip_16;
            if (extension == "jpg") return Resources.jpg_16;
            if (extension == "pdf") return Resources.pdf_16;
            if (extension == "pic") return Resources.pic_16;
            if (extension == "png") return Resources.png_16;
            if (extension == "pps") return Resources.pps_16;
            if (extension == "ppt") return Resources.ppt_16;
            if (extension == "txt") return Resources.txt_16;
            if (extension == "xls") return Resources.xls_16;
            if (extension == "zip") return Resources.zip_16;
            if (extension == "rar") return Resources.gz_16;

            return Resources.txt_16;
        }

        /// <summary>
        /// Returns a file extension whose icon is used in directory listings to represent files of the specified file extension.
        /// </summary>
        /// <param name="extension">The extension of the actual file for which to display an icon.</param>
        /// <returns>The file extension whose icon is used in directory listings to represent files of the specified file extension.</returns>
        private static string GetDirectoryListingIconStr(string extension)
        {
            if (extension == "folder") return extension;
            if (extension == "folderbig") return extension;

            if (extension == "bmp") return extension;
            if (extension == "csv") return extension;
            if (extension == "doc") return extension;
            if (extension == "exe") return extension;
            if (extension == "faq") return extension;
            if (extension == "gz") return "zip";
            if (extension == "jpg") return extension;
            if (extension == "pdf") return extension;
            if (extension == "pic") return extension;
            if (extension == "png") return extension;
            if (extension == "pps") return extension;
            if (extension == "ppt") return extension;
            if (extension == "txt") return extension;
            if (extension == "xls") return extension;
            if (extension == "zip") return extension;
            if (extension == "rar") return extension;

            return "txt";
        }
    }
}
