// -----------------------------------------------------------------------
//  <copyright file="Health.cs" company="PlayFab Inc">>
//    Copyright 2015 PlayFab Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        Task<http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//  </copyright>>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Consul
{
    public interface IAgentEndpoint
    {
        Task<WriteResult> CheckDeregister(string checkID);
        Task<WriteResult> CheckRegister(AgentCheckRegistration check);
        Task<QueryResult<Dictionary<string, AgentCheck>>> Checks();
        Task<WriteResult> DisableNodeMaintenance();
        Task<WriteResult> DisableServiceMaintenance(string serviceID);
        Task<WriteResult> EnableNodeMaintenance(string reason);
        Task<WriteResult> EnableServiceMaintenance(string serviceID, string reason);
        Task FailTTL(string checkID, string note);
        Task<WriteResult> ForceLeave(string node);
        Task<WriteResult> Join(string addr, bool wan);
        Task<QueryResult<AgentMember[]>> Members(bool wan);
        string NodeName { get; }
        Task PassTTL(string checkID, string note);
        Task<QueryResult<Dictionary<string, Dictionary<string, dynamic>>>> Self();
        Task<WriteResult> ServiceDeregister(string serviceID);
        Task<WriteResult> ServiceRegister(AgentServiceRegistration service);
        Task<QueryResult<Dictionary<string, AgentService>>> Services();
        Task<WriteResult> UpdateTTL(string checkID, string note, TTLStatus status);
        Task WarnTTL(string checkID, string note);
    }
}
