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

using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface IKVEndpoint
    {
        Task<WriteResult<bool>> Acquire(KVPair p);
        Task<WriteResult<bool>> Acquire(KVPair p, WriteOptions q);
        Task<WriteResult<bool>> CAS(KVPair p);
        Task<WriteResult<bool>> CAS(KVPair p, WriteOptions q);
        Task<WriteResult<bool>> Delete(string key);
        Task<WriteResult<bool>> Delete(string key, WriteOptions q);
        Task<WriteResult<bool>> DeleteCAS(KVPair p);
        Task<WriteResult<bool>> DeleteCAS(KVPair p, WriteOptions q);
        Task<WriteResult<bool>> DeleteTree(string prefix);
        Task<WriteResult<bool>> DeleteTree(string prefix, WriteOptions q);
        Task<QueryResult<KVPair>> Get(string key);
        Task<QueryResult<KVPair>> Get(string key, QueryOptions q);
        Task<QueryResult<KVPair>> Get(string key, QueryOptions q, CancellationToken ct);
        Task<QueryResult<string[]>> Keys(string prefix);
        Task<QueryResult<string[]>> Keys(string prefix, string separator);
        Task<QueryResult<string[]>> Keys(string prefix, string separator, QueryOptions q);
        Task<QueryResult<string[]>> Keys(string prefix, string separator, QueryOptions q, CancellationToken ct);
        Task<QueryResult<KVPair[]>> List(string prefix);
        Task<QueryResult<KVPair[]>> List(string prefix, QueryOptions q);
        Task<QueryResult<KVPair[]>> List(string prefix, QueryOptions q, CancellationToken ct);
        Task<WriteResult<bool>> Put(KVPair p);
        Task<WriteResult<bool>> Put(KVPair p, WriteOptions q);
        Task<WriteResult<bool>> Release(KVPair p);
        Task<WriteResult<bool>> Release(KVPair p, WriteOptions q);
    }
}