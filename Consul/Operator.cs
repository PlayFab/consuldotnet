using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
    
    /// <summary>
    /// Area defines a network area.
    /// </summary>
    public class Area
    {
        /// <summary>
        /// ID is this identifier for an area (a UUID). This must be left empty
        /// when creating a new area.
        /// </summary>
        public string ID { get; set; }
        
        /// <summary>
        /// PeerDatacenter is the peer Consul datacenter that will make up the
        /// other side of this network area. Network areas always involve a pair
        /// of datacenters: the datacenter where the area was created, and the
        /// peer datacenter. This is required.
        /// </summary>
        public string PeerDatacenter { get; set; }
        
        /// <summary>
        /// RetryJoin specifies the address of Consul servers to join to, such as
        /// an IPs or hostnames with an optional port number. This is optional.
        /// </summary>
        public string[] RetryJoin { get; set; }
    }
    
    /// <summary>
    /// AreaJoinResponse is returned when a join occurs and gives the result for each
    /// address.
    /// </summary>
    public class AreaJoinResponse
    {
        /// <summary>
        /// The address that was joined.
        /// </summary>
        public string Address { get; set; }
        
        /// <summary>
        /// Whether or not the join was a success.
        /// </summary>
        public bool Joined { get; set; }
        
        /// <summary>
        /// If we couldn't join, this is the message with information.
        /// </summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// SerfMember is a generic structure for reporting information about members in
    /// a Serf cluster. This is only used by the area endpoints right now, but this
    /// could be expanded to other endpoints in the future.
    /// </summary>
    public class SerfMember
    {
        /// <summary>
        /// ID is the node identifier (a UUID).
        /// </summary>
        public string ID { get; set; }
        
        /// <summary>
        /// Name is the node name.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Addr has the IP address.
        /// </summary>
        public string Addr { get; set; }
        
        /// <summary>
        /// Port is the RPC port.
        /// </summary>
        public ushort Port { get; set; }
        
        /// <summary>
        /// Datacenter is the DC name.
        /// </summary>
        public string Datacenter { get; set; }
        
        /// <summary>
        /// Role is "client", "server", or "unknown".
        /// </summary>
        public string Role { get; set; }
        
        /// <summary>
        /// Build has the version of the Consul agent.
        /// </summary>
        public string Build { get; set; }
        
        /// <summary>
        /// Protocol is the protocol of the Consul agent.
        /// </summary>
        public int Protocol { get; set; }
        
        /// <summary>
        /// Status is the Serf health status "none", "alive", "leaving", "left",
        /// or "failed".
        /// </summary>
        public string Status { get; set; }
        
        /// <summary>
        /// RTT is the estimated round trip time from the server handling the
        /// request to the this member. This will be negative if no RTT estimate
        /// is available.
        /// </summary>
        [JsonConverter(typeof(NanoSecTimespanConverter))]
        public TimeSpan RTT { get; set; }
    }
    
    /// <summary>
    /// AutopilotConfiguration is used for querying/setting the Autopilot configuration.
    /// Autopilot helps manage operator tasks related to Consul servers like removing
    /// failed servers from the Raft quorum.
    /// </summary>
    public class AutopilotConfiguration
    {
        /// <summary>
        /// CleanupDeadServers controls whether to remove dead servers from the Raft
        /// peer list when a new server joins
        /// </summary>
        public bool CleanupDeadServers { get; set; }
        
        /// <summary>
        /// LastContactThreshold is the limit on the amount of time a server can go
        /// without leader contact before being considered unhealthy.
        /// </summary>
        [JsonConverter(typeof(DurationTimespanConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TimeSpan? LastContactThreshold { get; set; }
        
        /// <summary>
        /// MaxTrailingLogs is the amount of entries in the Raft Log that a server can
        /// be behind before being considered unhealthy.
        /// </summary>
        public ulong MaxTrailingLogs { get; set; }
        
        /// <summary>
        /// ServerStabilizationTime is the minimum amount of time a server must be
        /// in a stable, healthy state before it can be added to the cluster. Only
        /// applicable with Raft protocol version 3 or higher.
        /// </summary>
        [JsonConverter(typeof(DurationTimespanConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TimeSpan? ServerStabilizationTime { get; set; }
        
        /// <summary>
        /// (Enterprise-only) RedundancyZoneTag is the node tag to use for separating
        /// servers into zones for redundancy. If left blank, this feature will be disabled.
        /// </summary>
        public string RedundancyZoneTag { get; set; }
        
        /// <summary>
        /// (Enterprise-only) DisableUpgradeMigration will disable Autopilot's upgrade migration
        /// strategy of waiting until enough newer-versioned servers have been added to the
        /// cluster before promoting them to voters.
        /// </summary>
        public bool DisableUpgradeMigration { get; set; }
        
        /// <summary>
        /// (Enterprise-only) UpgradeVersionTag is the node tag to use for version info when
        /// performing upgrade migrations. If left blank, the Consul version will be used.
        /// </summary>
        public string UpgradeVersionTag { get; set; }
        
        /// <summary>
        /// CreateIndex holds the index corresponding the creation of this configuration.
        /// This is a read-only field.
        /// </summary>
        public ulong CreateIndex { get; set; }
        
        /// <summary>
        /// ModifyIndex will be set to the index of the last update when retrieving the
        /// Autopilot configuration. Resubmitting a configuration with
        /// AutopilotCASConfiguration will perform a check-and-set operation which ensures
        /// there hasn't been a subsequent update since the configuration was retrieved.
        /// </summary>
        public ulong ModifyIndex { get; set; }
    }

    /// <summary>
    /// ServerHealth is the health (from the leader's point of view) of a server.
    /// </summary>
    public class ServerHealth
    {
        /// <summary>
        /// ID is the raft ID of the server.
        /// </summary>
        public string ID { get; set; }
        
        /// <summary>
        /// Name is the node name of the server.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Address is the address of the server.
        /// </summary>
        public string Address { get; set; }
        
        /// <summary>
        /// The status of the SerfHealth check for the server.
        /// </summary>
        public string SerfStatus { get; set; }
        
        /// <summary>
        /// Version is the Consul version of the server.
        /// </summary>
        public string Version { get; set; }
        
        /// <summary>
        /// Leader is whether this server is currently the leader.
        /// </summary>
        public string Leader { get; set; }
        
        /// <summary>
        /// LastContact is the time since this node's last contact with the leader.
        /// </summary>
        [JsonConverter(typeof(DurationTimespanConverter))]
        public TimeSpan LastContact { get; set; }
        
        /// <summary>
        /// LastTerm is the highest leader term this server has a record of in its Raft log.
        /// </summary>
        public ulong LastTerm { get; set; }
        
        /// <summary>
        /// LastIndex is the last log index this server has a record of in its Raft log.
        /// </summary>
        public ulong LastIndex { get; set; }
        
        /// <summary>
        /// Healthy is whether or not the server is healthy according to the current
        /// Autopilot config.
        /// </summary>
        public bool Healthy { get; set; }
        
        /// <summary>
        /// Voter is whether this is a voting server.
        /// </summary>
        public bool Voter { get; set; }
        
        /// <summary>
        /// StableSince is the last time this server's Healthy value changed.
        /// </summary>
        public DateTime StableSince { get; set; }
    }

    /// <summary>
    /// OperatorHealthReply is a representation of the overall health of the cluster
    /// </summary>
    public class OperatorHealthReply
    {
        /// <summary>
        /// Healthy is true if all the servers in the cluster are healthy.
        /// </summary>
        public bool Healthy { get; set; }
        
        /// <summary>
        /// FailureTolerance is the number of healthy servers that could be lost without
        /// an outage occurring.
        /// </summary>
        public int FailureTolerance { get; set; }
        
        /// <summary>
        /// Servers holds the health of each server.
        /// </summary>
        public ServerHealth[] Servers { get; set; }
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
        
        private class AreaCreationResult
        {
            [JsonProperty]
            internal string ID { get; set; }
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
        
        /// <summary>
        /// AreaCreate will create a new network area. The ID in the given structure must
        /// be empty and a generated ID will be returned on success.
        /// </summary>
        /// <param name="area"></param>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<WriteResult<string>> AreaCreate(Area area, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.Post<Area, AreaCreationResult>("/v1/operator/area", area, q).Execute(ct)
                .ConfigureAwait(false);

            return new WriteResult<string>(res, res.Response.ID);
        }

        /// <summary>
        /// AreaCreate will create a new network area. The ID in the given structure must
        /// be empty and a generated ID will be returned on success.
        /// </summary>
        /// <param name="area"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<WriteResult<string>> AreaCreate(Area area, CancellationToken ct = default(CancellationToken))
        {
            return AreaCreate(area, WriteOptions.Default, ct);
        }

        /// <summary>
        /// AreaDelete deletes the given network area.
        /// </summary>
        /// <param name="areaID"></param>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<WriteResult> AreaDelete(string areaID, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Delete(string.Format("/v1/operator/area/{0}", areaID)).Execute(ct);
        }

        /// <summary>
        /// AreaDelete deletes the given network area.
        /// </summary>
        /// <param name="areaID"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<WriteResult> AreaDelete(string areaID, CancellationToken ct = default(CancellationToken))
        {
            return AreaDelete(areaID, WriteOptions.Default, ct);
        }

        /// <summary>
        /// AreaGet returns a single network area.
        /// </summary>
        /// <param name="areaID"></param>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<QueryResult<Area[]>> AreaGet(string areaID, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<Area[]>(string.Format("/v1/operator/area/{0}", areaID), q).Execute(ct);
        }

        /// <summary>
        /// AreaGet returns a single network area.
        /// </summary>
        /// <param name="areaID"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<QueryResult<Area[]>> AreaGet(string areaID, CancellationToken ct = default(CancellationToken))
        {
            return AreaGet(areaID, QueryOptions.Default, ct);
        }

        /// <summary>
        /// AreaJoin attempts to join the given set of join addresses to the given
        /// network area. See the Area class for details about join addresses.
        /// </summary>
        /// <param name="areaID"></param>
        /// <param name="addresses"></param>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<WriteResult<AreaJoinResponse[]>> AreaJoin(string areaID, string[] addresses, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client
                .Put<string[], AreaJoinResponse[]>(string.Format("/v1/operator/area/{0}/join", areaID), addresses, q)
                .Execute(ct);
        }

        /// <summary>
        /// AreaJoin attempts to join the given set of join addresses to the given
        /// network area. See the Area class for details about join addresses.
        /// </summary>
        /// <param name="areaID"></param>
        /// <param name="addresses"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<WriteResult<AreaJoinResponse[]>> AreaJoin(string areaID, string[] addresses, CancellationToken ct = default(CancellationToken))
        {
            return AreaJoin(areaID, addresses, WriteOptions.Default, ct);
        }

        /// <summary>
        /// AreaList returns all the available network areas.
        /// </summary>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<QueryResult<Area[]>> AreaList(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<Area[]>("/v1/operator/area").Execute(ct);
        }

        /// <summary>
        /// AreaList returns all the available network areas.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<QueryResult<Area[]>> AreaList(CancellationToken ct = default(CancellationToken))
        {
            return AreaList(QueryOptions.Default, ct);
        }

        /// <summary>
        /// AreaMembers lists the Serf information about the members in the given area.
        /// </summary>
        /// <param name="areaID"></param>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<QueryResult<SerfMember[]>> AreaMembers(string areaID, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<SerfMember[]>(string.Format("/v1/operator/area/{0}/members", areaID), q).Execute(ct);
        }

        /// <summary>
        /// AreaMembers lists the Serf information about the members in the given area.
        /// </summary>
        /// <param name="areaID"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<QueryResult<SerfMember[]>> AreaMembers(string areaID, CancellationToken ct = default(CancellationToken))
        {
            return AreaMembers(areaID, QueryOptions.Default, ct);
        }

        /// <summary>
        /// AreaUpdate will update the configuration of the network area with the given ID.
        /// </summary>
        /// <param name="areaID"></param>
        /// <param name="area"></param>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<WriteResult<string>> AreaUpdate(string areaID, Area area, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.Put<Area, AreaCreationResult>(string.Format("/v1/operator/area/{0}", areaID), area, q)
                .Execute(ct).ConfigureAwait(false);

            return new WriteResult<string>(res, res.Response.ID);
        }

        /// <summary>
        /// AreaUpdate will update the configuration of the network area with the given ID.
        /// </summary>
        /// <param name="areaID"></param>
        /// <param name="area"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<WriteResult<string>> AreaUpdate(string areaID, Area area, CancellationToken ct = default(CancellationToken))
        {
            return AreaUpdate(areaID, area, WriteOptions.Default, ct);
        }

        /// <summary>
        /// SegmentList returns all the available LAN segments.
        /// </summary>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<QueryResult<string[]>> SegmentList(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<string[]>("/v1/operator/segment", q).Execute(ct);
        }

        /// <summary>
        /// SegmentList returns all the available LAN segments.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<QueryResult<string[]>> SegmentList(CancellationToken ct = default(CancellationToken))
        {
            return SegmentList(QueryOptions.Default, ct);
        }

        /// <summary>
        /// AutopilotCASConfiguration is used to perform a Check-And-Set update on the
        /// Autopilot configuration. The ModifyIndex value will be respected. Returns
        /// true on success or false on failures.
        /// </summary>
        /// <param name="conf"></param>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<bool> AutopilotCASConfiguration(AutopilotConfiguration conf, WriteOptions q,
            CancellationToken ct = default(CancellationToken))
        {
            //TODO: Maybe there is a better way to execute the request
            var req = _client.Put("/v1/operator/autopilot/configuration", conf, q);
            req.Params["cas"] = conf.ModifyIndex.ToString();

            await req.Execute(ct).ConfigureAwait(false);

            using (var reader = new StreamReader(req.ResponseStream))
            {
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                return body.Contains("true");
            }
        }

        /// <summary>
        /// AutopilotCASConfiguration is used to perform a Check-And-Set update on the
        /// Autopilot configuration. The ModifyIndex value will be respected. Returns
        /// true on success or false on failures.
        /// </summary>
        /// <param name="conf"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<bool> AutopilotCASConfiguration(AutopilotConfiguration conf, CancellationToken ct = default(CancellationToken))
        {
            return AutopilotCASConfiguration(conf, WriteOptions.Default, ct);
        }

        /// <summary>
        /// AutopilotGetConfiguration is used to query the current Autopilot configuration.
        /// </summary>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<AutopilotConfiguration> AutopilotGetConfiguration(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.Get<AutopilotConfiguration>("/v1/operator/autopilot/configuration", q).Execute(ct).ConfigureAwait(false);
            return res.Response;
        }

        /// <summary>
        /// AutopilotGetConfiguration is used to query the current Autopilot configuration.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<AutopilotConfiguration> AutopilotGetConfiguration(CancellationToken ct = default(CancellationToken))
        {
            return AutopilotGetConfiguration(QueryOptions.Default, ct);
        }

        /// <summary>
        /// AutopilotSetConfiguration is used to set the current Autopilot configuration.
        /// </summary>
        /// <param name="conf"></param>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task AutopilotSetConfiguration(AutopilotConfiguration conf, WriteOptions q,
            CancellationToken ct = default(CancellationToken))
        {
            await _client.Put("/v1/operator/autopilot/configuration", conf, q).Execute(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// AutopilotSetConfiguration is used to set the current Autopilot configuration.
        /// </summary>
        /// <param name="conf"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task AutopilotSetConfiguration(AutopilotConfiguration conf, CancellationToken ct = default(CancellationToken))
        {
            return AutopilotSetConfiguration(conf, WriteOptions.Default, ct);
        }

        /// <summary>
        /// AutopilotServerHealth
        /// </summary>
        /// <param name="q"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<OperatorHealthReply> AutopilotServerHealth(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            var resp = await _client.Get<OperatorHealthReply>("/v1/operator/autopilot/health", q).Execute(ct).ConfigureAwait(false);
            return resp.Response;
        }

        /// <summary>
        /// AutopilotServerHealth
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task<OperatorHealthReply> AutopilotServerHealth(CancellationToken ct = default(CancellationToken))
        {
            return AutopilotServerHealth(QueryOptions.Default, ct);
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
