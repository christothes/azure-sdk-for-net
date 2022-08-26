// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Azure.Core;
using Azure.Core.Pipeline;

namespace Azure.Storage
{
    /// <summary>
    /// This policy allows for storage accounts configured with multiple read and/or write regions to retry requests against the additional regional endpoints provided.
    /// </summary>
    internal class CosmosGeoredundantPolicy : HttpPipelineSynchronousPolicy
    {
        private readonly string[] _readEndpoints;
        private readonly string[] _writeEndpoints;
        internal const string AlternateHostIndexKey = "AlternateHostIndex";

        public CosmosGeoredundantPolicy(IList<string> readEndpoints, IList<string> writeEndpoints)
        {
            Argument.AssertNotNull(readEndpoints, nameof(readEndpoints));
            Argument.AssertNotNull(writeEndpoints, nameof(writeEndpoints));

            _readEndpoints = readEndpoints.ToArray();
            _writeEndpoints = writeEndpoints.ToArray();
        }

        public override void OnSendingRequest(HttpMessage message)
        {
            bool isWriteOperation = message.Request.Method != RequestMethod.Get && message.Request.Method != RequestMethod.Head;

            if (isWriteOperation && _writeEndpoints.Length == 0)
            {
                //This is a write operation and there are no alternate write endpoints
                return;
            }
            else if (!isWriteOperation && _readEndpoints.Length == 0)
            {
                // This is a read operation adn there are no alternate read endpoints
                return;
            }
            IList<string> alternateHostList = isWriteOperation ? _writeEndpoints : _readEndpoints;

            // Look up what the alternate host is set to in the message properties. For the initial request, this will
            // not be set.
            int? alternateHostIndex =
                message.TryGetProperty(
                    AlternateHostIndexKey,
                    out var alternateHostObj)
                ? alternateHostObj as int?
                : null;
            if (alternateHostIndex == null)
            {
                // queue up the secondary host for subsequent retries
                message.SetProperty(AlternateHostIndexKey, 0);
                return;
            }
            // Increment the index to point to the next endpoint in the list. If there are more retries than hosts, we will cycle through the list again.
            message.SetProperty(AlternateHostIndexKey, alternateHostIndex + 1);
            message.Request.Uri.Host = alternateHostList[alternateHostIndex.Value % alternateHostList.Count];
        }
    }
}
