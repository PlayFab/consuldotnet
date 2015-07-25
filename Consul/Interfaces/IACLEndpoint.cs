using System;
using System.Threading;
namespace Consul
{
    public interface IACLEndpoint
    {
        WriteResult<string> Clone(string id);
        WriteResult<string> Clone(string id, WriteOptions q);
        WriteResult<string> Create(ACLEntry acl);
        WriteResult<string> Create(ACLEntry acl, WriteOptions q);
        WriteResult<bool> Destroy(string id);
        WriteResult<bool> Destroy(string id, WriteOptions q);
        QueryResult<ACLEntry> Info(string id);
        QueryResult<ACLEntry> Info(string id, QueryOptions q);
        QueryResult<ACLEntry> Info(string id, QueryOptions q, CancellationToken ct);
        QueryResult<ACLEntry[]> List();
        QueryResult<ACLEntry[]> List(QueryOptions q);
        QueryResult<ACLEntry[]> List(QueryOptions q, CancellationToken ct);
        WriteResult Update(ACLEntry acl);
        WriteResult Update(ACLEntry acl, WriteOptions q);
    }
}
