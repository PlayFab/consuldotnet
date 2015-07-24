// -----------------------------------------------------------------------
//  <copyright file="ACLTest.cs" company="PlayFab Inc">
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Consul.Test
{
    [TestClass]
    public class ACLTest
    {
        private const string ConsulRoot = "yep";

        [TestMethod]
        public void ACL_CreateDestroy()
        {
            if (string.IsNullOrEmpty(ConsulRoot))
            {
                Assert.Inconclusive();
            }

            var client = new Client(new ConsulClientConfiguration() {Token = ConsulRoot});
            var aclEntry = new ACLEntry()
            {
                Name = "API Test",
                Type = ACLType.Client,
                Rules = "key \"\" { policy = \"deny\" }"
            };
            var res = client.ACL.Create(aclEntry);
            var id = res.Response;

            Assert.AreNotEqual(0, res.RequestTime.TotalMilliseconds);
            Assert.IsFalse(string.IsNullOrEmpty(res.Response));

            var aclEntry2 = client.ACL.Info(id);

            Assert.IsNotNull(aclEntry2.Response);
            Assert.AreEqual(aclEntry2.Response.Name, aclEntry.Name);
            Assert.AreEqual(aclEntry2.Response.Type, aclEntry.Type);
            Assert.AreEqual(aclEntry2.Response.Rules, aclEntry.Rules);

            Assert.IsTrue(client.ACL.Destroy(id).Response);
        }

        [TestMethod]
        public void ACL_CloneUpdateDestroy()
        {
            if (string.IsNullOrEmpty(ConsulRoot))
            {
                Assert.Inconclusive();
            }

            var client = new Client(new ConsulClientConfiguration() {Token = ConsulRoot});

            var cloneRequest = client.ACL.Clone(ConsulRoot);
            var aclID = cloneRequest.Response;

            var aclEntry = client.ACL.Info(aclID);
            aclEntry.Response.Rules = "key \"\" { policy = \"deny\" }";
            client.ACL.Update(aclEntry.Response);

            var aclEntry2 = client.ACL.Info(aclID);
            Assert.AreEqual("key \"\" { policy = \"deny\" }", aclEntry2.Response.Rules);

            var id = cloneRequest.Response;

            Assert.AreNotEqual(0, cloneRequest.RequestTime.TotalMilliseconds);
            Assert.IsFalse(string.IsNullOrEmpty(aclID));

            Assert.IsTrue(client.ACL.Destroy(id).Response);
        }

        [TestMethod]
        public void ACL_Info()
        {
            if (string.IsNullOrEmpty(ConsulRoot))
            {
                Assert.Inconclusive();
            }

            var client = new Client(new ConsulClientConfiguration() {Token = ConsulRoot});

            var aclEntry = client.ACL.Info(ConsulRoot);

            Assert.IsNotNull(aclEntry.Response);
            Assert.AreNotEqual(aclEntry.RequestTime.TotalMilliseconds, 0);
            Assert.AreEqual(aclEntry.Response.ID, ConsulRoot);
            Assert.AreEqual(aclEntry.Response.Type, ACLType.Management);
        }

        [TestMethod]
        public void ACL_List()
        {
            if (string.IsNullOrEmpty(ConsulRoot))
            {
                Assert.Inconclusive();
            }
            var client = new Client(new ConsulClientConfiguration() {Token = ConsulRoot});

            var aclList = client.ACL.List();

            Assert.IsNotNull(aclList.Response);
            Assert.AreNotEqual(aclList.RequestTime.TotalMilliseconds, 0);
            Assert.IsTrue(aclList.Response.Length >= 2);
        }
    }
}