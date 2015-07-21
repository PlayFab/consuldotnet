// -----------------------------------------------------------------------
//  <copyright file="LockTest.cs" company="PlayFab Inc">
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
    public class LockTest
    {
        [TestMethod]
        public void Lock_AcquireRelease()
        {
            var client = new Client();
            const string keyName = "test/lock/acquirerelease";
            var lockKey = client.CreateLock(keyName);

            try
            {
                lockKey.Release();
            }
            catch (LockNotHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(LockNotHeldException));
            }

            lockKey.Acquire(CancellationToken.None);

            try
            {
                lockKey.Acquire(CancellationToken.None);
            }
            catch (LockHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(LockHeldException));
            }

            Assert.IsTrue(lockKey.IsHeld);

            lockKey.Release();

            try
            {
                lockKey.Release();
            }
            catch (LockNotHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(LockNotHeldException));
            }

            Assert.IsFalse(lockKey.IsHeld);
        }

        [TestMethod]
        public void Lock_EphemeralAcquireRelease()
        {
            var client = new Client();
            const string keyName = "test/lock/ephemerallock";
            var sessionId = client.Session.Create(new SessionEntry { Behavior = SessionBehavior.Delete });
            using (var l = client.AcquireLock(new LockOptions(keyName) { Session = sessionId.Response }, CancellationToken.None))
            {
                Assert.IsTrue(l.IsHeld);
                client.Session.Destroy(sessionId.Response);
            }
            Assert.IsNull(client.KV.Get(keyName).Response);
        }
        [TestMethod]
        public void Lock_AcquireWaitRelease()
        {
            var client = new Client();

            const string keyName = "test/lock/acquirewaitrelease";

            var lockOptions = new LockOptions(keyName)
            {
                SessionName = "test_locksession",
                SessionTTL = TimeSpan.FromSeconds(10)
            };

            var l = client.CreateLock(lockOptions);

            l.Acquire(CancellationToken.None);

            Assert.IsTrue(l.IsHeld);

            // Wait for multiple renewal cycles to ensure the lock session stays renewed.
            Task.Delay(TimeSpan.FromSeconds(60)).Wait();
            Assert.IsTrue(l.IsHeld);

            l.Release();

            Assert.IsFalse(l.IsHeld);

            l.Destroy();
        }
        [TestMethod]
        public void Lock_Contend()
        {
            var client = new Client();

            const string keyName = "test/lock/contend";
            const int contenderPool = 3;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(contenderPool * (int)Lock.DefaultLockWaitTime.TotalMilliseconds);

                Parallel.For(0, contenderPool, new ParallelOptions { MaxDegreeOfParallelism = contenderPool, CancellationToken = cts.Token }, (v) =>
                {
                    var lockKey = client.CreateLock(keyName);
                    lockKey.Acquire(CancellationToken.None);
                    Assert.IsTrue(acquired.TryAdd(v, lockKey.IsHeld));
                    if (lockKey.IsHeld)
                    {
                        Task.Delay(1000).Wait();
                        lockKey.Release();
                    }
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
        public void Lock_Contend_LockDelay()
        {
            var client = new Client();

            const string keyName = "test/lock/contendlockdelay";

            const int contenderPool = 3;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter((contenderPool + 1) * (int)Lock.DefaultLockWaitTime.TotalMilliseconds);

                Parallel.For(0, contenderPool, new ParallelOptions { MaxDegreeOfParallelism = contenderPool, CancellationToken = cts.Token }, (v) =>
                {
                    var lockKey = client.CreateLock(keyName);
                    lockKey.Acquire(CancellationToken.None);
                    if (lockKey.IsHeld)
                    {
                        Assert.IsTrue(acquired.TryAdd(v, lockKey.IsHeld));
                        client.Session.Destroy(lockKey.LockSession);
                    }
                });
            }
            for (var i = 0; i < contenderPool; i++)
            {
                bool didContend = false;
                if (acquired.TryGetValue(i, out didContend))
                {
                    Assert.IsTrue(didContend);
                }
                else
                {
                    Assert.Fail("Contender " + i.ToString() + " did not acquire the lock");
                }
            }
        }
        [TestMethod]
        public void Lock_Destroy()
        {
            var client = new Client();

            const string keyName = "test/lock/contendlockdelay";

            var lockKey = client.CreateLock(keyName);

            try
            {
                lockKey.Acquire(CancellationToken.None);

                Assert.IsTrue(lockKey.IsHeld);

                try
                {
                    lockKey.Destroy();
                    Assert.Fail();
                }
                catch (LockHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(LockHeldException));
                }

                lockKey.Release();

                Assert.IsFalse(lockKey.IsHeld);

                var lockKey2 = client.CreateLock(keyName);

                lockKey2.Acquire(CancellationToken.None);

                Assert.IsTrue(lockKey2.IsHeld);

                try
                {
                    lockKey.Destroy();
                    Assert.Fail();
                }
                catch (LockInUseException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(LockInUseException));
                }

                lockKey2.Release();

                Assert.IsFalse(lockKey2.IsHeld);

                lockKey.Destroy();
                lockKey2.Destroy();
            }
            finally
            {
                try
                {
                    lockKey.Release();
                }
                catch (LockNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(LockNotHeldException));
                }
            }
        }

        [TestMethod]
        public void Lock_RunAction()
        {
            var client = new Client();

            const string keyName = "test/lock/runaction";

            Task.WaitAll(Task.Run(() =>
            {
                client.ExecuteLocked(keyName, () =>
                {
                    // Only executes if the lock is held
                    Assert.IsTrue(true);
                });
            }),
            Task.Run(() =>
            {
                client.ExecuteLocked(keyName, () =>
                {
                    // Only executes if the lock is held
                    Assert.IsTrue(true);
                });
            }));
        }
        [TestMethod]
        public void Lock_AbortAction()
        {
            var client = new Client();

            const string keyName = "test/lock/abort";

            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    string lockSession = client.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) }).Response;
                    client.Session.RenewPeriodic(TimeSpan.FromSeconds(10), lockSession, cts.Token);
                    Task.Delay(1000).ContinueWith((w) => { client.Session.Destroy(lockSession); });
                    client.ExecuteAbortableLocked(new LockOptions(keyName) { Session = lockSession }, CancellationToken.None, () =>
                    {
                        Thread.Sleep(60000);
                    });
                }
                catch (TimeoutException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(TimeoutException));
                }
                cts.Cancel();
            }
            using (var cts = new CancellationTokenSource())
            {
                string lockSession = client.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) }).Response;
                client.Session.RenewPeriodic(TimeSpan.FromSeconds(10), lockSession, cts.Token);
                client.ExecuteAbortableLocked(new LockOptions(keyName) { Session = lockSession }, CancellationToken.None, () =>
                {
                    Task.Delay(1000).ContinueWith((w) => { Assert.IsTrue(true); });
                });
                cts.Cancel();
            }
        }
        [TestMethod]
        public void Lock_ReclaimLock()
        {
            var client = new Client();

            const string keyName = "test/lock/reclaim";

            var sessionRequest = client.Session.Create();
            var sessionId = sessionRequest.Response;
            try
            {
                var lock1 = client.CreateLock(new LockOptions(keyName)
                {
                    Session = sessionId
                });

                var lock2 = client.CreateLock(new LockOptions(keyName)
                {
                    Session = sessionId
                });

                try
                {
                    lock1.Acquire(CancellationToken.None);

                    Assert.IsTrue(lock1.IsHeld);
                    if (lock1.IsHeld)
                    {
                        Task.WaitAny(new[] { Task.Run(() =>
                    {
                        lock2.Acquire(CancellationToken.None);
                        Assert.IsTrue(lock2.IsHeld);
                    }) }, 1000);
                    }
                }
                finally
                {
                    lock1.Release();
                }

                var lockCheck = new[]
            {
                Task.Run(() =>
                {
                    while (lock1.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                }),
                Task.Run(() =>
                {
                    while (lock2.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                })
            };

                Task.WaitAll(lockCheck, 1000);

                Assert.IsFalse(lock1.IsHeld);
                Assert.IsFalse(lock2.IsHeld);
            }
            finally
            {
                Assert.IsTrue(client.Session.Destroy(sessionId).Response);
            }
        }

        [TestMethod]
        public void Lock_SemaphoreConflict()
        {
            var client = new Client();

            const string keyName = "test/lock/semaphoreconflict";

            var semaphore = client.Semaphore(keyName, 2);

            semaphore.Acquire(CancellationToken.None);

            Assert.IsTrue(semaphore.IsHeld);

            var lockKey = client.CreateLock(keyName + "/.lock");

            try
            {
                lockKey.Acquire(CancellationToken.None);
            }
            catch (LockConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(LockConflictException));
            }

            try
            {
                lockKey.Destroy();
            }
            catch (LockConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(LockConflictException));
            }

            semaphore.Release();
            semaphore.Destroy();
        }

        [TestMethod]
        public void Lock_ForceInvalidate()
        {
            var client = new Client();

            const string keyName = "test/lock/forceinvalidate";

            var lockKey = client.CreateLock(keyName);
            try
            {
                lockKey.Acquire(CancellationToken.None);

                Assert.IsTrue(lockKey.IsHeld);

                var checker = Task.Run(() =>
                {
                    while (lockKey.IsHeld)
                    {
                        Task.Delay(10).Wait();
                    }
                });

                Task.Run(() => { client.Session.Destroy(lockKey.LockSession); });

                Task.WaitAny(new[] { checker }, 1000);

                Assert.IsFalse(lockKey.IsHeld);
            }
            finally
            {
                try
                {
                    lockKey.Release();
                    lockKey.Destroy();
                }
                catch (LockNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(LockNotHeldException));
                }
            }
        }

        [TestMethod]
        public void Lock_DeleteKey()
        {
            var client = new Client();

            const string keyName = "test/lock/deletekey";

            var lockKey = client.CreateLock(keyName);
            try
            {
                lockKey.Acquire(CancellationToken.None);

                Assert.IsTrue(lockKey.IsHeld);

                var checker = Task.Run(() =>
                {
                    while (lockKey.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                    Assert.IsFalse(lockKey.IsHeld);
                });

                Task.WaitAny(new[] { checker }, 1000);

                client.KV.Delete(lockKey.Opts.Key);
            }
            finally
            {
                try
                {
                    lockKey.Release();
                    lockKey.Destroy();
                }
                catch (LockNotHeldException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(LockNotHeldException));
                }
            }
        }
    }
}