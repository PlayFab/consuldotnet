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
using Xunit;

namespace Consul.Test
{
    public class AgentTest
    {
        [Fact]
        public void Agent_Self()
        {
            var client = new ConsulClient();

            var info = client.Agent.Self();

            Assert.NotNull(info);
            Assert.False(string.IsNullOrEmpty(info.Response["Config"]["NodeName"]));
            Assert.False(string.IsNullOrEmpty(info.Response["Member"]["Tags"]["bootstrap"].ToString()));
        }

        [Fact]
        public void Agent_Members()
        {
            var client = new ConsulClient();

            var members = client.Agent.Members(false);

            Assert.NotNull(members);
            Assert.Equal(1, members.Response.Length);
        }

        [Fact]
        public void Agent_Services()
        {
            var client = new ConsulClient();
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
            Assert.True(services.Response.ContainsKey("foo"));

            var checks = client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:foo"));

            Assert.Equal(CheckStatus.Critical, checks.Response["service:foo"].Status);

            client.Agent.ServiceDeregister("foo");
        }

        [Fact]
        public void Agent_Services_CheckPassing()
        {
            var client = new ConsulClient();
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
            Assert.True(services.Response.ContainsKey("foo"));

            var checks = client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:foo"));

            Assert.Equal(CheckStatus.Passing, checks.Response["service:foo"].Status);

            client.Agent.ServiceDeregister("foo");
        }

        [Fact]
        public void Agent_Services_CheckTTLNote()
        {
            var client = new ConsulClient();
            var registration = new AgentServiceRegistration()
            {
                Name = "foo",
                Tags = new[] { "bar", "baz" },
                Port = 8000,
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15),
                    Status = CheckStatus.Critical
                }
            };
            client.Agent.ServiceRegister(registration);

            var services = client.Agent.Services();
            Assert.True(services.Response.ContainsKey("foo"));

            var checks = client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:foo"));

            Assert.Equal(CheckStatus.Critical, checks.Response["service:foo"].Status);

            client.Agent.PassTTL("service:foo", "ok");
            checks = client.Agent.Checks();

            Assert.True(checks.Response.ContainsKey("service:foo"));
            Assert.Equal(CheckStatus.Passing, checks.Response["service:foo"].Status);
            Assert.Equal("ok", checks.Response["service:foo"].Output);

            client.Agent.ServiceDeregister("foo");
        }
        [Fact]
        public void Agent_Services_CheckBadStatus()
        {
            // Not needed due to not using a string for status.
            Assert.True(true);
        }

        [Fact]
        public void Agent_ServiceAddress()
        {
            var client = new ConsulClient();
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
            Assert.True(services.Response.ContainsKey("foo1"));
            Assert.True(services.Response.ContainsKey("foo2"));
            Assert.Equal("192.168.0.42", services.Response["foo1"].Address);
            Assert.True(string.IsNullOrEmpty(services.Response["foo2"].Address));

            client.Agent.ServiceDeregister("foo1");
            client.Agent.ServiceDeregister("foo2");
        }

        [Fact]
        public void Agent_Services_MultipleChecks()
        {
            var client = new ConsulClient();
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
            Assert.True(services.Response.ContainsKey("foo"));

            var checks = client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:foo:1"));
            Assert.True(checks.Response.ContainsKey("service:foo:2"));

            client.Agent.ServiceDeregister("foo");
        }

        [Fact]
        public void Agent_SetTTLStatus()
        {
            var client = new ConsulClient();
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
            Assert.True(checks.Response.ContainsKey("service:foo"));

            Assert.Equal(CheckStatus.Warning, checks.Response["service:foo"].Status);

            client.Agent.ServiceDeregister("foo");
        }

        [Fact]
        public void Agent_Checks()
        {
            var client = new ConsulClient();
            var registration = new AgentCheckRegistration
            {
                Name = "foo",
                TTL = TimeSpan.FromSeconds(15)
            };
            client.Agent.CheckRegister(registration);

            var checks = client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("foo"));
            Assert.Equal(CheckStatus.Critical, checks.Response["foo"].Status);

            client.Agent.CheckDeregister("foo");
        }

        [Fact]
        public void Agent_CheckStartPassing()
        {
            var client = new ConsulClient();
            var registration = new AgentCheckRegistration
            {
                Name = "foo",
                Status = CheckStatus.Passing,
                TTL = TimeSpan.FromSeconds(15)
            };
            client.Agent.CheckRegister(registration);

            var checks = client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("foo"));
            Assert.Equal(CheckStatus.Passing, checks.Response["foo"].Status);

            client.Agent.CheckDeregister("foo");
        }

        [Fact]
        public void Agent_Checks_ServiceBound()
        {
            var client = new ConsulClient();

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
            Assert.True(checks.Response.ContainsKey("redischeck"));
            Assert.Equal("redis", checks.Response["redischeck"].ServiceID);

            client.Agent.CheckDeregister("redischeck");
            client.Agent.ServiceDeregister("redis");
        }

        [Fact]
        public void Agent_Join()
        {
            var client = new ConsulClient();
            var info = client.Agent.Self();
            client.Agent.Join(info.Response["Config"]["AdvertiseAddr"], false);
            // Success is not throwing an exception
        }

        [Fact]
        public void Agent_ForceLeave()
        {
            var client = new ConsulClient();
            client.Agent.ForceLeave("foo");
            // Success is not throwing an exception
        }

        [Fact]
        public void Agent_ServiceMaintenance()
        {
            var client = new ConsulClient();

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
                    Assert.Equal(CheckStatus.Critical, check.Value.Status);
                    Assert.Equal("broken", check.Value.Notes);
                }
            }
            Assert.True(found);

            client.Agent.DisableServiceMaintenance("redis");

            checks = client.Agent.Checks();
            foreach (var check in checks.Response)
            {
                Assert.False(check.Value.CheckID.Contains("maintenance"));
            }

            client.Agent.ServiceDeregister("redis");
        }

        [Fact]
        public void Agent_NodeMaintenance()
        {
            var client = new ConsulClient();

            client.Agent.EnableNodeMaintenance("broken");
            var checks = client.Agent.Checks();

            var found = false;
            foreach (var check in checks.Response)
            {
                if (check.Value.CheckID.Contains("maintenance"))
                {
                    found = true;
                    Assert.Equal(CheckStatus.Critical, check.Value.Status);
                    Assert.Equal("broken", check.Value.Notes);
                }
            }
            Assert.True(found);

            client.Agent.DisableNodeMaintenance();

            checks = client.Agent.Checks();
            foreach (var check in checks.Response)
            {
                Assert.False(check.Value.CheckID.Contains("maintenance"));
            }
            client.Agent.CheckDeregister("foo");
        }
    }
}