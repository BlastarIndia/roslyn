﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Cci = Microsoft.Cci;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    partial class EventSymbol :
        Cci.IEventDefinition
    {
        #region IEventDefinition Members

        IEnumerable<Cci.IMethodReference> Cci.IEventDefinition.Accessors
        {
            get
            {
                CheckDefinitionInvariant();

                var addMethod = this.AddMethod;
                Debug.Assert((object)addMethod != null);
                yield return addMethod;

                var removeMethod = this.RemoveMethod;
                Debug.Assert((object)removeMethod != null);
                yield return removeMethod;
            }
        }

        Cci.IMethodReference Cci.IEventDefinition.Adder
        {
            get
            {
                CheckDefinitionInvariant();
                MethodSymbol addMethod = this.AddMethod;
                Debug.Assert((object)addMethod != null);
                return addMethod;
            }
        }

        Cci.IMethodReference Cci.IEventDefinition.Remover
        {
            get
            {
                CheckDefinitionInvariant();
                MethodSymbol removeMethod = this.RemoveMethod;
                Debug.Assert((object)removeMethod != null);
                return removeMethod;
            }
        }

        bool Cci.IEventDefinition.IsRuntimeSpecial
        {
            get
            {
                CheckDefinitionInvariant();
                return HasRuntimeSpecialName;
            }
        }

        internal virtual bool HasRuntimeSpecialName
        {
            get
            {
                CheckDefinitionInvariant();
                return false;
            }
        }

        bool Cci.IEventDefinition.IsSpecialName
        {
            get
            {
                CheckDefinitionInvariant();
                return this.HasSpecialName;
            }
        }

        Cci.IMethodReference Cci.IEventDefinition.Caller
        {
            get
            {
                CheckDefinitionInvariant();
                return null; // C# doesn't use the raise/fire accessor
            }
        }

        Cci.ITypeReference Cci.IEventDefinition.GetType(Microsoft.CodeAnalysis.Emit.Context context)
        {
            return ((PEModuleBuilder)context.Module).Translate(this.Type, syntaxNodeOpt: (CSharpSyntaxNode)context.SyntaxNodeOpt, diagnostics: context.Diagnostics);
        }

        #endregion

        #region ITypeDefinitionMember Members

        Cci.ITypeDefinition Cci.ITypeDefinitionMember.ContainingTypeDefinition
        {
            get
            {
                CheckDefinitionInvariant();
                return this.ContainingType;
            }
        }

        Cci.TypeMemberVisibility Cci.ITypeDefinitionMember.Visibility
        {
            get
            {
                CheckDefinitionInvariant();
                return PEModuleBuilder.MemberVisibility(this);
            }
        }

        #endregion

        #region ITypeMemberReference Members

        Cci.ITypeReference Cci.ITypeMemberReference.GetContainingType(Microsoft.CodeAnalysis.Emit.Context context)
        {
            CheckDefinitionInvariant();
            return this.ContainingType;
        }

        #endregion

        #region IReference Members

        void Cci.IReference.Dispatch(Cci.MetadataVisitor visitor)
        {
            CheckDefinitionInvariant();
            visitor.Visit((Cci.IEventDefinition)this);
        }

        Cci.IDefinition Cci.IReference.AsDefinition(Microsoft.CodeAnalysis.Emit.Context context)
        {
            CheckDefinitionInvariant();
            return this;
        }

        #endregion

        #region INamedEntity Members

        string Cci.INamedEntity.Name
        {
            get
            {
                CheckDefinitionInvariant();
                return this.MetadataName;
            }
        }

        #endregion
    }
}
