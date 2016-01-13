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
    /// <summary>
    /// HealthCheck is used to represent a single check
    /// </summary>
    public class HealthCheck
    {
        public string Node { get; set; }
        public string CheckID { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public string Output { get; set; }
        public string ServiceID { get; set; }
        public string ServiceName { get; set; }
    }

    /// <summary>
    /// ServiceEntry is used for the health service endpoint
    /// </summary>
    public class ServiceEntry
    {
        public Node Node { get; set; }
        public AgentService Service { get; set; }
        public HealthCheck[] Checks { get; set; }
    }

    /// <summary>
    /// Health can be used to query the Health endpoints
    /// </summary>
    public class Health : IHealthEndpoint
    {
        private readonly ConsulClient _client;

        internal Health(ConsulClient c)
        {
            _client = c;
        }

        /// <summary>
        /// Checks is used to return the checks associated with a service
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <returns>A query result containing the health checks matching the provided service ID, or a query result with a null response if no service matched the provided ID</returns>
        public async Task<QueryResult<HealthCheck[]>> Checks(string service)
        {
            return await Checks(service, QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks is used to return the checks associated with a service
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <returns>A query result containing the health checks matching the provided service ID, or a query result with a null response if no service matched the provided ID</returns>
        public async Task<QueryResult<HealthCheck[]>> Checks(string service, QueryOptions q)
        {
            return await Checks(service, q, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks is used to return the checks associated with a service
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing the health checks matching the provided service ID, or a query result with a null response if no service matched the provided ID</returns>
        public async Task<QueryResult<HealthCheck[]>> Checks(string service, QueryOptions q, CancellationToken ct)
        {
            return await _client.Get<HealthCheck[]>(string.Format("/v1/health/checks/{0}", service), q).Execute(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Node is used to query for checks belonging to a given node
        /// </summary>
        /// <param name="node">The node name</param>
        /// <returns>A query result containing the health checks matching the provided node ID, or a query result with a null response if no node matched the provided ID</returns>
        public async Task<QueryResult<HealthCheck[]>> Node(string node)
        {
            return await Node(node, QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Node is used to query for checks belonging to a given node
        /// </summary>
        /// <param name="node">The node name</param>
        /// <returns>A query result containing the health checks matching the provided node ID, or a query result with a null response if no node matched the provided ID</returns>
        public async Task<QueryResult<HealthCheck[]>> Node(string node, QueryOptions q)
        {
            return await Node(node, q, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Node is used to query for checks belonging to a given node
        /// </summary>
        /// <param name="node">The node name</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing the health checks matching the provided node ID, or a query result with a null response if no node matched the provided ID</returns>
        public async Task<QueryResult<HealthCheck[]>> Node(string node, QueryOptions q, CancellationToken ct)
        {
            return await _client.Get<HealthCheck[]>(string.Format("/v1/health/node/{0}", node), q).Execute(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Service is used to query health information along with service info for a given service. It can optionally do server-side filtering on a tag or nodes with passing health checks only.
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <returns>A query result containing the service members matching the provided service ID, or a query result with a null response if no service members matched the filters provided</returns>
        public async Task<QueryResult<ServiceEntry[]>> Service(string service)
        {
            return await Service(service, string.Empty, false, QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Service is used to query health information along with service info for a given service. It can optionally do server-side filtering on a tag or nodes with passing health checks only.
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="tag">The service member tag</param>
        /// <returns>A query result containing the service members matching the provided service ID and tag, or a query result with a null response if no service members matched the filters provided</returns>
        public async Task<QueryResult<ServiceEntry[]>> Service(string service, string tag)
        {
            return await Service(service, tag, false, QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Service is used to query health information along with service info for a given service. It can optionally do server-side filtering on a tag or nodes with passing health checks only.
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="tag">The service member tag</param>
        /// <param name="passingOnly">Only return if the health check is in the Passing state</param>
        /// <returns>A query result containing the service members matching the provided service ID, tag, and health status, or a query result with a null response if no service members matched the filters provided</returns>
        public async Task<QueryResult<ServiceEntry[]>> Service(string service, string tag, bool passingOnly)
        {
            return await Service(service, tag, passingOnly, QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Service is used to query health information along with service info for a given service. It can optionally do server-side filtering on a tag or nodes with passing health checks only.
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="tag">The service member tag</param>
        /// <param name="passingOnly">Only return if the health check is in the Passing state</param>
        /// <returns>A query result containing the service members matching the provided service ID, tag, and health status, or a query result with a null response if no service members matched the filters provided</returns>
        public async Task<QueryResult<ServiceEntry[]>> Service(string service, string tag, bool passingOnly, QueryOptions q)
        {
            return await Service(service, tag, passingOnly, q, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Service is used to query health information along with service info for a given service. It can optionally do server-side filtering on a tag or nodes with passing health checks only.
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="tag">The service member tag</param>
        /// <param name="passingOnly">Only return if the health check is in the Passing state</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing the service members matching the provided service ID, tag, and health status, or a query result with a null response if no service members matched the filters provided</returns>
        public async Task<QueryResult<ServiceEntry[]>> Service(string service, string tag, bool passingOnly, QueryOptions q, CancellationToken ct)
        {
            var req = _client.Get<ServiceEntry[]>(string.Format("/v1/health/service/{0}", service), q);
            if (!string.IsNullOrEmpty(tag))
            {
                req.Params["tag"] = tag;
            }
            if (passingOnly)
            {
                req.Params["passing"] = "1";
            }
            return await req.Execute(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// State is used to retrieve all the checks in a given state. The wildcard "any" state can also be used for all checks.
        /// </summary>
        /// <param name="status">The health status to filter for</param>
        /// <returns>A query result containing a list of health checks in the specified state, or a query result with a null response if no health checks matched the provided state</returns>
        public async Task<QueryResult<HealthCheck[]>> State(CheckStatus status)
        {
            return await State(status, QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// // State is used to retrieve all the checks in a given state. The wildcard "any" state can also be used for all checks.
        /// </summary>
        /// <param name="status">The health status to filter for</param>
        /// <returns>A query result containing a list of health checks in the specified state, or a query result with a null response if no health checks matched the provided state</returns>
        public async Task<QueryResult<HealthCheck[]>> State(CheckStatus status, QueryOptions q)
        {
            return await State(status, q, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// // State is used to retrieve all the checks in a given state. The wildcard "any" state can also be used for all checks.
        /// </summary>
        /// <param name="status">The health status to filter for</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing a list of health checks in the specified state, or a query result with a null response if no health checks matched the provided state</returns>
        public async Task<QueryResult<HealthCheck[]>> State(CheckStatus status, QueryOptions q, CancellationToken ct)
        {
            return await _client.Get<HealthCheck[]>(string.Format("/v1/health/state/{0}", status.Status), q).Execute(ct).ConfigureAwait(false);
        }
    }

    public partial class ConsulClient : IConsulClient
    {
        private Health _health;

        /// <summary>
        /// Health returns a handle to the health endpoint
        /// </summary>
        public IHealthEndpoint Health
        {
            get
            {
                if (_health == null)
                {
                    lock (_lock)
                    {
                        if (_health == null)
                        {
                            _health = new Health(this);
                        }
                    }
                }
                return _health;
            }
        }
    }
}