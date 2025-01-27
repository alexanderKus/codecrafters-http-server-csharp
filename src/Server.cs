using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

var buffer = new byte[4096];
TcpListener server = new(IPAddress.Any, 4221);
server.Start();
using var socket = server.AcceptSocket();
var bytesRead = socket.Receive(buffer);
var request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
var httpParts = request.Split("\r\n").AsSpan();
var httpInfo = httpParts[0].Split(" ").AsSpan();
var headers = httpParts[1..];
var httpMethod = httpInfo[0];
var httpPath = httpInfo[1];
var httpVersion = httpInfo[2];
var message = string.Empty;
if (httpPath == "/")
{
    Console.WriteLine("Success");
    message = "HTTP/1.1 200 OK\r\n\r\n";
}
else
{
    Console.WriteLine("Not Found");
    message = "HTTP/1.1 404 Not Found\r\n\r\n";
}

socket.Send(Encoding.UTF8.GetBytes(message));
