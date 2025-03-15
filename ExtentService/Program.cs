
using ExtentService.Configs;
using ExtentService.Services;

var config = new ServerConfiguration()
{
    ServerHost = "127.0.0.1",
    ServerPort = args[0],
    ServerRootPath = args[1]
};

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(config);

Console.WriteLine($"Server root path: {config.ServerRootPath}");

builder.Services.AddGrpc();
var app = builder.Build();
app.Urls.Add($"http://{config.ServerHost}:{config.ServerPort}");
app.MapGrpcService<ExtentGrpcService>();

app.Run();