// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using Azure.AI.OpenAI;
using Azure.Projects;
using Azure.Projects.OpenAI;
using OpenAI.Chat;

ProjectInfrastructure infrastructure = new();
infrastructure.AddFeature(new OpenAIModelFeature("gpt-4o-mini", "2024-07-18"));
infrastructure.AddFeature(new OpenAIModelFeature("text-embedding-ada-002", "2", AIModelKind.Embedding));

// the app can be called with -init switch to generate bicep and prepare for azd deployment.
if (infrastructure.TryExecuteCommand(args)) return;

ChatTools tools = new(typeof(Tools));

ProjectClient project = new();

List<ChatMessage> conversation = [];

ChatProcessor processor = new(
    project.GetOpenAIChatClient(),
    project.GetOpenAIEmbeddingClient(),
    tools
);

// await tools.AddMcpServerAsync(new("http://localhost:3001/sse"));

conversation.Add(new SystemChatMessage("When you make a tool call, DO NOT guess the values of required parameters. " +
    "Instead, ask the user for the values of the required parameters. " +
    "If there is a tool call available that seems capable of providing the value of a required parameter, " +
    "call that tool to get the value and ask the user, if necessary for the parameter values it requires. "));

while (true)
{
    Console.Write("> ");
    string prompt = Console.ReadLine();
    if (string.IsNullOrEmpty(prompt))
        continue;

    ChatCompletion completion = await processor.TakeTurnAsync(conversation, prompt).ConfigureAwait(false);

    Console.WriteLine(completion.AsText());
}

class Tools
{
    public static string GetCurrentTime() => DateTime.Now.ToString("T");

    public static string GetUriForStorageBlob(string storageAccountName, string containerName)
    {
        Console.WriteLine($"* tool call: GetUriForStorageAccount({storageAccountName}, {containerName})");
        // Simulate getting a URI for the storage account
        return $"https://{storageAccountName}.blob.core.windows.net/{containerName}";
    }
    public static string UploadFile([Description("ask for this")] string uriForStorageAccount, string filePath)
    {
        // Simulate file upload
        Console.WriteLine($"* Construct a BlobClient with the uri {uriForStorageAccount} to upload File {filePath}.");
        return $"Uploading file {filePath} to {uriForStorageAccount}...";
    }
}
