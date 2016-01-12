using System;
using System.Threading;
using System.Threading.Tasks;

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

    public interface ICatalogEndpointAsync
    {
        Task<QueryResult<string[]>> DatacentersAsync();
        Task<QueryResult<CatalogNode>> NodeAsync(string node);
        Task<QueryResult<CatalogNode>> NodeAsync(string node, QueryOptions q);
        Task<QueryResult<CatalogService[]>> ServiceAsync(string service);
        Task<QueryResult<CatalogService[]>> ServiceAsync(string service, string tag);
        Task<QueryResult<CatalogService[]>> ServiceAsync(string service, string tag, QueryOptions q);
        Task<QueryResult<System.Collections.Generic.Dictionary<string, string[]>>> ServicesAsync();
        Task<QueryResult<System.Collections.Generic.Dictionary<string, string[]>>> ServicesAsync(QueryOptions q);
        Task<QueryResult<System.Collections.Generic.Dictionary<string, string[]>>> ServicesAsync(QueryOptions q, CancellationToken ct);
    }
}
