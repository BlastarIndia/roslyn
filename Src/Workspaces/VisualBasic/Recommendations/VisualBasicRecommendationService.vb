﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.Options
Imports Microsoft.CodeAnalysis.Recommendations
Imports Microsoft.CodeAnalysis.Shared.Extensions.ContextQuery
Imports Microsoft.CodeAnalysis.VisualBasic.Extensions.ContextQuery
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

#If MEF Then
Imports Microsoft.CodeAnalysis.LanguageServices
#End If

Namespace Microsoft.CodeAnalysis.VisualBasic.Recommendations
#If MEF Then
    <ExportLanguageService(GetType(IRecommendationService), LanguageNames.VisualBasic)>
    Friend Class VisualBasicRecommendationService
#Else
    Friend Class VisualBasicRecommendationService
#End If
        Inherits AbstractRecommendationService

        Protected Overrides Function GetRecommendedSymbolsAtPositionWorker(
            workspace As Workspace,
            semanticModel As SemanticModel,
            position As Integer,
            options As OptionSet,
            cancellationToken As CancellationToken
        ) As Tuple(Of IEnumerable(Of ISymbol), AbstractSyntaxContext)

            Dim visualBasicSemanticModel = DirectCast(semanticModel, SemanticModel)
            Dim context = VisualBasicSyntaxContext.CreateContext(workspace, visualBasicSemanticModel, position, cancellationToken)

            Dim filterOutOfScopeLocals = options.GetOption(RecommendationOptions.FilterOutOfScopeLocals, semanticModel.Language)
            Dim symbols = GetSymbolsWorker(context, filterOutOfScopeLocals, cancellationToken)

            Dim hideAdvancedMembers = options.GetOption(RecommendationOptions.HideAdvancedMembers, semanticModel.Language)
            symbols = symbols.FilterToVisibleAndBrowsableSymbols(hideAdvancedMembers, visualBasicSemanticModel.Compilation)

            Return Tuple.Create(Of IEnumerable(Of ISymbol), AbstractSyntaxContext)(symbols, context)
        End Function

        Private Function GetSymbolsWorker(
            context As VisualBasicSyntaxContext,
            filterOutOfScopeLocals As Boolean,
            cancellationToken As CancellationToken
        ) As IEnumerable(Of ISymbol)

            If context.SyntaxTree.IsInNonUserCode(context.Position, cancellationToken) OrElse
               context.SyntaxTree.IsInSkippedText(context.Position, cancellationToken) Then
                Return SpecializedCollections.EmptyEnumerable(Of ISymbol)()
            End If

            Dim node = context.TargetToken.Parent
            If context.IsRightOfNameSeparator Then
                If node.VisualBasicKind = SyntaxKind.SimpleMemberAccessExpression Then
                    Return GetSymbolsForMemberAccessExpression(context, DirectCast(node, MemberAccessExpressionSyntax), cancellationToken)
                ElseIf node.VisualBasicKind = SyntaxKind.QualifiedName Then
                    Return GetSymbolsForQualifiedNameSyntax(context, DirectCast(node, QualifiedNameSyntax), cancellationToken)
                End If
            ElseIf context.SyntaxTree.IsQueryIntoClauseContext(context.Position, context.TargetToken, cancellationToken) Then
                Return GetUnqualifiedSymbolsForQueryIntoContext(context, cancellationToken)
            ElseIf context.IsAnyExpressionContext OrElse
                   context.IsSingleLineStatementContext Then
                Return GetUnqualifiedSymbolsForExpressionOrStatementContext(context, filterOutOfScopeLocals, cancellationToken)
            ElseIf context.IsTypeContext OrElse context.IsNamespaceContext Then
                Return GetUnqualifiedSymbolsForType(context, cancellationToken)
            ElseIf context.SyntaxTree.IsLabelContext(context.Position, context.TargetToken, cancellationToken) Then
                Return GetUnqualifiedSymbolsForLabelContext(context, cancellationToken)
            ElseIf context.SyntaxTree.IsRaiseEventContext(context.Position, context.TargetToken, cancellationToken) Then
                Return GetUnqualifiedSymbolsForRaiseEvent(context, cancellationToken)
            End If

            Return SpecializedCollections.EmptyEnumerable(Of ISymbol)()
        End Function

        Private Function GetUnqualifiedSymbolsForQueryIntoContext(
            context As VisualBasicSyntaxContext,
            cancellationToken As CancellationToken
        ) As IEnumerable(Of ISymbol)

            Dim symbols = context.SemanticModel _
                .LookupSymbols(context.TargetToken.SpanStart, includeReducedExtensionMethods:=True)

            Return symbols.OfType(Of IMethodSymbol)().Where(Function(m) m.IsAggregateFunction())
        End Function

        Private Function GetUnqualifiedSymbolsForLabelContext(
            context As VisualBasicSyntaxContext,
            cancellationToken As CancellationToken
        ) As IEnumerable(Of ISymbol)

            Return context.SemanticModel _
                .LookupLabels(context.TargetToken.SpanStart)
        End Function

        Private Function GetUnqualifiedSymbolsForRaiseEvent(
            context As VisualBasicSyntaxContext,
            cancellationToken As CancellationToken
        ) As IEnumerable(Of ISymbol)

            Dim containingType = context.SemanticModel.GetEnclosingSymbol(context.Position, cancellationToken).ContainingType

            Return context.SemanticModel _
                .LookupSymbols(context.Position, container:=containingType) _
                .Where(Function(s) s.Kind = SymbolKind.Event AndAlso s.ContainingType Is containingType)
        End Function

        Private Function GetUnqualifiedSymbolsForType(
            context As VisualBasicSyntaxContext,
            cancellationToken As CancellationToken
        ) As IEnumerable(Of ISymbol)

            Return context.SemanticModel _
                .LookupNamespacesAndTypes(context.TargetToken.SpanStart)
        End Function

        Private Function GetUnqualifiedSymbolsForExpressionOrStatementContext(
            context As VisualBasicSyntaxContext,
            filterOutOfScopeLocals As Boolean,
            cancellationToken As CancellationToken
        ) As IEnumerable(Of ISymbol)

            Dim lookupPosition = context.TargetToken.SpanStart
            If context.FollowsEndOfStatement Then
                lookupPosition = context.Position
            End If

            Dim symbols = If(
                context.TargetToken.Parent.IsInStaticContext(),
                context.SemanticModel.LookupStaticMembers(lookupPosition),
                context.SemanticModel.LookupSymbols(lookupPosition))

            If filterOutOfScopeLocals Then
                Return symbols.Where(Function(symbol) Not symbol.IsInaccessibleLocal(context.Position))
            End If

            Return symbols
        End Function

        Private Function GetSymbolsForQualifiedNameSyntax(
            context As VisualBasicSyntaxContext,
            node As QualifiedNameSyntax,
            cancellationToken As CancellationToken
        ) As IEnumerable(Of ISymbol)

            ' We're in a name-only context, since if we were an expression we'd be a
            ' MemberAccessExpressionSyntax. Thus, let's do other namespaces and types.
            Dim nameBinding = context.SemanticModel.GetSymbolInfo(node.Left, cancellationToken)
            Dim symbol = TryCast(nameBinding.Symbol, INamespaceOrTypeSymbol)
            If symbol Is Nothing Then
                Return SpecializedCollections.EmptyEnumerable(Of ISymbol)()
            End If

            If context.TargetToken.GetAncestor(Of NamespaceStatementSyntax)() IsNot Nothing Then
                Return SpecializedCollections.EmptyEnumerable(Of ISymbol)()
            End If

            Dim symbols = context.SemanticModel _
                .LookupNamespacesAndTypes(position:=node.SpanStart, container:=symbol)

            Dim implementsStatement = TryCast(node.Parent, ImplementsStatementSyntax)
            If implementsStatement IsNot Nothing Then
                Dim couldContainInterface = Function(s As INamedTypeSymbol) s.TypeKind = TypeKind.Class OrElse s.TypeKind = TypeKind.Module OrElse s.TypeKind = TypeKind.Structure

                Dim interfaces = symbols.Where(Function(s) s.Kind = SymbolKind.NamedType AndAlso DirectCast(s, INamedTypeSymbol).TypeKind = TypeKind.Interface).ToList()
                Dim otherTypes = symbols.OfType(Of INamedTypeSymbol).Where(Function(s) s.Kind = SymbolKind.NamedType AndAlso couldContainInterface(s) AndAlso
                                                SubclassContainsInterface(s)).ToList()
                Return interfaces.Concat(otherTypes)
            End If

            Return symbols
        End Function

        Private Function SubclassContainsInterface(symbol As INamedTypeSymbol) As Boolean
            Dim nestedTypes = symbol.GetTypeMembers()
            For Each type As INamedTypeSymbol In nestedTypes
                If type.TypeKind = TypeKind.Interface Then
                    Return True
                End If

                If SubclassContainsInterface(type) Then
                    Return True
                End If
            Next

            Return False
        End Function

        Private Function GetSymbolsForMemberAccessExpression(
            context As VisualBasicSyntaxContext,
            node As MemberAccessExpressionSyntax,
            cancellationToken As CancellationToken
        ) As IEnumerable(Of ISymbol)

            Dim leftExpression = node.GetExpressionOfMemberAccessExpression()
            If leftExpression Is Nothing Then
                Return SpecializedCollections.EmptyEnumerable(Of ISymbol)()
            End If

            Dim leftHandTypeInfo = context.SemanticModel.GetTypeInfo(leftExpression, cancellationToken)
            Dim leftHandBinding = context.SemanticModel.GetSymbolInfo(leftExpression, cancellationToken)

            Dim excludeInstance = False
            Dim excludeShared = True ' do not show shared members by default
            Dim useBaseReferenceAccessibility = False

            Dim container = DirectCast(leftHandTypeInfo.Type, INamespaceOrTypeSymbol)
            If leftHandTypeInfo.Type.IsErrorType AndAlso leftHandBinding.Symbol IsNot Nothing Then
                ' TODO remove this when 531549 which causes leftHandTypeInfo to be an error type is fixed
                container = TryCast(leftHandBinding.Symbol.GetSymbolType(), INamespaceOrTypeSymbol)
            End If

            If leftHandBinding.Symbol IsNot Nothing Then
                Dim firstSymbol = leftHandBinding.Symbol

                Select Case firstSymbol.Kind
                    Case SymbolKind.TypeParameter
                        ' 884060: We don't allow invocations off type parameters.
                        Return SpecializedCollections.EmptyEnumerable(Of ISymbol)()
                    Case SymbolKind.NamedType, SymbolKind.Namespace
                        excludeInstance = True
                        excludeShared = False
                        container = DirectCast(firstSymbol, INamespaceOrTypeSymbol)
                    Case SymbolKind.Alias
                        excludeInstance = True
                        excludeShared = False
                        container = DirectCast(firstSymbol, IAliasSymbol).Target
                    Case SymbolKind.Parameter
                        Dim parameter = DirectCast(firstSymbol, IParameterSymbol)

                        If parameter.IsMe Then
                            excludeShared = False
                        End If

                        ' case:
                        '    MyBase.
                        If parameter.IsMe AndAlso parameter.Type IsNot container Then
                            useBaseReferenceAccessibility = True
                        End If
                End Select

                If container Is Nothing OrElse container.IsType AndAlso DirectCast(container, ITypeSymbol).TypeKind = TypeKind.Enum Then
                    excludeShared = False ' need to allow shared members for enums
                End If

            ElseIf container IsNot Nothing Then
                ' In this case, we don't have any options to set because both instance and shared members
                ' can be called on instances in VB.
            Else
                Return SpecializedCollections.EmptyEnumerable(Of ISymbol)()
            End If

            Debug.Assert(Not excludeInstance OrElse Not excludeShared)
            Debug.Assert(Not excludeInstance OrElse Not useBaseReferenceAccessibility)

            Dim position = node.SpanStart
            Dim symbols = If(
                useBaseReferenceAccessibility,
                context.SemanticModel.LookupBaseMembers(position),
                If(
                    excludeInstance,
                    context.SemanticModel.LookupStaticMembers(position, container),
                    context.SemanticModel.LookupSymbols(position, container, includeReducedExtensionMethods:=True))).AsEnumerable()

            If excludeShared Then
                symbols = symbols.Where(Function(s) Not s.IsShared)
            End If

            ' If the left expression is Me, MyBase or MyClass and we're the first statement of constructor,
            ' we should filter out the parenting constructor. Otherwise, we should filter out all constructors.
            If leftExpression.IsMeMyBaseOrMyClass() AndAlso node.IsFirstStatementInCtor() Then
                Dim parentingCtor = GetEnclosingCtor(context.SemanticModel, node, cancellationToken)
                Debug.Assert(parentingCtor IsNot Nothing)

                symbols = symbols.Where(Function(s) Not s.Equals(parentingCtor)).ToList()
            Else
                symbols = symbols.Where(Function(s) Not s.IsConstructor()).ToList()
            End If

            ' If the left expression is My.MyForms, we should filter out all non-property symbols
            If leftHandBinding.Symbol IsNot Nothing AndAlso
               leftHandBinding.Symbol.IsMyFormsProperty(context.SemanticModel.Compilation) Then

                symbols = symbols.Where(Function(s) s.Kind = SymbolKind.Property)
            End If

            ' Also filter out operators
            symbols = symbols.Where(Function(s) s.Kind <> SymbolKind.Method OrElse DirectCast(s, IMethodSymbol).MethodKind <> MethodKind.UserDefinedOperator)

            Return symbols
        End Function

        Private Function GetEnclosingCtor(
            semanticModel As SemanticModel,
            node As MemberAccessExpressionSyntax,
            cancellationToken As CancellationToken) As IMethodSymbol
            Dim symbol = semanticModel.GetEnclosingSymbol(node.SpanStart, cancellationToken)

            While symbol IsNot Nothing
                Dim method = TryCast(symbol, IMethodSymbol)
                If method IsNot Nothing AndAlso method.MethodKind = MethodKind.Constructor Then
                    Return method
                End If
            End While

            Return Nothing
        End Function
    End Class
End Namespace
