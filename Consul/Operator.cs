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

        public Task<QueryResult<RaftConfiguration>> RaftGetConfiguration(CancellationToken ct = default(CancellationToken))
        {
            return RaftGetConfiguration(QueryOptions.Default, ct);
        }

        public Task<QueryResult<RaftConfiguration>> RaftGetConfiguration(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<RaftConfiguration>("/v1/operator/raft/configuration", q).Execute(ct);
        }

        public Task<WriteResult> RaftRemovePeerByAddress(string address, CancellationToken ct = default(CancellationToken))
        {
            return RaftRemovePeerByAddress(address, WriteOptions.Default, ct);
        }

        public Task<WriteResult> RaftRemovePeerByAddress(string address, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            var req = _client.Delete("/v1/operator/raft/peer", q);

            // From Consul repo:
            // TODO (slackpad) Currently we made address a query parameter. Once
            // IDs are in place this will be DELETE /v1/operator/raft/peer/<id>.
            req.Params["address"] = address;

            return req.Execute(ct);
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
