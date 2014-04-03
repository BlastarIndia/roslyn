// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FxCopAnalyzers.Globalization;
using Microsoft.CodeAnalysis.FxCopAnalyzers.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.FxCopAnalyzers.Globalization
{
    [DiagnosticAnalyzer]
    [ExportDiagnosticAnalyzer(RuleId, LanguageNames.CSharp)]
    public class CSharpCA1309DiagnosticAnalyzer : CA1309DiagnosticAnalyzer
    {
        protected override AbstractCodeBlockAnalyzer GetAnalyzer(INamedTypeSymbol stringComparisonType)
        {
            return new Analyzer(stringComparisonType);
        }

        private sealed class Analyzer : AbstractCodeBlockAnalyzer, ISyntaxNodeAnalyzer<SyntaxKind>
        {
            private static readonly ImmutableArray<SyntaxKind> kindsOfInterest = ImmutableArray.Create(
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression,
                SyntaxKind.InvocationExpression);

            public Analyzer(INamedTypeSymbol stringComparisonType)
                : base(stringComparisonType)
            {
            }

            public ImmutableArray<SyntaxKind> SyntaxKindsOfInterest
            {
                get
                {
                    return kindsOfInterest;
                }
            }

            public void AnalyzeNode(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken)
            {
                var kind = node.CSharpKind();
                if (kind == SyntaxKind.InvocationExpression)
                {
                    AnalyzeInvocationExpression((InvocationExpressionSyntax)node, semanticModel, addDiagnostic);
                }
                else if (kind == SyntaxKind.EqualsExpression || kind == SyntaxKind.NotEqualsExpression)
                {
                    AnalyzeBinaryExpression((BinaryExpressionSyntax)node, semanticModel, addDiagnostic);
                }
            }

            private void AnalyzeInvocationExpression(InvocationExpressionSyntax node, SemanticModel model, Action<Diagnostic> addDiagnostic)
            {
                if (node.Expression.CSharpKind() == SyntaxKind.SimpleMemberAccessExpression)
                {
                    var memberAccess = (MemberAccessExpressionSyntax)node.Expression;
                    var expressionType = model.GetSymbolInfo(memberAccess.Expression).Symbol;
                    if (expressionType != null)
                    {
                        var methodSymbol = model.GetSymbolInfo(memberAccess.Name).Symbol as IMethodSymbol;
                        if (methodSymbol != null && methodSymbol.ContainingType.SpecialType == SpecialType.System_String &&
                            IsEqualsOrCompare(methodSymbol.Name))
                        {
                            if (!IsAcceptableOverload(methodSymbol, model))
                            {
                                // wrong overload
                                addDiagnostic(memberAccess.Name.GetLocation().CreateDiagnostic(Rule));
                            }
                            else
                            {
                                var lastArgument = node.ArgumentList.Arguments.Last();
                                var lastArgSymbol = model.GetSymbolInfo(lastArgument.Expression).Symbol;
                                if (lastArgSymbol != null && lastArgSymbol.ContainingType != null &&
                                    lastArgSymbol.ContainingType.Equals(StringComparisonType) &&
                                    !IsOrdinalOrOrdinalIgnoreCase(lastArgument, model))
                                {
                                    // right overload, wrong value
                                    addDiagnostic(lastArgument.GetLocation().CreateDiagnostic(Rule));
                                }
                            }
                        }
                    }
                }
            }

            private static void AnalyzeBinaryExpression(BinaryExpressionSyntax node, SemanticModel model, Action<Diagnostic> addDiagnostic)
            {
                var leftType = model.GetTypeInfo(node.Left).Type;
                var rightType = model.GetTypeInfo(node.Right).Type;
                if (leftType != null && rightType != null && leftType.SpecialType == SpecialType.System_String && rightType.SpecialType == SpecialType.System_String)
                {
                    addDiagnostic(node.OperatorToken.GetLocation().CreateDiagnostic(Rule));
                }
            }

            private static bool IsOrdinalOrOrdinalIgnoreCase(ArgumentSyntax argumentSyntax, SemanticModel model)
            {
                var argumentSymbol = model.GetSymbolInfo(argumentSyntax.Expression).Symbol;
                if (argumentSymbol != null)
                {
                    return IsOrdinalOrOrdinalIgnoreCase(argumentSymbol.Name);
                }

                return false;
            }
        }
    }
}
