# MaxLib.WebServer

[![.NET
Core](https://github.com/Garados007/MaxLib.WebServer/workflows/.NET%20Core/badge.svg?branch=main)](https://github.com/Garados007/MaxLib.WebServer/actions?query=workflow%3A%22.NET+Core%22)
[![NuGet
Publish](https://github.com/Garados007/MaxLib.WebServer/workflows/NuGet%20Publish/badge.svg)](https://www.nuget.org/packages?q=Garados007+MaxLib.WebServer)
[![License:
MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/Garados007/MaxLib.WebServer/blob/master/LICENSE)
[![Current
Version](https://img.shields.io/github/tag/garados007/MaxLib.WebServer.svg?label=release)](https://github.com/Garados007/MaxLib.WebServer/releases)
![Top Language](https://img.shields.io/github/languages/top/garados007/MaxLib.WebServer.svg)

`MaxLib.WebServer` is a full web server written in C#. To use this webserver you only need to add
the .NuGet package to your project, instantiate the server in your code and thats it. No special
configuration files or multiple processes.

This web server is build modular. Any modules can be replaced by you and you decide what the server
is capable of. The server can fully configured (including exchanging the modules) during runtime -
no need to restart everything.

Some of the current features of the web server are:
- HTTP web server (of course, that's the purpose of the project)
- HTTPS web server with SSL certificates
- HTTP and HTTPS web server on the same port. The server detects automatically what the user intends
  to use.
- Asynchronous handling of requests. Every part of the pipeline works with awaitable Tasks.
- REST Api builder. You can directly bind your methods to the handlers.
- Chunked transport. The server understands chunked data streams and can produce these.
- Lazy handling of requests. The server allows you to produce the content while you are sending the
  response. No need to wait.
- Deliver contents from your local drive (e.g. HDD)
- Session keeping. You can identify the user later.
- ...

## Getting Started

This will add MaxLib.WebServer to your project and create a basic server with basic functions.

### Installing

Add the `MaxLib.WebServer` package.

```sh
dotnet add package MaxLib.WebServer
```

### Create a simple server

Write somewhere in your code (e.g.) in your Main method of your programm the following snippet:

```csharp
using MaxLib.WebServer;
using MaxLib.WebServer.Builder; // this using is used later in this tutorial

// ...

using var server = new Server(new WebServerSettings(
    8000, // this will run the server on port 8000
    5000  // set the timout to 5 seconds.
));

// now add some services. You can use your own implementations but here we
// will add a basic set of services from MaxLib.WebServer.Services.
server.InitDefault();

// the server can now be started. A basic set of services is defined so a new
// request will be handled and the user gets a response. Right now its a
// 404 NOT FOUND but we will add more.
server.Start();

// if you want to stop the server but don't release any of its resources you can do it with:
server.Stop();
```

Right now you can start the server and open the url [http://localhost:8000](http://localhost:8000)
in your browser and will get a nice 404 response.

### Create own service

Now we will create our own service, that will responds with a beautiful "Hello World" message.

Create a new class `MyServices` and put this code in it:

```csharp
using System;
using System.Threading.Tasks;
using MaxLib.WebServer;
using MaxLib.WebServer.Builder;

// every service that uses the included Builder system needs to be derived from Service
public class MyServices : Service
{
    // this method should listen on /hello and return a nice hello
    [Path("/hello")]
    public string Hello()
    {
        return "<html><head><title>Hello World</title></head>" +
            "<body><h1>Hello World!</h1></body></html>";
    }
}
```

Now you need to add this line to add your service to server:

```csharp
server.AddWebService(Service.Build<MyServices>()!);
```

After that you can run your programm and open the page
[http://localhost:8000/hello](http://localhost:8000/hello). You will see your hello world message.

> More information about the new builder system can be found [here](https://github.com/Garados007/MaxLib.WebServer/wiki/Builder-System)

## Logging

`MaxLib.WebServer` logs exclusively through the standard
[`Microsoft.Extensions.Logging.ILogger`](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
abstraction. There is no MaxLib-specific logging API to learn: point it at whatever
logging backend your application already uses (Serilog, NLog, log4net, the built-in
console/debug providers, ...) via that backend's own standard `ILoggerFactory` setup,
and every log call made inside this library flows into it.

> **The one-line contract:** call `WebServerLog.SetLoggerFactory(...)` **exactly once**,
> **before** constructing anything else from this library. Each class resolves its
> logger once, at first use, so a factory set afterwards will not be picked up by
> classes that already ran. A second call to `SetLoggerFactory` throws
> `InvalidOperationException` rather than silently changing anything.

```csharp
using Microsoft.Extensions.Logging;

// Wire this library's logging to whatever backend you already use - here plain
// console output, but AddSerilog(), AddNLog(), AddDebug(), etc. work the same way.
WebServerLog.SetLoggerFactory(LoggerFactory.Create(builder => builder.AddSimpleConsole()));

using var server = new Server(new WebServerSettings(8000, 5000));
// ...
```

## WebSocket Events

`MaxLib.WebServer.WebSocket` includes `EventBase`/`EventFactory`, a small typed-message
layer on top of raw WebSocket frames. Events are plain classes, and
`System.Text.Json`'s native polymorphic serialization takes care of reading/writing
the `"$type"` discriminator and the rest of the payload — no custom converters needed.

### Registering events

Subclass `EventBase` with public properties, create an `EventFactory`, and register
your event types before using it:

```csharp
using MaxLib.WebServer.WebSocket;

public class ChatMessage : EventBase
{
    public string Text { get; set; } = "";
}

var factory = new EventFactory();
factory.Add<ChatMessage>();              // wire "$type" defaults to the class name
// factory.Add<ChatMessage>("chat.msg"); // or pick an explicit wire name

// pass `factory` into your EventConnection subclass, then:
// await SendFrame(new ChatMessage { Text = "hi" });
```

All event types must be registered before the factory's first use (its registry is
sealed on first serialize/deserialize) — register everything once at startup.

### Automatic registration

There is no attribute-scanning or assembly discovery: declaring an `EventBase`
subclass alone does not register it. Every event type must be added explicitly via
`Add<T>()`/`Add<T>(string)`/`Add(string, Type)`.

### Customizing the wire format

Use standard `System.Text.Json` attributes directly on your event's properties:

```csharp
public class ChatMessage : EventBase
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonIgnore]
    public DateTime ReceivedAt { get; set; }
}
```

For settings that should apply to every event on a factory (a naming policy, a
converter for a shared type), pass a `JsonSerializerOptions` into the constructor:

```csharp
var factory = new EventFactory(new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
});
```

See Microsoft's [System.Text.Json property customization documentation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties)
for the full set of supported attributes and options.

### Multiple factories

Each `EventFactory` instance has its own independent type registry and
`JsonSerializerOptions` — nothing is shared statically between instances. Create one
factory per protocol/endpoint that needs a different set of event types (or different
wire settings), and give each `EventConnection` subclass the factory instance
appropriate to it.

## Example

- [example/MaxLib.WebServer.Example](example/MaxLib.WebServer.Example)
    - create a basic webserver
- [example/MaxLib.WebServer.Builder.Debugger.Example](example/MaxLib.WebServer.Builder.Debugger.Example)
    - a mix of working and deliberately misconfigured `Builder.Service` types, an index
      page linking to every endpoint, and the `Builder.Debugger.DebuggerService` wired up
      to explain what got built (or skipped/failed) and why a given request would or
      wouldn't reach a service

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct, and the process
for submitting pull requests to us.

## Versioning

We use [SemVer](semver.org) for versioning. For the versions available, see the [tags on this
repository](https://github.com/Garados007/MaxLib.WebServer/tags).

## Authors

- **Max Brauer** - *Initial work* - [Garados007](https://github.com/Garados007)

See also the list of [contributors](https://github.com/Garados007/srpc/contributors) who
participated in this project.

## Lincense

This project is licensed under the MIT License  - see the [LICENSE.md](LICENSE.md) file for details

## Acknowledgments

- StackOverflow for the help
- Wikipedia, SelfHTML and Mozilla for their documentation
- [PurpleBooth](https://github.com/PurpleBooth) for her
  [README.md](https://gist.github.com/PurpleBooth/109311bb0361f32d87a2) template

## Last Words

This project was a free time project of mine and I have done it because why not. The source code was
a long time a part of [MaxLib](https://github.com/Garados007/MaxLib) (a collection of other fun
projects and code) but got its own repository for better maintenance.

Some of the documentation inside the code is still in German and other things needs to be optimized.

I have used this for some projects with my friends. It can handle some TB of traffic over a long
period without any problems or crashes. I am a little proud of this.

I will further maintain and extend this library if I have new cool features in mind (this was the
case for the full WebSocket support and easy Builder System).
