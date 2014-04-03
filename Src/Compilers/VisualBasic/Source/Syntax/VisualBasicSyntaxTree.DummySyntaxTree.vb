﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.ObjectModel
Imports System.Threading
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports InternalSyntax = Microsoft.CodeAnalysis.VisualBasic.Syntax.InternalSyntax

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Class VisualBasicSyntaxTree
        Friend Class DummySyntaxTree
            Inherits VisualBasicSyntaxTree

            Private ReadOnly _node As CompilationUnitSyntax

            Public Sub New()
                _node = Me.CloneNodeAsRoot(SyntaxFactory.ParseCompilationUnit(String.Empty))
            End Sub

            Public Overrides Function GetText(Optional cancellationToken As CancellationToken = Nothing) As SourceText
                Return SourceText.From(String.Empty)
            End Function

            Public Overrides Function TryGetText(ByRef text As SourceText) As Boolean
                text = SourceText.From(String.Empty)
                Return True
            End Function

            Public Overrides ReadOnly Property Length As Integer
                Get
                    Return 0
                End Get
            End Property

            Public Overrides ReadOnly Property Options As VisualBasicParseOptions
                Get
                    Return VisualBasicParseOptions.Default
                End Get
            End Property

            Public Overrides ReadOnly Property FilePath As String
                Get
                    Return String.Empty
                End Get
            End Property

            Public Overrides Function GetReference(node As SyntaxNode) As SyntaxReference
                Return New SimpleSyntaxReference(Me, node)
            End Function

            Public Overrides Function WithChangedText(newText As SourceText) As SyntaxTree
                Throw New InvalidOperationException()
            End Function

            Public Overrides Function GetRoot(Optional cancellationToken As CancellationToken = Nothing) As VisualBasicSyntaxNode
                Return _node
            End Function

            Public Overrides Function TryGetRoot(ByRef root As VisualBasicSyntaxNode) As Boolean
                root = _node
                Return True
            End Function

            Public Overrides ReadOnly Property HasCompilationUnitRoot As Boolean
                Get
                    Return True
                End Get
            End Property
        End Class
    End Class
End Namespace