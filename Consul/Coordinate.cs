// -----------------------------------------------------------------------
//  <copyright file="ClientTest.cs" company="PlayFab Inc">
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

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Consul
{
    public class CoordinateEntry
    {
        public string Node { get; set; }
        public SerfCoordinate Coord { get; set; }
    }

    public class SerfCoordinate
    {
        public List<double> Vec { get; set; }
        public double Error { get; set; }
        public double Adjustment { get; set; }
        public double Height { get; set; }
        public SerfCoordinate()
        {
            Vec = new List<double>();
        }
    }

    // May want to rework this as Dictionary<string,List<CoordinateEntry>>
    public class CoordinateDatacenterMap
    {
        public string Datacenter { get; set; }
        public List<CoordinateEntry> Coordinates { get; set; }
        public CoordinateDatacenterMap()
        {
            Coordinates = new List<CoordinateEntry>();
        }
    }

    public class Coordinate : ICoordinateEndpoint
    {
        private readonly ConsulClient _client;

        internal Coordinate(ConsulClient c)
        {
            _client = c;
        }

        /// <summary>
        /// Datacenters is used to return the coordinates of all the servers in the WAN pool.
        /// </summary>
        /// <returns>A query result containing a map of datacenters, each with a list of coordinates of all the servers in the WAN pool</returns>
        public async Task<QueryResult<CoordinateDatacenterMap[]>> Datacenters()
        {
            return await _client.Get<CoordinateDatacenterMap[]>(string.Format("/v1/coordinate/datacenters")).Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// Nodes is used to return the coordinates of all the nodes in the LAN pool.
        /// </summary>
        /// <returns>A query result containing coordinates of all the nodes in the LAN pool</returns>
        public async Task<QueryResult<CoordinateEntry[]>> Nodes()
        {
            return await Nodes(QueryOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Nodes is used to return the coordinates of all the nodes in the LAN pool.
        /// </summary>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing coordinates of all the nodes in the LAN pool</returns>
        public async Task<QueryResult<CoordinateEntry[]>> Nodes(QueryOptions q)
        {
            return await _client.Get<CoordinateEntry[]>(string.Format("/v1/coordinate/nodes"), q).Execute().ConfigureAwait(false);
        }
    }

    public partial class ConsulClient : IConsulClient
    {
        private Coordinate _coordinate;

        /// <summary>
        /// Session returns a handle to the session endpoints
        /// </summary>
        public ICoordinateEndpoint Coordinate
        {
            get
            {
                if (_coordinate == null)
                {
                    lock (_lock)
                    {
                        if (_coordinate == null)
                        {
                            _coordinate = new Coordinate(this);
                        }
                    }
                }
                return _coordinate;
            }
        }
    }
}
