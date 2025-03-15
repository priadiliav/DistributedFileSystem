using LockService.Configs;
using LockService.Services;

var config = new ServerConfiguration()
{
    ServerHost = "127.0.0.1",
    ServerPort = args[0]
};

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

builder.Services.AddSingleton(config);
builder.Services.AddSingleton<LockGrpcService>();
builder.Services.AddGrpc();

var app = builder.Build();
app.Urls.Add($"http://{config.ServerHost}:{config.ServerPort}");
app.MapGrpcService<LockGrpcService>();

app.Run();