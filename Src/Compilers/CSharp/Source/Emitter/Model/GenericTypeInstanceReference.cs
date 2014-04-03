﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.CSharp.Emit
{
    /// <summary>
    /// Represents a reference to a generic type instantiation.
    /// Subclasses represent nested and namespace types.
    /// </summary>
    internal abstract class GenericTypeInstanceReference : NamedTypeReference, Microsoft.Cci.IGenericTypeInstanceReference
    {
        public GenericTypeInstanceReference(NamedTypeSymbol underlyingNamedType)
            : base(underlyingNamedType)
        {
        }

        public sealed override void Dispatch(Microsoft.Cci.MetadataVisitor visitor)
        {
            visitor.Visit((Microsoft.Cci.IGenericTypeInstanceReference)this);
        }

        ImmutableArray<Microsoft.Cci.ITypeReference> Microsoft.Cci.IGenericTypeInstanceReference.GetGenericArguments(Microsoft.CodeAnalysis.Emit.Context context)
        {
            PEModuleBuilder moduleBeingBuilt = (PEModuleBuilder)context.Module;
            var builder = ArrayBuilder<Microsoft.Cci.ITypeReference>.GetInstance();
            foreach (TypeSymbol type in UnderlyingNamedType.TypeArgumentsNoUseSiteDiagnostics)
            {
                builder.Add(moduleBeingBuilt.Translate(type, syntaxNodeOpt: (CSharpSyntaxNode)context.SyntaxNodeOpt, diagnostics: context.Diagnostics));
            }

            return builder.ToImmutableAndFree();
        }

        Microsoft.Cci.INamedTypeReference Microsoft.Cci.IGenericTypeInstanceReference.GenericType
        {
            get
            {
                System.Diagnostics.Debug.Assert(UnderlyingNamedType.OriginalDefinition.IsDefinition);
                return UnderlyingNamedType.OriginalDefinition;
            }
        }
    }
}
