// -----------------------------------------------------------------------
//  <copyright file="SessionTest.cs" company="PlayFab Inc">
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Consul.Test
{
    [TestClass]
    public class SessionTest
    {
        [TestMethod]
        public void Session_CreateDestroy()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create();
            var id = sessionRequest.Response;
            Assert.IsTrue(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(sessionRequest.Response));

            var destroyRequest = client.Session.Destroy(id);
            Assert.IsTrue(destroyRequest.Response);
        }

        [TestMethod]
        public void Session_CreateNoChecksDestroy()
        {
            var client = new Client();
            var sessionRequest = client.Session.CreateNoChecks();

            var id = sessionRequest.Response;
            Assert.IsTrue(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(sessionRequest.Response));

            var destroyRequest = client.Session.Destroy(id);
            Assert.IsTrue(destroyRequest.Response);
        }

        [TestMethod]
        public void Session_CreateRenewDestroy()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) });

            var id = sessionRequest.Response;
            Assert.IsTrue(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(sessionRequest.Response));

            var renewRequest = client.Session.Renew(id);
            Assert.IsTrue(renewRequest.RequestTime.TotalMilliseconds > 0);
            Assert.IsNotNull(renewRequest.Response.ID);
            Assert.AreEqual(sessionRequest.Response, renewRequest.Response.ID);
            Assert.IsTrue(renewRequest.Response.TTL.HasValue);
            Assert.AreEqual(renewRequest.Response.TTL.Value.TotalSeconds, TimeSpan.FromSeconds(10).TotalSeconds);

            var destroyRequest = client.Session.Destroy(id);
            Assert.IsTrue(destroyRequest.Response);
        }

        [TestMethod]
        public void Session_Create_RenewPeriodic_Destroy()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) });

            var id = sessionRequest.Response;
            Assert.IsTrue(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(sessionRequest.Response));

            var tokenSource = new CancellationTokenSource();
            var ct = tokenSource.Token;

            client.Session.RenewPeriodic(TimeSpan.FromSeconds(1), id, WriteOptions.Empty, ct);

            tokenSource.CancelAfter(3000);

            Task.Delay(3000, ct).Wait(ct);

            var infoRequest = client.Session.Info(id);
            Assert.IsTrue(infoRequest.LastIndex > 0);
            Assert.IsNotNull(infoRequest.KnownLeader);

            Assert.AreEqual(id, infoRequest.Response.ID);

            Assert.IsTrue(client.Session.Destroy(id).Response);
        }

        [TestMethod]
        public void Session_Info()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create();

            var id = sessionRequest.Response;

            Assert.IsTrue(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(sessionRequest.Response));

            var infoRequest = client.Session.Info(id);
            Assert.IsTrue(infoRequest.LastIndex > 0);
            Assert.IsNotNull(infoRequest.KnownLeader);

            Assert.AreEqual(id, infoRequest.Response.ID);

            Assert.IsTrue(string.IsNullOrEmpty(infoRequest.Response.Name));
            Assert.IsFalse(string.IsNullOrEmpty(infoRequest.Response.Node));
            Assert.IsTrue(infoRequest.Response.CreateIndex > 0);
            Assert.AreEqual(infoRequest.Response.Behavior, SessionBehavior.Release);

            Assert.IsTrue(string.IsNullOrEmpty(infoRequest.Response.Name));
            Assert.IsNotNull(infoRequest.KnownLeader);

            Assert.IsTrue(infoRequest.LastIndex > 0);
            Assert.IsNotNull(infoRequest.KnownLeader);

            var destroyRequest = client.Session.Destroy(id);

            Assert.IsTrue(destroyRequest.Response);
        }

        [TestMethod]
        public void Session_Node()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create();

            var id = sessionRequest.Response;
            try
            {
                var infoRequest = client.Session.Info(id);

                var nodeRequest = client.Session.Node(infoRequest.Response.Node);

                Assert.AreEqual(nodeRequest.Response.Length, 1);
                Assert.AreNotEqual(nodeRequest.LastIndex, 0);
                Assert.IsTrue(nodeRequest.KnownLeader);
            }
            finally
            {
                var destroyRequest = client.Session.Destroy(id);

                Assert.IsTrue(destroyRequest.Response);
            }
        }

        [TestMethod]
        public void Session_List()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create();

            var id = sessionRequest.Response;

            try
            {
                var listRequest = client.Session.List();

                Assert.AreEqual(listRequest.Response.Length, 1);
                Assert.AreNotEqual(listRequest.LastIndex, 0);
                Assert.IsTrue(listRequest.KnownLeader);
            }
            finally
            {
                var destroyRequest = client.Session.Destroy(id);

                Assert.IsTrue(destroyRequest.Response);
            }
        }
    }
}