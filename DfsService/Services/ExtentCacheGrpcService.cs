using System.Collections.Concurrent;
using Dfs.Extentservice;
using DfsService.Configs;
using Grpc.Core;

namespace DfsService.Services;

public class ExtentCacheGrpcService(ExtentService.ExtentServiceClient extentClient) : ExtentService.ExtentServiceBase
{
    private readonly ConcurrentDictionary<string, CachedExtent> _cache = new();

    public override async Task<GetResponse> get(GetRequest request, ServerCallContext context)
    {
        if (_cache.TryGetValue(request.FileName, out var cachedExtent) && !cachedExtent.IsDirty)
        {
            return new GetResponse { FileData = cachedExtent.FileData };
        }

        var serverResponse = await extentClient.getAsync(new GetRequest { FileName = request.FileName });
       
        _cache[request.FileName] = new CachedExtent { FileData = serverResponse.FileData, IsDirty = false, IsRemoved = false};

        return serverResponse;
    }

    public override async Task<PutResponse> put(PutRequest request, ServerCallContext context)
    {
        if (request.HasFileData)
        {
            var responseData = await extentClient.getAsync(new GetRequest { FileName = request.FileName });
            if (responseData.HasFileData)
            {
                return new PutResponse { Success = false };
            }
            _cache[request.FileName] = new CachedExtent { FileData = request.FileData, IsDirty = true, IsRemoved = false};
        }
        else
        {
            _cache[request.FileName] = new CachedExtent { FileData = request.FileData, IsDirty = true, IsRemoved = true};
        }
        
        return new PutResponse { Success = true };
    }
    
    public async Task update(string fileName)
    {
        if (_cache.TryGetValue(fileName, out var cachedExtent) && cachedExtent.IsDirty)
        {
            if (cachedExtent.IsRemoved)
            {
                await extentClient.putAsync(new PutRequest { FileName = fileName });
                updateRoot(fileName, cachedExtent.IsRemoved);
                _cache.TryRemove(fileName, out _);       
            }
            else
            {
                await extentClient.putAsync(new PutRequest { FileName = fileName, FileData = cachedExtent.FileData });
                updateRoot(fileName, cachedExtent.IsRemoved);
                cachedExtent.IsDirty = false;
            }
        }
    }

    private void updateRoot(string fileName, bool isRemoveRequest)
    {
        var path = fileName.Split("/", StringSplitOptions.RemoveEmptyEntries);                
        var rootPath = path.Length > 1 
            ? "/" + path[^2] + "/" 
            : "/";
        
        if (_cache.TryGetValue(rootPath, out var rootCachedExtent))
        {
            var cacheData = rootCachedExtent.FileData.ToStringUtf8().Split('\n', 
                StringSplitOptions.RemoveEmptyEntries).ToList();
            
            if (isRemoveRequest)
            {
                cacheData.Remove(path[^1]);
            }
            else
            {
                cacheData.Add(path[^1]);
            }
            rootCachedExtent.FileData = Google.Protobuf.ByteString.CopyFromUtf8(string.Join("\n", cacheData) + "\n");
        }
    }
    
    public Task flush()
    {
        _cache.Clear();
        
        return Task.CompletedTask;
    }
}