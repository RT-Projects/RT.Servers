
namespace RT.Servers
{
    /// <summary>
    ///     <para>
    ///         This assembly provides a flexible, high-performance HTTP server (webserver).</para>
    ///     <para>
    ///         The main class to consider is <see cref="HttpServer"/>. Instantiate this class and call <see
    ///         cref="HttpServer.StartListening"/> to try it out:</para>
    ///     <code>
    ///         var server = new HttpServer(80);
    ///         server.StartListening(blocking: true);</code>
    ///     <para>
    ///         The number <c>80</c> in this example is the port number, and “blocking” means that the program will keep
    ///         running, allowing you to try out the webserver. Now direct your browser to <c>http://localhost</c> and you
    ///         will see a response. For obvious reasons, that response is a 404 Not Found error, since we haven’t taught the
    ///         server anything to do yet.</para>
    ///     <para>
    ///         What the server does is controlled by “handlers”. A handler is a function that accepts an <see
    ///         cref="HttpRequest"/> object — which contains all the information about the request received from the browser —
    ///         and returns an <see cref="HttpResponse"/> object. That response object tells the server everything it needs to
    ///         send to the browser, whether it be a webpage, a file, a redirect, a WebSocket connection or anything else.</para>
    ///     <para>
    ///         Let’s try something simple and just return some plain text:</para>
    ///     <code>
    ///         var server = new HttpServer(80);
    ///         server.Handler = req =&gt; HttpResponse.PlainText("Hello, World!");
    ///         server.StartListening(true);</code>
    ///     <para>
    ///         (From now on, we will omit the first and third line and concentrate on just the handlers.) Now if you reload
    ///         <c>http://localhost</c> in your browser, you will see the text “Hello, World!” rendered as a plain-text file.
    ///         Your handler worked!</para>
    ///     <para>
    ///         However, if you go to <c>http://localhost/foo</c> or <c>http://localhost/?bar=baz</c>, you will also see the
    ///         “Hello, World!” text. The handler we defined simply ignores the URL and returns the plain text no matter what.
    ///         In most cases, this is not what we want, so we need something that resolves URLs and calls different handlers
    ///         depending on the URL.</para>
    ///     <para>
    ///         This is what <see cref="UrlResolver"/> is for. You instantiate it, give it a bunch of URL fragments to work
    ///         with, and a handler for each:</para>
    ///     <code>
    ///         var resolver = new UrlResolver(
    ///             new UrlMapping(path: "/foo", handler: req =&gt; HttpResponse.PlainText("The /foo URL returns plain text.")),
    ///             new UrlMapping(path: "/bar", handler: req =&gt; HttpResponse.Redirect("/baz")),
    ///             new UrlMapping(path: "/baz", handler: req =&gt; HttpResponse.PlainText("The /bar URL redirects to /baz. Did you notice the URL in the location bar change?")),
    ///             new UrlMapping(handler: req =&gt; HttpResponse.Html("&lt;ul&gt;&lt;li&gt;&lt;a href='/foo'&gt;Go to /foo.&lt;/a&gt;&lt;/li&gt;&lt;li&gt;&lt;a href='/bar'&gt;Go to /bar.&lt;/a&gt;&lt;/li&gt;&lt;/ul&gt;"))
    ///         );
    ///         server.Handler = resolver.Handle;</code>
    ///     <para>
    ///         Now try these new URLs in your browser and you will see they behave exactly as you’d expect. You’ll also note
    ///         that <c>http://localhost/</c> shows the HTML from the last mapping.</para>
    ///     <para>
    ///         Now try something like <c>http://localhost/foo/quux</c>. You will find that the handler for <c>/foo</c> still
    ///         triggers and the plain-text response for <c>/foo</c> shows. This is because, by default, paths in a <see
    ///         cref="UrlMapping"/> are really path prefixes: every URL path that begins with <c>/foo/</c> matches, as well as
    ///         <c>/foo</c> itself (but not, for example, <c>/foobar</c>). This makes it super easy to process the URL in
    ///         stages. For example, you could have a wiki under <c>http://localhost/wiki/</c> and something else entirely
    ///         under <c>http://localhost/blog/</c> and they can each have their sub-URLs.</para>
    ///     <para>
    ///         It’s super easy to process the rest of the URL because the request object exposes it conveniently. Let’s
    ///         change the handlers so that they display the rest of the URL:</para>
    ///     <code>
    ///         new UrlMapping(path: "/foo", handler: req =&gt; HttpResponse.PlainText("The additional path after /foo is: " + req.Url.Path)),
    ///         new UrlMapping(path: "/bar", handler: req =&gt; HttpResponse.Redirect("/baz")),
    ///         new UrlMapping(path: "/baz", handler: req =&gt; HttpResponse.PlainText("The additional path after /baz is: " + req.Url.Path)),</code>
    ///     <para>
    ///         Now if you go to <c>http://localhost/foo/quux</c>, you will see that <see cref="HttpUrl.Path"/> contains only
    ///         <c>/quux</c> — the part that has already been resolved has been removed.</para>
    ///     <para>
    ///         If you go to <c>http://localhost/bar/quux</c>, of course it will still redirect to <c>/baz</c> without the
    ///         extra path. But there’s bigger problem with the redirect: imagine I nested your <c>UrlResolver</c> inside an
    ///         outer one so that I can put everything under a <c>/examples</c> URL, i.e. <c>/examples/foo</c>,
    ///         <c>/examples/bar</c> and <c>/examples/baz</c>. The above code will still redirect to <c>/baz</c> instead of
    ///         <c>/examples/baz</c>, which would be wrong. <see cref="HttpUrl"/> provides some powerful extension methods for
    ///         correct manipulation of URLs. Consider changing the second handler to something like this:</para>
    ///     <code>
    ///         new UrlMapping(path: "/bar", handler: req =&gt; HttpResponse.Redirect(req.Url.WithPathParent().WithPath("/baz"))),</code>
    ///     <para>
    ///         Now the URL will be changed correctly no matter how many URL resolvers have been involved. Similar extension
    ///         methods exist to change the query parameters, and all of the above can be done with subdomains instead of URL
    ///         paths as well.</para>
    ///     <para>
    ///         The <see cref="HttpUrl"/> object also provides convenient access to data submitted through an HTML form and
    ///         files uploaded via an <c>&lt;input type='file'&gt;</c> element.</para>
    ///     <para>
    ///         You can also use a redirect in the main handler to ensure that your users always use HTTPS (encrypted
    ///         traffic):</para>
    ///     <code>
    ///         var options = new HttpServerOptions();
    ///         options.AddEndpoint("http", null, 80, secure: false);
    ///         options.AddEndpoint("https", null, 443, secure: true);
    ///         options.CertificatePath = @"...";   // path to HTTPS certificate file
    ///         var server = new HttpServer(options);
    ///         var resolver = new UrlResolver(
    ///             // [...]
    ///         );
    ///         server.Handler = req =&gt; req.Url.Https ? resolver.Handle(req) : HttpResponse.Redirect(req.Url.WithHttps(true));</code>
    ///     <para>
    ///         Finally, go to <c>http://localhost/blah</c>. You will notice that the last handler — the one that returns the
    ///         HTML — still triggers for any URL that isn’t already covered by a more specific handler. We usually don’t want
    ///         this. We can limit a <c>UrlMapping</c> by setting <c>specificPath</c> to <c>true</c>:</para>
    ///     <code>
    ///         new UrlMapping(path: "/", specificPath: true, handler: req =&gt; HttpResponse.Html("&lt;ul&gt;&lt;li&gt;&lt;a href='/foo'&gt;Go to /foo.&lt;/a&gt;&lt;/li&gt;&lt;li&gt;&lt;a href='/bar'&gt;Go to /bar.&lt;/a&gt;&lt;/li&gt;&lt;/ul&gt;"))</code>
    ///     <para>
    ///         Now the HTML is returned only at the root URL, while all other URLs go back to returning 404 errors.</para>
    ///     <para>
    ///         Generating the page HTML as a single huge string is tedious, error-prone, inextensible and has poor
    ///         performance. For this reason, <see cref="HttpResponse"/> also accepts a <see cref="RT.TagSoup.Tag"/> object
    ///         from the related <c>RT.TagSoup</c> library. This allows you to write HTML in very natural C# syntax:</para>
    ///     <code>
    ///         new UrlMapping(path: "/", specificPath: true, handler: req =&gt; HttpResponse.Html(
    ///             new UL(
    ///                 new LI(new A { href = req.Url.WithPath("/foo").ToHref() }._("Go to /foo.")),
    ///                 new LI(new A { href = req.Url.WithPath("/bar").ToHref() }._("Go to /bar.")))))</code>
    ///     <para>
    ///         The short time necessary to get used to this syntax is more than worth it once you consider the great
    ///         advantages of TagSoup:</para>
    ///     <list type="bullet">
    ///         <item><description>
    ///             <para>
    ///                 You can fluidly and intuitively insert variables, expressions and method calls and you never need to
    ///                 worry about HTML escaping. Everything you pass into a tag or attribute value is automatically escaped
    ///                 correctly.</para></description></item>
    ///         <item><description>
    ///             <para>
    ///                 All of the HTML is evaluated as lazily as possible. To demonstrate this, let’s output a filtered list:</para>
    ///             <code>
    ///                 new UrlMapping(path: "/", specificPath: true, handler: req =&gt; HttpResponse.Html(
    ///                     new UL(myList.Where(item =&gt; someCondition(item)).Select(item =&gt; new LI(item)))))</code>
    ///             <para>
    ///                 Due to the lazy nature of <c>.Where()</c> and <c>.Select()</c>, the list is processed and the HTML
    ///                 generated incrementally. The server never generates a single humongous string in memory containing the
    ///                 entire page; it sends the HTML to the browser as efficiently as possible.</para></description></item>
    ///         <item><description>
    ///             <para>
    ///                 The server starts responding to the browser while the page is still processing, thus giving a
    ///                 subjective impression of improved performance.</para></description></item>
    ///         <item><description>
    ///             You can write your own methods that generate HTML incrementally by declaring them as
    ///             <c>IEnumerable&lt;object&gt;</c> and returning pieces of HTML using <c>yield return</c>. Calling such a
    ///             method inside a TagSoup tree causes that method to be evaluated just as lazily as the above.</description></item></list>
    ///     <para>
    ///         Apart from plain text and HTML (and other text-based items such as CSS and JavaScript), HttpServer can deal
    ///         efficiently with files using <see cref="HttpResponse.File"/>. For example:</para>
    ///     <code>
    ///         new UrlMapping(path: "/image", handler: req =&gt; HttpResponse.File(@"C:\images\image.jpg", "image/jpeg", ifModifiedSince: req.Headers.IfModifiedSince)),
    ///         new UrlMapping(path: "/zipfile", handler: req =&gt; HttpResponse.File(@"C:\zipfiles\zipfile.zip"))</code>
    ///     <para>
    ///         The first example demonstrates how you specify the Content-Type header so that the browser knows it is
    ///         receiving an image. HttpServer automatically sends headers that allow the browser to cache the file and later
    ///         ask whether the file has changed. If the file has not changed, HttpServer can respond with a “304 Not
    ///         Modified” response instead of sending the file again.</para>
    ///     <para>
    ///         In the second example, we omit the Content-Type, which defaults to <c>"application/octet-stream"</c> for
    ///         binary files and <c>"text/plain; charset=utf-8"</c> for text files. In our case, the file is a ZIP file which
    ///         could be large, maybe gigabytes. HttpServer will correctly handle large files and not attempt to load the
    ///         entire file into memory. It correctly supports download resuming and segmented downloading, allowing the use
    ///         of download managers to download large files. You need to do nothing special on your end to enable all of
    ///         these features.</para>
    ///     <para>
    ///         With <see cref="FileSystemHandler"/> you can also serve entire directories from the local file system and have
    ///         HttpServer automatically resolve paths to folders. The following example will provide full access to every
    ///         file under <c>C:\DataFiles</c>, including listing the contents of directories. To forbid the listing of
    ///         directory contents, pass an extra <see cref="FileSystemOptions"/> object to the constructor.</para>
    ///     <code>
    ///         new UrlMapping(path: "/files", handler: new FileSystemHandler(@"C:\DataFiles").Handle)</code>
    ///     <para>
    ///         Finally, a response to a request can be a WebSocket connection. Implement the abstract <see cref="WebSocket"/>
    ///         class to implement your WebSocket server behavior, then return a response as follows, replacing
    ///         <c>MyWebSocket</c> with the name of your derived type:</para>
    ///     <code>
    ///         new UrlMapping(path: "/socket", handler: req =&gt; HttpResponse.WebSocket(new MyWebSocket()))</code>
    ///     <para>
    ///         If you wish to customize the look of error pages consistently across your site, use <see
    ///         cref="HttpServer.ErrorHandler"/> and/or <see cref="HttpServer.ResponseExceptionHandler"/>. You can also use
    ///         these to log error conditions to a database and/or send e-mails to administrators.</para>
    ///     <para>
    ///         <see cref="AjaxHandler{TApi}"/> provides a flexible and easy-to-use means of providing methods that can be
    ///         invoked by client-side JavaScript code via AJAX. Simply declare a class containing the permissible AJAX calls
    ///         as methods decorated with <see cref="AjaxMethodAttribute"/> and use that class for <c>TApi</c>.</para>
    ///     <para>
    ///         The <see cref="Session"/> class provides a means for user sessions, i.e. server-side data that persists
    ///         between requests (by using a cookie, which the server takes care of fully automatically). See <see
    ///         cref="Session"/> for details of how this is used. <see cref="FileSession"/> is a concrete implementation of
    ///         the abstract <see cref="Session"/> class which stores the session data in local files, but you can also derive
    ///         your own to store the session in a database or some other way.</para>
    ///     <para>
    ///         Once you have sessions, you can use <see cref="Authenticator"/> for a rudimentary login system. Again, <see
    ///         cref="FileAuthenticator"/> is an example derived class which takes the username/password information from a
    ///         local file, but you can derive your own to retrieve the information from a database or some other way.</para></summary>
    sealed class AssemblyDocumentation { }
}
