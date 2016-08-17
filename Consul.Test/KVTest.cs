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
using System.Collections.Generic;
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
            var keyChars = new char[16];
            for (var i = 0; i < keyChars.Length; i++)
            {
                keyChars[i] = Convert.ToChar(Random.Next(65, 91));
            }
            return new string(keyChars);
        }

        [Fact]
        public async Task KV_Put_Get_Delete()
        {
            var client = new ConsulClient();
            var kv = client.KV;

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var getRequest = await kv.Get(key);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, getRequest.StatusCode);
            Assert.Null(getRequest.Response);

            var pair = new KVPair(key)
            {
                Flags = 42,
                Value = value
            };

            var putRequest = await kv.Put(pair);
            Assert.Equal(System.Net.HttpStatusCode.OK, putRequest.StatusCode);
            Assert.True(putRequest.Response);

            try
            {
                // Put a key that begins with a '/'
                var invalidKey = new KVPair("/test")
                {
                    Flags = 42,
                    Value = value
                };
                await kv.Put(invalidKey);
                Assert.True(false, "Invalid key not detected");
            }
            catch (InvalidKeyPairException ex)
            {
                Assert.IsType<InvalidKeyPairException>(ex);
            }

            getRequest = await kv.Get(key);
            Assert.Equal(System.Net.HttpStatusCode.OK, getRequest.StatusCode);
            var res = getRequest.Response;

            Assert.NotNull(res);
            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, res.Value));
            Assert.Equal(pair.Flags, res.Flags);
            Assert.True(getRequest.LastIndex > 0);

            var del = await kv.Delete(key);
            Assert.True(del.Response);

            getRequest = await kv.Get(key);
            Assert.Null(getRequest.Response);
        }

        [Fact]
        public async Task KV_List_DeleteRecurse()
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
                Assert.True((await client.KV.Put(p)).Response);
            }

            var pairs = await client.KV.List(prefix);
            Assert.NotNull(pairs.Response);
            Assert.Equal(pairs.Response.Length, 100);
            foreach (var pair in pairs.Response)
            {
                Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            }
            Assert.False(pairs.LastIndex == 0);

            await client.KV.DeleteTree(prefix);

            pairs = await client.KV.List(prefix);
            Assert.Null(pairs.Response);
        }

        [Fact]
        public async Task KV_DeleteCAS()
        {
            var client = new ConsulClient();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value
            };

            var putRequest = await client.KV.CAS(pair);
            Assert.True(putRequest.Response);

            var getRequest = await client.KV.Get(key);
            pair = getRequest.Response;

            Assert.NotNull(pair);
            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            Assert.True(getRequest.LastIndex > 0);

            pair.ModifyIndex = 1;
            var deleteRequest = await client.KV.DeleteCAS(pair);

            Assert.False(deleteRequest.Response);

            pair.ModifyIndex = getRequest.LastIndex;
            deleteRequest = await client.KV.DeleteCAS(pair);

            Assert.True(deleteRequest.Response);
        }

        [Fact]
        public async Task KV_CAS()
        {
            var client = new ConsulClient();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value
            };

            var putRequest = await client.KV.CAS(pair);
            Assert.True(putRequest.Response);

            var getRequest = await client.KV.Get(key);
            pair = getRequest.Response;

            Assert.NotNull(pair);
            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, pair.Value));
            Assert.True(getRequest.LastIndex > 0);

            value = Encoding.UTF8.GetBytes("foo");
            pair.Value = value;

            pair.ModifyIndex = 1;
            var casRequest = await client.KV.CAS(pair);

            Assert.False(casRequest.Response);

            pair.ModifyIndex = getRequest.LastIndex;
            casRequest = await client.KV.CAS(pair);
            Assert.True(casRequest.Response);

            var deleteRequest = await client.KV.Delete(key);
            Assert.True(deleteRequest.Response);
        }

        [Fact]
        public async Task KV_WatchGet()
        {
            var client = new ConsulClient();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var getRequest = await client.KV.Get(key);
            Assert.Null(getRequest.Response);

            var pair = new KVPair(key)
            {
                Flags = 42,
                Value = value
            };

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                Task.Delay(1100).Wait();
                var p = new KVPair(key) { Flags = 42, Value = value };
                var putResponse = await client.KV.Put(p);
                Assert.True(putResponse.Response);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            var getRequest2 = await client.KV.Get(key, new QueryOptions() { WaitIndex = getRequest.LastIndex });
            var res = getRequest2.Response;

            Assert.NotNull(res);
            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, res.Value));
            Assert.Equal(pair.Flags, res.Flags);
            Assert.True(getRequest2.LastIndex > 0);

            var deleteRequest = await client.KV.Delete(key);
            Assert.True(deleteRequest.Response);

            getRequest = await client.KV.Get(key);
            Assert.Null(getRequest.Response);
        }
        [Fact]
        public async Task KV_WatchGet_Cancel()
        {
            var client = new ConsulClient();

            var key = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var getRequest = await client.KV.Get(key);
            Assert.Null(getRequest.Response);

            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(1000);

                try
                {
                    getRequest = await client.KV.Get(key, new QueryOptions() { WaitIndex = getRequest.LastIndex }, cts.Token);
                    Assert.True(false, "A cancellation exception was not thrown when one was expected.");
                }
                catch (TaskCanceledException ex)
                {
                    Assert.IsType<TaskCanceledException>(ex);
                }
            }
        }

        [Fact]
        public async Task KV_WatchList()
        {
            var client = new ConsulClient();

            var prefix = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pairs = await client.KV.List(prefix);
            Assert.Null(pairs.Response);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                Thread.Sleep(100);
                var p = new KVPair(prefix) { Flags = 42, Value = value };
                var putRes = await client.KV.Put(p);
                Assert.True(putRes.Response);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            var pairs2 = await client.KV.List(prefix, new QueryOptions() { WaitIndex = pairs.LastIndex });
            Assert.NotNull(pairs2.Response);
            Assert.Equal(pairs2.Response.Length, 1);
            Assert.True(StructuralComparisons.StructuralEqualityComparer.Equals(value, pairs2.Response[0].Value));
            Assert.Equal(pairs2.Response[0].Flags, (ulong)42);
            Assert.True(pairs2.LastIndex > pairs.LastIndex);

            var deleteTree = await client.KV.DeleteTree(prefix);
            Assert.True(deleteTree.Response);
        }
        [Fact]
        public async Task KV_WatchList_Cancel()
        {
            var client = new ConsulClient();

            var prefix = GenerateTestKeyName();

            var value = Encoding.UTF8.GetBytes("test");

            var pairs = await client.KV.List(prefix);
            Assert.Null(pairs.Response);

            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(1000);

                try
                {
                    pairs = await client.KV.List(prefix, new QueryOptions() { WaitIndex = pairs.LastIndex }, cts.Token);
                    Assert.True(false, "A cancellation exception was not thrown when one was expected.");
                }
                catch (TaskCanceledException ex)
                {
                    Assert.IsType<TaskCanceledException>(ex);
                }
            }
        }
        [Fact]
        public async Task KV_Keys_DeleteRecurse()
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
                putTasks[i] = (await client.KV.Put(pair)).Response;
            }

            var pairs = await client.KV.Keys(prefix, "");
            Assert.NotNull(pairs.Response);
            Assert.Equal(pairs.Response.Length, putTasks.Length);
            Assert.False(pairs.LastIndex == 0);

            var deleteTree = await client.KV.DeleteTree(prefix);

            pairs = await client.KV.Keys(prefix, "");
            Assert.Null(pairs.Response);
        }

        [Fact]
        public async Task KV_AcquireRelease()
        {
            var client = new ConsulClient();
            var sessionRequest = await client.Session.CreateNoChecks(new SessionEntry());
            var id = sessionRequest.Response;

            Assert.False(string.IsNullOrEmpty(sessionRequest.Response));

            var key = GenerateTestKeyName();
            var value = Encoding.UTF8.GetBytes("test");

            var pair = new KVPair(key)
            {
                Value = value,
                Session = id
            };

            var acquireRequest = await client.KV.Acquire(pair);
            Assert.True(acquireRequest.Response);

            var getRequest = await client.KV.Get(key);

            Assert.NotNull(getRequest.Response);
            Assert.Equal(id, getRequest.Response.Session);
            Assert.Equal(getRequest.Response.LockIndex, (ulong)1);
            Assert.True(getRequest.LastIndex > 0);

            acquireRequest = await client.KV.Release(pair);
            Assert.True(acquireRequest.Response);

            getRequest = await client.KV.Get(key);

            Assert.NotNull(getRequest.Response);
            Assert.Equal(null, getRequest.Response.Session);
            Assert.Equal(getRequest.Response.LockIndex, (ulong)1);
            Assert.True(getRequest.LastIndex > 0);

            var sessionDestroyRequest = await client.Session.Destroy(id);
            Assert.True(sessionDestroyRequest.Response);

            var deleteRequest = await client.KV.Delete(key);
            Assert.True(deleteRequest.Response);
        }

        [Fact]
        public async Task KV_Txn()
        {


            using (var client = new ConsulClient())
            {
                string id = string.Empty;

                try
                {
                    id = (await client.Session.CreateNoChecks()).Response;

                    var keyName = GenerateTestKeyName();
                    var keyValue = Encoding.UTF8.GetBytes("test");

                    var txn = new List<KVTxnOp> {
                        new KVTxnOp(keyName, KVTxnVerb.Lock) { Value = keyValue },
                        new KVTxnOp(keyName, KVTxnVerb.Get)
                    };

                    var result = await client.KV.Txn(txn);

                    Assert.False(result.Response.Success, "transaction should have failed");
                    Assert.Equal(2, result.Response.Errors.Count);
                    Assert.Equal(0, result.Response.Results.Count);

                    Assert.Equal(0, result.Response.Errors[0].OpIndex);
                    Assert.Contains("missing session", result.Response.Errors[0].What);
                    Assert.Contains("doesn't exist", result.Response.Errors[1].What);

                    // Now poke in a real session and try again.
                    txn[0].Session = id;

                    result = await client.KV.Txn(txn);

                    Assert.True(result.Response.Success, "transaction failure");
                    Assert.Equal(0, result.Response.Errors.Count);
                    Assert.Equal(2, result.Response.Results.Count);


                    for (int i = 0; i < result.Response.Results.Count; i++)
                    {
                        byte[] expected = null;
                        if (i == 1) { expected = keyValue; }

                        var item = result.Response.Results[i];

                        Assert.Equal(keyName, item.Key);
                        Assert.Equal(expected, item.Value);
                        Assert.Equal(1ul, item.LockIndex);
                        Assert.Equal(id, item.Session);
                    }

                    // Run a read-only transaction.
                    txn = new List<KVTxnOp> {
                        new KVTxnOp(keyName, KVTxnVerb.Get)
                    };

                    result = await client.KV.Txn(txn);

                    Assert.True(result.Response.Success, "transaction failure");
                    Assert.Equal(0, result.Response.Errors.Count);
                    Assert.Equal(1, result.Response.Results.Count);

                    var getResult = result.Response.Results[0];

                    Assert.Equal(keyName, getResult.Key);
                    Assert.Equal(keyValue, getResult.Value);
                    Assert.Equal(1ul, getResult.LockIndex);
                    Assert.Equal(id, getResult.Session);

                    // Sanity check using the regular GET API.
                    var pair = await client.KV.Get(keyName);

                    Assert.NotNull(pair.Response);
                    Assert.Equal(1ul, pair.Response.LockIndex);
                    Assert.Equal(id, pair.Response.Session);
                    Assert.NotEqual(0ul, pair.LastIndex);
                }
                finally
                {
                    await client.Session.Destroy(id);
                }
            }
        }
    }
}