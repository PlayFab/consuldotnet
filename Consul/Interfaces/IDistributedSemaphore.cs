using System.Threading;

namespace Consul
{
    public interface IDistributedSemaphore
    {
        bool IsHeld { get; }

        CancellationToken Acquire();
        CancellationToken Acquire(CancellationToken ct);
        void Destroy();
        void Release();
    }
}