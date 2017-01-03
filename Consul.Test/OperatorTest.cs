using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    public class OperatorTest : IDisposable
    {
        AsyncReaderWriterLock.Releaser m_lock;
        public OperatorTest()
        {
            m_lock = AsyncHelpers.RunSync(() => SelectiveParallel.Parallel());
        }

        public void Dispose()
        {
            m_lock.Dispose();
        }
    
        [Fact]
        public async Task Operator_RaftGetConfiguration()
        {
            using (var client = new ConsulClient())
            {
                var servers = await client.Operator.RaftGetConfiguration();

                Assert.Equal(1, servers.Response.Servers.Count);
                Assert.True(servers.Response.Servers[0].Leader);
                Assert.True(servers.Response.Servers[0].Voter);
            }
        }

        [Fact]
        public async Task Operator_RaftRemovePeerByAddress()
        {
            using (var client = new ConsulClient())
            {
                try
                {
                    await client.Operator.RaftRemovePeerByAddress("nope");
                }
                catch (ConsulRequestException e)
                {
                    Assert.Contains("address \"nope\" was not found in the Raft configuration", e.Message);
                }
            }
        }
    }
}
