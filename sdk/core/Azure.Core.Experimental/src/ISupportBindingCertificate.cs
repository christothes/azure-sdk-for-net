// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

namespace Azure.Core
{
    /// <summary>
    /// An interface used to decorate a <see cref="TokenCredential"/> that supports mTLS Proof of Possession for authenticating to Microsoft Entra ID.
    /// </summary>
    public interface ISupportBindingCertificate
    {
        /// <summary>
        /// Gets the Proof of Possession (PoP) binding certificate for the host, if available.
        /// </summary>
        /// <returns><c>true</c> if a binding certificate is available on the host, <c>false</c> if not.</returns>
        public bool TryGetBindingCertificate(out X509Certificate2 bindingCertificate);
    }
}
