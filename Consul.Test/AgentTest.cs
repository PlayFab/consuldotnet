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
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    public class AgentTest
    {
        [Fact]
        public async Task Agent_Self()
        {
            var client = new ConsulClient();

            var info = await client.Agent.Self();

            Assert.NotNull(info);
            Assert.False(string.IsNullOrEmpty(info.Response["Config"]["NodeName"]));
            Assert.False(string.IsNullOrEmpty(info.Response["Member"]["Tags"]["bootstrap"].ToString()));
        }

        [Fact]
        public async Task Agent_Members()
        {
            var client = new ConsulClient();

            var members = await client.Agent.Members(false);

            Assert.NotNull(members);
            Assert.Equal(1, members.Response.Length);
        }

        [Fact]
        public async Task Agent_Services()
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

            await client.Agent.ServiceRegister(registration);

            var services = await client.Agent.Services();
            Assert.True(services.Response.ContainsKey("foo"));

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:foo"));

            Assert.Equal(CheckStatus.Critical, checks.Response["service:foo"].Status);

            await client.Agent.ServiceDeregister("foo");
        }

        [Fact]
        public async Task Agent_Services_CheckPassing()
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

            await client.Agent.ServiceRegister(registration);

            var services = await client.Agent.Services();
            Assert.True(services.Response.ContainsKey("foo"));

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:foo"));

            Assert.Equal(CheckStatus.Passing, checks.Response["service:foo"].Status);

            await client.Agent.ServiceDeregister("foo");
        }

        [Fact]
        public async Task Agent_Services_CheckTTLNote()
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

            await client.Agent.ServiceRegister(registration);

            var services = await client.Agent.Services();
            Assert.True(services.Response.ContainsKey("foo"));

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:foo"));

            Assert.Equal(CheckStatus.Critical, checks.Response["service:foo"].Status);

            await client.Agent.PassTTL("service:foo", "test is ok");
            checks = await client.Agent.Checks();

            Assert.True(checks.Response.ContainsKey("service:foo"));
            Assert.Equal(CheckStatus.Passing, checks.Response["service:foo"].Status);
            Assert.Equal("test is ok", checks.Response["service:foo"].Output);

            await client.Agent.ServiceDeregister("foo");
        }
        [Fact]
        public void Agent_Services_CheckBadStatus()
        {
            // Not needed due to not using a string for status.
            Assert.True(true);
        }

        [Fact]
        public async Task Agent_ServiceAddress()
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

            await client.Agent.ServiceRegister(registration1);
            await client.Agent.ServiceRegister(registration2);

            var services = await client.Agent.Services();
            Assert.True(services.Response.ContainsKey("foo1"));
            Assert.True(services.Response.ContainsKey("foo2"));
            Assert.Equal("192.168.0.42", services.Response["foo1"].Address);
            Assert.True(string.IsNullOrEmpty(services.Response["foo2"].Address));

            await client.Agent.ServiceDeregister("foo1");
            await client.Agent.ServiceDeregister("foo2");
        }

        [Fact]
        public async Task Agent_EnableTagOverride()
        {
            var reg1 = new AgentServiceRegistration
            {
                Name = "foo1",
                Port = 8000,
                Address = "192.168.0.42",
                EnableTagOverride = true
            };

            var reg2 = new AgentServiceRegistration
            {
                Name = "foo2",
                Port = 8000
            };

            using (IConsulClient client = new ConsulClient())
            {
                await client.Agent.ServiceRegister(reg1);
                await client.Agent.ServiceRegister(reg2);

                var services = await client.Agent.Services();

                Assert.Contains("foo1", services.Response.Keys);
                Assert.True(services.Response["foo1"].EnableTagOverride);

                Assert.Contains("foo2", services.Response.Keys);
                Assert.False(services.Response["foo2"].EnableTagOverride);
            }
        }

        [Fact]
        public async Task Agent_Services_MultipleChecks()
        {
            var client = new ConsulClient();
            var svcID = KVTest.GenerateTestKeyName();
            var registration = new AgentServiceRegistration()
            {
                Name = svcID,
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
            await client.Agent.ServiceRegister(registration);

            var services = await client.Agent.Services();
            Assert.True(services.Response.ContainsKey(svcID));

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:" + svcID + ":1"));
            Assert.True(checks.Response.ContainsKey("service:" + svcID + ":2"));

            await client.Agent.ServiceDeregister(svcID);
        }

        [Fact]
        public async Task Agent_SetTTLStatus()
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
            await client.Agent.ServiceRegister(registration);

            await client.Agent.WarnTTL("service:foo", "warning");
            var checks = await client.Agent.Checks();
            Assert.Contains("service:foo", checks.Response.Keys);
            Assert.Equal(CheckStatus.Warning, checks.Response["service:foo"].Status);
            Assert.Equal("warning", checks.Response["service:foo"].Output);

            await client.Agent.PassTTL("service:foo", "passing");
            checks = await client.Agent.Checks();
            Assert.Contains("service:foo", checks.Response.Keys);
            Assert.Equal(CheckStatus.Passing, checks.Response["service:foo"].Status);
            Assert.Equal("passing", checks.Response["service:foo"].Output);

            await client.Agent.FailTTL("service:foo", "failing");
            checks = await client.Agent.Checks();
            Assert.Contains("service:foo", checks.Response.Keys);
            Assert.Equal(CheckStatus.Critical, checks.Response["service:foo"].Status);
            Assert.Equal("failing", checks.Response["service:foo"].Output);

            await client.Agent.UpdateTTL("service:foo", "foo", TTLStatus.Pass);
            checks = await client.Agent.Checks();
            Assert.Contains("service:foo", checks.Response.Keys);
            Assert.Equal(CheckStatus.Passing, checks.Response["service:foo"].Status);
            Assert.Equal("foo", checks.Response["service:foo"].Output);

            await client.Agent.UpdateTTL("service:foo", "foo warning", TTLStatus.Warn);
            checks = await client.Agent.Checks();
            Assert.Contains("service:foo", checks.Response.Keys);
            Assert.Equal(CheckStatus.Warning, checks.Response["service:foo"].Status);
            Assert.Equal("foo warning", checks.Response["service:foo"].Output);

            await client.Agent.UpdateTTL("service:foo", "foo failing", TTLStatus.Critical);
            checks = await client.Agent.Checks();
            Assert.Contains("service:foo", checks.Response.Keys);
            Assert.Equal(CheckStatus.Critical, checks.Response["service:foo"].Status);
            Assert.Equal("foo failing", checks.Response["service:foo"].Output);

            await client.Agent.ServiceDeregister("foo");
        }

        [Fact]
        public async Task Agent_Checks()
        {
            var client = new ConsulClient();
            var registration = new AgentCheckRegistration
            {
                Name = "foo",
                TTL = TimeSpan.FromSeconds(15)
            };
            await client.Agent.CheckRegister(registration);

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("foo"));
            Assert.Equal(CheckStatus.Critical, checks.Response["foo"].Status);

            await client.Agent.CheckDeregister("foo");
        }

        [Fact]
        public async Task Agent_Checks_Docker()
        {
            using (var client = new ConsulClient())
            {
                var serviceReg = new AgentServiceRegistration()
                {
                    Name = "redis"
                };
                await client.Agent.ServiceRegister(serviceReg);

                var reg = new AgentCheckRegistration()
                {
                    Name = "redischeck",
                    ServiceID = "redis",
                    DockerContainerID = "f972c95ebf0e",
                    Script = "/bin/true",
                    Shell = "/bin/bash",
                    Interval = TimeSpan.FromSeconds(10)
                };
                await client.Agent.CheckRegister(reg);

                var checks = await client.Agent.Checks();
                Assert.True(checks.Response.ContainsKey("redischeck"));
                Assert.Equal("redis", checks.Response["redischeck"].ServiceID);

                await client.Agent.CheckDeregister("redischeck");
                await client.Agent.ServiceDeregister("redis");
            }
        }

        [Fact]
        public async Task Agent_CheckStartPassing()
        {
            var client = new ConsulClient();
            var registration = new AgentCheckRegistration
            {
                Name = "foo",
                Status = CheckStatus.Passing,
                TTL = TimeSpan.FromSeconds(15)
            };
            await client.Agent.CheckRegister(registration);

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("foo"));
            Assert.Equal(CheckStatus.Passing, checks.Response["foo"].Status);

            await client.Agent.CheckDeregister("foo");
        }

        [Fact]
        public async Task Agent_Checks_ServiceBound()
        {
            var client = new ConsulClient();

            var serviceReg = new AgentServiceRegistration()
            {
                Name = "redis"
            };
            await client.Agent.ServiceRegister(serviceReg);

            var reg = new AgentCheckRegistration()
            {
                Name = "redischeck",
                ServiceID = "redis",
                TTL = TimeSpan.FromSeconds(15),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(90)
            };
            await client.Agent.CheckRegister(reg);

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("redischeck"));
            Assert.Equal("redis", checks.Response["redischeck"].ServiceID);

            await client.Agent.CheckDeregister("redischeck");
            await client.Agent.ServiceDeregister("redis");
        }

        [Fact]
        public async Task Agent_Join()
        {
            var client = new ConsulClient();
            var info = await client.Agent.Self();
            await client.Agent.Join(info.Response["Config"]["AdvertiseAddr"], false);
            // Success is not throwing an exception
        }

        [Fact]
        public async Task Agent_ForceLeave()
        {
            var client = new ConsulClient();
            await client.Agent.ForceLeave("foo");
            // Success is not throwing an exception
        }

        [Fact]
        public async Task Agent_ServiceMaintenance()
        {
            var client = new ConsulClient();

            var serviceReg = new AgentServiceRegistration()
            {
                Name = "redis"
            };
            await client.Agent.ServiceRegister(serviceReg);

            await client.Agent.EnableServiceMaintenance("redis", "broken");

            var checks = await client.Agent.Checks();
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

            await client.Agent.DisableServiceMaintenance("redis");

            checks = await client.Agent.Checks();
            foreach (var check in checks.Response)
            {
                Assert.False(check.Value.CheckID.Contains("maintenance"));
            }

            await client.Agent.ServiceDeregister("redis");
        }

        [Fact]
        public async Task Agent_NodeMaintenance()
        {
            var client = new ConsulClient();

            await client.Agent.EnableNodeMaintenance("broken");
            var checks = await client.Agent.Checks();

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

            await client.Agent.DisableNodeMaintenance();

            checks = await client.Agent.Checks();
            foreach (var check in checks.Response)
            {
                Assert.False(check.Value.CheckID.Contains("maintenance"));
            }
            await client.Agent.CheckDeregister("foo");
        }
    }
}
