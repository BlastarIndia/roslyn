﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Runtime.CompilerServices
Imports System.Threading
Imports Microsoft.CodeAnalysis.Shared.Extensions
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Extensions.ContextQuery
    Friend Module VisualBasicSyntaxTreeExtensions

        <Extension>
        Friend Function IsAccessibleEventContext(context As VisualBasicSyntaxContext, startAtEnclosingBaseType As Boolean, cancellationToken As CancellationToken) As Boolean
            If context.FollowsEndOfStatement Then
                Return False
            End If

            Dim targetToken = context.TargetToken
            If targetToken.IsChildToken(Of HandlesClauseSyntax)(Function(hc) hc.HandlesKeyword) OrElse
                targetToken.IsChildSeparatorToken(Function(hc As HandlesClauseSyntax) hc.Events) Then

                Dim container = context.EnclosingNamedType.GetValue(cancellationToken)

                If container Is Nothing Then
                    Return False
                End If

                If startAtEnclosingBaseType Then
                    Return container.BaseType.GetAccessibleMembersInThisAndBaseTypes(Of IEventSymbol)(container).Any()
                End If

                Return container.GetAccessibleMembersInThisAndBaseTypes(Of IEventSymbol)(container).Any()
            End If

            Return False
        End Function

        <Extension()>
        Friend Function IsFollowingCompleteStatement(Of TParent As SyntaxNode)(context As VisualBasicSyntaxContext, childGetter As Func(Of TParent, StatementSyntax)) As Boolean
            Dim targetToken = context.TargetToken

            Dim parent = targetToken.GetAncestor(Of TParent)()
            If parent Is Nothing Then
                Return False
            End If

            Dim statement = childGetter(parent)
            If statement Is Nothing Then
                Return False
            End If

            Dim visitor As New IsStatementTerminatingTokenVisitor(targetToken)
            Return visitor.Visit(statement)
        End Function

        ''' <summary>
        ''' The specified position is immediately following a statement of one of the given kinds.
        ''' </summary>
        <Extension()>
        Friend Function IsAfterStatementOfKind(context As VisualBasicSyntaxContext, ParamArray kinds As SyntaxKind()) As Boolean
            If Not context.FollowsEndOfStatement Then
                Return False
            End If

            Dim targetToken = context.TargetToken
            If targetToken.VisualBasicKind = SyntaxKind.None OrElse targetToken.Parent Is Nothing Then
                Return False
            End If

            Return targetToken.GetAncestor(Of StatementSyntax).MatchesKindIfNotNull(kinds)
        End Function

        <Extension()>
        Friend Function IsInStatementBlockOfKind(context As VisualBasicSyntaxContext, ParamArray kinds As SyntaxKind()) As Boolean
            Dim ancestor = context.TargetToken.Parent

            Do While ancestor IsNot Nothing
                If TypeOf ancestor Is EndBlockStatementSyntax Then
                    ' If we're within the End Block, skip the block itself
                    ancestor = ancestor.Parent.Parent

                    If ancestor Is Nothing Then
                        Return False
                    End If
                End If

                If ancestor.MatchesKind(kinds) Then
                    Return True
                End If

                If TypeOf ancestor Is LambdaExpressionSyntax Then
                    If Not (context.FollowsEndOfStatement AndAlso context.TargetToken = ancestor.GetLastToken()) Then
                        ' We should not look past lambdas
                        Return False
                    End If
                End If

                ancestor = ancestor.Parent
            Loop

            Return False
        End Function

        <Extension()>
        Public Function IsDelegateCreationContext(context As VisualBasicSyntaxContext) As Boolean
            If context.FollowsEndOfStatement Then
                Return False
            End If

            Dim token = context.TargetToken
            If token.IsParentKind(SyntaxKind.ArgumentList) AndAlso
               TypeOf token.Parent.Parent Is NewExpressionSyntax Then

                Dim symbolInfo = context.SemanticModel.GetSymbolInfo(DirectCast(token.Parent.Parent, NewExpressionSyntax).Type())
                Dim objectCreationType = TryCast(symbolInfo.Symbol, ITypeSymbol)
                If objectCreationType IsNot Nothing AndAlso
                   objectCreationType.TypeKind = TypeKind.Delegate Then

                    Return True
                End If
            End If

            Return False
        End Function

        <Extension()>
        Public Function CanDeclareCustomEventAccessor(context As VisualBasicSyntaxContext, accessorBlockKind As SyntaxKind) As Boolean
            If context.IsCustomEventContext Then
                Return Not context.TargetToken.GetAncestor(Of EventBlockSyntax)().Accessors.Any(Function(a) a.MatchesKind(accessorBlockKind))
            End If

            Return False
        End Function


    End Module
End Namespace
