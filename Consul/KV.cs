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
using System.Collections.Generic;

namespace Consul
{
    /// <summary>
    /// KVPair is used to represent a single K/V entry
    /// </summary>

#if !NET451
    [JsonConverter(typeof(KVPairConverter))]
#endif
    public class KVPair
    {
        public string Key { get; set; }

        public ulong CreateIndex { get; set; }
        public ulong ModifyIndex { get; set; }
        public ulong LockIndex { get; set; }
        public ulong Flags { get; set; }

        public byte[] Value { get; set; }
        public string Session { get; set; }

        public KVPair(string key)
        {
            Key = key;
        }

        internal KVPair() { }
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

    [JsonConverter(typeof(KVTxnVerbTypeConverter))]
    public class KVTxnVerb : IEquatable<KVTxnVerb>
    {
        private static readonly KVTxnVerb kvSetOp = new KVTxnVerb() { Operation = "set" };
        private static readonly KVTxnVerb kvDeleteOp = new KVTxnVerb() { Operation = "delete" };
        private static readonly KVTxnVerb kvDeleteCASOp = new KVTxnVerb() { Operation = "delete-cas" };
        private static readonly KVTxnVerb kvDeleteTreeOp = new KVTxnVerb() { Operation = "delete-tree" };
        private static readonly KVTxnVerb kvCASOp = new KVTxnVerb() { Operation = "cas" };
        private static readonly KVTxnVerb kvLockOp = new KVTxnVerb() { Operation = "lock" };
        private static readonly KVTxnVerb kvUnlockOp = new KVTxnVerb() { Operation = "unlock" };
        private static readonly KVTxnVerb kvGetOp = new KVTxnVerb() { Operation = "get" };
        private static readonly KVTxnVerb kvGetTreeOp = new KVTxnVerb() { Operation = "get-tree" };
        private static readonly KVTxnVerb kvCheckSessionOp = new KVTxnVerb() { Operation = "check-session" };
        private static readonly KVTxnVerb kvCheckIndexOp = new KVTxnVerb() { Operation = "check-index" };

        public static KVTxnVerb Set { get { return kvSetOp; } }
        public static KVTxnVerb Delete { get { return kvDeleteOp; } }
        public static KVTxnVerb DeleteCAS { get { return kvDeleteCASOp; } }
        public static KVTxnVerb DeleteTree { get { return kvDeleteTreeOp; } }
        public static KVTxnVerb CAS { get { return kvCASOp; } }
        public static KVTxnVerb Lock { get { return kvLockOp; } }
        public static KVTxnVerb Unlock { get { return kvUnlockOp; } }
        public static KVTxnVerb Get { get { return kvGetOp; } }
        public static KVTxnVerb GetTree { get { return kvGetTreeOp; } }
        public static KVTxnVerb CheckSession { get { return kvCheckSessionOp; } }
        public static KVTxnVerb CheckIndex { get { return kvCheckIndexOp; } }

        public string Operation { get; private set; }

        public bool Equals(KVTxnVerb other)
        {
            return Operation == other.Operation;
        }

        public override bool Equals(object other)
        {
            // other could be a reference type, the is operator will return false if null
            return other is KVTxnVerb && Equals(other as KVTxnVerb);
        }

        public override int GetHashCode()
        {
            return Operation.GetHashCode();
        }
    }

    public class KVTxnVerbTypeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((KVTxnVerb)value).Operation);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            var status = (string)serializer.Deserialize(reader, typeof(string));
            switch (status)
            {
                case "set":
                    return KVTxnVerb.Set;
                case "delete":
                    return KVTxnVerb.Delete;
                case "delete-cas":
                    return KVTxnVerb.DeleteCAS;
                case "delete-tree":
                    return KVTxnVerb.DeleteTree;
                case "cas":
                    return KVTxnVerb.CAS;
                case "lock":
                    return KVTxnVerb.Lock;
                case "unlock":
                    return KVTxnVerb.Unlock;
                case "get":
                    return KVTxnVerb.Get;
                case "get-tree":
                    return KVTxnVerb.GetTree;
                case "check-session":
                    return KVTxnVerb.CheckSession;
                case "check-index":
                    return KVTxnVerb.CheckIndex;
                default:
                    throw new ArgumentException("Invalid KVTxnOpType value during deserialization");
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(KVTxnVerb);
        }
    }

    /// <summary>
    /// KVTxnOp defines a single operation inside a transaction.
    /// </summary>
    public class KVTxnOp
    {
        public KVTxnVerb Verb { get; set; }
        public string Key { get; set; }
        public byte[] Value { get; set; }
        public ulong Flags { get; set; }
        public ulong Index { get; set; }
        public string Session { get; set; }
        public KVTxnOp(string key, KVTxnVerb verb)
        {
            Key = key;
            Verb = verb;
        }
    }

    /// <summary>
    /// KVTxnResponse  is used to return the results of a transaction.
    /// </summary>
    public class KVTxnResponse
    {
        [JsonIgnore]
        public bool Success { get; internal set; }
        [JsonProperty]
        public List<TxnError> Errors { get; internal set; }
        [JsonProperty]
        public List<KVPair> Results { get; internal set; }

        public KVTxnResponse()
        {
            Results = new List<KVPair>();
            Errors = new List<TxnError>();
        }

        internal KVTxnResponse(TxnResponse txnRes)
        {
            if (txnRes == null)
            {
                Results = new List<KVPair>(0);
                Errors = new List<TxnError>(0);
                return;
            }

            if (txnRes.Results == null)
            {
                Results = new List<KVPair>(0);
            }
            else
            {
                Results = new List<KVPair>(txnRes.Results.Count);
                foreach (var txnResult in txnRes.Results)
                {
                    Results.Add(txnResult.KV);
                }
            }

            if (txnRes.Errors == null)
            {
                Errors = new List<TxnError>(0);
            }
            else
            {
                Errors = txnRes.Errors;
            }
        }
    }

    /// <summary>
    /// Indicates that the key pair data is invalid
    /// </summary>
    public class InvalidKeyPairException : Exception
    {
        public InvalidKeyPairException() { }
        public InvalidKeyPairException(string message) : base(message) { }
        public InvalidKeyPairException(string message, Exception inner) : base(message, inner) { }
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
        public Task<WriteResult<bool>> Acquire(KVPair p, CancellationToken ct = default(CancellationToken))
        {
            return Acquire(p, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Acquire is used for a lock acquisition operation. The Key, Flags, Value and Session are respected.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the acquisition attempt succeeded</returns>
        public Task<WriteResult<bool>> Acquire(KVPair p, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            p.Validate();
            var req = _client.Put<byte[], bool>(string.Format("/v1/kv/{0}", p.Key), p.Value, q);
            if (p.Flags > 0)
            {
                req.Params["flags"] = p.Flags.ToString();
            }
            req.Params["acquire"] = p.Session;
            return req.Execute(ct);
        }

        /// <summary>
        /// CAS is used for a Check-And-Set operation. The Key, ModifyIndex, Flags and Value are respected. Returns true on success or false on failures.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <returns>A write result indicating if the write attempt succeeded</returns>
        public Task<WriteResult<bool>> CAS(KVPair p, CancellationToken ct = default(CancellationToken))
        {
            return CAS(p, WriteOptions.Default, ct);
        }

        /// <summary>
        /// CAS is used for a Check-And-Set operation. The Key, ModifyIndex, Flags and Value are respected. Returns true on success or false on failures.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the write attempt succeeded</returns>
        public Task<WriteResult<bool>> CAS(KVPair p, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            p.Validate();
            var req = _client.Put<byte[], bool>(string.Format("/v1/kv/{0}", p.Key), p.Value, q);
            if (p.Flags > 0)
            {
                req.Params["flags"] = p.Flags.ToString();
            }
            req.Params["cas"] = p.ModifyIndex.ToString();
            return req.Execute(ct);
        }

        /// <summary>
        /// Delete is used to delete a single key.
        /// </summary>
        /// <param name="key">The key name to delete</param>
        /// <returns>A write result indicating if the delete attempt succeeded</returns>
        public Task<WriteResult<bool>> Delete(string key, CancellationToken ct = default(CancellationToken))
        {
            return Delete(key, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Delete is used to delete a single key.
        /// </summary>
        /// <param name="key">The key name to delete</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the delete attempt succeeded</returns>
        public Task<WriteResult<bool>> Delete(string key, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            KVPair.ValidatePath(key);
            return _client.Delete<bool>(string.Format("/v1/kv/{0}", key), q).Execute(ct);
        }

        /// <summary>
        /// DeleteCAS is used for a Delete Check-And-Set operation. The Key and ModifyIndex are respected. Returns true on success or false on failures.
        /// </summary>
        /// <param name="p">The key/value pair to delete</param>
        /// <returns>A write result indicating if the delete attempt succeeded</returns>
        public Task<WriteResult<bool>> DeleteCAS(KVPair p, CancellationToken ct = default(CancellationToken))
        {
            return DeleteCAS(p, WriteOptions.Default, ct);
        }

        /// <summary>
        /// DeleteCAS is used for a Delete Check-And-Set operation. The Key and ModifyIndex are respected. Returns true on success or false on failures.
        /// </summary>
        /// <param name="p">The key/value pair to delete</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the delete attempt succeeded</returns>
        public Task<WriteResult<bool>> DeleteCAS(KVPair p, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            p.Validate();
            var req = _client.Delete<bool>(string.Format("/v1/kv/{0}", p.Key), q);
            req.Params.Add("cas", p.ModifyIndex.ToString());
            return req.Execute(ct);
        }

        /// <summary>
        /// DeleteTree is used to delete all keys under a prefix
        /// </summary>
        /// <param name="prefix">The key prefix to delete from</param>
        /// <returns>A write result indicating if the recursive delete attempt succeeded</returns>
        public Task<WriteResult<bool>> DeleteTree(string prefix, CancellationToken ct = default(CancellationToken))
        {
            return DeleteTree(prefix, WriteOptions.Default, ct);
        }

        /// <summary>
        /// DeleteTree is used to delete all keys under a prefix
        /// </summary>
        /// <param name="prefix">The key prefix to delete from</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the recursiv edelete attempt succeeded</returns>
        public Task<WriteResult<bool>> DeleteTree(string prefix, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            KVPair.ValidatePath(prefix);
            var req = _client.Delete<bool>(string.Format("/v1/kv/{0}", prefix), q);
            req.Params.Add("recurse", string.Empty);
            return req.Execute(ct);
        }

        /// <summary>
        /// Get is used to lookup a single key
        /// </summary>
        /// <param name="key">The key name</param>
        /// <returns>A query result containing the requested key/value pair, or a query result with a null response if the key does not exist</returns>
        public Task<QueryResult<KVPair>> Get(string key, CancellationToken ct = default(CancellationToken))
        {
            return Get(key, QueryOptions.Default, ct);
        }

        /// <summary>
        /// Get is used to lookup a single key
        /// </summary>
        /// <param name="key">The key name</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing the requested key/value pair, or a query result with a null response if the key does not exist</returns>
        public async Task<QueryResult<KVPair>> Get(string key, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            var req = _client.Get<KVPair[]>(string.Format("/v1/kv/{0}", key), q);
            var res = await req.Execute(ct).ConfigureAwait(false);
            return new QueryResult<KVPair>(res, res.Response != null && res.Response.Length > 0 ? res.Response[0] : null);
        }

        /// <summary>
        /// Keys is used to list all the keys under a prefix.
        /// </summary>
        /// <param name="prefix">The key prefix to filter on</param>
        /// <returns>A query result containing a list of key names</returns>
        public Task<QueryResult<string[]>> Keys(string prefix, CancellationToken ct = default(CancellationToken))
        {
            return Keys(prefix, string.Empty, QueryOptions.Default, ct);
        }

        /// <summary>
        /// Keys is used to list all the keys under a prefix. Optionally, a separator can be used to limit the responses.
        /// </summary>
        /// <param name="prefix">The key prefix to filter on</param>
        /// <param name="separator">The terminating suffix of the filter - e.g. a separator of "/" and a prefix of "/web/" will match "/web/foo" and "/web/foo/" but not "/web/foo/baz"</param>
        /// <returns>A query result containing a list of key names</returns>
        public Task<QueryResult<string[]>> Keys(string prefix, string separator, CancellationToken ct = default(CancellationToken))
        {
            return Keys(prefix, separator, QueryOptions.Default, ct);
        }

        /// <summary>
        /// Keys is used to list all the keys under a prefix. Optionally, a separator can be used to limit the responses.
        /// </summary>
        /// <param name="prefix">The key prefix to filter on</param>
        /// <param name="separator">The terminating suffix of the filter - e.g. a separator of "/" and a prefix of "/web/" will match "/web/foo" and "/web/foo/" but not "/web/foo/baz"</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns>A query result containing a list of key names</returns>
        public Task<QueryResult<string[]>> Keys(string prefix, string separator, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            var req = _client.Get<string[]>(string.Format("/v1/kv/{0}", prefix), q);
            req.Params["keys"] = string.Empty;
            if (!string.IsNullOrEmpty(separator))
            {
                req.Params["separator"] = separator;
            }
            return req.Execute(ct);
        }

        /// <summary>
        /// List is used to lookup all keys under a prefix
        /// </summary>
        /// <param name="prefix">The prefix to search under. Does not have to be a full path - e.g. a prefix of "ab" will find keys "abcd" and "ab11" but not "acdc"</param>
        /// <returns>A query result containing the keys matching the prefix</returns>
        public Task<QueryResult<KVPair[]>> List(string prefix, CancellationToken ct = default(CancellationToken))
        {
            return List(prefix, QueryOptions.Default, ct);
        }

        /// <summary>
        /// List is used to lookup all keys under a prefix
        /// </summary>
        /// <param name="prefix">The prefix to search under. Does not have to be a full path - e.g. a prefix of "ab" will find keys "abcd" and "ab11" but not "acdc"</param>
        /// <param name="q">Customized query options</param>
        /// <param name="ct">Cancellation token for long poll request. If set, OperationCanceledException will be thrown if the request is cancelled before completing</param>
        /// <returns></returns>
        public Task<QueryResult<KVPair[]>> List(string prefix, QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            var req = _client.Get<KVPair[]>(string.Format("/v1/kv/{0}", prefix), q);
            req.Params["recurse"] = string.Empty;
            return req.Execute(ct);
        }

        /// <summary>
        /// Put is used to write a new value. Only the Key, Flags and Value properties are respected.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <returns>A write result indicating if the write attempt succeeded</returns>
        public Task<WriteResult<bool>> Put(KVPair p, CancellationToken ct = default(CancellationToken))
        {
            return Put(p, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Put is used to write a new value. Only the Key, Flags and Value is respected.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the write attempt succeeded</returns>
        public Task<WriteResult<bool>> Put(KVPair p, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            p.Validate();
            var req = _client.Put<byte[], bool>(string.Format("/v1/kv/{0}", p.Key), p.Value, q);
            if (p.Flags > 0)
            {
                req.Params["flags"] = p.Flags.ToString();
            }
            return req.Execute(ct);
        }

        /// <summary>
        /// Release is used for a lock release operation. The Key, Flags, Value and Session are respected.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <returns>A write result indicating if the release attempt succeeded</returns>
        public Task<WriteResult<bool>> Release(KVPair p, CancellationToken ct = default(CancellationToken))
        {
            return Release(p, WriteOptions.Default, ct);
        }

        /// <summary>
        /// Release is used for a lock release operation. The Key, Flags, Value and Session are respected.
        /// </summary>
        /// <param name="p">The key/value pair to store in Consul</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result indicating if the release attempt succeeded</returns>
        public Task<WriteResult<bool>> Release(KVPair p, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            p.Validate();
            var req = _client.Put<object, bool>(string.Format("/v1/kv/{0}", p.Key), q);
            if (p.Flags > 0)
            {
                req.Params["flags"] = p.Flags.ToString();
            }
            req.Params["release"] = p.Session;
            return req.Execute(ct);
        }

        /// <summary>
        ///  Txn is used to apply multiple KV operations in a single, atomic transaction.
        /// </summary>
        /// <remarks>
        /// Transactions are defined as a
        /// list of operations to perform, using the KVOp constants and KVTxnOp structure
        /// to define operations. If any operation fails, none of the changes are applied
        /// to the state store. Note that this hides the internal raw transaction interface
        /// and munges the input and output types into KV-specific ones for ease of use.
        /// If there are more non-KV operations in the future we may break out a new
        /// transaction API client, but it will be easy to keep this KV-specific variant
        /// supported.
        /// 
        /// Even though this is generally a write operation, we take a QueryOptions input
        /// and return a QueryMeta output. If the transaction contains only read ops, then
        /// Consul will fast-path it to a different endpoint internally which supports
        /// consistency controls, but not blocking. If there are write operations then
        /// the request will always be routed through raft and any consistency settings
        /// will be ignored.
        /// 
        /// // If there is a problem making the transaction request then an error will be
        /// returned. Otherwise, the ok value will be true if the transaction succeeded
        /// or false if it was rolled back. The response is a structured return value which
        /// will have the outcome of the transaction. Its Results member will have entries
        /// for each operation. Deleted keys will have a nil entry in the, and to save
        /// space, the Value of each key in the Results will be nil unless the operation
        /// is a KVGet. If the transaction was rolled back, the Errors member will have
        /// entries referencing the index of the operation that failed along with an error
        /// message.
        /// </remarks>
        /// <param name="txn">The constructed transaction</param>
        /// <param name="ct">A CancellationToken to prematurely end the request</param>
        /// <returns>The transaction response</returns>
        public Task<WriteResult<KVTxnResponse>> Txn(List<KVTxnOp> txn, CancellationToken ct = default(CancellationToken))
        {
            return Txn(txn, WriteOptions.Default, ct);
        }

        /// <summary>
        ///  Txn is used to apply multiple KV operations in a single, atomic transaction.
        /// </summary>
        /// <remarks>
        /// Transactions are defined as a
        /// list of operations to perform, using the KVOp constants and KVTxnOp structure
        /// to define operations. If any operation fails, none of the changes are applied
        /// to the state store. Note that this hides the internal raw transaction interface
        /// and munges the input and output types into KV-specific ones for ease of use.
        /// If there are more non-KV operations in the future we may break out a new
        /// transaction API client, but it will be easy to keep this KV-specific variant
        /// supported.
        /// 
        /// Even though this is generally a write operation, we take a QueryOptions input
        /// and return a QueryMeta output. If the transaction contains only read ops, then
        /// Consul will fast-path it to a different endpoint internally which supports
        /// consistency controls, but not blocking. If there are write operations then
        /// the request will always be routed through raft and any consistency settings
        /// will be ignored.
        /// 
        /// // If there is a problem making the transaction request then an error will be
        /// returned. Otherwise, the ok value will be true if the transaction succeeded
        /// or false if it was rolled back. The response is a structured return value which
        /// will have the outcome of the transaction. Its Results member will have entries
        /// for each operation. Deleted keys will have a nil entry in the, and to save
        /// space, the Value of each key in the Results will be nil unless the operation
        /// is a KVGet. If the transaction was rolled back, the Errors member will have
        /// entries referencing the index of the operation that failed along with an error
        /// message.
        /// </remarks>
        /// <param name="txn">The constructed transaction</param>
        /// <param name="q">Customized write options</param>
        /// <param name="ct">A CancellationToken to prematurely end the request</param>
        /// <returns>The transaction response</returns>
        public async Task<WriteResult<KVTxnResponse>> Txn(List<KVTxnOp> txn, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            var txnOps = new List<TxnOp>(txn.Count);

            foreach (var kvTxnOp in txn)
            {
                txnOps.Add(new TxnOp() { KV = kvTxnOp });
            }

            var req = _client.Put<List<TxnOp>, TxnResponse>("/v1/txn", txnOps, q);
            var txnRes = await req.Execute(ct);

            var res = new WriteResult<KVTxnResponse>(txnRes, new KVTxnResponse(txnRes.Response));

            res.Response.Success = txnRes.StatusCode == System.Net.HttpStatusCode.OK;

            return res;
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