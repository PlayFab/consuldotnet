namespace Consul
{
    public interface ICoordinateEndpoint
    {
        QueryResult<CoordinateDatacenterMap[]> Datacenters();
        QueryResult<CoordinateEntry[]> Nodes();
        QueryResult<CoordinateEntry[]> Nodes(QueryOptions q);
    }
}