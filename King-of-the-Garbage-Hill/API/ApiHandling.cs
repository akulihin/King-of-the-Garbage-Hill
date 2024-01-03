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


    private async Task<(bool runServer, byte[] returnJsonBytes)> HandleGetRequest(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var runServer = true;
        switch(req.Url!.AbsolutePath)
        {
            case "/shutdown":
                Console.WriteLine("Shutdown requested");
                //We can stop server by setting it to false, why? not!
                runServer = false;
            break;

            default:
            break;
        }

        var jsonResponse = "{\"hello\": \"world GET\"}";
        var jsonBytes = Encoding.UTF8.GetBytes(jsonResponse);

        await Task.CompletedTask; //костыль, потом заберем
        return (runServer, jsonBytes);
    }

    private async Task<(bool runServer, byte[] returnJsonBytes)> HandlePostRequest(HttpListenerRequest req, HttpListenerResponse resp)
    {
        var runServer = true;
        switch(req.Url!.AbsolutePath)
        {
            case "/shutdown":
                Console.WriteLine("Shutdown requested");
                //We can stop server by setting it to false, why? not!
                runServer = false;
            break;

            default:
            break;
        }

        var jsonResponse = "{\"hello\": \"world POST\"}";
        var jsonBytes = Encoding.UTF8.GetBytes(jsonResponse);
        
        await Task.CompletedTask; //костыль, потом заберем
        return (runServer, jsonBytes);
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

            //Set Return Variable
            byte[] jsonBytes;

            //set response headers
            resp.ContentType = "application/json";
            resp.ContentEncoding = Encoding.UTF8;

            if (req.Url == null)
            {
                Console.WriteLine("req.Url == null ALLALALALALAALAL");
                jsonBytes = null;
            }
            else
            {
                switch (req.HttpMethod)
                {
                    case "POST":
                        var response = await HandlePostRequest(req, resp);
                        runServer = response.runServer;
                        jsonBytes = response.returnJsonBytes;
                        break;
                    case "GET":
                        response = await HandleGetRequest(req, resp);
                        jsonBytes = response.returnJsonBytes;
                        break;
                    default:
                        resp.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        jsonBytes = Encoding.UTF8.GetBytes("{\"error\": \"Method Not Allowed\"}");
                        break;
                }
            }


            if (jsonBytes == null)
            {
                resp.StatusCode = (int)HttpStatusCode.InternalServerError;
                jsonBytes = Encoding.UTF8.GetBytes("{\"error\": \"Internal Server Error\"}");
            }

            // Print out some info about the request
            Console.WriteLine("Request #: {0}", ++_requestCount);
            if (req.Url != null) Console.WriteLine(req.Url.ToString());
            Console.WriteLine(req.HttpMethod);
            Console.WriteLine(req.UserHostName);
            Console.WriteLine(req.UserAgent);
            Console.WriteLine();

            // Write JSON response
            resp.ContentLength64 = jsonBytes.LongLength;

            // Write out to the response stream (asynchronously), then close it
            await resp.OutputStream.WriteAsync(jsonBytes);
            
            //close response
            resp.Close();

            
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