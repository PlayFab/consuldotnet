namespace Consul
{
    public interface IStatusEndpoint
    {
        string Leader();
        string[] Peers();
    }
}