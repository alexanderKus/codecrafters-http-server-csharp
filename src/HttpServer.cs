using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace codecrafters_http_server;

public class HttpServer
{
    
    private byte[] _buffer = new byte[4096];
    private TcpListener? _server = null;

    public HttpServer(int port = 4221)
    {
        _server = new TcpListener(IPAddress.Any, port);
    }
    
    public void Start()
    {
        _server!.Start();
    }

    public void Handle()
    {
        if (_server is null) throw new Exception("server is null");
        using var socket = _server.AcceptSocket();
        var bytesRead = socket.Receive(_buffer);
        var request = Encoding.ASCII.GetString(_buffer, 0, bytesRead);
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
        else if (httpPathParts[0] == "echo")
        {
            if (httpPathParts.Length > 1)
            {
                var arg = httpPathParts[1];
                message = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {arg.Length}\r\n\r\n{arg}";
            }
            else
            {
                message = "HTTP/1.1 200 OK\r\n\r\n";
            }
        }
        else if (httpPathParts[0] == "user-agent" && headers.TryGetValue("User-Agent", out var value))
        {
                message = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {value.Length}\r\n\r\n{value}";
        }
        else
        {
            Console.WriteLine("Not Found");
            message = "HTTP/1.1 404 Not Found\r\n\r\n";
        }

        socket.Send(Encoding.UTF8.GetBytes(message));
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
}