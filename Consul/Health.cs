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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    /// <summary>
    /// The status of a health check
    /// </summary>
    public class HealthStatus : IEquatable<HealthStatus>
    {
        private static readonly HealthStatus passing = new HealthStatus() { Status = "passing" };
        private static readonly HealthStatus warning = new HealthStatus() { Status = "warning" };
        private static readonly HealthStatus critical = new HealthStatus() { Status = "critical" };
        private static readonly HealthStatus maintenance = new HealthStatus() { Status = "maintenance" };
        private static readonly HealthStatus any = new HealthStatus() { Status = "any" };

        public static readonly string NodeMaintenance = "_node_maintenance";
        public static readonly string ServiceMaintenancePrefix = "_service_maintenance:";

        public string Status { get; private set; }

        public static HealthStatus Passing
        {
            get { return passing; }
        }

        public static HealthStatus Warning
        {
            get { return warning; }
        }

        public static HealthStatus Critical
        {
            get { return critical; }
        }

        public static HealthStatus Maintenance
        {
            get { return maintenance; }
        }

        public static HealthStatus Any
        {
            get { return any; }
        }

        public bool Equals(HealthStatus other)
        {
            return other != null && ReferenceEquals(this, other);
        }

        public override bool Equals(object other)
        {
            // other could be a reference type, the is operator will return false if null
            return other is HealthStatus && Equals(other as HealthStatus);
        }

        public override int GetHashCode()
        {
            return Status.GetHashCode();
        }
    }

    public class HealthStatusConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((HealthStatus)value).Status);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            var status = (string)serializer.Deserialize(reader, typeof(string));
            switch (status)
            {
                case "passing":
                    return HealthStatus.Passing;
                case "warning":
                    return HealthStatus.Warning;
                case "critical":
                    return HealthStatus.Critical;
                default:
                    throw new ArgumentException("Invalid Check status value during deserialization");
            }
        }

        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(HealthStatus))
            {
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// HealthCheck is used to represent a single check
    /// </summary>
    public class HealthCheck
    {
        public string Node { get; set; }
        public string CheckID { get; set; }
        public string Name { get; set; }
        [JsonConverter(typeof(HealthStatusConverter))]
        public HealthStatus Status { get; set; }
        public string Notes { get; set; }
        public string Output { get; set; }
        public string ServiceID { get; set; }
        public string ServiceName { get; set; }
    }

    public static class HealthCheckExtension
    {
        public static HealthStatus AggregatedStatus(this IEnumerable<HealthCheck> checks)
        {
            bool warning = false, critical = false, maintenance = false;
            foreach (var check in checks)
            {
                if (check.CheckID == HealthStatus.NodeMaintenance || check.CheckID.StartsWith(HealthStatus.ServiceMaintenancePrefix))
                {
                    maintenance = true;
                }
                else if (check.Status == HealthStatus.Warning)
                {
                    warning = true;
                }
                else if (check.Status == HealthStatus.Critical)
                {
                    critical = true;
                }
            }

            if (maintenance)
            {
                return HealthStatus.Maintenance;
            }
            else if (critical)
            {
                return HealthStatus.Critical;
            }
            else if (warning)
            {
                return HealthStatus.Warning;
            }
            else
            {
                return HealthStatus.Passing;
            }
        }
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
        public Task<QueryResult<HealthCheck[]>> Checks(string service, CancellationToken ct = default(CancellationToken))
        {
            return Checks(service, QueryOptions.Default, ct);
        }

        /// <summary>
        /// Checks is used to return the checks associated with a service
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing the health checks matching the provided service ID, or a query result with a null response if no service matched the provided ID</returns>
        public Task<QueryResult<HealthCheck[]>> Checks(string service, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<HealthCheck[]>(string.Format("/v1/health/checks/{0}", service), q).Execute(ct);
        }

        /// <summary>
        /// Node is used to query for checks belonging to a given node
        /// </summary>
        /// <param name="node">The node name</param>
        /// <returns>A query result containing the health checks matching the provided node ID, or a query result with a null response if no node matched the provided ID</returns>
        public Task<QueryResult<HealthCheck[]>> Node(string node, CancellationToken ct = default(CancellationToken))
        {
            return Node(node, QueryOptions.Default, ct);
        }

        /// <summary>
        /// Node is used to query for checks belonging to a given node
        /// </summary>
        /// <param name="node">The node name</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing the health checks matching the provided node ID, or a query result with a null response if no node matched the provided ID</returns>
        public Task<QueryResult<HealthCheck[]>> Node(string node, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<HealthCheck[]>(string.Format("/v1/health/node/{0}", node), q).Execute(ct);
        }

        /// <summary>
        /// Service is used to query health information along with service info for a given service. It can optionally do server-side filtering on a tag or nodes with passing health checks only.
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <returns>A query result containing the service members matching the provided service ID, or a query result with a null response if no service members matched the filters provided</returns>
        public Task<QueryResult<ServiceEntry[]>> Service(string service, CancellationToken ct = default(CancellationToken))
        {
            return Service(service, string.Empty, false, QueryOptions.Default, ct);
        }

        /// <summary>
        /// Service is used to query health information along with service info for a given service. It can optionally do server-side filtering on a tag or nodes with passing health checks only.
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="tag">The service member tag</param>
        /// <returns>A query result containing the service members matching the provided service ID and tag, or a query result with a null response if no service members matched the filters provided</returns>
        public Task<QueryResult<ServiceEntry[]>> Service(string service, string tag, CancellationToken ct = default(CancellationToken))
        {
            return Service(service, tag, false, QueryOptions.Default, ct);
        }

        /// <summary>
        /// Service is used to query health information along with service info for a given service. It can optionally do server-side filtering on a tag or nodes with passing health checks only.
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="tag">The service member tag</param>
        /// <param name="passingOnly">Only return if the health check is in the Passing state</param>
        /// <returns>A query result containing the service members matching the provided service ID, tag, and health status, or a query result with a null response if no service members matched the filters provided</returns>
        public Task<QueryResult<ServiceEntry[]>> Service(string service, string tag, bool passingOnly, CancellationToken ct = default(CancellationToken))
        {
            return Service(service, tag, passingOnly, QueryOptions.Default, ct);
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
        public Task<QueryResult<ServiceEntry[]>> Service(string service, string tag, bool passingOnly, QueryOptions q, CancellationToken ct = default(CancellationToken))
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
            return req.Execute(ct);
        }

        /// <summary>
        /// State is used to retrieve all the checks in a given state. The wildcard "any" state can also be used for all checks.
        /// </summary>
        /// <param name="status">The health status to filter for</param>
        /// <returns>A query result containing a list of health checks in the specified state, or a query result with a null response if no health checks matched the provided state</returns>
        public Task<QueryResult<HealthCheck[]>> State(HealthStatus status, CancellationToken ct = default(CancellationToken))
        {
            return State(status, QueryOptions.Default, ct);
        }

        /// <summary>
        /// // State is used to retrieve all the checks in a given state. The wildcard "any" state can also be used for all checks.
        /// </summary>
        /// <param name="status">The health status to filter for</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing a list of health checks in the specified state, or a query result with a null response if no health checks matched the provided state</returns>
        public Task<QueryResult<HealthCheck[]>> State(HealthStatus status, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<HealthCheck[]>(string.Format("/v1/health/state/{0}", status.Status), q).Execute(ct);
        }
    }

    public partial class ConsulClient : IConsulClient
    {
        private Lazy<Health> _health;

        /// <summary>
        /// Health returns a handle to the health endpoint
        /// </summary>
        public IHealthEndpoint Health
        {
            get
            {
                return _health.Value;
            }
        }
    }
}