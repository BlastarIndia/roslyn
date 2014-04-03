﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic
    ''' <summary>
    ''' Represents a <see cref="T:Roslyn.Microsoft.CodeAnalysis.VisualBasic.SyntaxVisitor"/> that descends an entire <see cref="T:Microsoft.CodeAnalysis.VisualBasic.SyntaxNode"/> graph
    ''' visiting each SyntaxNode and its child SyntaxNodes and <see cref="T:Microsoft.CodeAnalysis.VisualBasic.SyntaxToken"/>s in depth-first order.
    ''' </summary>
    Public MustInherit Class VisualBasicSyntaxWalker
        Inherits VisualBasicSyntaxVisitor

        Protected ReadOnly Depth As SyntaxWalkerDepth

        Protected Sub New(Optional depth As SyntaxWalkerDepth = SyntaxWalkerDepth.Node)
            Me.Depth = depth
        End Sub

        Public Overrides Sub DefaultVisit(node As SyntaxNode)
            Dim list = node.ChildNodesAndTokens()
            Dim childCnt = list.Count

            Dim i As Integer = 0
            Do
                Dim child = list(i)
                i = i + 1

                Dim asNode = child.AsNode()
                If asNode IsNot Nothing Then
                    If Depth >= SyntaxWalkerDepth.Node Then
                        Me.Visit(asNode)
                    End If
                Else
                    If Depth >= SyntaxWalkerDepth.Token Then
                        Me.VisitToken(child.AsToken())
                    End If
                End If
            Loop While i < childCnt

        End Sub

        Public Overridable Sub VisitToken(token As SyntaxToken)
            If Depth >= SyntaxWalkerDepth.Trivia Then
                Me.VisitLeadingTrivia(token)
                Me.VisitTrailingTrivia(token)
            End If
        End Sub

        Public Overridable Sub VisitLeadingTrivia(token As SyntaxToken)
            If token.HasLeadingTrivia Then
                For Each tr In token.LeadingTrivia
                    VisitTrivia(tr)
                Next
            End If
        End Sub

        Public Overridable Sub VisitTrailingTrivia(token As SyntaxToken)
            If token.HasTrailingTrivia Then
                For Each tr In token.TrailingTrivia
                    VisitTrivia(tr)
                Next
            End If
        End Sub

        Public Overridable Sub VisitTrivia(trivia As SyntaxTrivia)
            If Depth >= SyntaxWalkerDepth.StructuredTrivia AndAlso trivia.HasStructure Then
                Visit(DirectCast(trivia.GetStructure(), VisualBasicSyntaxNode))
            End If
        End Sub
    End Class

    ''' <summary>
    ''' Represents a <see cref="T:Roslyn.Compilers.VisualBasic.SyntaxNode"/> visitor that visits only the single SyntaxNode
    ''' passed into its <see cref="M:Roslyn.Compilers.VisualBasic.SyntaxVisitor.Visit(Roslyn.Compilers.VisualBasic.SyntaxNode)"/> method.
    ''' </summary>
    Partial Public MustInherit Class VisualBasicSyntaxVisitor
    End Class

    ''' <summary>
    ''' Represents a <see cref="T:Roslyn.Compilers.VisualBasic.SyntaxNode"/> visitor that visits only the single SyntaxNode
    ''' passed into its <see cref="M:Roslyn.Compilers.VisualBasic.SyntaxVisitor`1.Visit(Roslyn.Compilers.VisualBasic.SyntaxNode)"/> method and produces 
    ''' a value of the type specified by the <typeparamref name="TResult"/> parameter.
    ''' </summary>
    ''' <typeparam name="TResult">
    ''' The type of the return value this visitor's Visit method.
    ''' </typeparam>
    Partial Public MustInherit Class VisualBasicSyntaxVisitor(Of TResult)
    End Class
End Namespace