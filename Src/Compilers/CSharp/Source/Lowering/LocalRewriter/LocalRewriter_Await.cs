﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed partial class LocalRewriter
    {
        public override BoundNode VisitAwaitExpression(BoundAwaitExpression node)
        {
            if (this.ExceptionHandleNesting != 0)
            {
                Debug.Assert(this.ExceptionHandleNesting > 0);
                this.sawAwaitInExceptionHandler = true;
            }

            return base.VisitAwaitExpression(node);
        }
    }
}
