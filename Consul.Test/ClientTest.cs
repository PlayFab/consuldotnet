// -----------------------------------------------------------------------
//  <copyright file="ClientTest.cs" company="PlayFab Inc">
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
using System.Net;
using System.Net.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Consul.Test
{
    [TestClass]
    public class ClientTest
    {
        public static Client MakeClient()
        {
            return new Client();
        }

        [TestMethod]
        public void Client_DefaultConfig_env()
        {
            const string addr = "1.2.3.4:5678";
            const string token = "abcd1234";
            const string auth = "username:password";
            Environment.SetEnvironmentVariable("CONSUL_HTTP_ADDR", addr);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_TOKEN", token);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_AUTH", auth);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_SSL", "1");
            Environment.SetEnvironmentVariable("CONSUL_HTTP_SSL_VERIFY", "0");

            var config = new Config();

            Assert.AreEqual(config.Address, addr);
            Assert.AreEqual(config.Token, token);
            Assert.IsNotNull(config.HttpAuth);
            Assert.AreEqual(config.HttpAuth.UserName, "username");
            Assert.AreEqual(config.HttpAuth.Password, "password");
            Assert.AreEqual(config.Scheme, "https");
            Assert.IsTrue(ServicePointManager.ServerCertificateValidationCallback(null, null, null,
                SslPolicyErrors.RemoteCertificateChainErrors));

            Environment.SetEnvironmentVariable("CONSUL_HTTP_ADDR", string.Empty);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_TOKEN", string.Empty);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_AUTH", string.Empty);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_SSL", string.Empty);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_SSL_VERIFY", string.Empty);
            ServicePointManager.ServerCertificateValidationCallback = null;

            var client = new Client(config);

            Assert.IsNotNull(client);
        }

        [TestMethod]
        public void Client_SetQueryOptions()
        {
            var c = MakeClient();
            var q = new QueryOptions()
            {
                Datacenter = "foo",
                Consistency = ConsistencyMode.Consistent,
                WaitIndex = 1000,
                WaitTime = new TimeSpan(0, 0, 100),
                Token = "12345"
            };
            var r = c.CreateQueryRequest<object>("/v1/kv/foo", q);
            try
            {
                r.Execute().GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // ignored
            }
            Assert.AreEqual(r.Params["dc"], "foo");
            Assert.IsTrue(r.Params.ContainsKey("consistent"));
            Assert.AreEqual(r.Params["index"], "1000");
            Assert.AreEqual(r.Params["wait"], "1m40s");
            Assert.AreEqual(r.Params["token"], "12345");
        }

        [TestMethod]
        public void Client_SetWriteOptions()
        {
            var c = MakeClient();

            var q = new WriteOptions()
            {
                Datacenter = "foo",
                Token = "12345"
            };

            var r = c.CreateWriteRequest<object, object>("/v1/kv/foo", q);
            try
            {
                r.Execute().GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // ignored
            }

            Assert.AreEqual(r.Params["dc"], "foo");
            Assert.AreEqual(r.Params["token"], "12345");
        }
    }
}