using System;
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    public class CoordinateTest
    {
        [SkippableFact]
        public async Task Coordinate_Datacenters()
        {
            var client = new ConsulClient();

            var info = await client.Agent.Self();

            Skip.IfNot(info.Response.ContainsKey("Coord"), "This version of Consul does not support the coordinate API");

            var datacenters = await client.Coordinate.Datacenters();

            Assert.NotNull(datacenters.Response);
            Assert.True(datacenters.Response.Length > 0);
        }

        [SkippableFact]
        public async Task Coordinate_Nodes()
        {
            var client = new ConsulClient();

            var info = await client.Agent.Self();

            Skip.If(!info.Response.ContainsKey("Coord"), "This version of Consul does not support the coordinate API");

            var nodes = await client.Coordinate.Nodes();

            // There's not a good way to populate coordinates without
            // waiting for them to calculate and update, so the best
            // we can do is call the endpoint and make sure we don't
            // get an error. - from offical API.
            Assert.NotNull(nodes);
        }
    }
}
