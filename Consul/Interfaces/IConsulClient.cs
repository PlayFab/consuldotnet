using System;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface IConsulClient : IDisposable
    {
        IACLEndpoint ACL { get; }
        Task<IDistributedLock> AcquireLock(LockOptions opts);
        Task<IDistributedLock> AcquireLock(LockOptions opts, CancellationToken ct);
        Task<IDistributedLock> AcquireLock(string key);
        Task<IDistributedLock> AcquireLock(string key, CancellationToken ct);
        Task<IDistributedSemaphore> AcquireSemaphore(SemaphoreOptions opts);
        Task<IDistributedSemaphore> AcquireSemaphore(string prefix, int limit);
        IAgentEndpoint Agent { get; }
        ICatalogEndpoint Catalog { get; }
        IDistributedLock CreateLock(LockOptions opts);
        IDistributedLock CreateLock(string key);
        IEventEndpoint Event { get; }
        Task ExecuteInSemaphore(SemaphoreOptions opts, Action a);
        Task ExecuteInSemaphore(string prefix, int limit, Action a);
        Task ExecuteLocked(LockOptions opts, Action action);
        Task ExecuteLocked(LockOptions opts, CancellationToken ct, Action action);
        Task ExecuteLocked(string key, Action action);
        Task ExecuteLocked(string key, CancellationToken ct, Action action);
        IHealthEndpoint Health { get; }
        IKVEndpoint KV { get; }
        IRawEndpoint Raw { get; }
        IDistributedSemaphore Semaphore(SemaphoreOptions opts);
        IDistributedSemaphore Semaphore(string prefix, int limit);
        ISessionEndpoint Session { get; }
        IStatusEndpoint Status { get; }
    }
}
