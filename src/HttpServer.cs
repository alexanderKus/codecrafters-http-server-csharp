using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace codecrafters_http_server;


public abstract class MyHttpMethod
{
    public abstract string Method { get; }
    
    public class Get : MyHttpMethod
    {
        public override string Method { get; } = "GET";
    }
    public class Post : MyHttpMethod
    {
        public override string Method { get; } = "POST";
    }
}

public abstract class MyHttpCode
{
    public abstract string Code { get; }
    public abstract string Name { get; }

    public class Ok : MyHttpCode
    {
        public override string Code { get; } = "200";
        public override string Name { get; } = "OK";
        public override string ToString()
            => $"{Code} {Name}";
    }
    public class Created : MyHttpCode
    {
        public override string Code { get; } = "201";
        public override string Name { get; } = "Created";
        public override string ToString()
            => $"{Code} {Name}";
    }
    public class NotFound : MyHttpCode
    {
        public override string Code { get; } = "404";
        public override string Name { get; } = "Not Found";
        public override string ToString()
            => $"{Code} {Name}";
    }
    public class BadRequest : MyHttpCode
    {
        public override string Code { get; } = "400";
        public override string Name { get; } = "Bad Request";
        public override string ToString()
            => $"{Code} {Name}";
    }
}

public class MyResponse(MyHttpMethod method, IDictionary<string, string> requestHeader)
{
    public IReadOnlyDictionary<string, string> RequestHeader { get; } = requestHeader.AsReadOnly();
    private readonly Dictionary<string, string> _responseHeader = new();
    private MyHttpMethod? _status = method;
    private string _body = string.Empty;
    private MyHttpCode? _code;
    private static readonly string[] ValidCompressions = ["gzip"];

    public void SetHttpCode(MyHttpCode code)
        => _code = code;

    public void SetResponseHeader(string key, string value)
        => _responseHeader.Add(key, value);
    public void SetBody(string body)
        => _body = body;
    public byte[] GetBytes()
    {
        StringBuilder context = new();
        context.Append($"HTTP/1.1 {_code}\r\n");
        foreach (var header in _responseHeader)
        {
            context.Append(header.Key);
            context.Append(": ");
            context.Append(header.Value);
            context.Append("\r\n");
        }
        var hasCompression = false;
        if (RequestHeader.TryGetValue("Accept-Encoding", out var value))
        {
            var compressions = value.Split(',', StringSplitOptions.TrimEntries).ToArray();
            foreach (var compression in compressions)
            {
                if (!ValidCompressions.Contains(compression.ToLower())) continue;
                hasCompression = true;
                context.Append("Content-Encoding");
                context.Append(": ");
                context.Append(compression);
                context.Append("\r\n");
                break;
            }
        }
        if (hasCompression)
        {
            var bytes  = Encoding.UTF8.GetBytes(_body);
            using var memoryStream = new MemoryStream();
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true);
            gzipStream.Write(bytes, 0, bytes.Length);
            gzipStream.Flush();
            gzipStream.Close();
            var compressed = memoryStream.ToArray();
            context.Append("Content-Length");
            context.Append(": ");
            context.Append(compressed.Length);
            context.Append("\r\n\r\n");
            return [..Encoding.UTF8.GetBytes(context.ToString()), ..compressed];
        }
        context.Append("Content-Length");
        context.Append(": ");
        context.Append(_body.Length);
        context.Append("\r\n\r\n");
        context.Append(_body);
        return Encoding.UTF8.GetBytes(context.ToString());
    }
}

public class HttpServer
{
    private TcpListener? _server = null;

    public HttpServer(int port = 4221)
    {
        _server = new TcpListener(IPAddress.Any, port);
    }
    
    public void Start()
    {
        _server!.Start();
    }

    public async Task Handle()
    {
        if (_server is null) throw new Exception("server is null");
        while (true)
        {
            var socket = await _server.AcceptSocketAsync();
            Task.Run(async () =>
            {
                try
                {
                    await HandleConnection(socket);
                }
                finally
                {
                    socket.Dispose();
                }
            });
        }
    }

    private async Task HandleConnection(Socket socket)
    {
        var buffer = new byte[4096];
        var bytesRead = socket.Receive(buffer);
        var request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
        var httpParts = request.Split("\r\n").Where(x => !string.IsNullOrEmpty(x)).ToArray();
        var hasBody = !request.EndsWith("\r\n");
        var headers = GetHeaders(httpParts, hasBody);
        var (httpMethod, httpPath, httpVersion) = GetHttpInfo(httpParts);
        var body = hasBody ? httpParts.Last() : string.Empty;
        var httpPathParts = httpPath.Split("/")
            .Where(x => x != string.Empty)
            .ToArray();
        var response = new MyResponse(httpMethod == "GET" ? new MyHttpMethod.Get() : new MyHttpMethod.Post(), headers);
        
        if (httpPathParts.Length == 0)
        {
            // Path: /
            response.SetHttpCode(new MyHttpCode.Ok());
        }
        else
        {
            switch (httpMethod)
            {
                case "GET":
                    switch (httpPathParts[0])
                    {
                        case "echo": 
                            HandleGetEcho(response, httpPathParts);
                            break;
                        case "user-agent":
                            HandleGetUserAgent(response);
                            break;
                        case "files":
                            HandleGetFiles(response, httpPathParts);
                            break;
                        default:
                            Console.WriteLine("Get Not Found");
                            response.SetHttpCode(new MyHttpCode.NotFound());
                            break;
                    }
                    break;
                case "POST":
                    switch (httpPathParts[0])
                    {
                        case "files":
                            HandlePostFiles(response, httpPathParts, body);
                            break;
                        default:
                            Console.WriteLine("Post Not Found");
                            response.SetHttpCode(new MyHttpCode.NotFound());
                            break;
                    }
                    break;
                default:
                    throw new Exception("Unsupported method");
            }
            
        }
        await socket.SendAsync(response.GetBytes());
    }

    private ReadOnlyDictionary<string, string> GetHeaders(ReadOnlySpan<string> httpParts, bool hasBody)
    { 
        Dictionary<string, string> dictionary = new();
        if (hasBody)
        {
            foreach(var header in httpParts[1..^1])
            {
                var parts = header.Split(':', 2);
                dictionary[parts[0]] = parts[1].Trim();
            }
        }
        else
        {
            foreach(var header in httpParts[1..])
            {
                var parts = header.Split(':', 2);
                dictionary[parts[0]] = parts[1].Trim();
            }
        }

        return dictionary.AsReadOnly();
    }
    private (string, string, string) GetHttpInfo(ReadOnlySpan<string> httpParts)
    {
        var httpInfo = httpParts[0].Split(" ").AsSpan();
        var httpMethod = httpInfo[0];
        var httpPath = httpInfo[1];
        var httpVersion = httpInfo[2];
        return (httpMethod, httpPath, httpVersion);
    }

    private void HandleGetEcho(MyResponse response, string[] path)
    {
        response.SetHttpCode(new MyHttpCode.Ok());
        if (path.Length <= 1) return;
        var arg = path[1];
        response.SetResponseHeader("Content-Type", "text/plain");
        response.SetBody(arg);
    }

    private void HandleGetUserAgent(MyResponse response)
    {
        if (!response.RequestHeader.TryGetValue("User-Agent", out var value))
        {
            response.SetHttpCode(new MyHttpCode.NotFound());
            return;
        }
        response.SetHttpCode(new MyHttpCode.Ok());
        response.SetResponseHeader("Content-Type", "text/plain");
        response.SetBody(value);
    }
    
    private void HandleGetFiles(MyResponse response, string[] path)
    {
        var filename = path[1];
        var directory = Environment.GetCommandLineArgs()[2];
        var fullPath = Path.Combine(directory, filename);
        if (!File.Exists(fullPath))
        {
            response.SetHttpCode(new MyHttpCode.NotFound());
            return;
        }
        var content = File.ReadAllText(fullPath);
        response.SetHttpCode(new MyHttpCode.Ok());
        response.SetResponseHeader("Content-Type", "application/octet-stream");
        response.SetBody(content);
    }
    
    private void HandlePostFiles(MyResponse response, string[] path, string body)
    {
        var filename = path[1];
        var directory = Environment.GetCommandLineArgs()[2];
        var fullPath = Path.Combine(directory, filename);
        try
        {
            var file = File.Create(fullPath);
            file.Write(Encoding.UTF8.GetBytes(body));
            file.Close();
        }
        catch (IOException)
        {
            Console.WriteLine($"Cannot write to a file {fullPath}, content {body}");
            response.SetHttpCode(new MyHttpCode.BadRequest());
            return;
        }
        response.SetHttpCode(new MyHttpCode.Created());
    }
}