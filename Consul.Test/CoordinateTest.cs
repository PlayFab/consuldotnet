using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Consul.Test
{
    [TestClass]
    public class CoordinateTest
    {
        [TestMethod]
        public void Coordinate_Datacenters()
        {
            var client = new Client();

            var info = client.Agent.Self();

            if (!info.Response.ContainsKey("Coord"))
            {
                Assert.Inconclusive("This version of Consul does not support the coordinate API");
            }

            var datacenters = client.Coordinate.Datacenters();

            Assert.IsNotNull(datacenters.Response);
            Assert.IsTrue(datacenters.Response.Length > 0);
        }

        [TestMethod]
        public void Coordinate_Nodes()
        {
            var client = new Client();

            var info = client.Agent.Self();

            if (!info.Response.ContainsKey("Coord"))
            {
                Assert.Inconclusive("This version of Consul does not support the coordinate API");
            }

            var nodes = client.Coordinate.Nodes();

            // There's not a good way to populate coordinates without
            // waiting for them to calculate and update, so the best
            // we can do is call the endpoint and make sure we don't
            // get an error. - from offical API.
            Assert.IsNotNull(nodes);
        }
    }
}
