# Changelog

## BREAKING CHANGES FOR 0.6.x
* ___THE ENTIRE CLIENT HAS BEEN REWRITTEN TO BE ASYNC___. This means
  that any sync calls will need to be reworked to either call the Async
  API with `GetAwaiter().GetResult()` or, better yet, the calling method
  needs to be `async` as well and then simply `await` the call.
* The `Client` class has been renamed `ConsulClient`. The interface
  remains the same - `IConsulClient`.
* The `ConsulClientConfiguration` class no longer accepts a string
  `Address` property to the Consul server. It is now a `System.Uri`
  named `Address`.
* `ConsulClient` is now `IDisposable` and should have `Dispose()` called to
  clean it up. It is still supposed to be used in a long-lived fashion, though.

## 2016-02-09
* Implemented the IDisposable Pattern for the `ConsulClient` class.
  `ConsulClient` objects should now have `Dispose()` called on them to properly
  clean up. Thanks @TMaster!
* Cleaned up the Prepared Queries endpoint stack.
* Fixed a timing bug in one of the client execute calls.
* Added Docker checks
* Added the ability for Semaphores and Locks to ride out brief periods of
  failure using the `MonitorRetries` and `MonitorRetryTime` fields in
  `LockOptions` and `SemaphoreOptions` classes.
* Added the ability for Semaphores and Locks to have configureable WaitTime
  values, as well as to operate in `TryOnce` mode, which means it attempts to
  acquire once and throws an exception if the acquisition was not successful.
  To use these, set the `LockWaitTime` and `LockTryOnce` fields on the
  `LockOptions` class and the `SemaphoreWaitTime` and `SemaphoreTryOnce` fields
  on the `SemaphoreOptions` class.

## 2016-02-07
* Reduce the callstack and task overhead by returning the originating
  Task where possible. Thanks @TMaster!

## 2016-01-12
* Rewrote entire API to be `async`.
* Added Prepared Queries from Consul 0.6.0.

## 2015-11-21
* Reworked the entire Client class to use `System.Net.HttpClient` as its
  underpinning rather than `WebRequest`/`WebResponse`.
* Moved all tests to Xunit.
* Converted all uses of `System.Web.HttpUtility.UrlPathEncode` to
  `System.Uri.EscapeDataString`.

## 2015-11-09

* Added coordinates API. *WARNING*: No check is done to see if the API
  is available. If used against a 0.5.2 agent, an exception will be
  thrown because the agent does not understand the coordinate URL.
* Fixed bug in tests for session renewal.

## 2015-11-03

* Fixed a bug where the node name was not deserialized when using the
  `Catalog.Nodes()` endpoint. Thanks @lockwobr!
* Fixed a bug where a zero timespan could not be specified for Lock
  Delays, TTLs or Check Intervals. Thanks @eugenyshchelkanov!

## 2015-10-24

* Port in changes from hashicorp/consul master:
  * Add TCP check type
  * BEHAVIOR CHANGE: Changed Session.Renew() to now throw a
    SessionExpiredException when the session does not exist on the
    Consul server
  * BEHAVIOR CHANGE: Changed all the KV write methods (Put, Delete,
    DeleteTree, DeleteCAS, CAS, Release, Acquire) to throw an
    InvalidKeyPairException if the key path or prefix begins with a `/`
    character.
* Fixed documentation typos.

## 2015-08-27

* Convert all uses of
  [System.Web.HttpUtility.UrlEncode](https://msdn.microsoft.com/en-us/library/system.web.httputility.urlencode)
  and corresponding UrlDecode calls to
  [UrlPathEncode](https://msdn.microsoft.com/en-us/library/system.web.httputility.urlpathencode)/Decode.
  This is was because UrlEncode encodes spaces as a `+` symbol rather
  than the hex `%20` as expected.

## 2015-08-26

* Fix a NullReferenceException when the Consul connection is down and
  the WebException returned has an empty response.

## 2015-07-25

* BREAKING CHANGE: Renamed `Client` class to `ConsulClient` and `Config`
  to `ConsulClientConfiguration` to reduce confusion.
* Completed major rework of the Client class to remove unneeded type
  parameters from various internal calls
* Added interfaces to all the endpoint classes so that test mocking is
  possible.
