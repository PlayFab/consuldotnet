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
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create();
            var id = res.Response;
            Assert.IsTrue(res.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Response));

            var des = s.Destroy(id);
            Assert.IsTrue(des.Response);
        }

        [TestMethod]
        public void Session_CreateNoChecksDestroy()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.CreateNoChecks();

            var id = res.Response;
            Assert.IsTrue(res.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Response));

            var des = s.Destroy(id);
            Assert.IsTrue(des.Response);
        }

        [TestMethod]
        public void Session_CreateRenewDestroy()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create(new SessionEntry() {TTL = TimeSpan.FromSeconds(10)});

            var id = res.Response;
            Assert.IsTrue(res.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Response));

            var renew = s.Renew(id);
            Assert.IsTrue(renew.RequestTime.TotalMilliseconds > 0);
            Assert.IsNotNull(renew.Response.ID);
            Assert.AreEqual(res.Response, renew.Response.ID);
            Assert.AreEqual(renew.Response.TTL.TotalSeconds, TimeSpan.FromSeconds(10).TotalSeconds);

            var des = s.Destroy(id);
            Assert.IsTrue(des.Response);
        }

        [TestMethod]
        public void Session_Create_RenewPeriodic_Destroy()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create(new SessionEntry() {TTL = TimeSpan.FromSeconds(10)});

            var id = res.Response;
            Assert.IsTrue(res.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Response));

            var tokenSource = new CancellationTokenSource();
            var ct = tokenSource.Token;

            s.RenewPeriodic(TimeSpan.FromSeconds(1), id, WriteOptions.Empty, ct);

            tokenSource.CancelAfter(3000);

            Task.Delay(3000, ct).Wait(ct);

            var info = s.Info(id);
            Assert.IsTrue(info.LastIndex > 0);
            Assert.IsNotNull(info.KnownLeader);

            Assert.AreEqual(id, info.Response.ID);

            Assert.IsTrue(s.Destroy(id).Response);
        }

        [TestMethod]
        public void Session_Info()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create();

            var id = res.Response;

            Assert.IsTrue(res.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Response));

            var info = s.Info(id);
            Assert.IsTrue(info.LastIndex > 0);
            Assert.IsNotNull(info.KnownLeader);

            Assert.AreEqual(id, info.Response.ID);

            Assert.IsTrue(string.IsNullOrEmpty(info.Response.Name));
            Assert.IsFalse(string.IsNullOrEmpty(info.Response.Node));
            Assert.IsTrue(info.Response.CreateIndex > 0);
            Assert.AreEqual(info.Response.Behavior, SessionBehavior.Release);

            Assert.IsTrue(string.IsNullOrEmpty(info.Response.Name));
            Assert.IsNotNull(info.KnownLeader);

            Assert.IsTrue(info.LastIndex > 0);
            Assert.IsNotNull(info.KnownLeader);

            var des = s.Destroy(id);

            Assert.IsTrue(des.Response);
        }

        [TestMethod]
        public void Session_Node()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create();

            var id = res.Response;
            try
            {
                var info = s.Info(id);

                var node = s.Node(info.Response.Node);

                Assert.AreEqual(node.Response.Length, 1);
                Assert.AreNotEqual(node.LastIndex, 0);
                Assert.IsTrue(node.KnownLeader);
            }
            finally
            {
                var des = s.Destroy(id);

                Assert.IsTrue(des.Response);
            }
        }

        [TestMethod]
        public void Session_List()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create();

            var id = res.Response;
            try
            {
                var list = s.List();

                Assert.AreEqual(list.Response.Length, 1);
                Assert.AreNotEqual(list.LastIndex, 0);
                Assert.IsTrue(list.KnownLeader);
            }
            finally
            {
                var des = s.Destroy(id);

                Assert.IsTrue(des.Response);
            }
        }
    }
}