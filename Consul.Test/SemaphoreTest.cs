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
        public void Semaphore_BadLimit()
        {
            var client = new Client();

            const string keyName = "test/semaphore/badlimit";

            try
            {
                client.Semaphore(keyName, 0);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(ArgumentOutOfRangeException));
            }

            var semaphore1 = client.Semaphore(keyName, 1);
            semaphore1.Acquire(CancellationToken.None);

            try
            {
                var semaphore2 = client.Semaphore(keyName, 2);
                semaphore2.Acquire(CancellationToken.None);
            }
            catch (SemaphoreLimitConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(SemaphoreLimitConflictException));
                Assert.AreEqual(1, ex.RemoteLimit);
                Assert.AreEqual(2, ex.LocalLimit);
            }

            try
            {
                semaphore1.Release();
                semaphore1.Destroy();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(SemaphoreNotHeldException));
            }

            Assert.IsFalse(semaphore1.IsHeld);
        }
        [TestMethod]
        public void Semaphore_AcquireRelease()
        {
            var client = new Client();

            const string keyName = "test/semaphore/acquirerelease";

            var semaphore = client.Semaphore(keyName, 2);

            try
            {
                semaphore.Release();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(SemaphoreNotHeldException));
            }

            semaphore.Acquire(CancellationToken.None);

            Assert.IsTrue(semaphore.IsHeld);

            try
            {
                semaphore.Acquire(CancellationToken.None);
            }
            catch (SemaphoreHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(SemaphoreHeldException));
            }

            Assert.IsTrue(semaphore.IsHeld);

            semaphore.Release();

            try
            {
                semaphore.Release();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(SemaphoreNotHeldException));
            }

            Assert.IsFalse(semaphore.IsHeld);
        }

        [TestMethod]
        public void Semaphore_AcquireWaitRelease()
        {

            var client = new Client();

            const string keyName = "test/semaphore/acquirewaitrelease";

            var semaphoreOptions = new SemaphoreOptions(keyName, 1)
            {
                SessionName = "test_semaphoresession",
                SessionTTL = TimeSpan.FromSeconds(10)
            };

            var semaphore = client.Semaphore(semaphoreOptions);

            semaphore.Acquire(CancellationToken.None);

            Assert.IsTrue(semaphore.IsHeld);

            // Wait for multiple renewal cycles to ensure the semaphore session stays renewed.
            Task.Delay(TimeSpan.FromSeconds(60)).Wait();
            Assert.IsTrue(semaphore.IsHeld);

            semaphore.Release();

            Assert.IsFalse(semaphore.IsHeld);

            semaphore.Destroy();
        }

        [TestMethod]
        public void Semaphore_Contend()
        {
            var client = new Client();

            const string keyName = "test/semaphore/contend";
            const int contenderPool = 4;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(contenderPool-1 * (int)Semaphore.DefaultSemaphoreWaitTime.TotalMilliseconds);

                Parallel.For(0, contenderPool, new ParallelOptions { MaxDegreeOfParallelism = contenderPool, CancellationToken = cts.Token }, (v) =>
                {
                    var semaphore = client.Semaphore(keyName, 2);
                    semaphore.Acquire(CancellationToken.None);
                    acquired[v] = semaphore.IsHeld;
                    semaphore.Release();
                });
            }

            for (var i = 0; i < contenderPool; i++)
            {
                if (acquired[i])
                {
                    Assert.IsTrue(acquired[i]);
                }
                else
                {
                    Assert.Fail("Contender " + i.ToString() + " did not acquire the lock");
                }
            }
        }

        [TestMethod]
        public void Semaphore_Destroy()
        {
            var c = new Client();

            const string keyName = "test/semaphore/destroy";

            var semaphore1 = c.Semaphore(keyName, 2);
            var semaphore2 = c.Semaphore(keyName, 2);
            try
            {
                semaphore1.Acquire(CancellationToken.None);
                Assert.IsTrue(semaphore1.IsHeld);
                semaphore2.Acquire(CancellationToken.None);
                Assert.IsTrue(semaphore2.IsHeld);

                try
                {
                    semaphore1.Destroy();
                    Assert.Fail();
                }
                catch (SemaphoreHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(SemaphoreHeldException));
                }

                semaphore1.Release();
                Assert.IsFalse(semaphore1.IsHeld);

                try
                {
                    semaphore1.Destroy();
                    Assert.Fail();
                }
                catch (SemaphoreInUseException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(SemaphoreInUseException));
                }

                semaphore2.Release();
                Assert.IsFalse(semaphore2.IsHeld);
                semaphore1.Destroy();
                semaphore2.Destroy();
            }
            finally
            {
                try
                {
                    semaphore1.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(SemaphoreNotHeldException));
                }
                try
                {
                    semaphore2.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(SemaphoreNotHeldException));
                }
            }
        }

        [TestMethod]
        public void Semaphore_ForceInvalidate()
        {
            var client = new Client();

            const string keyName = "test/semaphore/forceinvalidate";

            var semaphore = client.Semaphore(keyName, 2);

            try
            {
                semaphore.Acquire(CancellationToken.None);

                Assert.IsTrue(semaphore.IsHeld);

                var checker = Task.Run(() =>
                {
                    while (semaphore.IsHeld)
                    {
                        Thread.Sleep(10);
                    }

                    Assert.IsFalse(semaphore.IsHeld);
                });

                Task.WaitAny(new[] { checker }, 1000);

                client.Session.Destroy(semaphore.LockSession);
            }
            finally
            {
                try
                {
                    semaphore.Release();
                    semaphore.Destroy();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(SemaphoreNotHeldException));
                }
            }
        }

        [TestMethod]
        public void Semaphore_DeleteKey()
        {
            var client = new Client();

            const string keyName = "test/semaphore/deletekey";

            var semaphore = client.Semaphore(keyName, 2);

            try
            {
                semaphore.Acquire(CancellationToken.None);

                Assert.IsTrue(semaphore.IsHeld);

                var checker = Task.Run(() =>
                {
                    while (semaphore.IsHeld)
                    {
                        Thread.Sleep(10);
                    }

                    Assert.IsFalse(semaphore.IsHeld);
                });

                Task.WaitAny(new[] { checker }, 1000);

                var req = client.KV.DeleteTree(semaphore.Opts.Prefix);
                Assert.IsTrue(req.Response);
            }
            finally
            {
                try
                {
                    semaphore.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(SemaphoreNotHeldException));
                }
            }
        }

        [TestMethod]
        public void Semaphore_Conflict()
        {
            var client = new Client();

            const string keyName = "test/semaphore/conflict";

            var semaphoreLock = client.CreateLock(keyName + "/.lock");

            semaphoreLock.Acquire(CancellationToken.None);

            Assert.IsTrue(semaphoreLock.IsHeld);

            var semaphore = client.Semaphore(keyName, 2);

            try
            {
                semaphore.Acquire(CancellationToken.None);
            }
            catch (SemaphoreConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(SemaphoreConflictException));
            }

            try
            {
                semaphore.Destroy();
            }
            catch (SemaphoreConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(SemaphoreConflictException));
            }

            semaphoreLock.Release();

            Assert.IsFalse(semaphoreLock.IsHeld);

            semaphoreLock.Destroy();
        }
    }
}