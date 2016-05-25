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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Consul.Test
{
    [Trait("speed", "slow")]
    public class SemaphoreTest
    {
        [Fact]
        public async Task Semaphore_BadLimit()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/badlimit";

            try
            {
                client.Semaphore(keyName, 0);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Assert.IsType<ArgumentOutOfRangeException>(ex);
            }

            var semaphore1 = client.Semaphore(keyName, 1);
            await semaphore1.Acquire(CancellationToken.None);

            try
            {
                var semaphore2 = client.Semaphore(keyName, 2);
                await semaphore2.Acquire(CancellationToken.None);
            }
            catch (SemaphoreLimitConflictException ex)
            {
                Assert.IsType<SemaphoreLimitConflictException>(ex);
                Assert.Equal(1, ex.RemoteLimit);
                Assert.Equal(2, ex.LocalLimit);
            }

            try
            {
                await semaphore1.Release();
                await semaphore1.Destroy();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsType<SemaphoreNotHeldException>(ex);
            }

            Assert.False(semaphore1.IsHeld);
        }
        [Fact]
        public async Task Semaphore_AcquireRelease()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/acquirerelease";

            var semaphore = client.Semaphore(keyName, 2);

            try
            {
                await semaphore.Release();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsType<SemaphoreNotHeldException>(ex);
            }

            await semaphore.Acquire(CancellationToken.None);

            Assert.True(semaphore.IsHeld);

            try
            {
                await semaphore.Acquire(CancellationToken.None);
            }
            catch (SemaphoreHeldException ex)
            {
                Assert.IsType<SemaphoreHeldException>(ex);
            }

            Assert.True(semaphore.IsHeld);

            await semaphore.Release();

            try
            {
                await semaphore.Release();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsType<SemaphoreNotHeldException>(ex);
            }

            Assert.False(semaphore.IsHeld);
        }

        [Fact]
        public async Task Semaphore_OneShot()
        {
            var client = new ConsulClient();
            const string keyName = "test/semaphore/oneshot";
            var semaphoreOptions = new SemaphoreOptions(keyName, 2)
            {
                SemaphoreTryOnce = true
            };

            Assert.Equal(Semaphore.DefaultSemaphoreWaitTime, semaphoreOptions.SemaphoreWaitTime);

            semaphoreOptions.SemaphoreWaitTime = TimeSpan.FromMilliseconds(250);

            var semaphorekey = client.Semaphore(semaphoreOptions);

            await semaphorekey.Acquire(CancellationToken.None);

            var another = client.Semaphore(new SemaphoreOptions(keyName, 2)
            {
                SemaphoreTryOnce = true,
                SemaphoreWaitTime = TimeSpan.FromMilliseconds(250)
            });

            await another.Acquire();

            var contender = client.Semaphore(new SemaphoreOptions(keyName, 2)
            {
                SemaphoreTryOnce = true,
                SemaphoreWaitTime = TimeSpan.FromMilliseconds(250)
            });

            Task.WaitAny(Task.Run(async () =>
            {
                await Assert.ThrowsAsync<SemaphoreMaxAttemptsReachedException>(async () =>
                    await contender.Acquire()
                );
            }),
            Task.Delay(2 * semaphoreOptions.SemaphoreWaitTime.Milliseconds).ContinueWith((t) => Assert.True(false, "Took too long"))
            );

            await semaphorekey.Release();
            await another.Release();
            await contender.Destroy();
        }

        [Fact]
        public async Task Semaphore_AcquireSemaphore()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/disposable";
            var semaphore = await client.AcquireSemaphore(keyName, 2);

            try
            {
                Assert.True(semaphore.IsHeld);
            }
            finally
            {
                await semaphore.Release();
            }
        }
        [Fact]
        public async Task Semaphore_ExecuteAction()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/action";
            await client.ExecuteInSemaphore(keyName, 2, () => Assert.True(true));
        }
        [Fact]
        public async Task Semaphore_AcquireWaitRelease()
        {

            var client = new ConsulClient();

            const string keyName = "test/semaphore/acquirewaitrelease";

            var semaphoreOptions = new SemaphoreOptions(keyName, 1)
            {
                SessionName = "test_semaphoresession",
                SessionTTL = TimeSpan.FromSeconds(10), MonitorRetries = 10
            };

            var semaphore = client.Semaphore(semaphoreOptions);

            await semaphore.Acquire(CancellationToken.None);

            Assert.True(semaphore.IsHeld);

            // Wait for multiple renewal cycles to ensure the semaphore session stays renewed.
            await Task.Delay(TimeSpan.FromSeconds(60));
            Assert.True(semaphore.IsHeld);

            await semaphore.Release();

            Assert.False(semaphore.IsHeld);

            await semaphore.Destroy();
        }

        [Fact]
        public async Task Semaphore_ContendWait()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/contend";
            const int contenderPool = 4;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter((contenderPool - 1) * (int)Semaphore.DefaultSemaphoreWaitTime.TotalMilliseconds);

                var tasks = new List<Task>();
                for (var i = 0; i < contenderPool; i++)
                {
                    var v = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        var semaphore = client.Semaphore(keyName, 2);
                        await semaphore.Acquire(CancellationToken.None);
                        acquired[v] = semaphore.IsHeld;
                        await Task.Delay(1000);
                        await semaphore.Release();
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
        public async Task Semaphore_ContendFast()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/contend";
            const int contenderPool = 15;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter((contenderPool - 1) * (int)Semaphore.DefaultSemaphoreWaitTime.TotalMilliseconds);

                var tasks = new List<Task>();
                for (var i = 0; i < contenderPool; i++)
                {
                    var v = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        var semaphore = client.Semaphore(keyName, 2);
                        await semaphore.Acquire(CancellationToken.None);
                        acquired[v] = semaphore.IsHeld;
                        await semaphore.Release();
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
        public async Task Semaphore_Destroy()
        {
            var c = new ConsulClient();

            const string keyName = "test/semaphore/destroy";

            var semaphore1 = c.Semaphore(keyName, 2);
            var semaphore2 = c.Semaphore(keyName, 2);
            try
            {
                await semaphore1.Acquire(CancellationToken.None);
                Assert.True(semaphore1.IsHeld);
                await semaphore2.Acquire(CancellationToken.None);
                Assert.True(semaphore2.IsHeld);

                try
                {
                    await semaphore1.Destroy();
                    Assert.True(false);
                }
                catch (SemaphoreHeldException ex)
                {
                    Assert.IsType<SemaphoreHeldException>(ex);
                }

                await semaphore1.Release();
                Assert.False(semaphore1.IsHeld);

                try
                {
                    await semaphore1.Destroy();
                    Assert.True(false);
                }
                catch (SemaphoreInUseException ex)
                {
                    Assert.IsType<SemaphoreInUseException>(ex);
                }

                await semaphore2.Release();
                Assert.False(semaphore2.IsHeld);
                await semaphore1.Destroy();
                await semaphore2.Destroy();
            }
            finally
            {
                try
                {
                    await semaphore1.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsType<SemaphoreNotHeldException>(ex);
                }
                try
                {
                    await semaphore2.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsType<SemaphoreNotHeldException>(ex);
                }
            }
        }

        [Fact]
        public async Task Semaphore_ForceInvalidate()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/forceinvalidate";

            var semaphore = (Semaphore)client.Semaphore(keyName, 2);

            try
            {
                await semaphore.Acquire(CancellationToken.None);

                Assert.True(semaphore.IsHeld);

                var checker = Task.Run(() =>
                {
                    while (semaphore.IsHeld)
                    {
                        Thread.Sleep(10);
                    }

                    Assert.False(semaphore.IsHeld);
                });

                Task.WaitAny(new[] { checker }, 1000);

                await client.Session.Destroy(semaphore.LockSession);
            }
            finally
            {
                try
                {
                    await semaphore.Release();
                    await semaphore.Destroy();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsType<SemaphoreNotHeldException>(ex);
                }
            }
        }

        [Fact]
        public async Task Semaphore_DeleteKey()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/deletekey";

            var semaphore = (Semaphore)client.Semaphore(keyName, 2);

            try
            {
                await semaphore.Acquire(CancellationToken.None);

                Assert.True(semaphore.IsHeld);

                var checker = Task.Run(() =>
                {
                    while (semaphore.IsHeld)
                    {
                        Thread.Sleep(10);
                    }

                    Assert.False(semaphore.IsHeld);
                });

                Task.WaitAny(new[] { checker }, 1000);

                var req = await client.KV.DeleteTree(semaphore.Opts.Prefix);
                Assert.True(req.Response);
            }
            finally
            {
                try
                {
                    await semaphore.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsType<SemaphoreNotHeldException>(ex);
                }
            }
        }

        [Fact]
        public async Task Semaphore_Conflict()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/conflict";

            var semaphoreLock = client.CreateLock(keyName + "/.lock");

            await semaphoreLock.Acquire(CancellationToken.None);

            Assert.True(semaphoreLock.IsHeld);

            var semaphore = client.Semaphore(keyName, 2);

            try
            {
                await semaphore.Acquire(CancellationToken.None);
            }
            catch (SemaphoreConflictException ex)
            {
                Assert.IsType<SemaphoreConflictException>(ex);
            }

            try
            {
                await semaphore.Destroy();
            }
            catch (SemaphoreConflictException ex)
            {
                Assert.IsType<SemaphoreConflictException>(ex);
            }

            await semaphoreLock.Release();

            Assert.False(semaphoreLock.IsHeld);

            await semaphoreLock.Destroy();
        }
    }
}
