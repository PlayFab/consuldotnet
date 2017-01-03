// -----------------------------------------------------------------------
//  <copyright file="StatusTest.cs" company="PlayFab Inc">
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
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    public class SnapshotTest : IDisposable
    {
        AsyncReaderWriterLock.Releaser m_lock;   
        public SnapshotTest()
        {
            m_lock = AsyncHelpers.RunSync(() => SelectiveParallel.NoParallel());
        }

        public void Dispose()
        {
            m_lock.Dispose();
        }

        [Fact]
        public async Task Snapshot_TakeRestore()
        {
            using (var client = new ConsulClient((c) => { c.Token = ACLTest.ConsulRoot; }))
            {
                var keyName = KVTest.GenerateTestKeyName();

                var key = new KVPair(keyName)
                {
                    Value = Encoding.UTF8.GetBytes("hello")
                };

                Assert.True((await client.KV.Put(key)).Response);

                Assert.Equal(Encoding.UTF8.GetBytes("hello"), (await client.KV.Get(keyName)).Response.Value);

                var snap = await client.Snapshot.Save();

                Assert.NotEqual<ulong>(0, snap.LastIndex);
                Assert.True(snap.KnownLeader);

                key.Value = Encoding.UTF8.GetBytes("goodbye");

                Assert.True((await client.KV.Put(key)).Response);

                Assert.Equal(Encoding.UTF8.GetBytes("goodbye"), (await client.KV.Get(keyName)).Response.Value);

                await client.Snapshot.Restore(snap.Response);

                Assert.Equal(Encoding.UTF8.GetBytes("hello"), (await client.KV.Get(keyName)).Response.Value);
            }
        }

        [Fact]
        public async Task Snapshot_Options()
        {
            using (var client = new ConsulClient())
            {
                // Try to take a snapshot with a bad token.
                await Assert.ThrowsAsync<ConsulRequestException>(() => client.Snapshot.Save(new QueryOptions() { Token = "anonymous" }));

                // Now try an unknown DC.
                await Assert.ThrowsAsync<ConsulRequestException>(() => client.Snapshot.Save(new QueryOptions() { Datacenter = "nope" }));

                // This should work with a valid token.
                var snap = await client.Snapshot.Save(new QueryOptions() { Token = ACLTest.ConsulRoot });
                Assert.IsAssignableFrom(typeof(Stream), snap.Response);

                // This should work with a stale snapshot. This doesn't have good feedback
                // that the stale option was sent, but it makes sure nothing bad happens.
                var snapStale = await client.Snapshot.Save(new QueryOptions() { Token = ACLTest.ConsulRoot, Consistency = ConsistencyMode.Stale });
                Assert.IsAssignableFrom(typeof(Stream), snapStale.Response);

                byte[] snapData;

                using (MemoryStream ms = new MemoryStream())
                {
                    snap.Response.CopyTo(ms);
                    snapData = ms.ToArray();
                }

                Assert.NotEmpty(snapData);

                // Try to restore a snapshot with a bad token.
                await Assert.ThrowsAsync<ConsulRequestException>(() => client.Snapshot.Restore(new MemoryStream(snapData, false), new WriteOptions() { Token = "anonymous" }));

                // Now try an unknown DC.
                await Assert.ThrowsAsync<ConsulRequestException>(() => client.Snapshot.Restore(new MemoryStream(snapData, false), new WriteOptions() { Datacenter = "nope" }));

                // This should work.
                await client.Snapshot.Restore(new MemoryStream(snapData, false), new WriteOptions() { Token = ACLTest.ConsulRoot });
            }
        }
    }
}