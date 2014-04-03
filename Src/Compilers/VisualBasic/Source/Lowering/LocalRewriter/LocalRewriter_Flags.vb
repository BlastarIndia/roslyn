﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports TypeKind = Microsoft.CodeAnalysis.TypeKind

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend NotInheritable Class LocalRewriter
        Inherits BoundTreeRewriter

        <Flags>
        Friend Enum RewritingFlags
            [Default] = 0
            AllowSequencePoints = 1
            AllowEndOfMethodReturnWithExpression = 2
            AllowCatchWithErrorLineNumberReference = 4
        End Enum

        Private ReadOnly Flags As RewritingFlags

    End Class
End Namespace
