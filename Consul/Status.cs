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

using System.Threading.Tasks;

namespace Consul
{
    public class Status
    {
        private readonly Client _client;

        internal Status(Client c)
        {
            _client = c;
        }

        /// <summary>
        /// Leader is used to query for a known leader
        /// </summary>
        /// <returns>A write result containing the leader node name</returns>
        public string Leader()
        {
            var res = _client.CreateQuery<string>("/v1/status/leader").Execute();
            return res.Response;
        }

        /// <summary>
        /// Peers is used to query for a known raft peers
        /// </summary>
        /// <returns>A write result containing the list of Raft peers</returns>
        public string[] Peers()
        {
            var res = _client.CreateQuery<string[]>("/v1/status/peers").Execute();
            return res.Response;
        }
    }

    public partial class Client : IConsulClient
    {
        private Status _status;

        /// <summary>
        /// Status returns a handle to the status endpoints
        /// </summary>
        public Status Status
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