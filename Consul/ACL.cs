using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Consul
{
    /// <summary>
    /// The type of ACL token, which sets the permissions ceiling
    /// </summary>
    public class ACLType : IEquatable<ACLType>
    {
        public string Type { get; private set; }

        /// <summary>
        /// Token type which cannot modify ACL rules
        /// </summary>
        public static ACLType Client
        {
            get { return new ACLType() { Type = "client" }; }
        }

        /// <summary>
        /// Token type which is allowed to perform all actions
        /// </summary>
        public static ACLType Management
        {
            get { return new ACLType() { Type = "management" }; }
        }

        public bool Equals(ACLType other)
        {
            if (other == null)
            {
                return false;
            }
            return Type.Equals(other.Type);
        }

        public override bool Equals(object other)
        {
            var a = other as ACLType;
            return a != null && Equals(a);
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode();
        }
    }

    public class ACLTypeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((ACLType)value).Type);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            var type = (string)serializer.Deserialize(reader, typeof(string));
            switch (type)
            {
                case "client":
                    return ACLType.Client;
                case "management":
                    return ACLType.Management;
                default:
                    throw new ArgumentOutOfRangeException("serializer", type,
                        "Unknown ACL token type value found during deserialization");
            }
        }

        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(ACLType))
            {
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// ACLEntry is used to represent an ACL entry
    /// </summary>
    public class ACLEntry
    {
        public ulong CreateIndex { get; set; }
        public ulong ModifyIndex { get; set; }

        public string ID { get; set; }
        public string Name { get; set; }

        [JsonConverter(typeof(ACLTypeConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ACLType Type { get; set; }

        public string Rules { get; set; }

        public bool ShouldSerializeCreateIndex()
        {
            return false;
        }

        public bool ShouldSerializeModifyIndex()
        {
            return false;
        }

        public ACLEntry()
            : this(string.Empty, string.Empty, string.Empty)
        {
        }

        public ACLEntry(string name, string rules)
            : this(string.Empty, name, rules)
        {
        }

        public ACLEntry(string id, string name, string rules)
        {
            Type = ACLType.Client;
            ID = id;
            Name = name;
            Rules = rules;
        }
    }

    /// <summary>
    /// ACLReplicationStatus is used to represent the status of ACL replication.
    /// </summary>
    public class ACLReplicationStatus
    {
        public bool Enabled { get; set; }
        public bool Running { get; set; }
        public string SourceDatacenter { get; set; }
        public ulong ReplicatedIndex { get; set; }
        [JsonConverter(typeof(DateTimeConverter))]
        public DateTime LastSuccess { get; set; }
        [JsonConverter(typeof(DateTimeConverter))]
        public DateTime LastError { get; set; }
    }

    /// <summary>
    /// ACL can be used to query the ACL endpoints
    /// </summary>
    public class ACL : IACLEndpoint
    {
        private readonly ConsulClient _client;

        internal ACL(ConsulClient c)
        {
            _client = c;
        }

        private class ACLCreationResult
        {
            [JsonProperty]
            internal string ID { get; set; }
        }

        /// <summary>
        /// Create is used to generate a new token with the given parameters
        /// </summary>
        /// <param name="acl">The ACL entry to create</param>
        /// <returns>A write result containing the newly created ACL token</returns>
        public Task<WriteResult<string>> Create(ACLEntry acl, CancellationToken ct = default(CancellationToken))
        {
            return Create(acl, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Create is used to generate a new token with the given parameters
        /// </summary>
        /// <param name="acl">The ACL entry to create</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result containing the newly created ACL token</returns>
        public async Task<WriteResult<string>> Create(ACLEntry acl, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.Put<ACLEntry, ACLCreationResult>("/v1/acl/create", acl, q).Execute(ct).ConfigureAwait(false);
            return new WriteResult<string>(res, res.Response.ID);
        }

        /// <summary>
        /// Update is used to update the rules of an existing token
        /// </summary>
        /// <param name="acl">The ACL entry to update</param>
        /// <returns>An empty write result</returns>
        public Task<WriteResult> Update(ACLEntry acl, CancellationToken ct = default(CancellationToken))
        {
            return Update(acl, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Update is used to update the rules of an existing token
        /// </summary>
        /// <param name="acl">The ACL entry to update</param>
        /// <param name="q">Customized write options</param>
        /// <returns>An empty write result</returns>
        public Task<WriteResult> Update(ACLEntry acl, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Put("/v1/acl/update", acl, q).Execute(ct);
        }

        /// <summary>
        /// Destroy is used to destroy a given ACL token ID
        /// </summary>
        /// <param name="id">The ACL ID to destroy</param>
        /// <returns>An empty write result</returns>
        public Task<WriteResult<bool>> Destroy(string id, CancellationToken ct = default(CancellationToken))
        {
            return Destroy(id, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Destroy is used to destroy a given ACL token ID
        /// </summary>
        /// <param name="id">The ACL ID to destroy</param>
        /// <param name="q">Customized write options</param>
        /// <returns>An empty write result</returns>
        public Task<WriteResult<bool>> Destroy(string id, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.PutReturning<bool>(string.Format("/v1/acl/destroy/{0}", id), q).Execute(ct);
        }

        /// <summary>
        /// Bootstrap is used to perform a one-time ACL bootstrap operation on a cluster
        /// to get the first management token.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<WriteResult<string>> Bootstrap(CancellationToken ct = default(CancellationToken))
        {
            var req = _client.PutNothing("/v1/acl/bootstrap");
            var resp = await req.Execute(ct);

            using (var reader = new StreamReader(req.ResponseStream))
            {
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                var jo = JObject.Parse(body);
                return new WriteResult<string>(resp, jo.SelectToken("ID").ToObject<string>());
            }
        }

        /// <summary>
        /// Replication returns the status of the ACL replication process in the datacenter
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<QueryResult<ACLReplicationStatus>> Replication(CancellationToken ct = default(CancellationToken))
        {
            return Replication(QueryOptions.Default, ct);
        }

        /// <summary>
        /// Replication returns the status of the ACL replication process in the datacenter
        /// </summary>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<QueryResult<ACLReplicationStatus>> Replication(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<ACLReplicationStatus>("/v1/acl/replication", q).Execute(ct);
        }

        /// <summary>
        /// Clone is used to return a new token cloned from an existing one
        /// </summary>
        /// <param name="id">The ACL ID to clone</param>
        /// <returns>A write result containing the newly created ACL token</returns>
        public Task<WriteResult<string>> Clone(string id, CancellationToken ct = default(CancellationToken))
        {
            return Clone(id, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Clone is used to return a new token cloned from an existing one
        /// </summary>
        /// <param name="id">The ACL ID to clone</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result containing the newly created ACL token</returns>
        public async Task<WriteResult<string>> Clone(string id, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.PutReturning<ACLCreationResult>(string.Format("/v1/acl/clone/{0}", id), q).Execute(ct).ConfigureAwait(false);
            return new WriteResult<string>(res, res.Response.ID);
        }

        /// <summary>
        /// Info is used to query for information about an ACL token
        /// </summary>
        /// <param name="id">The ACL ID to request information about</param>
        /// <returns>A query result containing the ACL entry matching the provided ID, or a query result with a null response if no token matched the provided ID</returns>
        public Task<QueryResult<ACLEntry>> Info(string id, CancellationToken ct = default(CancellationToken))
        {
            return Info(id, QueryOptions.Default, ct);
        }

        /// <summary>
        /// Info is used to query for information about an ACL token
        /// </summary>
        /// <param name="id">The ACL ID to request information about</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing the ACL entry matching the provided ID, or a query result with a null response if no token matched the provided ID</returns>
        public async Task<QueryResult<ACLEntry>> Info(string id, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.Get<ACLEntry[]>(string.Format("/v1/acl/info/{0}", id), q).Execute(ct).ConfigureAwait(false);
            return new QueryResult<ACLEntry>(res, res.Response != null && res.Response.Length > 0 ? res.Response[0] : null);
        }

        /// <summary>
        /// List is used to get all the ACL tokens
        /// </summary>
        /// <returns>A write result containing the list of all ACLs</returns>
        public Task<QueryResult<ACLEntry[]>> List(CancellationToken ct = default(CancellationToken))
        {
            return List(QueryOptions.Default, ct);
        }

        /// <summary>
        /// List is used to get all the ACL tokens
        /// </summary>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A write result containing the list of all ACLs</returns>
        public Task<QueryResult<ACLEntry[]>> List(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<ACLEntry[]>("/v1/acl/list", q).Execute(ct);
        }
    }

    public partial class ConsulClient : IConsulClient
    {
        private Lazy<ACL> _acl;

        /// <summary>
        /// ACL returns a handle to the ACL endpoints
        /// </summary>
        public IACLEndpoint ACL
        {
            get { return _acl.Value; }
        }
    }
}