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
            var c = ClientTest.MakeClient();

            var info = c.Agent.Self();

            Assert.IsNotNull(info);
            Assert.IsFalse(string.IsNullOrEmpty(info.Response["Config"]["NodeName"]));
            Assert.IsFalse(string.IsNullOrEmpty(info.Response["Member"]["Tags"]["bootstrap"].ToString()));
        }

        [TestMethod]
        public void Agent_Members()
        {
            var c = ClientTest.MakeClient();

            var members = c.Agent.Members(false);

            Assert.IsNotNull(members);
            Assert.AreEqual(members.Response.Length, 1);
        }

        [TestMethod]
        public void Agent_Services()
        {
            var c = ClientTest.MakeClient();
            var reg = new AgentServiceRegistration()
            {
                Name = "foo",
                Tags = new[] {"bar", "baz"},
                Port = 8000,
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15)
                }
            };
            c.Agent.ServiceRegister(reg);

            var services = c.Agent.Services();
            Assert.IsTrue(services.Response.ContainsKey("foo"));

            var checks = c.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("service:foo"));

            Assert.AreEqual(checks.Response["service:foo"].Status, CheckStatus.Critical);

            c.Agent.ServiceDeregister("foo");
        }

        [TestMethod]
        public void Agent_Services_CheckPassing()
        {
            var c = ClientTest.MakeClient();
            var reg = new AgentServiceRegistration()
            {
                Name = "foo",
                Tags = new[] {"bar", "baz"},
                Port = 8000,
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15),
                    Status = CheckStatus.Passing
                }
            };
            c.Agent.ServiceRegister(reg);

            var services = c.Agent.Services();
            Assert.IsTrue(services.Response.ContainsKey("foo"));

            var checks = c.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("service:foo"));

            Assert.AreEqual(checks.Response["service:foo"].Status, CheckStatus.Passing);

            c.Agent.ServiceDeregister("foo");
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
            var c = ClientTest.MakeClient();
            var reg1 = new AgentServiceRegistration()
            {
                Name = "foo1",
                Port = 8000,
                Address = "192.168.0.42"
            };
            var reg2 = new AgentServiceRegistration()
            {
                Name = "foo2",
                Port = 8000
            };

            c.Agent.ServiceRegister(reg1);
            c.Agent.ServiceRegister(reg2);

            var services = c.Agent.Services();
            Assert.IsTrue(services.Response.ContainsKey("foo1"));
            Assert.IsTrue(services.Response.ContainsKey("foo2"));
            Assert.AreEqual(services.Response["foo1"].Address, "192.168.0.42");
            Assert.IsTrue(string.IsNullOrEmpty(services.Response["foo2"].Address));

            c.Agent.ServiceDeregister("foo1");
            c.Agent.ServiceDeregister("foo2");
        }

        [TestMethod]
        public void Agent_Services_MultipleChecks()
        {
            var c = ClientTest.MakeClient();
            var reg = new AgentServiceRegistration()
            {
                Name = "foo",
                Tags = new[] {"bar", "baz"},
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
            c.Agent.ServiceRegister(reg);

            var services = c.Agent.Services();
            Assert.IsTrue(services.Response.ContainsKey("foo"));

            var checks = c.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("service:foo:1"));
            Assert.IsTrue(checks.Response.ContainsKey("service:foo:2"));

            c.Agent.ServiceDeregister("foo");
        }

        [TestMethod]
        public void Agent_SetTTLStatus()
        {
            var c = ClientTest.MakeClient();
            var reg = new AgentServiceRegistration()
            {
                Name = "foo",
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15)
                }
            };
            c.Agent.ServiceRegister(reg);

            c.Agent.WarnTTL("service:foo", "test");

            var checks = c.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("service:foo"));

            Assert.AreEqual(checks.Response["service:foo"].Status, CheckStatus.Warning);

            c.Agent.ServiceDeregister("foo");
        }

        [TestMethod]
        public void Agent_Checks()
        {
            var c = ClientTest.MakeClient();
            var reg = new AgentCheckRegistration
            {
                Name = "foo",
                TTL = TimeSpan.FromSeconds(15)
            };
            c.Agent.CheckRegister(reg);

            var checks = c.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("foo"));
            Assert.AreEqual(checks.Response["foo"].Status, CheckStatus.Critical);

            c.Agent.CheckDeregister("foo");
        }

        [TestMethod]
        public void Agent_CheckStartPassing()
        {
            var c = ClientTest.MakeClient();
            var reg = new AgentCheckRegistration
            {
                Name = "foo",
                Status = CheckStatus.Passing,
                TTL = TimeSpan.FromSeconds(15)
            };
            c.Agent.CheckRegister(reg);

            var checks = c.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("foo"));
            Assert.AreEqual(checks.Response["foo"].Status, CheckStatus.Passing);

            c.Agent.CheckDeregister("foo");
        }

        [TestMethod]
        public void Agent_Checks_ServiceBound()
        {
            var c = ClientTest.MakeClient();

            var serviceReg = new AgentServiceRegistration()
            {
                Name = "redis"
            };
            c.Agent.ServiceRegister(serviceReg);

            var reg = new AgentCheckRegistration()
            {
                Name = "redischeck",
                ServiceID = "redis",
                TTL = TimeSpan.FromSeconds(15)
            };
            c.Agent.CheckRegister(reg);

            var checks = c.Agent.Checks();
            Assert.IsTrue(checks.Response.ContainsKey("redischeck"));
            Assert.AreEqual(checks.Response["redischeck"].ServiceID, "redis");

            c.Agent.CheckDeregister("redischeck");
            c.Agent.ServiceDeregister("redis");
        }

        [TestMethod]
        public void Agent_Join()
        {
            var c = ClientTest.MakeClient();
            var info = c.Agent.Self();
            c.Agent.Join(info.Response["Config"]["AdvertiseAddr"], false);
        }

        [TestMethod]
        public void Agent_ForceLeave()
        {
            var c = ClientTest.MakeClient();
            c.Agent.ForceLeave("foo");
        }

        [TestMethod]
        public void Agent_ServiceMaintenance()
        {
            var c = ClientTest.MakeClient();

            var serviceReg = new AgentServiceRegistration()
            {
                Name = "redis"
            };
            c.Agent.ServiceRegister(serviceReg);

            c.Agent.EnableServiceMaintenance("redis", "broken");

            var checks = c.Agent.Checks();
            var found = false;
            foreach (var check in checks.Response)
            {
                if (check.Value.CheckID.Contains("maintenance"))
                {
                    found = true;
                    Assert.AreEqual(check.Value.Status, CheckStatus.Critical);
                    Assert.AreEqual(check.Value.Notes, "broken");
                }
            }
            Assert.IsTrue(found);

            c.Agent.DisableServiceMaintenance("redis");

            checks = c.Agent.Checks();
            foreach (var check in checks.Response)
            {
                Assert.IsFalse(check.Value.CheckID.Contains("maintenance"));
            }

            c.Agent.ServiceDeregister("redis");
        }

        [TestMethod]
        public void Agent_NodeMaintenance()
        {
            var c = ClientTest.MakeClient();

            c.Agent.EnableNodeMaintenance("broken");
            var checks = c.Agent.Checks();

            var found = false;
            foreach (var check in checks.Response)
            {
                if (check.Value.CheckID.Contains("maintenance"))
                {
                    found = true;
                    Assert.AreEqual(check.Value.Status, CheckStatus.Critical);
                    Assert.AreEqual(check.Value.Notes, "broken");
                }
            }
            Assert.IsTrue(found);

            c.Agent.DisableNodeMaintenance();

            checks = c.Agent.Checks();
            foreach (var check in checks.Response)
            {
                Assert.IsFalse(check.Value.CheckID.Contains("maintenance"));
            }
            c.Agent.CheckDeregister("foo");
        }
    }
}