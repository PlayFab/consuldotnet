using System;
using System.Threading;
namespace Consul
{
    interface IConsulClient
    {
        IACLEndpoint ACL { get; }
        DisposableLock AcquireLock(LockOptions opts);
        DisposableLock AcquireLock(LockOptions opts, CancellationToken ct);
        DisposableLock AcquireLock(string key);
        DisposableLock AcquireLock(string key, CancellationToken ct);
        AutoSemaphore AcquireSemaphore(SemaphoreOptions opts);
        AutoSemaphore AcquireSemaphore(string prefix, int limit);
        IAgentEndpoint Agent { get; }
        ICatalogEndpoint Catalog { get; }
        Lock CreateLock(LockOptions opts);
        Lock CreateLock(string key);
        Event Event { get; }
        void ExecuteAbortableLocked(LockOptions opts, Action action);
        void ExecuteAbortableLocked(LockOptions opts, CancellationToken ct, Action action);
        void ExecuteAbortableLocked(string key, Action action);
        void ExecuteAbortableLocked(string key, CancellationToken ct, Action action);
        void ExecuteInSemaphore(SemaphoreOptions opts, Action a);
        void ExecuteInSemaphore(string prefix, int limit, Action a);
        void ExecuteLocked(LockOptions opts, Action action);
        void ExecuteLocked(LockOptions opts, CancellationToken ct, Action action);
        void ExecuteLocked(string key, Action action);
        void ExecuteLocked(string key, CancellationToken ct, Action action);
        Health Health { get; }
        KV KV { get; }
        Raw Raw { get; }
        Semaphore Semaphore(SemaphoreOptions opts);
        Semaphore Semaphore(string prefix, int limit);
        Session Session { get; }
        Status Status { get; }
    }
}
