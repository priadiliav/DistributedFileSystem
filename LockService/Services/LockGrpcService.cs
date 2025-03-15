using System.Collections.Concurrent;
using Dfs.Dfsservice;
using Dfs.Lockservice;
using Grpc.Core;
using Grpc.Net.Client;
using LockService.Configs;
using StopRequest = Dfs.Lockservice.StopRequest;
using StopResponse = Dfs.Lockservice.StopResponse;

namespace LockService.Services;

public class LockGrpcService : Dfs.Lockservice.LockService.LockServiceBase
{
    private static readonly Dictionary<string, LockState> LockState = new(); 
    
    private static readonly BlockingCollection<string> RevokeChannel = new(); 
    private static readonly BlockingCollection<string> RetryChannel = new(); 
    
    private static readonly Dictionary<string, AcquireRequest> Queue = new(); 
    private static readonly object LockStateMutex = new(); 

    private readonly IHostApplicationLifetime _lifetime;

    public LockGrpcService(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
        
        Task.Run(Revoker);
        Task.Run(Retrier);
    }

    public override Task<AcquireResponse> acquire(AcquireRequest request, ServerCallContext context)
    {
        Console.WriteLine($"Acquire request: lockId={request.LockId} ownerId={request.OwnerId} sequence={request.Sequence}");

        lock (LockStateMutex)
        {
            
            if (!LockState.TryGetValue(request.LockId, out var lockInfo) || lockInfo.OwnerId == request.OwnerId)
            {
                LockState[request.LockId] = new LockState { OwnerId = request.OwnerId, Sequence = request.Sequence };
                
                return Task.FromResult(new AcquireResponse { Success = true });
            }
            
            Queue[request.LockId] = request;
            
            RevokeChannel.Add(request.LockId);
            
            return Task.FromResult(new AcquireResponse { Success = false });
        }
    }

    public override Task<ReleaseResponse> release(ReleaseRequest request, ServerCallContext context)
    {
        Console.WriteLine($"Release request: lockId={request.LockId} ownerId={request.OwnerId}");

        lock (LockStateMutex)
        {
            if (LockState.TryGetValue(request.LockId, out var lockInfo) && lockInfo.OwnerId == request.OwnerId)
            {
                LockState.Remove(request.LockId);
                
                if (Queue.ContainsKey(request.LockId))
                {
                    RetryChannel.Add(request.LockId);
                }
            }
            return Task.FromResult(new ReleaseResponse());
        }
    }

    private void Revoker()
    {
        foreach (var lockId in RevokeChannel.GetConsumingEnumerable())
        {
            lock (LockStateMutex)
            {
                if (!LockState.TryGetValue(lockId, out var lockInfo)) 
                    continue;
                
                var parts = lockInfo.OwnerId.Split(':');
                var address = parts[0];
                var port = parts[1];
                    
                var client = new LockCacheService.LockCacheServiceClient(
                    GrpcChannel.ForAddress($"http://{address}:{port}"));
                    
                client.revokeAsync(new RevokeRequest { LockId = lockId });
                
                Console.WriteLine($"Revoking process: lockId={lockId} ownerId={lockInfo.OwnerId}");
            }
        }
    }
    
    private void Retrier()
    {
        foreach (var lockId in RetryChannel.GetConsumingEnumerable())
        {
            lock (LockStateMutex)
            {
                if (!Queue.TryGetValue(lockId, out var request)) continue;
                var parts = request.OwnerId.Split(':');
                var address = parts[0];
                var port = parts[1];
    
                var client = new LockCacheService.LockCacheServiceClient(
                    GrpcChannel.ForAddress($"http://{address}:{port}"));
                
                client.retryAsync(new RetryRequest { LockId = lockId, Sequence = request.Sequence });
                
                Queue.Remove(lockId);
    
                Console.WriteLine($"Retrying process: lockId={lockId} ownerId={request.OwnerId} sequence={request.Sequence}");
            }
        }
    }

    public override Task<StopResponse> stop(StopRequest request, ServerCallContext context)
    {
        _lifetime.StopApplication();
        
        RevokeChannel.CompleteAdding();
        RetryChannel.CompleteAdding();
        
        return Task.FromResult(new StopResponse());
    }
}

