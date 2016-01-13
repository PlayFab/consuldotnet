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

namespace Consul
{
    public interface IKVEndpoint
    {
        WriteResult<bool> Acquire(KVPair p);
        WriteResult<bool> Acquire(KVPair p, WriteOptions q);
        WriteResult<bool> CAS(KVPair p);
        WriteResult<bool> CAS(KVPair p, WriteOptions q);
        WriteResult<bool> Delete(string key);
        WriteResult<bool> Delete(string key, WriteOptions q);
        WriteResult<bool> DeleteCAS(KVPair p);
        WriteResult<bool> DeleteCAS(KVPair p, WriteOptions q);
        WriteResult<bool> DeleteTree(string prefix);
        WriteResult<bool> DeleteTree(string prefix, WriteOptions q);
        QueryResult<KVPair> Get(string key);
        QueryResult<KVPair> Get(string key, QueryOptions q);
        QueryResult<KVPair> Get(string key, QueryOptions q, CancellationToken ct);
        QueryResult<string[]> Keys(string prefix);
        QueryResult<string[]> Keys(string prefix, string separator);
        QueryResult<string[]> Keys(string prefix, string separator, QueryOptions q);
        QueryResult<string[]> Keys(string prefix, string separator, QueryOptions q, CancellationToken ct);
        QueryResult<KVPair[]> List(string prefix);
        QueryResult<KVPair[]> List(string prefix, QueryOptions q);
        QueryResult<KVPair[]> List(string prefix, QueryOptions q, CancellationToken ct);
        WriteResult<bool> Put(KVPair p);
        WriteResult<bool> Put(KVPair p, WriteOptions q);
        WriteResult<bool> Release(KVPair p);
        WriteResult<bool> Release(KVPair p, WriteOptions q);
    }
}