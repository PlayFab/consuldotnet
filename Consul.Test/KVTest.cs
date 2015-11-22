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
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    public class KVTest
    {
        private static readonly Random Random = new Random((int)DateTime.Now.Ticks);

        internal static string GenerateTestKeyName()
        {
            StackFrame frame = new StackFrame(1);
            var keyChars = new char[16];
            for (var i = 0; i < keyChars.Length; i++)
            {
                keyChars[i] = Convert.ToChar(Random.Next(65, 91));
            }
            return (new string(keyChars)) + "_" + frame.GetMethod().Name;
        }

        [Fact]
        public void KV_Put_Get_Delete()
        {
            var client = new ConsulClient();
            var kv = client.KV;

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var getRequest = kv.Get(key);
            Assert.Null(getRequest.Response);

            var pair = new KVPair(key)
            {
                Flags = 42,
                Value = value
            };

            var putRequest = kv.Put(pair);
            Assert.True(putRequest.Response);

            try
            {
                // Put a key that begins with a '/'
                var invalidKey = new KVPair("/test")
                {
                    Flags = 42,
                    Value = value
                };
                kv.Put(invalidKey);
                Assert.True(false, "Invalid key not detected");
            }
            catch (InvalidKeyPairException ex)
            {
                Assert.IsType<InvalidKeyPairException>(ex);
            }

            getRequest = kv.Get(key);
            var res = getRequest.Response;

            Assert.NotNull(res);
            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, res.Value));
            Assert.Equal(pair.Flags, res.Flags);
            Assert.True(getRequest.LastIndex > 0);

            var del = kv.Delete(key);
            Assert.True(del.Response);

            getRequest = kv.Get(key);
            Assert.Null(getRequest.Response);
        }

        [Fact]
        public void KV_List_DeleteRecurse()
        {
            var client = new ConsulClient();

            var prefix = GenerateTestKeyName();


            var value = Encoding.UTF8.GetBytes("test");
            for (var i = 0; i < 100; i++)
            {
                var p = new KVPair(string.Join("/", prefix, GenerateTestKeyName()))
                {
                    Value = value
                };
                Assert.True(client.KV.Put(p).Response);
            }

            var pairs = client.KV.List(prefix);
            Assert.NotNull(pairs.Response);
            Assert.Equal(pairs.Response.Length, 100);
            foreach (var pair in pairs.Response)
            {
                Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            }
            Assert.False(pairs.LastIndex == 0);

            client.KV.DeleteTree(prefix);

            pairs = client.KV.List(prefix);
            Assert.Null(pairs.Response);
        }

        [Fact]
        public void KV_DeleteCAS()
        {
            var client = new ConsulClient();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value
            };

            var putRequest = client.KV.CAS(pair);
            Assert.True(putRequest.Response);

            var getRequest = client.KV.Get(key);
            pair = getRequest.Response;

            Assert.NotNull(pair);
            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            Assert.True(getRequest.LastIndex > 0);

            pair.ModifyIndex = 1;
            var deleteRequest = client.KV.DeleteCAS(pair);

            Assert.False(deleteRequest.Response);

            pair.ModifyIndex = getRequest.LastIndex;
            deleteRequest = client.KV.DeleteCAS(pair);

            Assert.True(deleteRequest.Response);
        }

        [Fact]
        public void KV_CAS()
        {
            var client = new ConsulClient();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value
            };

            var putRequest = client.KV.CAS(pair);
            Assert.True(putRequest.Response);

            var getRequest = client.KV.Get(key);
            pair = getRequest.Response;

            Assert.NotNull(pair);
            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            Assert.True(getRequest.LastIndex > 0);

            value = Encoding.UTF8.GetBytes("foo");
            pair.Value = value;

            pair.ModifyIndex = 1;
            var casRequest = client.KV.CAS(pair);

            Assert.False(casRequest.Response);

            pair.ModifyIndex = getRequest.LastIndex;
            casRequest = client.KV.CAS(pair);
            Assert.True(casRequest.Response);

            var deleteRequest = client.KV.Delete(key);
            Assert.True(deleteRequest.Response);
        }

        [Fact]
        public void KV_WatchGet()
        {
            var client = new ConsulClient();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var getRequest = client.KV.Get(key);
            Assert.Null(getRequest.Response);

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
                Assert.True(putResponse.Response);
            });

            var getRequest2 = client.KV.Get(key, new QueryOptions() { WaitIndex = getRequest.LastIndex });
            var res = getRequest2.Response;

            Assert.NotNull(res);
            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, res.Value));
            Assert.Equal(pair.Flags, res.Flags);
            Assert.True(getRequest2.LastIndex > 0);

            var deleteRequest = client.KV.Delete(key);
            Assert.True(deleteRequest.Response);

            getRequest = client.KV.Get(key);
            Assert.Null(getRequest.Response);
        }
        [Fact]
        public void KV_WatchGet_Cancel()
        {
            var client = new ConsulClient();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var getRequest = client.KV.Get(key);
            Assert.Null(getRequest.Response);

            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(1000);

                try
                {
                    getRequest = client.KV.Get(key, new QueryOptions() { WaitIndex = getRequest.LastIndex }, cts.Token);
                    Assert.True(false, "A cancellation exception was not thrown when one was expected.");
                }
                catch (TaskCanceledException ex)
                {
                    Assert.IsType<TaskCanceledException>(ex);
                }
            }
        }

        [Fact]
        public void KV_WatchList()
        {
            var client = new ConsulClient();

            var prefix = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pairs = client.KV.List(prefix);
            Assert.Null(pairs.Response);

            Task.Run(() =>
            {
                Thread.Sleep(100);
                var p = new KVPair(prefix) { Flags = 42, Value = value };
                var putRes = client.KV.Put(p);
                Assert.True(putRes.Response);
            });

            var pairs2 = client.KV.List(prefix, new QueryOptions() { WaitIndex = pairs.LastIndex });
            Assert.NotNull(pairs2.Response);
            Assert.Equal(pairs2.Response.Length, 1);
            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, pairs2.Response[0].Value));
            Assert.Equal(pairs2.Response[0].Flags, (ulong)42);
            Assert.True(pairs2.LastIndex > pairs.LastIndex);

            var deleteTree = client.KV.DeleteTree(prefix);
            Assert.True(deleteTree.Response);
        }
        [Fact]
        public void KV_WatchList_Cancel()
        {
            var client = new ConsulClient();

            var prefix = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pairs = client.KV.List(prefix);
            Assert.Null(pairs.Response);

            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(1000);

                try
                {
                    pairs = client.KV.List(prefix, new QueryOptions() { WaitIndex = pairs.LastIndex }, cts.Token);
                    Assert.True(false, "A cancellation exception was not thrown when one was expected.");
                }
                catch (TaskCanceledException ex)
                {
                    Assert.IsType<TaskCanceledException>(ex);
                }
            }
        }
        [Fact]
        public void KV_Keys_DeleteRecurse()
        {
            var client = new ConsulClient();

            var prefix = GenerateTestKeyName();

            var putTasks = new bool[100];

            var value = Encoding.UTF8.GetBytes("test");
            for (var i = 0; i < 100; i++)
            {
                var pair = new KVPair(string.Join("/", prefix, GenerateTestKeyName()))
                {
                    Value = value
                };
                putTasks[i] = client.KV.Put(pair).Response;
            }

            var pairs = client.KV.Keys(prefix, "");
            Assert.NotNull(pairs.Response);
            Assert.Equal(pairs.Response.Length, putTasks.Length);
            Assert.False(pairs.LastIndex == 0);

            var deleteTree = client.KV.DeleteTree(prefix);

            pairs = client.KV.Keys(prefix, "");
            Assert.Null(pairs.Response);
        }

        [Fact]
        public void KV_AcquireRelease()
        {
            var client = new ConsulClient();
            var sessionRequest = client.Session.CreateNoChecks(new SessionEntry());
            var id = sessionRequest.Response;

            Assert.False(string.IsNullOrEmpty(sessionRequest.Response));

            var key = GenerateTestKeyName();
            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value,
                Session = id
            };

            var acquireRequest = client.KV.Acquire(pair);
            Assert.True(acquireRequest.Response);

            var getRequest = client.KV.Get(key);

            Assert.NotNull(getRequest.Response);
            Assert.Equal(id, getRequest.Response.Session);
            Assert.Equal(getRequest.Response.LockIndex, (ulong)1);
            Assert.True(getRequest.LastIndex > 0);

            acquireRequest = client.KV.Release(pair);
            Assert.True(acquireRequest.Response);

            getRequest = client.KV.Get(key);

            Assert.NotNull(getRequest.Response);
            Assert.Equal(null, getRequest.Response.Session);
            Assert.Equal(getRequest.Response.LockIndex, (ulong)1);
            Assert.True(getRequest.LastIndex > 0);

            var sessionDestroyRequest = client.Session.Destroy(id);
            Assert.True(sessionDestroyRequest.Response);

            var deleteRequest = client.KV.Delete(key);
            Assert.True(deleteRequest.Response);
        }
    }
}