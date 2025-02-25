# ZeroLog                    <a href="#"><img src="icon.png" align="right" alt="Logo" /></a>

[![Build](https://github.com/Abc-Arbitrage/ZeroLog/workflows/Build/badge.svg)](https://github.com/Abc-Arbitrage/ZeroLog/actions?query=workflow%3ABuild)
[![NuGet](https://img.shields.io/nuget/v/ZeroLog.svg?label=NuGet&logo=NuGet)](http://www.nuget.org/packages/ZeroLog/)

**ZeroLog is a high-performance, zero-allocation .NET logging library**.

It provides logging capabilities to be used in latency-sensitive applications, where garbage collections are undesirable. ZeroLog can be used in a complete zero-allocation manner, meaning that after the initialization phase, it will not allocate any managed object on the heap, thus preventing any GC from being triggered.

Since ZeroLog does not aim to replace any existing logging libraries in any kind of application, it won't try to compete on feature set level with more pre-eminent projects like log4net or NLog for example. The focus will remain on performance and allocation free aspects.

The project is production ready and you can [get it via NuGet](https://www.nuget.org/packages/ZeroLog) if you want to give it a try.

**ZeroLog v2 requires .NET 6 and C# 10.** If your application targets an earlier .NET version, you can use [ZeroLog v1](https://github.com/Abc-Arbitrage/ZeroLog/tree/v1). Note that a .NET Standard 2.0 target is provided with a limited API surface for use by libraries, but it still requires the main application to target .NET 6 or later.


## Internal Design
 
ZeroLog was designed to meet two main objectives:

 - Being a **zero allocation library**.
 - Doing **as little work as possible in calling threads**.

The second goal implies a major design choice: the actual logging is completely asynchronous. It means that writing messages to the appenders occurs in a background thread, and *all formatting operations are delayed to be performed just before the appending*. **No formatting occurs in the calling thread**, the log data is merely marshalled to the background logging thread in the most efficient way possible.

Internally, each logging call data (context, log messages, arguments, etc.) will be serialized to a pooled log message, before being enqueued in a concurrent data structure the background logging thread consumes. The logging thread will then format the log messages and append them to the configured appenders.


## Getting Started

Before using ZeroLog, you need to initialize the `LogManager` by calling `LogManager.Initialize` and providing a configuration.

```csharp
LogManager.Initialize(new ZeroLogConfiguration
{
    RootLogger =
    {
        Appenders =
        {
            new ConsoleAppender()
        }
    }
});
```

The `LogManager` needs to be shut down by calling `LogManager.Shutdown()` when your application needs to exit.

You can retrieve a logger that will be the logging API entry point. Store this logger in a field.

```csharp
private static readonly Log _log = LogManager.GetLogger(typeof(YourClass));
```

Two logging APIs are provided:

 - A string interpolation API:
    
    ```csharp
    var date = DateTime.Today.AddDays(1); 
    _log.Info($"Tomorrow ({date:yyyy-MM-dd}) will be in {GetNumberOfSecondsUntilTomorrow():N0} seconds.");
    ```
    
    This API uses C# 10 string interpolation handlers to implement custom interpolation support without allocations.

    Note that if the log level is disabled (`Info` in this example), method calls such as `GetNumberOfSecondsUntilTomorrow()` will *not* be executed. 


 - A `StringBuilder`-like API:

    ```csharp
    _log.Info()
        .Append("Tomorrow (")
        .Append(DateTime.Today.AddDays(1), "yyyy-MM-dd")
        .Append(") will occur in ")
        .Append(GetNumberOfSecondsUntilTomorrow(), "N0")
        .Append(" seconds.")
        .Log();
    ```

    This API supports more features, but is less convenient to use. You need to call `Log` at the end of the chain. Note that an `Append` overload with a string interpolation handler is provided though.

The library provides Roslyn analyzers that check for incorrect usages of these APIs.


### Structured Data

ZeroLog supports appending structured data (formatted as JSON) to log messages.

Structured data can be appended by calling `AppendKeyValue`, like so:

```csharp
_log.Info()
    .Append("Tomorrow is another day.")
    .AppendKeyValue("NumSecondsUntilTomorrow", GetNumberOfSecondsUntilTomorrow())
    .Log();
```


## Configuration

### Appenders

You need to instantiate a set of appenders (*output channels*) that can be used by loggers, and pass them to the logger configurations.

Two appenders are provided by default: `ConsoleAppener` and `DateAndSizeRollingFileAppender`, but you can also write your own.

### Loggers

ZeroLog supports hierarchical loggers. When `GetLogger("Foo.Bar.Baz")` is called, it will try to find the best matching configuration using a hierarchical namespace-like mode. If `Foo.Bar` is configured, but `Foo.Bar.Baz` is not, it will use the configuration for `Foo.Bar`.

You can specify the following options for each level, by adding items to the `Loggers` collection:

 - `Level` is the minimal level the logger will work on.
 - `Appenders` is a set of appenders the logger will use, in addition to the parent appenders which are included by default.
 - `IncludeParentAppenders` can be set to `false` to disable appenders configured at parent levels.
 - `LogMessagePoolExhaustionStrategy` is used to specify what to do when the log message queue is full.

Each appender can be additionally configured with a `Level`, either at the logger configuration level, or on the appender itself.

You can add an appender instance to multiple logger configurations.

### Root Logger

The root logger is the default logger. If a `GetLogger` is called on an unconfigured namespace, it will fallback to the root logger.

### Log Message Pool Exhaustion Strategy

There are currently three strategies to handle a full queue scenario:

 - `DropLogMessageAndNotifyAppenders` (default) - Drop the log message and log an error instead.
 - `DropLogMessage` - Forget about the message.
 - `WaitUntilAvailable` - Block until it's possible to log.

### Queue and Message Size

These values can be configured directly in `ZeroLogConfiguration`:

 - `LogMessagePoolSize` (default: `1024`) - Count of pooled log messages. A log message is acquired from the pool on demand, and released by the logging thread.
 - `LogMessageBufferSize` (default: `128`) - The size of the buffer used to serialize log message arguments. Once exceeded, the message is truncated. All `Append` calls use a few bytes, except for those with a `ReadOnlySpan` parameter, which copy the whole data into the buffer.
 - `LogMessageStringCapacity` (default: `32`) - The maximum number of `Append` calls which involve `string` objects that can be made for a log message. Note that `string` objects are also used for format strings.

### Other Settings

Other settings that can be set on the `ZeroLogConfiguration` object are:

 - `AutoRegisterEnums` (default: `false`) - Automatically registers an enum type when it's logged for the first time. This causes allocations. Use `LogManager.RegisterEnum` when automatic registration is disabled.
 - `NullDisplayString` (default: `"null"`) - The string which should be logged instead of a `null` value.
 - `TruncatedMessageSuffix` (default: `" [TRUNCATED]"`) - The string which is appended to a message when it is truncated.
 - `AppenderQuarantineDelay` (default: 15 seconds) - The time an appender will be put into quarantine (not used to log messages) after it throws an exception.
