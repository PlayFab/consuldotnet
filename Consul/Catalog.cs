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

using System.Collections.Generic;

namespace Consul
{
    public class Node
    {
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
    public class Catalog
    {
        private readonly Client _client;

        internal Catalog(Client c)
        {
            _client = c;
        }

        /// <summary>
        /// Register a new catalog item
        /// </summary>
        /// <param name="reg">A catalog registration</param>
        /// <returns>An empty write result</returns>
        public WriteResult<object> Register(CatalogRegistration reg)
        {
            return Register(reg, WriteOptions.Empty);
        }

        /// <summary>
        /// Register a new catalog item
        /// </summary>
        /// <param name="reg">A catalog registration</param>
        /// <param name="q">Customized write options</param>
        /// <returns>An empty write result</returns>
        public WriteResult<object> Register(CatalogRegistration reg, WriteOptions q)
        {
            return
                _client.CreateWriteRequest<CatalogRegistration, object>("/v1/catalog/register", reg, q).Execute();
        }

        /// <summary>
        /// Deregister an existing catalog item
        /// </summary>
        /// <param name="reg">A catalog deregistration</param>
        /// <returns>An empty write result</returns>
        public WriteResult<object> Deregister(CatalogDeregistration reg)
        {
            return Deregister(reg, WriteOptions.Empty);
        }

        /// <summary>
        /// Deregister an existing catalog item
        /// </summary>
        /// <param name="reg">A catalog deregistration</param>
        /// <param name="q">Customized write options</param>
        /// <returns>An empty write result</returns>
        public WriteResult<object> Deregister(CatalogDeregistration reg, WriteOptions q)
        {
            return _client.CreateWriteRequest<CatalogDeregistration, object>("/v1/catalog/deregister", reg, q)
                        .Execute();
        }

        /// <summary>
        /// Datacenters is used to query for all the known datacenters
        /// </summary>
        /// <returns>A list of datacenter names</returns>
        public QueryResult<string[]> Datacenters()
        {
            return _client.CreateQueryRequest<string[]>("/v1/catalog/datacenters").Execute();
        }

        /// <summary>
        /// Nodes is used to query all the known nodes
        /// </summary>
        /// <returns>A list of all nodes</returns>
        public QueryResult<Node[]> Nodes()
        {
            return Nodes(QueryOptions.Empty);
        }

        /// <summary>
        /// Nodes is used to query all the known nodes
        /// </summary>
        /// <param name="q">Customized query options</param>
        /// <returns>A list of all nodes</returns>
        public QueryResult<Node[]> Nodes(QueryOptions q)
        {
            return _client.CreateQueryRequest<Node[]>("/v1/catalog/nodes", q).Execute();
        }

        /// <summary>
        /// Services is used to query for all known services
        /// </summary>
        /// <returns>A list of all services</returns>
        public QueryResult<Dictionary<string, string[]>> Services()
        {
            return Services(QueryOptions.Empty);
        }

        /// <summary>
        /// Services is used to query for all known services
        /// </summary>
        /// <param name="q">Customized query options</param>
        /// <returns>A list of all services</returns>
        public QueryResult<Dictionary<string, string[]>> Services(QueryOptions q)
        {
            return _client.CreateQueryRequest<Dictionary<string, string[]>>("/v1/catalog/services", q).Execute();
        }

        /// <summary>
        /// Service is used to query catalog entries for a given service
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <returns>A list of service instances</returns>
        public QueryResult<CatalogService[]> Service(string service)
        {
            return Service(service, string.Empty, QueryOptions.Empty);
        }

        /// <summary>
        /// Service is used to query catalog entries for a given service
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="tag">A tag to filter on</param>
        /// <returns>A list of service instances</returns>
        public QueryResult<CatalogService[]> Service(string service, string tag)
        {
            return Service(service, tag, QueryOptions.Empty);
        }

        /// <summary>
        /// Service is used to query catalog entries for a given service
        /// </summary>
        /// <param name="service">The service ID</param>
        /// <param name="tag">A tag to filter on</param>
        /// <param name="q">Customized query options</param>
        /// <returns>A list of service instances</returns>
        public QueryResult<CatalogService[]> Service(string service, string tag, QueryOptions q)
        {
            var req = _client.CreateQueryRequest<CatalogService[]>(string.Format("/v1/catalog/service/{0}", service), q);
            if (!string.IsNullOrEmpty(tag))
            {
                req.Params["tag"] = tag;
            }
            return req.Execute();
        }

        /// <summary>
        /// Node is used to query for service information about a single node
        /// </summary>
        /// <param name="node">The node name</param>
        /// <returns>The node information including a list of services</returns>
        public QueryResult<CatalogNode> Node(string node)
        {
            return Node(node, QueryOptions.Empty);
        }

        /// <summary>
        /// Node is used to query for service information about a single node
        /// </summary>
        /// <param name="node">The node name</param>
        /// <param name="q">Customized query options</param>
        /// <returns>The node information including a list of services</returns>
        public QueryResult<CatalogNode> Node(string node, QueryOptions q)
        {
            return
                _client.CreateQueryRequest<CatalogNode>(string.Format("/v1/catalog/node/{0}", node), q).Execute();
        }
    }

    public partial class Client
    {
        private Catalog _catalog;

        /// <summary>
        /// Catalog returns a handle to the catalog endpoints
        /// </summary>
        public Catalog Catalog
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