using System.Threading.Tasks;

namespace Consul
{
    public interface ICoordinateEndpoint
    {
        Task<QueryResult<CoordinateDatacenterMap[]>> Datacenters();
        Task<QueryResult<CoordinateEntry[]>> Nodes();
        Task<QueryResult<CoordinateEntry[]>> Nodes(QueryOptions q);
    }
}