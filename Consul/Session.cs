// -----------------------------------------------------------------------
//  <copyright file="Session.cs" company="PlayFab Inc">
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
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Consul
{
    public class SessionBehavior : IEquatable<SessionBehavior>
    {
        public string Behavior { get; private set; }

        public static SessionBehavior Release
        {
            get { return new SessionBehavior() {Behavior = "release"}; }
        }

        public static SessionBehavior Delete
        {
            get { return new SessionBehavior() {Behavior = "delete"}; }
        }

        public bool Equals(SessionBehavior other)
        {
            return other != null && Behavior.Equals(other.Behavior);
        }

        public override bool Equals(object other)
        {
            // other could be a reference type, the is operator will return false if null
            var a = other as SessionBehavior;
            return a != null && Equals(a);
        }

        public override int GetHashCode()
        {
            return Behavior.GetHashCode();
        }
    }

    public class SessionBehaviorConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((SessionBehavior) value).Behavior);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            var behavior = (string) serializer.Deserialize(reader, typeof (string));
            switch (behavior)
            {
                case "release":
                    return SessionBehavior.Release;
                case "delete":
                    return SessionBehavior.Delete;
                default:
                    throw new ArgumentException("Unknown session behavior value during deserialization");
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof (SessionBehavior);
        }
    }


    public class SessionEntry
    {
        [JsonProperty]
        public ulong CreateIndex { get; private set; }

        public string ID { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Node { get; set; }

        public List<string> Checks { get; set; }

        [JsonConverter(typeof (NanoSecTimespanConverter))]
        public TimeSpan LockDelay { get; set; }

        [JsonConverter(typeof (SessionBehaviorConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SessionBehavior Behavior { get; set; }

        [JsonConverter(typeof (DurationTimespanConverter))]
        public TimeSpan TTL { get; set; }

        public SessionEntry()
        {
            Checks = new List<string>();
        }

        public bool ShouldSerializeID()
        {
            return false;
        }

        public bool ShouldSerializeCreateIndex()
        {
            return false;
        }

        public bool ShouldSerializeLockDelay()
        {
            return LockDelay != TimeSpan.Zero;
        }

        public bool ShouldSerializeTTL()
        {
            return TTL != TimeSpan.Zero;
        }

        public bool ShouldSerializeChecks()
        {
            return Checks != null && Checks.Count != 0;
        }
    }

    /// <summary>
    /// Session can be used to query the Session endpoints
    /// </summary>
    public class Session
    {
        private class SessionCreationResult
        {
            [JsonProperty]
            internal string ID { get; set; }
        }

        private readonly Client _client;

        internal Session(Client c)
        {
            _client = c;
        }

        /// <summary>
        /// CreateNoChecks is like Create but is used specifically to create a session with no associated health checks.
        /// </summary>
        public async Task<WriteResult<string>> CreateNoChecks()
        {
            return await CreateNoChecks(new SessionEntry(), WriteOptions.Empty);
        }

        /// <summary>
        /// CreateNoChecks is like Create but is used specifically to create a session with no associated health checks.
        /// </summary>
        /// <param name="se">The SessionEntry options to use</param>
        /// <returns>A write result containing the new session ID</returns>
        public async Task<WriteResult<string>> CreateNoChecks(SessionEntry se)
        {
            return await CreateNoChecks(se, WriteOptions.Empty);
        }

        /// <summary>
        /// CreateNoChecks is like Create but is used specifically to create a session with no associated health checks.
        /// </summary>
        /// <param name="se">The SessionEntry options to use</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result containing the new session ID</returns>
        public async Task<WriteResult<string>> CreateNoChecks(SessionEntry se, WriteOptions q)
        {
            if (se == null)
            {
                return await Create(null, q);
            }
            var noChecksEntry = new SessionEntry()
            {
                Behavior = se.Behavior,
                Checks = new List<string>(0),
                LockDelay = se.LockDelay,
                Name = se.Name,
                Node = se.Node,
                TTL = se.TTL
            };
            return await Create(noChecksEntry, q);
        }

        /// <summary>
        /// Create makes a new session with default options.
        /// </summary>
        /// <returns>A write result containing the new session ID</returns>
        public async Task<WriteResult<string>> Create()
        {
            return await Create(null, WriteOptions.Empty);
        }

        /// <summary>
        /// Create makes a new session. Providing a session entry can customize the session. It can also be null to use defaults.
        /// </summary>
        /// <param name="se">The SessionEntry options to use</param>
        /// <returns>A write result containing the new session ID</returns>
        public async Task<WriteResult<string>> Create(SessionEntry se)
        {
            return await Create(se, WriteOptions.Empty);
        }

        /// <summary>
        /// Create makes a new session. Providing a session entry can customize the session. It can also be null to use defaults.
        /// </summary>
        /// <param name="se">The SessionEntry options to use</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result containing the new session ID</returns>
        public async Task<WriteResult<string>> Create(SessionEntry se, WriteOptions q)
        {
            var res =
                await
                    _client.CreateWriteRequest<SessionEntry, SessionCreationResult>("/v1/session/create", se, q)
                        .Execute();
            return new WriteResult<string>()
            {
                RequestTime = res.RequestTime,
                Response = res.Response.ID
            };
        }

        public async Task<WriteResult<bool>> Destroy(string id)
        {
            return await Destroy(id, WriteOptions.Empty);
        }

        /// <summary>
        /// Destroy invalidates a given session
        /// </summary>
        /// <param name="id">The session ID to destroy</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result containing the result of the session destruction</returns>
        public async Task<WriteResult<bool>> Destroy(string id, WriteOptions q)
        {
            return
                await
                    _client.CreateWriteRequest<object, bool>(string.Format("/v1/session/destroy/{0}", id), q).Execute();
        }

        public async Task<WriteResult<SessionEntry>> Renew(string id)
        {
            return await Renew(id, WriteOptions.Empty);
        }

        /// <summary>
        /// Renew renews the TTL on a given session
        /// </summary>
        /// <param name="id">The session ID to renew</param>
        /// <param name="q">Customized write options</param>
        /// <returns>An updated session entry</returns>
        public async Task<WriteResult<SessionEntry>> Renew(string id, WriteOptions q)
        {
            var res =
                await
                    _client.CreateWriteRequest<object, SessionEntry[]>(string.Format("/v1/session/renew/{0}", id), q)
                        .Execute();
            var ret = new WriteResult<SessionEntry>() {RequestTime = res.RequestTime};
            if (res.Response != null && res.Response.Length > 0)
            {
                ret.Response = res.Response[0];
            }
            return ret;
        }

        /// <summary>
        /// RenewPeriodic is used to periodically invoke Session.Renew on a session until a CancellationToken is cancelled.
        /// This is meant to be used in a long running call to ensure a session stays valid until completed.
        /// </summary>
        /// <param name="initialTTL">The initital TTL to renew for</param>
        /// <param name="id">The session ID to renew</param>
        /// <param name="ct">The CancellationToken used to stop the session from being renewed (e.g. when the long-running action completes)</param>
        public void RenewPeriodic(TimeSpan initialTTL, string id, CancellationToken ct)
        {
            RenewPeriodic(initialTTL, id, WriteOptions.Empty, ct);
        }

        /// <summary>
        /// RenewPeriodic is used to periodically invoke Session.Renew on a session until a CancellationToken is cancelled.
        /// This is meant to be used in a long running call to ensure a session stays valid until completed.
        /// </summary>
        /// <param name="initialTTL">The initital TTL to renew for</param>
        /// <param name="id">The session ID to renew</param>
        /// <param name="q">Customized write options</param>
        /// <param name="ct">The CancellationToken used to stop the session from being renewed (e.g. when the long-running action completes)</param>
        public async void RenewPeriodic(TimeSpan initialTTL, string id, WriteOptions q, CancellationToken ct)
        {
            if (q == null)
            {
                throw new ArgumentNullException("q");
            }
            var waitDuration = (int) (initialTTL.TotalMilliseconds/2);
            var lastRenewTime = DateTime.Now;
            while (!ct.IsCancellationRequested)
            {
                if (DateTime.Now.Subtract(lastRenewTime) > initialTTL)
                {
                    throw new TaskCanceledException("Session timed out");
                }

                try
                {
                    await Task.Delay(waitDuration, ct);
                }
                catch (TaskCanceledException)
                {
                }

                try
                {
                    var res = await Renew(id, q);
                    if (res.Response == null)
                    {
                        throw new TaskCanceledException("Session no longer exists");
                    }
                    initialTTL = res.Response.TTL;
                    waitDuration = (int) (initialTTL.TotalMilliseconds/2);
                    lastRenewTime = DateTime.Now;
                }
                catch (Exception)
                {
                    waitDuration = 1000;
                }
            }
            if (ct.IsCancellationRequested)
            {
                await _client.Session.Destroy(id);
            }
        }

        /// <summary>
        /// Info looks up a single session
        /// </summary>
        /// <param name="id">The session ID to look up</param>
        /// <returns>A query result containing the session information, or an empty query result if the session entry does not exist</returns>
        public async Task<QueryResult<SessionEntry>> Info(string id)
        {
            return await Info(id, QueryOptions.Empty);
        }

        /// <summary>
        /// Info looks up a single session
        /// </summary>
        /// <param name="id">The session ID to look up</param>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing the session information, or an empty query result if the session entry does not exist</returns>
        public async Task<QueryResult<SessionEntry>> Info(string id, QueryOptions q)
        {
            var res =
                await _client.CreateQueryRequest<SessionEntry[]>(string.Format("/v1/session/info/{0}", id), q).Execute();
            var ret = new QueryResult<SessionEntry>()
            {
                KnownLeader = res.KnownLeader,
                LastContact = res.LastContact,
                LastIndex = res.LastIndex,
                RequestTime = res.RequestTime
            };
            if (res.Response != null && res.Response.Length > 0)
            {
                ret.Response = res.Response[0];
            }
            return ret;
        }

        /// <summary>
        /// List gets all active sessions
        /// </summary>
        /// <returns>A query result containing list of all sessions, or an empty query result if no sessions exist</returns>
        public async Task<QueryResult<SessionEntry[]>> List()
        {
            return await List(QueryOptions.Empty);
        }

        /// <summary>
        /// List gets all active sessions
        /// </summary>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing the list of sessions, or an empty query result if no sessions exist</returns>
        public async Task<QueryResult<SessionEntry[]>> List(QueryOptions q)
        {
            return await _client.CreateQueryRequest<SessionEntry[]>("/v1/session/list", q).Execute();
        }

        /// <summary>
        /// Node gets all sessions for a node
        /// </summary>
        /// <param name="node">The node ID</param>
        /// <returns>A query result containing the list of sessions, or an empty query result if no sessions exist</returns>
        public async Task<QueryResult<SessionEntry[]>> Node(string node)
        {
            return await Node(node, QueryOptions.Empty);
        }

        /// <summary>
        /// Node gets all sessions for a node
        /// </summary>
        /// <param name="node">The node ID</param>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing the list of sessions, or an empty query result if no sessions exist</returns>
        public async Task<QueryResult<SessionEntry[]>> Node(string node, QueryOptions q)
        {
            return
                await
                    _client.CreateQueryRequest<SessionEntry[]>(string.Format("/v1/session/node/{0}", node), q).Execute();
        }
    }

    public partial class Client
    {
        private Session _session;

        /// <summary>
        /// Session returns a handle to the session endpoints
        /// </summary>
        public Session Session
        {
            get
            {
                if (_session == null)
                {
                    lock (_lock)
                    {
                        if (_session == null)
                        {
                            _session = new Session(this);
                        }
                    }
                }
                return _session;
            }
        }
    }
}