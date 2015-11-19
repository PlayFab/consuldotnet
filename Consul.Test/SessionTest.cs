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
using Xunit;

namespace Consul.Test
{
    public class SessionTest
    {
        [Fact]
        public void Session_CreateDestroy()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create();
            var id = sessionRequest.Response;
            Assert.True(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.False(string.IsNullOrEmpty(sessionRequest.Response));

            var destroyRequest = client.Session.Destroy(id);
            Assert.True(destroyRequest.Response);
        }

        [Fact]
        public void Session_CreateNoChecksDestroy()
        {
            var client = new Client();
            var sessionRequest = client.Session.CreateNoChecks();

            var id = sessionRequest.Response;
            Assert.True(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.False(string.IsNullOrEmpty(sessionRequest.Response));

            var destroyRequest = client.Session.Destroy(id);
            Assert.True(destroyRequest.Response);
        }

        [Fact]
        public void Session_CreateRenewDestroy()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) });

            var id = sessionRequest.Response;
            Assert.True(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.False(string.IsNullOrEmpty(sessionRequest.Response));

            var renewRequest = client.Session.Renew(id);
            Assert.True(renewRequest.RequestTime.TotalMilliseconds > 0);
            Assert.NotNull(renewRequest.Response.ID);
            Assert.Equal(sessionRequest.Response, renewRequest.Response.ID);
            Assert.True(renewRequest.Response.TTL.HasValue);
            Assert.Equal(renewRequest.Response.TTL.Value.TotalSeconds, TimeSpan.FromSeconds(10).TotalSeconds);

            var destroyRequest = client.Session.Destroy(id);
            Assert.True(destroyRequest.Response);
        }

        [Fact]
        public void Session_CreateRenewDestroyRenew()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) });

            var id = sessionRequest.Response;
            Assert.True(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.False(string.IsNullOrEmpty(sessionRequest.Response));

            var renewRequest = client.Session.Renew(id);
            Assert.True(renewRequest.RequestTime.TotalMilliseconds > 0);
            Assert.NotNull(renewRequest.Response.ID);
            Assert.Equal(sessionRequest.Response, renewRequest.Response.ID);
            Assert.Equal(renewRequest.Response.TTL.Value.TotalSeconds, TimeSpan.FromSeconds(10).TotalSeconds);

            var destroyRequest = client.Session.Destroy(id);
            Assert.True(destroyRequest.Response);

            try
            {
                renewRequest = client.Session.Renew(id);
                Assert.True(false, "Session still exists");
            }
            catch (SessionExpiredException ex)
            {
                Assert.IsType<SessionExpiredException>(ex);
            }
        }

        [Fact]
        public void Session_Create_RenewPeriodic_Destroy()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) });

            var id = sessionRequest.Response;
            Assert.True(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.False(string.IsNullOrEmpty(sessionRequest.Response));

            var tokenSource = new CancellationTokenSource();
            var ct = tokenSource.Token;

            var renewTask = client.Session.RenewPeriodic(TimeSpan.FromSeconds(1), id, WriteOptions.Empty, ct);

            var infoRequest = client.Session.Info(id);
            Assert.True(infoRequest.LastIndex > 0);
            Assert.NotNull(infoRequest.KnownLeader);

            Assert.Equal(id, infoRequest.Response.ID);

            Assert.True(client.Session.Destroy(id).Response);

            try
            {
                renewTask.Wait(1000);
                Assert.True(false, "timedout: missing session did not terminate renewal loop");
            }
            catch (AggregateException ae)
            {
                Assert.IsType<SessionExpiredException>(ae.InnerExceptions[0]);
            }
        }

        [Fact]
        public void Session_Create_RenewPeriodic_TTLExpire()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(500) });

            var id = sessionRequest.Response;
            Assert.True(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.False(string.IsNullOrEmpty(sessionRequest.Response));

            var tokenSource = new CancellationTokenSource();
            var ct = tokenSource.Token;

            try
            {
                var renewTask = client.Session.RenewPeriodic(TimeSpan.FromSeconds(1), id, WriteOptions.Empty, ct);
                Assert.True(client.Session.Destroy(id).Response);
                renewTask.Wait(1000);
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    Assert.IsType<SessionExpiredException>(e);
                }
                return;
            }
            catch (SessionExpiredException ex)
            {
                Assert.IsType<SessionExpiredException>(ex);
                return;
            }
            Assert.True(false, "timed out: missing session did not terminate renewal loop");
        }

        [Fact]
        public void Session_Info()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create();

            var id = sessionRequest.Response;

            Assert.True(sessionRequest.RequestTime.TotalMilliseconds > 0);
            Assert.False(string.IsNullOrEmpty(sessionRequest.Response));

            var infoRequest = client.Session.Info(id);
            Assert.True(infoRequest.LastIndex > 0);
            Assert.NotNull(infoRequest.KnownLeader);

            Assert.Equal(id, infoRequest.Response.ID);

            Assert.True(string.IsNullOrEmpty(infoRequest.Response.Name));
            Assert.False(string.IsNullOrEmpty(infoRequest.Response.Node));
            Assert.True(infoRequest.Response.CreateIndex > 0);
            Assert.Equal(infoRequest.Response.Behavior, SessionBehavior.Release);

            Assert.True(string.IsNullOrEmpty(infoRequest.Response.Name));
            Assert.NotNull(infoRequest.KnownLeader);

            Assert.True(infoRequest.LastIndex > 0);
            Assert.NotNull(infoRequest.KnownLeader);

            var destroyRequest = client.Session.Destroy(id);

            Assert.True(destroyRequest.Response);
        }

        [Fact]
        public void Session_Node()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create();

            var id = sessionRequest.Response;
            try
            {
                var infoRequest = client.Session.Info(id);

                var nodeRequest = client.Session.Node(infoRequest.Response.Node);

                Assert.Equal(nodeRequest.Response.Length, 1);
                Assert.NotEqual((ulong)0, nodeRequest.LastIndex);
                Assert.True(nodeRequest.KnownLeader);
            }
            finally
            {
                var destroyRequest = client.Session.Destroy(id);

                Assert.True(destroyRequest.Response);
            }
        }

        [Fact]
        public void Session_List()
        {
            var client = new Client();
            var sessionRequest = client.Session.Create();

            var id = sessionRequest.Response;

            try
            {
                var listRequest = client.Session.List();

                Assert.Equal(listRequest.Response.Length, 1);
                Assert.NotEqual((ulong)0, listRequest.LastIndex);
                Assert.True(listRequest.KnownLeader);
            }
            finally
            {
                var destroyRequest = client.Session.Destroy(id);

                Assert.True(destroyRequest.Response);
            }
        }
    }
}