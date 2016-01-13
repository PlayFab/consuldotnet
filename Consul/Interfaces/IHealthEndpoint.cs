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