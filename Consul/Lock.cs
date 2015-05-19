// -----------------------------------------------------------------------
//  <copyright file="Lock.cs" company="PlayFab Inc">
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
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    [Serializable]
    public class LockHeldException : Exception
    {
        public LockHeldException()
        {
        }

        public LockHeldException(string message)
            : base(message)
        {
        }

        public LockHeldException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected LockHeldException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class LockNotHeldException : Exception
    {
        public LockNotHeldException()
        {
        }

        public LockNotHeldException(string message)
            : base(message)
        {
        }

        public LockNotHeldException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected LockNotHeldException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class LockInUseException : Exception
    {
        public LockInUseException()
        {
        }

        public LockInUseException(string message)
            : base(message)
        {
        }

        public LockInUseException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected LockInUseException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class LockConflictException : Exception
    {
        public LockConflictException()
        {
        }

        public LockConflictException(string message)
            : base(message)
        {
        }

        public LockConflictException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected LockConflictException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Lock is used to implement client-side leader election. It is follows the algorithm as described here: https://consul.io/docs/guides/leader-election.html.
    /// </summary>
    public class Lock
    {
        /// <summary>
        /// DefaultLockWaitTime is how long we block for at a time to check if lock acquisition is possible. This affects the minimum time it takes to cancel a Lock acquisition.
        /// </summary>
        public static readonly TimeSpan DefaultLockWaitTime = TimeSpan.FromSeconds(15);

        /// <summary>
        /// DefaultLockRetryTime is how long we wait after a failed lock acquisition before attempting to do the lock again. This is so that once a lock-delay is in affect, we do not hot loop retrying the acquisition.
        /// </summary>
        public static readonly TimeSpan DefaultLockRetryTime = TimeSpan.FromSeconds(5);

        /// <summary>
        /// LockFlagValue is a magic flag we set to indicate a key is being used for a lock. It is used to detect a potential conflict with a semaphore.
        /// </summary>
        private const ulong LockFlagValue = 0x2ddccbc058a50c18;

        private readonly object _lock = new object();
        private readonly object _heldLock = new object();
        private bool _isheld;

        private CancellationTokenSource _cts;

        internal Client Client { get; set; }
        internal LockOptions Opts { get; set; }
        internal string LockSession { get; set; }

        /// <summary>
        /// If the lock is held or not.
        /// Users of the Lock object should check the IsHeld property before entering the critical section of their code, e.g. in a "while (myLock.IsHeld) {criticalsection}" block.
        /// Calls to IsHeld are syncronized across threads using a lock, so multiple threads sharing a single Consul Lock will queue up reading the IsHeld property of the lock.
        /// </summary>
        public bool IsHeld
        {
            get
            {
                lock (_heldLock)
                {
                    return _isheld;
                }
            }
            private set
            {
                lock (_heldLock)
                {
                    _isheld = value;
                }
            }
        }


        public Lock(Client client)
        {
            Client = client;
        }

        /// <summary>
        /// Lock attempts to acquire the lock and blocks while doing so. Not providing a CancellationToken means the thread can block indefinitely until the lock is acquired.
        /// There is no notification that the lock has been lost, but it may be closed at any time due to session invalidation, communication errors, operator intervention, etc.
        /// It is NOT safe to assume that the lock is held until Unlock() unless the Session is specifically created without any associated health checks.
        /// Users of the Lock object should check the IsHeld property before entering the critical section of their code, e.g. in a "while (myLock.IsHeld) {criticalsection}" block.
        /// By default Consul sessions prefer liveness over safety and an application must be able to handle the lock being lost.
        /// </summary>
        public void Acquire()
        {
            Acquire(CancellationToken.None);
        }

        /// <summary>
        /// Lock attempts to acquire the lock and blocks while doing so.
        /// Providing a CancellationToken can be used to abort the lock attempt.
        /// There is no notification that the lock has been lost, but IsHeld may be set to False at any time due to session invalidation, communication errors, operator intervention, etc.
        /// It is NOT safe to assume that the lock is held until Unlock() unless the Session is specifically created without any associated health checks.
        /// Users of the Lock object should check the IsHeld property before entering the critical section of their code, e.g. in a "while (myLock.IsHeld) {criticalsection}" block.
        /// By default Consul sessions prefer liveness over safety and an application must be able to handle the lock being lost.
        /// </summary>
        /// <param name="ct">The cancellation token to cancel lock acquisition</param>
        public void Acquire(CancellationToken ct)
        {
            lock (_lock)
            {
                try
                {
                    if (IsHeld)
                    {
                        // Check if we already hold the lock
                        throw new LockHeldException();
                    }
                    // Don't overwrite the CancellationTokenSource until AFTER we've tested for holding, since there might be tasks that are currently running for this lock.
                    if (_cts != null && _cts.IsCancellationRequested)
                    {
                        _cts.Dispose();
                        _cts = null;
                    }
                    _cts = new CancellationTokenSource();
                    LockSession = Opts.Session;
                    // Check if we need to create a session first
                    if (string.IsNullOrEmpty(Opts.Session))
                    {
                        try
                        {
                            Opts.Session = CreateSession();
                            Client.Session.RenewPeriodic(Opts.SessionTTL, Opts.Session, WriteOptions.Empty, _cts.Token);
                            LockSession = Opts.Session;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("Failed to create session", ex);
                        }
                    }

                    var qOpts = new QueryOptions()
                    {
                        WaitTime = DefaultLockWaitTime
                    };

                    while (!ct.IsCancellationRequested)
                    {
                        QueryResult<KVPair> pair;
                        try
                        {
                            pair = Client.KV.Get(Opts.Key, qOpts).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            throw new ApplicationException("failed to read lock", ex);
                        }

                        if (pair.Response != null)
                        {
                            if (pair.Response.Flags != LockFlagValue)
                            {
                                throw new LockConflictException();
                            }

                            if (IsHeld == false && pair.Response.Session == LockSession)
                            {
                                IsHeld = true;
                                MonitorLock();
                                return;
                            }

                            if (!string.IsNullOrEmpty(pair.Response.Session))
                            {
                                qOpts.WaitIndex = pair.LastIndex;
                                continue;
                            }
                        }

                        pair.Response = LockEntry(Opts.Session);
                        var acquisitionResult = Client.KV.Acquire(pair.Response).GetAwaiter().GetResult();

                        if (!acquisitionResult.Response)
                        {
                            qOpts.WaitIndex = pair.LastIndex;
                            continue;
                        }

                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        if (pair.Response == null || (IsHeld == false && pair.Response.Session != LockSession))
                        {
                            try
                            {
                                Task.Delay(DefaultLockRetryTime, ct).Wait(ct);
                            }
                            catch (TaskCanceledException)
                            {
                            }

                            continue;
                        }

                        IsHeld = true;
                        MonitorLock();
                        return;
                    }
                }
                finally
                {
                    if (ct.IsCancellationRequested || (!IsHeld && !string.IsNullOrEmpty(Opts.Session)))
                    {
                        if (_cts != null)
                        {
                            _cts.Cancel();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Unlock released the lock. It is an error to call this if the lock is not currently held.
        /// </summary>
        public void Release()
        {
            lock (_lock)
            {
                if (!IsHeld)
                {
                    throw new LockNotHeldException();
                }

                IsHeld = false;

                _cts.Cancel();

                var lockEnt = LockEntry(Opts.Session);
                Opts.Session = null;

                var releaseReq = Client.KV.Release(lockEnt);
                releaseReq.Wait();
            }
        }

        /// <summary>
        /// Destroy is used to cleanup the lock entry. It is not necessary to invoke. It will fail if the lock is in use.
        /// </summary>
        public void Destroy()
        {
            lock (_lock)
            {
                if (IsHeld)
                {
                    throw new LockHeldException();
                }

                var keyReq = Client.KV.Get(Opts.Key);
                keyReq.Wait();
                var pair = keyReq.Result.Response;

                if (pair == null)
                {
                    return;
                }

                if (pair.Flags != LockFlagValue)
                {
                    throw new LockConflictException();
                }

                if (!string.IsNullOrEmpty(pair.Session))
                {
                    throw new LockInUseException();
                }

                var removeReq = Client.KV.DeleteCAS(pair);
                removeReq.Wait();
                var didRemove = removeReq.Result.Response;

                if (!didRemove)
                {
                    throw new LockInUseException();
                }
            }
        }

        /// <summary>
        /// monitorLock is a long running routine to monitor a lock ownership. It sets IsHeld to false if we lose our leadership.
        /// </summary>
        private async void MonitorLock()
        {
            try
            {
                var opts = new QueryOptions() { Consistency = ConsistencyMode.Consistent };
                while (IsHeld && !_cts.Token.IsCancellationRequested)
                {
                    var pair = await Client.KV.Get(Opts.Key, opts);
                    if (pair.Response != null)
                    {
                        if (pair.Response.Session != Opts.Session)
                        {
                            IsHeld = false;
                            break;
                        }
                        opts.WaitIndex = pair.LastIndex;
                    }
                    else
                    {
                        IsHeld = false;
                        return;
                    }
                }
            }
            finally
            {
                IsHeld = false;
            }
        }

        /// <summary>
        /// CreateSession is used to create a new managed session
        /// </summary>
        /// <returns>The session ID</returns>
        private string CreateSession()
        {
            var se = new SessionEntry
            {
                Name = Opts.SessionName,
                TTL = Opts.SessionTTL
            };
            return Client.Session.Create(se).GetAwaiter().GetResult().Response;
        }

        /// <summary>
        /// LockEntry returns a formatted KVPair for the lock
        /// </summary>
        /// <param name="session">The session ID</param>
        /// <returns>A KVPair with the lock flag set</returns>
        private KVPair LockEntry(string session)
        {
            return new KVPair(Opts.Key)
            {
                Value = Opts.Value,
                Session = session,
                Flags = LockFlagValue
            };
        }
    }

    /// <summary>
    /// LockOptions is used to parameterize the Lock behavior.
    /// </summary>
    public class LockOptions
    {
        /// <summary>
        ///  DefaultLockSessionName is the Session Name we assign if none is provided
        /// </summary>
        private const string DefaultLockSessionName = "Consul API Lock";

        /// <summary>
        /// DefaultLockSessionTTL is the default session TTL if no Session is provided when creating a new Lock. This is used because we do not have another other check to depend upon.
        /// </summary>
        private readonly TimeSpan DefaultLockSessionTTL = TimeSpan.FromSeconds(15);

        public string Key { get; set; }
        public byte[] Value { get; set; }
        public string Session { get; set; }
        public string SessionName { get; set; }
        public TimeSpan SessionTTL { get; set; }

        public LockOptions(string key)
        {
            Key = key;
            SessionName = DefaultLockSessionName;
            SessionTTL = DefaultLockSessionTTL;
        }
    }

    public partial class Client
    {
        /// <summary>
        /// Lock returns a handle to a lock struct which can be used to acquire and release the mutex. The key used must have write permissions.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Lock Lock(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("key");
            }
            return Lock(new LockOptions(key));
        }

        /// <summary>
        /// Lock returns a handle to a lock struct which can be used to acquire and release the mutex. The key used must have write permissions.
        /// </summary>
        /// <param name="opts"></param>
        /// <returns></returns>
        public Lock Lock(LockOptions opts)
        {
            if (opts == null)
            {
                throw new ArgumentNullException("opts");
            }
            return new Lock(this) { Opts = opts };
        }
        public void ExecuteLocked(string key, Action a)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("key");
            }
            ExecuteLocked(new LockOptions(key), a);
        }
        public void ExecuteLocked(LockOptions opts, Action a)
        {
            if (a == null)
            {
                throw new ArgumentNullException("a");
            }
            var l = Lock(opts);
            l.Acquire();
            if (l.IsHeld)
            {
                a();
            }
            else
            {
                throw new LockNotHeldException("Unable to acquire the lock");
            }
            l.Release();
        }
    }
}