using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.Runtime.Serialization;

namespace Consul
{
    public class SessionBehavior : IEquatable<SessionBehavior>
    {
        private static readonly SessionBehavior release = new SessionBehavior() { Behavior = "release" };
        private static readonly SessionBehavior delete = new SessionBehavior() { Behavior = "delete" };

        public string Behavior { get; private set; }

        public static SessionBehavior Release
        {
            get { return release; }
        }

        public static SessionBehavior Delete
        {
            get { return delete; }
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
            serializer.Serialize(writer, ((SessionBehavior)value).Behavior);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            var behavior = (string)serializer.Deserialize(reader, typeof(string));
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
            return objectType == typeof(SessionBehavior);
        }
    }

#if !(CORECLR || PORTABLE || PORTABLE40)
    [Serializable]
#endif
    public class SessionExpiredException : Exception
    {
        public SessionExpiredException() { }
        public SessionExpiredException(string message) : base(message) { }
        public SessionExpiredException(string message, Exception inner) : base(message, inner) { }
#if !(CORECLR || PORTABLE || PORTABLE40)
        protected SessionExpiredException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
#endif

    }

#if !(CORECLR || PORTABLE || PORTABLE40)
    [Serializable]
#endif
    public class SessionCreationException : Exception
    {
        public SessionCreationException() { }
        public SessionCreationException(string message) : base(message) { }
        public SessionCreationException(string message, Exception inner) : base(message, inner) { }
#if !(CORECLR || PORTABLE || PORTABLE40)
        protected SessionCreationException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
#endif
    }

    public class SessionEntry
    {
        public ulong CreateIndex { get; set; }

        public string ID { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Node { get; set; }

        public List<string> Checks { get; set; }

        [JsonConverter(typeof(NanoSecTimespanConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TimeSpan? LockDelay { get; set; }

        [JsonConverter(typeof(SessionBehaviorConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SessionBehavior Behavior { get; set; }

        [JsonConverter(typeof(DurationTimespanConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TimeSpan? TTL { get; set; }

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

        public bool ShouldSerializeChecks()
        {
            return Checks != null && Checks.Count != 0;
        }
    }

    /// <summary>
    /// Session can be used to query the Session endpoints
    /// </summary>
    public class Session : ISessionEndpoint
    {
        private class SessionCreationResult
        {
            [JsonProperty]
            internal string ID { get; set; }
        }

        private readonly ConsulClient _client;

        internal Session(ConsulClient c)
        {
            _client = c;
        }

        /// <summary>
        /// RenewPeriodic is used to periodically invoke Session.Renew on a session until a CancellationToken is cancelled.
        /// This is meant to be used in a long running call to ensure a session stays valid until completed.
        /// </summary>
        /// <param name="initialTTL">The initital TTL to renew for</param>
        /// <param name="id">The session ID to renew</param>
        /// <param name="ct">The CancellationToken used to stop the session from being renewed (e.g. when the long-running action completes)</param>
        public Task RenewPeriodic(TimeSpan initialTTL, string id, CancellationToken ct)
        {
            return RenewPeriodic(initialTTL, id, WriteOptions.Default, ct);
        }

        /// <summary>
        /// RenewPeriodic is used to periodically invoke Session.Renew on a session until a CancellationToken is cancelled.
        /// This is meant to be used in a long running call to ensure a session stays valid until completed.
        /// </summary>
        /// <param name="initialTTL">The initital TTL to renew for</param>
        /// <param name="id">The session ID to renew</param>
        /// <param name="q">Customized write options</param>
        /// <param name="ct">The CancellationToken used to stop the session from being renewed (e.g. when the long-running action completes)</param>
        public Task RenewPeriodic(TimeSpan initialTTL, string id, WriteOptions q, CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                if (q == null)
                {
                    throw new ArgumentNullException(nameof(q));
                }
                var waitDuration = (int)(initialTTL.TotalMilliseconds / 2);
                var lastRenewTime = DateTime.Now;
                Exception lastException = new SessionExpiredException(string.Format("Session expired: {0}", id));
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        if (DateTime.Now.Subtract(lastRenewTime) > initialTTL)
                        {
                            throw lastException;
                        }
                        try
                        {
                            await Task.Delay(waitDuration, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // Ignore OperationCanceledException because it means the wait cancelled in response to a CancellationToken being cancelled.
                        }

                        try
                        {
                            var res = await Renew(id, q).ConfigureAwait(false);
                            initialTTL = res.Response.TTL ?? TimeSpan.Zero;
                            waitDuration = (int)(initialTTL.TotalMilliseconds / 2);
                            lastRenewTime = DateTime.Now;
                        }
                        catch (SessionExpiredException)
                        {
                            throw;
                        }
                        catch (OperationCanceledException)
                        {
                            // Ignore OperationCanceledException/TaskCanceledException since it means the session no longer exists or the task is stopping.
                        }
                        catch (Exception ex)
                        {
                            waitDuration = 1000;
                            lastException = ex;
                        }
                    }
                }
                finally
                {
                    if (ct.IsCancellationRequested)
                    {
                        await _client.Session.Destroy(id).ConfigureAwait(false);
                    }
                }
            });
        }

        /// <summary>
        /// Create makes a new session. Providing a session entry can customize the session. It can also be null to use defaults.
        /// </summary>
        /// <param name="se">The SessionEntry options to use</param>
        /// <returns>A write result containing the new session ID</returns>

        public Task<WriteResult<string>> Create(CancellationToken ct = default(CancellationToken))
        {
            return Create(null, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Create makes a new session with default options.
        /// </summary>
        /// <returns>A write result containing the new session ID</returns>
        public Task<WriteResult<string>> Create(SessionEntry se, CancellationToken ct = default(CancellationToken))
        {
            return Create(se, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Create makes a new session. Providing a session entry can customize the session. It can also be null to use defaults.
        /// </summary>
        /// <param name="se">The SessionEntry options to use</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result containing the new session ID</returns>
        public async Task<WriteResult<string>> Create(SessionEntry se, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.Put<SessionEntry, SessionCreationResult>("/v1/session/create", se, q).Execute(ct).ConfigureAwait(false);
            return new WriteResult<string>(res, res.Response.ID);
        }
        /// <summary>
        /// CreateNoChecks is like Create but is used specifically to create a session with no associated health checks.
        /// </summary>
        public Task<WriteResult<string>> CreateNoChecks(CancellationToken ct = default(CancellationToken))
        {
            return CreateNoChecks(null, WriteOptions.Default, ct);
        }

        /// <summary>
        /// CreateNoChecks is like Create but is used specifically to create a session with no associated health checks.
        /// </summary>
        /// <param name="se">The SessionEntry options to use</param>
        /// <returns>A write result containing the new session ID</returns>
        public Task<WriteResult<string>> CreateNoChecks(SessionEntry se, CancellationToken ct = default(CancellationToken))
        {
            return CreateNoChecks(se, WriteOptions.Default, ct);
        }

        /// <summary>
        /// CreateNoChecks is like Create but is used specifically to create a session with no associated health checks.
        /// </summary>
        /// <param name="se">The SessionEntry options to use</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result containing the new session ID</returns>
        public Task<WriteResult<string>> CreateNoChecks(SessionEntry se, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            if (se == null)
            {
                return Create(null, q);
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
            return Create(noChecksEntry, q, ct);
        }

        /// <summary>
        /// Destroy invalidates a given session
        /// </summary>
        /// <param name="id">The session ID to destroy</param>
        /// <returns>A write result containing the result of the session destruction</returns>
        public Task<WriteResult<bool>> Destroy(string id, CancellationToken ct = default(CancellationToken))
        {
            return Destroy(id, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Destroy invalidates a given session
        /// </summary>
        /// <param name="id">The session ID to destroy</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result containing the result of the session destruction</returns>
        public Task<WriteResult<bool>> Destroy(string id, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Put<object, bool>(string.Format("/v1/session/destroy/{0}", id), q).Execute(ct);
        }

        /// <summary>
        /// Info looks up a single session
        /// </summary>
        /// <param name="id">The session ID to look up</param>
        /// <returns>A query result containing the session information, or an empty query result if the session entry does not exist</returns>
        public Task<QueryResult<SessionEntry>> Info(string id, CancellationToken ct = default(CancellationToken))
        {
            return Info(id, QueryOptions.Default, ct);
        }

        /// <summary>
        /// Info looks up a single session
        /// </summary>
        /// <param name="id">The session ID to look up</param>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing the session information, or an empty query result if the session entry does not exist</returns>
        public async Task<QueryResult<SessionEntry>> Info(string id, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.Get<SessionEntry[]>(string.Format("/v1/session/info/{0}", id), q).Execute(ct).ConfigureAwait(false);
            return new QueryResult<SessionEntry>(res, res.Response != null && res.Response.Length > 0 ? res.Response[0] : null);
        }

        /// <summary>
        /// List gets all active sessions
        /// </summary>
        /// <returns>A query result containing list of all sessions, or an empty query result if no sessions exist</returns>
        public Task<QueryResult<SessionEntry[]>> List(CancellationToken ct = default(CancellationToken))
        {
            return List(QueryOptions.Default, ct);
        }

        /// <summary>
        /// List gets all active sessions
        /// </summary>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing the list of sessions, or an empty query result if no sessions exist</returns>
        public Task<QueryResult<SessionEntry[]>> List(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<SessionEntry[]>("/v1/session/list", q).Execute(ct);
        }

        /// <summary>
        /// Node gets all sessions for a node
        /// </summary>
        /// <param name="node">The node ID</param>
        /// <returns>A query result containing the list of sessions, or an empty query result if no sessions exist</returns>
        public Task<QueryResult<SessionEntry[]>> Node(string node, CancellationToken ct = default(CancellationToken))
        {
            return Node(node, QueryOptions.Default, ct);
        }

        /// <summary>
        /// Node gets all sessions for a node
        /// </summary>
        /// <param name="node">The node ID</param>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing the list of sessions, or an empty query result if no sessions exist</returns>
        public Task<QueryResult<SessionEntry[]>> Node(string node, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<SessionEntry[]>(string.Format("/v1/session/node/{0}", node), q).Execute(ct);
        }

        /// <summary>
        /// Renew renews the TTL on a given session
        /// </summary>
        /// <param name="id">The session ID to renew</param>
        /// <returns>An updated session entry</returns>
        public Task<WriteResult<SessionEntry>> Renew(string id, CancellationToken ct = default(CancellationToken))
        {
            return Renew(id, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Renew renews the TTL on a given session
        /// </summary>
        /// <param name="id">The session ID to renew</param>
        /// <param name="q">Customized write options</param>
        /// <returns>An updated session entry</returns>
        public async Task<WriteResult<SessionEntry>> Renew(string id, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.Put<object, SessionEntry[]>(string.Format("/v1/session/renew/{0}", id), q).Execute(ct).ConfigureAwait(false);
            if (res.StatusCode == HttpStatusCode.NotFound)
            {
                throw new SessionExpiredException(string.Format("Session expired: {0}", id));
            }
            return new WriteResult<SessionEntry>(res, res.Response != null && res.Response.Length > 0 ? res.Response[0] : null);
        }
    }

    public partial class ConsulClient : IConsulClient
    {
        private Lazy<Session> _session;

        /// <summary>
        /// Session returns a handle to the session endpoint
        /// </summary>
        public ISessionEndpoint Session
        {
            get { return _session.Value; }
        }
    }
}