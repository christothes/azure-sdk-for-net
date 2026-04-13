# Writing Performance Tests for Client Libraries

> **Applies to:** Data-plane and management-plane client libraries

This guide describes how to create performance testing projects for Azure SDK client libraries.

## Location of the project

1. Create the folder structure `sdk/<service>/perf/<service-name>.Perf/`.
1. Create a new SDK-style project named `<service-name>.Perf.csproj` in the above folder.

## Structure and contents of the project

* Contents of the project file should look like

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="$(MSBuildThisFileDirectory)<relative/path/to/the/SDK/project>" />
    <ProjectReference Include="$(MSBuildThisFileDirectory)..\..\..\..\..\common\Perf\Azure.Test.Perf\Azure.Test.Perf.csproj" />
    <ProjectReference Include="$(MSBuildThisFileDirectory)..\..\..\..\core\Azure.Core.TestFramework\src\Azure.Core.TestFramework.csproj" />
  </ItemGroup>
</Project>
```

The project only needs references to the test framework project (`Azure.Core.TestFramework.csproj`), the performance infrastructure project (`Azure.Test.Perf.csproj`) and the client SDK project(s).

> **NOTE:** If the Client SDK source project does not live in the repo, add a `PackageReference` to the SDK's public NuGet package, like
> `<PackageReference Include="<nuget-package-name>" />`.

This project will build an executable with the same name as the name of the project.

* Add a README.md in the folder to help readers understand the target of the performance tests.
* The entry-point for the project executable should be added to `Program.cs` with the following contents:

```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Azure.Test.Perf;

await PerfProgram.Main(Assembly.GetEntryAssembly(), args);
```

This just calls the performance test infrastructure code with the current assembly and any arguments passed to the program. The code in the tests is called as part of execution.

* Create a folder called `Infrastructure` under the project folder.
* This should contain classes that define the environment of the test execution. Callers can use the singleton `PerfTestEnvironment` to obtain account names, account keys, connection strings and other values needed to make a connection to the Azure service.

```csharp
internal sealed class PerfTestEnvironment : TestEnvironment
{
    /// <summary>
    /// The shared instance of the <see cref="PerfTestEnvironment"/> to be used during test runs.
    /// </summary>
    public static PerfTestEnvironment Instance { get; } = new PerfTestEnvironment();
}
```

* Create a folder called `Scenarios` under the project folder.
* This should contain classes that represent the individual stand-alone test scenarios.
* The structure of each test should look like (tests may not need to override all the base class methods):

```csharp
public sealed class <test-name> : PerfTest<SizeOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="<test-name>"/> class.
    /// </summary>
    /// <param name="options">The set of options to consider for configuring the scenario.</param>
    public <test-name>(PerfOptions options) : base(options)
    {
    }

    public override void Dispose(bool disposing)
    {
    }

    public override async Task GlobalSetupAsync()
    {
        await base.GlobalSetupAsync();
        // Global setup code that runs once at the beginning of test execution.
    }

    public override async Task SetupAsync()
    {
        await base.SetupAsync();
        // Individual test-level setup code that runs for each instance of the test.
    }

    public override async Task CleanupAsync()
    {
        // Individual test-level cleanup code that runs for each instance of the test.
        await base.CleanupAsync();
    }

    public override async Task GlobalCleanupAsync()
    {
        // Global cleanup code that runs once at the end of test execution.
        await base.GlobalCleanupAsync();
    }

    public override void Run(CancellationToken cancellationToken)
    {
        // Scenario execution using synchronous APIs
    }

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        // Scenario execution using asynchronous APIs
    }
}
```

* Arguments are passed to tests using one or more of the properties defined in `PerfOptions` or a class derived from it. Check if there is an existing options class that can serve the purpose of the new test before creating a new one for your test. Some predefined options classes are:
  * [`PerfOptions`](https://github.com/Azure/azure-sdk-for-net/blob/main/common/Perf/Azure.Test.Perf/PerfOptions.cs)
  * [`CountOptions`](https://github.com/Azure/azure-sdk-for-net/blob/main/common/Perf/Azure.Test.Perf/CountOptions.cs)
  * [`SizeOptions`](https://github.com/Azure/azure-sdk-for-net/blob/main/common/Perf/Azure.Test.Perf/SizeOptions.cs)

### Structure of a sample performance test project

The below code map shows Track 1 and Track 2 performance test projects created for the Azure Storage File Shares SDK. The Track 1 project references `Microsoft.Azure.Storage.File.dll` while the Track 2 project references `Azure.Storage.Files.Shares.dll`.
Both projects reference `Azure.Test.Perf` and the individual test classes like `UploadFile` and `DownloadFile` inherit from `PerfTest<TOptions>`.

## Build a performance test project

```bash
dotnet build -c Release -f <supported-framework> <path/to/project/file>
```

## Run the executable output of a project

```bash
dotnet run -c Release -f <supported-framework> --no-build -p <path/to/project/file> -- [parameters needed for the test]
```

`<supported-framework>` can be one of `net8.0`, `net462`, or other target frameworks configured for the project. Note the `--` before any custom parameters to pass. This prevents `dotnet` from trying to handle any ambiguous command line switches.

## Examples of performance tests

1. [Azure Storage File Shares](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/storage/Azure.Storage.Files.Shares/perf)
   1. [Track 1 performance test files](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/storage/Azure.Storage.Files.Shares/perf/Microsoft.Azure.Storage.File.Perf)
      Note that the code for Track 1 of this SDK does not live in the Azure SDK for .NET repo, so:
      1. We add a `PackageReference` to the latest SDK library in the [project file](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/storage/Azure.Storage.Files.Shares/perf/Microsoft.Azure.Storage.File.Perf/Microsoft.Azure.Storage.File.Perf.csproj).
      2. The Track 1 performance test project is located next to the Track 2 performance test project (each in their own separate directory under `perf/`).
   1. [Track 2 performance test files](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/storage/Azure.Storage.Files.Shares/perf/Azure.Storage.Files.Shares.Perf)
1. [Azure Search documents tests](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/search/Azure.Search.Documents/perf)
   1. [Track 2 performance test files](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/search/Azure.Search.Documents/perf/Azure.Search.Documents.Perf)
