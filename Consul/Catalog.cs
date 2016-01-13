// -----------------------------------------------------------------------
//  <copyright file="Catalog.cs" company="PlayFab Inc">
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public class Node
    {
        // Cannot be "Node" as in the Go API because in C#, properties cannot
        // have the same name as their enclosing class.
        [JsonProperty(PropertyName = "Node")]
        public string Name { get; set; }
        public string Address { get; set; }
    }

    public class CatalogService
    {
        public string Node { get; set; }
        public string Address { get; set; }
        public string ServiceID { get; set; }
        public string ServiceName { get; set; }
        public string ServiceAddress { get; set; }
        public string[] ServiceTags { get; set; }
        public int ServicePort { get; set; }
    }

    public class CatalogNode
    {
        public Node Node { get; set; }
        public Dictionary<string, AgentService> Services { get; set; }

        public CatalogNode()
        {
            Services = new Dictionary<string, AgentService>();
        }
    }

    public class CatalogRegistration
    {
        public string Node { get; set; }
        public string Address { get; set; }
        public string Datacenter { get; set; }
        public AgentService Service { get; set; }
        public AgentCheck Check { get; set; }
    }

    public class CatalogDeregistration
    {
        public string Node { get; set; }
        public string Address { get; set; }
        public string Datacenter { get; set; }
        public string ServiceID { get; set; }
        public string CheckID { get; set; }
    }

    /// <summary>
    /// Catalog can be used to query the Catalog endpoints
    /// </summary>
    public class Catalog : ICatalogEndpoint
    {
        private readonly ConsulClient _client;

        internal Catalog(ConsulClient c)
        {
            _client = c;
        }

        /// <summary>
        /// Register a new catalog item
        /// </summary>
        /// <param name="reg">A catalog registration</param>
        /// <returns>An empty write result</returns>
        public async Task<WriteResult> Register(CatalogRegistration reg)
        {
            return await Register(reg, WriteOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Register a new catalog item
        /// </summary>
        /// <param name="reg">A catalog registration</param>
        /// <param name="q">Customized write options</param>
        /// <returns>An empty write result</returns>
        public async Task<WriteResult> Register(CatalogRegistration reg, WriteOptions q)
        {
            return await _client.Put("/v1/catalog/register", reg, q).Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// Deregister an existing catalog item
        /// </summary>
        /// <param name="reg">A catalog deregistration</param>
        /// <returns>An empty write result</returns>
        public async Task<WriteResult> Deregister(CatalogDeregistration reg)
        {
            return await Deregister(reg, WriteOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Deregister an existing catalog item
        /// </summary>
        /// <param name="reg">A catalog deregistration</param>
        /// <param name="q">Customized write options</param>
        /// <returns>An empty write result</returns>
        public async Task<WriteResult> Deregister(CatalogDeregistration reg, WriteOptions q)
        {
            return await _client.Put("/v1/catalog/deregister", reg, q).Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// Datacenters is used to query for all the known datacenters
        /// </summary>
        /// <returns>A list of datacenter names</returns>
        public async Task<QueryResult<string[]>> Datacenters()
        {
            return await _client.Get<string[]>("/v1/catalog/datacenters").Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// Nodes is used to query all the known nodes
        /// </summary>
        /// <returns>A list of all nodes</returns>
        public async Task<QueryResult<Node[]>> Nodes()
        {
            return await Nodes(QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }
        /// <summary>
        /// Nodes is used to query all the known nodes
        /// </summary>
        /// <returns>A list of all nodes</returns>
        public async Task<QueryResult<Node[]>> Nodes(QueryOptions q)
        {
            return await Nodes(q, CancellationToken.None).ConfigureAwait(false);
        }
        /// <summary>
        /// Nodes is used to query all the known nodes
        /// </summary>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A list of all nodes</returns>
        public async Task<QueryResult<Node[]>> Nodes(QueryOptions q, CancellationToken ct)
        {
            return await _client.Get<Node[]>("/v1/catalog/nodes", q).Execute(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Services is used to query for all known services
        /// </summary>
        /// <returns>A list of all services</returns>
        public async Task<QueryResult<Dictionary<string, string[]>>> Services()
        {
            return await Services(QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }
        /// <summary>
        /// Services is used to query for all known services
        /// </summary>
        /// <param name="q">Customized query options</param>
        /// <returns>A list of all services</returns>
        public async Task<QueryResult<Dictionary<string, string[]>>> Services(QueryOptions q)
        {
            return await Services(q, CancellationToken.None).ConfigureAwait(false);
        }
        /// <summary>
        /// Services is used to query for all known services
        /// </summary>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A list of all services</returns>
        public async Task<QueryResult<Dictionary<string, string[]>>> Services(QueryOptions q, CancellationToken ct)
        {
            return await _client.Get<Dictionary<string, string[]>>("/v1/catalog/services", q).Execute(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Service is used to query catalog entries for a given service
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <returns>A list of service instances</returns>
        public async Task<QueryResult<CatalogService[]>> Service(string service)
        {
            return await Service(service, string.Empty, QueryOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Service is used to query catalog entries for a given service
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="tag">A tag to filter on</param>
        /// <returns>A list of service instances</returns>
        public async Task<QueryResult<CatalogService[]>> Service(string service, string tag)
        {
            return await Service(service, tag, QueryOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Service is used to query catalog entries for a given service
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="tag">A tag to filter on</param>
        /// <param name="q">Customized query options</param>
        /// <returns>A list of service instances</returns>
        public async Task<QueryResult<CatalogService[]>> Service(string service, string tag, QueryOptions q)
        {
            var req = _client.Get<CatalogService[]>(string.Format("/v1/catalog/service/{0}", service), q);
            if (!string.IsNullOrEmpty(tag))
            {
                req.Params["tag"] = tag;
            }
            return await req.Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// Node is used to query for service information about a single node
        /// </summary>
        /// <param name="node">The node name</param>
        /// <returns>The node information including a list of services</returns>
        public async Task<QueryResult<CatalogNode>> Node(string node)
        {
            return await Node(node, QueryOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Node is used to query for service information about a single node
        /// </summary>
        /// <param name="node">The node name</param>
        /// <param name="q">Customized query options</param>
        /// <returns>The node information including a list of services</returns>
        public async Task<QueryResult<CatalogNode>> Node(string node, QueryOptions q)
        {
            return await _client.Get<CatalogNode>(string.Format("/v1/catalog/node/{0}", node), q).Execute().ConfigureAwait(false);
        }
    }

    public partial class ConsulClient : IConsulClient
    {
        private Catalog _catalog;

        /// <summary>
        /// Catalog returns a handle to the catalog endpoints
        /// </summary>
        public ICatalogEndpoint Catalog
        {
            get
            {
                if (_catalog == null)
                {
                    lock (_lock)
                    {
                        if (_catalog == null)
                        {
                            _catalog = new Catalog(this);
                        }
                    }
                }
                return _catalog;
            }
        }
    }
}
