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
            m_lock = SelectiveParallel.Parallel().GetAwaiter().GetResult();
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

        [Fact]
        public async Task Operator_KeyringInstallListPutRemove()
        {
            const string oldKey = "d8wu8CSUrqgtjVsvcBPmhQ==";
            const string newKey = "qxycTi/SsePj/TZzCBmNXw==";

            using (var c = new ConsulClient())
            {
                await c.Operator.KeyringInstall(oldKey);
                await c.Operator.KeyringUse(oldKey);
                await c.Operator.KeyringInstall(newKey);

                var listResponses = await c.Operator.KeyringList();

                Assert.Equal(2, listResponses.Response.Length);

                foreach (var response in listResponses.Response)
                {
                    Assert.Equal(2, response.Keys.Count);
                    Assert.True(response.Keys.ContainsKey(oldKey));
                    Assert.True(response.Keys.ContainsKey(newKey));
                }

                await c.Operator.KeyringUse(newKey);

                await c.Operator.KeyringRemove(oldKey);

                listResponses = await c.Operator.KeyringList();
                Assert.Equal(2, listResponses.Response.Length);

                foreach (var response in listResponses.Response)
                {
                    Assert.Equal(1, response.Keys.Count);
                    Assert.False(response.Keys.ContainsKey(oldKey));
                    Assert.True(response.Keys.ContainsKey(newKey));
                }
            }
        }
    }
}
