# Tanaro

A testing harness for Azure Functions (isolated worker model), inspired by [Alba](https://github.com/JasperFx/alba). See [readme.md](../readme.md) for the name origin.

## Architecture

- `Tanaro/` — the harness library (net10.0). `FunctionHost` builds a real `IHost` from a Function App's own `Program.Main` via `HostFactoryResolver` (intercepts `HostBuilder.Build`/`HostApplicationBuilder.Build` using `DiagnosticListener`, no test seam needed in the app). `Scenario<TFunction, TResult>` overloads resolve a function class from DI and invoke it with a `DummyFunctionContext`.
- `Tanaro.Generators/` — a Roslyn incremental source generator (netstandard2.0). Discovers types flagged with `[FunctionUnderTest<T>]` (via `ForAttributeWithMetadataName`, so discovery is driven by syntax in the *consuming* project, not by scanning referenced assemblies) and emits typed `FunctionHost` extension methods for every method on `T` carrying `[Microsoft.Azure.Functions.Worker.Function("Name")]`. Generated methods land in the same namespace as `T` (the function class under test).
- `Tanaro.DemoFunction/` — a sample Azure Function App used as the generator/test fixture. **Never add a project reference from here to `Tanaro`** — production function apps shouldn't depend on the test harness. `Tanaro.Generators` only needs to see this project's public types/attributes via metadata, which works without any reference in the other direction.
- `Tanaro.Nunit.Tests/` — NUnit tests. References both `Tanaro` and `Tanaro.DemoFunction`, and pulls in `Tanaro.Generators` as an analyzer (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`).

The `Tanaro` package itself bundles `Tanaro.Generators.dll` under `analyzers/dotnet/cs` (see the `_PackTanaroGenerator` target in `Tanaro/Tanaro.csproj`) so external consumers get generation for free just by referencing `Tanaro` — no separate analyzer reference needed.

## Build and Test

- Solution file is `Tanaro.slnx` (new XML solution format) — add new projects here, not via `dotnet sln`.
- Build: `dotnet build Tanaro.slnx`
- Test: run/build `Tanaro.Nunit.Tests` (NUnit)
- Pack (to verify generator packaging): `dotnet pack Tanaro/Tanaro.csproj -c Release`, then unzip the produced `.nupkg` to confirm `analyzers/dotnet/cs/Tanaro.Generators.dll` exists and `lib/net10.0/Tanaro.dll` has no generator dependency.

## Conventions specific to this repo

- **`Tanaro.Generators` targets netstandard2.0** — this misses some C# features other projects here take for granted:
  - Records need a local `System.Runtime.CompilerServices.IsExternalInit` polyfill (see `IsExternalInit.cs`) for init-only properties to compile.
  - List-pattern syntax (`is [X y]`, `is not [{ Value: ... }]`) requires `System.Index`, also absent — use explicit `.Length`/indexer checks instead.
- **Generator codegen uses block-bodied lambdas** (`(f, ctx) => { return ...; }` / `(f, ctx) => { ...; }`), not expression lambdas. `FunctionHost.Scenario` has both an `Action<TFunction, FunctionContext>` and a `Func<TFunction, FunctionContext, Task>` overload; a single-expression lambda is ambiguous between them, but a block lambda with an explicit `return` binds only to the `Func` overload and one without `return` binds only to `Action`.
- **Generator model types must stay plain-value equatable** (strings/enums, not `ImmutableArray<T>` or symbols) — `ImmutableArray<T>`'s default equality is reference-based, which silently defeats incremental generator caching.
- Keep `Tanaro.DemoFunction` free of any reference to `Tanaro` — it exists to look like a real Function App.
