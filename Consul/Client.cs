using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Consul
{
    /// <summary>
    /// Represents errors that occur while sending data to or fetching data from the Consul agent.
    /// </summary>
    public class ConsulRequestException : Exception
    {
        public ConsulRequestException() { }
        public ConsulRequestException(string message) : base(message) { }
        public ConsulRequestException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Represents errors that occur during initalization of the Consul client's configuration.
    /// </summary>
    public class ConsulConfigurationException : Exception
    {
        public ConsulConfigurationException() { }
        public ConsulConfigurationException(string message) : base(message) { }
        public ConsulConfigurationException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Represents the configuration options for a Consul client.
    /// </summary>
    public class ConsulClientConfiguration
    {
        private NetworkCredential _httpauth;
        private bool _disableServerCertificateValidation;
        private X509Certificate2 _clientCertificate;

        internal event EventHandler Updated;

        internal static Lazy<bool> _clientCertSupport = new Lazy<bool>(() => { return Type.GetType("Mono.Runtime") == null; });

        internal bool ClientCertificateSupported { get { return _clientCertSupport.Value; } }

        internal bool DisableServerCertificateValidation
        {
            get
            {
                return _disableServerCertificateValidation;
            }
            set
            {
                _disableServerCertificateValidation = value;
                OnUpdated(new EventArgs());
            }
        }

        /// <summary>
        /// The Uri to connect to the Consul agent.
        /// </summary>
        public Uri Address { get; set; }

        /// <summary>
        /// Datacenter to provide with each request. If not provided, the default agent datacenter is used.
        /// </summary>
        public string Datacenter { get; set; }

        /// <summary>
        /// Credentials to use for access to the HTTP API.
        /// This is only needed if an authenticating service exists in front of Consul; Token is used for ACL authentication by Consul.
        /// </summary>
        public NetworkCredential HttpAuth
        {
            internal get
            {
                return _httpauth;
            }
            set
            {
                _httpauth = value;
                OnUpdated(new EventArgs());
            }
        }

        /// <summary>
        /// TLS Client Certificate used to secure a connection to a Consul agent. Not supported on Mono.
        /// This is only needed if an authenticating service exists in front of Consul; Token is used for ACL authentication by Consul. This is not the same as RPC Encryption with TLS certificates.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Setting this property will throw a PlatformNotSupportedException on Mono</exception>
#if __MonoCS__
        [Obsolete("Client Certificates are not implemented in Mono", true)]
#endif
        public X509Certificate2 ClientCertificate
        {
            internal get
            {
                return _clientCertificate;
            }
            set
            {
                if (!ClientCertificateSupported)
                {
                    throw new PlatformNotSupportedException("Client certificates are not supported on this platform");
                }
                _clientCertificate = value;
                OnUpdated(new EventArgs());
            }
        }

        /// <summary>
        /// Token is used to provide an ACL token which overrides the agent's default token. This ACL token is used for every request by
        /// clients created using this configuration.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// WaitTime limits how long a Watch will block. If not provided, the agent default values will be used.
        /// </summary>
        public TimeSpan? WaitTime { get; set; }

        /// <summary>
        /// Creates a new instance of a Consul client configuration.
        /// </summary>
        /// <exception cref="ConsulConfigurationException">An error that occured while building the configuration.</exception>
        public ConsulClientConfiguration()
        {
            UriBuilder consulAddress = new UriBuilder("http://127.0.0.1:8500");
            ConfigureFromEnvironment(consulAddress);

            Address = consulAddress.Uri;
        }

        /// <summary>
        /// Builds configuration based on environment variables.
        /// </summary>
        /// <exception cref="ConsulConfigurationException">An environment variable could not be parsed</exception>
        private void ConfigureFromEnvironment(UriBuilder consulAddress)
        {
            var envAddr = (Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR") ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(envAddr))
            {
                var addrParts = envAddr.Split(':');
                for (int i = 0; i < addrParts.Length; i++)
                {
                    addrParts[i] = addrParts[i].Trim();
                }
                if (!string.IsNullOrEmpty(addrParts[0]))
                {
                    consulAddress.Host = addrParts[0];
                }
                if (!string.IsNullOrEmpty(addrParts[1]))
                {
                    try
                    {
                        consulAddress.Port = ushort.Parse(addrParts[1]);
                    }
                    catch (Exception ex)
                    {
                        throw new ConsulConfigurationException("Failed parsing port from environment variable CONSUL_HTTP_ADDR", ex);
                    }
                }
            }

            var useSsl = (Environment.GetEnvironmentVariable("CONSUL_HTTP_SSL") ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(useSsl))
            {
                try
                {
                    if (useSsl == "1" || bool.Parse(useSsl))
                    {
                        consulAddress.Scheme = "https";
                    }
                }
                catch (Exception ex)
                {
                    throw new ConsulConfigurationException("Could not parse environment variable CONSUL_HTTP_SSL", ex);
                }
            }

            var verifySsl = (Environment.GetEnvironmentVariable("CONSUL_HTTP_SSL_VERIFY") ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(verifySsl))
            {
                try
                {
                    if (verifySsl == "0" || bool.Parse(verifySsl))
                    {
                        DisableServerCertificateValidation = true;
                    }
                }
                catch (Exception ex)
                {
                    throw new ConsulConfigurationException("Could not parse environment variable CONSUL_HTTP_SSL_VERIFY", ex);
                }
            }

            var auth = Environment.GetEnvironmentVariable("CONSUL_HTTP_AUTH");
            if (!string.IsNullOrEmpty(auth))
            {
                var credential = new NetworkCredential();
                if (auth.Contains(":"))
                {
                    var split = auth.Split(':');
                    credential.UserName = split[0];
                    credential.Password = split[1];
                }
                else
                {
                    credential.UserName = auth;
                }
                HttpAuth = credential;
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONSUL_HTTP_TOKEN")))
            {
                Token = Environment.GetEnvironmentVariable("CONSUL_HTTP_TOKEN");
            }
        }

        internal virtual void OnUpdated(EventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            EventHandler handler = Updated;

            // Event will be null if there are no subscribers
            if (handler != null)
            {
                // Use the () operator to raise the event.
                handler(this, e);
            }
        }
    }

    /// <summary>
    /// The consistency mode of a request.
    /// </summary>
    /// <see cref="http://www.consul.io/docs/agent/http.html"/>
    public enum ConsistencyMode
    {
        /// <summary>
        /// Default is strongly consistent in almost all cases. However, there is a small window in which a new leader may be elected during which the old leader may service stale values. The trade-off is fast reads but potentially stale values. The condition resulting in stale reads is hard to trigger, and most clients should not need to worry about this case. Also, note that this race condition only applies to reads, not writes.
        /// </summary>
        Default,

        /// <summary>
        /// Consistent forces the read to be fully consistent. This mode is strongly consistent without caveats. It requires that a leader verify with a quorum of peers that it is still leader. This introduces an additional round-trip to all server nodes. The trade-off is increased latency due to an extra round trip. Most clients should not use this unless they cannot tolerate a stale read.
        /// </summary>
        Consistent,

        /// <summary>
        /// Stale allows any Consul server (non-leader) to service a read. This mode allows any server to service the read regardless of whether it is the leader. This means reads can be arbitrarily stale; however, results are generally consistent to within 50 milliseconds of the leader. The trade-off is very fast and scalable reads with a higher likelihood of stale values. Since this mode allows reads without a leader, a cluster that is unavailable will still be able to respond to queries.
        /// </summary>
        Stale
    }

    /// <summary>
    /// QueryOptions are used to parameterize a query
    /// </summary>
    public class QueryOptions
    {
        public static readonly QueryOptions Default = new QueryOptions()
        {
            Consistency = ConsistencyMode.Default,
            Datacenter = string.Empty,
            Token = string.Empty,
            WaitIndex = 0
        };

        /// <summary>
        /// Providing a datacenter overwrites the DC provided by the Config
        /// </summary>
        public string Datacenter { get; set; }

        /// <summary>
        /// The consistency level required for the operation
        /// </summary>
        public ConsistencyMode Consistency { get; set; }

        /// <summary>
        /// WaitIndex is used to enable a blocking query. Waits until the timeout or the next index is reached
        /// </summary>
        public ulong WaitIndex { get; set; }

        /// <summary>
        /// WaitTime is used to bound the duration of a wait. Defaults to that of the Config, but can be overridden.
        /// </summary>
        public TimeSpan? WaitTime { get; set; }

        /// <summary>
        /// Token is used to provide a per-request ACL token which overrides the agent's default token.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Near is used to provide a node name that will sort the results
        /// in ascending order based on the estimated round trip time from
        /// that node. Setting this to "_agent" will use the agent's node
        /// for the sort.
        /// </summary>
        public string Near { get; set; }
    }

    /// <summary>
    /// WriteOptions are used to parameterize a write
    /// </summary>
    public class WriteOptions
    {
        public static readonly WriteOptions Default = new WriteOptions()
        {
            Datacenter = string.Empty,
            Token = string.Empty
        };

        /// <summary>
        /// Providing a datacenter overwrites the DC provided by the Config
        /// </summary>
        public string Datacenter { get; set; }

        /// <summary>
        /// Token is used to provide a per-request ACL token which overrides the agent's default token.
        /// </summary>
        public string Token { get; set; }
    }
    public abstract class ConsulResult
    {
        /// <summary>
        /// How long the request took
        /// </summary>
        public TimeSpan RequestTime { get; set; }

        /// <summary>
        /// Exposed so that the consumer can to check for a specific status code
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }
        public ConsulResult() { }
        public ConsulResult(ConsulResult other)
        {
            RequestTime = other.RequestTime;
            StatusCode = other.StatusCode;
        }
    }
    /// <summary>
    /// The result of a Consul API query
    /// </summary>
    public class QueryResult : ConsulResult
    {
        /// <summary>
        /// The index number when the query was serviced. This can be used as a WaitIndex to perform a blocking query
        /// </summary>
        public ulong LastIndex { get; set; }

        /// <summary>
        /// Time of last contact from the leader for the server servicing the request
        /// </summary>
        public TimeSpan LastContact { get; set; }

        /// <summary>
        /// Is there a known leader
        /// </summary>
        public bool KnownLeader { get; set; }

        public QueryResult() { }
        public QueryResult(QueryResult other) : base(other)
        {
            LastIndex = other.LastIndex;
            LastContact = other.LastContact;
            KnownLeader = other.KnownLeader;
        }
    }

    /// <summary>
    /// The result of a Consul API query
    /// </summary>
    /// <typeparam name="T">Must be able to be deserialized from JSON</typeparam>
    public class QueryResult<T> : QueryResult
    {
        /// <summary>
        /// The result of the query
        /// </summary>
        public T Response { get; set; }
        public QueryResult() { }
        public QueryResult(QueryResult other) : base(other) { }
        public QueryResult(QueryResult other, T value) : base(other)
        {
            Response = value;
        }
    }

    /// <summary>
    /// The result of a Consul API write
    /// </summary>
    public class WriteResult : ConsulResult
    {
        public WriteResult() { }
        public WriteResult(WriteResult other) : base(other) { }
    }
    /// <summary>
    /// The result of a Consul API write
    /// </summary>
    /// <typeparam name="T">Must be able to be deserialized from JSON. Some writes return nothing, in which case this should be an empty Object</typeparam>
    public class WriteResult<T> : WriteResult
    {
        /// <summary>
        /// The result of the write
        /// </summary>
        public T Response { get; set; }
        public WriteResult() { }
        public WriteResult(WriteResult other) : base(other) { }
        public WriteResult(WriteResult other, T value) : base(other)
        {
            Response = value;
        }
    }

    /// <summary>
    /// Represents a persistant connection to a Consul agent. Instances of this class should be created rarely and reused often.
    /// </summary>
    public partial class ConsulClient : IDisposable
    {
        private object _lock = new object();
        private bool skipClientDispose;
        internal HttpClient HttpClient { get; set; }
        internal HttpMessageHandler Handler;
        internal ConsulClientConfiguration Config { get; set; }

        internal readonly JsonSerializer serializer = new JsonSerializer();

        /// <summary>
        /// Initializes a new Consul client with a default configuration.
        /// </summary>
        public ConsulClient() : this(new ConsulClientConfiguration()) { }

        /// <summary>
        /// Initializes a new Consul client with the configuration specified.
        /// </summary>
        /// <param name="config">A Consul client configuration</param>
        public ConsulClient(ConsulClientConfiguration config)
        {
            Config = config;
            config.Updated += HandleConfigUpdateEvent;
            Handler = new WebRequestHandler();
            ApplyConfig(config);
            HttpClient = new HttpClient(Handler);
            HttpClient.Timeout = TimeSpan.FromMinutes(15);
            HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpClient.DefaultRequestHeaders.Add("Keep-Alive", "true");
        }

        /// <summary>
        /// Initializes a new Consul client with the configuration specified and a custom HttpClient, which is useful for setting proxies/custom timeouts.
        /// The HttpClient must accept the "application/json" content type and the Timeout property should be set to at least 15 minutes to allow for blocking queries.
        /// </summary>
        /// <param name="config">A Consul client configuration</param>
        /// <param name="client">A custom HttpClient</param>
        public ConsulClient(ConsulClientConfiguration config, HttpClient client)
        {
            Config = config;
            HttpClient = client;
            skipClientDispose = true;
            if (!HttpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
            {
                throw new ArgumentException("HttpClient must accept the application/json content type", "client");
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Config.Updated -= HandleConfigUpdateEvent;
                    if (HttpClient != null && !skipClientDispose)
                    {
                        HttpClient.Dispose();
                    }
                    if (Handler != null)
                    {
                        Handler.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        //~ConsulClient()
        //{
        //    // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //    Dispose(false);
        //}

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void CheckDisposed()
        {
            if (disposedValue)
            {
                throw new ObjectDisposedException(typeof(ConsulClient).FullName.ToString());
            }
        }
        #endregion

        void HandleConfigUpdateEvent(object sender, EventArgs e)
        {
            ApplyConfig(sender as ConsulClientConfiguration);

        }
        void ApplyConfig(ConsulClientConfiguration config)
        {
            var handler = (Handler as WebRequestHandler);
            if (config.HttpAuth != null)
            {
                handler.Credentials = config.HttpAuth;
            }
#if !__MonoCS__
            if (config.ClientCertificateSupported)
            {
                if (config.ClientCertificate != null)
                {
                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                    handler.ClientCertificates.Add(config.ClientCertificate);
                }
                else
                {
                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                    handler.ClientCertificates.Clear();
                }
            }
#endif
            if (config.DisableServerCertificateValidation)
            {
                handler.ServerCertificateValidationCallback += (certSender, cert, chain, sslPolicyErrors) => true;
            }
            else
            {
                handler.ServerCertificateValidationCallback = null;
            }
        }

        internal GetRequest<T> Get<T>(string path, QueryOptions opts = null)
        {
            return new GetRequest<T>(this, path, opts ?? QueryOptions.Default);
        }

        internal DeleteRequest<T> Delete<T>(string path, WriteOptions opts = null)
        {
            return new DeleteRequest<T>(this, path, opts ?? WriteOptions.Default);
        }

        internal EmptyPutRequest<TOut> EmptyPut<TOut>(string path, WriteOptions opts = null)
        {
            return new EmptyPutRequest<TOut>(this, path, opts ?? WriteOptions.Default);
        }

        internal PutRequest<TIn> Put<TIn>(string path, TIn body, WriteOptions opts = null)
        {
            return new PutRequest<TIn>(this, path, body, opts ?? WriteOptions.Default);
        }

        internal SilentPutRequest Put(string path, WriteOptions opts = null)
        {
            return new SilentPutRequest(this, path, opts ?? WriteOptions.Default);
        }

        internal PutRequest<TIn, TOut> Put<TIn, TOut>(string path, TIn body, WriteOptions opts = null)
        {
            return new PutRequest<TIn, TOut>(this, path, body, opts ?? WriteOptions.Default);
        }

        internal PostRequest<TIn, TOut> Post<TIn, TOut>(string path, TIn body, WriteOptions opts = null)
        {
            return new PostRequest<TIn, TOut>(this, path, body, opts ?? WriteOptions.Default);
        }
    }

    public abstract class ConsulRequest
    {
        internal ConsulClient Client { get; set; }
        internal HttpMethod Method { get; set; }
        internal Dictionary<string, string> Params { get; set; }
        internal Stream ResponseStream { get; set; }
        internal string Endpoint { get; set; }

        protected Stopwatch timer = new Stopwatch();

        internal ConsulRequest(ConsulClient client, string url, HttpMethod method)
        {
            Client = client;
            Method = method;
            Endpoint = url;

            Params = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(client.Config.Datacenter))
            {
                Params["dc"] = client.Config.Datacenter;
            }
            if (client.Config.WaitTime.HasValue)
            {
                Params["wait"] = client.Config.WaitTime.Value.ToGoDuration();
            }
            if (!string.IsNullOrEmpty(client.Config.Token))
            {
                Params["token"] = client.Config.Token;
            }
        }

        protected abstract void ApplyOptions();

        protected Uri BuildConsulUri(string url, Dictionary<string, string> p)
        {
            var builder = new UriBuilder(Client.Config.Address);
            builder.Path = url;

            ApplyOptions();

            var queryParams = new List<string>(Params.Count / 2);
            foreach (var queryParam in Params)
            {
                if (!string.IsNullOrEmpty(queryParam.Value))
                {
                    queryParams.Add(string.Format("{0}={1}", Uri.EscapeDataString(queryParam.Key),
                        Uri.EscapeDataString(queryParam.Value)));
                }
                else
                {
                    queryParams.Add(string.Format("{0}", Uri.EscapeDataString(queryParam.Key)));
                }
            }

            builder.Query = string.Join("&", queryParams);
            return builder.Uri;
        }

        protected TOut Deserialize<TOut>(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                using (var jsonReader = new JsonTextReader(reader))
                {
                    return Client.serializer.Deserialize<TOut>(jsonReader);
                }
            }
        }

        protected byte[] Serialize(object value)
        {
            return System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));
        }
    }
    public class GetRequest<T> : ConsulRequest
    {
        public QueryOptions Options { get; set; }

        public GetRequest(ConsulClient client, string url, QueryOptions options = null) : base(client, url, HttpMethod.Get)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException(url);
            }
            Options = options ?? QueryOptions.Default;
        }

        public Task<QueryResult<T>> Execute() { return Execute(CancellationToken.None); }
        public async Task<QueryResult<T>> Execute(CancellationToken ct)
        {
            Client.CheckDisposed();
            timer.Start();
            var result = new QueryResult<T>();

            var message = new HttpRequestMessage(HttpMethod.Get, BuildConsulUri(Endpoint, Params));
            var response = await Client.HttpClient.SendAsync(message, ct).ConfigureAwait(false);

            ParseQueryHeaders(response, result);
            result.StatusCode = response.StatusCode;
            ResponseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
            {
                if (ResponseStream == null)
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}",
                        response.StatusCode));
                }
                using (var sr = new StreamReader(ResponseStream))
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}: {1}",
                        response.StatusCode, sr.ReadToEnd()));
                }
            }

            if (response.IsSuccessStatusCode)
            {
                result.Response = Deserialize<T>(ResponseStream);
            }

            result.RequestTime = timer.Elapsed;
            timer.Stop();

            return result;
        }

        protected override void ApplyOptions()
        {
            if (Options == QueryOptions.Default)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Options.Datacenter))
            {
                Params["dc"] = Options.Datacenter;
            }
            switch (Options.Consistency)
            {
                case ConsistencyMode.Consistent:
                    Params["consistent"] = string.Empty;
                    break;
                case ConsistencyMode.Stale:
                    Params["stale"] = string.Empty;
                    break;
                case ConsistencyMode.Default:
                    break;
            }
            if (Options.WaitIndex != 0)
            {
                Params["index"] = Options.WaitIndex.ToString();
            }
            if (Options.WaitTime.HasValue)
            {
                Params["wait"] = Options.WaitTime.Value.ToGoDuration();
            }
            if (!string.IsNullOrEmpty(Options.Token))
            {
                Params["token"] = Options.Token;
            }
            if (!string.IsNullOrEmpty(Options.Near))
            {
                Params["near"] = Options.Near;
            }
        }

        protected void ParseQueryHeaders(HttpResponseMessage res, QueryResult<T> meta)
        {
            var headers = res.Headers;

            if (headers.Contains("X-Consul-Index"))
            {
                try
                {
                    meta.LastIndex = ulong.Parse(headers.GetValues("X-Consul-Index").First());
                }
                catch (Exception ex)
                {
                    throw new ConsulRequestException("Failed to parse X-Consul-Index", ex);
                }
            }

            if (headers.Contains("X-Consul-LastContact"))
            {
                try
                {
                    meta.LastContact = TimeSpan.FromMilliseconds(ulong.Parse(headers.GetValues("X-Consul-LastContact").First()));
                }
                catch (Exception ex)
                {
                    throw new ConsulRequestException("Failed to parse X-Consul-LastContact", ex);
                }
            }

            if (headers.Contains("X-Consul-KnownLeader"))
            {
                try
                {
                    meta.KnownLeader = bool.Parse(headers.GetValues("X-Consul-KnownLeader").First());
                }
                catch (Exception ex)
                {
                    throw new ConsulRequestException("Failed to parse X-Consul-KnownLeader", ex);
                }
            }
        }
    }

    public class DeleteRequest<TOut> : ConsulRequest
    {
        public WriteOptions Options { get; set; }

        public DeleteRequest(ConsulClient client, string url, WriteOptions options = null) : base(client, url, HttpMethod.Delete)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException(url);
            }
            Options = options ?? WriteOptions.Default;
        }

        public Task<WriteResult<TOut>> Execute() { return Execute(CancellationToken.None); }
        public async Task<WriteResult<TOut>> Execute(CancellationToken ct)
        {
            Client.CheckDisposed();
            timer.Start();
            var result = new WriteResult<TOut>();

            var response = await Client.HttpClient.DeleteAsync(BuildConsulUri(Endpoint, Params), ct).ConfigureAwait(false);

            result.StatusCode = response.StatusCode;

            ResponseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
            {
                if (ResponseStream == null)
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}",
                        response.StatusCode));
                }
                using (var sr = new StreamReader(ResponseStream))
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}: {1}",
                        response.StatusCode, sr.ReadToEnd()));
                }
            }

            if (response.IsSuccessStatusCode)
            {
                result.Response = Deserialize<TOut>(ResponseStream);
            }

            result.RequestTime = timer.Elapsed;
            timer.Stop();

            return result;
        }

        protected override void ApplyOptions()
        {
            if (Options == WriteOptions.Default)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Options.Datacenter))
            {
                Params["dc"] = Options.Datacenter;
            }
            if (!string.IsNullOrEmpty(Options.Token))
            {
                Params["token"] = Options.Token;
            }
        }
    }

    public class EmptyPutRequest<TOut> : ConsulRequest
    {
        public WriteOptions Options { get; set; }

        public EmptyPutRequest(ConsulClient client, string url, WriteOptions options = null) : base(client, url, HttpMethod.Put)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException(url);
            }
            Options = options ?? WriteOptions.Default;
        }

        public Task<WriteResult<TOut>> Execute() { return Execute(CancellationToken.None); }
        public async Task<WriteResult<TOut>> Execute(CancellationToken ct)
        {
            Client.CheckDisposed();
            timer.Start();
            var result = new WriteResult<TOut>();

            var response = await Client.HttpClient.PutAsync(BuildConsulUri(Endpoint, Params), null, ct).ConfigureAwait(false);

            result.StatusCode = response.StatusCode;

            ResponseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
            {
                if (ResponseStream == null)
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}",
                        response.StatusCode));
                }
                using (var sr = new StreamReader(ResponseStream))
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}: {1}",
                        response.StatusCode, sr.ReadToEnd()));
                }
            }

            if (response.IsSuccessStatusCode)
            {
                result.Response = Deserialize<TOut>(ResponseStream);
            }

            result.RequestTime = timer.Elapsed;
            timer.Stop();

            return result;
        }

        protected override void ApplyOptions()
        {
            if (Options == WriteOptions.Default)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Options.Datacenter))
            {
                Params["dc"] = Options.Datacenter;
            }
            if (!string.IsNullOrEmpty(Options.Token))
            {
                Params["token"] = Options.Token;
            }
        }
    }

    public class SilentPutRequest : ConsulRequest
    {
        public WriteOptions Options { get; set; }

        public SilentPutRequest(ConsulClient client, string url, WriteOptions options = null) : base(client, url, HttpMethod.Put)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException(url);
            }
            Options = options ?? WriteOptions.Default;
        }

        public Task<WriteResult> Execute() { return Execute(CancellationToken.None); }
        public async Task<WriteResult> Execute(CancellationToken ct)
        {
            Client.CheckDisposed();
            timer.Start();
            var result = new WriteResult();

            var response = await Client.HttpClient.PutAsync(BuildConsulUri(Endpoint, Params), null, ct).ConfigureAwait(false);

            result.StatusCode = response.StatusCode;

            ResponseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
            {
                if (ResponseStream == null)
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}",
                        response.StatusCode));
                }
                using (var sr = new StreamReader(ResponseStream))
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}: {1}",
                        response.StatusCode, sr.ReadToEnd()));
                }
            }

            result.RequestTime = timer.Elapsed;
            timer.Stop();

            return result;
        }

        protected override void ApplyOptions()
        {
            if (Options == WriteOptions.Default)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Options.Datacenter))
            {
                Params["dc"] = Options.Datacenter;
            }
            if (!string.IsNullOrEmpty(Options.Token))
            {
                Params["token"] = Options.Token;
            }
        }
    }

    public class PutRequest<TIn> : ConsulRequest
    {
        public WriteOptions Options { get; set; }
        private TIn _body;

        private readonly bool UseRawRequestBody = typeof(TIn) == typeof(byte[]);

        public PutRequest(ConsulClient client, string url, TIn body, WriteOptions options = null) : base(client, url, HttpMethod.Put)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException(url);
            }
            _body = body;
            Options = options ?? WriteOptions.Default;
        }

        public Task<WriteResult> Execute() { return Execute(CancellationToken.None); }
        public async Task<WriteResult> Execute(CancellationToken ct)
        {
            Client.CheckDisposed();
            timer.Start();
            var result = new WriteResult();

            HttpContent content;

            if (UseRawRequestBody)
            {
                content = new ByteArrayContent((_body as byte[]) ?? new byte[0]);
            }
            else
            {
                content = new ByteArrayContent(Serialize(_body));
            }

            var response = await Client.HttpClient.PutAsync(BuildConsulUri(Endpoint, Params), content, ct).ConfigureAwait(false);

            result.StatusCode = response.StatusCode;

            ResponseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
            {
                if (ResponseStream == null)
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}",
                        response.StatusCode));
                }
                using (var sr = new StreamReader(ResponseStream))
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}: {1}",
                        response.StatusCode, sr.ReadToEnd()));
                }
            }

            result.RequestTime = timer.Elapsed;
            timer.Stop();

            return result;
        }

        protected override void ApplyOptions()
        {
            if (Options == WriteOptions.Default)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Options.Datacenter))
            {
                Params["dc"] = Options.Datacenter;
            }
            if (!string.IsNullOrEmpty(Options.Token))
            {
                Params["token"] = Options.Token;
            }
        }
    }

    public class PutRequest<TIn, TOut> : ConsulRequest
    {
        public WriteOptions Options { get; set; }
        private TIn _body;

        private readonly bool UseRawRequestBody = typeof(TIn) == typeof(byte[]);

        public PutRequest(ConsulClient client, string url, TIn body, WriteOptions options = null) : base(client, url, HttpMethod.Put)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException(url);
            }
            _body = body;
            Options = options ?? WriteOptions.Default;
        }

        public Task<WriteResult<TOut>> Execute() { return Execute(CancellationToken.None); }
        public async Task<WriteResult<TOut>> Execute(CancellationToken ct)
        {
            Client.CheckDisposed();
            timer.Start();
            var result = new WriteResult<TOut>();

            HttpContent content = null;

            if (UseRawRequestBody)
            {
                if (_body != null)
                {
                    content = new ByteArrayContent((_body as byte[]) ?? new byte[0]);
                }
                // If body is null and should be a byte array, then just don't send anything.
            }
            else
            {
                content = new ByteArrayContent(Serialize(_body));
            }

            var response = await Client.HttpClient.PutAsync(BuildConsulUri(Endpoint, Params), content, ct).ConfigureAwait(false);

            result.StatusCode = response.StatusCode;

            ResponseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
            {
                if (ResponseStream == null)
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}",
                        response.StatusCode));
                }
                using (var sr = new StreamReader(ResponseStream))
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}: {1}",
                        response.StatusCode, sr.ReadToEnd()));
                }
            }

            if (response.IsSuccessStatusCode)
            {
                result.Response = Deserialize<TOut>(ResponseStream);
            }

            result.RequestTime = timer.Elapsed;
            timer.Stop();

            return result;
        }

        protected override void ApplyOptions()
        {
            if (Options == WriteOptions.Default)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Options.Datacenter))
            {
                Params["dc"] = Options.Datacenter;
            }
            if (!string.IsNullOrEmpty(Options.Token))
            {
                Params["token"] = Options.Token;
            }
        }
    }


    public class PostRequest<TIn, TOut> : ConsulRequest
    {
        public WriteOptions Options { get; set; }
        private TIn _body;

        private readonly bool UseRawRequestBody = typeof(TIn) == typeof(byte[]);

        public PostRequest(ConsulClient client, string url, TIn body, WriteOptions options = null) : base(client, url, HttpMethod.Post)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException(url);
            }
            _body = body;
            Options = options ?? WriteOptions.Default;
        }

        public Task<WriteResult<TOut>> Execute() { return Execute(CancellationToken.None); }
        public async Task<WriteResult<TOut>> Execute(CancellationToken ct)
        {
            Client.CheckDisposed();
            timer.Start();
            var result = new WriteResult<TOut>();

            HttpContent content = null;

            if (UseRawRequestBody)
            {
                if (_body != null)
                {
                    content = new ByteArrayContent((_body as byte[]) ?? new byte[0]);
                }
                // If body is null and should be a byte array, then just don't send anything.
            }
            else
            {
                content = new ByteArrayContent(Serialize(_body));
            }

            var response = await Client.HttpClient.PostAsync(BuildConsulUri(Endpoint, Params), content, ct).ConfigureAwait(false);

            result.StatusCode = response.StatusCode;

            ResponseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.NotFound && !response.IsSuccessStatusCode)
            {
                if (ResponseStream == null)
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}",
                        response.StatusCode));
                }
                using (var sr = new StreamReader(ResponseStream))
                {
                    throw new ConsulRequestException(string.Format("Unexpected response, status code {0}: {1}",
                        response.StatusCode, sr.ReadToEnd()));
                }
            }

            if (response.IsSuccessStatusCode)
            {
                result.Response = Deserialize<TOut>(ResponseStream);
            }

            result.RequestTime = timer.Elapsed;
            timer.Stop();

            return result;
        }

        protected override void ApplyOptions()
        {
            if (Options == WriteOptions.Default)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Options.Datacenter))
            {
                Params["dc"] = Options.Datacenter;
            }
            if (!string.IsNullOrEmpty(Options.Token))
            {
                Params["token"] = Options.Token;
            }
        }
    }
}