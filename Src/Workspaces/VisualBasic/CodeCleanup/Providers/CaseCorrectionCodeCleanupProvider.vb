﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis.CaseCorrection
Imports Microsoft.CodeAnalysis.Text

Namespace Microsoft.CodeAnalysis.CodeCleanup.Providers
#If MEF Then
    <ExportCodeCleanupProvider(PredefinedCodeCleanupProviderNames.CaseCorrection, LanguageNames.VisualBasic)>
    <ExtensionOrder(Before:=PredefinedCodeCleanupProviderNames.Format)>
    Friend Class CaseCorrectionCodeCleanupProvider
#Else
    Friend Class CaseCorrectionCodeCleanupProvider
#End If
        Implements ICodeCleanupProvider

        Public ReadOnly Property Name As String Implements ICodeCleanupProvider.Name
            Get
                Return PredefinedCodeCleanupProviderNames.CaseCorrection
            End Get
        End Property

        Public Function CleanupAsync(document As Document, spans As IEnumerable(Of TextSpan), Optional cancellationToken As CancellationToken = Nothing) As Task(Of Document) Implements ICodeCleanupProvider.CleanupAsync
            Return CaseCorrector.CaseCorrectAsync(document, spans, cancellationToken)
        End Function

        Public Function Cleanup(root As SyntaxNode, spans As IEnumerable(Of TextSpan), workspace As Workspace, Optional cancellationToken As CancellationToken = Nothing) As SyntaxNode Implements ICodeCleanupProvider.Cleanup
            Return CaseCorrector.CaseCorrect(root, spans, workspace, cancellationToken)
        End Function
    End Class
End Namespace
