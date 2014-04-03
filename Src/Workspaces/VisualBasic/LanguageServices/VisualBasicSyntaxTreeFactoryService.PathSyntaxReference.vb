﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System
Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
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
            ''' Represents a syntax reference that doesn't actually hold onto the 
            ''' referenced node.  Instead, enough data is held onto so that the node
            ''' can be recovered and returned if necessary.
            ''' </summary>
            Private Class PathSyntaxReference
                Inherits SyntaxReference

                Private ReadOnly tree As SyntaxTree

                Private ReadOnly kind As SyntaxKind

                Private ReadOnly _span As TextSpan

                Private ReadOnly pathFromRoot As ImmutableList(Of Integer)

                Public Sub New(tree As SyntaxTree, node As SyntaxNode)
                    Me.tree = tree
                    Me.kind = node.VisualBasicKind()
                    Me._span = node.Span
                    Me.pathFromRoot = ComputePathFromRoot(node)
                End Sub

                Public Overrides ReadOnly Property SyntaxTree As SyntaxTree
                    Get
                        Return Me.tree
                    End Get
                End Property

                Public Overrides ReadOnly Property Span As TextSpan
                    Get
                        Return Me._span
                    End Get
                End Property

                Private Function ComputePathFromRoot(node As SyntaxNode) As ImmutableList(Of Integer)
                    Dim path = New List(Of Integer)()
                    Dim root = tree.GetRoot()
                    While node IsNot root
                        While node.Parent IsNot Nothing
                            Dim index = GetChildIndex(node)
                            path.Add(index)
                            node = node.Parent
                        End While

                        If node.IsStructuredTrivia Then
                            Dim trivia = (DirectCast(node, StructuredTriviaSyntax)).ParentTrivia
                            Dim triviaIndex = GetTriviaIndex(trivia)
                            path.Add(triviaIndex)
                            Dim tokenIndex = GetChildIndex(trivia.Token)
                            path.Add(tokenIndex)
                            node = trivia.Token.Parent
                            Continue While
                        ElseIf node IsNot root Then
                            Throw New InvalidOperationException(VBWorkspaceResources.NodeDoesNotDescendFromRoot)
                        End If
                    End While

                    path.Reverse()
                    Return path.ToImmutableList()
                End Function

                Private Function GetChildIndex(child As SyntaxNodeOrToken) As Integer
                    Dim parent As SyntaxNode = child.Parent
                    Dim index As Integer = 0
                    For Each snot In parent.ChildNodesAndTokens()
                        If snot = child Then
                            Return index
                        End If

                        index = index + 1
                    Next

                    Throw New InvalidOperationException(VBWorkspaceResources.NodeNotInParentsChildList)
                End Function

                Private Function GetTriviaIndex(trivia As SyntaxTrivia) As Integer
                    Dim token = trivia.Token
                    Dim index As Integer = 0
                    For Each tr In token.LeadingTrivia
                        If tr = trivia Then
                            Return index
                        End If

                        index = index + 1
                    Next

                    For Each tr In token.TrailingTrivia
                        If tr = trivia Then
                            Return index
                        End If

                        index = index + 1
                    Next

                    Throw New InvalidOperationException(VBWorkspaceResources.TriviaIsNotAssociatedWithToken)
                End Function

                Private Function GetTrivia(token As SyntaxToken, triviaIndex As Integer) As SyntaxTrivia
                    Dim leadingCount = token.LeadingTrivia.Count
                    If triviaIndex <= leadingCount Then
                        Return token.LeadingTrivia.ElementAt(triviaIndex)
                    End If

                    triviaIndex -= leadingCount
                    Return token.TrailingTrivia.ElementAt(triviaIndex)
                End Function

                Public Overrides Function GetSyntax(Optional cancellationToken As CancellationToken = Nothing) As SyntaxNode
                    Return DirectCast(Me.GetNode(Me.tree.GetRoot(cancellationToken)), SyntaxNode)
                End Function

                Public Overrides Async Function GetSyntaxAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of SyntaxNode)
                    Dim root = Await Me.tree.GetRootAsync(cancellationToken).ConfigureAwait(False)
                    Return Me.GetNode(root)
                End Function

                Private Function GetNode(root As SyntaxNode) As SyntaxNode
                    Dim node = root
                    Dim i As Integer = 0
                    Dim n As Integer = Me.pathFromRoot.Count

                    While i < n
                        Dim child = node.ChildNodesAndTokens().ElementAt(Me.pathFromRoot(i))

                        If child.IsToken Then
                            i = i + 1
                            System.Diagnostics.Debug.Assert(i < n)
                            Dim triviaIndex = Me.pathFromRoot(i)
                            Dim trivia = GetTrivia(child.AsToken(), triviaIndex)
                            node = trivia.GetStructure()
                        Else
                            node = child.AsNode()
                        End If

                        i = i + 1
                    End While

                    System.Diagnostics.Debug.Assert(node.VisualBasicKind = Me.kind)
                    System.Diagnostics.Debug.Assert(node.Span = Me._span)
                    Return node

                End Function
            End Class
        End Class
    End Class
End Namespace