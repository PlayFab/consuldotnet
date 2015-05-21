// -----------------------------------------------------------------------
// <copyright file="ACL.cs" company="PlayFab Inc">
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
// </copyright>
// -----------------------------------------------------------------------

using System;
using Newtonsoft.Json;

namespace Consul
{
    /// <summary>
    /// The type of ACL token, which sets the permissions ceiling
    /// </summary>
    public class ACLType : IEquatable<ACLType>
    {
        public string Type { get; private set; }

        /// <summary>
        /// Token type which cannot modify ACL rules
        /// </summary>
        public static ACLType Client
        {
            get { return new ACLType() { Type = "client" }; }
        }

        /// <summary>
        /// Token type which is allowed to perform all actions
        /// </summary>
        public static ACLType Management
        {
            get { return new ACLType() { Type = "management" }; }
        }

        public bool Equals(ACLType other)
        {
            if (other == null)
            {
                return false;
            }
            return Type.Equals(other.Type);
        }

        public override bool Equals(object other)
        {
            var a = other as ACLType;
            return a != null && Equals(a);
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode();
        }
    }

    public class ACLTypeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((ACLType)value).Type);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            var type = (string)serializer.Deserialize(reader, typeof(string));
            switch (type)
            {
                case "client":
                    return ACLType.Client;
                case "management":
                    return ACLType.Management;
                default:
                    throw new ArgumentOutOfRangeException("serializer", type,
                        "Unknown ACL token type value found during deserialization");
            }
        }

        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(ACLType))
            {
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// ACLEntry is used to represent an ACL entry
    /// </summary>
    public class ACLEntry
    {
        [JsonProperty]
        public ulong CreateIndex { get; private set; }

        [JsonProperty]
        public ulong ModifyIndex { get; private set; }

        public string ID { get; set; }
        public string Name { get; set; }

        [JsonConverter(typeof(ACLTypeConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ACLType Type { get; set; }

        public string Rules { get; set; }

        public bool ShouldSerializeCreateIndex()
        {
            return false;
        }

        public bool ShouldSerializeModifyIndex()
        {
            return false;
        }

        public ACLEntry()
            : this(string.Empty, string.Empty, string.Empty)
        {
        }

        public ACLEntry(string name, string rules)
            : this(string.Empty, name, rules)
        {
        }

        public ACLEntry(string id, string name, string rules)
        {
            Type = ACLType.Client;
            ID = id;
            Name = name;
            Rules = rules;
        }
    }

    /// <summary>
    /// ACL can be used to query the ACL endpoints
    /// </summary>
    public class ACL
    {
        private readonly Client _client;

        internal ACL(Client c)
        {
            _client = c;
        }

        private class ACLCreationResult
        {
            [JsonProperty]
            internal string ID { get; set; }
        }

        /// <summary>
        /// Create is used to generate a new token with the given parameters
        /// </summary>
        /// <param name="acl">The ACL entry to create</param>
        /// <returns>A write result containing the newly created ACL token</returns>
        public WriteResult<string> Create(ACLEntry acl)
        {
            return Create(acl, WriteOptions.Empty);
        }

        /// <summary>
        /// Create is used to generate a new token with the given parameters
        /// </summary>
        /// <param name="acl">The ACL entry to create</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result containing the newly created ACL token</returns>
        public WriteResult<string> Create(ACLEntry acl, WriteOptions q)
        {
            var res = _client.CreateWriteRequest<ACLEntry, ACLCreationResult>("/v1/acl/create", acl, q).Execute();
            return new WriteResult<string>()
            {
                RequestTime = res.RequestTime,
                Response = res.Response.ID
            };
        }

        /// <summary>
        /// Update is used to update the rules of an existing token
        /// </summary>
        /// <param name="acl">The ACL entry to update</param>
        /// <returns>An empty write result</returns>
        public WriteResult<object> Update(ACLEntry acl)
        {
            return Update(acl, WriteOptions.Empty);
        }

        /// <summary>
        /// Update is used to update the rules of an existing token
        /// </summary>
        /// <param name="acl">The ACL entry to update</param>
        /// <param name="q">Customized write options</param>
        /// <returns>An empty write result</returns>
        public WriteResult<object> Update(ACLEntry acl, WriteOptions q)
        {
            return _client.CreateWriteRequest<ACLEntry, object>("/v1/acl/update", acl, q).Execute();
        }

        /// <summary>
        /// Destroy is used to destroy a given ACL token ID
        /// </summary>
        /// <param name="id">The ACL ID to destroy</param>
        /// <returns>An empty write result</returns>
        public WriteResult<bool> Destroy(string id)
        {
            return Destroy(id, WriteOptions.Empty);
        }

        /// <summary>
        /// Destroy is used to destroy a given ACL token ID
        /// </summary>
        /// <param name="id">The ACL ID to destroy</param>
        /// <param name="q">Customized write options</param>
        /// <returns>An empty write result</returns>
        public WriteResult<bool> Destroy(string id, WriteOptions q)
        {
            return _client.CreateWriteRequest<object, bool>(string.Format("/v1/acl/destroy/{0}", id)).Execute();
        }

        /// <summary>
        /// Clone is used to return a new token cloned from an existing one
        /// </summary>
        /// <param name="id">The ACL ID to clone</param>
        /// <returns>A write result containing the newly created ACL token</returns>
        public WriteResult<string> Clone(string id)
        {
            return Clone(id, WriteOptions.Empty);
        }

        /// <summary>
        /// Clone is used to return a new token cloned from an existing one
        /// </summary>
        /// <param name="id">The ACL ID to clone</param>
        /// <param name="q">Customized write options</param>
        /// <returns>A write result containing the newly created ACL token</returns>
        public WriteResult<string> Clone(string id, WriteOptions q)
        {
            var res = _client.CreateWriteRequest<ACLEntry, ACLCreationResult>(string.Format("/v1/acl/clone/{0}", id), q).Execute();
            var ret = new WriteResult<string>
            {
                RequestTime = res.RequestTime,
                Response = res.Response.ID
            };
            return ret;
        }

        /// <summary>
        /// Info is used to query for information about an ACL token
        /// </summary>
        /// <param name="id">The ACL ID to request information about</param>
        /// <returns>A query result containing the ACL entry matching the provided ID, or a query result with a null response if no token matched the provided ID</returns>
        public QueryResult<ACLEntry> Info(string id)
        {
            return Info(id, QueryOptions.Empty);
        }

        /// <summary>
        /// Info is used to query for information about an ACL token
        /// </summary>
        /// <param name="id">The ACL ID to request information about</param>
        /// <param name="q">Customized query options</param>
        /// <returns>A query result containing the ACL entry matching the provided ID, or a query result with a null response if no token matched the provided ID</returns>
        public QueryResult<ACLEntry> Info(string id, QueryOptions q)
        {
            var res = _client.CreateQueryRequest<ACLEntry[]>(string.Format("/v1/acl/info/{0}", id), q).Execute();
            var ret = new QueryResult<ACLEntry>()
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
        /// List is used to get all the ACL tokens
        /// </summary>
        /// <returns>A write result containing the list of all ACLs</returns>
        public QueryResult<ACLEntry[]> List()
        {
            return List(QueryOptions.Empty);
        }

        /// <summary>
        /// List is used to get all the ACL tokens
        /// </summary>
        /// <param name="q">Customized query options</param>
        /// <returns>A write result containing the list of all ACLs</returns>
        public QueryResult<ACLEntry[]> List(QueryOptions q)
        {
            return _client.CreateQueryRequest<ACLEntry[]>("/v1/acl/list", q).Execute();
        }
    }

    public partial class Client
    {
        private ACL _acl;

        /// <summary>
        /// ACL returns a handle to the ACL endpoints
        /// </summary>
        public ACL ACL
        {
            get
            {
                if (_acl == null)
                {
                    lock (_lock)
                    {
                        if (_acl == null)
                        {
                            _acl = new ACL(this);
                        }
                    }
                }
                return _acl;
            }
        }
    }
}