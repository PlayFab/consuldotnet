// -----------------------------------------------------------------------
//  <copyright file="KVTest.cs" company="PlayFab Inc">
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
using System.Collections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Consul.Test
{
    [TestClass]
    public class KVTest
    {
        private static readonly Random Random = new Random((int) DateTime.Now.Ticks);

        internal static string TestKey()
        {
            var keyChars = new char[16];
            for (var i = 0; i < keyChars.Length; i++)
            {
                keyChars[i] = Convert.ToChar(Convert.ToInt32(Math.Floor(26*Random.NextDouble() + 65)));
            }
            return new string(keyChars);
        }

        [TestMethod]
        public void KV_Put_Get_Delete()
        {
            var c = ClientTest.MakeClient();
            var kv = c.KV;

            var key = TestKey();

            var value = Encoding.UTF8.GetBytes("test");

            var g = kv.Get(key);
            g.Wait();
            Assert.IsNull(g.Result.Response);

            var pair = new KVPair(key)
            {
                Flags = 42,
                Value = value
            };

            var p = kv.Put(pair);
            p.Wait();
            Assert.IsTrue(p.Result.Response);

            g = kv.Get(key);
            g.Wait();
            var res = g.Result.Response;

            Assert.IsNotNull(res);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, res.Value));
            Assert.AreEqual(pair.Flags, res.Flags);
            Assert.IsTrue(g.Result.LastIndex > 0);

            var del = kv.Delete(key);
            del.Wait();
            Assert.IsTrue(del.Result.Response);

            g = kv.Get(key);
            g.Wait();
            Assert.IsNull(g.Result.Response);
        }

        [TestMethod]
        public void KV_List_DeleteRecurse()
        {
            var c = ClientTest.MakeClient();
            var kv = c.KV;

            var prefix = TestKey();

            var putTasks = new Task[100];

            var value = Encoding.UTF8.GetBytes("test");
            for (var i = 0; i < 100; i++)
            {
                var p = new KVPair(string.Join("/", prefix, TestKey()))
                {
                    Value = value
                };
                putTasks[i] = kv.Put(p);
            }

            Task.WaitAll(putTasks);

            var pairs = kv.List(prefix);
            pairs.Wait();
            Assert.IsNotNull(pairs.Result.Response);
            Assert.AreEqual(pairs.Result.Response.Length, putTasks.Length);
            foreach (var pair in pairs.Result.Response)
            {
                Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            }
            Assert.IsFalse(pairs.Result.LastIndex == 0);

            var deleteTree = kv.DeleteTree(prefix);
            deleteTree.Wait();

            pairs = kv.List(prefix);
            pairs.Wait();
            Assert.IsNull(pairs.Result.Response);
        }

        [TestMethod]
        public void KV_DeleteCAS()
        {
            var c = ClientTest.MakeClient();
            var kv = c.KV;

            var key = TestKey();

            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value
            };

            var p = kv.CAS(pair);
            p.Wait();
            Assert.IsTrue(p.Result.Response);

            var g = kv.Get(key);
            g.Wait();
            pair = g.Result.Response;

            Assert.IsNotNull(pair);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            Assert.IsTrue(g.Result.LastIndex > 0);

            pair.ModifyIndex = 1;
            var dcas = kv.DeleteCAS(pair);
            dcas.Wait();

            Assert.IsFalse(dcas.Result.Response);

            pair.ModifyIndex = g.Result.LastIndex;
            dcas = kv.DeleteCAS(pair);
            dcas.Wait();

            Assert.IsTrue(dcas.Result.Response);
        }

        [TestMethod]
        public void KV_CAS()
        {
            var c = ClientTest.MakeClient();
            var kv = c.KV;

            var key = TestKey();

            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value
            };

            var p = kv.CAS(pair);
            p.Wait();
            Assert.IsTrue(p.Result.Response);

            var g = kv.Get(key);
            g.Wait();
            pair = g.Result.Response;

            Assert.IsNotNull(pair);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            Assert.IsTrue(g.Result.LastIndex > 0);

            value = Encoding.UTF8.GetBytes("foo");
            pair.Value = value;

            pair.ModifyIndex = 1;
            var cas = kv.CAS(pair);
            cas.Wait();

            Assert.IsFalse(cas.Result.Response);

            pair.ModifyIndex = g.Result.LastIndex;
            cas = kv.CAS(pair);
            cas.Wait();

            Assert.IsTrue(cas.Result.Response);

            var del = kv.Delete(key);
            del.Wait();
            Assert.IsTrue(del.Result.Response);
        }

        [TestMethod]
        public void KV_WatchGet()
        {
            var c = ClientTest.MakeClient();
            var kv = c.KV;

            var key = TestKey();

            var value = Encoding.UTF8.GetBytes("test");

            var g = kv.Get(key);
            g.Wait();
            Assert.IsNull(g.Result.Response);

            var pair = new KVPair(key)
            {
                Flags = 42,
                Value = value
            };

            Task.Run(() =>
            {
                Thread.Sleep(100);
                var p = new KVPair(key) {Flags = 42, Value = value};
                var putRes = kv.Put(p);
                putRes.Wait();
                Assert.IsTrue(putRes.Result.Response);
            });

            var g2 = kv.Get(key, new QueryOptions() {WaitIndex = g.Result.LastIndex});
            g2.Wait();
            var res = g2.Result.Response;

            Assert.IsNotNull(res);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, res.Value));
            Assert.AreEqual(pair.Flags, res.Flags);
            Assert.IsTrue(g2.Result.LastIndex > 0);

            var del = kv.Delete(key);
            del.Wait();
            Assert.IsTrue(del.Result.Response);

            g = kv.Get(key);
            g.Wait();
            Assert.IsNull(g.Result.Response);
        }

        [TestMethod]
        public void KV_WatchList()
        {
            var c = ClientTest.MakeClient();
            var kv = c.KV;

            var prefix = TestKey();

            var value = Encoding.UTF8.GetBytes("test");

            var pairs = kv.List(prefix);
            pairs.Wait();
            Assert.IsNull(pairs.Result.Response);

            Task.Run(() =>
            {
                Thread.Sleep(100);
                var p = new KVPair(prefix) {Flags = 42, Value = value};
                var putRes = kv.Put(p);
                putRes.Wait();
                Assert.IsTrue(putRes.Result.Response);
            });

            var pairs2 = kv.List(prefix, new QueryOptions() {WaitIndex = pairs.Result.LastIndex});
            pairs2.Wait();
            Assert.IsNotNull(pairs2.Result.Response);
            Assert.AreEqual(pairs2.Result.Response.Length, 1);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pairs2.Result.Response[0].Value));
            Assert.AreEqual(pairs2.Result.Response[0].Flags, (ulong) 42);
            Assert.IsTrue(pairs2.Result.LastIndex > pairs.Result.LastIndex);

            var deleteTree = kv.DeleteTree(prefix);
            deleteTree.Wait();
            Assert.IsTrue(deleteTree.Result.Response);
        }

        [TestMethod]
        public void KV_Keys_DeleteRecurse()
        {
            var c = ClientTest.MakeClient();
            var kv = c.KV;

            var prefix = TestKey();

            var putTasks = new Task[100];

            var value = Encoding.UTF8.GetBytes("test");
            for (var i = 0; i < 100; i++)
            {
                var p = new KVPair(string.Join("/", prefix, TestKey()))
                {
                    Value = value
                };
                putTasks[i] = kv.Put(p);
            }

            Task.WaitAll(putTasks);

            var pairs = kv.Keys(prefix, "");
            pairs.Wait();
            Assert.IsNotNull(pairs.Result.Response);
            Assert.AreEqual(pairs.Result.Response.Length, putTasks.Length);
            Assert.IsFalse(pairs.Result.LastIndex == 0);

            var deleteTree = kv.DeleteTree(prefix);
            deleteTree.Wait();

            pairs = kv.Keys(prefix, "");
            pairs.Wait();
            Assert.IsNull(pairs.Result.Response);
        }

        [TestMethod]
        public void KV_AcquireRelease()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var sesRes = s.CreateNoChecks(new SessionEntry());
            sesRes.Wait();
            var id = sesRes.Result.Response;

            Assert.IsFalse(string.IsNullOrEmpty(sesRes.Result.Response));

            var kv = c.KV;
            var key = TestKey();
            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value,
                Session = id
            };

            var g = kv.Acquire(pair);
            g.Wait();
            Assert.IsTrue(g.Result.Response);

            var res = kv.Get(key);
            res.Wait();
            Assert.IsNotNull(res.Result.Response);
            Assert.AreEqual(id, res.Result.Response.Session);
            Assert.AreEqual(res.Result.Response.LockIndex, (ulong) 1);
            Assert.IsTrue(res.Result.LastIndex > 0);

            g = kv.Release(pair);
            g.Wait();
            Assert.IsTrue(g.Result.Response);

            res = kv.Get(key);
            res.Wait();
            Assert.IsNotNull(res.Result.Response);
            Assert.AreEqual(null, res.Result.Response.Session);
            Assert.AreEqual(res.Result.Response.LockIndex, (ulong) 1);
            Assert.IsTrue(res.Result.LastIndex > 0);

            var sesDesRes = s.Destroy(id);
            sesDesRes.Wait();
            Assert.IsTrue(sesDesRes.Result.Response);

            var del = kv.Delete(key);
            del.Wait();
            Assert.IsTrue(del.Result.Response);
        }
    }
}