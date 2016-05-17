using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    public class ClientTest
    {
        [Fact]
        public void Client_DefaultConfig_env()
        {
            const string addr = "1.2.3.4:5678";
            const string token = "abcd1234";
            const string auth = "username:password";
            Environment.SetEnvironmentVariable("CONSUL_HTTP_ADDR", addr);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_TOKEN", token);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_AUTH", auth);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_SSL", "1");
            Environment.SetEnvironmentVariable("CONSUL_HTTP_SSL_VERIFY", "0");

            var config = new ConsulClientConfiguration();

            Assert.Equal(addr, string.Format("{0}:{1}", config.Address.Host, config.Address.Port));
            Assert.Equal(token, config.Token);
            Assert.NotNull(config.HttpAuth);
            Assert.Equal("username", config.HttpAuth.UserName);
            Assert.Equal("password", config.HttpAuth.Password);
            Assert.Equal("https", config.Address.Scheme);

            Environment.SetEnvironmentVariable("CONSUL_HTTP_ADDR", string.Empty);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_TOKEN", string.Empty);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_AUTH", string.Empty);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_SSL", string.Empty);
            Environment.SetEnvironmentVariable("CONSUL_HTTP_SSL_VERIFY", string.Empty);
            ServicePointManager.ServerCertificateValidationCallback = null;

            var client = new ConsulClient(config);

            Assert.True((client.Handler as WebRequestHandler).ServerCertificateValidationCallback(null, null, null,
                SslPolicyErrors.RemoteCertificateChainErrors));

            Assert.NotNull(client);
        }

        [Fact]
        public async Task Client_SetQueryOptions()
        {
            var client = new ConsulClient();
            var opts = new QueryOptions()
            {
                Datacenter = "foo",
                Consistency = ConsistencyMode.Consistent,
                WaitIndex = 1000,
                WaitTime = new TimeSpan(0, 0, 100),
                Token = "12345"
            };
            var request = client.Get<KVPair>("/v1/kv/foo", opts);

            await Assert.ThrowsAsync<ConsulRequestException>(async () => await request.Execute());

            Assert.Equal("foo", request.Params["dc"]);
            Assert.True(request.Params.ContainsKey("consistent"));
            Assert.Equal("1000", request.Params["index"]);
            Assert.Equal("1m40s", request.Params["wait"]);
            Assert.Equal("12345", request.Params["token"]);
        }

        [Fact]
        public async Task Client_SetClientOptions()
        {
            var config = new ConsulClientConfiguration()
            {
                Datacenter = "foo",
                WaitTime = new TimeSpan(0, 0, 100),
                Token = "12345"
            };
            var client = new ConsulClient(config);
            var request = client.Get<KVPair>("/v1/kv/foo");

            await Assert.ThrowsAsync<ConsulRequestException>(async () => await request.Execute());

            Assert.Equal("foo", request.Params["dc"]);
            Assert.Equal("1m40s", request.Params["wait"]);
            Assert.Equal("12345", request.Params["token"]);
        }
        [Fact]
        public async Task Client_SetWriteOptions()
        {
            var client = new ConsulClient();

            var opts = new WriteOptions()
            {
                Datacenter = "foo",
                Token = "12345"
            };

            var request = client.Put("/v1/kv/foo", new KVPair("kv/foo"), opts);

            await Assert.ThrowsAsync<ConsulRequestException>(async () => await request.Execute());

            Assert.Equal("foo", request.Params["dc"]);
            Assert.Equal("12345", request.Params["token"]);
        }

        [Fact]
        public async Task Client_CustomHttpClient()
        {
            using (var hc = new HttpClient())
            {
                hc.Timeout = TimeSpan.FromDays(10);
                hc.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using (var client = new ConsulClient(new ConsulClientConfiguration(), hc))
                {
                    await client.KV.Put(new KVPair("customhttpclient") { Value = System.Text.Encoding.UTF8.GetBytes("hello world") });
                    Assert.Equal(TimeSpan.FromDays(10), client.HttpClient.Timeout);
                    Assert.True(client.HttpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")));
                }
                Assert.Equal("hello world", await (await hc.GetAsync("http://localhost:8500/v1/kv/customhttpclient?raw")).Content.ReadAsStringAsync());
            }
        }

        [Fact]
        public async Task Client_DisposeBehavior()
        {
            var client = new ConsulClient();
            var opts = new WriteOptions()
            {
                Datacenter = "foo",
                Token = "12345"
            };

            client.Dispose();

            var request = client.Put("/v1/kv/foo", new KVPair("kv/foo"), opts);

            await Assert.ThrowsAsync<ObjectDisposedException>(() => request.Execute());
        }

        [Fact]
        public async Task Client_ReuseAndUpdateConfig()
        {
            var config = new ConsulClientConfiguration();

            using (var client = new ConsulClient(config))
            {
                config.DisableServerCertificateValidation = true;
                await client.KV.Put(new KVPair("kv/reuseconfig") { Flags = 1000 });
                Assert.Equal<ulong>(1000, (await client.KV.Get("kv/reuseconfig")).Response.Flags);
                Assert.True((client.Handler as WebRequestHandler).ServerCertificateValidationCallback(null, null, null,
                    SslPolicyErrors.RemoteCertificateChainErrors));
            }

            using (var client = new ConsulClient(config))
            {
                config.DisableServerCertificateValidation = false;
                await client.KV.Put(new KVPair("kv/reuseconfig") { Flags = 2000 });
                Assert.Equal<ulong>(2000, (await client.KV.Get("kv/reuseconfig")).Response.Flags);
                Assert.Null((client.Handler as WebRequestHandler).ServerCertificateValidationCallback);
            }

            using (var client = new ConsulClient(config))
            {
                await client.KV.Delete("kv/reuseconfig");
            }
        }
    }
}