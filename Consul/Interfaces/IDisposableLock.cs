using System;
using System.Threading;

namespace Consul
{
    public interface IDisposableLock : IDistributedLock, IDisposable
    {
        CancellationToken CancellationToken { get; }
    }
}