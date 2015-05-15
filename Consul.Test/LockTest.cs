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
            var lockKey = c.Lock("test/lock");

            try
            {
                lockKey.Release();
            }
            catch (LockNotHeldException ex)
            {
                Assert.IsInstanceOfType(ex, typeof (LockNotHeldException));
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
                Assert.IsInstanceOfType(ex, typeof (LockHeldException));
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
                Assert.IsInstanceOfType(ex, typeof (LockNotHeldException));
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }

            Assert.IsFalse(lockKey.IsHeld);
        }

        [TestMethod]
        public void Lock_Contend()
        {
            var c = ClientTest.MakeClient();
            var key = "test/lock";

            var acquired = new bool[3];

            var acquireTasks = new Task[3];

            for (var i = 0; i < 3; i++)
            {
                var v = i;
                acquireTasks[i] = new Task(() =>
                {
                    var lockKey = c.Lock(key);
                    lockKey.Acquire(CancellationToken.None);
                    acquired[v] = lockKey.IsHeld;
                    if (lockKey.IsHeld)
                    {
                        Debug.WriteLine("Contender {0} acquired", v);
                    }
                    lockKey.Release();
                });
                acquireTasks[i].Start();
            }

            Task.WaitAll(acquireTasks, (int) (3*Lock.DefaultLockRetryTime.TotalMilliseconds));

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
            var lockKey = c.Lock(key);
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
                    Assert.IsInstanceOfType(ex, typeof (LockHeldException));
                }

                lockKey.Release();

                Assert.IsFalse(lockKey.IsHeld);

                var lockKey2 = c.Lock(key);

                lockKey2.Acquire(CancellationToken.None);

                Assert.IsTrue(lockKey2.IsHeld);

                try
                {
                    lockKey.Destroy();
                    Assert.Fail();
                }
                catch (LockInUseException ex)
                {
                    Assert.IsInstanceOfType(ex, typeof (LockInUseException));
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
                    Assert.IsInstanceOfType(ex, typeof (LockNotHeldException));
                }
                catch (Exception ex)
                {
                    Assert.Fail(ex.ToString());
                }
            }
        }

        [TestMethod]
        public void Lock_SemaphoreConflict()
        {
            var c = ClientTest.MakeClient();
            var sema = c.Semaphore("test/lock", 2);

            sema.Acquire(CancellationToken.None);

            Assert.IsTrue(sema.IsHeld);

            var lockKey = c.Lock("test/lock/.lock");
            try
            {
                lockKey.Acquire(CancellationToken.None);
            }
            catch (LockConflictException ex)
            {
                Assert.IsInstanceOfType(ex, typeof (LockConflictException));
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
                Assert.IsInstanceOfType(ex, typeof (LockConflictException));
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }
            sema.Release();
        }

        [TestMethod]
        public void Lock_ReclaimLock()
        {
            var c = ClientTest.MakeClient();
            var sessReq = c.Session.Create();
            sessReq.Wait();
            var sess = sessReq.Result.Response;

            var lockOpts = c.Lock(new LockOptions("test/lock")
            {
                Session = sess
            });

            var lockKey2 = c.Lock(new LockOptions("test/lock")
            {
                Session = sess
            });

            try
            {
                lockOpts.Acquire(CancellationToken.None);

                Assert.IsTrue(lockOpts.IsHeld);

                Task.Run(() => lockKey2.Acquire(CancellationToken.None));

                var lock2Hold = new Task(() =>
                {
                    while (!lockKey2.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                });
                lock2Hold.Start();

                Task.WaitAny(new[] {lock2Hold}, 1000);

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
            c.Session.Destroy(sess).Wait();
        }

        [TestMethod]
        public void Lock_ForceInvalidate()
        {
            var c = ClientTest.MakeClient();
            var lockKey = c.Lock("test/lock");
            try
            {
                lockKey.Acquire(CancellationToken.None);

                Assert.IsTrue(lockKey.IsHeld);

                Task.Run(() => { c.Session.Destroy(lockKey.LockSession).Wait(); });

                var checker = new Task(() =>
                {
                    while (lockKey.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                });

                checker.Start();

                Task.WaitAny(new[] {checker}, 1000);

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
                    Assert.IsInstanceOfType(ex, typeof (LockNotHeldException));
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
            var lockKey = c.Lock("test/lock");
            try
            {
                lockKey.Acquire(CancellationToken.None);

                Assert.IsTrue(lockKey.IsHeld);

                c.KV.Delete(lockKey.Opts.Key).Wait();

                var checker = new Task(() =>
                {
                    while (lockKey.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                });

                checker.Start();

                Task.WaitAny(new[] {checker}, 1000);

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
                    Assert.IsInstanceOfType(ex, typeof (LockNotHeldException));
                }
                catch (Exception ex)
                {
                    Assert.Fail(ex.ToString());
                }
            }
        }
    }
}