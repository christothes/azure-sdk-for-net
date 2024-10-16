// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System;

namespace Azure.ResourceManager.DataMigration.Models
{
    /// <summary> Migration validation report result, contains the url for downloading the generated report. </summary>
    public partial class MigrationReportResult
    {
        /// <summary> Initializes a new instance of MigrationReportResult. </summary>
        internal MigrationReportResult()
        {
        }

        /// <summary> Initializes a new instance of MigrationReportResult. </summary>
        /// <param name="id"> Migration validation result identifier. </param>
        /// <param name="reportUri"> The url of the report. </param>
        internal MigrationReportResult(string id, Uri reportUri)
        {
            Id = id;
            ReportUri = reportUri;
        }

        /// <summary> Migration validation result identifier. </summary>
        public string Id { get; }
        /// <summary> The url of the report. </summary>
        public Uri ReportUri { get; }
    }
}
