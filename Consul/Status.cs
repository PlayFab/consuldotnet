// -----------------------------------------------------------------------
//  <copyright file="Status.cs" company="PlayFab Inc">
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

namespace Consul
{
    public class Status : IStatusEndpoint
    {
        private readonly ConsulClient _client;

        internal Status(ConsulClient c)
        {
            _client = c;
        }
        /// <summary>
        /// Leader is used to query for a known leader
        /// </summary>
        /// <returns>A write result containing the leader node name</returns>
        public async Task<string> Leader(CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.Get<string>("/v1/status/leader").Execute(ct).ConfigureAwait(false);
            return res.Response;
        }

        /// <summary>
        /// Peers is used to query for a known raft peers
        /// </summary>
        /// <returns>A write result containing the list of Raft peers</returns>
        public async Task<string[]> Peers(CancellationToken ct = default(CancellationToken))
        {
            var res = await _client.Get<string[]>("/v1/status/peers").Execute(ct).ConfigureAwait(false);
            return res.Response;
        }
    }

    public partial class ConsulClient : IConsulClient
    {
        private Status _status;

        /// <summary>
        /// Status returns a handle to the status endpoint
        /// </summary>
        public IStatusEndpoint Status
        {
            get
            {
                if (_status == null)
                {
                    lock (_lock)
                    {
                        if (_status == null)
                        {
                            _status = new Status(this);
                        }
                    }
                }
                return _status;
            }
        }
    }
}