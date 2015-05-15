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
            res.Wait();
            var id = res.Result.Response;
            Assert.IsTrue(res.Result.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Result.Response));

            var des = s.Destroy(id);
            des.Wait();
            Assert.IsTrue(des.Result.Response);
        }

        [TestMethod]
        public void Session_CreateNoChecksDestroy()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.CreateNoChecks();
            res.Wait();
            var id = res.Result.Response;
            Assert.IsTrue(res.Result.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Result.Response));

            var des = s.Destroy(id);
            des.Wait();
            Assert.IsTrue(des.Result.Response);
        }

        [TestMethod]
        public void Session_CreateRenewDestroy()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create(new SessionEntry() {TTL = TimeSpan.FromSeconds(10)});
            res.Wait();
            var id = res.Result.Response;
            Assert.IsTrue(res.Result.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Result.Response));

            var renew = s.Renew(id);
            renew.Wait();
            Assert.IsTrue(renew.Result.RequestTime.TotalMilliseconds > 0);
            Assert.IsNotNull(renew.Result.Response.ID);
            Assert.AreEqual(res.Result.Response, renew.Result.Response.ID);
            Assert.AreEqual(renew.Result.Response.TTL.TotalSeconds, TimeSpan.FromSeconds(10).TotalSeconds);

            var des = s.Destroy(id);
            des.Wait();
            Assert.IsTrue(des.Result.Response);
        }

        [TestMethod]
        public void Session_Create_RenewPeriodic_Destroy()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create(new SessionEntry() {TTL = TimeSpan.FromSeconds(10)});
            res.Wait();
            var id = res.Result.Response;
            Assert.IsTrue(res.Result.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Result.Response));

            var tokenSource = new CancellationTokenSource();
            var ct = tokenSource.Token;

            s.RenewPeriodic(TimeSpan.FromSeconds(1), id, WriteOptions.Empty, ct);

            tokenSource.CancelAfter(3000);

            Task.Delay(3000, ct).Wait(ct);

            var info = s.Info(id).GetAwaiter().GetResult();
            Assert.IsTrue(info.LastIndex > 0);
            Assert.IsNotNull(info.KnownLeader);

            Assert.AreEqual(id, info.Response.ID);

            Assert.IsTrue(s.Destroy(id).GetAwaiter().GetResult().Response);
        }

        [TestMethod]
        public void Session_Info()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create();
            res.Wait();
            var id = res.Result.Response;

            Assert.IsTrue(res.Result.RequestTime.TotalMilliseconds > 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Result.Response));

            var info = s.Info(id);
            info.Wait();
            Assert.IsTrue(info.Result.LastIndex > 0);
            Assert.IsNotNull(info.Result.KnownLeader);

            Assert.AreEqual(id, info.Result.Response.ID);

            Assert.IsTrue(string.IsNullOrEmpty(info.Result.Response.Name));
            Assert.IsFalse(string.IsNullOrEmpty(info.Result.Response.Node));
            Assert.IsTrue(info.Result.Response.CreateIndex > 0);
            Assert.AreEqual(info.Result.Response.Behavior, SessionBehavior.Release);

            Assert.IsTrue(string.IsNullOrEmpty(info.Result.Response.Name));
            Assert.IsNotNull(info.Result.KnownLeader);

            Assert.IsTrue(info.Result.LastIndex > 0);
            Assert.IsNotNull(info.Result.KnownLeader);

            var des = s.Destroy(id);
            des.Wait();
            Assert.IsTrue(des.Result.Response);
        }

        [TestMethod]
        public void Session_Node()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create();
            res.Wait();
            var id = res.Result.Response;
            try
            {
                var info = s.Info(id);
                info.Wait();

                var node = s.Node(info.Result.Response.Node);
                node.Wait();

                Assert.AreEqual(node.Result.Response.Length, 1);
                Assert.AreNotEqual(node.Result.LastIndex, 0);
                Assert.IsTrue(node.Result.KnownLeader);
            }
            finally
            {
                var des = s.Destroy(id);
                des.Wait();
                Assert.IsTrue(des.Result.Response);
            }
        }

        [TestMethod]
        public void Session_List()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var res = s.Create();
            res.Wait();
            var id = res.Result.Response;
            try
            {
                var list = s.List();
                list.Wait();

                Assert.AreEqual(list.Result.Response.Length, 1);
                Assert.AreNotEqual(list.Result.LastIndex, 0);
                Assert.IsTrue(list.Result.KnownLeader);
            }
            finally
            {
                var des = s.Destroy(id);
                des.Wait();
                Assert.IsTrue(des.Result.Response);
            }
        }
    }
}