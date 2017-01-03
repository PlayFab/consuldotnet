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

using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface IHealthEndpoint
    {
        Task<QueryResult<HealthCheck[]>> Checks(string service, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<HealthCheck[]>> Checks(string service, QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<HealthCheck[]>> Node(string node, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<HealthCheck[]>> Node(string node, QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<ServiceEntry[]>> Service(string service, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<ServiceEntry[]>> Service(string service, string tag, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<ServiceEntry[]>> Service(string service, string tag, bool passingOnly, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<ServiceEntry[]>> Service(string service, string tag, bool passingOnly, QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<HealthCheck[]>> State(HealthStatus status, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<HealthCheck[]>> State(HealthStatus status, QueryOptions q, CancellationToken ct = default(CancellationToken));
    }
}