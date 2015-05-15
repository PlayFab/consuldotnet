// -----------------------------------------------------------------------
//  <copyright file="SemaphoreTest.cs" company="PlayFab Inc">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Consul.Test
{
    [TestClass]
    public class SemaphoreTest
    {
        [TestMethod]
        public void Semaphore_AcquireRelease()
        {
            var c = ClientTest.MakeClient();
            var s = c.Semaphore("test/semaphore", 2);

            try
            {
                s.Release();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof (SemaphoreNotHeldException));
            }

            s.Acquire(CancellationToken.None);

            Assert.IsTrue(s.IsHeld);

            try
            {
                s.Acquire(CancellationToken.None);
            }
            catch (SemaphoreHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof (SemaphoreHeldException));
            }

            Assert.IsTrue(s.IsHeld);

            s.Release();

            try
            {
                s.Release();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof (SemaphoreNotHeldException));
            }

            Assert.IsFalse(s.IsHeld);
        }

        [TestMethod]
        public void Semaphore_BadLimit()
        {
            var c = ClientTest.MakeClient();
            try
            {
                c.Semaphore("test/semaphore", 0);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Assert.IsInstanceOfType(ex, typeof (ArgumentOutOfRangeException));
            }

            var s = c.Semaphore("test/semaphore", 1);
            s.Acquire(CancellationToken.None);

            try
            {
                var s2 = c.Semaphore("test/semaphore", 2);
                s2.Acquire(CancellationToken.None);
            }
            catch (SemaphoreLimitConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof (SemaphoreLimitConflictException));
                Assert.AreEqual(1, ex.RemoteLimit);
                Assert.AreEqual(2, ex.LocalLimit);
            }

            try
            {
                s.Release();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof (SemaphoreNotHeldException));
            }

            Assert.IsFalse(s.IsHeld);
        }

        [TestMethod]
        public void Semaphore_Contend()
        {
            var c = ClientTest.MakeClient();

            var acquired = new bool[4];

            var acquireTasks = new Task[4];

            for (var i = 0; i < 4; i++)
            {
                var v = i;
                acquireTasks[i] = new Task(() =>
                {
                    var s = c.Semaphore("test/semaphore", 2);
                    s.Acquire(CancellationToken.None);
                    acquired[v] = s.IsHeld;
                    if (s.IsHeld)
                    {
                        Debug.WriteLine("Contender {0} acquired", v);
                    }
                    s.Release();
                });
                acquireTasks[v].Start();
            }

            Task.WaitAll(acquireTasks, (int) (3*Semaphore.DefaultSemaphoreRetryTime.TotalMilliseconds));

            foreach (var item in acquired)
            {
                Assert.IsTrue(item);
            }
        }

        [TestMethod]
        public void Semaphore_Destroy()
        {
            var c = ClientTest.MakeClient();
            var key = "test/semaphore";
            var s = c.Semaphore(key, 2);
            var s2 = c.Semaphore(key, 2);
            try
            {
                s.Acquire(CancellationToken.None);
                Assert.IsTrue(s.IsHeld);
                s2.Acquire(CancellationToken.None);
                Assert.IsTrue(s2.IsHeld);

                try
                {
                    s.Destroy();
                    Assert.Fail();
                }
                catch (SemaphoreHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof (SemaphoreHeldException));
                }

                s.Release();
                Assert.IsFalse(s.IsHeld);

                try
                {
                    s.Destroy();
                    Assert.Fail();
                }
                catch (SemaphoreInUseException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof (SemaphoreInUseException));
                }

                s2.Release();
                Assert.IsFalse(s2.IsHeld);
                s.Destroy();
                s2.Destroy();
            }
            finally
            {
                try
                {
                    s.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof (SemaphoreNotHeldException));
                }
                try
                {
                    s2.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof (SemaphoreNotHeldException));
                }
            }
        }

        [TestMethod]
        public void Semaphore_ForceInvalidate()
        {
            var c = ClientTest.MakeClient();
            var s = c.Semaphore("test/semaphore", 2);

            try
            {
                s.Acquire(CancellationToken.None);

                Assert.IsTrue(s.IsHeld);

                c.Session.Destroy(s.LockSession).Wait();

                var checker = new Task(() =>
                {
                    while (s.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                });

                checker.Start();

                Task.WaitAny(new[] {checker}, 1000);

                Assert.IsFalse(s.IsHeld);
            }
            finally
            {
                try
                {
                    s.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof (SemaphoreNotHeldException));
                }
            }
        }

        [TestMethod]
        public void Semaphore_DeleteKey()
        {
            var c = ClientTest.MakeClient();
            var s = c.Semaphore("test/semaphore", 2);

            try
            {
                s.Acquire(CancellationToken.None);

                Assert.IsTrue(s.IsHeld);

                var req = c.KV.DeleteTree(s.Opts.Prefix);
                req.Wait();
                Assert.IsTrue(req.Result.Response);

                var checker = new Task(() =>
                {
                    while (s.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                });

                checker.Start();

                Task.WaitAny(new[] {checker}, 1000);

                Assert.IsFalse(s.IsHeld);
            }
            finally
            {
                try
                {
                    s.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof (SemaphoreNotHeldException));
                }
            }
        }

        [TestMethod]
        public void Semaphore_Conflict()
        {
            var c = ClientTest.MakeClient();

            var semaphoreLock = c.Lock("test/sema/.lock");

            semaphoreLock.Acquire(CancellationToken.None);

            Assert.IsTrue(semaphoreLock.IsHeld);

            var s = c.Semaphore("test/sema", 2);

            try
            {
                s.Acquire(CancellationToken.None);
            }
            catch (SemaphoreConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof (SemaphoreConflictException));
            }

            try
            {
                s.Destroy();
            }
            catch (SemaphoreConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof (SemaphoreConflictException));
            }

            semaphoreLock.Release();

            Assert.IsFalse(semaphoreLock.IsHeld);
        }
    }
}