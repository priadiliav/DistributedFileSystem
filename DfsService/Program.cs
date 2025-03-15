using Dfs.Extentservice;
using Dfs.Lockservice;
using DfsService.Configs;
using DfsService.Services;
using Grpc.Net.Client;

var config = new ServerConfiguration()
{
    ServerHost = "127.0.0.1",
    ServerPort = args[0],
    ExtentServiceAddress = $"http://{args[1]}",
    LockServiceAddress = $"http://{args[2]}"
};

var extentServiceClient = new ExtentService.ExtentServiceClient(GrpcChannel.ForAddress(config.ExtentServiceAddress)); 
var lockServiceClient = new LockService.LockServiceClient(GrpcChannel.ForAddress(config.LockServiceAddress));

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton(extentServiceClient);
builder.Services.AddSingleton(lockServiceClient);

builder.Services.AddSingleton<LockCacheGrpcService>();
builder.Services.AddSingleton<ExtentCacheGrpcService>();

builder.Services.AddGrpc();
var app = builder.Build();
app.MapGrpcService<LockCacheGrpcService>();
app.MapGrpcService<DfsGrpcService>();
app.MapGrpcService<ExtentCacheGrpcService>();

app.Urls.Add($"http://{config.ServerHost}:{config.ServerPort}");

app.Run(); 