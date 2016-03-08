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
using Xunit;

namespace Consul.Test
{
    [Trait("speed", "slow")]
    public class SemaphoreTest
    {
        [Fact]
        public void Semaphore_BadLimit()
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
            semaphore1.Acquire(CancellationToken.None);

            try
            {
                var semaphore2 = client.Semaphore(keyName, 2);
                semaphore2.Acquire(CancellationToken.None);
            }
            catch (SemaphoreLimitConflictException ex)
            {
                Assert.IsType<SemaphoreLimitConflictException>(ex);
                Assert.Equal(1, ex.RemoteLimit);
                Assert.Equal(2, ex.LocalLimit);
            }

            try
            {
                semaphore1.Release();
                semaphore1.Destroy();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsType<SemaphoreNotHeldException>(ex);
            }

            Assert.False(semaphore1.IsHeld);
        }
        [Fact]
        public void Semaphore_AcquireRelease()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/acquirerelease";

            var semaphore = client.Semaphore(keyName, 2);

            try
            {
                semaphore.Release();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsType<SemaphoreNotHeldException>(ex);
            }

            semaphore.Acquire(CancellationToken.None);

            Assert.True(semaphore.IsHeld);

            try
            {
                semaphore.Acquire(CancellationToken.None);
            }
            catch (SemaphoreHeldException ex)
            {
                Assert.IsType<SemaphoreHeldException>(ex);
            }

            Assert.True(semaphore.IsHeld);

            semaphore.Release();

            try
            {
                semaphore.Release();
            }
            catch (SemaphoreNotHeldException ex)
            {
                Assert.IsType<SemaphoreNotHeldException>(ex);
            }

            Assert.False(semaphore.IsHeld);
        }

        [Fact]
        public void Semaphore_OneShot()
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

            semaphorekey.Acquire(CancellationToken.None);

            var another = client.Semaphore(new SemaphoreOptions(keyName, 2)
            {
                SemaphoreTryOnce = true,
                SemaphoreWaitTime = TimeSpan.FromMilliseconds(250)
            });

            another.Acquire();

            var contender = client.Semaphore(new SemaphoreOptions(keyName, 2)
            {
                SemaphoreTryOnce = true,
                SemaphoreWaitTime = TimeSpan.FromMilliseconds(250)
            });

            Task.WaitAny(Task.Run(() =>
            {
                Assert.Throws<SemaphoreMaxAttemptsReachedException>(() =>
                contender.Acquire()
                );
            }),
            Task.Delay(2 * semaphoreOptions.SemaphoreWaitTime.Milliseconds).ContinueWith((t) => Assert.True(false, "Took too long"))
            );

            semaphorekey.Release();
            another.Release();
            contender.Destroy();
        }

        [Fact]
        public void Semaphore_Disposable()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/disposable";
            using (var semaphore = client.AcquireSemaphore(keyName, 2))
            {
                Assert.True(semaphore.IsHeld);
            }
        }
        [Fact]
        public void Semaphore_ExecuteAction()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/action";
            client.ExecuteInSemaphore(keyName, 2, () => Assert.True(true));
        }
        [Fact]
        public void Semaphore_AcquireWaitRelease()
        {

            var client = new ConsulClient();

            const string keyName = "test/semaphore/acquirewaitrelease";

            var semaphoreOptions = new SemaphoreOptions(keyName, 1)
            {
                SessionName = "test_semaphoresession",
                SessionTTL = TimeSpan.FromSeconds(10), MonitorRetries = 10
            };

            var semaphore = client.Semaphore(semaphoreOptions);

            semaphore.Acquire(CancellationToken.None);

            Assert.True(semaphore.IsHeld);

            // Wait for multiple renewal cycles to ensure the semaphore session stays renewed.
            Task.Delay(TimeSpan.FromSeconds(60)).Wait();
            Assert.True(semaphore.IsHeld);

            semaphore.Release();

            Assert.False(semaphore.IsHeld);

            semaphore.Destroy();
        }

        [Fact]
        public void Semaphore_ContendWait()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/contend";
            const int contenderPool = 4;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter((contenderPool - 1) * (int)Semaphore.DefaultSemaphoreWaitTime.TotalMilliseconds);

                Parallel.For(0, contenderPool, new ParallelOptions { MaxDegreeOfParallelism = contenderPool, CancellationToken = cts.Token }, (v) =>
                {
                    var semaphore = client.Semaphore(keyName, 2);
                    semaphore.Acquire(CancellationToken.None);
                    acquired[v] = semaphore.IsHeld;
                    Task.Delay(1000).Wait();
                    semaphore.Release();
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
        public void Semaphore_ContendFast()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/contend";
            const int contenderPool = 15;

            var acquired = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter((contenderPool - 1) * (int)Semaphore.DefaultSemaphoreWaitTime.TotalMilliseconds);

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
                    Assert.True(acquired[i]);
                }
                else
                {
                    Assert.True(false, "Contender " + i.ToString() + " did not acquire the lock");
                }
            }
        }

        [Fact]
        public void Semaphore_Destroy()
        {
            var c = new ConsulClient();

            const string keyName = "test/semaphore/destroy";

            var semaphore1 = c.Semaphore(keyName, 2);
            var semaphore2 = c.Semaphore(keyName, 2);
            try
            {
                semaphore1.Acquire(CancellationToken.None);
                Assert.True(semaphore1.IsHeld);
                semaphore2.Acquire(CancellationToken.None);
                Assert.True(semaphore2.IsHeld);

                try
                {
                    semaphore1.Destroy();
                    Assert.True(false);
                }
                catch (SemaphoreHeldException ex)
                {
                    Assert.IsType<SemaphoreHeldException>(ex);
                }

                semaphore1.Release();
                Assert.False(semaphore1.IsHeld);

                try
                {
                    semaphore1.Destroy();
                    Assert.True(false);
                }
                catch (SemaphoreInUseException ex)
                {
                    Assert.IsType<SemaphoreInUseException>(ex);
                }

                semaphore2.Release();
                Assert.False(semaphore2.IsHeld);
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
                    Assert.IsType<SemaphoreNotHeldException>(ex);
                }
                try
                {
                    semaphore2.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsType<SemaphoreNotHeldException>(ex);
                }
            }
        }

        [Fact]
        public void Semaphore_ForceInvalidate()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/forceinvalidate";

            var semaphore = (Semaphore)client.Semaphore(keyName, 2);

            try
            {
                semaphore.Acquire(CancellationToken.None);

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
                semaphore.Acquire(CancellationToken.None);

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
                    semaphore.Release();
                }
                catch (SemaphoreNotHeldException ex)
                {
                    Assert.IsType<SemaphoreNotHeldException>(ex);
                }
            }
        }

        [Fact]
        public void Semaphore_Conflict()
        {
            var client = new ConsulClient();

            const string keyName = "test/semaphore/conflict";

            var semaphoreLock = client.CreateLock(keyName + "/.lock");

            semaphoreLock.Acquire(CancellationToken.None);

            Assert.True(semaphoreLock.IsHeld);

            var semaphore = client.Semaphore(keyName, 2);

            try
            {
                semaphore.Acquire(CancellationToken.None);
            }
            catch (SemaphoreConflictException ex)
            {
                Assert.IsType<SemaphoreConflictException>(ex);
            }

            try
            {
                semaphore.Destroy();
            }
            catch (SemaphoreConflictException ex)
            {
                Assert.IsType<SemaphoreConflictException>(ex);
            }

            semaphoreLock.Release();

            Assert.False(semaphoreLock.IsHeld);

            semaphoreLock.Destroy();
        }
    }
}
