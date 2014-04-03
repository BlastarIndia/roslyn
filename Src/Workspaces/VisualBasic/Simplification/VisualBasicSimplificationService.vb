﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Internal.Log
Imports Microsoft.CodeAnalysis.Simplification
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.VisualBasic.Utilities

#If MEF Then
Imports Microsoft.CodeAnalysis.LanguageServices
#End If

Namespace Microsoft.CodeAnalysis.VisualBasic.Simplification
#If MEF Then
    <ExportLanguageService(GetType(ISimplificationService), LanguageNames.VisualBasic)>
    Partial Friend Class VisualBasicSimplificationService
#Else
    Partial Friend Class VisualBasicSimplificationService
#End If
        Inherits AbstractSimplificationService(Of ExpressionSyntax, ExecutableStatementSyntax, CrefReferenceSyntax)

        Protected Overrides Function GetReducers() As IEnumerable(Of AbstractReducer)
            Return {
                New VisualBasicExtensionMethodReducer(),
                New VisualBasicCastReducer(),
                New VisualBasicNameReducer(),
                New VisualBasicParenthesesReducer(),
                New VisualBasicCallReducer(),
                New VisualBasicEscapingReducer(), ' order before VisualBasicMiscellaneousReducer, see RenameNewOverload test
                New VisualBasicMiscellaneousReducer(),
                New VisualBasicCastReducer(),
                New VisualBasicVariableDeclaratorReducer()
            }
        End Function

        Public Overrides Function Expand(node As SyntaxNode, semanticModel As SemanticModel, aliasReplacementAnnotation As SyntaxAnnotation, expandInsideNode As Func(Of SyntaxNode, Boolean), expandParameter As Boolean, cancellationToken As CancellationToken) As SyntaxNode
            Using Logger.LogBlock(FeatureId.Simplifier, FunctionId.Simplifier_ExpandNode, cancellationToken)
                If TypeOf node Is ExpressionSyntax OrElse
                    TypeOf node Is StatementSyntax OrElse
                    TypeOf node Is AttributeSyntax OrElse
                    TypeOf node Is CrefReferenceSyntax Then

                    Dim rewriter = New Expander(semanticModel, expandInsideNode, cancellationToken, expandParameter, aliasReplacementAnnotation)
                    Return rewriter.Visit(node)
                Else
                    Throw New ArgumentException(
                        VBWorkspaceResources.CannotMakeExplicit,
                        paramName:="node")
                End If
            End Using
        End Function

        Public Overrides Function Expand(token As SyntaxToken, semanticModel As SemanticModel, expandInsideNode As Func(Of SyntaxNode, Boolean), cancellationToken As CancellationToken) As SyntaxToken
            Using Logger.LogBlock(FeatureId.Simplifier, FunctionId.Simplifier_ExpandToken, cancellationToken)
                Dim vbSemanticModel = DirectCast(semanticModel, SemanticModel)
                Dim rewriter = New Expander(vbSemanticModel, expandInsideNode, cancellationToken)
                Return TryEscapeIdentifierToken(rewriter.VisitToken(token), vbSemanticModel)
            End Using
        End Function

        Public Shared Function TryEscapeIdentifierToken(identifierToken As SyntaxToken, semanticModel As SemanticModel, Optional oldIdentifierToken As SyntaxToken? = Nothing) As SyntaxToken
            If identifierToken.VisualBasicKind <> SyntaxKind.IdentifierToken OrElse identifierToken.ValueText.Length = 0 Then
                Return identifierToken
            End If

            If identifierToken.IsBracketed Then
                Return identifierToken
            End If

            If identifierToken.GetTypeCharacter() <> TypeCharacter.None Then
                Return identifierToken
            End If

            Dim unescapedIdentifier = identifierToken.ValueText
            If SyntaxFacts.GetKeywordKind(unescapedIdentifier) = SyntaxKind.None AndAlso SyntaxFacts.GetContextualKeywordKind(unescapedIdentifier) = SyntaxKind.None Then
                Return identifierToken
            End If

            Return identifierToken.CopyAnnotationsTo(
                        SyntaxFactory.BracketedIdentifier(identifierToken.LeadingTrivia, identifierToken.ValueText, identifierToken.TrailingTrivia) _
                            .WithAdditionalAnnotations(Simplifier.Annotation))
        End Function

        Protected Overrides Function GetSpeculativeSemanticModel(ByRef nodeToSpeculate As SyntaxNode, originalSemanticModel As SemanticModel, originalNode As SyntaxNode) As SemanticModel
            Contract.ThrowIfNull(nodeToSpeculate)
            Contract.ThrowIfNull(originalNode)

            Dim speculativeModel As SemanticModel
            Dim methodBlockBase = TryCast(nodeToSpeculate, MethodBlockBaseSyntax)

            ' Speculation over Field Declarations is not supported
            If originalNode.VisualBasicKind() = SyntaxKind.VariableDeclarator AndAlso
               originalNode.Parent.VisualBasicKind() = SyntaxKind.FieldDeclaration Then
                Return originalSemanticModel
            End If

            If methodBlockBase IsNot Nothing Then
                ' Certain reducers for VB (escaping, parentheses) require to operate on the entire method body, rather than individual statements.
                ' Hence, we need to reduce the entire method body as a single unit.
                ' However, there is no SyntaxNode for the method body or statement list, hence NodesAndTokensToReduceComputer added the MethodBlockBaseSyntax to the list of nodes to be reduced.
                ' Here we make sure that we create a speculative semantic model for the method body for the given MethodBlockBaseSyntax.
                Dim originalMethod = DirectCast(originalNode, MethodBlockBaseSyntax)
                Contract.ThrowIfFalse(originalMethod.Statements.Any(), "How did empty method body get reduced?")

                Dim position As Integer
                If originalSemanticModel.IsSpeculativeSemanticModel Then
                    ' Chaining speculative model Not supported, speculate off the original model.
                    Debug.Assert(originalSemanticModel.ParentModel IsNot Nothing)
                    Debug.Assert(Not originalSemanticModel.ParentModel.IsSpeculativeSemanticModel)
                    position = originalSemanticModel.OriginalPositionForSpeculation
                    originalSemanticModel = originalSemanticModel.ParentModel
                Else
                    position = originalMethod.Statements.First.SpanStart
                End If

                speculativeModel = Nothing
                originalSemanticModel.TryGetSpeculativeSemanticModelForMethodBody(position, methodBlockBase, speculativeModel)
                Return speculativeModel
            End If

            Contract.ThrowIfFalse(SpeculationAnalyzer.CanSpeculateOnNode(nodeToSpeculate))

            Dim isAsNewClause = nodeToSpeculate.VisualBasicKind = SyntaxKind.AsNewClause
            If isAsNewClause Then
                ' Currently, there is no support for speculating on an AsNewClauseSyntax node.
                ' So we synthesize an EqualsValueSyntax with the inner NewExpression and speculate on this EqualsValueSyntax node.
                Dim asNewClauseNode = DirectCast(nodeToSpeculate, AsNewClauseSyntax)
                nodeToSpeculate = SyntaxFactory.EqualsValue(asNewClauseNode.NewExpression)
                nodeToSpeculate = asNewClauseNode.CopyAnnotationsTo(nodeToSpeculate)
            End If

            speculativeModel = SpeculationAnalyzer.CreateSpeculativeSemanticModelForNode(originalNode, nodeToSpeculate, DirectCast(originalSemanticModel, SemanticModel))

            If isAsNewClause Then
                nodeToSpeculate = speculativeModel.SyntaxTree.GetRoot()
            End If

            Return speculativeModel
        End Function

        Protected Overrides Function TransformReducedNode(reducedNode As SyntaxNode, originalNode As SyntaxNode) As SyntaxNode
            ' Please see comments within the above GetSpeculativeSemanticModel method for details.

            If originalNode.VisualBasicKind = SyntaxKind.AsNewClause AndAlso reducedNode.VisualBasicKind = SyntaxKind.EqualsValue Then
                Return originalNode.ReplaceNode(DirectCast(originalNode, AsNewClauseSyntax).NewExpression, DirectCast(reducedNode, EqualsValueSyntax).Value)
            End If

            Dim originalMethod = TryCast(originalNode, MethodBlockBaseSyntax)
            If originalMethod IsNot Nothing Then
                Dim reducedMethod = DirectCast(reducedNode, MethodBlockBaseSyntax)
                reducedMethod = reducedMethod.ReplaceNode(reducedMethod.Begin, originalMethod.Begin)
                Return reducedMethod.ReplaceNode(reducedMethod.End, originalMethod.End)
            End If

            Return reducedNode
        End Function

        Protected Overrides Function GetNodesAndTokensToReduce(root As SyntaxNode, isNodeOrTokenOutsideSimplifySpans As Func(Of SyntaxNodeOrToken, Boolean)) As ImmutableArray(Of NodeOrTokenToReduce)
            Return NodesAndTokensToReduceComputer.Compute(root, isNodeOrTokenOutsideSimplifySpans)
        End Function

        Protected Overrides Function CanNodeBeSimplifiedWithoutSpeculation(node As SyntaxNode) As Boolean
            Return node IsNot Nothing AndAlso node.Parent IsNot Nothing AndAlso
                TypeOf node Is VariableDeclaratorSyntax AndAlso
                TypeOf node.Parent Is FieldDeclarationSyntax
        End Function
    End Class
End Namespace
