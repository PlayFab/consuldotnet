using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface ICatalogEndpoint
    {
        Task<QueryResult<string[]>> Datacenters();
        Task<WriteResult> Deregister(CatalogDeregistration reg);
        Task<WriteResult> Deregister(CatalogDeregistration reg, WriteOptions q);
        Task<QueryResult<CatalogNode>> Node(string node);
        Task<QueryResult<CatalogNode>> Node(string node, QueryOptions q);
        Task<QueryResult<Node[]>> Nodes();
        Task<QueryResult<Node[]>> Nodes(QueryOptions q);
        Task<QueryResult<Node[]>> Nodes(QueryOptions q, CancellationToken ct);
        Task<WriteResult> Register(CatalogRegistration reg);
        Task<WriteResult> Register(CatalogRegistration reg, WriteOptions q);
        Task<QueryResult<CatalogService[]>> Service(string service);
        Task<QueryResult<CatalogService[]>> Service(string service, string tag);
        Task<QueryResult<CatalogService[]>> Service(string service, string tag, QueryOptions q);
        Task<QueryResult<CatalogService[]>> Service(string service, CancellationToken ct);
        Task<QueryResult<CatalogService[]>> Service(string service, string tag, CancellationToken ct);
        Task<QueryResult<CatalogService[]>> Service(string service, string tag, QueryOptions q, CancellationToken ct);
        Task<QueryResult<Dictionary<string, string[]>>> Services();
        Task<QueryResult<Dictionary<string, string[]>>> Services(QueryOptions q);
        Task<QueryResult<Dictionary<string, string[]>>> Services(QueryOptions q, CancellationToken ct);
    }
}
