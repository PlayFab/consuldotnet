using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    public class PreparedQueryTest
    {
        [Fact]
        public async Task PreparedQuery_Test()
        {
            var client = new ConsulClient();

            var registration = new CatalogRegistration()
            {
                Datacenter = "dc1",
                Node = "foobar",
                Address = "192.168.10.10",
                Service = new AgentService()
                {
                    ID = "redis1",
                    Service = "redis",
                    Tags = new[] { "master", "v1" },
                    Port = 8000
                }
            };

            await client.Catalog.Register(registration);

            Assert.NotNull((await client.Catalog.Node("foobar")).Response);

            var mgmtquerytoken = new QueryOptions() { Token = "yep" };

            var def = new PreparedQueryDefinition { Service = new ServiceQuery() { Service = "redis" } };

            var id = (await client.PreparedQuery.Create(def)).Response;
            def.ID = id;

            var defs = (await client.PreparedQuery.Get(id)).Response;

            Assert.NotNull(defs);
            Assert.True(defs.Length == 1);
            Assert.Equal(def.Service.Service, defs[0].Service.Service);

            defs = null;
            defs = (await client.PreparedQuery.List(mgmtquerytoken)).Response;

            Assert.NotNull(defs);
            Assert.True(defs.Length == 1);
            Assert.Equal(def.Service.Service, defs[0].Service.Service);

            def.Name = "my-query";

            await client.PreparedQuery.Update(def);

            defs = null;
            defs = (await client.PreparedQuery.Get(id)).Response;

            Assert.NotNull(defs);
            Assert.True(defs.Length == 1);
            Assert.Equal(def.Name, defs[0].Name);

            var results = (await client.PreparedQuery.Execute(id)).Response;

            Assert.NotNull(results);
            Assert.True(results.Nodes.Length == 1);
            Assert.Equal(results.Nodes[0].Node.Name, "foobar");

            results = null;
            results = (await client.PreparedQuery.Execute("my-query")).Response;

            Assert.NotNull(results);
            Assert.True(results.Nodes.Length == 1);
            Assert.Equal(results.Nodes[0].Node.Name, "foobar");

            await client.PreparedQuery.Delete(id);

            defs = null;
            defs = (await client.PreparedQuery.List(mgmtquerytoken)).Response;

            Assert.True(defs.Length == 0);
        }
    }
}
