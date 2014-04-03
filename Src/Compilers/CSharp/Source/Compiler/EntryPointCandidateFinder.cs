﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal class EntryPointCandidateFinder : CSharpSymbolVisitor<object, object>
    {
        private readonly ArrayBuilder<MethodSymbol> entryPointCandidates;
        private readonly bool visitNestedTypes;
        private readonly CancellationToken cancellationToken;

        public static void FindCandidatesInNamespace(NamespaceSymbol root, ArrayBuilder<MethodSymbol> entryPointCandidates, CancellationToken cancellationToken)
        {
            EntryPointCandidateFinder finder = new EntryPointCandidateFinder(entryPointCandidates, visitNestedTypes: true, cancellationToken: cancellationToken);
            finder.Visit(root);
        }

        public static void FindCandidatesInSingleType(NamedTypeSymbol root, ArrayBuilder<MethodSymbol> entryPointCandidates, CancellationToken cancellationToken)
        {
            EntryPointCandidateFinder finder = new EntryPointCandidateFinder(entryPointCandidates, visitNestedTypes: false, cancellationToken: cancellationToken);
            finder.Visit(root);
        }

        private EntryPointCandidateFinder(ArrayBuilder<MethodSymbol> entryPointCandidates, bool visitNestedTypes, CancellationToken cancellationToken)
        {
            this.entryPointCandidates = entryPointCandidates;
            this.visitNestedTypes = visitNestedTypes;
            this.cancellationToken = cancellationToken;
        }

        public override object VisitNamespace(NamespaceSymbol symbol, object arg)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var s in symbol.GetMembersUnordered())
            {
                s.Accept(this, arg);
            }

            return null;
        }

        public override object VisitNamedType(NamedTypeSymbol symbol, object arg)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var member in symbol.GetMembersUnordered())
            {
                switch (member.Kind)
                {
                    case SymbolKind.NamedType:
                        if (this.visitNestedTypes)
                        {
                            member.Accept(this, arg);
                        }
                        break;

                    case SymbolKind.Method:
                        {
                            MethodSymbol method = (MethodSymbol)member;
                            if (method.IsPartial())
                            {
                                method = method.PartialImplementation();
                                if ((object)method == null)
                                {
                                    continue;
                                }
                            }

                            if (entryPointCandidates != null && method.IsEntryPointCandidate)
                            {
                                entryPointCandidates.Add(method);
                            }
                            break;
                        }
                }
            }

            return null;
        }

        public override object VisitMethod(MethodSymbol symbol, object arg)
        {
            throw ExceptionUtilities.Unreachable;
        }

        public override object VisitProperty(PropertySymbol symbol, object argument)
        {
            throw ExceptionUtilities.Unreachable;
        }

        public override object VisitEvent(EventSymbol symbol, object argument)
        {
            throw ExceptionUtilities.Unreachable;
        }

        public override object VisitField(FieldSymbol symbol, object argument)
        {
            throw ExceptionUtilities.Unreachable;
        }
    }
}
