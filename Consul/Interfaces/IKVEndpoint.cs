// -----------------------------------------------------------------------
//  <copyright file="Health.cs" company="PlayFab Inc">
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface IKVEndpoint
    {
        Task<WriteResult<bool>> Acquire(KVPair p, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> Acquire(KVPair p, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> CAS(KVPair p, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> CAS(KVPair p, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> Delete(string key, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> Delete(string key, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> DeleteCAS(KVPair p, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> DeleteCAS(KVPair p, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> DeleteTree(string prefix, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> DeleteTree(string prefix, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<KVPair>> Get(string key, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<KVPair>> Get(string key, QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<string[]>> Keys(string prefix, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<string[]>> Keys(string prefix, string separator, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<string[]>> Keys(string prefix, string separator, QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<KVPair[]>> List(string prefix, CancellationToken ct = default(CancellationToken));
        Task<QueryResult<KVPair[]>> List(string prefix, QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> Put(KVPair p, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> Put(KVPair p, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> Release(KVPair p, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<bool>> Release(KVPair p, WriteOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<KVTxnResponse>> Txn(List<KVTxnOp> txn, CancellationToken ct = default(CancellationToken));
        Task<WriteResult<KVTxnResponse>> Txn(List<KVTxnOp> txn, WriteOptions q, CancellationToken ct = default(CancellationToken));
    }
}