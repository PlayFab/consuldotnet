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
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Consul
{
    [Serializable]
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

        protected SemaphoreLimitConflictException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class SemaphoreHeldException : Exception
    {
        public SemaphoreHeldException()
        {
        }

        public SemaphoreHeldException(string message) : base(message)
        {
        }

        public SemaphoreHeldException(string message, Exception inner) : base(message, inner)
        {
        }

        protected SemaphoreHeldException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class SemaphoreNotHeldException : Exception
    {
        public SemaphoreNotHeldException()
        {
        }

        public SemaphoreNotHeldException(string message) : base(message)
        {
        }

        public SemaphoreNotHeldException(string message, Exception inner) : base(message, inner)
        {
        }

        protected SemaphoreNotHeldException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class SemaphoreInUseException : Exception
    {
        public SemaphoreInUseException()
        {
        }

        public SemaphoreInUseException(string message) : base(message)
        {
        }

        public SemaphoreInUseException(string message, Exception inner) : base(message, inner)
        {
        }

        protected SemaphoreInUseException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class SemaphoreConflictException : Exception
    {
        public SemaphoreConflictException()
        {
        }

        public SemaphoreConflictException(string message) : base(message)
        {
        }

        public SemaphoreConflictException(string message, Exception inner) : base(message, inner)
        {
        }

        protected SemaphoreConflictException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Semaphore is used to implement a distributed semaphore using the Consul KV primitives.
    /// </summary>
    public class Semaphore
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
                        throw new ArgumentOutOfRangeException("value", "Semaphore limit must be greater than 0");
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
        /// DefaultSemaphoreKey is the key used within the prefix to use for coordination between all the contenders.
        /// </summary>
        public static readonly string DefaultSemaphoreKey = ".lock";

        /// <summary>
        /// SemaphoreFlagValue is a magic flag we set to indicate a key is being used for a semaphore. It is used to detect a potential conflict with a lock.
        /// </summary>
        private const ulong SemaphoreFlagValue = 0xe0f69a2baa414de0;

        private readonly object _lock = new object();
        private readonly object _heldLock = new object();
        private bool _isheld;

        private CancellationTokenSource _cts;

        private readonly Client _client;
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

        internal Semaphore(Client c)
        {
            _client = c;
        }

        /// <summary>
        /// Acquire attempts to reserve a slot in the semaphore, blocking until success. Not providing a CancellationToken means the thread can block indefinitely until the lock is acquired.
        /// There is no notification that the semaphore slot has been lost, but IsHeld may be set to False at any time due to session invalidation, communication errors, operator intervention, etc.
        /// It is NOT safe to assume that the slot is held until Release() unless the Session is specifically created without any associated health checks.
        /// By default Consul sessions prefer liveness over safety and an application must be able to handle the session being lost.
        /// </summary>
        public void Acquire()
        {
            Acquire(CancellationToken.None);
        }

        /// <summary>
        /// Acquire attempts to reserve a slot in the semaphore, blocking until success, interrupted via CancellationToken or if an error is encountered.
        /// A provided CancellationToken can be used to abort the attempt.
        /// There is no notification that the semaphore slot has been lost, but IsHeld may be set to False at any time due to session invalidation, communication errors, operator intervention, etc.
        /// It is NOT safe to assume that the slot is held until Release() unless the Session is specifically created without any associated health checks.
        /// By default Consul sessions prefer liveness over safety and an application must be able to handle the session being lost.
        /// </summary>
        /// <param name="ct">The cancellation token to cancel semaphore acquisition</param>
        public void Acquire(CancellationToken ct)
        {
            lock (_lock)
            {
                try
                {
                    if (IsHeld)
                    {
                        // Check if we already hold the lock
                        throw new SemaphoreHeldException();
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
                            _client.Session.RenewPeriodic(Opts.SessionTTL, Opts.Session, WriteOptions.Empty, _cts.Token);
                            LockSession = Opts.Session;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("Failed to create session", ex);
                        }
                    }

                    var acquire = _client.KV.Acquire(ContenderEntry(LockSession)).GetAwaiter().GetResult().Response;

                    if (!acquire)
                    {
                        throw new ApplicationException("Failed to make contender entry");
                    }

                    var qOpts = new QueryOptions()
                    {
                        WaitTime = DefaultSemaphoreWaitTime
                    };

                    while (!ct.IsCancellationRequested)
                    {
                        QueryResult<KVPair[]> pairs;
                        try
                        {
                            pairs = _client.KV.List(Opts.Prefix, qOpts).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            throw new ApplicationException("Failed to read prefix", ex);
                        }

                        var lockPair = FindLock(pairs.Response);

                        if (lockPair.Flags != SemaphoreFlagValue)
                        {
                            throw new SemaphoreConflictException();
                        }

                        var semaphoreLock = DecodeLock(lockPair);

                        if (semaphoreLock.Limit != Opts.Limit)
                        {
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
                            return;
                        }

                        if (!_client.KV.CAS(newLock).GetAwaiter().GetResult().Response)
                        {
                            continue;
                        }
                        IsHeld = true;
                        MonitorLock(LockSession);
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
                        _client.KV.Delete(ContenderEntry(LockSession).Key).GetAwaiter().GetResult();
                    }
                }
            }
        }

        /// <summary>
        /// Release is used to voluntarily give up our semaphore slot. It is an error to call this if the semaphore has not been acquired.
        /// </summary>
        public void Release()
        {
            lock (_lock)
            {
                if (!IsHeld)
                {
                    throw new SemaphoreNotHeldException();
                }

                IsHeld = false;

                _cts.Cancel();

                var lockSession = LockSession;
                LockSession = null;

                var key = string.Join("/", Opts.Prefix, DefaultSemaphoreKey);

                var didSet = false;

                var holders = 0;

                while (!didSet)
                {
                    var pairReq = _client.KV.Get(key);
                    pairReq.Wait();
                    var pair = pairReq.Result;

                    if (pair.Response == null)
                    {
                        pair.Response = new KVPair(key);
                    }

                    var semaphoreLock = DecodeLock(pair.Response);

                    if (semaphoreLock.Holders.ContainsKey(lockSession))
                    {
                        semaphoreLock.Holders.Remove(lockSession);
                        var newLock = EncodeLock(semaphoreLock, pair.Response.ModifyIndex);

                        holders = semaphoreLock.Holders.Count;

                        var setReq = _client.KV.CAS(newLock);
                        setReq.Wait();
                        didSet = setReq.Result.Response;
                    }
                    else
                    {
                        holders = semaphoreLock.Holders.Count;
                        break;
                    }
                }

                var contenderKey = string.Join("/", Opts.Prefix, lockSession);

                if (holders == 0)
                {
                    Task.WaitAll(_client.KV.Delete(contenderKey), _client.KV.Delete(key));
                }
                else
                {
                    _client.KV.Delete(contenderKey).Wait();
                }
            }
        }

        /// <summary>
        /// Destroy is used to cleanup the semaphore entry. It is not necessary to invoke. It will fail if the semaphore is in use.
        /// </summary>
        public void Destroy()
        {
            lock (_lock)
            {
                if (IsHeld)
                {
                    throw new SemaphoreHeldException();
                }

                QueryResult<KVPair[]> pairs;
                try
                {
                    var pairReq = _client.KV.List(Opts.Prefix);
                    pairReq.Wait();
                    pairs = pairReq.Result;
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("failed to read prefix", ex);
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

                var removeReq = _client.KV.DeleteCAS(lockPair);
                removeReq.Wait();
                var didRemove = removeReq.Result.Response;

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
        private async void MonitorLock(string lockSession)
        {
            try
            {
                var opts = new QueryOptions() {Consistency = ConsistencyMode.Consistent};
                while (IsHeld && !_cts.Token.IsCancellationRequested)
                {
                    var pairs = await _client.KV.List(Opts.Prefix, opts);
                    if (pairs.Response != null)
                    {
                        var lockPair = FindLock(pairs.Response);
                        var semaphoreLock = DecodeLock(lockPair);
                        PruneDeadHolders(semaphoreLock, pairs.Response);
                        if (semaphoreLock.Holders.ContainsKey(lockSession))
                        {
                            opts.WaitIndex = pairs.LastIndex;
                        }
                        else
                        {
                            IsHeld = false;
                            return;
                        }
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
            return _client.Session.Create(se).GetAwaiter().GetResult().Response;
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
                return pairs.FirstOrDefault(p => p.Key == key) ?? new KVPair(key) {Flags = SemaphoreFlagValue};
            }
            return new KVPair(key) {Flags = SemaphoreFlagValue};
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
                return new SemaphoreLock() {Limit = Opts.Limit};
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
                    throw new ArgumentException("Semaphore prefix cannot be null or empty", "value");
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
                    throw new ArgumentOutOfRangeException("value", "Semaphore limit must be greater than 0");
                }
            }
        }

        public byte[] Value { get; set; }
        public string Session { get; set; }
        public string SessionName { get; set; }
        public TimeSpan SessionTTL { get; set; }

        public SemaphoreOptions(string prefix, int limit)
        {
            Prefix = prefix;
            Limit = limit;
            SessionName = DefaultLockSessionName;
            SessionTTL = DefaultLockSessionTTL;
        }
    }

    public partial class Client
    {
        /// <summary>
        /// Used to created a Semaphore which will operate at the given KV prefix and uses the given limit for the semaphore.
        /// The prefix must have write privileges, and the limit must be agreed upon by all contenders.
        /// </summary>
        /// <param name="prefix">The keyspace prefix (e.g. "locks/semaphore")</param>
        /// <param name="limit">The number of available semaphore slots</param>
        /// <returns>An unlocked semaphore</returns>
        public Semaphore Semaphore(string prefix, int limit)
        {
            return Semaphore(new SemaphoreOptions(prefix, limit));
        }

        /// <summary>
        /// SemaphoreOpts is used to create a Semaphore with the given options.
        /// The prefix must have write privileges, and the limit must be agreed upon by all contenders.
        /// If a Session is not provided, one will be created.
        /// </summary>
        /// <param name="opts">The semaphore options</param>
        /// <returns>An unlocked semaphore</returns>
        public Semaphore Semaphore(SemaphoreOptions opts)
        {
            if (opts == null)
            {
                throw new ArgumentNullException("opts");
            }
            return new Semaphore(this) {Opts = opts};
        }
    }
}