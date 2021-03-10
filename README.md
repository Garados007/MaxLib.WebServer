# MaxLib.WebServer

[![.NET Core](https://github.com/Garados007/MaxLib.WebServer/workflows/.NET%20Core/badge.svg?branch=main)](https://github.com/Garados007/MaxLib.WebServer/actions?query=workflow%3A%22.NET+Core%22) 
[![NuGet Publish](https://github.com/Garados007/MaxLib.WebServer/workflows/NuGet%20Publish/badge.svg)](https://www.nuget.org/packages?q=Garados007+MaxLib.WebServer) 
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/Garados007/MaxLib.WebServer/blob/master/LICENSE) 
[![Current Version](https://img.shields.io/github/tag/garados007/MaxLib.WebServer.svg?label=release)](https://github.com/Garados007/MaxLib.WebServer/releases) 
![Top Language](https://img.shields.io/github/languages/top/garados007/MaxLib.WebServer.svg)

`MaxLib.WebServer` is a full web server written in C#. To use this webserver you only need to add the .NuGet package to your project, instantiate the server in your code and thats it. No special configuration files or multiple processes.

This web server is build modular. Any modules can be replaced by you and you decide what the server is cabable of. The server can fully configured (including exchanging the modules) during runtime - no need to restart everything.

Some of the current features of the web server are:
- HTTP web server (of course, that's the purpose of the project)
- HTTPS web server with SSL certificates
- HTTP and HTTPS web server on the same port. The server detects automaticly what the user intends to use.
- Asynchronous handling of requests. Every part of the pipeline works with awaitable Tasks.
- REST Api builder. You can directly bind your methods to the handlers.
- Chunked transport. The server understands chunked datastreams and can produce these.
- Lazy handling of requests. The server allows you to produce the content while you are sending the response. No need to wait.
- Work with components that belongs to another AppDomain with Marshaling.
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
using MaxLib.WebServer.Services;

// in your code 
void SetupServer()
{
    // this expects that server is a variable that you have defined in your class
    server = new Server(new WebServerSettings(
        8000, // this will run the server on port 8000
        5000  // set the timout to 5 seconds.
    ));

    // now add some services. You can use your own implementations but here we
    // will add a basic set of services from MaxLib.WebServer.Services.

    // this will read the request from the network stream
    server.AddWebService(new HttpHeaderParser());
    // this will read the header information and prepare them for later usage.
    server.AddWebService(new HttpHeaderPostParser());
    // this will take care of HTTP OPTIONS or HEAD requests
    server.AddWebService(new HttpHeaderSpecialAction());
    // this will serve 404 responses if no service has created a content for the request
    server.AddWebService(new Http404Service());
    // this will prepare the response headers before everything will be send to the user
    server.AddWebService(new HttpResponseCreator());
    // this will send the response to the user
    server.AddWebService(new HttpSender());

    // the server can now be startet. A basic set of services is defined so a new
    // request will be handled and the user gets a response. Right now its a 
    // 404 NOT FOUND but we will add more.
    server.Start();

    // if you don't need the server anymore you can close the server with
    server.Stop();
}

```

Right now you can start the server and open the url [http://localhost:8000](http://localhost:8000) in your browser and will get a nice 404 response.

### Create own service

Now we will create our own service, that will responds with a beatiful "Hello World" message.

Create a new class `HelloWorldService` and put this code in it:

```csharp
using System;
using System.Threading.Tasks;
using MaxLib.WebServer;
using MaxLib.WebServer.Services;

// every service needs to be derived from WebService
public class HelloWorldService : WebService
{
    public HelloWorldService()
        // This will tell the server when this service should be executed.
        : base(ServerStage.CreateDocument)
    {
        // This tells the priority this service will be executed in the current stage.
        // right now we want the default normal priority.
        Importance = WebProgressImportance.Normal; // optional
    }

    // the server asks every service in the current stage if they can do something
    // with the current request. Right now we only want to act if the url is
    // "/hello". This needs to be checked here.
    public override bool CanWorkWith(WebProgressTask task)
    {
        // IsUrl checks if the path is "/hello" or "/hello/".
        return task.Request.Location.IsUrl(new[] { "hello" });
        // If you want to check for "/hello/world" you need to call:
        // return task.Request.Location.IsUrl(new[] { "hello", "world" });
    }

    // this function will be called from the server only if CanWorkWith succeeds.
    // Here we create our response
    public override async Task ProgressTask(WebProgressTask task)
    {
        // Our response. In this case a simple html page.
        var text = "<html><head><title>Hello World</title></head>" +
            "<body><h1>Hello World!</h1></body></html>";
        // now we add the result to the output. We can add any kind of data
        // source. This library has the helper classes for strings, Streams
        // and files.
        task.Document.DataSources.Add(new HttpStringDataSource(text)
        {
            // this will specify the Mime-Type as "text/html". The static
            // class MimeType contains many definitions but you can use your
            // own here if you want. The default Mime-Type is "text/plain".
            MimeType = MimeType.TextHtml,
            // you can specify your encoding here. Default is "utf-8".
            TextEncoding = "utf-8",
        });
        // we are now finished
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
```

Now you need to add this line to add your service to server:

```csharp
server.AddWebService(new HelloWorldService());
```

After that you can run your programm and open the page [http://localhost:8000/hello](http://localhost:8000/hello). You will see your hello world message.

## Example

- [example/MaxLib.WebServer.Example](example/MaxLib.WebServer.Example)
	- create a basic webserver

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct, and the process for submitting pull requests to us.

## Versioning

We use [SemVer](semver.org) for versioning. For the versions available, see the [tags on this repository](https://github.com/Garados007/MaxLib.WebServer/tags).

## Authors

- **Max Brauer** - *Initial work* - [Garados007](https://github.com/Garados007)

See also the list of [contributors](https://github.com/Garados007/srpc/contributors) who participated in this project.

## Lincense

This project is licensed under the MIT License  - see the [LICENSE.md](LICENSE.md) file for details

## Acknowledgments

- StackOverflow for the help
- Wikipedia, SelfHTML and Mozilla for their documentation
- [PurpleBooth](https://github.com/PurpleBooth) for her [README.md](https://gist.github.com/PurpleBooth/109311bb0361f32d87a2) template

## Last Words

This project was a free time project of mine and I have done it because why not. The source
code was a long time a part of [MaxLib](https://github.com/Garados007/MaxLib) (a collection
of other fun projects and code) but got its own repository for better maintenance.

Some of the documentation inside the code is still in German and other things needs to be
optimized.

I have used this for some projects with my friends. It can handle some TB of traffic over a long period without any problems or crashes. I am a little proud of this.
