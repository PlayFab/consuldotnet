// -----------------------------------------------------------------------
//  <copyright file="KV.cs" company="PlayFab Inc">
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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Consul
{
    /// <summary>
    /// KVPair is used to represent a single K/V entry
    /// </summary>
    public class KVPair
    {
        public string Key { get; set; }

        [JsonProperty]
        public ulong CreateIndex { get; private set; }

        [JsonProperty]
        public ulong ModifyIndex { get; set; }

        [JsonProperty]
        public ulong LockIndex { get; private set; }

        public ulong Flags { get; set; }
        public byte[] Value { get; set; }
        public string Session { get; set; }

        public KVPair(string key)
        {
            Key = key;
        }
        internal void Validate()
        {
            ValidatePath(Key);
        }
        static internal void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidKeyPairException("Invalid key. Key path is empty.");
            }
            else if (path[0] == '/')
            {
                throw new InvalidKeyPairException(string.Format("Invalid key. Key must not begin with a '/': {0}", path));
            }
        }
    }

    [Serializable]
    public class InvalidKeyPairException : Exception
    {
        public InvalidKeyPairException() { }
        public InvalidKeyPairException(string message) : base(message) { }
        public InvalidKeyPairException(string message, System.Exception inner) : base(message, inner) { }
        protected InvalidKeyPairException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }

    /// <summary>
    /// KV is used to manipulate the key/value pair API
    /// </summary>
    public class KV : IKVEndpoint
    {
        private readonly ConsulClient _client;

        public KV(ConsulClient c)
        {
            _client = c;
        }
        
        /// <summary>
        /// Acquire is used for a lock acquisition operation. The Key, Flags, Value and Session are respected.
        /// </summary>p.Validate();
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <returns>A write result indicating if the acquisition attempt succeeded</returns>
        public async Task<WriteResult<bool>> Acquire(KVPair p)
        {
            return await Acquire(p, WriteOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Acquire is used for a lock acquisition operation. The Key, Flags, Value and Session are respected.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the acquisition attempt succeeded</returns>
        public async Task<WriteResult<bool>> Acquire(KVPair p, WriteOptions q)
        {
            p.Validate();
            var req = _client.Put<byte[], bool>(string.Format("/v1/kv/{0}", p.Key), p.Value, q);
            if (p.Flags > 0)
            {
                req.Params["flags"] = p.Flags.ToString();
            }
            req.Params["acquire"] = p.Session;
            return await req.Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// CAS is used for a Check-And-Set operation. The Key, ModifyIndex, Flags and Value are respected. Returns true on success or false on failures.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <returns>A write result indicating if the write attempt succeeded</returns>
        public async Task<WriteResult<bool>> CAS(KVPair p)
        {
            return await CAS(p, WriteOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// CAS is used for a Check-And-Set operation. The Key, ModifyIndex, Flags and Value are respected. Returns true on success or false on failures.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the write attempt succeeded</returns>
        public async Task<WriteResult<bool>> CAS(KVPair p, WriteOptions q)
        {
            p.Validate();
            var req = _client.Put<byte[], bool>(string.Format("/v1/kv/{0}", p.Key), p.Value, q);
            if (p.Flags > 0)
            {
                req.Params["flags"] = p.Flags.ToString();
            }
            req.Params["cas"] = p.ModifyIndex.ToString();
            return await req.Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// Delete is used to delete a single key.
        /// </summary>
        /// <param name="key">The key name to delete</param>
        /// <returns>A write result indicating if the delete attempt succeeded</returns>
        public async Task<WriteResult<bool>> Delete(string key)
        {
            return await Delete(key, WriteOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete is used to delete a single key.
        /// </summary>
        /// <param name="key">The key name to delete</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the delete attempt succeeded</returns>
        public async Task<WriteResult<bool>> Delete(string key, WriteOptions q)
        {
            KVPair.ValidatePath(key);
            return await _client.Delete<bool>(string.Format("/v1/kv/{0}", key), q).Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// DeleteCAS is used for a Delete Check-And-Set operation. The Key and ModifyIndex are respected. Returns true on success or false on failures.
        /// </summary>
        /// <param name="p">The key/value pair to delete</param>
        /// <returns>A write result indicating if the delete attempt succeeded</returns>
        public async Task<WriteResult<bool>> DeleteCAS(KVPair p)
        {
            return await DeleteCAS(p, WriteOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// DeleteCAS is used for a Delete Check-And-Set operation. The Key and ModifyIndex are respected. Returns true on success or false on failures.
        /// </summary>
        /// <param name="p">The key/value pair to delete</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the delete attempt succeeded</returns>
        public async Task<WriteResult<bool>> DeleteCAS(KVPair p, WriteOptions q)
        {
            p.Validate();
            var req = _client.Delete<bool>(string.Format("/v1/kv/{0}", p.Key), q);
            req.Params.Add("cas", p.ModifyIndex.ToString());
            return await req.Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// DeleteTree is used to delete all keys under a prefix
        /// </summary>
        /// <param name="prefix">The key prefix to delete from</param>
        /// <returns>A write result indicating if the recursive delete attempt succeeded</returns>
        public async Task<WriteResult<bool>> DeleteTree(string prefix)
        {
            return await DeleteTree(prefix, WriteOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// DeleteTree is used to delete all keys under a prefix
        /// </summary>
        /// <param name="prefix">The key prefix to delete from</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the recursiv edelete attempt succeeded</returns>
        public async Task<WriteResult<bool>> DeleteTree(string prefix, WriteOptions q)
        {
            KVPair.ValidatePath(prefix);
            var req = _client.Delete<bool>(string.Format("/v1/kv/{0}", prefix), q);
            req.Params.Add("recurse", string.Empty);
            return await req.Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// Get is used to lookup a single key
        /// </summary>
        /// <param name="key">The key name</param>
        /// <returns>A query result containing the requested key/value pair, or a query result with a null response if the key does not exist</returns>
        public async Task<QueryResult<KVPair>> Get(string key)
        {
            return await Get(key, QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Get is used to lookup a single key
        /// </summary>
        /// <param name="key">The key name</param>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing the requested key/value pair, or a query result with a null response if the key does not exist</returns>
        public async Task<QueryResult<KVPair>> Get(string key, QueryOptions q)
        {
            return await Get(key, q, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Get is used to lookup a single key
        /// </summary>
        /// <param name="key">The key name</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing the requested key/value pair, or a query result with a null response if the key does not exist</returns>
        public async Task<QueryResult<KVPair>> Get(string key, QueryOptions q, CancellationToken ct)
        {
            var req = _client.Get<KVPair[]>(string.Format("/v1/kv/{0}", key), q);
            var res = await req.Execute(ct).ConfigureAwait(false);
            var ret = new QueryResult<KVPair>()
            {
                KnownLeader = res.KnownLeader,
                LastContact = res.LastContact,
                LastIndex = res.LastIndex,
                RequestTime = res.RequestTime
            };
            if (res.Response != null && res.Response.Length > 0)
            {
                ret.Response = res.Response[0];
            }
            return ret;
        }

        /// <summary>
        /// Keys is used to list all the keys under a prefix.
        /// </summary>
        /// <param name="prefix">The key prefix to filter on</param>
        /// <returns>A query result containing a list of key names</returns>
        public async Task<QueryResult<string[]>> Keys(string prefix)
        {
            return await Keys(prefix, string.Empty, QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Keys is used to list all the keys under a prefix. Optionally, a separator can be used to limit the responses.
        /// </summary>
        /// <param name="prefix">The key prefix to filter on</param>
        /// <param name="separator">The terminating suffix of the filter - e.g. a separator of "/" and a prefix of "/web/" will match "/web/foo" and "/web/foo/" but not "/web/foo/baz"</param>
        /// <returns>A query result containing a list of key names</returns>
        public async Task<QueryResult<string[]>> Keys(string prefix, string separator)
        {
            return await Keys(prefix, separator, QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Keys is used to list all the keys under a prefix. Optionally, a separator can be used to limit the responses.
        /// </summary>
        /// <param name="prefix">The key prefix to filter on</param>
        /// <param name="separator">The terminating suffix of the filter - e.g. a separator of "/" and a prefix of "/web/" will match "/web/foo" and "/web/foo/" but not "/web/foo/baz"</param>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing a list of key names</returns>
        public async Task<QueryResult<string[]>> Keys(string prefix, string separator, QueryOptions q)
        {
            return await Keys(prefix, separator, q, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Keys is used to list all the keys under a prefix. Optionally, a separator can be used to limit the responses.
        /// </summary>
        /// <param name="prefix">The key prefix to filter on</param>
        /// <param name="separator">The terminating suffix of the filter - e.g. a separator of "/" and a prefix of "/web/" will match "/web/foo" and "/web/foo/" but not "/web/foo/baz"</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing a list of key names</returns>
        public async Task<QueryResult<string[]>> Keys(string prefix, string separator, QueryOptions q, CancellationToken ct)
        {
            var req = _client.Get<string[]>(string.Format("/v1/kv/{0}", prefix), q);
            req.Params["keys"] = string.Empty;
            if (!string.IsNullOrEmpty(separator))
            {
                req.Params["separator"] = separator;
            }
            return await req.Execute(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// List is used to lookup all keys under a prefix
        /// </summary>
        /// <param name="prefix">The prefix to search under. Does not have to be a full path - e.g. a prefix of "ab" will find keys "abcd" and "ab11" but not "acdc"</param>
        /// <returns>A query result containing the keys matching the prefix</returns>
        public async Task<QueryResult<KVPair[]>> List(string prefix)
        {
            return await List(prefix, QueryOptions.Default, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// List is used to lookup all keys under a prefix
        /// </summary>
        /// <param name="prefix">The prefix to search under. Does not have to be a full path - e.g. a prefix of "ab" will find keys "abcd" and "ab11" but not "acdc"</param>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing the keys matching the prefix</returns>
        public async Task<QueryResult<KVPair[]>> List(string prefix, QueryOptions q)
        {
            return await List(prefix, q, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// List is used to lookup all keys under a prefix
        /// </summary>
        /// <param name="prefix">The prefix to search under. Does not have to be a full path - e.g. a prefix of "ab" will find keys "abcd" and "ab11" but not "acdc"</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns></returns>
        public async Task<QueryResult<KVPair[]>> List(string prefix, QueryOptions q, CancellationToken ct)
        {
            var req = _client.Get<KVPair[]>(string.Format("/v1/kv/{0}", prefix), q);
            req.Params["recurse"] = string.Empty;
            return await req.Execute(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Put is used to write a new value. Only the Key, Flags and Value properties are respected.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <returns>A write result indicating if the write attempt succeeded</returns>
        public async Task<WriteResult<bool>> Put(KVPair p)
        {
            return await Put(p, WriteOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Put is used to write a new value. Only the Key, Flags and Value is respected.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the write attempt succeeded</returns>
        public async Task<WriteResult<bool>> Put(KVPair p, WriteOptions q)
        {
            p.Validate();
            var req = _client.Put<byte[], bool>(string.Format("/v1/kv/{0}", p.Key), p.Value, q);
            if (p.Flags > 0)
            {
                req.Params["flags"] = p.Flags.ToString();
            }
            return await req.Execute().ConfigureAwait(false);
        }

        /// <summary>
        /// Release is used for a lock release operation. The Key, Flags, Value and Session are respected.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <returns>A write result indicating if the release attempt succeeded</returns>
        public async Task<WriteResult<bool>> Release(KVPair p)
        {
            return await Release(p, WriteOptions.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Release is used for a lock release operation. The Key, Flags, Value and Session are respected.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the release attempt succeeded</returns>
        public async Task<WriteResult<bool>> Release(KVPair p, WriteOptions q)
        {
            p.Validate();
            var req = _client.Put<object, bool>(string.Format("/v1/kv/{0}", p.Key), q);
            if (p.Flags > 0)
            {
                req.Params["flags"] = p.Flags.ToString();
            }
            req.Params["release"] = p.Session;
            return await req.Execute().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// KV is used to return a handle to the K/V apis
    /// </summary>
    public partial class ConsulClient : IConsulClient
    {
        private KV _kv;

        /// <summary>
        /// KV returns a handle to the KV endpoint
        /// </summary>
        public IKVEndpoint KV
        {
            get
            {
                if (_kv == null)
                {
                    lock (_lock)
                    {
                        if (_kv == null)
                        {
                            _kv = new KV(this);
                        }
                    }
                }
                return _kv;
            }
        }
    }
}