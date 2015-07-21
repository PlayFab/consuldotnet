// -----------------------------------------------------------------------
//  <copyright file="HealthTest.cs" company="PlayFab Inc">
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
    public class HealthTest
    {
        [TestMethod]
        public void Health_Node()
        {
            var client = new Client();

            var info = client.Agent.Self();
            var checks = client.Health.Node((string) info.Response["Config"]["NodeName"]);

            Assert.AreNotEqual(0, checks.LastIndex);
            Assert.AreNotEqual(0, checks.Response.Length);
        }

        [TestMethod]
        public void Health_Checks()
        {
            var client = new Client();

            var registration = new AgentServiceRegistration()
            {
                Name = "foo",
                Tags = new[] {"bar", "baz"},
                Port = 8000,
                Check = new AgentServiceCheck
                {
                    TTL = TimeSpan.FromSeconds(15)
                }
            };
            try
            {
                client.Agent.ServiceRegister(registration);
                var checks = client.Health.Checks("foo");
                Assert.AreNotEqual(0, checks.LastIndex);
                Assert.AreNotEqual(0, checks.Response.Length);
            }
            finally
            {
                client.Agent.ServiceDeregister("foo");
            }
        }

        [TestMethod]
        public void Health_Service()
        {
            var client = new Client();

            var checks = client.Health.Service("consul", "", true);
            Assert.AreNotEqual(0, checks.LastIndex);
            Assert.AreNotEqual(0, checks.Response.Length);
        }

        [TestMethod]
        public void Health_State()
        {
            var client = new Client();

            var checks = client.Health.State(CheckStatus.Any);
            Assert.AreNotEqual(0, checks.LastIndex);
            Assert.AreNotEqual(0, checks.Response.Length);
        }
    }
}