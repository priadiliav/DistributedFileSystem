using System.Collections.Concurrent;
using Dfs.Dfsservice;
using Dfs.Lockservice;
using DfsService.Utils;
using Grpc.Core;

namespace DfsService.Services;
public class LockCacheGrpcService : LockCacheService.LockCacheServiceBase
{
    private readonly ExtentCacheGrpcService _extentCacheGrpcService;
    private readonly LockService.LockServiceClient _lockServiceClient;

    private static readonly KeyedSemaphore<string> KeyedSemaphore = new();
    private static readonly ConcurrentDictionary<string, string> LockState = new();
    private static readonly CancellationTokenSource Token = new();
    private static readonly ConcurrentQueue<Tuple<string, string>> ReleaseQueue = new();
    
    public LockCacheGrpcService(LockService.LockServiceClient lockServiceClient, 
        ExtentCacheGrpcService extentCacheGrpcService)
    {
        _extentCacheGrpcService = extentCacheGrpcService;
        _lockServiceClient = lockServiceClient;
        Task.Run(async () => await ReleaserProcess());
    }

    public override Task<RetryResponse> retry(RetryRequest request, ServerCallContext context)
    {
        Console.WriteLine($"Retry request: lockId:{request.LockId}");
        KeyedSemaphore.Release(request.LockId);

        if (LockState.ContainsKey(request.LockId))
            LockState.TryRemove(request.LockId, out _);

        return Task.FromResult(new RetryResponse());
    }

    public override Task<RevokeResponse> revoke(RevokeRequest request, ServerCallContext context)
    {
        Console.WriteLine($"Revoke request: lockId:{request.LockId}");
        if (LockState.TryGetValue(request.LockId, out var alreadyLocked))
        {
            ReleaseQueue.Enqueue(new Tuple<string, string>(request.LockId, alreadyLocked));
        }
        return Task.FromResult(new RevokeResponse());
    }

    private async Task ReleaserProcess()
    {
        while (!Token.IsCancellationRequested)
        {
            if (ReleaseQueue.TryDequeue(out var lockId))
            {
                if (KeyedSemaphore.IsLocked(lockId.Item1))
                    KeyedSemaphore.Release(lockId.Item1);
                
                await _extentCacheGrpcService.flush();
                
                await _lockServiceClient.releaseAsync(new ReleaseRequest
                {
                    LockId = lockId.Item1,
                    OwnerId = lockId.Item2
                });
                
                LockState.TryRemove(lockId.Item1, out _);
                
                Console.WriteLine($"Releaser sent request: lockId:{lockId.Item1} ownerId:{lockId.Item2}");
            }
        }
    }

    public async Task<AcquireResponse> Acquire(AcquireRequest request)
    {
        Console.WriteLine($"Acquire request: lockId:{request.LockId} ownerId:{request.OwnerId}");

        if (KeyedSemaphore.IsLocked(request.LockId))
        {
            await Task.Delay(100);

            await Acquire(request);
        }
        else
        {
            if (!LockState.ContainsKey(request.LockId))
            {
                var response = await _lockServiceClient.acquireAsync(new AcquireRequest
                {
                    LockId = request.LockId,
                    OwnerId = request.OwnerId,
                    Sequence = request.Sequence
                });
                
                if (response.Success)
                {
                    KeyedSemaphore.AddToState(request.LockId);

                    LockState.TryAdd(request.LockId, request.OwnerId);

                    await KeyedSemaphore.WaitAsync(request.LockId);

                    return new AcquireResponse { Success = true };
                }

                KeyedSemaphore.AddToState(request.LockId);

                await KeyedSemaphore.WaitAsync(request.LockId);

                await Acquire(request);
            }
        }

        return new AcquireResponse { Success = true };
    }

    public async Task<ReleaseResponse> ReleaseAsync(ReleaseRequest request, bool isGetRequest = false)
    {
        Console.WriteLine($"Release local request: lockId:{request.LockId} ownerId:{request.OwnerId}");
        
        if (KeyedSemaphore.IsLocked(request.LockId))
        {
            KeyedSemaphore.Release(request.LockId);
        }
        
        if (!isGetRequest)
            await _extentCacheGrpcService.update(request.LockId);

        return new ReleaseResponse();
    }

    public void Stop() => Token.Cancel();
}
