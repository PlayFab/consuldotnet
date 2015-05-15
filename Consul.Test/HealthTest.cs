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
            var c = ClientTest.MakeClient();

            var info = c.Agent.Self().GetAwaiter().GetResult();
            var checks = c.Health.Node((string) info.Response["Config"]["NodeName"]).GetAwaiter().GetResult();

            Assert.AreNotEqual(0, checks.LastIndex);
            Assert.AreNotEqual(0, checks.Response.Length);
        }

        [TestMethod]
        public void Health_Checks()
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
            try
            {
                c.Agent.ServiceRegister(reg).Wait();
                var checks = c.Health.Checks("foo").GetAwaiter().GetResult();
                Assert.AreNotEqual(0, checks.LastIndex);
                Assert.AreNotEqual(0, checks.Response.Length);
            }
            finally
            {
                c.Agent.ServiceDeregister("foo").Wait();
            }
        }

        [TestMethod]
        public void Health_Service()
        {
            var c = ClientTest.MakeClient();

            var checks = c.Health.Service("consul", "", true).GetAwaiter().GetResult();
            Assert.AreNotEqual(0, checks.LastIndex);
            Assert.AreNotEqual(0, checks.Response.Length);
        }

        [TestMethod]
        public void Health_State()
        {
            var c = ClientTest.MakeClient();

            var checks = c.Health.State(CheckStatus.Any).GetAwaiter().GetResult();
            Assert.AreNotEqual(0, checks.LastIndex);
            Assert.AreNotEqual(0, checks.Response.Length);
        }
    }
}