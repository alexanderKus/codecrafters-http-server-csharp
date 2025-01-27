using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace codecrafters_http_server;

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
        var headers = GetHeaders(httpParts);
        var (httpMethod, httpPath, httpVersion) = GetHttpInfo(httpParts);
        var httpPathParts = httpPath.Split("/")
            .Where(x => x != string.Empty)
            .ToArray();
        var message = string.Empty;
        
        if (httpPathParts.Length == 0)
        {
            // Path: /
            message = "HTTP/1.1 200 OK\r\n\r\n";
        }
        else
        {
            switch (httpPathParts[0])
            {
                case "echo": 
                    message = HandleEcho(httpPathParts);
                    break;
                case "user-agent":
                    message = HandleUserAgent(headers);
                    break;
                case "files":
                    message = HandleFiles(httpPathParts);
                    break;
                default:
                    Console.WriteLine("Not Found");
                    message = "HTTP/1.1 404 Not Found\r\n\r\n";
                    break;
            }
        }
        await socket.SendAsync(Encoding.UTF8.GetBytes(message));
    }

    private ReadOnlyDictionary<string, string> GetHeaders(ReadOnlySpan<string> httpParts)
    { 
        Dictionary<string, string> dictionary = new();
        foreach(var header in httpParts[1..])
        {
            var parts = header.Split(':', 2);
            dictionary[parts[0]] = parts[1].Trim();
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

    private string HandleEcho(string[] path)
    {
        if (path.Length <= 1) return "HTTP/1.1 200 OK\r\n\r\n";
        var arg = path[1];
        return $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {arg.Length}\r\n\r\n{arg}";
    }

    private string HandleUserAgent(ReadOnlyDictionary<string, string> headers)
    {
        return headers.TryGetValue("User-Agent", out var value) 
            ? $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {value.Length}\r\n\r\n{value}" 
            : "HTTP/1.1 404 Not Found\r\n\r\n";
    }
    
    private string HandleFiles(string[] path)
    {
        var filename = path[1];
        var directory = Environment.GetCommandLineArgs()[2];
        var fullPath = Path.Combine(directory, filename);
        if (!File.Exists(fullPath)) return "HTTP/1.1 404 Not Found\r\n\r\n";
        var content = File.ReadAllText(fullPath);
        return $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: {content.Length}\r\n\r\n{content}";
    }
}