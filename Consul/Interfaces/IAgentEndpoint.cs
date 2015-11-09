// -----------------------------------------------------------------------
//  <copyright file="Health.cs" company="PlayFab Inc">
//    Copyright 2015 PlayFab Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
}
