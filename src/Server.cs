using codecrafters_http_server;

Console.WriteLine("APPLICATION START");
var server = new HttpServer();
server.Start();
await server.Handle();

Console.WriteLine("APPLICATION END");
