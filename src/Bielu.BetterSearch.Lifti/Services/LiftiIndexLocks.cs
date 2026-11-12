using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Bielu.BetterSearch.Lifti.Services;

/// <summary>
/// One lock per index name, shared by every provider that uses the same <see cref="ILiftiIndexManager"/>.
/// The provider is scoped and the manager is a singleton, so keying the locks on the manager makes all scopes
/// share them, and providers with separate managers don't block each other.
/// </summary>
internal sealed class LiftiIndexLocks
{
    private static readonly ConditionalWeakTable<ILiftiIndexManager, LiftiIndexLocks> ByManager = new();

    // Index names are matched as LiftiIndexManager matches them, case-sensitively. A lock is never removed; there is
    // one small semaphore per index name ever used, which suits the handful of indexes an application has.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public static LiftiIndexLocks For(ILiftiIndexManager manager) => ByManager.GetValue(manager, _ => new LiftiIndexLocks());

    /// <summary>Waits for the index's lock; dispose the result to release it.</summary>
    public async Task<IDisposable> AcquireAsync(string index, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(index, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
