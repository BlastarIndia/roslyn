﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.Classification
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.VisualBasic.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.Classification
    Friend Module ClassificationHelpers
        ''' <summary>
        ''' Return the classification type associated with this token.
        ''' </summary>
        ''' <param name="token">The token to be classified.</param>
        ''' <returns>The classification type for the token</returns>
        ''' <remarks></remarks>
        Public Function GetClassification(token As SyntaxToken) As String
            If SyntaxFacts.IsKeywordKind(token.VisualBasicKind) Then
                Return ClassificationTypeNames.Keyword
            ElseIf SyntaxFacts.IsPunctuation(token.VisualBasicKind) Then
                Return ClassifyPunctuation(token)
            ElseIf token.VisualBasicKind = SyntaxKind.IdentifierToken Then
                Return ClassifyIdentifierSyntax(token)
            ElseIf token.MatchesKind(SyntaxKind.StringLiteralToken, SyntaxKind.CharacterLiteralToken) Then
                Return ClassificationTypeNames.StringLiteral
            ElseIf token.IsNumericLiteral() Then
                Return ClassificationTypeNames.NumericLiteral
            ElseIf token.VisualBasicKind = SyntaxKind.XmlNameToken Then
                Return ClassificationTypeNames.XmlLiteralName
            ElseIf token.VisualBasicKind = SyntaxKind.XmlTextLiteralToken Then
                Select Case token.Parent.VisualBasicKind
                    Case SyntaxKind.XmlString
                        Return ClassificationTypeNames.XmlLiteralAttributeValue
                    Case SyntaxKind.XmlProcessingInstruction
                        Return ClassificationTypeNames.XmlLiteralProcessingInstruction
                    Case SyntaxKind.XmlComment
                        Return ClassificationTypeNames.XmlLiteralComment
                    Case SyntaxKind.XmlCDataSection
                        Return ClassificationTypeNames.XmlLiteralCDataSection
                    Case Else
                        Return ClassificationTypeNames.XmlLiteralText
                End Select
            ElseIf token.VisualBasicKind = SyntaxKind.XmlEntityLiteralToken Then
                Return ClassificationTypeNames.XmlLiteralEntityReference
            ElseIf token.MatchesKind(SyntaxKind.None, SyntaxKind.BadToken) Then
                Return Nothing
            Else
                Return Contract.FailWithReturn(Of String)("Unhandled token kind: " & token.VisualBasicKind)
            End If
        End Function

        Private Function ClassifyPunctuation(token As SyntaxToken) As String
            If AllOperators.Contains(token.VisualBasicKind) Then
                ' special cases...
                Select Case token.VisualBasicKind
                    Case SyntaxKind.LessThanToken, SyntaxKind.GreaterThanToken
                        If TypeOf token.Parent Is AttributeListSyntax Then
                            Return ClassificationTypeNames.Punctuation
                        End If
                End Select

                Return ClassificationTypeNames.Operator
            Else
                Return ClassificationTypeNames.Punctuation
            End If
        End Function

        Private Function ClassifyIdentifierSyntax(identifier As SyntaxToken) As String
            'Note: parent might be Nothing, if we are classifying raw tokens.
            Dim parent = identifier.Parent

            If TypeOf parent Is TypeStatementSyntax AndAlso DirectCast(parent, TypeStatementSyntax).Identifier = identifier Then
                Return ClassifyTypeDeclarationIdentifier(identifier)
            ElseIf TypeOf parent Is EnumStatementSyntax AndAlso DirectCast(parent, EnumStatementSyntax).Identifier = identifier Then
                Return ClassificationTypeNames.EnumName
            ElseIf TypeOf parent Is DelegateStatementSyntax AndAlso DirectCast(parent, DelegateStatementSyntax).Identifier = identifier AndAlso
                  (parent.VisualBasicKind = SyntaxKind.DelegateSubStatement OrElse parent.VisualBasicKind = SyntaxKind.DelegateFunctionStatement) Then

                Return ClassificationTypeNames.DelegateName
            ElseIf TypeOf parent Is TypeParameterSyntax AndAlso DirectCast(parent, TypeParameterSyntax).Identifier = identifier Then
                Return ClassificationTypeNames.TypeParameterName
            ElseIf (identifier.ToString() = "IsTrue" OrElse identifier.ToString() = "IsFalse") AndAlso
                TypeOf parent Is OperatorStatementSyntax AndAlso DirectCast(parent, OperatorStatementSyntax).OperatorToken = identifier Then

                Return ClassificationTypeNames.Keyword
            Else
                Return ClassificationTypeNames.Identifier
            End If
        End Function

        Private Function ClassifyTypeDeclarationIdentifier(identifier As SyntaxToken) As String
            Select Case identifier.Parent.VisualBasicKind
                Case SyntaxKind.ClassStatement
                    Return ClassificationTypeNames.ClassName
                Case SyntaxKind.ModuleStatement
                    Return ClassificationTypeNames.ModuleName
                Case SyntaxKind.InterfaceStatement
                    Return ClassificationTypeNames.InterfaceName
                Case SyntaxKind.StructureStatement
                    Return ClassificationTypeNames.StructName
                Case Else
                    Return Contract.FailWithReturn(Of String)("Unhandled type declaration")
            End Select
        End Function

        Friend Sub AddLexicalClassifications(text As SourceText, textSpan As TextSpan, result As List(Of ClassifiedSpan), cancellationToken As CancellationToken)
            Dim text2 = text.ToString(textSpan)
            Dim tokens = SyntaxFactory.ParseTokens(text2, initialTokenPosition:=textSpan.Start)
            Worker.CollectClassifiedSpans(tokens, textSpan, result, cancellationToken)
        End Sub

        Friend Function AdjustStaleClassification(text As SourceText, classifiedSpan As ClassifiedSpan) As ClassifiedSpan
            ' TODO: Do we need to do the same work here that we do in C#?
            Return classifiedSpan
        End Function
    End Module
End Namespace
