﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Diagnostics;
    using System.Net;

    internal sealed class InternalServerErrorException : CosmosHttpException
    {
        public InternalServerErrorException()
            : base(statusCode: HttpStatusCode.InternalServerError, message: null)
        {
        }

        public InternalServerErrorException(string message)
            : base(statusCode: HttpStatusCode.InternalServerError, message: message)
        {
        }

        public InternalServerErrorException(string message, Exception innerException)
            : base(statusCode: HttpStatusCode.InternalServerError, message: message, innerException: innerException)
        {
        }

        internal InternalServerErrorException(
            int subStatusCode,
            string message,
            StackTrace stackTrace,
            string activityId,
            double requestCharge,
            TimeSpan? retryAfter,
            Headers headers,
            CosmosDiagnosticsContext diagnosticsContext,
            Exception innerException)
            : base(HttpStatusCode.InternalServerError,
             subStatusCode,
             message,
             stackTrace,
             activityId,
             requestCharge,
             retryAfter,
             headers,
             diagnosticsContext,
             innerException)
        {
        }
    }
}
