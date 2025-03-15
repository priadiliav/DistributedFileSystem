namespace DfsService.Utils;

public sealed class KeyedSemaphore<TKey>(IEqualityComparer<TKey>? comparer = null) where TKey : notnull
{
    private readonly Dictionary<TKey, SemaphoreSlim?> _semaphores = new(comparer);
    private readonly object _lock = new();

    public void AddToState(TKey key)
    {
        lock (_lock)
        {
            if (!_semaphores.ContainsKey(key))
            {
                _semaphores[key] = new SemaphoreSlim(1, 1);
            }
        }
    }

    public async Task WaitAsync(TKey key)
    {
        SemaphoreSlim? semaphore;

        lock (_lock)
        {
            if (!_semaphores.TryGetValue(key, out semaphore))
            {
                return;
            }
        }

        if (semaphore != null)
            await semaphore.WaitAsync().ConfigureAwait(false);
    }

    public void Release(TKey key)
    {
        lock (_lock)
        {
            if (!_semaphores.TryGetValue(key, out var semaphore)) return;
            semaphore?.Release();

            if (semaphore is { CurrentCount: 1 })
            {
                _semaphores.Remove(key);
            }
        }
    }

    public bool IsLocked(TKey key)
    {
        lock (_lock)
        {
            if (_semaphores.TryGetValue(key, out var semaphore))
            {
                return semaphore?.CurrentCount == 0;
            }
        }

        return false;
    }
}
