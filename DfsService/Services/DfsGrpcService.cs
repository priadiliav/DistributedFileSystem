using Dfs.Dfsservice;
using Dfs.Lockservice;
using DfsService.Configs;
using Grpc.Core;
using GetRequest = Dfs.Dfsservice.GetRequest;
using GetResponse = Dfs.Dfsservice.GetResponse;
using PutRequest = Dfs.Dfsservice.PutRequest;
using PutResponse = Dfs.Dfsservice.PutResponse;
using StopRequest = Dfs.Dfsservice.StopRequest;
using StopResponse = Dfs.Dfsservice.StopResponse;

namespace DfsService.Services;

public class DfsGrpcService(
    ServerConfiguration configuration,
    IHostApplicationLifetime lifetime,
    LockCacheGrpcService lockCacheService,
    ExtentCacheGrpcService extentCacheGrpcService)
    : Dfs.Dfsservice.DfsService.DfsServiceBase
{
    private readonly string _clientId = Guid.NewGuid().ToString();
    private string GetOwnerId() => $"{configuration.ServerHost}:{configuration.ServerPort}:{_clientId}";

    private async Task AcquireLock(string lockId)
    {
        await lockCacheService.Acquire(new AcquireRequest { LockId = lockId, OwnerId = GetOwnerId(), Sequence = 1});
    }

    private async Task ReleaseLock(string lockId, bool isGetRequest = false)
    {
        await lockCacheService.ReleaseAsync(new ReleaseRequest { LockId = lockId, OwnerId = GetOwnerId() }, isGetRequest);
    }

    public override async Task<DeleteResponse> delete(DeleteRequest request, ServerCallContext context)
    {
        await AcquireLock(request.FileName);

        var extentCacheResponse = await extentCacheGrpcService.put(new Dfs.Extentservice.PutRequest
            { FileName = request.FileName }, context);
        
        await ReleaseLock(request.FileName);
        
        return new DeleteResponse { Success = extentCacheResponse.Success };
    }

    public override async Task<DirResponse> dir(DirRequest request, ServerCallContext context)
    {
        await AcquireLock(request.DirectoryName);
        
        var extentCacheResponse = await extentCacheGrpcService.get(new Dfs.Extentservice.GetRequest 
            { FileName = request.DirectoryName }, context);
        
        var dirList = extentCacheResponse.FileData.ToStringUtf8().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

        await ReleaseLock(request.DirectoryName, true);
        
        return new DirResponse { Success = true, DirList = { dirList } };
    }

    public override async Task<GetResponse> get(GetRequest request, ServerCallContext context)
    {
        await AcquireLock(request.FileName);
        
        var extentCacheResponse = await extentCacheGrpcService.get(new Dfs.Extentservice.GetRequest 
            { FileName = request.FileName }, context);
        
        await ReleaseLock(request.FileName, true);
        
        return new GetResponse { FileData = extentCacheResponse.FileData };
    }

    public override async Task<MkdirResponse> mkdir(MkdirRequest request, ServerCallContext context)
    {
        await AcquireLock(request.DirectoryName);
        
        var extentCacheResponse = await extentCacheGrpcService.put(new Dfs.Extentservice.PutRequest 
            { FileName = request.DirectoryName, FileData = Google.Protobuf.ByteString.CopyFromUtf8("") }, context);
        
        await ReleaseLock(request.DirectoryName);
        
        return new MkdirResponse { Success = extentCacheResponse.Success };
    }

    public override async Task<PutResponse> put(PutRequest request, ServerCallContext context)
    {
        await AcquireLock(request.FileName);
        var extentRequest = new Dfs.Extentservice.PutRequest
        {
            FileName = request.FileName, 
            FileData = request.FileData
        };
        
        var extentCacheResponse = await extentCacheGrpcService.put(extentRequest, context);
        
        await ReleaseLock(request.FileName);
        
        return new PutResponse { Success = extentCacheResponse.Success };
    }

    public override async Task<RmdirResponse> rmdir(RmdirRequest request, ServerCallContext context)
    {
        await AcquireLock(request.DirectoryName);
        
        var extentCacheResponse = await extentCacheGrpcService.put(new Dfs.Extentservice.PutRequest { FileName = request.DirectoryName }, context);

        await ReleaseLock(request.DirectoryName);
        
        return new RmdirResponse { Success = extentCacheResponse.Success };
    }

    public override Task<StopResponse> stop(StopRequest request, ServerCallContext context)
    {
        lifetime.StopApplication();
        lockCacheService.Stop();
        
        return Task.FromResult(new StopResponse());
    }
}