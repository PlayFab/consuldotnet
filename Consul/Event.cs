// -----------------------------------------------------------------------
//  <copyright file="Event.cs" company="PlayFab Inc">
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

using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Consul
{
    /// <summary>
    /// UserEvent represents an event that was fired by the user
    /// </summary>
    public class UserEvent
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public byte[] Payload { get; set; }
        public string NodeFilter { get; set; }
        public string ServiceFilter { get; set; }
        public string TagFilter { get; set; }
        public int Version { get; set; }
        public ulong LTime { get; set; }
    }

    public class Event
    {
        private class EventCreationResult
        {
            [JsonProperty]
            internal string ID { get; set; }
        }

        private readonly Client _client;

        internal Event(Client c)
        {
            _client = c;
        }

        public async Task<WriteResult<string>> Fire(UserEvent ue)
        {
            return await Fire(ue, WriteOptions.Empty);
        }

        /// <summary>
        /// Fire is used to fire a new user event. Only the Name, Payload and Filters are respected. This returns the ID or an associated error. Cross DC requests are supported.
        /// </summary>
        /// <param name="ue">A User Event definition</param>
        /// <param name="q">Customized write options</param>
        /// <returns></returns>
        public async Task<WriteResult<string>> Fire(UserEvent ue, WriteOptions q)
        {
            if (ue.Payload == null)
            {
                ue.Payload = new byte[0];
            }
            var req =
                _client.CreateWriteRequest<byte[], EventCreationResult>(string.Format("/v1/event/fire/{0}", ue.Name),
                    ue.Payload, q);
            if (!string.IsNullOrEmpty(ue.NodeFilter))
            {
                req.Params["node"] = ue.NodeFilter;
            }
            if (!string.IsNullOrEmpty(ue.ServiceFilter))
            {
                req.Params["service"] = ue.ServiceFilter;
            }
            if (!string.IsNullOrEmpty(ue.TagFilter))
            {
                req.Params["tag"] = ue.TagFilter;
            }
            var res = await req.Execute();
            var ret = new WriteResult<string>()
            {
                RequestTime = res.RequestTime,
                Response = res.Response.ID
            };
            return ret;
        }

        /// <summary>
        /// List is used to get the most recent events an agent has received. This list can be optionally filtered by the name. This endpoint supports quasi-blocking queries. The index is not monotonic, nor does it provide provide LastContact or KnownLeader.
        /// </summary>
        /// <returns>An array of events</returns>
        public async Task<QueryResult<UserEvent[]>> List()
        {
            return await List(string.Empty, QueryOptions.Empty);
        }

        /// <summary>
        /// List is used to get the most recent events an agent has received. This list can be optionally filtered by the name. This endpoint supports quasi-blocking queries. The index is not monotonic, nor does it provide provide LastContact or KnownLeader.
        /// </summary>
        /// <param name="name">The name of the event to filter for</param>
        /// <returns>An array of events</returns>
        public async Task<QueryResult<UserEvent[]>> List(string name)
        {
            return await List(name, QueryOptions.Empty);
        }

        /// <summary>
        /// List is used to get the most recent events an agent has received. This list can be optionally filtered by the name. This endpoint supports quasi-blocking queries. The index is not monotonic, nor does it provide provide LastContact or KnownLeader.
        /// </summary>
        /// <param name="name">The name of the event to filter for</param>
        /// <param name="q">Customized query options</param>
        /// <returns>An array of events</returns>
        public async Task<QueryResult<UserEvent[]>> List(string name, QueryOptions q)
        {
            var req = _client.CreateQueryRequest<UserEvent[]>("/v1/event/list", q);
            if (!string.IsNullOrEmpty(name))
            {
                req.Params["name"] = name;
            }
            return await req.Execute();
        }

        /// <summary>
        /// IDToIndex is a bit of a hack. This simulates the index generation to convert an event ID into a WaitIndex.
        /// </summary>
        /// <param name="uuid">The Event UUID</param>
        /// <returns>A "wait index" generated from the UUID</returns>
        public ulong IDToIndex(string uuid)
        {
            var lower = uuid.Take(8).Concat(uuid.Skip(9).Take(4)).Concat(uuid.Skip(14).Take(4)).ToArray();
            var upper = uuid.Skip(19).Take(4).Concat(uuid.Skip(24).Take(12)).ToArray();
            var lowVal = ulong.Parse(new string(lower), NumberStyles.HexNumber);
            var highVal = ulong.Parse(new string(upper), NumberStyles.HexNumber);
            return lowVal ^ highVal;
        }
    }

    public partial class Client
    {
        private Event _event;

        /// <summary>
        /// Event returns a handle to the event endpoints
        /// </summary>
        public Event Event
        {
            get
            {
                if (_event == null)
                {
                    lock (_lock)
                    {
                        if (_event == null)
                        {
                            _event = new Event(this);
                        }
                    }
                }
                return _event;
            }
        }
    }
}