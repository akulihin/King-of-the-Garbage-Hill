using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.LocalPersistentData.ServerAccounts;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace King_of_the_Garbage_Hill.API;

public class ApiHandling : IServiceSingleton
{
    private HttpListener _listener;
    private const string Url = "http://localhost:8000/";
    private int _pageViews;
    private int _requestCount;

    public string PageData =
        "<!DOCTYPE>" +
        "<html>" +
        "  <head>" +
        "    <title>HttpListener Example</title>" +
        "  </head>" +
        "  <body>" +
        "    <p>Page Views: {0}</p>" +
        "    <form method=\"post\" action=\"shutdown\">" +
        "      <input type=\"submit\" value=\"Shutdown\" {1}>" +
        "    </form>" +
        "  </body>" +
        "</html>";

    private readonly Global _global;
    private readonly LoginFromConsole _log;
    private readonly ServerAccounts _server;

    public ApiHandling(Global global, LoginFromConsole log, ServerAccounts server)
    {
        _global = global;
        _log = log;
        _server = server;
        
    }

    public Task InitializeAsync()
    {
        ApiTest();
        return Task.CompletedTask;
    }

    public async Task HandleIncomingConnections()
    {
        var runServer = true;

        // While a user hasn't visited the `shutdown` url, keep on handling requests
        while (runServer)
        {
            // Will wait here until we hear from a connection
            var ctx = await _listener.GetContextAsync();

            // Peel out the requests and response objects
            var req = ctx.Request;
            var resp = ctx.Response;

            // Print out some info about the request
            Console.WriteLine("Request #: {0}", ++_requestCount);
            Console.WriteLine(req.Url.ToString());
            Console.WriteLine(req.HttpMethod);
            Console.WriteLine(req.UserHostName);
            Console.WriteLine(req.UserAgent);
            Console.WriteLine();

            // If `shutdown` url requested w/ POST, then shutdown the server after serving the page
            if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/shutdown")
            {
                Console.WriteLine("Shutdown requested");
                //We can stop server by setting it to false, why? not!
                runServer = false;
            }

            // Make sure we don't increment the page views counter if `favicon.ico` is requested
            if (req.Url.AbsolutePath != "/favicon.ico")
                _pageViews += 1;

            // Write the response info
            var disableSubmit = !runServer ? "disabled" : "";
            var data = Encoding.UTF8.GetBytes(string.Format(PageData, _pageViews, disableSubmit));
            resp.ContentType = "text/html";
            resp.ContentEncoding = Encoding.UTF8;
            resp.ContentLength64 = data.LongLength;

            //example of game json
            try
            {
                var options1 = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic),
                    WriteIndented = true
                };

                var game1 = _global.GamesList[0];
                var jsonString = JsonSerializer.Serialize(game1, options1);
                Console.Write(jsonString);
            }
            catch
            {
                //
            }

            // Write out to the response stream (asynchronously), then close it
            await resp.OutputStream.WriteAsync(data);
            resp.Close();
        }
    }

    public async void ApiTest()
    {
        try
        {
            // Create a Http server and start listening for incoming connections
            _listener = new HttpListener();
            _listener.Prefixes.Add(Url);
            _listener.Start();
            Console.WriteLine("Listening for connections on {0}", Url);


#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            HandleIncomingConnections();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }
        catch (Exception error)
        {
            _log.Warning($"ERROR! APi TEST: {error}");
        }

        await Task.CompletedTask;
    }
}