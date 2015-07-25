using System.Threading;

namespace Consul
{
    public interface IEventEndpoint
    {
        WriteResult<string> Fire(UserEvent ue);
        WriteResult<string> Fire(UserEvent ue, WriteOptions q);
        ulong IDToIndex(string uuid);
        QueryResult<UserEvent[]> List();
        QueryResult<UserEvent[]> List(string name);
        QueryResult<UserEvent[]> List(string name, QueryOptions q);
        QueryResult<UserEvent[]> List(string name, QueryOptions q, CancellationToken ct);
    }
}