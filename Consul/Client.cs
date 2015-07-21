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

namespace Consul
{
    public class Config
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
        public Config()
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
        public static readonly QueryOptions Empty = new QueryOptions()
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

    /// <summary>
    /// The result of a Consul API query
    /// </summary>
    /// <typeparam name="T">Must be able to be deserialized from JSON</typeparam>
    public class QueryResult<T>
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

        /// <summary>
        /// How long the request took
        /// </summary>
        public TimeSpan RequestTime { get; set; }

        /// <summary>
        /// The result of the query
        /// </summary>
        public T Response { get; internal set; }
    }

    /// <summary>
    /// The result of a Consul API write
    /// </summary>
    /// <typeparam name="T">Must be able to be deserialized from JSON. Some writes return nothing, in which case this should be an empty Object</typeparam>
    public class WriteResult<T>
    {
        /// <summary>
        /// How long did the request take
        /// </summary>
        public TimeSpan RequestTime { get; set; }

        /// <summary>
        /// The result of the write
        /// </summary>
        public T Response { get; internal set; }
    }

    /// <summary>
    /// A query to the Consul service
    /// </summary>
    /// <typeparam name="T">Must be JSON deserializable. Some writes return nothing, in which case this should be an empty Object</typeparam>
    public class Query<T> : Request
    {
        /// <summary>
        /// Annotate the request with additional query options
        /// </summary>
        public QueryOptions Options { get; set; }

        public Query(Config config, HttpMethod method, string path, QueryOptions q)
            : base(config, method, path)
        {
            if (q == null)
            {
                throw new ArgumentNullException("q");
            }
            Options = q;
        }

        private static void ParseQueryHeaders(WebResponse resp, ref QueryResult<T> meta)
        {
            var headers = resp.Headers;

            if (headers["X-Consul-Index"] != null)
            {
                try
                {
                    meta.LastIndex = ulong.Parse(headers["X-Consul-Index"]);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Failed to parse X-Consul-Index", ex);
                }
            }

            if (headers["X-Consul-LastContact"] != null)
            {
                try
                {
                    meta.LastContact = TimeSpan.FromMilliseconds(ulong.Parse(headers["X-Consul-LastContact"]));
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Failed to parse X-Consul-LastContact", ex);
                }
            }

            if (headers["X-Consul-KnownLeader"] != null)
            {
                try
                {
                    meta.KnownLeader = bool.Parse(headers["X-Consul-KnownLeader"]);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Failed to parse X-Consul-KnownLeader", ex);
                }
            }
        }

        protected override void ApplyOptions()
        {
            if (Options == QueryOptions.Empty)
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
                Params["wait"] = Duration.ToDuration(Options.WaitTime);
            }
            if (!string.IsNullOrEmpty(Options.Token))
            {
                Params["token"] = Options.Token;
            }
        }

        public QueryResult<T> Execute()
        {
            return Execute(CancellationToken.None);
        }

        public QueryResult<T> Execute(CancellationToken cancel)
        {
            var stopwatch = Stopwatch.StartNew();

            var req = WebRequest.CreateHttp(BuildConsulUri(Url, Params));
            req.Timeout = _requestTimeout;
            req.ReadWriteTimeout = _requestTimeout;
            req.Method = Method.Method;
            req.Accept = "application/json";
            req.KeepAlive = true;
            req.Credentials = Config.HttpAuth;

            using (cancel.Register(req.Abort))
            {

                try
                {
                    var res = (HttpWebResponse)(req.GetResponse());

                    var result = new QueryResult<T>()
                    {
                        Response = DecodeBody<T>(res.GetResponseStream())
                    };

                    ParseQueryHeaders(res, ref result);
                    stopwatch.Stop();
                    result.RequestTime = stopwatch.Elapsed;
                    return result;
                }
                catch (WebException ex)
                {
                    if(cancel.IsCancellationRequested)
                        throw new OperationCanceledException();

                    var res = (HttpWebResponse)ex.Response;
                    if (res == null)
                    {
                        throw new ApplicationException("Unexpected HTTP exception calling Consul", ex);
                    }
                    if (res.StatusCode == HttpStatusCode.NotFound)
                    {
                        var result = new QueryResult<T>();
                        ParseQueryHeaders(res, ref result);
                        stopwatch.Stop();
                        result.RequestTime = stopwatch.Elapsed;
                        return result;
                    }
                    var stream = res.GetResponseStream();
                    if (stream == null)
                    {
                        throw new ArgumentException(string.Format("Unexpected response code {0}",
                            res.StatusCode));
                    }
                    using (var sr = new StreamReader(stream))
                    {
                        throw new ArgumentException(string.Format("Unexpected response code {0}: {1}",
                            res.StatusCode, sr.ReadToEnd()));
                    }
                }
            }
        }
    }

    /// <summary>
    /// A write to the Consul service
    /// </summary>
    /// <typeparam name="T1">The type encoded and send to Consul. Must be JSON serializable, or a byte[]. If the type is byte[], then it is not encoded before transmission</typeparam>
    /// <typeparam name="T2">The type returned by the write. Must be JSON deserializable. Some writes return nothing, in which case this should be an empty Object</typeparam>
    public class Write<T1, T2> : Request
    {
        /// <summary>
        /// The data to write to Consul. Must be serializable to JSON.
        /// </summary>
        public T1 RequestBody { get; set; }

        internal bool UseRawRequestBody
        {
            get { return GetType().GenericTypeArguments[0] == typeof (byte[]); }
        }

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

        public Write(Config config, HttpMethod method, string path, WriteOptions q)
            : base(config, method, path)
        {
            if (q == null)
            {
                throw new ArgumentNullException("q");
            }
            Options = q;
        }

        public Write(Config config, HttpMethod method, string path, T1 body, WriteOptions q)
            : base(config, method, path)
        {
            if (q == null)
            {
                throw new ArgumentNullException("q");
            }
            Options = q;
            RequestBody = body;
        }

        public WriteResult<T2> Execute()
        {
            var stopwatch = Stopwatch.StartNew();
            var req = WebRequest.CreateHttp(BuildConsulUri(Url, Params));
            req.Timeout = _requestTimeout;
            req.ReadWriteTimeout = _requestTimeout;
            req.Method = Method.Method;
            req.Accept = "application/json";
            req.KeepAlive = true;
            req.Credentials = Config.HttpAuth;
            if (ResponseStream == null && RequestBody != null)
            {
                try
                {
                    var reqStream = req.GetRequestStream();

                    if (UseRawRequestBody)
                    {
                        WriteRawRequestBody(RequestBody, reqStream);
                    }
                    else
                    {
                        EncodeBody(RequestBody, reqStream);
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Unable to encode body for Consul request", ex);
                }
            }
            else
            {
                if (ResponseStream != null)
                {
                    throw new ApplicationException("Server has responded already, cannot reuse request objects");
                }
            }

            try
            {
                var res = req.GetResponse();

                if (((HttpWebResponse) res).StatusCode == HttpStatusCode.OK)
                {
                    var result = new WriteResult<T2>()
                    {
                        Response = DecodeBody<T2>(res.GetResponseStream())
                    };

                    stopwatch.Stop();
                    result.RequestTime = stopwatch.Elapsed;

                    return result;
                }
                else
                {
                    var stream = res.GetResponseStream();
                    if (stream == null)
                    {
                        throw new ArgumentException(string.Format("Unexpected response code {0}",
                            ((HttpWebResponse) res).StatusCode));
                    }
                    else
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            throw new ArgumentException(string.Format("Unexpected response code {0}: {1}",
                                ((HttpWebResponse) res).StatusCode, sr.ReadToEnd()));
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                var res = (HttpWebResponse) ex.Response;
                if (res == null)
                {
                    throw new ApplicationException("Unexpected HTTP exception calling Consul", ex);
                }
                else if (res.StatusCode == HttpStatusCode.NotFound)
                {
                    var result = new WriteResult<T2>();
                    stopwatch.Stop();
                    result.RequestTime = stopwatch.Elapsed;
                    return result;
                }
                else
                {
                    var stream = res.GetResponseStream();
                    if (stream == null)
                    {
                        throw new ArgumentException(string.Format("Unexpected response code {0}",
                            res.StatusCode));
                    }
                    else
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            throw new ArgumentException(string.Format("Unexpected response code {0}: {1}",
                                res.StatusCode, sr.ReadToEnd()));
                        }
                    }
                }
            }
        }

        /// Must be object because T1 cannot be converted to byte[]
        private static void WriteRawRequestBody(object body, Stream stream)
        {
            if (body == null)
            {
                return;
            }
            using (stream)
            {
                stream.Write(((byte[]) body), 0, ((byte[]) body).Length);
            }
        }
    }

    /// <summary>
    /// A Consul API request
    /// </summary>
    public abstract class Request
    {
        // 15 minute HTTP timeout
        protected const int _requestTimeout = 900000;

        public Config Config { get; set; }
        public HttpMethod Method { get; set; }
        public Uri Url { get; set; }
        public Dictionary<string, string> Params { get; set; }
        public Stream ResponseStream { get; set; }

        internal Request()
        {
            Params = new Dictionary<string, string>();
        }

        internal Request(Config config, HttpMethod method, string path)
            : this()
        {
            Config = config;
            Method = method;

            var builder = new UriBuilder {Scheme = config.Scheme};
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

        protected Uri BuildConsulUri(Uri url, Dictionary<string, string> p)
        {
            var builder = new UriBuilder(Url);
            ApplyOptions();
            var queryParams = new List<string>(Params.Count/2);
            foreach (var queryParam in Params)
            {
                if (!string.IsNullOrEmpty(queryParam.Value))
                {
                    queryParams.Add(string.Format("{0}={1}", HttpUtility.UrlEncode(queryParam.Key),
                        HttpUtility.UrlEncode(queryParam.Value)));
                }
                else
                {
                    queryParams.Add(string.Format("{0}", HttpUtility.UrlEncode(queryParam.Key)));
                }
            }
            builder.Query = string.Join("&", queryParams);
            return builder.Uri;
        }

        protected abstract void ApplyOptions();

        internal static TX DecodeBody<TX>(Stream stream)
        {
            var reader = new StreamReader(stream);
            var jsonReader = new JsonTextReader(reader);
            var ser = new JsonSerializer();
            return ser.Deserialize<TX>(jsonReader);
        }

        internal static void EncodeBody(object value, Stream stream)
        {
            var writer = new StreamWriter(stream);
            var jsonWriter = new JsonTextWriter(writer);
            var ser = new JsonSerializer();
            ser.Serialize(jsonWriter, value);
            jsonWriter.Flush();
        }
    }

    /// <summary>
    /// Client provides a client to the Consul API. Operations should be done by constructing a client, then using the various APIs from that Client object.
    /// </summary>
    public partial class Client
    {
        private static readonly object _lock = new object();
        private readonly Config _config;

        public Client()
        {
            _config = new Config();
        }

        public Client(Config c)
        {
            _config = c;
        }

        internal Query<T> CreateQueryRequest<T>(string path)
        {
            return CreateQueryRequest<T>(HttpMethod.Get, path, QueryOptions.Empty);
        }

        internal Query<T> CreateQueryRequest<T>(string path, QueryOptions q)
        {
            return CreateQueryRequest<T>(HttpMethod.Get, path, q);
        }

        internal Query<T> CreateQueryRequest<T>(HttpMethod method, string path, QueryOptions q)
        {
            return new Query<T>(_config, method, path, q);
        }

        internal Write<T1, T2> CreateWriteRequest<T1, T2>(string path)
        {
            return CreateWriteRequest<T1, T2>(HttpMethod.Put, path, WriteOptions.Empty);
        }

        internal Write<T1, T2> CreateWriteRequest<T1, T2>(string path, WriteOptions q)
        {
            return CreateWriteRequest<T1, T2>(HttpMethod.Put, path, q);
        }

        internal Write<T1, T2> CreateWriteRequest<T1, T2>(HttpMethod method, string path, WriteOptions q)
        {
            return new Write<T1, T2>(_config, method, path, q);
        }

        internal Write<T1, T2> CreateWriteRequest<T1, T2>(string path, T1 body)
        {
            return CreateWriteRequest<T1, T2>(HttpMethod.Put, path, body, WriteOptions.Empty);
        }

        internal Write<T1, T2> CreateWriteRequest<T1, T2>(string path, T1 body, WriteOptions q)
        {
            return CreateWriteRequest<T1, T2>(HttpMethod.Put, path, body, q);
        }

        internal Write<T1, T2> CreateWriteRequest<T1, T2>(HttpMethod method, string path, T1 body, WriteOptions q)
        {
            return new Write<T1, T2>(_config, method, path, body, q);
        }
    }
}