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

using System;
using System.Threading;
namespace Consul
{
    public interface IACLEndpoint
    {
        WriteResult<string> Clone(string id);
        WriteResult<string> Clone(string id, WriteOptions q);
        WriteResult<string> Create(ACLEntry acl);
        WriteResult<string> Create(ACLEntry acl, WriteOptions q);
        WriteResult<bool> Destroy(string id);
        WriteResult<bool> Destroy(string id, WriteOptions q);
        QueryResult<ACLEntry> Info(string id);
        QueryResult<ACLEntry> Info(string id, QueryOptions q);
        QueryResult<ACLEntry> Info(string id, QueryOptions q, CancellationToken ct);
        QueryResult<ACLEntry[]> List();
        QueryResult<ACLEntry[]> List(QueryOptions q);
        QueryResult<ACLEntry[]> List(QueryOptions q, CancellationToken ct);
        WriteResult Update(ACLEntry acl);
        WriteResult Update(ACLEntry acl, WriteOptions q);
    }
}
