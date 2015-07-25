using System.Threading;

namespace Consul
{
    public interface IHealthEndpoint
    {
        QueryResult<HealthCheck[]> Checks(string service);
        QueryResult<HealthCheck[]> Checks(string service, QueryOptions q);
        QueryResult<HealthCheck[]> Checks(string service, QueryOptions q, CancellationToken ct);
        QueryResult<HealthCheck[]> Node(string node);
        QueryResult<HealthCheck[]> Node(string node, QueryOptions q);
        QueryResult<HealthCheck[]> Node(string node, QueryOptions q, CancellationToken ct);
        QueryResult<ServiceEntry[]> Service(string service);
        QueryResult<ServiceEntry[]> Service(string service, string tag);
        QueryResult<ServiceEntry[]> Service(string service, string tag, bool passingOnly);
        QueryResult<ServiceEntry[]> Service(string service, string tag, bool passingOnly, QueryOptions q);
        QueryResult<ServiceEntry[]> Service(string service, string tag, bool passingOnly, QueryOptions q, CancellationToken ct);
        QueryResult<HealthCheck[]> State(CheckStatus status);
        QueryResult<HealthCheck[]> State(CheckStatus status, QueryOptions q);
        QueryResult<HealthCheck[]> State(CheckStatus status, QueryOptions q, CancellationToken ct);
    }
}