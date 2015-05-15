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

            var client = new Client();
            var c = new Client(new Config() {Token = ConsulRoot});
            var ae = new ACLEntry()
            {
                Name = "API Test",
                Type = ACLType.Client,
                Rules = "key \"\" { policy = \"deny\" }"
            };
            var res = c.ACL.Create(ae).GetAwaiter().GetResult();
            var id = res.Response;

            Assert.AreNotEqual(res.RequestTime.TotalMilliseconds, 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Response));

            var ae2 = c.ACL.Info(id).GetAwaiter().GetResult();

            Assert.IsNotNull(ae2.Response);
            Assert.AreEqual(ae.Name, ae2.Response.Name);
            Assert.AreEqual(ae.Type, ae2.Response.Type);
            Assert.AreEqual(ae.Rules, ae2.Response.Rules);

            var des = c.ACL.Destroy(id).GetAwaiter().GetResult();
            Assert.IsTrue(des.Response);
        }

        [TestMethod]
        public void ACL_CloneUpdateDestroy()
        {
            if (string.IsNullOrEmpty(ConsulRoot))
            {
                Assert.Inconclusive();
            }
            var c = new Client(new Config() {Token = ConsulRoot});

            var res = c.ACL.Clone(ConsulRoot).GetAwaiter().GetResult();

            var ae = c.ACL.Info(res.Response).GetAwaiter().GetResult();
            ae.Response.Rules = "key \"\" { policy = \"deny\" }";
            c.ACL.Update(ae.Response).GetAwaiter().GetResult();

            var ae2 = c.ACL.Info(res.Response).GetAwaiter().GetResult();
            Assert.AreEqual("key \"\" { policy = \"deny\" }", ae2.Response.Rules);

            var id = res.Response;

            Assert.AreNotEqual(res.RequestTime.TotalMilliseconds, 0);
            Assert.IsFalse(string.IsNullOrEmpty(res.Response));

            var des = c.ACL.Destroy(id).GetAwaiter().GetResult();
            Assert.IsTrue(des.Response);
        }

        [TestMethod]
        public void ACL_Info()
        {
            if (string.IsNullOrEmpty(ConsulRoot))
            {
                Assert.Inconclusive();
            }
            var c = new Client(new Config() {Token = ConsulRoot});

            var res = c.ACL.Info(ConsulRoot).GetAwaiter().GetResult();

            Assert.IsNotNull(res.Response);
            Assert.AreNotEqual(res.RequestTime.TotalMilliseconds, 0);
            Assert.AreEqual(res.Response.ID, ConsulRoot);
            Assert.AreEqual(res.Response.Type, ACLType.Management);
        }

        [TestMethod]
        public void ACL_List()
        {
            if (string.IsNullOrEmpty(ConsulRoot))
            {
                Assert.Inconclusive();
            }
            var c = new Client(new Config() {Token = ConsulRoot});

            var res = c.ACL.List().GetAwaiter().GetResult();

            Assert.IsNotNull(res.Response);
            Assert.AreNotEqual(res.RequestTime.TotalMilliseconds, 0);
            Assert.IsTrue(res.Response.Length >= 2);
        }
    }
}