using System;
using System.Threading;

namespace Consul
{
    public interface IDisposableSemaphore : IDistributedSemaphore, IDisposable
    {
        CancellationToken CancellationToken { get; }
    }
}