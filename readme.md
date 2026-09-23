# Tanaro

A testing harness for Azure Functions (isolated worker model), inspired by [Alba](https://github.com/JasperFx/alba).

Tanaro boots a real `IHost` from your Function App's own `Program.cs` (no test seam needed in the app itself),
then lets you invoke individual functions directly and assert on their result, without going through the
Functions runtime or any network hop.

## Install

```
dotnet add package Tanaro
```

Referencing the package also pulls in a source generator that emits typed scenario methods - no separate
analyzer reference required.

## Quick start

`Program` from top-level statements is `internal`, so your Function App needs to expose it to the test
project via `InternalsVisibleTo`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="YourTestProject" />
</ItemGroup>
```

Flag your test fixture with `[FunctionUnderTest<T>]` for every function class you want to exercise, and start
the host from your Function App's `Program`.

### NUnit

Building the FunctionHost is expensive so you only want to do it once per test fixture. The following pattern allows you to reuse your host across tests.

```csharp
[SetUpFixture]
[FunctionUnderTest<SampleFunctions>]
public class AppUnderTest
{
    public static FunctionHost Host { get; private set; } = null!;

    [OneTimeSetUp]
    public void Setup() => Host = FunctionHost.For<Program>(builder => { }, []);

    [OneTimeTearDown]
    public void TearDown() => Host.Dispose();
}
```

The generator emits one scenario method per `[Function("Name")]` method on `SampleFunctions`, named after the
function. Call it to invoke the function through DI and assert on its return value:

```csharp
using Tanaro.DemoFunction;

[Test]
public async Task CountsEvents()
{
    var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

    var result = await AppUnderTest.Host.Run<SampleFunctions>()
        .SampleTaskOfValue(s => s.Execute([eventData]));

    Assert.That(result.Value, Is.EqualTo(1));
}
```

`Execute(...)` records the call; `WithContext(ctx => ...)` lets you inspect or mutate the `FunctionContext`
(bindings, items, retry context, ...) before the function actually runs.

The function under test is expected to succeed by default - a thrown exception or a middleware short-circuit
fails the scenario immediately via `EnsureSuccess()`. If you're deliberately testing a failure path, opt out
with `s.Execute(...).ExpectFailure()` and inspect `result.Faulted`/`result.Exception`/`result.Invoked` yourself.

### xUnit

xUnit doesn't have `[SetUpFixture]`/`[OneTimeSetUp]`, so start the host in an `IAsyncDisposable` fixture and
share it across tests with an `[assembly: AssemblyFixture(...)]`:

```csharp
[assembly: AssemblyFixture(typeof(Tanaro.XUnit.Tests.AppFixture))]

namespace Tanaro.XUnit.Tests;

[FunctionUnderTest<SampleFunctions>]
public class AppFixture : IAsyncDisposable
{
    public FunctionHost Host { get; } = FunctionHost.For<Program>(_ => { }, []);

    public ValueTask DisposeAsync() => Host.DisposeAsync();
}
```

Inject the fixture into your test class and call the generated scenario methods the same way:

```csharp
using Tanaro.DemoFunction;

public class SampleFunctionsScenarioTests(AppFixture fixture)
{
    [Fact]
    public async Task TaskOfValueShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await fixture.Host.Run<SampleFunctions>().SampleTaskOfValue(s => s.Execute([eventData]));

        Assert.Equal(1, result.Value);
    }
}
```

## AI Stance
Tanaro isn't vibe-coded. We use a mix of regular coding and agent-assisted work, but people make the design decisions. Every change is reviewed, understood, and validated by a person before it lands.

We don't mind contributors using agents either, as long as their contributions follow the contributing guide. The rule is simple: **you vibe it, you own it**. If you submit a change, you must understand it, stand behind it, and be able to explain, test, and fix it. The contribution is yours, not the tool's, so we don't accept generative AI tools as authors or co-authors.

Please don't submit AI slop. Generic, unreviewed, or needlessly verbose generated issues, pull request descriptions, or review comments will be immediately closed when they create more work than value.

(Generously stolen from [Oskar Dudycz](https://lnkd.in/p/eqt5Yxct))

## Name
[Tanaro](https://en.wikipedia.org/wiki/Tanaro) is the river running trough the city of Alba, Italy. 
Since this project is inspired by the [Alba](https://github.com/JasperFx/alba) testing framework, that seemed like a good name.

And yes, I know that library is named after Alba, Missouri. 