// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Azure.Security.ConfidentialLedger
{
    public partial class ConfidentialLedgerClient
    {
        /// <summary> Initializes a new instance of ConfidentialLedgerClient. </summary>
        /// <param name="ledgerUri"> The Confidential Ledger URL, for example https://contoso.confidentialledger.azure.com. </param>
        /// <param name="credential"> A credential used to authenticate to an Azure Service. </param>
        /// <param name="options"> The options for configuring the client. </param>
        public ConfidentialLedgerClient(Uri ledgerUri, TokenCredential credential, ConfidentialLedgerClientOptions options)
        {
            if (ledgerUri == null)
            {
                throw new ArgumentNullException(nameof(ledgerUri));
            }
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            var actualOptions = options ?? new ConfidentialLedgerClientOptions();
            var transportOptions = GetIdentityServerTlsCertAndTrust(ledgerUri, actualOptions);
            ClientDiagnostics = new ClientDiagnostics(actualOptions, true);
            _tokenCredential = credential;
            var authPolicy = new BearerTokenAuthenticationPolicy(_tokenCredential, AuthorizationScopes);
            _pipeline = HttpPipelineBuilder.Build(
                actualOptions,
                Array.Empty<HttpPipelinePolicy>(),
                new HttpPipelinePolicy[] { authPolicy },
                transportOptions,
                new ResponseClassifier());
            _ledgerUri = ledgerUri;
            _apiVersion = actualOptions.Version;
        }

        /// <summary> Posts a new entry to the ledger. A sub-ledger id may optionally be specified. </summary>
        /// <remarks>
        /// Schema for <c>Request Body</c>:
        /// <list type="table">
        ///   <listheader>
        ///     <term>Name</term>
        ///     <term>Type</term>
        ///     <term>Required</term>
        ///     <term>Description</term>
        ///   </listheader>
        ///   <item>
        ///     <term>contents</term>
        ///     <term>string</term>
        ///     <term>Yes</term>
        ///     <term> Contents of the ledger entry. </term>
        ///   </item>
        ///   <item>
        ///     <term>subLedgerId</term>
        ///     <term>string</term>
        ///     <term></term>
        ///     <term> Identifier for sub-ledgers. </term>
        ///   </item>
        ///   <item>
        ///     <term>transactionId</term>
        ///     <term>string</term>
        ///     <term></term>
        ///     <term> A unique identifier for the state of the ledger. If returned as part of a LedgerEntry, it indicates the state from which the entry was read. </term>
        ///   </item>
        /// </list>
        /// </remarks>
        /// <param name="waitUntil"> <see cref="WaitUntil.Completed"/> if the method should wait to return until the long-running operation has completed on the service; <see cref="WaitUntil.Started"/> if it should return after starting the operation. For more information on long-running operations, please see <see href="https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core/samples/LongRunningOperations.md"> Azure.Core Long-Running Operation samples</see>. </param>
        /// <param name="content"> The content to send as the body of the request. </param>
        /// <param name="subLedgerId"> The sub-ledger id. </param>
        /// <param name="context"> The request context. </param>
        public virtual Operation PostLedgerEntry(
            WaitUntil waitUntil,
            RequestContent content,
            string subLedgerId = null,
            RequestContext context = null)
        {
            var response = PostLedgerEntry(content, subLedgerId, context);
            response.Headers.TryGetValue(ConfidentialLedgerConstants.TransactionIdHeaderName, out string transactionId);

            var operation = new PostLedgerEntryOperation(this, transactionId);
            if (waitUntil == WaitUntil.Completed)
            {
                operation.WaitForCompletionResponse(context?.CancellationToken ?? default);
            }
            return operation;
        }

        /// <summary> Posts a new entry to the ledger. A sub-ledger id may optionally be specified. </summary>
        /// <remarks>
        /// Schema for <c>Request Body</c>:
        /// <list type="table">
        ///   <listheader>
        ///     <term>Name</term>
        ///     <term>Type</term>
        ///     <term>Required</term>
        ///     <term>Description</term>
        ///   </listheader>
        ///   <item>
        ///     <term>contents</term>
        ///     <term>string</term>
        ///     <term>Yes</term>
        ///     <term> Contents of the ledger entry. </term>
        ///   </item>
        ///   <item>
        ///     <term>subLedgerId</term>
        ///     <term>string</term>
        ///     <term></term>
        ///     <term> Identifier for sub-ledgers. </term>
        ///   </item>
        ///   <item>
        ///     <term>transactionId</term>
        ///     <term>string</term>
        ///     <term></term>
        ///     <term> A unique identifier for the state of the ledger. If returned as part of a LedgerEntry, it indicates the state from which the entry was read. </term>
        ///   </item>
        /// </list>
        /// </remarks>
        /// <param name="waitUntil"> <see cref="WaitUntil.Completed"/> if the method should wait to return until the long-running operation has completed on the service; <see cref="WaitUntil.Started"/> if it should return after starting the operation. For more information on long-running operations, please see <see href="https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core/samples/LongRunningOperations.md"> Azure.Core Long-Running Operation samples</see>. </param>
        /// <param name="content"> The content to send as the body of the request. </param>
        /// <param name="subLedgerId"> The sub-ledger id. </param>
        /// <param name="context"> The request context. </param>
        public virtual async Task<Operation> PostLedgerEntryAsync(
            WaitUntil waitUntil,
            RequestContent content,
            string subLedgerId = null,
            RequestContext context = null)
        {
           var response = await PostLedgerEntryAsync(content, subLedgerId, context).ConfigureAwait(false);
            response.Headers.TryGetValue(ConfidentialLedgerConstants.TransactionIdHeaderName, out string transactionId);

            var operation = new PostLedgerEntryOperation(this, transactionId);
            if (waitUntil == WaitUntil.Completed)
            {
                await operation.WaitForCompletionResponseAsync(context?.CancellationToken ?? default).ConfigureAwait(false);
            }
            return operation;
        }

        internal static HttpPipelineTransportOptions GetIdentityServerTlsCertAndTrust(Uri ledgerUri, ConfidentialLedgerClientOptions options)
        {
            var identityClient = new ConfidentialLedgerIdentityServiceClient(new Uri("https://identity.confidential-ledger.core.azure.com"), options);

            // Get the ledger's  TLS certificate for our ledger.
            var ledgerId = ledgerUri.Host.Substring(0, ledgerUri.Host.IndexOf('.'));
            Response response = identityClient.GetLedgerIdentity(ledgerId, new());

            // extract the ECC PEM value from the response.
            var eccPem = JsonDocument.Parse(response.Content)
                .RootElement
                .GetProperty("ledgerTlsCertificate")
                .GetString();

            // construct an X509Certificate2 with the ECC PEM value.
            var span = new ReadOnlySpan<char>(eccPem.ToCharArray());
            var ledgerTlsCert = PemReader.LoadCertificate(span, null, PemReader.KeyType.Auto, true);

            X509Chain certificateChain = new();
            certificateChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            certificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            certificateChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            certificateChain.ChainPolicy.VerificationTime = DateTime.Now;
            certificateChain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 0, 0);
            certificateChain.ChainPolicy.ExtraStore.Add(ledgerTlsCert);

            // Define a validation function to ensure that the ledger certificate is trusted by the ledger identity TLS certificate.
            bool CertValidationCheck(X509Certificate2 cert)
            {
                bool isChainValid = certificateChain.Build(cert);
                if (!isChainValid)
                    return false;

                var isCertSignedByTheTlsCert = certificateChain.ChainElements.Cast<X509ChainElement>()
                    .Any(x => x.Certificate.Thumbprint == ledgerTlsCert.Thumbprint);
                return isCertSignedByTheTlsCert;
            }

            return new HttpPipelineTransportOptions { ServerCertificateCustomValidationCallback = args => CertValidationCheck(args.Certificate) };
        }
    }
}
