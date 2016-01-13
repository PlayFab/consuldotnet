using System;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface IACLEndpoint
    {
        Task<WriteResult<string>> Clone(string id);
        Task<WriteResult<string>> Clone(string id, WriteOptions q);
        Task<WriteResult<string>> Create(ACLEntry acl);
        Task<WriteResult<string>> Create(ACLEntry acl, WriteOptions q);
        Task<WriteResult<bool>> Destroy(string id);
        Task<WriteResult<bool>> Destroy(string id, WriteOptions q);
        Task<QueryResult<ACLEntry>> Info(string id);
        Task<QueryResult<ACLEntry>> Info(string id, QueryOptions q);
        Task<QueryResult<ACLEntry>> Info(string id, QueryOptions q, CancellationToken ct);
        Task<QueryResult<ACLEntry[]>> List();
        Task<QueryResult<ACLEntry[]>> List(QueryOptions q);
        Task<QueryResult<ACLEntry[]>> List(QueryOptions q, CancellationToken ct);
        Task<WriteResult> Update(ACLEntry acl);
        Task<WriteResult> Update(ACLEntry acl, WriteOptions q);
    }
}
