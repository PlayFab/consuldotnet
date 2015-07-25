using System;
using System.Threading;
namespace Consul
{
    public interface ICatalogEndpoint
    {
        QueryResult<string[]> Datacenters();
        WriteResult Deregister(CatalogDeregistration reg);
        WriteResult Deregister(CatalogDeregistration reg, WriteOptions q);
        QueryResult<CatalogNode> Node(string node);
        QueryResult<CatalogNode> Node(string node, QueryOptions q);
        QueryResult<Node[]> Nodes();
        QueryResult<Node[]> Nodes(QueryOptions q);
        QueryResult<Node[]> Nodes(QueryOptions q, CancellationToken ct);
        WriteResult Register(CatalogRegistration reg);
        WriteResult Register(CatalogRegistration reg, WriteOptions q);
        QueryResult<CatalogService[]> Service(string service);
        QueryResult<CatalogService[]> Service(string service, string tag);
        QueryResult<CatalogService[]> Service(string service, string tag, QueryOptions q);
        QueryResult<System.Collections.Generic.Dictionary<string, string[]>> Services();
        QueryResult<System.Collections.Generic.Dictionary<string, string[]>> Services(QueryOptions q);
        QueryResult<System.Collections.Generic.Dictionary<string, string[]>> Services(QueryOptions q, CancellationToken ct);
    }
}
