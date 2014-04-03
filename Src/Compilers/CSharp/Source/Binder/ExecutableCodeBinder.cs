﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp
{
    /// <summary>
    /// This binder owns and lazily creates the map of SyntaxNodes to Binders associated with
    /// the syntax with which it is created. This binder is not created in reaction to any
    /// specific syntax node type. It is inserted into the binder chain
    /// between the binder which it is constructed with and those that it constructs via
    /// the LocalBinderFactory. 
    /// </summary>
    internal sealed class ExecutableCodeBinder : Binder
    {
        private readonly Symbol memberSymbol;
        private readonly CSharpSyntaxNode root;
        private readonly MethodSymbol owner;
        private SmallDictionary<CSharpSyntaxNode, Binder> lazyBinderMap;

        internal ExecutableCodeBinder(CSharpSyntaxNode root, Symbol memberSymbol, Binder next)
            : this(root, memberSymbol, next, next.Flags)
        {
        }

        internal ExecutableCodeBinder(CSharpSyntaxNode root, Symbol memberSymbol, Binder next, BinderFlags additionalFlags)
            : base(next, (next.Flags | additionalFlags) & ~BinderFlags.AllClearedAtExecutableCodeBoundary)
        {
            this.memberSymbol = memberSymbol;
            this.root = root;
            this.owner = memberSymbol as MethodSymbol;
        }

        internal override Symbol ContainingMemberOrLambda
        {
            get { return this.owner ?? Next.ContainingMemberOrLambda; }
        }

        internal Symbol MemberSymbol { get { return this.memberSymbol; } }

        internal override Binder GetBinder(CSharpSyntaxNode node)
        {
            Binder binder;
            return this.BinderMap.TryGetValue(node, out binder) ? binder : Next.GetBinder(node);
        }

        private SmallDictionary<CSharpSyntaxNode, Binder> BinderMap
        {
            get
            {
                if (this.lazyBinderMap == null)
                {
                    SmallDictionary<CSharpSyntaxNode, Binder> map;
                    var methodSymbol = this.owner;

                    // Ensure that the member symbol is a method symbol.
                    if ((object)methodSymbol != null && this.root != null)
                    {
                        bool sawYield;
                        map = LocalBinderFactory.BuildMap(methodSymbol, this.root, this, out sawYield);
                        if (sawYield && ((MethodSymbol)this.ContainingMemberOrLambda).MethodKind != MethodKind.AnonymousFunction)
                        {
                            for (Binder b = this; b != null; b = b.Next)
                            {
                                var inMethod = b as InMethodBinder;
                                if (inMethod != null)
                                {
                                    inMethod.MakeIterator();
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        map = SmallDictionary<CSharpSyntaxNode, Binder>.Empty;
                    }

                    Interlocked.CompareExchange(ref this.lazyBinderMap, map, null);
                }

                return this.lazyBinderMap;
            }
        }
    }
}