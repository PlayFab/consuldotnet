using System;
using System.Threading.Tasks;

namespace Consul
{
    public interface IPreparedQueryEndpoint
    {
        Task<WriteResult<string>> Create(PreparedQueryDefinition query);
        Task<WriteResult<string>> Create(PreparedQueryDefinition query, WriteOptions q);
        Task<WriteResult> Update(PreparedQueryDefinition query);
        Task<WriteResult> Update(PreparedQueryDefinition query, WriteOptions q);
        Task<QueryResult<PreparedQueryDefinition[]>> List();
        Task<QueryResult<PreparedQueryDefinition[]>> List(QueryOptions q);
        Task<QueryResult<PreparedQueryDefinition[]>> Get(string queryID);
        Task<QueryResult<PreparedQueryDefinition[]>> Get(string queryID, QueryOptions q);
        Task<WriteResult> Delete(string queryID);
        Task<WriteResult> Delete(string queryID, WriteOptions q);
        Task<QueryResult<PreparedQueryExecuteResponse>> Execute(string queryIDOrName);
        Task<QueryResult<PreparedQueryExecuteResponse>> Execute(string queryIDOrName, QueryOptions q);
    }
}
