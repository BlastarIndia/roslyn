// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FxCopAnalyzers.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FxCopAnalyzers.Design
{
    public abstract class CA1017DiagnosticAnalyzer : ICompilationStartedAnalyzer
    {
        internal const string RuleId = "CA1017";
        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                                      FxCopRulesResources.MarkAllAssembliesWithComVisible,
                                                                                      "{0}",
                                                                                      FxCopDiagnosticCategory.Design,
                                                                                      DiagnosticSeverity.Warning);        

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(Rule);
            }
        }

        public ICompilationEndedAnalyzer OnCompilationStarted(Compilation compilation, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken)
        {
            if (AssemblyHasPublicTypes(compilation.Assembly))
            {
                var comVisibleAttributeSymbol = WellKnownTypes.ComVisibleAttribute(compilation);
                if (comVisibleAttributeSymbol == null)
                {
                    return null;
                }

                var attributeInstance = compilation.Assembly.GetAttributes().FirstOrDefault(a => a.AttributeClass.Equals(comVisibleAttributeSymbol));

                if (attributeInstance != null)
                {
                    if (attributeInstance.ConstructorArguments.Length > 0 &&
                        attributeInstance.ConstructorArguments[0].Kind == TypedConstantKind.Primitive &&
                        attributeInstance.ConstructorArguments[0].Value != null &
                        attributeInstance.ConstructorArguments[0].Value.Equals(true))
                    {
                        // Has the attribute, with the value 'true'.
                        addDiagnostic(Diagnostic.Create(Rule, Location.None, string.Format(FxCopRulesResources.CA1017_AttributeTrue, compilation.Assembly.Name)));
                    }
                }
                else
                {
                    // No ComVisible attribute at all.
                    addDiagnostic(Diagnostic.Create(Rule, Location.None, string.Format(FxCopRulesResources.CA1017_NoAttribute, compilation.Assembly.Name)));
                }
            }

            return null;
        }

        private static bool AssemblyHasPublicTypes(IAssemblySymbol assembly)
        {
            return assembly
                    .GlobalNamespace
                    .GetMembers()
                    .OfType<INamedTypeSymbol>()
                    .Where(s => s.DeclaredAccessibility == Accessibility.Public)
                    .Any();
        }
    }
}
