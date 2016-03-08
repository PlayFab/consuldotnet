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
using Xunit;

namespace Consul.Test
{
    [Trait("speed", "slow")]
    public class LockTest
    {
        [Fact]
        public void Lock_AcquireRelease()
        {
            var client = new ConsulClient();
            const string keyName = "test/lock/acquirerelease";
            var lockKey = client.CreateLock(keyName);

            try
            {
                lockKey.Release();
            }
            catch (LockNotHeldException ex)
            {
                Assert.IsType<LockNotHeldException>(ex);
            }

            lockKey.Acquire(CancellationToken.None);

            try
            {
                lockKey.Acquire(CancellationToken.None);
            }
            catch (LockHeldException ex)
            {
                Assert.IsType<LockHeldException>(ex);
            }

            Assert.True(lockKey.IsHeld);

            lockKey.Release();

            try
            {
                lockKey.Release();
            }
            catch (LockNotHeldException ex)
            {
                Assert.IsType<LockNotHeldException>(ex);
            }

            Assert.False(lockKey.IsHeld);
        }

        [Fact]
        public void Lock_OneShot()
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

            lockKey.Acquire(CancellationToken.None);

            var contender = client.CreateLock(new LockOptions(keyName)
            {
                LockTryOnce = true,
                LockWaitTime = TimeSpan.FromMilliseconds(250)
            });

            Task.WaitAny(Task.Run(() =>
            {
                Assert.Throws<LockMaxAttemptsReachedException>(() => 
                contender.Acquire()
                );
            }),
            Task.Delay(2 * lockOptions.LockWaitTime.Milliseconds).ContinueWith((t) => Assert.True(false, "Took too long"))
            );

            lockKey.Release();

            contender.Acquire();
            contender.Release();
            contender.Destroy();
        }

        [Fact]
        public async Task Lock_EphemeralAcquireRelease()
        {
            var client = new ConsulClient();
            const string keyName = "test/lock/ephemerallock";
            var sessionId = await client.Session.Create(new SessionEntry { Behavior = SessionBehavior.Delete });
            using (var l = client.AcquireLock(new LockOptions(keyName) { Session = sessionId.Response }, CancellationToken.None))
            {
                Assert.True(l.IsHeld);
                await client.Session.Destroy(sessionId.Response);
            }
            Assert.Null((await client.KV.Get(keyName)).Response);
        }

        [Fact]
        public void Lock_Disposable()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/disposable";
            using (var l = client.AcquireLock(keyName))
            {
                Assert.True(l.IsHeld);
            }
        }
        [Fact]
        public void Lock_ExecuteAction()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/action";
            client.ExecuteLocked(keyName, () => Assert.True(true));
        }
        [Fact]
        public void Lock_AcquireWaitRelease()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/acquirewaitrelease";

            var lockOptions = new LockOptions(keyName)
            {
                SessionName = "test_locksession",
                SessionTTL = TimeSpan.FromSeconds(10)
            };

            var l = client.CreateLock(lockOptions);

            l.Acquire(CancellationToken.None);

            Assert.True(l.IsHeld);

            // Wait for multiple renewal cycles to ensure the lock session stays renewed.
            Task.Delay(TimeSpan.FromSeconds(60)).Wait();
            Assert.True(l.IsHeld);

            l.Release();

            Assert.False(l.IsHeld);

            l.Destroy();
        }
        [Fact]
        public void Lock_ContendWait()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/contendwait";
            const int contenderPool = 3;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(contenderPool * (int)Lock.DefaultLockWaitTime.TotalMilliseconds);

                Parallel.For(0, contenderPool, new ParallelOptions { MaxDegreeOfParallelism = contenderPool, CancellationToken = cts.Token }, (v) =>
                {
                    var lockKey = client.CreateLock(keyName);
                    lockKey.Acquire(CancellationToken.None);
                    Assert.True(acquired.TryAdd(v, lockKey.IsHeld));
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
                    Assert.True(acquired[i]);
                }
                else
                {
                    Assert.True(false, "Contender " + i.ToString() + " did not acquire the lock");
                }
            }
        }
        [Fact]
        public void Lock_ContendFast()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/contendfast";
            const int contenderPool = 10;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(contenderPool * (int)Lock.DefaultLockWaitTime.TotalMilliseconds);

                Parallel.For(0, contenderPool, new ParallelOptions { MaxDegreeOfParallelism = contenderPool, CancellationToken = cts.Token }, (v) =>
                {
                    var lockKey = client.CreateLock(keyName);
                    lockKey.Acquire(CancellationToken.None);
                    Assert.True(acquired.TryAdd(v, lockKey.IsHeld));
                    if (lockKey.IsHeld)
                    {
                        lockKey.Release();
                    }
                });
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
        public void Lock_Contend_LockDelay()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/contendlockdelay";

            const int contenderPool = 3;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter((contenderPool + 1) * (int)Lock.DefaultLockWaitTime.TotalMilliseconds);

                Parallel.For(0, contenderPool, new ParallelOptions { MaxDegreeOfParallelism = contenderPool, CancellationToken = cts.Token }, (v) =>
                {
                    var lockKey = (Lock)client.CreateLock(keyName);
                    lockKey.Acquire(CancellationToken.None);
                    if (lockKey.IsHeld)
                    {
                        Assert.True(acquired.TryAdd(v, lockKey.IsHeld));
                        client.Session.Destroy(lockKey.LockSession);
                    }
                });
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
        public void Lock_Destroy()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/contendlockdelay";

            var lockKey = client.CreateLock(keyName);

            try
            {
                lockKey.Acquire(CancellationToken.None);

                Assert.True(lockKey.IsHeld);

                try
                {
                    lockKey.Destroy();
                    Assert.True(false);
                }
                catch (LockHeldException ex)
                {
                    Assert.IsType<LockHeldException>(ex);
                }

                lockKey.Release();

                Assert.False(lockKey.IsHeld);

                var lockKey2 = client.CreateLock(keyName);

                lockKey2.Acquire(CancellationToken.None);

                Assert.True(lockKey2.IsHeld);

                try
                {
                    lockKey.Destroy();
                    Assert.True(false);
                }
                catch (LockInUseException ex)
                {
                    Assert.IsType<LockInUseException>(ex);
                }

                lockKey2.Release();

                Assert.False(lockKey2.IsHeld);

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
        public async Task Lock_AbortAction()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/abort";

            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    string lockSession = (await client.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) })).Response;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    client.Session.RenewPeriodic(TimeSpan.FromSeconds(10), lockSession, cts.Token);
                    Task.Delay(1000).ContinueWith((w) => { client.Session.Destroy(lockSession); });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    client.ExecuteAbortableLocked(new LockOptions(keyName) { Session = lockSession }, CancellationToken.None, () =>
                    {
                        Thread.Sleep(60000);
                    });
                }
                catch (TimeoutException ex)
                {
                    Assert.IsType<TimeoutException>(ex);
                }
                cts.Cancel();
            }
            using (var cts = new CancellationTokenSource())
            {
                string lockSession = (await client.Session.Create(new SessionEntry() { TTL = TimeSpan.FromSeconds(10) })).Response;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                client.Session.RenewPeriodic(TimeSpan.FromSeconds(10), lockSession, cts.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                client.ExecuteAbortableLocked(new LockOptions(keyName) { Session = lockSession }, CancellationToken.None, () =>
                {
                    Task.Delay(1000).ContinueWith((w) => { Assert.True(true); });
                });
                cts.Cancel();
            }
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
                    lock1.Acquire(CancellationToken.None);

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

                Assert.False(lock1.IsHeld);
                Assert.False(lock2.IsHeld);
            }
            finally
            {
                Assert.True((await client.Session.Destroy(sessionId)).Response);
            }
        }

        [Fact]
        public void Lock_SemaphoreConflict()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/semaphoreconflict";

            var semaphore = client.Semaphore(keyName, 2);

            semaphore.Acquire(CancellationToken.None);

            Assert.True(semaphore.IsHeld);

            var lockKey = client.CreateLock(keyName + "/.lock");

            try
            {
                lockKey.Acquire(CancellationToken.None);
            }
            catch (LockConflictException ex)
            {
                Assert.IsType<LockConflictException>(ex);
            }

            try
            {
                lockKey.Destroy();
            }
            catch (LockConflictException ex)
            {
                Assert.IsType<LockConflictException>(ex);
            }

            semaphore.Release();
            semaphore.Destroy();
        }

        [Fact]
        public void Lock_ForceInvalidate()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/forceinvalidate";

            var lockKey = (Lock)client.CreateLock(keyName);
            try
            {
                lockKey.Acquire(CancellationToken.None);

                Assert.True(lockKey.IsHeld);

                var checker = Task.Run(() =>
                {
                    while (lockKey.IsHeld)
                    {
                        Task.Delay(10).Wait();
                    }
                    Assert.False(lockKey.IsHeld);
                });

                Task.Run(() => { client.Session.Destroy(lockKey.LockSession); });

                Task.WaitAny(new[] { checker }, 1000);
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
                    Assert.IsType<LockNotHeldException>(ex);
                }
            }
        }

        [Fact]
        public void Lock_DeleteKey()
        {
            var client = new ConsulClient();

            const string keyName = "test/lock/deletekey";

            var lockKey = (Lock)client.CreateLock(keyName);
            try
            {
                lockKey.Acquire(CancellationToken.None);

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
                    Assert.IsType<LockNotHeldException>(ex);
                }
            }
        }
    }
}