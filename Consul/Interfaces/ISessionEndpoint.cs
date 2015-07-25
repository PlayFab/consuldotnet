using System;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface ISessionEndpoint
    {
        WriteResult<string> Create();
        WriteResult<string> Create(SessionEntry se);
        WriteResult<string> Create(SessionEntry se, WriteOptions q);
        WriteResult<string> CreateNoChecks();
        WriteResult<string> CreateNoChecks(SessionEntry se);
        WriteResult<string> CreateNoChecks(SessionEntry se, WriteOptions q);
        WriteResult<bool> Destroy(string id);
        WriteResult<bool> Destroy(string id, WriteOptions q);
        QueryResult<SessionEntry> Info(string id);
        QueryResult<SessionEntry> Info(string id, QueryOptions q);
        QueryResult<SessionEntry[]> List();
        QueryResult<SessionEntry[]> List(QueryOptions q);
        QueryResult<SessionEntry[]> Node(string node);
        QueryResult<SessionEntry[]> Node(string node, QueryOptions q);
        WriteResult<SessionEntry> Renew(string id);
        WriteResult<SessionEntry> Renew(string id, WriteOptions q);
        Task RenewPeriodic(TimeSpan initialTTL, string id, CancellationToken ct);
        Task RenewPeriodic(TimeSpan initialTTL, string id, WriteOptions q, CancellationToken ct);
    }
}