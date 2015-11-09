// -----------------------------------------------------------------------
//  <copyright file="Health.cs" company="PlayFab Inc">
//    Copyright 2015 PlayFab Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
namespace Consul
{
    public interface IConsulClient
    {
        IACLEndpoint ACL { get; }
        IDisposableLock AcquireLock(LockOptions opts);
        IDisposableLock AcquireLock(LockOptions opts, CancellationToken ct);
        IDisposableLock AcquireLock(string key);
        IDisposableLock AcquireLock(string key, CancellationToken ct);
        IDisposableSemaphore AcquireSemaphore(SemaphoreOptions opts);
        IDisposableSemaphore AcquireSemaphore(string prefix, int limit);
        IAgentEndpoint Agent { get; }
        ICatalogEndpoint Catalog { get; }
        IDistributedLock CreateLock(LockOptions opts);
        IDistributedLock CreateLock(string key);
        IEventEndpoint Event { get; }
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
        IHealthEndpoint Health { get; }
        IKVEndpoint KV { get; }
        IRawEndpoint Raw { get; }
        IDistributedSemaphore Semaphore(SemaphoreOptions opts);
        IDistributedSemaphore Semaphore(string prefix, int limit);
        ISessionEndpoint Session { get; }
        IStatusEndpoint Status { get; }
    }
}
