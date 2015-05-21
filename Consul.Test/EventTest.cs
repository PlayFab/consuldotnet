// -----------------------------------------------------------------------
//  <copyright file="EventTest.cs" company="PlayFab Inc">
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
    public class EventTest
    {
        [TestMethod]
        public void Event_FireList()
        {
            var c = ClientTest.MakeClient();

            var p = new UserEvent()
            {
                Name = "foo"
            };

            var res = c.Event.Fire(p);

            Assert.AreNotEqual(0, res.RequestTime);
            Assert.IsFalse(string.IsNullOrEmpty(res.Response));

            var events = c.Event.List();
            Assert.AreNotEqual(0, events.Response.Length);
            Assert.AreEqual(res.Response, events.Response[events.Response.Length - 1].ID);
            Assert.AreEqual(c.Event.IDToIndex(res.Response), events.LastIndex);
        }
    }
}