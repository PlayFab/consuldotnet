# Changelog

## 2015-03-2015

* Fixed a bug where the node name was not deserialized when using the
  `Catalog.Nodes()` endpoint. Thanks @lockwobr!
* Fixed a bug where a zero timespan could not be specified for Lock
  Delays, TTLs or Check Intervals. Thanks @eugenyshchelkanov!

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
