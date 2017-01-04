// -----------------------------------------------------------------------
//  <copyright file="EventTest.cs" company="PlayFab Inc">
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
using Xunit;

namespace Consul.Test
{    
    public class EventTest : IDisposable
    {
        AsyncReaderWriterLock.Releaser m_lock;
        public EventTest()
        {
            m_lock = AsyncHelpers.RunSync(() => SelectiveParallel.Parallel());
        }

        public void Dispose()
        {
            m_lock.Dispose();
        }
    
        [Fact]
        public async Task Event_FireList()
        {
            var client = new ConsulClient();

            var userevent = new UserEvent()
            {
                Name = "foo"
            };

            var res = await client.Event.Fire(userevent);

            await Task.Delay(100);

            Assert.NotEqual(TimeSpan.Zero, res.RequestTime);
            Assert.False(string.IsNullOrEmpty(res.Response));

            var events = await client.Event.List();
            Assert.NotEqual(0, events.Response.Length);
            Assert.Equal(res.Response, events.Response[events.Response.Length - 1].ID);
            Assert.Equal(client.Event.IDToIndex(res.Response), events.LastIndex);
        }
    }
}