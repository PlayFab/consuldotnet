using System;
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    public class CoordinateTest : IDisposable
    {
        AsyncReaderWriterLock.Releaser m_lock;
        public CoordinateTest()
        {
            m_lock = SelectiveParallel.Parallel().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            m_lock.Dispose();
        }
        [Fact]
        public async Task Coordinate_Datacenters()
        {
            var client = new ConsulClient();

            var info = await client.Agent.Self();

            var datacenters = await client.Coordinate.Datacenters();

            Assert.NotNull(datacenters.Response);
            Assert.True(datacenters.Response.Length > 0);
        }

        [Fact]
        public async Task Coordinate_Nodes()
        {
            var client = new ConsulClient();

            var info = await client.Agent.Self();

            var nodes = await client.Coordinate.Nodes();

            // There's not a good way to populate coordinates without
            // waiting for them to calculate and update, so the best
            // we can do is call the endpoint and make sure we don't
            // get an error. - from offical API.
            Assert.NotNull(nodes);
        }
    }
}
