using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    /// <summary>
    /// RaftServer has information about a server in the Raft configuration.
    /// </summary>
    public class RaftServer
    {
        /// <summary>
        /// ID is the unique ID for the server. These are currently the same
        /// as the address, but they will be changed to a real GUID in a future
        /// release of Consul.
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Node is the node name of the server, as known by Consul, or this
        /// will be set to "(unknown)" otherwise.
        /// </summary>
        public string Node { get; set; }

        /// <summary>
        /// Address is the IP:port of the server, used for Raft communications.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Leader is true if this server is the current cluster leader.
        /// </summary>
        public bool Leader { get; set; }

        /// <summary>
        /// Voter is true if this server has a vote in the cluster. This might
        /// be false if the server is staging and still coming online, or if
        /// it's a non-voting server, which will be added in a future release of
        /// Consul
        /// </summary>
        public bool Voter { get; set; }
    }

    /// <summary>
    /// RaftConfigration is returned when querying for the current Raft configuration.
    /// </summary>
    public class RaftConfiguration
    {
        /// <summary>
        /// Servers has the list of servers in the Raft configuration.
        /// </summary>
        public List<RaftServer> Servers { get; set; }

        /// <summary>
        /// Index has the Raft index of this configuration.
        /// </summary>
        public ulong Index { get; set; }
    }

    /// <summary>
    /// KeyringResponse is returned when listing the gossip encryption keys
    /// </summary>
    public class KeyringResponse
    {
        /// <summary>
        /// Whether this response is for a WAN ring
        /// </summary>
        public bool WAN { get; set; }
        /// <summary>
        /// The datacenter name this request corresponds to
        /// </summary>
        public string Datacenter { get; set; }
        /// <summary>
        /// A map of the encryption keys to the number of nodes they're installed on
        /// </summary>
        public IDictionary<string, int> Keys { get; set; }
        /// <summary>
        /// The total number of nodes in this ring
        /// </summary>
        public int NumNodes { get; set; }
    }

    public class Operator : IOperatorEndpoint
    {
        private readonly ConsulClient _client;

        /// <summary>
        /// Operator can be used to perform low-level operator tasks for Consul.
        /// </summary>
        /// <param name="c"></param>
        internal Operator(ConsulClient c)
        {
            _client = c;
        }

        /// <summary>
        /// KeyringRequest is used for performing Keyring operations
        /// </summary>
        private class KeyringRequest
        {
            [JsonProperty]
            internal string Key { get; set; }
        }

        /// <summary>
        /// RaftGetConfiguration is used to query the current Raft peer set.
        /// </summary>
        public Task<QueryResult<RaftConfiguration>> RaftGetConfiguration(CancellationToken ct = default(CancellationToken))
        {
            return RaftGetConfiguration(QueryOptions.Default, ct);
        }

        /// <summary>
        /// RaftGetConfiguration is used to query the current Raft peer set.
        /// </summary>
        public Task<QueryResult<RaftConfiguration>> RaftGetConfiguration(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<RaftConfiguration>("/v1/operator/raft/configuration", q).Execute(ct);
        }

        /// <summary>
        /// RaftRemovePeerByAddress is used to kick a stale peer (one that it in the Raft
        /// quorum but no longer known to Serf or the catalog) by address in the form of
        /// "IP:port".
        /// </summary>
        public Task<WriteResult> RaftRemovePeerByAddress(string address, CancellationToken ct = default(CancellationToken))
        {
            return RaftRemovePeerByAddress(address, WriteOptions.Default, ct);
        }

        /// <summary>
        /// RaftRemovePeerByAddress is used to kick a stale peer (one that it in the Raft
        /// quorum but no longer known to Serf or the catalog) by address in the form of
        /// "IP:port".
        /// </summary>
        public Task<WriteResult> RaftRemovePeerByAddress(string address, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            var req = _client.Delete("/v1/operator/raft/peer", q);

            // From Consul repo:
            // TODO (slackpad) Currently we made address a query parameter. Once
            // IDs are in place this will be DELETE /v1/operator/raft/peer/<id>.
            req.Params["address"] = address;

            return req.Execute(ct);
        }

        /// <summary>
        /// KeyringInstall is used to install a new gossip encryption key into the cluster
        /// </summary>
        public Task<WriteResult> KeyringInstall(string key, CancellationToken ct = default(CancellationToken))
        {
            return KeyringInstall(key, WriteOptions.Default, ct);
        }

        /// <summary>
        /// KeyringInstall is used to install a new gossip encryption key into the cluster
        /// </summary>
        public Task<WriteResult> KeyringInstall(string key, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Post("/v1/operator/keyring", new KeyringRequest() { Key = key }, q).Execute(ct);
        }

        /// <summary>
        /// KeyringList is used to list the gossip keys installed in the cluster
        /// </summary>
        public Task<QueryResult<KeyringResponse[]>> KeyringList(CancellationToken ct = default(CancellationToken))
        {
            return KeyringList(QueryOptions.Default, ct);
        }

        /// <summary>
        /// KeyringList is used to list the gossip keys installed in the cluster
        /// </summary>
        public Task<QueryResult<KeyringResponse[]>> KeyringList(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<KeyringResponse[]>("/v1/operator/keyring", q).Execute(ct);
        }

        /// <summary>
        /// KeyringRemove is used to remove a gossip encryption key from the cluster
        /// </summary>
        public Task<WriteResult> KeyringRemove(string key, CancellationToken ct = default(CancellationToken))
        {
            return KeyringRemove(key, WriteOptions.Default, ct);
        }

        /// <summary>
        /// KeyringRemove is used to remove a gossip encryption key from the cluster
        /// </summary>
        public Task<WriteResult> KeyringRemove(string key, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.DeleteAccepting("/v1/operator/keyring", new KeyringRequest() { Key = key }, q).Execute(ct);
        }

        /// <summary>
        /// KeyringUse is used to change the active gossip encryption key
        /// </summary>
        public Task<WriteResult> KeyringUse(string key, CancellationToken ct = default(CancellationToken))
        {
            return KeyringUse(key, WriteOptions.Default, ct);
        }

        /// <summary>
        /// KeyringUse is used to change the active gossip encryption key
        /// </summary>
        public Task<WriteResult> KeyringUse(string key, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Put("/v1/operator/keyring", new KeyringRequest() { Key = key }, q).Execute(ct);
        }
    }

    public partial class ConsulClient : IConsulClient
    {
        private Lazy<Operator> _operator;

        /// <summary>
        /// Operator returns a handle to the operator endpoints.
        /// </summary>
        public IOperatorEndpoint Operator
        {
            get { return _operator.Value; }
        }
    }
}
