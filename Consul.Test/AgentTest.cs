// -----------------------------------------------------------------------
//  <copyright file="AgentTest.cs" company="PlayFab Inc">
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Consul.Test
{
    [TestClass]
    public class AgentTest
    {
        [TestMethod]
        public void Agent_Self()
        {
            var client = new Client();

            var info = client.Agent.Self();

            Assert.IsNotNull(info);
            Assert.IsFalse(string.IsNullOrEmpty(info.Response["Config"]["NodeName"]));
            Assert.IsFalse(string.IsNullOrEmpty(info.Response["Member"]["Tags"]["bootstrap"].ToString()));
        }

        [TestMethod]
        public void Agent_Members()
        {
            var client = new Client();

            var members = client.Agent.Members(false);

            Assert.IsNotNull(members);
            Assert.AreEqual(1, members.Response.Length);
        }

        [TestMethod]
        public void Agent_Services()
        {
            var client = new Client();
            var registration = new AgentServiceRegistration()
            {
                Name = "foo",
                Tags = new[] { "bar", "baz" },
                Port = 8000,
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15)
                }
            };

            client.Agent.ServiceRegister(registration);

            var services = client.Agent.Services();
            Assert.IsTrue(services.Response.ContainsKey("foo"));

            var checks = client.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("service:foo"));

            Assert.AreEqual(checks.Response["service:foo"].Status, CheckStatus.Critical);

            client.Agent.ServiceDeregister("foo");
        }

        [TestMethod]
        public void Agent_Services_CheckPassing()
        {
            var client = new Client();
            var registration = new AgentServiceRegistration()
            {
                Name = "foo",
                Tags = new[] { "bar", "baz" },
                Port = 8000,
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15),
                    Status = CheckStatus.Passing
                }
            };
            client.Agent.ServiceRegister(registration);

            var services = client.Agent.Services();
            Assert.IsTrue(services.Response.ContainsKey("foo"));

            var checks = client.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("service:foo"));

            Assert.AreEqual(checks.Response["service:foo"].Status, CheckStatus.Passing);

            client.Agent.ServiceDeregister("foo");
        }

        [TestMethod]
        public void Agent_Services_CheckBadStatus()
        {
            // Not needed due to not using a string for status.
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void Agent_ServiceAddress()
        {
            var client = new Client();
            var registration1 = new AgentServiceRegistration()
            {
                Name = "foo1",
                Port = 8000,
                Address = "192.168.0.42"
            };
            var registration2 = new AgentServiceRegistration()
            {
                Name = "foo2",
                Port = 8000
            };

            client.Agent.ServiceRegister(registration1);
            client.Agent.ServiceRegister(registration2);

            var services = client.Agent.Services();
            Assert.IsTrue(services.Response.ContainsKey("foo1"));
            Assert.IsTrue(services.Response.ContainsKey("foo2"));
            Assert.AreEqual("192.168.0.42", services.Response["foo1"].Address);
            Assert.IsTrue(string.IsNullOrEmpty(services.Response["foo2"].Address));

            client.Agent.ServiceDeregister("foo1");
            client.Agent.ServiceDeregister("foo2");
        }

        [TestMethod]
        public void Agent_Services_MultipleChecks()
        {
            var client = new Client();
            var registration = new AgentServiceRegistration()
            {
                Name = "foo",
                Tags = new[] { "bar", "baz" },
                Port = 8000,
                Checks = new[]
                {
                    new AgentServiceCheck
                    {
                        TTL = TimeSpan.FromSeconds(15)
                    },
                    new AgentServiceCheck
                    {
                        TTL = TimeSpan.FromSeconds(15)
                    }
                }
            };
            client.Agent.ServiceRegister(registration);

            var services = client.Agent.Services();
            Assert.IsTrue(services.Response.ContainsKey("foo"));

            var checks = client.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("service:foo:1"));
            Assert.IsTrue(checks.Response.ContainsKey("service:foo:2"));

            client.Agent.ServiceDeregister("foo");
        }

        [TestMethod]
        public void Agent_SetTTLStatus()
        {
            var client = new Client();
            var registration = new AgentServiceRegistration()
            {
                Name = "foo",
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15)
                }
            };
            client.Agent.ServiceRegister(registration);

            client.Agent.WarnTTL("service:foo", "test");

            var checks = client.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("service:foo"));

            Assert.AreEqual(CheckStatus.Warning, checks.Response["service:foo"].Status);

            client.Agent.ServiceDeregister("foo");
        }

        [TestMethod]
        public void Agent_Checks()
        {
            var client = new Client();
            var registration = new AgentCheckRegistration
            {
                Name = "foo",
                TTL = TimeSpan.FromSeconds(15)
            };
            client.Agent.CheckRegister(registration);

            var checks = client.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("foo"));
            Assert.AreEqual(CheckStatus.Critical, checks.Response["foo"].Status);

            client.Agent.CheckDeregister("foo");
        }

        [TestMethod]
        public void Agent_CheckStartPassing()
        {
            var client = new Client();
            var registration = new AgentCheckRegistration
            {
                Name = "foo",
                Status = CheckStatus.Passing,
                TTL = TimeSpan.FromSeconds(15)
            };
            client.Agent.CheckRegister(registration);

            var checks = client.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("foo"));
            Assert.AreEqual(CheckStatus.Passing, checks.Response["foo"].Status);

            client.Agent.CheckDeregister("foo");
        }

        [TestMethod]
        public void Agent_Checks_ServiceBound()
        {
            var client = new Client();

            var serviceReg = new AgentServiceRegistration()
            {
                Name = "redis"
            };
            client.Agent.ServiceRegister(serviceReg);

            var reg = new AgentCheckRegistration()
            {
                Name = "redischeck",
                ServiceID = "redis",
                TTL = TimeSpan.FromSeconds(15)
            };
            client.Agent.CheckRegister(reg);

            var checks = client.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("redischeck"));
            Assert.AreEqual("redis", checks.Response["redischeck"].ServiceID);

            client.Agent.CheckDeregister("redischeck");
            client.Agent.ServiceDeregister("redis");
        }

        [TestMethod]
        public void Agent_Join()
        {
            var client = new Client();
            var info = client.Agent.Self();
            client.Agent.Join(info.Response["Config"]["AdvertiseAddr"], false);
            // Success is not throwing an exception
        }

        [TestMethod]
        public void Agent_ForceLeave()
        {
            var client = new Client();
            client.Agent.ForceLeave("foo");
            // Success is not throwing an exception
        }

        [TestMethod]
        public void Agent_ServiceMaintenance()
        {
            var client = new Client();

            var serviceReg = new AgentServiceRegistration()
            {
                Name = "redis"
            };
            client.Agent.ServiceRegister(serviceReg);

            client.Agent.EnableServiceMaintenance("redis", "broken");

            var checks = client.Agent.Checks();
            var found = false;
            foreach (var check in checks.Response)
            {
                if (check.Value.CheckID.Contains("maintenance"))
                {
                    found = true;
                    Assert.AreEqual(CheckStatus.Critical, check.Value.Status);
                    Assert.AreEqual("broken", check.Value.Notes);
                }
            }
            Assert.IsTrue(found);

            client.Agent.DisableServiceMaintenance("redis");

            checks = client.Agent.Checks();
            foreach (var check in checks.Response)
            {
                Assert.IsFalse(check.Value.CheckID.Contains("maintenance"));
            }

            client.Agent.ServiceDeregister("redis");
        }

        [TestMethod]
        public void Agent_NodeMaintenance()
        {
            var client = new Client();

            client.Agent.EnableNodeMaintenance("broken");
            var checks = client.Agent.Checks();

            var found = false;
            foreach (var check in checks.Response)
            {
                if (check.Value.CheckID.Contains("maintenance"))
                {
                    found = true;
                    Assert.AreEqual(CheckStatus.Critical, check.Value.Status);
                    Assert.AreEqual("broken", check.Value.Notes);
                }
            }
            Assert.IsTrue(found);

            client.Agent.DisableNodeMaintenance();

            checks = client.Agent.Checks();
            foreach (var check in checks.Response)
            {
                Assert.IsFalse(check.Value.CheckID.Contains("maintenance"));
            }
            client.Agent.CheckDeregister("foo");
        }
    }
}