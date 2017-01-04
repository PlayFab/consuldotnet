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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    public class AgentTest : IDisposable
    {
        AsyncReaderWriterLock.Releaser m_lock;
        public AgentTest()
        {
            m_lock = AsyncHelpers.RunSync(() => SelectiveParallel.Parallel());
        }

        public void Dispose()
        {
            m_lock.Dispose();
        }

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
            var svcID = KVTest.GenerateTestKeyName();
            var registration = new AgentServiceRegistration()
            {
                Name = svcID,
                Tags = new[] { "bar", "baz" },
                Port = 8000,
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15)
                }
            };

            await client.Agent.ServiceRegister(registration);

            var services = await client.Agent.Services();
            Assert.True(services.Response.ContainsKey(svcID));

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:" + svcID));

            Assert.Equal(HealthStatus.Critical, checks.Response["service:" + svcID].Status);

            await client.Agent.ServiceDeregister(svcID);
        }

        [Fact]
        public async Task Agent_Services_CheckPassing()
        {
            var client = new ConsulClient();
            var svcID = KVTest.GenerateTestKeyName();
            var registration = new AgentServiceRegistration()
            {
                Name = svcID,
                Tags = new[] { "bar", "baz" },
                Port = 8000,
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15),
                    Status = HealthStatus.Passing
                }
            };

            await client.Agent.ServiceRegister(registration);

            var services = await client.Agent.Services();
            Assert.True(services.Response.ContainsKey(svcID));

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:" + svcID));

            Assert.Equal(HealthStatus.Passing, checks.Response["service:" + svcID].Status);

            await client.Agent.ServiceDeregister(svcID);
        }

        [Fact]
        public async Task Agent_Services_CheckTTLNote()
        {
            var client = new ConsulClient();
            var svcID = KVTest.GenerateTestKeyName();
            var registration = new AgentServiceRegistration()
            {
                Name = svcID,
                Tags = new[] { "bar", "baz" },
                Port = 8000,
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15),
                    Status = HealthStatus.Critical
                }
            };

            await client.Agent.ServiceRegister(registration);

            var services = await client.Agent.Services();
            Assert.True(services.Response.ContainsKey(svcID));

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("service:" + svcID));

            Assert.Equal(HealthStatus.Critical, checks.Response["service:" + svcID].Status);

            await client.Agent.PassTTL("service:" + svcID, "test is ok");
            checks = await client.Agent.Checks();

            Assert.True(checks.Response.ContainsKey("service:" + svcID));
            Assert.Equal(HealthStatus.Passing, checks.Response["service:" + svcID].Status);
            Assert.Equal("test is ok", checks.Response["service:" + svcID].Output);

            await client.Agent.ServiceDeregister(svcID);
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
            var svcID1 = KVTest.GenerateTestKeyName();
            var svcID2 = KVTest.GenerateTestKeyName();
            var registration1 = new AgentServiceRegistration()
            {
                Name = svcID1,
                Port = 8000,
                Address = "192.168.0.42"
            };
            var registration2 = new AgentServiceRegistration()
            {
                Name = svcID2,
                Port = 8000
            };

            await client.Agent.ServiceRegister(registration1);
            await client.Agent.ServiceRegister(registration2);

            var services = await client.Agent.Services();
            Assert.True(services.Response.ContainsKey(svcID1));
            Assert.True(services.Response.ContainsKey(svcID2));
            Assert.Equal("192.168.0.42", services.Response[svcID1].Address);
            Assert.True(string.IsNullOrEmpty(services.Response[svcID2].Address));

            await client.Agent.ServiceDeregister(svcID1);
            await client.Agent.ServiceDeregister(svcID2);
        }

        [Fact]
        public async Task Agent_EnableTagOverride()
        {
            var svcID1 = KVTest.GenerateTestKeyName();
            var svcID2 = KVTest.GenerateTestKeyName();
            var reg1 = new AgentServiceRegistration
            {
                Name = svcID1,
                Port = 8000,
                Address = "192.168.0.42",
                EnableTagOverride = true
            };

            var reg2 = new AgentServiceRegistration
            {
                Name = svcID2,
                Port = 8000
            };

            using (IConsulClient client = new ConsulClient())
            {
                await client.Agent.ServiceRegister(reg1);
                await client.Agent.ServiceRegister(reg2);

                var services = await client.Agent.Services();

                Assert.Contains(svcID1, services.Response.Keys);
                Assert.True(services.Response[svcID1].EnableTagOverride);

                Assert.Contains(svcID2, services.Response.Keys);
                Assert.False(services.Response[svcID2].EnableTagOverride);
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
            var svcID = KVTest.GenerateTestKeyName();
            var registration = new AgentServiceRegistration()
            {
                Name = svcID,
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15)
                }
            };
            await client.Agent.ServiceRegister(registration);

            await client.Agent.WarnTTL("service:" + svcID, "warning");
            var checks = await client.Agent.Checks();
            Assert.Contains("service:" + svcID, checks.Response.Keys);
            Assert.Equal(HealthStatus.Warning, checks.Response["service:" + svcID].Status);
            Assert.Equal("warning", checks.Response["service:" + svcID].Output);

            await client.Agent.PassTTL("service:" + svcID, "passing");
            checks = await client.Agent.Checks();
            Assert.Contains("service:" + svcID, checks.Response.Keys);
            Assert.Equal(HealthStatus.Passing, checks.Response["service:" + svcID].Status);
            Assert.Equal("passing", checks.Response["service:" + svcID].Output);

            await client.Agent.FailTTL("service:" + svcID, "failing");
            checks = await client.Agent.Checks();
            Assert.Contains("service:" + svcID, checks.Response.Keys);
            Assert.Equal(HealthStatus.Critical, checks.Response["service:" + svcID].Status);
            Assert.Equal("failing", checks.Response["service:" + svcID].Output);

            await client.Agent.UpdateTTL("service:" + svcID, svcID, TTLStatus.Pass);
            checks = await client.Agent.Checks();
            Assert.Contains("service:" + svcID, checks.Response.Keys);
            Assert.Equal(HealthStatus.Passing, checks.Response["service:" + svcID].Status);
            Assert.Equal(svcID, checks.Response["service:" + svcID].Output);

            await client.Agent.UpdateTTL("service:" + svcID, "foo warning", TTLStatus.Warn);
            checks = await client.Agent.Checks();
            Assert.Contains("service:" + svcID, checks.Response.Keys);
            Assert.Equal(HealthStatus.Warning, checks.Response["service:" + svcID].Status);
            Assert.Equal("foo warning", checks.Response["service:" + svcID].Output);

            await client.Agent.UpdateTTL("service:" + svcID, "foo failing", TTLStatus.Critical);
            checks = await client.Agent.Checks();
            Assert.Contains("service:" + svcID, checks.Response.Keys);
            Assert.Equal(HealthStatus.Critical, checks.Response["service:" + svcID].Status);
            Assert.Equal("foo failing", checks.Response["service:" + svcID].Output);

            await client.Agent.ServiceDeregister(svcID);
        }

        [Fact]
        public async Task Agent_Checks()
        {
            var client = new ConsulClient();
            var svcID = KVTest.GenerateTestKeyName();
            var registration = new AgentCheckRegistration
            {
                Name = svcID,
                TTL = TimeSpan.FromSeconds(15)
            };
            await client.Agent.CheckRegister(registration);

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey(svcID));
            Assert.Equal(HealthStatus.Critical, checks.Response[svcID].Status);

            await client.Agent.CheckDeregister(svcID);
        }

        [Fact]
        public async Task Agent_Checks_Docker()
        {
            using (var client = new ConsulClient())
            {
                var svcID = KVTest.GenerateTestKeyName();
                var serviceReg = new AgentServiceRegistration()
                {
                    Name = svcID
                };
                await client.Agent.ServiceRegister(serviceReg);

                var reg = new AgentCheckRegistration()
                {
                    Name = "redischeck",
                    ServiceID = svcID,
                    DockerContainerID = "f972c95ebf0e",
                    Script = "/bin/true",
                    Shell = "/bin/bash",
                    Interval = TimeSpan.FromSeconds(10)
                };
                await client.Agent.CheckRegister(reg);

                var checks = await client.Agent.Checks();
                Assert.True(checks.Response.ContainsKey("redischeck"));
                Assert.Equal(svcID, checks.Response["redischeck"].ServiceID);

                await client.Agent.CheckDeregister("redischeck");
                await client.Agent.ServiceDeregister(svcID);
            }
        }

        [Fact]
        public async Task Agent_CheckStartPassing()
        {
            var client = new ConsulClient();
            var svcID = KVTest.GenerateTestKeyName();
            var registration = new AgentCheckRegistration()
            {
                Name = svcID,
                Status = HealthStatus.Passing,
                TTL = TimeSpan.FromSeconds(15)
            };
            await client.Agent.CheckRegister(registration);

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey(svcID));
            Assert.Equal(HealthStatus.Passing, checks.Response[svcID].Status);

            await client.Agent.CheckDeregister(svcID);
        }

        [Fact]
        public async Task Agent_Checks_ServiceBound()
        {
            var client = new ConsulClient();
            var svcID = KVTest.GenerateTestKeyName();
            var serviceReg = new AgentServiceRegistration()
            {
                Name = svcID
            };
            await client.Agent.ServiceRegister(serviceReg);

            var reg = new AgentCheckRegistration()
            {
                Name = "redischeck",
                ServiceID = svcID,
                TTL = TimeSpan.FromSeconds(15),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(90)
            };
            await client.Agent.CheckRegister(reg);

            var checks = await client.Agent.Checks();
            Assert.True(checks.Response.ContainsKey("redischeck"));
            Assert.Equal(svcID, checks.Response["redischeck"].ServiceID);

            await client.Agent.CheckDeregister("redischeck");
            await client.Agent.ServiceDeregister(svcID);
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
            await client.Agent.ForceLeave("nonexistant");
            // Success is not throwing an exception
        }

        [Fact]
        public async Task Agent_ServiceMaintenance()
        {
            var client = new ConsulClient();
            var svcID = KVTest.GenerateTestKeyName();
            var serviceReg = new AgentServiceRegistration()
            {
                Name = svcID
            };
            await client.Agent.ServiceRegister(serviceReg);

            await client.Agent.EnableServiceMaintenance(svcID, "broken");

            var checks = await client.Agent.Checks();
            var found = false;
            foreach (var check in checks.Response)
            {
                if (check.Value.CheckID.Contains("maintenance"))
                {
                    found = true;
                    Assert.Equal(HealthStatus.Critical, check.Value.Status);
                    Assert.Equal("broken", check.Value.Notes);
                }
            }
            Assert.True(found);

            await client.Agent.DisableServiceMaintenance(svcID);

            checks = await client.Agent.Checks();
            foreach (var check in checks.Response)
            {
                Assert.False(check.Value.CheckID.Contains("maintenance"));
            }

            await client.Agent.ServiceDeregister(svcID);
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
                    Assert.Equal(HealthStatus.Critical, check.Value.Status);
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
        }

        [Fact]
        public async Task Agent_Monitor()
        {
            using (var client = new ConsulClient())
            {
                var logs = await client.Agent.Monitor(LogLevel.Trace);
                var counter = 0;
                var successToken = new CancellationTokenSource();
                var failTask = Task.Delay(5000, successToken.Token).ContinueWith(x => Assert.True(false, "Failed to finish reading logs in time"));
                foreach (var line in logs)
                {
                    Assert.False(string.IsNullOrEmpty(await line));
                    counter++;
                    if (counter > 5) { break; }
                }
                successToken.Cancel();
                logs.Dispose();
            }
        }
    }
}
