using System.Threading;

namespace Consul
{
    public interface IRawEndpoint
    {
        QueryResult<dynamic> Query(string endpoint, QueryOptions q);
        QueryResult<dynamic> Query(string endpoint, QueryOptions q, CancellationToken ct);
        WriteResult<dynamic> Write(string endpoint, object obj, WriteOptions q);
    }
}