using System.Threading.Tasks;

namespace Consul
{
    public interface IStatusEndpoint
    {
        string Leader();
        string[] Peers();
    }

    public interface IStatusEndpointAsync
    {
        Task<string> LeaderAsync();
        Task<string[]> PeersAsync();
    }
}