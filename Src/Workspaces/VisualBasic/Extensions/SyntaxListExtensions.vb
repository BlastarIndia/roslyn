﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Runtime.CompilerServices
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Extensions
    Friend Module SyntaxListExtensions
        <Extension()>
        Public Function RemoveRange(Of T As SyntaxNode)(syntaxList As SyntaxList(Of T), index As Integer, count As Integer) As SyntaxList(Of T)
            Dim result = New List(Of T)(syntaxList)
            result.RemoveRange(index, count)
            Return SyntaxFactory.List(result)
        End Function
    End Module
End Namespace