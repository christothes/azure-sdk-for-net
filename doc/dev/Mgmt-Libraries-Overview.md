# Management Libraries for .NET

> **Applies to:** Management-plane libraries only

## Management: Current Releases

A new set of management libraries that follow the [Azure SDK Design Guidelines for .NET](https://azure.github.io/azure-sdk/dotnet_introduction.html) and based on [Azure.Core libraries](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/core/Azure.Core) are available. These libraries provide a number of core capabilities that are shared amongst all Azure SDKs, including the intuitive Azure Identity library, an HTTP Pipeline with custom policies, error-handling, distributed tracing, and much more. You can find the list of packages [on this page](https://azure.github.io/azure-sdk/releases/latest/dotnet.html).

To get started with these libraries, please see the [quickstart guide](https://github.com/Azure/azure-sdk-for-net/blob/main/doc/dev/mgmt_quickstart.md). These libraries can be identified by namespaces that start with `Azure.ResourceManager`, e.g. `Azure.ResourceManager.Network`.

> **NOTE:** If you need to ensure your code is ready for production use one of the stable, non-preview libraries.

## Management: Previous Versions

For a complete list of management libraries which enable you to provision and manage Azure resources, please check [here](https://azure.github.io/azure-sdk/releases/latest/all/dotnet.html). They might not have the same feature set as the new releases but they do offer wider coverage of services. Previous versions of management libraries can be identified by namespaces that start with `Microsoft.Azure.Management`, e.g. `Microsoft.Azure.Management.Network`.

Documentation and code samples for these libraries can be found [here](https://azure.github.io/azure-sdk-for-net).
