// -----------------------------------------------------------------------
//  <copyright file="Semaphore.cs" company="PlayFab Inc">
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
#if !(CORECLR || PORTABLE || PORTABLE40)
using System.Security.Permissions;
using System.Runtime.Serialization;
#endif

namespace Consul
{
#if !(CORECLR || PORTABLE || PORTABLE40)
    [Serializable]
#endif
    public class SemaphoreLimitConflictException : Exception
    {
        public int RemoteLimit { get; private set; }
        public int LocalLimit { get; private set; }

        public SemaphoreLimitConflictException()
        {
        }

        public SemaphoreLimitConflictException(string message, int remoteLimit, int localLimit)
            : base(message)
        {
            RemoteLimit = remoteLimit;
            LocalLimit = localLimit;
        }

        public SemaphoreLimitConflictException(string message, int remoteLimit, int localLimit, Exception inner)
            : base(message, inner)
        {
            RemoteLimit = remoteLimit;
            LocalLimit = localLimit;
        }

#if !(CORECLR || PORTABLE || PORTABLE40)
        protected SemaphoreLimitConflictException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("RemoteLimit", RemoteLimit);
            info.AddValue("LocalLimit", LocalLimit);
        }
#endif
    }

#if !(CORECLR || PORTABLE || PORTABLE40)
    [Serializable]
#endif
    public class SemaphoreHeldException : Exception
    {
        public SemaphoreHeldException()
        {
        }

        public SemaphoreHeldException(string message)
            : base(message)
        {
        }

        public SemaphoreHeldException(string message, Exception inner)
            : base(message, inner)
        {
        }
#if !(CORECLR || PORTABLE || PORTABLE40)
        protected SemaphoreHeldException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
#endif
    }

#if !(CORECLR || PORTABLE || PORTABLE40)
    [Serializable]
#endif
    public class SemaphoreNotHeldException : Exception
    {
        public SemaphoreNotHeldException()
        {
        }

        public SemaphoreNotHeldException(string message)
            : base(message)
        {
        }

        public SemaphoreNotHeldException(string message, Exception inner)
            : base(message, inner)
        {
        }
#if !(CORECLR || PORTABLE || PORTABLE40)
        protected SemaphoreNotHeldException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
#endif
    }

#if !(CORECLR || PORTABLE || PORTABLE40)
    [Serializable]
#endif
    public class SemaphoreInUseException : Exception
    {
        public SemaphoreInUseException()
        {
        }

        public SemaphoreInUseException(string message)
            : base(message)
        {
        }

        public SemaphoreInUseException(string message, Exception inner)
            : base(message, inner)
        {
        }

#if !(CORECLR || PORTABLE || PORTABLE40)
        protected SemaphoreInUseException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
#endif

    }

#if !(CORECLR || PORTABLE || PORTABLE40)
    [Serializable]
#endif
    public class SemaphoreConflictException : Exception
    {
        public SemaphoreConflictException()
        {
        }

        public SemaphoreConflictException(string message)
            : base(message)
        {
        }

        public SemaphoreConflictException(string message, Exception inner)
            : base(message, inner)
        {
        }
#if !(CORECLR || PORTABLE || PORTABLE40)
        protected SemaphoreConflictException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
#endif
    }

#if !(CORECLR || PORTABLE || PORTABLE40)
    [Serializable]
#endif
    public class SemaphoreMaxAttemptsReachedException : Exception
    {
        public SemaphoreMaxAttemptsReachedException() { }
        public SemaphoreMaxAttemptsReachedException(string message) : base(message) { }
        public SemaphoreMaxAttemptsReachedException(string message, Exception inner) : base(message, inner) { }
#if !(CORECLR || PORTABLE || PORTABLE40)
        protected SemaphoreMaxAttemptsReachedException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
#endif
    }

    /// <summary>
    /// Semaphore is used to implement a distributed semaphore using the Consul KV primitives.
    /// </summary>
    public class Semaphore : IDistributedSemaphore
    {
        /// <summary>
        /// SemaphoreLock is written under the DefaultSemaphoreKey and is used to coordinate between all the contenders.
        /// </summary>
        private class SemaphoreLock
        {
            private int _limit;

            [JsonProperty]
            internal int Limit
            {
                get { return _limit; }
                set
                {
                    if (value > 0)
                    {
                        _limit = value;
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(nameof(Limit), "Semaphore limit must be greater than 0");
                    }
                }
            }

            [JsonProperty]
            internal Dictionary<string, bool> Holders { get; set; }

            internal SemaphoreLock()
            {
                Holders = new Dictionary<string, bool>();
            }
        }

        /// <summary>
        /// DefaultSemaphoreWaitTime is how long we block for at a time to check if semaphore acquisition is possible. This affects the minimum time it takes to cancel a Semaphore acquisition.
        /// </summary>
        public static readonly TimeSpan DefaultSemaphoreWaitTime = TimeSpan.FromSeconds(15);

        /// <summary>
        /// DefaultSemaphoreRetryTime is how long we wait after a failed lock acquisition before attempting to do the lock again. This is so that once a lock-delay is in affect, we do not hot loop retrying the acquisition.
        /// </summary>
        public static readonly TimeSpan DefaultSemaphoreRetryTime = TimeSpan.FromSeconds(5);

        /// <summary>
        /// DefaultMonitorRetryTime is how long we wait after a failed monitor check
        /// of a semaphore (500 response code). This allows the monitor to ride out brief
        /// periods of unavailability, subject to the MonitorRetries setting in the
        /// lock options which is by default set to 0, disabling this feature.
        /// </summary>
        public static readonly TimeSpan DefaultMonitorRetryTime = TimeSpan.FromSeconds(2);

        /// <summary>
        /// DefaultSemaphoreKey is the key used within the prefix to use for coordination between all the contenders.
        /// </summary>
        public static readonly string DefaultSemaphoreKey = ".lock";

        /// <summary>
        /// SemaphoreFlagValue is a magic flag we set to indicate a key is being used for a semaphore. It is used to detect a potential conflict with a lock.
        /// </summary>
        private const ulong SemaphoreFlagValue = 0xe0f69a2baa414de0;

        private readonly AsyncLock _mutex = new AsyncLock();
        private readonly object _heldLock = new object();
        private bool _isheld;
        private int _retries;

        private CancellationTokenSource _cts;

        private readonly ConsulClient _client;
        private Task _sessionRenewTask;
        private Task _monitorTask;

        internal SemaphoreOptions Opts { get; set; }

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

        internal string LockSession { get; set; }

        internal Semaphore(ConsulClient c)
        {
            _client = c;
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Acquire attempts to reserve a slot in the semaphore, blocking until success. Not providing a CancellationToken means the thread can block indefinitely until the lock is acquired.
        /// There is no notification that the semaphore slot has been lost, but IsHeld may be set to False at any time due to session invalidation, communication errors, operator intervention, etc.
        /// It is NOT safe to assume that the slot is held until Release() unless the Session is specifically created without any associated health checks.
        /// By default Consul sessions prefer liveness over safety and an application must be able to handle the session being lost.
        /// </summary>
        public Task<CancellationToken> Acquire()
        {
            return Acquire(CancellationToken.None);
        }

        /// <summary>
        /// Acquire attempts to reserve a slot in the semaphore, blocking until success, interrupted via CancellationToken or if an error is encountered.
        /// A provided CancellationToken can be used to abort the attempt.
        /// There is no notification that the semaphore slot has been lost, but IsHeld may be set to False at any time due to session invalidation, communication errors, operator intervention, etc.
        /// It is NOT safe to assume that the slot is held until Release() unless the Session is specifically created without any associated health checks.
        /// By default Consul sessions prefer liveness over safety and an application must be able to handle the session being lost.
        /// </summary>
        /// <param name="ct">The cancellation token to cancel semaphore acquisition</param>
        public async Task<CancellationToken> Acquire(CancellationToken ct)
        {
            try
            {
                using (await _mutex.LockAsync().ConfigureAwait(false))
                {
                    if (IsHeld)
                    {
                        // Check if we already hold the lock
                        throw new SemaphoreHeldException();
                    }
                    // Don't overwrite the CancellationTokenSource until AFTER we've tested for holding, since there might be tasks that are currently running for this lock.
                    DisposeCancellationTokenSource();
                    _cts = new CancellationTokenSource();

                    // Check if we need to create a session first
                    if (string.IsNullOrEmpty(Opts.Session))
                    {
                        try
                        {
                            LockSession = await CreateSession().ConfigureAwait(false);
                            _sessionRenewTask = _client.Session.RenewPeriodic(Opts.SessionTTL, LockSession, WriteOptions.Default, _cts.Token);
                        }
                        catch (Exception ex)
                        {
                            DisposeCancellationTokenSource();
                            throw new InvalidOperationException("Failed to create session", ex);
                        }
                    }
                    else
                    {
                        LockSession = Opts.Session;
                    }

                    var contender = (await _client.KV.Acquire(ContenderEntry(LockSession)).ConfigureAwait(false)).Response;
                    if (!contender)
                    {
                        DisposeCancellationTokenSource();
                        throw new KeyNotFoundException("Failed to make contender entry");
                    }

                    var qOpts = new QueryOptions()
                    {
                        WaitTime = Opts.SemaphoreWaitTime
                    };

                    var attempts = 0;
                    var start = DateTime.UtcNow;

                    while (!ct.IsCancellationRequested)
                    {
                        if (attempts > 0 && Opts.SemaphoreTryOnce)
                        {
                            var elapsed = DateTime.UtcNow.Subtract(start);
                            if (elapsed > qOpts.WaitTime)
                            {
                                DisposeCancellationTokenSource();
                                throw new SemaphoreMaxAttemptsReachedException("SemaphoreTryOnce is set and the semaphore is already at maximum capacity");
                            }
                            qOpts.WaitTime -= elapsed;
                        }

                        attempts++;

                        QueryResult<KVPair[]> pairs;
                        try
                        {
                            pairs = await _client.KV.List(Opts.Prefix, qOpts).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            DisposeCancellationTokenSource();
                            throw new KeyNotFoundException("Failed to read prefix", ex);
                        }

                        var lockPair = FindLock(pairs.Response);
                        if (lockPair.Flags != SemaphoreFlagValue)
                        {
                            DisposeCancellationTokenSource();
                            throw new SemaphoreConflictException();
                        }

                        var semaphoreLock = DecodeLock(lockPair);
                        if (semaphoreLock.Limit != Opts.Limit)
                        {
                            DisposeCancellationTokenSource();
                            throw new SemaphoreLimitConflictException(
                                string.Format("Semaphore limit conflict (lock: {0}, local: {1})", semaphoreLock.Limit,
                                    Opts.Limit),
                                semaphoreLock.Limit, Opts.Limit);
                        }

                        PruneDeadHolders(semaphoreLock, pairs.Response);
                        if (semaphoreLock.Holders.Count >= semaphoreLock.Limit)
                        {
                            qOpts.WaitIndex = pairs.LastIndex;
                            continue;
                        }

                        semaphoreLock.Holders[LockSession] = true;

                        var newLock = EncodeLock(semaphoreLock, lockPair.ModifyIndex);

                        if (ct.IsCancellationRequested)
                        {
                            DisposeCancellationTokenSource();
                            throw new TaskCanceledException();
                        }

                        // Handle the case of not getting the lock
                        if (!(await _client.KV.CAS(newLock).ConfigureAwait(false)).Response)
                        {
                            continue;
                        }

                        IsHeld = true;
                        _monitorTask = MonitorLock(LockSession);
                        return _cts.Token;
                    }
                    DisposeCancellationTokenSource();
                    throw new SemaphoreNotHeldException("Unable to acquire the semaphore with Consul");
                }
            }
            finally
            {
                if (ct.IsCancellationRequested || (!IsHeld && !string.IsNullOrEmpty(Opts.Session)))
                {
                    DisposeCancellationTokenSource();
                    await _client.KV.Delete(ContenderEntry(LockSession).Key).ConfigureAwait(false);
                    if (_sessionRenewTask != null)
                    {
                        try
                        {
                            await _monitorTask.ConfigureAwait(false);
                            await _sessionRenewTask.ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // Ignore Exceptions from the tasks during Release, since if the Renew task died, the developer will be Super Confused if they see the exception during Release.
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Release is used to voluntarily give up our semaphore slot. It is an error to call this if the semaphore has not been acquired.
        /// </summary>
        public async Task Release(CancellationToken ct = default(CancellationToken))
        {
            try
            {
                using (await _mutex.LockAsync().ConfigureAwait(false))
                {
                    if (!IsHeld)
                    {
                        throw new SemaphoreNotHeldException();
                    }
                    IsHeld = false;

                    DisposeCancellationTokenSource();

                    var lockSession = LockSession;
                    LockSession = null;

                    var key = string.Join("/", Opts.Prefix, DefaultSemaphoreKey);

                    var didSet = false;

                    while (!didSet)
                    {
                        var pair = await _client.KV.Get(key, ct).ConfigureAwait(false);

                        if (pair.Response == null)
                        {
                            pair.Response = new KVPair(key);
                        }

                        var semaphoreLock = DecodeLock(pair.Response);

                        if (semaphoreLock.Holders.ContainsKey(lockSession))
                        {
                            semaphoreLock.Holders.Remove(lockSession);
                            var newLock = EncodeLock(semaphoreLock, pair.Response.ModifyIndex);

                            didSet = (await _client.KV.CAS(newLock, ct).ConfigureAwait(false)).Response;
                        }
                        else
                        {
                            break;
                        }
                    }

                    var contenderKey = string.Join("/", Opts.Prefix, lockSession);

                    await _client.KV.Delete(contenderKey, ct).ConfigureAwait(false);
                }
            }
            finally
            {
                if (_sessionRenewTask != null)
                {
                    try
                    {
                        await _sessionRenewTask.ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // Ignore Exceptions from the tasks during Release, since if the Renew task died, the developer will be Super Confused if they see the exception during Release.
                    }
                }
            }
        }

        /// <summary>
        /// Destroy is used to cleanup the semaphore entry. It is not necessary to invoke. It will fail if the semaphore is in use.
        /// </summary>
        public async Task Destroy(CancellationToken ct = default(CancellationToken))
        {
            using (await _mutex.LockAsync().ConfigureAwait(false))
            {
                if (IsHeld)
                {
                    throw new SemaphoreHeldException();
                }

                QueryResult<KVPair[]> pairs;
                try
                {
                    pairs = await _client.KV.List(Opts.Prefix, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new KeyNotFoundException("failed to read prefix", ex);
                }

                var lockPair = FindLock(pairs.Response);

                if (lockPair.ModifyIndex == 0)
                {
                    return;
                }
                if (lockPair.Flags != SemaphoreFlagValue)
                {
                    throw new SemaphoreConflictException();
                }

                var semaphoreLock = DecodeLock(lockPair);

                PruneDeadHolders(semaphoreLock, pairs.Response);

                if (semaphoreLock.Holders.Count > 0)
                {
                    throw new SemaphoreInUseException();
                }

                var didRemove = (await _client.KV.DeleteCAS(lockPair, ct).ConfigureAwait(false)).Response;

                if (!didRemove)
                {
                    throw new SemaphoreInUseException();
                }
            }
        }

        /// <summary>
        /// monitorLock is a long running routine to monitor a semaphore ownership
        /// It sets IsHeld to false if we lose our slot.
        /// </summary>
        /// <param name="lockSession">The session ID to monitor</param>
        private Task MonitorLock(string lockSession)
        {
            return Task.Factory.StartNew(async () =>
            {
                try
                {
                    var opts = new QueryOptions() { Consistency = ConsistencyMode.Consistent };
                    _retries = Opts.MonitorRetries;
                    while (IsHeld && !_cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var pairs = await _client.KV.List(Opts.Prefix, opts).ConfigureAwait(false);
                            if (pairs.Response != null)
                            {
                                _retries = Opts.MonitorRetries;

                                var lockPair = FindLock(pairs.Response);
                                var semaphoreLock = DecodeLock(lockPair);
                                PruneDeadHolders(semaphoreLock, pairs.Response);

                                // Slot is no longer held! Shut down everything.
                                if (!semaphoreLock.Holders.ContainsKey(lockSession))
                                {

                                    IsHeld = false;
                                    DisposeCancellationTokenSource();
                                    return;
                                }

                                // Semaphore is still held, start a blocking query
                                opts.WaitIndex = pairs.LastIndex;
                                continue;
                            }
                            // Failsafe in case the KV store is unavailable
                            else
                            {
                                IsHeld = false;
                                DisposeCancellationTokenSource();
                                return;
                            }
                        }
                        catch (ConsulRequestException)
                        {
                            if (_retries > 0)
                            {
                                await Task.Delay(Opts.MonitorRetryTime, _cts.Token).ConfigureAwait(false);
                                _retries--;
                                opts.WaitIndex = 0;
                                continue;
                            }
                            throw;
                        }
                        catch (OperationCanceledException)
                        {
                            // Ignore and retry since this could be the underlying HTTPClient being swapped out/disposed of.
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    }
                }
                finally
                {
                    IsHeld = false;
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        /// <summary>
        /// CreateSession is used to create a new managed session
        /// </summary>
        /// <returns>The session ID</returns>
        private async Task<string> CreateSession()
        {
            var se = new SessionEntry
            {
                Name = Opts.SessionName,
                TTL = Opts.SessionTTL
            };
            return (await _client.Session.Create(se).ConfigureAwait(false)).Response;
        }

        /// <summary>
        /// contenderEntry returns a formatted KVPair for the contender
        /// </summary>
        /// <param name="session">The session ID</param>
        /// <returns>The K/V pair with the Semaphore flag set</returns>
        private KVPair ContenderEntry(string session)
        {
            return new KVPair(string.Join("/", Opts.Prefix, session))
            {
                Value = Opts.Value,
                Session = session,
                Flags = SemaphoreFlagValue
            };
        }

        /// <summary>
        /// findLock is used to find the KV Pair which is used for coordination
        /// </summary>
        /// <param name="pairs">A list of KVPairs</param>
        /// <returns>The semaphore storage KV pair</returns>
        private KVPair FindLock(KVPair[] pairs)
        {
            var key = string.Join("/", Opts.Prefix, DefaultSemaphoreKey);
            if (pairs != null)
            {
                return pairs.FirstOrDefault(p => p.Key == key) ?? new KVPair(key) { Flags = SemaphoreFlagValue };
            }
            return new KVPair(key) { Flags = SemaphoreFlagValue };
        }

        /// <summary>
        /// DecodeLock is used to decode a SemaphoreLock from an entry in Consul
        /// </summary>
        /// <param name="pair"></param>
        /// <returns>A decoded lock or a new, blank lock</returns>
        private SemaphoreLock DecodeLock(KVPair pair)
        {
            if (pair == null || pair.Value == null)
            {
                return new SemaphoreLock() { Limit = Opts.Limit };
            }

            return JsonConvert.DeserializeObject<SemaphoreLock>(Encoding.UTF8.GetString(pair.Value));
        }

        /// <summary>
        /// EncodeLock is used to encode a SemaphoreLock into a KVPair that can be PUT
        /// </summary>
        /// <param name="l">The SemaphoreLock data</param>
        /// <param name="oldIndex">The index that the data was fetched from, for CAS</param>
        /// <returns>A K/V pair with the lock data encoded in the Value field</returns>
        private KVPair EncodeLock(SemaphoreLock l, ulong oldIndex)
        {
            var jsonValue = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(l));

            return new KVPair(string.Join("/", Opts.Prefix, DefaultSemaphoreKey))
            {
                Value = jsonValue,
                Flags = SemaphoreFlagValue,
                ModifyIndex = oldIndex
            };
        }

        /// <summary>
        /// PruneDeadHolders is used to remove all the dead lock holders
        /// </summary>
        /// <param name="l">The SemaphoreLock to prune</param>
        /// <param name="pairs">The list of K/V that currently hold locks</param>
        private static void PruneDeadHolders(SemaphoreLock l, IEnumerable<KVPair> pairs)
        {
            var alive = new HashSet<string>();
            foreach (var pair in pairs)
            {
                if (!string.IsNullOrEmpty(pair.Session))
                {
                    alive.Add(pair.Session);
                }
            }

            var newHolders = new Dictionary<string, bool>(l.Holders);

            foreach (var holder in l.Holders)
            {
                if (!alive.Contains(holder.Key))
                {
                    newHolders.Remove(holder.Key);
                }
            }

            l.Holders = newHolders;
        }

        private void DisposeCancellationTokenSource()
        {
            // Make a copy of the reference to the CancellationTokenSource in case it gets removed before we finish.
            // It's okay to cancel and dispose of them twice, it doesn't cause exceptions.
            var cts = _cts;
            if (cts != null)
            {
                Interlocked.CompareExchange(ref _cts, null, cts);
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    /// <summary>
    /// SemaphoreOptions is used to parameterize the Semaphore
    /// </summary>
    public class SemaphoreOptions
    {
        /// <summary>
        ///  DefaultSemaphoreSessionName is the Session Name we assign if none is provided
        /// </summary>
        private const string DefaultLockSessionName = "Consul API Semaphore";

        /// <summary>
        /// DefaultSemaphoreSessionTTL is the default session TTL if no Session is provided when creating a new Semaphore. This is used because we do not have any other check to depend upon.
        /// </summary>
        private readonly TimeSpan DefaultLockSessionTTL = TimeSpan.FromSeconds(15);

        private string _prefix;

        public string Prefix
        {
            get { return _prefix; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _prefix = value;
                }
                else
                {
                    throw new ArgumentException("Semaphore prefix cannot be null or empty", nameof(Prefix));
                }
            }
        }

        private int _limit;

        public int Limit
        {
            get { return _limit; }
            set
            {
                if (value > 0)
                {
                    _limit = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(Limit), "Semaphore limit must be greater than 0");
                }
            }
        }

        public byte[] Value { get; set; }
        public string Session { get; set; }
        public string SessionName { get; set; }
        public TimeSpan SessionTTL { get; set; }
        public int MonitorRetries { get; set; }
        public TimeSpan MonitorRetryTime { get; set; }
        public TimeSpan SemaphoreWaitTime { get; set; }
        public bool SemaphoreTryOnce { get; set; }

        public SemaphoreOptions(string prefix, int limit)
        {
            Prefix = prefix;
            Limit = limit;
            SessionName = DefaultLockSessionName;
            SessionTTL = DefaultLockSessionTTL;
            MonitorRetryTime = Semaphore.DefaultMonitorRetryTime;
            SemaphoreWaitTime = Semaphore.DefaultSemaphoreWaitTime;
        }
    }

    public partial class ConsulClient : IConsulClient
    {
        /// <summary>
        /// Used to created a Semaphore which will operate at the given KV prefix and uses the given limit for the semaphore.
        /// The prefix must have write privileges, and the limit must be agreed upon by all contenders.
        /// </summary>
        /// <param name="prefix">The keyspace prefix (e.g. "locks/semaphore")</param>
        /// <param name="limit">The number of available semaphore slots</param>
        /// <returns>An unlocked semaphore</returns>
        public IDistributedSemaphore Semaphore(string prefix, int limit)
        {
            if (prefix == null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }
            return Semaphore(new SemaphoreOptions(prefix, limit));
        }

        /// <summary>
        /// SemaphoreOpts is used to create a Semaphore with the given options.
        /// The prefix must have write privileges, and the limit must be agreed upon by all contenders.
        /// If a Session is not provided, one will be created.
        /// </summary>
        /// <param name="opts">The semaphore options</param>
        /// <returns>An unlocked semaphore</returns>
        public IDistributedSemaphore Semaphore(SemaphoreOptions opts)
        {
            if (opts == null)
            {
                throw new ArgumentNullException(nameof(opts));
            }
            return new Semaphore(this) { Opts = opts };
        }
        public Task<IDistributedSemaphore> AcquireSemaphore(string prefix, int limit, CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(prefix))
            {
                throw new ArgumentNullException(nameof(prefix));
            }
            if (limit <= 0)
            {
                throw new ArgumentNullException(nameof(limit));
            }
            return AcquireSemaphore(new SemaphoreOptions(prefix, limit), ct);
        }
        public async Task<IDistributedSemaphore> AcquireSemaphore(SemaphoreOptions opts, CancellationToken ct = default(CancellationToken))
        {
            if (opts == null)
            {
                throw new ArgumentNullException(nameof(opts));
            }

            var semaphore = Semaphore(opts);
            await semaphore.Acquire(ct).ConfigureAwait(false);
            return semaphore;
        }

        public Task ExecuteInSemaphore(string prefix, int limit, Action a, CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(prefix))
            {
                throw new ArgumentNullException(nameof(prefix));
            }
            if (limit <= 0)
            {
                throw new ArgumentNullException(nameof(limit));
            }
            return ExecuteInSemaphore(new SemaphoreOptions(prefix, limit), a, ct);
        }

        public async Task ExecuteInSemaphore(SemaphoreOptions opts, Action a, CancellationToken ct = default(CancellationToken))
        {
            if (opts == null)
            {
                throw new ArgumentNullException(nameof(opts));
            }
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            var semaphore = await AcquireSemaphore(opts, ct).ConfigureAwait(false);

            try
            {
                if (!semaphore.IsHeld)
                {
                    throw new LockNotHeldException("Could not obtain the lock");
                }
                a();
            }
            finally
            {
                await semaphore.Release(ct).ConfigureAwait(false);
            }
        }
    }
}