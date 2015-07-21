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

        internal static string GenerateTestKeyName()
        {
            var keyChars = new char[16];
            for (var i = 0; i < keyChars.Length; i++)
            {
                keyChars[i] = Convert.ToChar(Random.Next(65, 91));
            }
            return new string(keyChars);
        }

        [TestMethod]
        public void KV_Put_Get_Delete()
        {
            var client = new Client();
            var kv = client.KV;

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var getRequest = kv.Get(key);
            Assert.IsNull(getRequest.Response);

            var pair = new KVPair(key)
            {
                Flags = 42,
                Value = value
            };

            var putRequest = kv.Put(pair);
            Assert.IsTrue(putRequest.Response);

            getRequest = kv.Get(key);
            var res = getRequest.Response;

            Assert.IsNotNull(res);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, res.Value));
            Assert.AreEqual(pair.Flags, res.Flags);
            Assert.IsTrue(getRequest.LastIndex > 0);

            var del = kv.Delete(key);
            Assert.IsTrue(del.Response);

            getRequest = kv.Get(key);
            Assert.IsNull(getRequest.Response);
        }

        [TestMethod]
        public void KV_List_DeleteRecurse()
        {
            var client = new Client();

            var prefix = GenerateTestKeyName();

            var putTasks = new Task[100];

            var value = Encoding.UTF8.GetBytes("test");
            for (var i = 0; i < 100; i++)
            {
                var p = new KVPair(string.Join("/", prefix, GenerateTestKeyName()))
                {
                    Value = value
                };
                putTasks[i] = Task.Run(() => client.KV.Put(p));
            }

            Task.WaitAll(putTasks);

            var pairs = client.KV.List(prefix);
            Assert.IsNotNull(pairs.Response);
            Assert.AreEqual(pairs.Response.Length, putTasks.Length);
            foreach (var pair in pairs.Response)
            {
                Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            }
            Assert.IsFalse(pairs.LastIndex == 0);

            client.KV.DeleteTree(prefix);

            pairs = client.KV.List(prefix);
            Assert.IsNull(pairs.Response);
        }

        [TestMethod]
        public void KV_DeleteCAS()
        {
            var client = new Client();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value
            };

            var putRequest = client.KV.CAS(pair);
            Assert.IsTrue(putRequest.Response);

            var getRequest = client.KV.Get(key);
            pair = getRequest.Response;

            Assert.IsNotNull(pair);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            Assert.IsTrue(getRequest.LastIndex > 0);

            pair.ModifyIndex = 1;
            var deleteRequest = client.KV.DeleteCAS(pair);

            Assert.IsFalse(deleteRequest.Response);

            pair.ModifyIndex = getRequest.LastIndex;
            deleteRequest = client.KV.DeleteCAS(pair);

            Assert.IsTrue(deleteRequest.Response);
        }

        [TestMethod]
        public void KV_CAS()
        {
            var client = new Client();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value
            };

            var putRequest = client.KV.CAS(pair);
            Assert.IsTrue(putRequest.Response);

            var getRequest = client.KV.Get(key);
            pair = getRequest.Response;

            Assert.IsNotNull(pair);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            Assert.IsTrue(getRequest.LastIndex > 0);

            value = Encoding.UTF8.GetBytes("foo");
            pair.Value = value;

            pair.ModifyIndex = 1;
            var casRequest = client.KV.CAS(pair);

            Assert.IsFalse(casRequest.Response);

            pair.ModifyIndex = getRequest.LastIndex;
            casRequest = client.KV.CAS(pair);
            Assert.IsTrue(casRequest.Response);

            var deleteRequest = client.KV.Delete(key);
            Assert.IsTrue(deleteRequest.Response);
        }

        [TestMethod]
        public void KV_WatchGet()
        {
            var client = new Client();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var getRequest = client.KV.Get(key);
            Assert.IsNull(getRequest.Response);

            var pair = new KVPair(key)
            {
                Flags = 42,
                Value = value
            };

            Task.Run(() =>
            {
                Task.Delay(1100).Wait();
                var p = new KVPair(key) { Flags = 42, Value = value };
                var putResponse = client.KV.Put(p);
                Assert.IsTrue(putResponse.Response);
            });

            var getRequest2 = client.KV.Get(key, new QueryOptions() { WaitIndex = getRequest.LastIndex });
            var res = getRequest2.Response;

            Assert.IsNotNull(res);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(value, res.Value));
            Assert.AreEqual(pair.Flags, res.Flags);
            Assert.IsTrue(getRequest2.LastIndex > 0);

            var deleteRequest = client.KV.Delete(key);
            Assert.IsTrue(deleteRequest.Response);

            getRequest = client.KV.Get(key);
            Assert.IsNull(getRequest.Response);
        }
        [TestMethod]
        public void KV_WatchGet_Cancel()
        {
            var client = new Client();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var getRequest = client.KV.Get(key);
            Assert.IsNull(getRequest.Response);

            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(1000);

                try
                {
                    getRequest = client.KV.Get(key, new QueryOptions() { WaitIndex = getRequest.LastIndex }, cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(OperationCanceledException));
                }
            }
        }

        [TestMethod]
        public void KV_WatchList()
        {
            var client = new Client();

            var prefix = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pairs = client.KV.List(prefix);
            Assert.IsNull(pairs.Response);

            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(1000);

                try
                {
                    pairs = client.KV.List(prefix, new QueryOptions() { WaitIndex = pairs.LastIndex }, cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(OperationCanceledException));
                }
            }
        }

        [TestMethod]
        public void KV_Keys_DeleteRecurse()
        {
            var client = new Client();

            var prefix = GenerateTestKeyName();

            var putTasks = new Task[100];

            var value = Encoding.UTF8.GetBytes("test");
            for (var i = 0; i < 100; i++)
            {
                var pair = new KVPair(string.Join("/", prefix, GenerateTestKeyName()))
                {
                    Value = value
                };
                putTasks[i] = Task.Run(() => client.KV.Put(pair));
            }

            Task.WaitAll(putTasks);

            var pairs = client.KV.Keys(prefix, "");
            Assert.IsNotNull(pairs.Response);
            Assert.AreEqual(pairs.Response.Length, putTasks.Length);
            Assert.IsFalse(pairs.LastIndex == 0);

            var deleteTree = client.KV.DeleteTree(prefix);

            pairs = client.KV.Keys(prefix, "");
            Assert.IsNull(pairs.Response);
        }

        [TestMethod]
        public void KV_AcquireRelease()
        {
            var client = new Client();
            var sessionRequest = client.Session.CreateNoChecks(new SessionEntry());
            var id = sessionRequest.Response;

            Assert.IsFalse(string.IsNullOrEmpty(sessionRequest.Response));

            var key = GenerateTestKeyName();
            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value,
                Session = id
            };

            var acquireRequest = client.KV.Acquire(pair);
            Assert.IsTrue(acquireRequest.Response);

            var getRequest = client.KV.Get(key);

            Assert.IsNotNull(getRequest.Response);
            Assert.AreEqual(id, getRequest.Response.Session);
            Assert.AreEqual(getRequest.Response.LockIndex, (ulong)1);
            Assert.IsTrue(getRequest.LastIndex > 0);

            acquireRequest = client.KV.Release(pair);
            Assert.IsTrue(acquireRequest.Response);

            getRequest = client.KV.Get(key);

            Assert.IsNotNull(getRequest.Response);
            Assert.AreEqual(null, getRequest.Response.Session);
            Assert.AreEqual(getRequest.Response.LockIndex, (ulong)1);
            Assert.IsTrue(getRequest.LastIndex > 0);

            var sessionDestroyRequest = client.Session.Destroy(id);
            Assert.IsTrue(sessionDestroyRequest.Response);

            var deleteRequest = client.KV.Delete(key);
            Assert.IsTrue(deleteRequest.Response);
        }
    }
}