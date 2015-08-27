# Changelog

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
