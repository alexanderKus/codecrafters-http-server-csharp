using System.Net;
using System.Net.Sockets;
using System.Text;
using codecrafters_http_server;

Console.WriteLine("APPLICATION START");
var server = new HttpServer();
server.Start();
server.Handle();
Console.WriteLine("APPLICATION END");
