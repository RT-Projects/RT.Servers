using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RT.Json;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    ///     Provides a handler for <see cref="HttpServer"/> that can return files and list the contents of directories on the
    ///     local file system.</summary>
    /// <remarks>
    ///     <para>
    ///         The behavior of <see cref="FileSystemHandler"/> can be configured by placing a file called
    ///         <c>folder_config.json</c> in a folder. The following options are supported:</para>
    ///     <list type="bullet">
    ///         <item><term>
    ///             <c>redirect_to</c> (string)</term>
    ///         <description>
    ///             If the user accesses the folder itself directly, HttpServer will redirect the user to the specified
    ///             sub-URL. This effectively prevents the user from seeing a directory listing even if this would otherwise
    ///             be allowed. The value must be a non-empty string, not begin with <c>/</c> and not contain a <c>..</c> path
    ///             segment.</description></item>
    ///         <item><term>
    ///             <c>wildcards</c> (bool)</term>
    ///         <description>
    ///             Specifies whether the user may access files using wildcards (for example, <c>/foo*.txt</c> will access the
    ///             first file in the folder that matches this pattern). If not specified, the default is <c>false</c> if <see
    ///             cref="FileSystemOptions.DirectoryListingStyle"/> is <see cref="DirectoryListingStyle.Forbidden"/> and
    ///             <c>true</c> otherwise. If specified, it applies recursively to all subfolders (except those that override
    ///             it again).</description></item></list></remarks>
    public class FileSystemHandler
    {
        private static FileSystemOptions _defaultOptions;
        /// <summary>
        ///     Specifies the default options to fall back to whenever an instance of this class has no options of its own.</summary>
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
        /// <summary>
        ///     Specifies the directory in the local file system from which files are served and directory contents are
        ///     listed.</summary>
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

        /// <summary>
        ///     Initializes a new instance of <see cref="FileSystemHandler"/>.</summary>
        /// <param name="baseDir">
        ///     Specifies the directory in the local file system from which files are served and directory contents are
        ///     listed.</param>
        /// <param name="options">
        ///     Specifies the options associated with this instance, or null to use the <see cref="DefaultOptions"/>.</param>
        public FileSystemHandler(string baseDir, FileSystemOptions options = null)
        {
            Options = options;
            BaseDirectory = baseDir;
        }

        private HttpResponse HandleHeaderProcessor(FileSystemResponseType type, HttpResponse response)
        {
            if (Options != null && Options.ResponseHeaderProcessor != null)
                Options.ResponseHeaderProcessor(response.Headers, type);
            return response;
        }

        /// <summary>
        ///     Returns an <see cref="HttpResponse"/> that handles the specified request, either by delivering a file from the
        ///     local file system, or by listing the contents of a directory in the local file system. The file or directory
        ///     served is determined from the configured <see cref="BaseDirectory"/> and the <see cref="HttpRequest.Url"/> of
        ///     the specified <paramref name="request"/>.</summary>
        /// <param name="request">
        ///     HTTP request from the client.</param>
        /// <returns>
        ///     An <see cref="HttpResponse"/> encapsulating the file transfer or directory listing.</returns>
        public HttpResponse Handle(HttpRequest request)
        {
            if (request.Url.Path == "/$/directory-listing/xsl")
                return HandleHeaderProcessor(FileSystemResponseType.Internal, HttpResponse.Create(new MemoryStream(DirectoryListingXsl), "application/xml; charset=utf-8"));

            if (request.Url.Path.StartsWith("/$/directory-listing/icons/" /* watch out for the hardcoded length below */))
                return HandleHeaderProcessor(FileSystemResponseType.Internal, HttpResponse.Create(new MemoryStream(GetDirectoryListingIcon(request.Url.Path.Substring(27))), "image/png"));

            if (request.Url.Path.StartsWith("/$/"))
                throw new HttpNotFoundException();

            var dirStyle = (Options ?? DefaultOptions).DirectoryListingStyle;
            var dirAuth = (Options ?? DefaultOptions).DirectoryListingAuth;
            var allowWildcards = dirStyle != DirectoryListingStyle.Forbidden;
            JsonDict lastConfig = null;
            string p = BaseDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()) ? BaseDirectory.Remove(BaseDirectory.Length - 1) : BaseDirectory;
            string[] urlPieces = request.Url.Path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string soFar = "";
            string soFarUrl = "";

            void processConfig()
            {
                var configFile = Path.Combine(p + soFar, "folder_config.json");
                if (File.Exists(configFile) && JsonDict.TryParse(File.ReadAllText(configFile), out var cfg))
                {
                    if (cfg.ContainsKey("wildcards") && cfg["wildcards"].GetBoolSafe() is bool wildcards)
                        allowWildcards = wildcards;
                    lastConfig = cfg;
                }
            }
            processConfig();

            for (int i = 0; i < urlPieces.Length; i++)
            {
                lastConfig = null;
                string piece = urlPieces[i].UrlUnescape();
                if (piece == "..")
                    throw new HttpException(HttpStatusCode._403_Forbidden);

                foreach (var suitablePiece in getCandidates(piece))
                {
                    string nextSoFar = soFar + Path.DirectorySeparatorChar + suitablePiece;
                    string curPath = p + nextSoFar;

                    if (allowWildcards && !File.Exists(curPath) && !Directory.Exists(curPath) && curPath.Contains('*') && (dirAuth == null || dirAuth(request) == null))
                        curPath = new DirectoryInfo(p + soFar).GetFileSystemInfos(suitablePiece).Select(fs => fs.FullName).FirstOrDefault() ?? curPath;

                    if (File.Exists(curPath))
                    {
                        soFarUrl += "/" + new DirectoryInfo(p + soFar).GetFiles(suitablePiece)[0].Name.UrlEscape();

                        if (request.Url.Path != soFarUrl)
                            return HandleHeaderProcessor(FileSystemResponseType.Redirect, HttpResponse.Redirect(request.Url.WithPath(soFarUrl)));

                        var opts = Options ?? DefaultOptions;
                        return HandleHeaderProcessor(FileSystemResponseType.File, HttpResponse.File(curPath, opts.GetMimeType(curPath), opts.MaxAge, request.Headers.IfModifiedSince));
                    }
                    else if (Directory.Exists(curPath))
                    {
                        soFarUrl += "/" + new DirectoryInfo(p + soFar).GetDirectories(suitablePiece)[0].Name.UrlEscape();
                        soFar = nextSoFar;
                        processConfig();
                        goto foundOne;
                    }
                }

                // The specified piece is neither a file nor a directory.
                throw new HttpNotFoundException(request.Url.WithPathOnly(soFarUrl + "/" + piece).ToHref());

                foundOne:;
            }

            // If this point is reached, it’s a directory.

            if (lastConfig != null && lastConfig.ContainsKey("redirect_to")
                    && lastConfig["redirect_to"].GetStringSafe() is string redirectTo
                    && redirectTo.Length > 0 && !redirectTo.StartsWith("/") && !redirectTo.Contains("/..") && !redirectTo.Contains("../") && redirectTo.Trim() != "..")
                return HandleHeaderProcessor(FileSystemResponseType.Redirect, HttpResponse.Redirect(request.Url.WithPath(soFarUrl + "/" + redirectTo)));

            if (request.Url.Path != soFarUrl + "/")
                return HandleHeaderProcessor(FileSystemResponseType.Redirect, HttpResponse.Redirect(request.Url.WithPath(soFarUrl + "/")));

            switch (dirStyle)
            {
                case DirectoryListingStyle.Forbidden:
                    throw new HttpException(HttpStatusCode._401_Unauthorized);
                case DirectoryListingStyle.XmlPlusXsl:
                    var auth = (Options ?? DefaultOptions).DirectoryListingAuth;
                    if (auth != null)
                    {
                        var response = auth(request);
                        if (response != null)
                            return response;
                    }
                    if (!Directory.Exists(p + soFar))
                        throw new FileNotFoundException("Directory does not exist.", p + soFar);
                    return HandleHeaderProcessor(FileSystemResponseType.Directory, HttpResponse.Create(generateDirectoryXml(p + soFar, request.Url), "application/xml; charset=utf-8"));
                default:
                    throw new InvalidOperationException("Invalid directory listing style: " + (int) dirStyle);
            }
        }

        /// <summary>
        ///     Generates candidate variant filenames. For example, if the user is looking for a <c>.htm</c> file but an
        ///     otherwise equivalent <c>.html</c> file exists, <see cref="FileSystemHandler"/> returns an appropriate redirect
        ///     (assuming directory listings are allowed). This method does not take care of the <c>*</c> wildcard (that
        ///     happens in <see cref="Handle(HttpRequest)"/>).</summary>
        private IEnumerable<string> getCandidates(string piece)
        {
            yield return piece;

            var candidates = new List<string> { piece };

            // WARNING: Code further down assumes that this only modifies the end of ‘piece’ in such a way that it cannot add or remove apostrophes or shift their indices.
            if (piece.EndsWith(".htm"))
                candidates.Add(piece + "l");
            else if (piece.EndsWith(".html"))
                candidates.Add(piece.Remove(piece.Length - 1));
            else if (piece.EndsWith(".jpg"))
                candidates.Add(piece.Insert(piece.Length - 1, "e"));
            else if (piece.EndsWith(".jpeg"))
                candidates.Add(piece.Remove(piece.Length - 2, 1));

            // Tolerate apostrophe variations (' U+0027 vs. ’ U+2019), but only up to a point
            if (!(piece.Contains('\'') || piece.Contains('’')) || piece.Count(ch => ch == '\'' || ch == '’') > 8)
            {
                if (candidates.Count == 1)
                    yield break;
                foreach (var candidate in candidates.Skip(1))
                    yield return candidate;
            }
            else
            {
                // WARNING: This relies on the assumption mentioned above.
                var apos = piece.SelectIndexWhere(ch => ch == '\'' || ch == '’').ToArray();
                foreach (var candidate in candidates)
                    for (var bits = 0; bits < 1 << apos.Length; bits++)
                    {
                        var pi = candidate;
                        for (var n = 0; n < apos.Length; n++)
                            pi = pi.Remove(apos[n], 1).Insert(apos[n], (bits & (1 << n)) != 0 ? "’" : "'");
                        yield return pi;
                    }
            }
        }

        private static IEnumerable<string> generateDirectoryXml(string localPath, IHttpUrl url)
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
        ///     XSL to use for directory listings. This will be converted to UTF-8, whitespace-optimised and cached before
        ///     being output. This is the file that is returned at the URL /$/directory-listing/xsl.</summary>
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

        /// <summary>Caches the UTF-8-encoded version of the directory-listing XSL file.</summary>
        private static byte[] _directoryListingXslByteArray = null;

        /// <summary>
        ///     Returns a byte array containing the UTF-8-encoded directory-listing XSL.</summary>
        /// <returns>
        ///     A byte array containing the UTF-8-encoded directory-listing XSL.</returns>
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
        ///     Returns a byte array representing an icon in PNG format that corresponds to the specified file extension.</summary>
        /// <param name="extension">
        ///     The file extension for which to retrieve an icon in PNG format.</param>
        /// <returns>
        ///     A byte array representing an icon in PNG format that corresponds to the specified file extension.</returns>
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
        ///     Returns a file extension whose icon is used in directory listings to represent files of the specified file
        ///     extension.</summary>
        /// <param name="extension">
        ///     The extension of the actual file for which to display an icon.</param>
        /// <returns>
        ///     The file extension whose icon is used in directory listings to represent files of the specified file
        ///     extension.</returns>
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
