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
        private static readonly Random Random = new Random((int)DateTime.Now.Ticks);

        internal static string TestKey()
        {
            var keyChars = new char[16];
            for (var i = 0; i < keyChars.Length; i++)
            {
                keyChars[i] = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * Random.NextDouble() + 65)));
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
            Assert.IsNull(g.Response);

            var pair = new KVPair(key)
            {
                Flags = 42,
                Value = value
            };

            var p = kv.Put(pair);
            Assert.IsTrue(p.Response);

            g = kv.Get(key);
            var res = g.Response;

            Assert.IsNotNull(res);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, res.Value));
            Assert.AreEqual(pair.Flags, res.Flags);
            Assert.IsTrue(g.LastIndex > 0);

            var del = kv.Delete(key);
            Assert.IsTrue(del.Response);

            g = kv.Get(key);
            Assert.IsNull(g.Response);
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
                putTasks[i] = Task.Run(() => kv.Put(p));
            }

            Task.WaitAll(putTasks);

            var pairs = kv.List(prefix);
            Assert.IsNotNull(pairs.Response);
            Assert.AreEqual(pairs.Response.Length, putTasks.Length);
            foreach (var pair in pairs.Response)
            {
                Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            }
            Assert.IsFalse(pairs.LastIndex == 0);

            kv.DeleteTree(prefix);

            pairs = kv.List(prefix);
            Assert.IsNull(pairs.Response);
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
            Assert.IsTrue(p.Response);

            var g = kv.Get(key);
            pair = g.Response;

            Assert.IsNotNull(pair);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            Assert.IsTrue(g.LastIndex > 0);

            pair.ModifyIndex = 1;
            var dcas = kv.DeleteCAS(pair);

            Assert.IsFalse(dcas.Response);

            pair.ModifyIndex = g.LastIndex;
            dcas = kv.DeleteCAS(pair);

            Assert.IsTrue(dcas.Response);
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
            Assert.IsTrue(p.Response);

            var g = kv.Get(key);
            pair = g.Response;

            Assert.IsNotNull(pair);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            Assert.IsTrue(g.LastIndex > 0);

            value = Encoding.UTF8.GetBytes("foo");
            pair.Value = value;

            pair.ModifyIndex = 1;
            var cas = kv.CAS(pair);

            Assert.IsFalse(cas.Response);

            pair.ModifyIndex = g.LastIndex;
            cas = kv.CAS(pair);
            Assert.IsTrue(cas.Response);

            var del = kv.Delete(key);
            Assert.IsTrue(del.Response);
        }

        [TestMethod]
        public void KV_WatchGet()
        {
            var c = ClientTest.MakeClient();
            var kv = c.KV;

            var key = TestKey();

            var value = Encoding.UTF8.GetBytes("test");

            var g = kv.Get(key);
            Assert.IsNull(g.Response);

            var pair = new KVPair(key)
            {
                Flags = 42,
                Value = value
            };

            Task.Run(() =>
            {
                Task.Delay(1100).Wait();
                var p = new KVPair(key) { Flags = 42, Value = value };
                var putRes = kv.Put(p);
                Assert.IsTrue(putRes.Response);
            });

            var g2 = kv.Get(key, new QueryOptions() { WaitIndex = g.LastIndex });
            var res = g2.Response;

            Assert.IsNotNull(res);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, res.Value));
            Assert.AreEqual(pair.Flags, res.Flags);
            Assert.IsTrue(g2.LastIndex > 0);

            var del = kv.Delete(key);
            Assert.IsTrue(del.Response);

            g = kv.Get(key);
            Assert.IsNull(g.Response);
        }

        [TestMethod]
        public void KV_WatchList()
        {
            var c = ClientTest.MakeClient();
            var kv = c.KV;

            var prefix = TestKey();

            var value = Encoding.UTF8.GetBytes("test");

            var pairs = kv.List(prefix);
            Assert.IsNull(pairs.Response);

            Task.Run(() =>
            {
                Thread.Sleep(100);
                var p = new KVPair(prefix) { Flags = 42, Value = value };
                var putRes = kv.Put(p);
                Assert.IsTrue(putRes.Response);
            });

            var pairs2 = kv.List(prefix, new QueryOptions() { WaitIndex = pairs.LastIndex });
            Assert.IsNotNull(pairs2.Response);
            Assert.AreEqual(pairs2.Response.Length, 1);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pairs2.Response[0].Value));
            Assert.AreEqual(pairs2.Response[0].Flags, (ulong)42);
            Assert.IsTrue(pairs2.LastIndex > pairs.LastIndex);

            var deleteTree = kv.DeleteTree(prefix);
            Assert.IsTrue(deleteTree.Response);
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
                putTasks[i] = Task.Run(() => kv.Put(p));
            }

            Task.WaitAll(putTasks);

            var pairs = kv.Keys(prefix, "");
            Assert.IsNotNull(pairs.Response);
            Assert.AreEqual(pairs.Response.Length, putTasks.Length);
            Assert.IsFalse(pairs.LastIndex == 0);

            var deleteTree = kv.DeleteTree(prefix);

            pairs = kv.Keys(prefix, "");
            Assert.IsNull(pairs.Response);
        }

        [TestMethod]
        public void KV_AcquireRelease()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session;
            var sesRes = s.CreateNoChecks(new SessionEntry());
            var id = sesRes.Response;

            Assert.IsFalse(string.IsNullOrEmpty(sesRes.Response));

            var kv = c.KV;
            var key = TestKey();
            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value,
                Session = id
            };

            var g = kv.Acquire(pair);
            Assert.IsTrue(g.Response);

            var res = kv.Get(key);

            Assert.IsNotNull(res.Response);
            Assert.AreEqual(id, res.Response.Session);
            Assert.AreEqual(res.Response.LockIndex, (ulong)1);
            Assert.IsTrue(res.LastIndex > 0);

            g = kv.Release(pair);
            Assert.IsTrue(g.Response);

            res = kv.Get(key);

            Assert.IsNotNull(res.Response);
            Assert.AreEqual(null, res.Response.Session);
            Assert.AreEqual(res.Response.LockIndex, (ulong)1);
            Assert.IsTrue(res.LastIndex > 0);

            var sesDesRes = s.Destroy(id);
            Assert.IsTrue(sesDesRes.Response);

            var del = kv.Delete(key);
            Assert.IsTrue(del.Response);
        }
    }
}