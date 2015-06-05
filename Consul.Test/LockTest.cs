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
            var c = ClientTest.MakeClient();
            var lockKey = c.CreateLock("test/lock");

            try
            {
                lockKey.Release();
            }
            catch (LockNotHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(LockNotHeldException));
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
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
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
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
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }

            Assert.IsFalse(lockKey.IsHeld);
        }

        [TestMethod]
        public void Lock_EphemeralAcquireRelease()
        {
            var c = ClientTest.MakeClient();
            var s = c.Session.Create(new SessionEntry { Behavior = SessionBehavior.Delete });
            using (var l = c.AcquireLock(new LockOptions("test/ephemerallock") { Session = s.Response }, CancellationToken.None))
            {
                Assert.IsTrue(l.IsHeld);
                c.Session.Destroy(s.Response);
            }
            Assert.IsNull(c.KV.Get("test/ephemerallock").Response);
        }
        [TestMethod]
        public void Lock_AcquireWaitRelease()
        {
            var lockOptions = new LockOptions("test/lock")
            {
                SessionName = "test_locksession",
                SessionTTL = TimeSpan.FromSeconds(10)
            };
            var c = ClientTest.MakeClient();

            var l = c.CreateLock(lockOptions);

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
            var c = ClientTest.MakeClient();
            const string key = "test/lock";

            var acquired = new bool[3];

            var acquireTasks = new Task[3];

            for (var i = 0; i < 3; i++)
            {
                var v = i;
                acquireTasks[i] = new Task(() =>
                {
                    var lockKey = c.CreateLock(key);
                    lockKey.Acquire(CancellationToken.None);
                    acquired[v] = lockKey.IsHeld;
                    if (lockKey.IsHeld)
                    {
                        Debug.WriteLine("Contender {0} acquired", v);
                    }
                    Task.Delay(1000).Wait();
                    lockKey.Release();
                });
                acquireTasks[i].Start();
            }

            Task.WaitAll(acquireTasks, (int)(3 * Lock.DefaultLockRetryTime.TotalMilliseconds));

            foreach (var item in acquired)
            {
                Assert.IsTrue(item);
            }
        }

        [TestMethod]
        public void Lock_Contend_LockDelay()
        {
            var c = ClientTest.MakeClient();
            const string key = "test/lock";

            var acquired = new bool[3];

            var acquireTasks = new Task[3];

            for (var i = 0; i < 3; i++)
            {
                var v = i;
                acquireTasks[i] = new Task(() =>
                {
                    var lockKey = c.CreateLock(key);
                    lockKey.Acquire(CancellationToken.None);
                    acquired[v] = lockKey.IsHeld;
                    if (lockKey.IsHeld)
                    {
                        Debug.WriteLine("Contender {0} acquired", v);
                    }
                    c.Session.Destroy(lockKey.LockSession);
                });
                acquireTasks[i].Start();
            }

            Task.WaitAll(acquireTasks, (int)(4 * Lock.DefaultLockWaitTime.TotalMilliseconds));

            foreach (var item in acquired)
            {
                Assert.IsTrue(item);
            }
        }
        [TestMethod]
        public void Lock_Destroy()
        {
            var c = ClientTest.MakeClient();
            var key = "test/lock";
            var lockKey = c.CreateLock(key);
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

                var lockKey2 = c.CreateLock(key);

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
                catch (Exception ex)
                {
                    Assert.Fail(ex.ToString());
                }
            }
        }

        [TestMethod]
        public void Lock_RunAction()
        {
            var c = ClientTest.MakeClient();
            Task.WaitAll(Task.Run(() =>
            {
                c.ExecuteLocked("test/lock", () =>
                {
                    // Only executes if the lock is held
                    Debug.WriteLine("Contender {0} acquired", 1);
                    Assert.IsTrue(true);
                });
            }),
            Task.Run(() =>
            {
                c.ExecuteLocked("test/lock", () =>
                {
                    // Only executes if the lock is held
                    Debug.WriteLine("Contender {0} acquired", 2);
                    Assert.IsTrue(true);
                });
            }));
        }
        [TestMethod]
        public void Lock_AbortAction()
        {
            var c = ClientTest.MakeClient();
            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    string ls = c.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) }).Response;
                    c.Session.RenewPeriodic(TimeSpan.FromSeconds(10), ls, cts.Token);
                    Task.Delay(1000).ContinueWith((t1) => { c.Session.Destroy(ls); });
                    c.ExecuteAbortableLocked(new LockOptions("test/lock") { Session = ls }, CancellationToken.None, () =>
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
                string ls = c.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) }).Response;
                c.Session.RenewPeriodic(TimeSpan.FromSeconds(10), ls, cts.Token);
                c.ExecuteAbortableLocked(new LockOptions("test/lock") { Session = ls }, CancellationToken.None, () =>
                {
                    Thread.Sleep(1000);
                    Assert.IsTrue(true);
                });
                cts.Cancel();
            }
        }
        [TestMethod]
        public void Lock_ReclaimLock()
        {
            var c = ClientTest.MakeClient();
            var sessReq = c.Session.Create();
            var sess = sessReq.Response;

            var lockOpts = c.CreateLock(new LockOptions("test/lock")
            {
                Session = sess
            });

            var lockKey2 = c.CreateLock(new LockOptions("test/lock")
            {
                Session = sess
            });

            try
            {
                lockOpts.Acquire(CancellationToken.None);

                Assert.IsTrue(lockOpts.IsHeld);

                Task.Run(() =>
                {
                    Debugger.Break();
                    lockKey2.Acquire(CancellationToken.None);
                });

                var lock2Hold = new Task(() =>
                {
                    while (!lockKey2.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                });
                lock2Hold.Start();

                Task.WaitAny(new[] { lock2Hold }, 1000);

                Assert.IsTrue(lockKey2.IsHeld);
            }
            finally
            {
                try
                {
                    lockOpts.Release();
                }
                catch (Exception ex)
                {
                    Assert.Fail(ex.ToString());
                }
            }

            var lockCheck = new[]
            {
                new Task(() =>
                {
                    while (lockOpts.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                }),
                new Task(() =>
                {
                    while (lockKey2.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                })
            };

            foreach (var item in lockCheck)
            {
                item.Start();
            }

            Task.WaitAll(lockCheck, 1000);

            Assert.IsFalse(lockOpts.IsHeld);
            Assert.IsFalse(lockKey2.IsHeld);
            c.Session.Destroy(sess);
        }

        [TestMethod]
        public void Lock_SemaphoreConflict()
        {
            var c = ClientTest.MakeClient();
            var sema = c.Semaphore("test/lock", 2);

            sema.Acquire(CancellationToken.None);

            Assert.IsTrue(sema.IsHeld);

            var lockKey = c.CreateLock("test/lock/.lock");
            try
            {
                lockKey.Acquire(CancellationToken.None);
            }
            catch (LockConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(LockConflictException));
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }
            try
            {
                lockKey.Destroy();
            }
            catch (LockConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(LockConflictException));
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }
            sema.Release();
        }
        [TestMethod]
        public void Lock_ForceInvalidate()
        {
            var c = ClientTest.MakeClient();
            var lockKey = c.CreateLock("test/lock");
            try
            {
                lockKey.Acquire(CancellationToken.None);

                Assert.IsTrue(lockKey.IsHeld);

                Task.Run(() => { c.Session.Destroy(lockKey.LockSession); });

                var checker = new Task(() =>
                {
                    while (lockKey.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                });

                checker.Start();

                Task.WaitAny(new[] { checker }, 1000);

                Assert.IsFalse(lockKey.IsHeld);
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
                catch (Exception ex)
                {
                    Assert.Fail(ex.ToString());
                }
            }
        }

        [TestMethod]
        public void Lock_DeleteKey()
        {
            var c = ClientTest.MakeClient();
            var lockKey = c.CreateLock("test/lock");
            try
            {
                lockKey.Acquire(CancellationToken.None);

                Assert.IsTrue(lockKey.IsHeld);

                c.KV.Delete(lockKey.Opts.Key);

                var checker = new Task(() =>
                {
                    while (lockKey.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                });

                checker.Start();

                Task.WaitAny(new[] { checker }, 1000);

                Assert.IsFalse(lockKey.IsHeld);
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
                catch (Exception ex)
                {
                    Assert.Fail(ex.ToString());
                }
            }
        }
    }
}