﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    /// <summary>
    /// A binder that knows no symbols and will not delegate further.
    /// </summary>
    internal partial class BuckStopsHereBinder : Binder
    {
        internal BuckStopsHereBinder(CSharpCompilation compilation)
            : base(compilation)
        {
        }

        public override ConsList<LocalSymbol> ImplicitlyTypedLocalsBeingBound
        {
            get
            {
                return ConsList<LocalSymbol>.Empty;
            }
        }

        internal override ConsList<Imports> ImportsList
        {
            get
            {
                return ConsList<Imports>.Empty;
            }
        }

        protected override SourceLocalSymbol LookupLocal(SyntaxToken nameToken)
        {
            return null;
        }

        protected override bool CanHaveMultipleMeanings(string name)
        {
            // everything can have multiple meanings unless proven otherwise.
            return true;
        }

        internal override bool IsAccessible(Symbol symbol, TypeSymbol accessThroughType, out bool failedThroughTypeCheck, ref HashSet<DiagnosticInfo> useSiteDiagnostics, ConsList<Symbol> basesBeingResolved = null)
        {
            failedThroughTypeCheck = false;
            return this.IsSymbolAccessibleConditional(symbol, Compilation.Assembly, ref useSiteDiagnostics);
        }

        internal override ConstantFieldsInProgress ConstantFieldsInProgress
        {
            get
            {
                return ConstantFieldsInProgress.Empty;
            }
        }

        internal override ConsList<FieldSymbol> FieldsBeingBound
        {
            get
            {
                return ConsList<FieldSymbol>.Empty;
            }
        }

        internal override LocalSymbol LocalInProgress
        {
            get
            {
                return null;
            }
        }

        protected override bool IsUnboundTypeAllowed(GenericNameSyntax syntax)
        {
            return false;
        }

        internal override bool IsDirectlyInIterator
        {
            get
            {
                return false;
            }
        }

        internal override bool IsIndirectlyInIterator
        {
            get
            {
                return false;
            }
        }

        internal override GeneratedLabelSymbol BreakLabel
        {
            get
            {
                return null;
            }
        }

        internal override GeneratedLabelSymbol ContinueLabel
        {
            get
            {
                return null;
            }
        }

        // This should only be called in the context of syntactically incorrect programs.  In other
        // contexts statements are surrounded by some enclosing method or lambda.
        internal override TypeSymbol GetIteratorElementType(YieldStatementSyntax node, DiagnosticBag diagnostics)
        {
            // There's supposed to be an enclosing method or lambda.
            throw ExceptionUtilities.Unreachable;
        }

        internal override Symbol ContainingMemberOrLambda
        {
            get
            {
                return null;
            }
        }

        internal override Binder GetBinder(CSharpSyntaxNode node)
        {
            return null;
        }

        internal override BoundSwitchStatement BindSwitchExpressionAndSections(SwitchStatementSyntax node, DiagnosticBag diagnostics)
        {
            // There's supposed to be a SwitchBinder (or other overrider of this method) in the chain.
            throw ExceptionUtilities.Unreachable;
        }

        internal override BoundStatement BindForEachParts(DiagnosticBag diagnostics)
        {
            // There's supposed to be a ForEachLoopBinder (or other overrider of this method) in the chain.
            throw ExceptionUtilities.Unreachable;
        }

        internal override BoundStatement BindUsingStatementParts(DiagnosticBag diagnostics)
        {
            // There's supposed to be a UsingStatementBinder (or other overrider of this method) in the chain.
            throw ExceptionUtilities.Unreachable;
        }

        internal override BoundStatement BindLockStatementParts(DiagnosticBag diagnostics)
        {
            // There's supposed to be a LockBinder (or other overrider of this method) in the chain.
            throw ExceptionUtilities.Unreachable;
        }

        internal override ImmutableArray<LocalSymbol> Locals
        {
            get
            {
                // There's supposed to be a LocalScopeBinder (or other overrider of this method) in the chain.
                throw ExceptionUtilities.Unreachable;
            }
        }

        internal override ImmutableArray<LabelSymbol> Labels
        {
            get
            {
                // There's supposed to be a LocalScopeBinder (or other overrider of this method) in the chain.
                throw ExceptionUtilities.Unreachable;
            }
        }

        protected override bool EnsureInvariantMeaningInScope(Symbol symbol, Location location, string name, DiagnosticBag diagnostics, Symbol colorColorVariable = null)
        {
            // This should never happen in the batch compiler, but it may occur as a result of API accesses
            // (if there's no LocalScopeBinder or other overrider in the chain).  Just indicate that no errors
            // need to be reported.
            return false;
        }

        internal override ImmutableHashSet<Symbol> LockedOrDisposedVariables
        {
            get { return ImmutableHashSet.Create<Symbol>(); }
        }
    }
}