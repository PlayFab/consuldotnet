using System;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface IConsulClient : IDisposable
    {
        IACLEndpoint ACL { get; }
        Task<IDistributedLock> AcquireLock(LockOptions opts, CancellationToken ct = default(CancellationToken));
        Task<IDistributedLock> AcquireLock(string key, CancellationToken ct = default(CancellationToken));
        Task<IDistributedSemaphore> AcquireSemaphore(SemaphoreOptions opts, CancellationToken ct = default(CancellationToken));
        Task<IDistributedSemaphore> AcquireSemaphore(string prefix, int limit, CancellationToken ct = default(CancellationToken));
        IAgentEndpoint Agent { get; }
        ICatalogEndpoint Catalog { get; }
        IDistributedLock CreateLock(LockOptions opts);
        IDistributedLock CreateLock(string key);
        IEventEndpoint Event { get; }
        Task ExecuteInSemaphore(SemaphoreOptions opts, Action a, CancellationToken ct = default(CancellationToken));
        Task ExecuteInSemaphore(string prefix, int limit, Action a, CancellationToken ct = default(CancellationToken));
        Task ExecuteLocked(LockOptions opts, Action action, CancellationToken ct = default(CancellationToken));
        [Obsolete("This method will be removed in 0.8.0. Replace calls with the method signature ExecuteLocked(LockOptions, Action, CancellationToken)")]
        Task ExecuteLocked(LockOptions opts, CancellationToken ct, Action action);
        Task ExecuteLocked(string key, Action action, CancellationToken ct = default(CancellationToken));
        [Obsolete("This method will be removed in 0.8.0. Replace calls with the method signature ExecuteLocked(string, Action, CancellationToken)")]
        Task ExecuteLocked(string key, CancellationToken ct, Action action);
        IHealthEndpoint Health { get; }
        IKVEndpoint KV { get; }
        IRawEndpoint Raw { get; }
        IDistributedSemaphore Semaphore(SemaphoreOptions opts);
        IDistributedSemaphore Semaphore(string prefix, int limit);
        ISessionEndpoint Session { get; }
        IStatusEndpoint Status { get; }
        IOperatorEndpoint Operator { get; }
        IPreparedQueryEndpoint PreparedQuery { get; }
    }
}
