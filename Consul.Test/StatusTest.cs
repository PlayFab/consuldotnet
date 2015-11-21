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

using Xunit;

namespace Consul.Test
{
    public class Status
    {
        [Fact]
        public void Status_Leader()
        {
            var client = new Client();
            var leader = client.Status.Leader();
            Assert.False(string.IsNullOrEmpty(leader));
        }

        [Fact]
        public void Status_Peers()
        {
            var client = new Client();
            var peers = client.Status.Peers();
            Assert.True(peers.Length > 0);
        }
    }
}