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
    public interface ICatalogEndpoint
    {
        QueryResult<string[]> Datacenters();
        WriteResult Deregister(CatalogDeregistration reg);
        WriteResult Deregister(CatalogDeregistration reg, WriteOptions q);
        QueryResult<CatalogNode> Node(string node);
        QueryResult<CatalogNode> Node(string node, QueryOptions q);
        QueryResult<Node[]> Nodes();
        QueryResult<Node[]> Nodes(QueryOptions q);
        QueryResult<Node[]> Nodes(QueryOptions q, CancellationToken ct);
        WriteResult Register(CatalogRegistration reg);
        WriteResult Register(CatalogRegistration reg, WriteOptions q);
        QueryResult<CatalogService[]> Service(string service);
        QueryResult<CatalogService[]> Service(string service, string tag);
        QueryResult<CatalogService[]> Service(string service, string tag, QueryOptions q);
        QueryResult<System.Collections.Generic.Dictionary<string, string[]>> Services();
        QueryResult<System.Collections.Generic.Dictionary<string, string[]>> Services(QueryOptions q);
        QueryResult<System.Collections.Generic.Dictionary<string, string[]>> Services(QueryOptions q, CancellationToken ct);
    }
}
