﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System
Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend Class VisualBasicSyntaxTreeFactoryServiceFactory

        Partial Private Class VisualBasicSyntaxTreeFactoryService

            ''' <summary>
            ''' Represents a syntax reference that was passed a null
            ''' reference to a node. In this case, we just hold onto the
            ''' weak tree reference and throw if any invalid properties
            ''' are accessed.
            ''' </summary>
            Private Class NullSyntaxReference
                Inherits SyntaxReference

                Private ReadOnly _tree As SyntaxTree

                Public Sub New(tree As SyntaxTree)
                    _tree = tree
                End Sub

                Public Overrides ReadOnly Property SyntaxTree As SyntaxTree
                    Get
                        Return _tree
                    End Get
                End Property

                Public Overrides Function GetSyntax(Optional cancellationToken As CancellationToken = Nothing) As SyntaxNode
                    Return Nothing
                End Function

                Public Overrides ReadOnly Property Span As TextSpan
                    Get
                        Throw New NotSupportedException("Cannot retrieve the Span of a null syntax reference.")
                    End Get
                End Property
            End Class
        End Class
    End Class
End Namespace