using System.Threading;

namespace Consul
{
    public interface IKVEndpoint
    {
        WriteResult<bool> Acquire(KVPair p);
        WriteResult<bool> Acquire(KVPair p, WriteOptions q);
        WriteResult<bool> CAS(KVPair p);
        WriteResult<bool> CAS(KVPair p, WriteOptions q);
        WriteResult<bool> Delete(string key);
        WriteResult<bool> Delete(string key, WriteOptions q);
        WriteResult<bool> DeleteCAS(KVPair p);
        WriteResult<bool> DeleteCAS(KVPair p, WriteOptions q);
        WriteResult<bool> DeleteTree(string prefix);
        WriteResult<bool> DeleteTree(string prefix, WriteOptions q);
        QueryResult<KVPair> Get(string key);
        QueryResult<KVPair> Get(string key, QueryOptions q);
        QueryResult<KVPair> Get(string key, QueryOptions q, CancellationToken ct);
        QueryResult<string[]> Keys(string prefix);
        QueryResult<string[]> Keys(string prefix, string separator);
        QueryResult<string[]> Keys(string prefix, string separator, QueryOptions q);
        QueryResult<string[]> Keys(string prefix, string separator, QueryOptions q, CancellationToken ct);
        QueryResult<KVPair[]> List(string prefix);
        QueryResult<KVPair[]> List(string prefix, QueryOptions q);
        QueryResult<KVPair[]> List(string prefix, QueryOptions q, CancellationToken ct);
        WriteResult<bool> Put(KVPair p);
        WriteResult<bool> Put(KVPair p, WriteOptions q);
        WriteResult<bool> Release(KVPair p);
        WriteResult<bool> Release(KVPair p, WriteOptions q);
    }
}