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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    [Trait("speed", "slow")]
    public class LockTest
    {
        [Fact]
        public async Task Lock_AcquireRelease()
        {
            var client = new ConsulClient();
            const string keyName = "test/lock/acquirerelease";
            var lockKey = client.CreateLock(keyName);

            try
            {
                await lockKey.Release();
            }
            catch (LockNotHeldException ex)
            {
                Assert.IsType<LockNotHeldException>(ex);
            }

            await lockKey.Acquire(CancellationToken.None);

            try
            {
                await lockKey.Acquire(CancellationToken.None);
            }
            catch (LockHeldException ex)
            {
                Assert.IsType<LockHeldException>(ex);
            }

            Assert.True(lockKey.IsHeld);

            await lockKey.Release();

            try
            {
                await lockKey.Release();
            }
            catch (LockNotHeldException ex)
            {
                Assert.IsType<LockNotHeldException>(ex);
            }

            Assert.False(lockKey.IsHeld);
        }

        [Fact]
        public async Task Lock_OneShot()
        {
            var client = new ConsulClient();
            const string keyName = "test/lock/oneshot";
            var lockOptions = new LockOptions(keyName)
            {
                LockTryOnce = true
            };

            Assert.Equal(Lock.DefaultLockWaitTime, lockOptions.LockWaitTime);

            lockOptions.LockWaitTime = TimeSpan.FromMilliseconds(250);

            var lockKey = client.CreateLock(lockOptions);

            await lockKey.Acquire(CancellationToken.None);

            var contender = client.CreateLock(new LockOptions(keyName)
            {
                LockTryOnce = true,
                LockWaitTime = TimeSpan.FromMilliseconds(250)
            });

            Task.WaitAny(Task.Run(async () =>
            {
                await Assert.ThrowsAsync<LockMaxAttemptsReachedException>(async () => 
                    await contender.Acquire()
                );
            }),
            Task.Delay(2 * lockOptions.LockWaitTime.Milliseconds).ContinueWith((t) => Assert.True(false, "Took too long"))
            );

            await lockKey.Release();

            await contender.Acquire();
            await contender.Release();
            await contender.Destroy();
        }

        [Fact]
        public async Task Lock_EphemeralAcquireRelease()
        {
            var client = new ConsulClient();
            const string keyName = "test/lock/ephemerallock";
            var sessionId = await client.Session.Create(new SessionEntry { Behavior = SessionBehavior.Delete });

            var l = await client.AcquireLock(new LockOptions(keyName) { Session = sessionId.Response }, CancellationToken.None);
            try
            {
                Assert.True(l.IsHeld);
                await client.Session.Destroy(sessionId.Response);
            }
            finally
            {
                await l.Release();
            }
            Assert.Null((await client.KV.Get(keyName)).Response);
        }

        [Fact]
        public async Task Lock_Disposable()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/disposable";

            var l = await client.AcquireLock(keyName);
            try
            {
                Assert.True(l.IsHeld);
            }
            finally {
                await l.Release();
            }
        }
        [Fact]
        public async Task Lock_ExecuteAction()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/action";
            await client.ExecuteLocked(keyName, () => Assert.True(true));
        }
        [Fact]
        public async Task Lock_AcquireWaitRelease()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/acquirewaitrelease";

            var lockOptions = new LockOptions(keyName)
            {
                SessionName = "test_locksession",
                SessionTTL = TimeSpan.FromSeconds(10)
            };

            var l = client.CreateLock(lockOptions);

            await l.Acquire(CancellationToken.None);

            Assert.True(l.IsHeld);

            // Wait for multiple renewal cycles to ensure the lock session stays renewed.
            Task.Delay(TimeSpan.FromSeconds(60)).Wait();
            Assert.True(l.IsHeld);

            await l.Release();

            Assert.False(l.IsHeld);

            await l.Destroy();
        }
        [Fact]
        public async Task Lock_ContendWait()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/contendwait";
            const int contenderPool = 3;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(contenderPool * (int)Lock.DefaultLockWaitTime.TotalMilliseconds);

                var tasks = new List<Task>();
                for (var i = 0; i < contenderPool; i++)
                {
                    var v = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        var lockKey = client.CreateLock(keyName);
                        await lockKey.Acquire(CancellationToken.None);
                        Assert.True(acquired.TryAdd(v, lockKey.IsHeld));
                        if (lockKey.IsHeld)
                        {
                            await Task.Delay(1000);
                            await lockKey.Release();
                        }
                    }));
                }

                await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(Timeout.Infinite, cts.Token));
            }

            for (var i = 0; i < contenderPool; i++)
            {
                if (acquired[i])
                {
                    Assert.True(acquired[i]);
                }
                else
                {
                    Assert.True(false, "Contender " + i.ToString() + " did not acquire the lock");
                }
            }
        }
        [Fact]
        public async Task Lock_ContendFast()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/contendfast";
            const int contenderPool = 10;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(contenderPool * (int)Lock.DefaultLockWaitTime.TotalMilliseconds);

                var tasks = new List<Task>();
                for (var i = 0; i < contenderPool; i++)
                {
                    var v = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        var lockKey = client.CreateLock(keyName);
                        await lockKey.Acquire(CancellationToken.None);
                        Assert.True(acquired.TryAdd(v, lockKey.IsHeld));
                        if (lockKey.IsHeld)
                        {
                            await lockKey.Release();
                        }
                    }));
                }

                await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(Timeout.Infinite, cts.Token));
            }

            for (var i = 0; i < contenderPool; i++)
            {
                if (acquired[i])
                {
                    Assert.True(acquired[i]);
                }
                else
                {
                    Assert.True(false, "Contender " + i.ToString() + " did not acquire the lock");
                }
            }
        }

        [Fact]
        public async Task Lock_Contend_LockDelay()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/contendlockdelay";

            const int contenderPool = 3;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter((contenderPool + 1) * (int)Lock.DefaultLockWaitTime.TotalMilliseconds);

                var tasks = new List<Task>();
                for (var i = 0; i < contenderPool; i++)
                {
                    var v = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        var lockKey = (Lock)client.CreateLock(keyName);
                        await lockKey.Acquire(CancellationToken.None);
                        if (lockKey.IsHeld)
                        {
                            Assert.True(acquired.TryAdd(v, lockKey.IsHeld));
                            await client.Session.Destroy(lockKey.LockSession);
                        }
                    }));
                }

                await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(Timeout.Infinite, cts.Token));
            }
            for (var i = 0; i < contenderPool; i++)
            {
                bool didContend = false;
                if (acquired.TryGetValue(i, out didContend))
                {
                    Assert.True(didContend);
                }
                else
                {
                    Assert.True(false, "Contender " + i.ToString() + " did not acquire the lock");
                }
            }
        }
        [Fact]
        public async Task Lock_Destroy()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/contendlockdelay";

            var lockKey = client.CreateLock(keyName);

            try
            {
                await lockKey.Acquire(CancellationToken.None);

                Assert.True(lockKey.IsHeld);

                try
                {
                    await lockKey.Destroy();
                    Assert.True(false);
                }
                catch (LockHeldException ex)
                {
                    Assert.IsType<LockHeldException>(ex);
                }

                await lockKey.Release();

                Assert.False(lockKey.IsHeld);

                var lockKey2 = client.CreateLock(keyName);

                await lockKey2.Acquire(CancellationToken.None);

                Assert.True(lockKey2.IsHeld);

                try
                {
                    await lockKey.Destroy();
                    Assert.True(false);
                }
                catch (LockInUseException ex)
                {
                    Assert.IsType<LockInUseException>(ex);
                }

                await lockKey2.Release();

                Assert.False(lockKey2.IsHeld);

                await lockKey.Destroy();
                await lockKey2.Destroy();
            }
            finally
            {
                try
                {
                    await lockKey.Release();
                }
                catch (LockNotHeldException ex)
                {
                    Assert.IsType<LockNotHeldException>(ex);
                }
            }
        }

        [Fact]
        public void Lock_RunAction()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/runaction";

            Task.WaitAll(Task.Run(() =>
            {
                client.ExecuteLocked(keyName, () =>
                {
                    // Only executes if the lock is held
                    Assert.True(true);
                });
            }),
            Task.Run(() =>
            {
                client.ExecuteLocked(keyName, () =>
                {
                    // Only executes if the lock is held
                    Assert.True(true);
                });
            }));
        }

        [Fact]
        public async Task Lock_ReclaimLock()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/reclaim";

            var sessionRequest = await client.Session.Create();
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
                    await lock1.Acquire(CancellationToken.None);

                    Assert.True(lock1.IsHeld);
                    if (lock1.IsHeld)
                    {
                        Task.WaitAny(new[] { Task.Run(() =>
                    {
                        lock2.Acquire(CancellationToken.None);
                        Assert.True(lock2.IsHeld);
                    }) }, 1000);
                    }
                }
                finally
                {
                    await lock1.Release();
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

                Assert.False(lock1.IsHeld);
                Assert.False(lock2.IsHeld);
            }
            finally
            {
                Assert.True((await client.Session.Destroy(sessionId)).Response);
            }
        }

        [Fact]
        public async Task Lock_SemaphoreConflict()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/semaphoreconflict";

            var semaphore = client.Semaphore(keyName, 2);

            await semaphore.Acquire(CancellationToken.None);

            Assert.True(semaphore.IsHeld);

            var lockKey = client.CreateLock(keyName + "/.lock");

            try
            {
                await lockKey.Acquire(CancellationToken.None);
            }
            catch (LockConflictException ex)
            {
                Assert.IsType<LockConflictException>(ex);
            }

            try
            {
                await lockKey.Destroy();
            }
            catch (LockConflictException ex)
            {
                Assert.IsType<LockConflictException>(ex);
            }

            await semaphore.Release();
            await semaphore.Destroy();
        }

        [Fact]
        public async Task Lock_ForceInvalidate()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/forceinvalidate";

            var lockKey = (Lock)client.CreateLock(keyName);
            try
            {
                await lockKey.Acquire(CancellationToken.None);

                Assert.True(lockKey.IsHeld);

                var checker = Task.Run(async () =>
                {
                    while (lockKey.IsHeld)
                    {
                        await Task.Delay(10);
                    }
                    Assert.False(lockKey.IsHeld);
                });

                await Task.Run(() => { client.Session.Destroy(lockKey.LockSession); });

                Task.WaitAny(new[] { checker }, 1000);
            }
            finally
            {
                try
                {
                    await lockKey.Release();
                    await lockKey.Destroy();
                }
                catch (LockNotHeldException ex)
                {
                    Assert.IsType<LockNotHeldException>(ex);
                }
            }
        }

        [Fact]
        public async Task Lock_DeleteKey()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/deletekey";

            var lockKey = (Lock)client.CreateLock(keyName);
            try
            {
                await lockKey.Acquire(CancellationToken.None);

                Assert.True(lockKey.IsHeld);

                var checker = Task.Run(() =>
                {
                    while (lockKey.IsHeld)
                    {
                        Thread.Sleep(10);
                    }
                    Assert.False(lockKey.IsHeld);
                });

                Task.WaitAny(new[] { checker }, 1000);

                await client.KV.Delete(lockKey.Opts.Key);
            }
            finally
            {
                try
                {
                    await lockKey.Release();
                    await lockKey.Destroy();
                }
                catch (LockNotHeldException ex)
                {
                    Assert.IsType<LockNotHeldException>(ex);
                }
            }
        }
    }
}