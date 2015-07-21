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

            Assert.AreEqual(addr, config.Address);
            Assert.AreEqual(token, config.Token);
            Assert.IsNotNull(config.HttpAuth);
            Assert.AreEqual("username", config.HttpAuth.UserName);
            Assert.AreEqual("password", config.HttpAuth.Password);
            Assert.AreEqual("https", config.Scheme);
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
            var client = new Client();
            var opts = new QueryOptions()
            {
                Datacenter = "foo",
                Consistency = ConsistencyMode.Consistent,
                WaitIndex = 1000,
                WaitTime = new TimeSpan(0, 0, 100),
                Token = "12345"
            };
            var request = client.CreateQueryRequest<object>("/v1/kv/foo", opts);
            try
            {
                request.Execute();
            }
            catch (Exception)
            {
                // ignored
            }
            Assert.AreEqual("foo", request.Params["dc"]);
            Assert.IsTrue(request.Params.ContainsKey("consistent"));
            Assert.AreEqual("1000", request.Params["index"]);
            Assert.AreEqual("1m40s", request.Params["wait"]);
            Assert.AreEqual("12345", request.Params["token"]);
        }

        [TestMethod]
        public void Client_SetWriteOptions()
        {
            var client = new Client();

            var opts = new WriteOptions()
            {
                Datacenter = "foo",
                Token = "12345"
            };

            var request = client.CreateWriteRequest<object, object>("/v1/kv/foo", opts);
            try
            {
                request.Execute();
            }
            catch (Exception)
            {
                // ignored
            }

            Assert.AreEqual("foo", request.Params["dc"]);
            Assert.AreEqual("12345", request.Params["token"]);
        }
    }
}