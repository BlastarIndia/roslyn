﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Runtime.CompilerServices
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Extensions
    Friend Module SyntaxTokenExtensions
        <Extension()>
        Public Function IsKindOrHasMatchingText(token As SyntaxToken, kind As SyntaxKind) As Boolean
            Return token.VisualBasicKind = kind OrElse
                   token.HasMatchingText(kind)
        End Function

        <Extension()>
        Public Function HasMatchingText(token As SyntaxToken, kind As SyntaxKind) As Boolean
            Return String.Equals(token.ToString(), SyntaxFacts.GetText(kind), StringComparison.OrdinalIgnoreCase)
        End Function

        <Extension()>
        Public Function IsParentKind(token As SyntaxToken, kind As SyntaxKind) As Boolean
            Return token.Parent.IsKind(kind)
        End Function

        <Extension()>
        Public Function MatchesKind(token As SyntaxToken, kind As SyntaxKind) As Boolean
            Return token.VisualBasicKind = kind
        End Function

        <Extension()>
        Public Function MatchesKind(token As SyntaxToken, kind1 As SyntaxKind, kind2 As SyntaxKind) As Boolean
            Return token.VisualBasicKind = kind1 OrElse
                   token.VisualBasicKind = kind2
        End Function

        <Extension()>
        Public Function MatchesKind(token As SyntaxToken, ParamArray kinds As SyntaxKind()) As Boolean
            Return kinds.Contains(token.VisualBasicKind)
        End Function

        <Extension()>
        Public Function IsLiteral(token As SyntaxToken) As Boolean
            Return _
                token.VisualBasicKind = SyntaxKind.CharacterLiteralToken OrElse
                token.VisualBasicKind = SyntaxKind.DateLiteralToken OrElse
                token.VisualBasicKind = SyntaxKind.DecimalLiteralToken OrElse
                token.VisualBasicKind = SyntaxKind.FalseKeyword OrElse
                token.VisualBasicKind = SyntaxKind.FloatingLiteralToken OrElse
                token.VisualBasicKind = SyntaxKind.IntegerLiteralToken OrElse
                token.VisualBasicKind = SyntaxKind.StringLiteralToken OrElse
                token.VisualBasicKind = SyntaxKind.TrueKeyword
        End Function

        <Extension()>
        Public Function IsCharacterLiteral(token As SyntaxToken) As Boolean
            Return token.VisualBasicKind = SyntaxKind.CharacterLiteralToken
        End Function

        <Extension()>
        Public Function IsNumericLiteral(token As SyntaxToken) As Boolean
            Return _
                token.VisualBasicKind = SyntaxKind.DateLiteralToken OrElse
                token.VisualBasicKind = SyntaxKind.DecimalLiteralToken OrElse
                token.VisualBasicKind = SyntaxKind.FloatingLiteralToken OrElse
                token.VisualBasicKind = SyntaxKind.IntegerLiteralToken
        End Function

        <Extension()>
        Public Function IsNewOnRightSideOfDotOrBang(token As SyntaxToken) As Boolean
            Dim expression = TryCast(token.Parent, ExpressionSyntax)
            Return If(expression IsNot Nothing,
                      expression.IsNewOnRightSideOfDotOrBang(),
                      False)
        End Function

        <Extension()>
        Public Function IsSkipped(token As SyntaxToken) As Boolean
            Return TypeOf token.Parent Is SkippedTokensTriviaSyntax
        End Function

        <Extension()>
        Public Function FirstAncestorOrSelf(token As SyntaxToken, predicate As Func(Of SyntaxNode, Boolean)) As SyntaxNode
            Return token.Parent.FirstAncestorOrSelf(predicate)
        End Function

        <Extension()>
        Public Function HasAncestor(Of T As SyntaxNode)(token As SyntaxToken) As Boolean
            Return token.GetAncestor(Of T)() IsNot Nothing
        End Function

        ''' <summary>
        ''' Returns true if is a given token is a child token of of a certain type of parent node.
        ''' </summary>
        ''' <typeparam name="TParent">The type of the parent node.</typeparam>
        ''' <param name="token">The token that we are testing.</param>
        ''' <param name="childGetter">A function that, when given the parent node, returns the child token we are interested in.</param>
        <Extension()>
        Public Function IsChildToken(Of TParent As SyntaxNode)(token As SyntaxToken, childGetter As Func(Of TParent, SyntaxToken)) As Boolean
            Dim ancestor = token.GetAncestor(Of TParent)()

            If ancestor Is Nothing Then
                Return False
            End If

            Dim ancestorToken = childGetter(ancestor)

            Return token = ancestorToken
        End Function

        ''' <summary>
        ''' Returns true if is a given token is a separator token in a given parent list.
        ''' </summary>
        ''' <typeparam name="TParent">The type of the parent node containing the separated list.</typeparam>
        ''' <param name="token">The token that we are testing.</param>
        ''' <param name="childGetter">A function that, when given the parent node, returns the separated list.</param>
        <Extension()>
        Public Function IsChildSeparatorToken(Of TParent As SyntaxNode, TChild As SyntaxNode)(token As SyntaxToken, childGetter As Func(Of TParent, SeparatedSyntaxList(Of TChild))) As Boolean
            Dim ancestor = token.GetAncestor(Of TParent)()

            If ancestor Is Nothing Then
                Return False
            End If

            Dim separatedList = childGetter(ancestor)
            For i = 0 To separatedList.SeparatorCount - 1
                If separatedList.GetSeparator(i) = token Then
                    Return True
                End If
            Next

            Return False
        End Function

        <Extension>
        Public Function IsDescendantOf(token As SyntaxToken, node As SyntaxNode) As Boolean
            Return token.Parent IsNot Nothing AndAlso
                   token.Parent.AncestorsAndSelf().Any(Function(n) n Is node)
        End Function

        <Extension()>
        Friend Function GetInnermostDeclarationContext(node As SyntaxToken) As SyntaxNode
            Return node.GetAncestors(Of SyntaxNode).FirstOrDefault(
                Function(ancestor) ancestor.MatchesKind(SyntaxKind.ClassBlock,
                                                        SyntaxKind.StructureBlock,
                                                        SyntaxKind.EnumBlock,
                                                        SyntaxKind.InterfaceBlock,
                                                        SyntaxKind.NamespaceBlock,
                                                        SyntaxKind.ModuleBlock,
                                                        SyntaxKind.CompilationUnit))
        End Function

        <Extension()>
        Public Function GetContainingMember(token As SyntaxToken) As DeclarationStatementSyntax
            Return token.GetAncestors(Of DeclarationStatementSyntax) _
                .FirstOrDefault(Function(a)
                                    Return a.IsMemberDeclaration() OrElse
                                          (a.IsMemberBlock() AndAlso a.GetMemberBlockBegin().IsMemberDeclaration())
                                End Function)
        End Function

        <Extension()>
        Public Function GetContainingMemberBlockBegin(token As SyntaxToken) As StatementSyntax
            Return token.GetContainingMember().GetMemberBlockBegin()
        End Function

        ''' <summary>
        ''' Determines whether the given SyntaxToken is the first token on a line
        ''' </summary>
        <Extension()>
        Public Function IsFirstTokenOnLine(token As SyntaxToken, cancellationToken As CancellationToken) As Boolean
            Dim previousToken = token.GetPreviousToken(includeSkipped:=True, includeDirectives:=True, includeDocumentationComments:=True)
            If previousToken.VisualBasicKind = SyntaxKind.None Then
                Return True
            End If

            Dim text = token.SyntaxTree.GetText()
            Dim tokenLine = text.Lines.IndexOf(token.SpanStart)
            Dim previousTokenLine = text.Lines.IndexOf(previousToken.SpanStart)
            Return tokenLine > previousTokenLine
        End Function

        <Extension()>
        Public Function SpansPreprocessorDirective(tokens As IEnumerable(Of SyntaxToken)) As Boolean
            ' we want to check all leading trivia of all tokens (except the 
            ' first one), and all trailing trivia of all tokens (except the
            ' last one).

            Dim first As Boolean = True
            Dim previousToken As SyntaxToken = Nothing

            For Each token In tokens
                If first Then
                    first = False
                Else
                    ' check the leading trivia of this token, and the trailing trivia
                    ' of the previous token.
                    If token.LeadingTrivia.ContainsPreprocessorDirective() OrElse
                       previousToken.TrailingTrivia.ContainsPreprocessorDirective() Then
                        Return True
                    End If
                End If

                previousToken = token
            Next token

            Return False
        End Function

        <Extension()>
        Public Function GetPreviousTokenIfTouchingWord(token As SyntaxToken, position As Integer) As SyntaxToken
            Return If(token.IntersectsWith(position) AndAlso IsWord(token),
                      token.GetPreviousToken(includeSkipped:=True),
                      token)
        End Function

        <Extension>
        Public Function IsWord(token As SyntaxToken) As Boolean
            Return New VisualBasicSyntaxFactsService().IsWord(token)
        End Function

        <Extension()>
        Public Function IntersectsWith(token As SyntaxToken, position As Integer) As Boolean
            Return token.Span.IntersectsWith(position)
        End Function

        <Extension()>
        Public Function GetNextNonZeroWidthTokenOrEndOfFile(token As SyntaxToken) As SyntaxToken
            Dim nextToken = token.GetNextToken()
            Return If(nextToken.VisualBasicKind = SyntaxKind.None, token.GetAncestor(Of CompilationUnitSyntax)().EndOfFileToken, nextToken)
        End Function

        <Extension()>
        Public Function WithPrependedLeadingTrivia(
            token As SyntaxToken,
            trivia As SyntaxTriviaList) As SyntaxToken
            If trivia.Count = 0 Then
                Return token
            End If

            Return token.WithLeadingTrivia(trivia.Concat(token.LeadingTrivia))
        End Function

        <Extension()>
        Public Function WithAppendedTrailingTrivia(
            token As SyntaxToken,
            trivia As SyntaxTrivia) As SyntaxToken

            Return token.WithTrailingTrivia(token.TrailingTrivia.Concat(trivia))
        End Function

        <Extension()>
        Public Function WithAppendedTrailingTrivia(
            token As SyntaxToken,
            trivia As SyntaxTriviaList) As SyntaxToken
            If trivia.Count = 0 Then
                Return token
            End If

            Return token.WithTrailingTrivia(token.TrailingTrivia.Concat(trivia))
        End Function

        <Extension()>
        Public Function WithAppendedTrailingTrivia(
            token As SyntaxToken,
            trivia As IEnumerable(Of SyntaxTrivia)) As SyntaxToken
            Return token.WithAppendedTrailingTrivia(trivia.ToSyntaxTriviaList())
        End Function

        <Extension>
        Public Function GetNextTokenOrEndOfFile(
            token As SyntaxToken,
            Optional includeZeroWidth As Boolean = False,
            Optional includeSkipped As Boolean = False,
            Optional includeDirectives As Boolean = False,
            Optional includeDocumentationComments As Boolean = False) As SyntaxToken

            Dim nextToken = token.GetNextToken(includeZeroWidth, includeSkipped, includeDirectives, includeDocumentationComments)

            Return If(nextToken.VisualBasicKind = SyntaxKind.None,
                      token.GetAncestor(Of CompilationUnitSyntax).EndOfFileToken,
                      nextToken)
        End Function

        <Extension>
        Public Function IsValidAttributeTarget(token As SyntaxToken) As Boolean
            Return token.VisualBasicKind() = SyntaxKind.AssemblyKeyword OrElse
                   token.VisualBasicKind() = SyntaxKind.ModuleKeyword
        End Function
    End Module
End Namespace