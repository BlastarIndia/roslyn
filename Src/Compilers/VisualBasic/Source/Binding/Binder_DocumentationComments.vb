﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports System.Runtime.InteropServices

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend Class Binder

        Friend Overridable Function BindInsideCrefAttributeValue(name As TypeSyntax, preserveAliases As Boolean, <[In], Out> ByRef useSiteDiagnostics As HashSet(Of DiagnosticInfo)) As ImmutableArray(Of Symbol)
            Return Me.ContainingBinder.BindInsideCrefAttributeValue(name, preserveAliases, useSiteDiagnostics)
        End Function

        Friend Overridable Function BindInsideCrefAttributeValue(reference As CrefReferenceSyntax, preserveAliases As Boolean, <[In], Out> ByRef useSiteDiagnostics As HashSet(Of DiagnosticInfo)) As ImmutableArray(Of Symbol)
            Return Me.ContainingBinder.BindInsideCrefAttributeValue(reference, preserveAliases, useSiteDiagnostics)
        End Function

        Friend Overridable Function BindXmlNameAttributeValue(identifier As IdentifierNameSyntax, <[In], Out> ByRef useSiteDiagnostics As HashSet(Of DiagnosticInfo)) As ImmutableArray(Of Symbol)
            Return Me.ContainingBinder.BindXmlNameAttributeValue(identifier, useSiteDiagnostics)
        End Function

    End Class
End Namespace
