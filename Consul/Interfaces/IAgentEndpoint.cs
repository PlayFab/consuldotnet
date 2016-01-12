using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Consul
{
    public interface IAgentEndpoint
    {
        WriteResult CheckDeregister(string checkID);
        WriteResult CheckRegister(AgentCheckRegistration check);
        QueryResult<System.Collections.Generic.Dictionary<string, AgentCheck>> Checks();
        WriteResult DisableNodeMaintenance();
        WriteResult DisableServiceMaintenance(string serviceID);
        WriteResult EnableNodeMaintenance(string reason);
        WriteResult EnableServiceMaintenance(string serviceID, string reason);
        void FailTTL(string checkID, string note);
        WriteResult ForceLeave(string node);
        WriteResult Join(string addr, bool wan);
        QueryResult<AgentMember[]> Members(bool wan);
        string NodeName { get; }
        void PassTTL(string checkID, string note);
        QueryResult<Dictionary<string, Dictionary<string, dynamic>>> Self();
        WriteResult ServiceDeregister(string serviceID);
        WriteResult ServiceRegister(AgentServiceRegistration service);
        QueryResult<System.Collections.Generic.Dictionary<string, AgentService>> Services();
        WriteResult UpdateTTL(string checkID, string note, TTLStatus status);
        void WarnTTL(string checkID, string note);
    }

    public interface IAgentEndpointAsync
    {
        Task<QueryResult<Dictionary<string, Dictionary<string, dynamic>>>> SelfAsync();
    }
}
