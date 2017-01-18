# Consul.NET

[![Build Status](https://ci.appveyor.com/api/projects/status/github/PlayFab/consuldotnet?branch=master&svg=true)](https://ci.appveyor.com/project/highlyunavailable/consuldotnet)

* Consul API: [v0.7.2](https://github.com/hashicorp/consul/tree/v0.7.2/api)
* .NET: >= 4.5 - .NET Core: >= 1.0.0

Consul.NET is a .NET port of the Go Consul API, but reworked to use .NET
idioms such as Tasks/CancellationTokens instead of Goroutines/Channels.
The majority of the calls directly track the [HTTP
API](https://www.consul.io/docs/agent/http.html), but this API does have
additional functionality that is provided in the Go API, like Locks and
Semaphores.

## Example

You'll need a running Consul Server on your local machine, or a Consul
Agent connected to a Consul Server cluster. To run a local server:

1. [Download a copy](https://www.consul.io/downloads.html) of the latest Windows
version and unzip it into the `Consul.Test` folder.
2. Open a command prompt and `cd` to the `Consul.Test` folder.
3. Run `.\consul.exe agent -dev -config-file test_config.json`

This creates a 1-server cluster that operates in "dev" mode (does not
write data to disk) and listens on `127.0.0.1:8500`.

Once Consul is running (you'll see something like `consul: cluster
leadership acquired`) in your command prompt, then do the following
steps in your project.

Add a reference to the Consul library and add a using statement:

```csharp
using Consul;
```

Write a function to talk to the KV store:

```csharp
public static async Task<string> HelloConsul()
{
    using (var client = new ConsulClient())
    {
        var putPair = new KVPair("hello")
        {
            Value = Encoding.UTF8.GetBytes("Hello Consul")
        };

        var putAttempt = await client.KV.Put(putPair);

        if (putAttempt.Response)
        {
            var getPair = await client.KV.Get("hello");
            return Encoding.UTF8.GetString(getPair.Response.Value, 0,
                getPair.Response.Value.Length);
        }
        return "";
    }
}
```

And call it:

```csharp
Console.WriteLine(HelloConsul().GetAwaiter().GetResult());
```

You should see `Hello Consul` in the output of your program. You should
also see the following lines in your command prompt, if you're running
a local Consul server:

```
[DEBUG] http: Request /v1/kv/hello (6.0039ms)
[DEBUG] http: Request /v1/kv/hello (0)
```

The API just went out to Consul, wrote "Hello Consul" under the key
"hello", then fetched the data back out and wrote it to your prompt.

## Usage

All operations are done using a `ConsulClient` object. First,
instantiate a `ConsulClient` object, which connects to `localhost:8500`,
the default Consul HTTP API port. Once you've got a `ConsulClient`
object, various functionality is exposed as properties under the
`ConsulClient`.

All responses are wrapped in `QueryResponse` and `WriteResponse`
classes, which provide metadata about the request, like how long it
took and the monotonic Consul index when the operation occured.

This API also assumes some knowledge of Consul, including things like
[blocking queries and consistency
modes](https://www.consul.io/docs/agent/http.html)

### ACL

The ACL endpoints are used to create, update, destroy, and query ACL tokens.

### Agent

The Agent endpoints are used to interact with the local Consul agent.
Usually, services and checks are registered with an agent which then
takes on the burden of keeping that data synchronized with the cluster.
For example, the agent registers services and checks with the Catalog
and performs anti-entropy to recover from outages.

### Catalog

The Catalog is the endpoint used to register and deregister nodes,
services, and checks. It also provides query endpoints.

### Event

The Event endpoints are used to fire new events and to query the
available events.

### Health

The Health endpoints are used to query health-related information. They
are provided separately from the Catalog since users may prefer not to
use the optional health checking mechanisms. Additionally, some of the
query results from the Health endpoints are filtered while the Catalog
endpoints provide the raw entries.

### KV

The KV endpoint is used to access Consul's simple key/value store,
useful for storing service configuration or other metadata.

### Query

The Prepared Query endpoints are used to create, update, destroy, and
execute prepared queries. Prepared queries allow you to register a
complex service query and then execute it later via its ID or name to
get a set of healthy nodes that provide a given service.

### Session

The Session endpoints are used to create, destroy, and query sessions.

### Status

The Status endpoints are used to get information about the status of the
Consul cluster. This information is generally very low level and not
often useful for clients.

### Additional Functions

Functionality based on the Consul guides using the available primitives
has been implemented as well, just like the Go API.

### Lock

Lock is used to implement client-side leader election for a distributed
lock. It is an implementation of the [Consul Leader
Election](https://consul.io/docs/guides/leader-election.html) guide.

### Semaphore

Semaphore is used to implement a distributed semaphore using the Consul
KV primitives. It is an implementation of the [Consul Semaphore
](https://www.consul.io/docs/guides/semaphore.html) guide.

## Using with .NET Core and Mono

Both .NET 4.5+ and .NET Core 1.0+ are fully supported. Mono is supported on a
best-effort basis. It should compile and run happily on Mono but this is not as
heavily tested as Microsoft .NET stacks. If you have any issues using the Nuget
package or compiling this code with .NET, .NET Core, or Mono, please file a
Github issue with details of the problem.
