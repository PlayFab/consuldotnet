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
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface IAgentEndpoint
    {
        Task<WriteResult> CheckDeregister(string checkID, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> CheckRegister(AgentCheckRegistration check, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<Dictionary<string, AgentCheck>>> Checks(CancellationToken ct = default(CancellationToken));
        Task<WriteResult> DisableNodeMaintenance(CancellationToken ct = default(CancellationToken));
        Task<WriteResult> DisableServiceMaintenance(string serviceID, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> EnableNodeMaintenance(string reason, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> EnableServiceMaintenance(string serviceID, string reason, CancellationToken ct = default(CancellationToken));
        Task FailTTL(string checkID, string note, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> ForceLeave(string node, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> Join(string addr, bool wan, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<AgentMember[]>> Members(bool wan, CancellationToken ct = default(CancellationToken));
        [Obsolete("This property will be removed in 0.8.0. Replace uses of it with a call to GetNodeName()")]
        string NodeName { get; }
        Task<string> GetNodeName(CancellationToken ct = default(CancellationToken));
        Task PassTTL(string checkID, string note, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<Dictionary<string, Dictionary<string, dynamic>>>> Self(CancellationToken ct = default(CancellationToken));
        Task<WriteResult> ServiceDeregister(string serviceID, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> ServiceRegister(AgentServiceRegistration service, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<Dictionary<string, AgentService>>> Services(CancellationToken ct = default(CancellationToken));
        Task<WriteResult> UpdateTTL(string checkID, string output, TTLStatus status, CancellationToken ct = default(CancellationToken));
        Task WarnTTL(string checkID, string note, CancellationToken ct = default(CancellationToken));
    }
}
