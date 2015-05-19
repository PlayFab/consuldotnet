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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Consul.Test
{
    [TestClass]
    public class CatalogTest
    {
        [TestMethod]
        public void Catalog_Datacenters()
        {
            var c = ClientTest.MakeClient();
            var res = c.Catalog.Datacenters();
            res.Wait();

            Assert.AreNotEqual(0, res.Result.Response.Length);
        }

        [TestMethod]
        public void Catalog_Nodes()
        {
            var c = ClientTest.MakeClient();
            var res = c.Catalog.Nodes();
            res.Wait();

            Assert.AreNotEqual(0, res.Result.LastIndex);
            Assert.AreNotEqual(0, res.Result.Response.Length);
        }

        [TestMethod]
        public void Catalog_Services()
        {
            var c = ClientTest.MakeClient();
            var res = c.Catalog.Services();
            res.Wait();

            Assert.AreNotEqual(0, res.Result.LastIndex);
            Assert.AreNotEqual(0, res.Result.Response.Count);
        }

        [TestMethod]
        public void Catalog_Service()
        {
            var c = ClientTest.MakeClient();
            var res = c.Catalog.Service("consul");
            res.Wait();

            Assert.AreNotEqual(0, res.Result.LastIndex);
            Assert.AreNotEqual(0, res.Result.Response.Length);
        }

        [TestMethod]
        public void Catalog_Node()
        {
            var c = ClientTest.MakeClient();

            var res = c.Catalog.Node(c.Agent.NodeName);
            res.Wait();

            Assert.AreNotEqual(0, res.Result.LastIndex);
            Assert.IsNotNull(res.Result.Response.Services);
        }

        [TestMethod]
        public void Catalog_RegistrationDeregistration()
        {
            var c = ClientTest.MakeClient();
            var service = new AgentService()
            {
                ID = "redis1",
                Service = "redis",
                Tags = new[] {"master", "v1"},
                Port = 8000
            };

            var check = new AgentCheck()
            {
                Node = "foobar",
                CheckID = "service:redis1",
                Name = "Redis health check",
                Notes = "Script based health check",
                Status = CheckStatus.Passing,
                ServiceID = "redis1"
            };

            var reg = new CatalogRegistration()
            {
                Datacenter = "dc1",
                Node = "foobar",
                Address = "192.168.10.10",
                Service = service,
                Check = check
            };

            c.Catalog.Register(reg).Wait();

            var node = c.Catalog.Node("foobar");
            node.Wait();
            Assert.IsTrue(node.Result.Response.Services.ContainsKey("redis1"));

            var health = c.Health.Node("foobar");
            health.Wait();
            Assert.AreEqual("service:redis1", health.Result.Response[0].CheckID);

            var dereg = new CatalogDeregistration()
            {
                Datacenter = "dc1",
                Node = "foobar",
                Address = "192.168.10.10",
                CheckID = "service:redis1"
            };

            c.Catalog.Deregister(dereg).Wait();

            health = c.Health.Node("foobar");
            health.Wait();
            Assert.AreEqual(0, health.Result.Response.Length);

            dereg = new CatalogDeregistration()
            {
                Datacenter = "dc1",
                Node = "foobar",
                Address = "192.168.10.10"
            };

            c.Catalog.Deregister(dereg).Wait();

            node = c.Catalog.Node("foobar");
            node.Wait();
            Assert.IsNull(node.Result.Response);
        }
    }
}