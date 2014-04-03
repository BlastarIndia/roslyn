﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    partial class ArrayTypeSymbol :
        Cci.IArrayTypeReference
    {
        Cci.ITypeReference Cci.IArrayTypeReference.GetElementType(Microsoft.CodeAnalysis.Emit.Context context)
        {
            PEModuleBuilder moduleBeingBuilt = (PEModuleBuilder)context.Module;

            var type = moduleBeingBuilt.Translate(this.ElementType, syntaxNodeOpt: (CSharpSyntaxNode)context.SyntaxNodeOpt, diagnostics: context.Diagnostics);

            if (this.CustomModifiers.Length == 0)
            {
                return type;
            }
            else
            {
                return new Cci.ModifiedTypeReference(type, this.CustomModifiers);
            }
        }

        bool Cci.IArrayTypeReference.IsVector
        {
            get
            {
                return this.Rank == 1;
            }
        }

        IEnumerable<int> Cci.IArrayTypeReference.LowerBounds
        {
            get
            {
                for (int i = 0; i < this.Rank; ++i)
                    yield return 0;
            }
        }

        uint Cci.IArrayTypeReference.Rank
        {
            get
            {
                return (uint)this.Rank;
            }
        }

        IEnumerable<ulong> Cci.IArrayTypeReference.Sizes
        {
            get
            {
                return SpecializedCollections.EmptyEnumerable<ulong>();
            }
        }

        bool Cci.ITypeReference.IsEnum
        {
            get { return false; }
        }

        bool Cci.ITypeReference.IsValueType
        {
            get { return false; }
        }

        Cci.ITypeDefinition Cci.ITypeReference.GetResolvedType(Microsoft.CodeAnalysis.Emit.Context context)
        {
            return null;
        }

        Cci.PrimitiveTypeCode Cci.ITypeReference.TypeCode(Microsoft.CodeAnalysis.Emit.Context context)
        {
            return Cci.PrimitiveTypeCode.NotPrimitive;
        }

        TypeHandle Cci.ITypeReference.TypeDef
        {
            get { return default(TypeHandle); }
        }

        Cci.IGenericMethodParameterReference Cci.ITypeReference.AsGenericMethodParameterReference
        {
            get { return null; }
        }

        Cci.IGenericTypeInstanceReference Cci.ITypeReference.AsGenericTypeInstanceReference
        {
            get { return null; }
        }

        Cci.IGenericTypeParameterReference Cci.ITypeReference.AsGenericTypeParameterReference
        {
            get { return null; }
        }

        Cci.INamespaceTypeDefinition Cci.ITypeReference.AsNamespaceTypeDefinition(Microsoft.CodeAnalysis.Emit.Context context)
        {
            return null;
        }

        Cci.INamespaceTypeReference Cci.ITypeReference.AsNamespaceTypeReference
        {
            get { return null; }
        }

        Cci.INestedTypeDefinition Cci.ITypeReference.AsNestedTypeDefinition(Microsoft.CodeAnalysis.Emit.Context context)
        {
            return null;
        }

        Cci.INestedTypeReference Cci.ITypeReference.AsNestedTypeReference
        {
            get { return null; }
        }

        Cci.ISpecializedNestedTypeReference Cci.ITypeReference.AsSpecializedNestedTypeReference
        {
            get { return null; }
        }

        Cci.ITypeDefinition Cci.ITypeReference.AsTypeDefinition(Microsoft.CodeAnalysis.Emit.Context context)
        {
            return null;
        }

        void Cci.IReference.Dispatch(Cci.MetadataVisitor visitor)
        {
            visitor.Visit((Cci.IArrayTypeReference)this);
        }

        Cci.IDefinition Cci.IReference.AsDefinition(Microsoft.CodeAnalysis.Emit.Context context)
        {
            return null;
        }
    }
}
