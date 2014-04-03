// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FxCopAnalyzers.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FxCopAnalyzers.Usage
{
    // Complain if the type implements Equals without overloading the equality operator.
    public abstract class CA2231DiagnosticAnalyzer : AbstractNamedTypeAnalyzer, ISymbolAnalyzer
    {
        internal const string RuleId = "CA2231";
        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                         FxCopRulesResources.OverloadOperatorEqualsOnOverridingValueTypeEquals,
                                                                         FxCopRulesResources.OverloadOperatorEqualsOnOverridingValueTypeEquals,
                                                                         FxCopDiagnosticCategory.Usage,
                                                                         DiagnosticSeverity.Warning);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(Rule);
            }
        }

        public override void AnalyzeSymbol(INamedTypeSymbol namedTypeSymbol, Compilation compilation, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken)
        {
            if (namedTypeSymbol.IsValueType && IsOverridesEquals(namedTypeSymbol) && !IsEqualityOperatorImplemented(namedTypeSymbol))
            {
                addDiagnostic(namedTypeSymbol.CreateDiagnostic(Rule));
            }
        }

        private static bool IsOverridesEquals(INamedTypeSymbol symbol)
        {
            // do override Object.Equals?
            return symbol.GetMembers(WellKnownMemberNames.ObjectEquals).OfType<IMethodSymbol>().Where(m => IsEqualsOverride(m)).Any();
        }

        private static bool IsEqualsOverride(IMethodSymbol method)
        {
            return method != null &&
                   method.IsOverride &&
                   method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                   method.Parameters.Length == 1 &&
                   method.Parameters[0].Type.SpecialType == SpecialType.System_Object;
        }

        private static bool IsEqualityOperatorImplemented(INamedTypeSymbol symbol)
        {
            // do implement the equality operator?
            return symbol.GetMembers(WellKnownMemberNames.EqualityOperatorName).OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.UserDefinedOperator).Any() ||
                    symbol.GetMembers(WellKnownMemberNames.InequalityOperatorName).OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.UserDefinedOperator).Any();
        }
    }
}
