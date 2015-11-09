using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Consul.Test
{
    [TestClass]
    public class CoordinateTest
    {
        [TestMethod]
        public void TestCoordinate_Datacenters()
        {
            var client = new Client();

            var datacenters = client.Coordinate.Datacenters();

            Assert.IsNotNull(datacenters.Response);
            Assert.IsTrue(datacenters.Response.Length > 0);
        }

        [TestMethod]
        public void TestCoordinate_Nodes()
        {
            var client = new Client();

            var nodes = client.Coordinate.Nodes();

            Assert.IsNotNull(nodes.Response);
            Assert.IsTrue(nodes.Response.Length > 0);
        }
    }
}
