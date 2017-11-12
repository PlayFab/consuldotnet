using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface IOperatorEndpoint
    {
        Task<QueryResult<RaftConfiguration>> RaftGetConfiguration(CancellationToken ct = default(CancellationToken));
        Task<QueryResult<RaftConfiguration>> RaftGetConfiguration(QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> RaftRemovePeerByAddress(string address, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> RaftRemovePeerByAddress(string address, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> KeyringInstall(string key, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> KeyringInstall(string key, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<KeyringResponse[]>> KeyringList(CancellationToken ct = default(CancellationToken));
        Task<QueryResult<KeyringResponse[]>> KeyringList(QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> KeyringRemove(string key, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> KeyringRemove(string key, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> KeyringUse(string key, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> KeyringUse(string key, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<string>> AreaCreate(Area area, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<string>> AreaCreate(Area area, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> AreaDelete(string areaID, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> AreaDelete(string areaID, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<Area[]>> AreaGet(string areaID, QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<Area[]>> AreaGet(string areaID, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<AreaJoinResponse[]>> AreaJoin(string areaID, string[] addresses, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<AreaJoinResponse[]>> AreaJoin(string areaID, string[] addresses, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<Area[]>> AreaList(QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<Area[]>> AreaList(CancellationToken ct = default(CancellationToken));
        Task<QueryResult<SerfMember[]>> AreaMembers(string areaID, QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<SerfMember[]>> AreaMembers(string areaID, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<string>> AreaUpdate(string areaID, Area area, WriteOptions q, CancellationToken ct = default (CancellationToken));
        Task<WriteResult<string>> AreaUpdate(string areaID, Area area, CancellationToken ct = default (CancellationToken));
        Task<QueryResult<string[]>> SegmentList(QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<string[]>> SegmentList(CancellationToken ct = default(CancellationToken));
        Task<bool> AutopilotCASConfiguration(AutopilotConfiguration conf, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<bool> AutopilotCASConfiguration(AutopilotConfiguration conf, CancellationToken ct = default(CancellationToken));
        Task<AutopilotConfiguration> AutopilotGetConfiguration(QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<AutopilotConfiguration> AutopilotGetConfiguration(CancellationToken ct = default(CancellationToken));
        Task AutopilotSetConfiguration(AutopilotConfiguration conf, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task AutopilotSetConfiguration(AutopilotConfiguration conf, CancellationToken ct = default(CancellationToken));
        Task<OperatorHealthReply> AutopilotServerHealth(QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<OperatorHealthReply> AutopilotServerHealth(CancellationToken ct = default(CancellationToken));
    }
}
