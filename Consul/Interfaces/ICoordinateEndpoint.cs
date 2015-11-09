namespace Consul
{
    public interface ICoordinateEndpoint
    {
        QueryResult<CoordinateDatacenterMap[]> Datacenters();
        QueryResult<CoordinateEntry[]> Node();
        QueryResult<CoordinateEntry[]> Node(QueryOptions q);
    }
}