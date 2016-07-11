using System;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface IPreparedQueryEndpoint
    {
        Task<WriteResult<string>> Create(PreparedQueryDefinition query, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<string>> Create(PreparedQueryDefinition query, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> Update(PreparedQueryDefinition query, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> Update(PreparedQueryDefinition query, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<PreparedQueryDefinition[]>> List(CancellationToken ct = default(CancellationToken));
        Task<QueryResult<PreparedQueryDefinition[]>> List(QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<PreparedQueryDefinition[]>> Get(string queryID, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<PreparedQueryDefinition[]>> Get(string queryID, QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> Delete(string queryID, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> Delete(string queryID, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<PreparedQueryExecuteResponse>> Execute(string queryIDOrName, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<PreparedQueryExecuteResponse>> Execute(string queryIDOrName, QueryOptions q, CancellationToken ct = default(CancellationToken));
    }
}
