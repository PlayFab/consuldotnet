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
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface ISessionEndpoint
    {
        WriteResult<string> Create();
        WriteResult<string> Create(SessionEntry se);
        WriteResult<string> Create(SessionEntry se, WriteOptions q);
        WriteResult<string> CreateNoChecks();
        WriteResult<string> CreateNoChecks(SessionEntry se);
        WriteResult<string> CreateNoChecks(SessionEntry se, WriteOptions q);
        WriteResult<bool> Destroy(string id);
        WriteResult<bool> Destroy(string id, WriteOptions q);
        QueryResult<SessionEntry> Info(string id);
        QueryResult<SessionEntry> Info(string id, QueryOptions q);
        QueryResult<SessionEntry[]> List();
        QueryResult<SessionEntry[]> List(QueryOptions q);
        QueryResult<SessionEntry[]> Node(string node);
        QueryResult<SessionEntry[]> Node(string node, QueryOptions q);
        WriteResult<SessionEntry> Renew(string id);
        WriteResult<SessionEntry> Renew(string id, WriteOptions q);
        Task RenewPeriodic(TimeSpan initialTTL, string id, CancellationToken ct);
        Task RenewPeriodic(TimeSpan initialTTL, string id, WriteOptions q, CancellationToken ct);
    }
}