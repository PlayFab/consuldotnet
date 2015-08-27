// -----------------------------------------------------------------------
//  <copyright file="Client.cs" company="PlayFab Inc">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Consul
{
    [Serializable]
    public class ConsulRequestException : Exception
    {
        public ConsulRequestException() { }
        public ConsulRequestException(string message) : base(message) { }
        public ConsulRequestException(string message, Exception inner) : base(message, inner) { }
        protected ConsulRequestException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
    public class ConsulClientConfiguration
    {
        /// <summary>
        /// Address is the address of the Consul server
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Scheme is the URI scheme for the Consul server
        /// </summary>
        public string Scheme { get; set; }

        /// <summary>
        /// Datacenter to use. If not provided, the default agent datacenter is used.
        /// </summary>
        public string Datacenter { get; set; }

        /// <summary>
        /// HttpAuth is the auth info to use for http access.
        /// </summary>
        public NetworkCredential HttpAuth { get; set; }

        /// <summary>
        /// WaitTime limits how long a Watch will block. If not provided, the agent default values will be used.
        /// </summary>
        public TimeSpan WaitTime { get; set; }

        /// <summary>
        /// Token is used to provide a per-request ACL token which overrides the agent's default token.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Constructs a default configuration for the client
        /// </summary>
        public ConsulClientConfiguration()
        {
            Address = "127.0.0.1:8500";
            Scheme = "http";

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR")))
            {
                Address = Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR");
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONSUL_HTTP_TOKEN")))
            {
                Token = Environment.GetEnvironmentVariable("CONSUL_HTTP_TOKEN");
            }

            var consulHttpAuth = Environment.GetEnvironmentVariable("CONSUL_HTTP_AUTH");
            if (!string.IsNullOrEmpty(consulHttpAuth))
            {
                HttpAuth = new NetworkCredential();
                if (consulHttpAuth.Contains(":"))
                {
                    var split = consulHttpAuth.Split(':');
                    HttpAuth.UserName = split[0];
                    HttpAuth.Password = split[1];
                }
                else
                {
                    HttpAuth.UserName = consulHttpAuth;
                }
            }

            var consulHttpSsl = Environment.GetEnvironmentVariable("CONSUL_HTTP_SSL");
            if (!string.IsNullOrEmpty(consulHttpSsl))
            {
                try
                {
                    if (consulHttpSsl == "1" || bool.Parse(consulHttpSsl))
                    {
                        Scheme = "https";
                    }
                }
                catch (Exception)
                {
                    throw new ArgumentException("Could not parse environment CONSUL_HTTP_SSL");
                }
            }

            var consulHttpSslVerify = Environment.GetEnvironmentVariable("CONSUL_HTTP_SSL_VERIFY");
            if (!string.IsNullOrEmpty(consulHttpSslVerify))
            {
                try
                {
                    if (consulHttpSslVerify == "0" || bool.Parse(consulHttpSslVerify))
                    {
                        ServicePointManager.ServerCertificateValidationCallback +=
                            (sender, certificate, chain, errors) => true;
                    }
                }
                catch (Exception)
                {
                    throw new ArgumentException("Could not parse environment CONSUL_HTTP_SSL_VERIFY");
                }
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
            WaitIndex = 0,
            WaitTime = TimeSpan.Zero
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
        public TimeSpan WaitTime { get; set; }

        /// <summary>
        /// Token is used to provide a per-request ACL token which overrides the agent's default token.
        /// </summary>
        public string Token { get; set; }
    }

    /// <summary>
    /// WriteOptions are used to parameterize a write
    /// </summary>
    public class WriteOptions
    {
        public static readonly WriteOptions Empty = new WriteOptions()
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
        /// How long did the request take
        /// </summary>
        public TimeSpan RequestTime { get; set; }
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
        public T Response { get; internal set; }
    }

    /// <summary>
    /// The result of a Consul API write
    /// </summary>
    public class WriteResult : ConsulResult
    {
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
        public T Response { get; internal set; }
    }

    /// <summary>
    /// A query to the Consul service
    /// </summary>
    /// <typeparam name="T">Must be JSON deserializable. Some writes return nothing, in which case this should be an empty Object</typeparam>
    public class Query : Request
    {
        private QueryResult result;
        /// <summary>
        /// Annotate the request with additional query options
        /// </summary>
        public QueryOptions Options { get; set; }

        public Query(ConsulClientConfiguration config, HttpMethod method, string path, QueryOptions q)
            : base(config, method, path)
        {
            if (q == null)
            {
                throw new ArgumentNullException("q");
            }
            Options = q;
        }

        protected static void ParseQueryHeaders(WebResponse res, QueryResult meta)
        {
            var headers = res.Headers;

            if (!string.IsNullOrEmpty(headers["X-Consul-Index"]))
            {
                try
                {
                    meta.LastIndex = ulong.Parse(headers["X-Consul-Index"]);
                }
                catch (Exception ex)
                {
                    throw new ConsulRequestException("Failed to parse X-Consul-Index", ex);
                }
            }

            if (!string.IsNullOrEmpty(headers["X-Consul-LastContact"]))
            {
                try
                {
                    meta.LastContact = TimeSpan.FromMilliseconds(ulong.Parse(headers["X-Consul-LastContact"]));
                }
                catch (Exception ex)
                {
                    throw new ConsulRequestException("Failed to parse X-Consul-LastContact", ex);
                }
            }

            if (!string.IsNullOrEmpty(headers["X-Consul-KnownLeader"]))
            {
                try
                {
                    meta.KnownLeader = bool.Parse(headers["X-Consul-KnownLeader"]);
                }
                catch (Exception ex)
                {
                    throw new ConsulRequestException("Failed to parse X-Consul-KnownLeader", ex);
                }
            }
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
            if (Options.WaitTime != TimeSpan.Zero)
            {
                Params["wait"] = Options.WaitTime.ToGoDuration();
            }
            if (!string.IsNullOrEmpty(Options.Token))
            {
                Params["token"] = Options.Token;
            }
        }

        protected HttpWebResponse ExecuteInternal<TResult>(TResult result, CancellationToken ct) where TResult : QueryResult
        {
            var req = BuildWebRequest();

            timer.Start();

            try
            {
                using (ct.Register(() => req.Abort()))
                {
                    var res = (HttpWebResponse)(req.GetResponse());
                    ReadResponse(res.GetResponseStream(), ref result);
                    ParseQueryHeaders(res, result);
                    return res;
                }
            }
            catch (WebException ex)
            {
                ct.ThrowIfCancellationRequested();
                if (ex.Response == null)
                {
                    throw new ConsulRequestException("No response from server");
                }
                var res = (HttpWebResponse)ex.Response;
                if (res.StatusCode == HttpStatusCode.NotFound)
                {
                    ParseQueryHeaders(res, result);
                    // Let consumers handle not found since it usually means there was just an empty result from Consul.
                    throw;
                }
                var stream = res.GetResponseStream();
                if (stream == null)
                {
                    throw new ConsulRequestException(string.Format("Unexpected response code {0}",
                        res.StatusCode));
                }
                using (var sr = new StreamReader(stream))
                {
                    throw new ConsulRequestException(string.Format("Unexpected response code {0}: {1}",
                        res.StatusCode, sr.ReadToEnd()));
                }
            }
            finally
            {
                result.RequestTime = timer.Elapsed;
                timer.Stop();
            }
        }

        public QueryResult Execute()
        {
            return Execute(CancellationToken.None);
        }

        public QueryResult Execute(CancellationToken ct)
        {
            result = new QueryResult();
            try
            {
                var response = ExecuteInternal(result, ct);
                return result;
            }
            catch (WebException ex)
            {
                if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                {
                    return result;
                }
                throw;
            }
        }

    }    /// <summary>
    /// A query to the Consul service
    /// </summary>
    /// <typeparam name="T">Must be JSON deserializable. Some writes return nothing, in which case this should be an empty Object</typeparam>
    public class Query<T> : Query
    {
        public Query(ConsulClientConfiguration config, HttpMethod method, string path, QueryOptions q)
            : base(config, method, path, q) { }
        public new QueryResult<T> Execute()
        {
            return Execute(CancellationToken.None);
        }
        protected override void ReadResponse<TResult>(Stream responseStream, ref TResult result)
        {
            (result as QueryResult<T>).Response = DecodeFromStream<T>(responseStream);
        }

        public new QueryResult<T> Execute(CancellationToken ct)
        {
            var result = new QueryResult<T>();
            try
            {
                var response = ExecuteInternal(result, ct);
                return result;
            }
            catch (WebException ex)
            {
                if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                {
                    return result;
                }
                throw;
            }
        }
    }

    /// <summary>
    /// A write to the Consul service
    /// </summary>
    public class Write : Request
    {
        /// <summary>
        /// Annotate the request with additional write options
        /// </summary>
        public WriteOptions Options { get; set; }

        protected override void ApplyOptions()
        {
            if (Options == WriteOptions.Empty)
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
        public Write(ConsulClientConfiguration config, HttpMethod method, string path, WriteOptions q)
            : base(config, method, path)
        {
            if (q == null)
            {
                throw new ArgumentNullException("q");
            }
            Options = q;
        }
        protected WriteResult ExecuteInternal(WriteResult result)
        {
            var req = BuildWebRequest();
            HttpWebResponse res;
            timer.Start();
            WriteData(req.GetRequestStream());
            try
            {
                res = (HttpWebResponse)req.GetResponse();
                if (res.StatusCode == HttpStatusCode.OK)
                {
                    var stream = res.GetResponseStream();
                    if (stream != null)
                        ReadResponse(stream, ref result);
                }
            }
            catch (WebException ex)
            {
                res = (HttpWebResponse)ex.Response;
                if (res.StatusCode == HttpStatusCode.NotFound)
                {
                    // Let consumers handle not found since it usually means there was just an empty result from Consul.
                    throw;
                }
                var stream = res.GetResponseStream();
                if (stream == null)
                {
                    throw new ConsulRequestException(string.Format("Unexpected response code {0}",
                        res.StatusCode));
                }
                using (var sr = new StreamReader(stream))
                {
                    throw new ConsulRequestException(string.Format("Unexpected response code {0}: {1}",
                        res.StatusCode, sr.ReadToEnd()));
                }
            }
            finally
            {
                result.RequestTime = timer.Elapsed;
                timer.Stop();
            }
            return result;
        }
        public virtual WriteResult Execute()
        {
            var result = new WriteResult();
            ExecuteInternal(result);
            return result;
        }
    }

    /// <summary>
    /// A write to the Consul service
    /// </summary>
    /// <typeparam name="TIn">The type encoded and sent to Consul. Must be JSON serializable, or a byte[]. If the type is byte[], then it is not JSON encoded before transmission</typeparam>
    public class Write<TIn> : Write
    {
        /// <summary>
        /// The data to write to Consul. Must be serializable to JSON.
        /// </summary>
        public TIn RequestBody { get; set; }

        internal bool UseRawRequestBody
        {
            get { return GetType().GenericTypeArguments[0] == typeof(byte[]); }
        }

        public Write(ConsulClientConfiguration config, HttpMethod method, string path, TIn body, WriteOptions q)
            : base(config, method, path, q)
        {
            RequestBody = body;
        }
        public Write(ConsulClientConfiguration config, HttpMethod method, string path, WriteOptions q)
            : base(config, method, path, q)
        {
        }
        protected override void WriteData(Stream requestStream)
        {
            if (UseRawRequestBody)
            {
                WriteRawRequestBody(RequestBody, requestStream);
            }
            else
            {
                EncodeToStream(RequestBody, requestStream);
            }
        }
        /// Must be object because T1 cannot be converted to byte[]
        protected static void WriteRawRequestBody(object body, Stream stream)
        {
            if (body == null)
            {
                return;
            }
            var buf = (byte[])body;
            using (stream)
            {
                stream.Write(buf, 0, buf.Length);
            }
        }
    }
    /// <summary>
    /// A write to the Consul service
    /// </summary>
    /// <typeparam name="TIn">The type encoded and sent to Consul. Must be JSON serializable, or a byte[]. If the type is byte[], then it is not JSON encoded before transmission</typeparam>
    public class Modify<TOut> : Write
    {
        public Modify(ConsulClientConfiguration config, HttpMethod method, string path, WriteOptions q)
            : base(config, method, path, q) { }

        protected override void ReadResponse<TResult>(Stream responseStream, ref TResult result)
        {
            (result as WriteResult<TOut>).Response = DecodeFromStream<TOut>(responseStream);
        }

        public new WriteResult<TOut> Execute()
        {
            var result = new WriteResult<TOut>();
            ExecuteInternal(result);
            return result;
        }
    }

    /// <summary>
    /// A write to the Consul service
    /// </summary>
    /// <typeparam name="TIn">The type encoded and sent to Consul. Must be JSON serializable, or a byte[]. If the type is byte[], then it is not JSON encoded before transmission</typeparam>
    /// <typeparam name="TOut">The type returned by the write. Must be JSON deserializable. Some writes return nothing, in which case this should be an empty Object</typeparam>
    public class Write<TIn, TOut> : Write<TIn>
    {
        public Write(ConsulClientConfiguration config, HttpMethod method, string path, TIn body, WriteOptions q)
            : base(config, method, path, body, q) { }
        public Write(ConsulClientConfiguration config, HttpMethod method, string path, WriteOptions q)
            : base(config, method, path, q) { }

        protected override void ReadResponse<TResult>(Stream responseStream, ref TResult result)
        {
            (result as WriteResult<TOut>).Response = DecodeFromStream<TOut>(responseStream);
        }

        public new WriteResult<TOut> Execute()
        {
            var result = new WriteResult<TOut>();
            ExecuteInternal(result);
            return result;
        }
    }

    /// <summary>
    /// A Consul API request
    /// </summary>
    public abstract class Request
    {
        // 15 minute HTTP timeout
        protected const int _requestTimeout = 900000;

        protected Stopwatch timer = new Stopwatch();
        public ConsulClientConfiguration Config { get; set; }
        public HttpMethod Method { get; set; }
        public Uri Url { get; set; }
        public Dictionary<string, string> Params { get; set; }
        public Stream ResponseStream { get; set; }

        internal Request()
        {
            Params = new Dictionary<string, string>();
        }

        internal Request(ConsulClientConfiguration config, HttpMethod method, string path)
            : this()
        {
            Config = config;
            Method = method;

            var builder = new UriBuilder { Scheme = config.Scheme };
            if (config.Address.Contains(":"))
            {
                var split = config.Address.Split(':');
                try
                {
                    builder.Host = split[0];
                    builder.Port = int.Parse(split[1]);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Could not parse port from client config address", ex);
                }
            }
            else
            {
                builder.Host = config.Address;
            }

            builder.Path = path;

            if (!string.IsNullOrEmpty(config.Datacenter))
            {
                Params["dc"] = config.Datacenter;
            }

            if (config.WaitTime != TimeSpan.Zero)
            {
                Params["wait"] = config.WaitTime.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrEmpty(config.Token))
            {
                Params["token"] = config.Token;
            }

            Url = builder.Uri;
        }
        protected virtual void WriteData(Stream requestStream) { }
        protected virtual void ReadResponse<TResult>(Stream responseStream, ref TResult result) where TResult : ConsulResult { }
        protected WebRequest BuildWebRequest()
        {
            var req = WebRequest.CreateHttp(BuildConsulUri(Url, Params));
            req.Timeout = _requestTimeout;
            req.ReadWriteTimeout = _requestTimeout;
            req.Method = Method.Method;
            req.Accept = "application/json";
            req.KeepAlive = true;
            req.Credentials = Config.HttpAuth;
            return req;
        }
        protected Uri BuildConsulUri(Uri url, Dictionary<string, string> p)
        {
            var builder = new UriBuilder(Url);
            ApplyOptions();
            var queryParams = new List<string>(Params.Count / 2);
            foreach (var queryParam in Params)
            {
                if (!string.IsNullOrEmpty(queryParam.Value))
                {
                    queryParams.Add(string.Format("{0}={1}", HttpUtility.UrlPathEncode(queryParam.Key),
                        HttpUtility.UrlPathEncode(queryParam.Value)));
                }
                else
                {
                    queryParams.Add(string.Format("{0}", HttpUtility.UrlPathEncode(queryParam.Key)));
                }
            }
            builder.Query = string.Join("&", queryParams);
            return builder.Uri;
        }

        protected abstract void ApplyOptions();

        internal TOut DecodeFromStream<TOut>(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                using (var jsonReader = new JsonTextReader(reader))
                {
                    var ser = new JsonSerializer();
                    return ser.Deserialize<TOut>(jsonReader);
                }
            }
        }

        internal void EncodeToStream(object value, Stream stream)
        {
            using (var writer = new StreamWriter(stream))
            {
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    var ser = new JsonSerializer();
                    ser.Serialize(jsonWriter, value);
                    jsonWriter.Flush();
                }
            }
        }
    }

    /// <summary>
    /// Client provides a client to the Consul API. Operations should be done by constructing a client, then using the various APIs from that Client object.
    /// </summary>
    public partial class Client : IConsulClient
    {
        private static readonly object _lock = new object();
        private readonly ConsulClientConfiguration _config;

        public Client()
        {
            _config = new ConsulClientConfiguration();
        }

        public Client(ConsulClientConfiguration c)
        {
            _config = c;
        }
        internal Query CreateQuery(string path)
        {
            return CreateQuery(HttpMethod.Get, path, QueryOptions.Default);
        }

        internal Query CreateQuery(string path, QueryOptions q)
        {
            return CreateQuery(HttpMethod.Get, path, q);
        }

        internal Query CreateQuery(HttpMethod method, string path, QueryOptions q)
        {
            return new Query(_config, method, path, q);
        }
        internal Query<T> CreateQuery<T>(string path)
        {
            return CreateQuery<T>(HttpMethod.Get, path, QueryOptions.Default);
        }

        internal Query<T> CreateQuery<T>(string path, QueryOptions q)
        {
            return CreateQuery<T>(HttpMethod.Get, path, q);
        }

        internal Query<T> CreateQuery<T>(HttpMethod method, string path, QueryOptions q)
        {
            return new Query<T>(_config, method, path, q);
        }
        internal Modify<TOut> CreateOutWrite<TOut>(string path)
        {
            return CreateOutWrite<TOut>(HttpMethod.Put, path, WriteOptions.Empty);
        }

        internal Modify<TOut> CreateOutWrite<TOut>(string path, WriteOptions q)
        {
            return CreateOutWrite<TOut>(HttpMethod.Put, path, q);
        }

        internal Modify<TOut> CreateOutWrite<TOut>(HttpMethod method, string path, WriteOptions q)
        {
            return new Modify<TOut>(_config, method, path, q);
        }
        internal Write<TIn> CreateInWrite<TIn>(string path, TIn body)
        {
            return CreateInWrite<TIn>(HttpMethod.Put, path, body, WriteOptions.Empty);
        }

        internal Write<TIn> CreateInWrite<TIn>(string path, TIn body, WriteOptions q)
        {
            return CreateInWrite<TIn>(HttpMethod.Put, path, body, q);
        }

        internal Write<TIn> CreateInWrite<TIn>(HttpMethod method, string path, TIn body, WriteOptions q)
        {
            return new Write<TIn>(_config, method, path, body, q);
        }
        internal Write CreateWrite(string path)
        {
            return CreateWrite(HttpMethod.Put, path, WriteOptions.Empty);
        }

        internal Write CreateWrite(string path, WriteOptions q)
        {
            return CreateWrite(HttpMethod.Put, path, q);
        }

        internal Write CreateWrite(HttpMethod method, string path, WriteOptions q)
        {
            return new Write(_config, method, path, q);
        }

        internal Write<TIn, TOut> CreateWrite<TIn, TOut>(string path, TIn body)
        {
            return CreateWrite<TIn, TOut>(HttpMethod.Put, path, body, WriteOptions.Empty);
        }

        internal Write<TIn, TOut> CreateWrite<TIn, TOut>(string path, TIn body, WriteOptions q)
        {
            return CreateWrite<TIn, TOut>(HttpMethod.Put, path, body, q);
        }

        internal Write<TIn, TOut> CreateWrite<TIn, TOut>(HttpMethod method, string path, TIn body, WriteOptions q)
        {
            return new Write<TIn, TOut>(_config, method, path, body, q);
        }
    }
}