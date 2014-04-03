// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FxCopAnalyzers.Shared.Extensions;
using Microsoft.CodeAnalysis.FxCopAnalyzers.Utilities;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FxCopAnalyzers.Design
{
    /// <summary>
    /// CA1012: Abstract classes should not have public constructors
    /// </summary>
    public abstract class CA1012DiagnosticAnalyzer : AbstractNamedTypeAnalyzer
    {
        internal const string RuleId = "CA1012";
        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                         FxCopRulesResources.AbstractTypesShouldNotHavePublicConstructors,
                                                                         FxCopRulesResources.TypeIsAbstractButHasPublicConstructors,
                                                                         FxCopDiagnosticCategory.Design,
                                                                         DiagnosticSeverity.Warning);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(Rule);
            }
        }

        public override void AnalyzeSymbol(INamedTypeSymbol symbol, Compilation compilation, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken)
        {
            if (symbol.IsAbstract)
            {
                // TODO: Should we also check symbol.GetResultantVisibility() == SymbolVisibility.Public?

                var hasAnyPublicConstructors =
                    symbol.InstanceConstructors.Any(
                        (constructor) => constructor.DeclaredAccessibility == Accessibility.Public);

                if (hasAnyPublicConstructors)
                {
                    addDiagnostic(symbol.CreateDiagnostic(Rule, symbol.Name));
                }
            }
        }
    }
}
