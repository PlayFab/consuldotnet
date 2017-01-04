// -----------------------------------------------------------------------
//  <copyright file="CatalogTest.cs" company="PlayFab Inc">
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
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    public class CatalogTest : IDisposable
    {
        AsyncReaderWriterLock.Releaser m_lock;
        public CatalogTest()
        {
            m_lock = AsyncHelpers.RunSync(() => SelectiveParallel.Parallel());
        }

        public void Dispose()
        {
            m_lock.Dispose();
        }
    
        [Fact]
        public async Task Catalog_Datacenters()
        {
            var client = new ConsulClient();
            var datacenterList = await client.Catalog.Datacenters();

            Assert.NotEqual(0, datacenterList.Response.Length);
        }

        [Fact]
        public async Task Catalog_Nodes()
        {
            var client = new ConsulClient();
            var nodeList = await client.Catalog.Nodes();

            Assert.NotEqual((ulong)0, nodeList.LastIndex);
            Assert.NotEqual(0, nodeList.Response.Length);
            // make sure deserialization is working right
            Assert.NotNull(nodeList.Response[0].Address);
            Assert.NotNull(nodeList.Response[0].Name);
        }

        [Fact]
        public async Task Catalog_Services()
        {
            var client = new ConsulClient();
            var servicesList = await client.Catalog.Services();

            Assert.NotEqual((ulong)0, servicesList.LastIndex);
            Assert.NotEqual(0, servicesList.Response.Count);
        }

        [Fact]
        public async Task Catalog_Service()
        {
            var client = new ConsulClient();
            var serviceList = await client.Catalog.Service("consul");

            Assert.NotEqual((ulong)0, serviceList.LastIndex);
            Assert.NotEqual(0, serviceList.Response.Length);
        }

        [Fact]
        public async Task Catalog_Node()
        {
            var client = new ConsulClient();

            var node = await client.Catalog.Node(await client.Agent.GetNodeName());

            Assert.NotEqual((ulong)0, node.LastIndex);
            Assert.NotNull(node.Response.Services);
            Assert.Equal("127.0.0.1", node.Response.Node.Address);
            Assert.True(node.Response.Node.TaggedAddresses.Count > 0);
            Assert.True(node.Response.Node.TaggedAddresses.ContainsKey("wan"));
        }

        [Fact]
        public async Task Catalog_RegistrationDeregistration()
        {
            var client = new ConsulClient();
            var svcID = KVTest.GenerateTestKeyName();
            var service = new AgentService()
            {
                ID = svcID,
                Service = "redis",
                Tags = new[] { "master", "v1" },
                Port = 8000
            };

            var check = new AgentCheck()
            {
                Node = "foobar",
                CheckID = "service:" + svcID,
                Name = "Redis health check",
                Notes = "Script based health check",
                Status = HealthStatus.Passing,
                ServiceID = svcID
            };

            var registration = new CatalogRegistration()
            {
                Datacenter = "dc1",
                Node = "foobar",
                Address = "192.168.10.10",
                Service = service,
                Check = check
            };

            await client.Catalog.Register(registration);

            var node = await client.Catalog.Node("foobar");
            Assert.True(node.Response.Services.ContainsKey(svcID));

            var health = await client.Health.Node("foobar");
            Assert.Equal("service:" + svcID, health.Response[0].CheckID);

            var dereg = new CatalogDeregistration()
            {
                Datacenter = "dc1",
                Node = "foobar",
                Address = "192.168.10.10",
                CheckID = "service:" + svcID
            };

            await client.Catalog.Deregister(dereg);

            health = await client.Health.Node("foobar");
            Assert.Equal(0, health.Response.Length);

            dereg = new CatalogDeregistration()
            {
                Datacenter = "dc1",
                Node = "foobar",
                Address = "192.168.10.10"
            };

            await client.Catalog.Deregister(dereg);

            node = await client.Catalog.Node("foobar");
            Assert.Null(node.Response);
        }

        [Fact]
        public async Task Catalog_EnableTagOverride()
        {
            var svcID = KVTest.GenerateTestKeyName();
            var service = new AgentService()
            {
                ID = svcID,
                Service = svcID,
                Tags = new[] { "master", "v1" },
                Port = 8000
            };

            var registration = new CatalogRegistration()
            {
                Datacenter = "dc1",
                Node = "foobar",
                Address = "192.168.10.10",
                Service = service
            };

            using (IConsulClient client = new ConsulClient())
            {
                await client.Catalog.Register(registration);

                var node = await client.Catalog.Node("foobar");

                Assert.Contains(svcID, node.Response.Services.Keys);
                Assert.False(node.Response.Services[svcID].EnableTagOverride);

                var services = await client.Catalog.Service(svcID);

                Assert.NotEmpty(services.Response);
                Assert.Equal(svcID, services.Response[0].ServiceName);

                Assert.False(services.Response[0].ServiceEnableTagOverride);
            }

            // Use a new scope
            using (IConsulClient client = new ConsulClient())
            {
                service.EnableTagOverride = true;

                await client.Catalog.Register(registration);
                var node = await client.Catalog.Node("foobar");

                Assert.Contains(svcID, node.Response.Services.Keys);
                Assert.True(node.Response.Services[svcID].EnableTagOverride);

                var services = await client.Catalog.Service(svcID);

                Assert.NotEmpty(services.Response);
                Assert.Equal(svcID, services.Response[0].ServiceName);

                Assert.True(services.Response[0].ServiceEnableTagOverride);
            }
        }
    }
}