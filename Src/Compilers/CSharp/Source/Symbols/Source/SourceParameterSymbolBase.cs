﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    /// <summary>
    /// Base class for all parameters that are emitted.
    /// </summary>
    internal abstract class SourceParameterSymbolBase : ParameterSymbol
    {
        private readonly Symbol containingSymbol;
        private readonly ushort ordinal;

        public SourceParameterSymbolBase(Symbol containingSymbol, int ordinal)
        {
            Debug.Assert((object)containingSymbol != null);
            this.ordinal = (ushort)ordinal;
            this.containingSymbol = containingSymbol;
        }

        public sealed override bool Equals(object obj)
        {
            if (obj == (object)this)
            {
                return true;
            }

            var symbol = obj as SourceParameterSymbolBase;
            return (object)symbol != null
                && symbol.Ordinal == this.Ordinal
                && Equals(symbol.containingSymbol, this.containingSymbol);
        }

        public sealed override int GetHashCode()
        {
            return Hash.Combine(this.containingSymbol.GetHashCode(), this.Ordinal);
        }

        public sealed override int Ordinal
        {
            get { return this.ordinal; }
        }

        public sealed override Symbol ContainingSymbol
        {
            get { return containingSymbol; }
        }

        public sealed override AssemblySymbol ContainingAssembly
        {
            get { return containingSymbol.ContainingAssembly; }
        }

        internal abstract ConstantValue DefaultValueFromAttributes { get; }

        internal sealed override void AddSynthesizedAttributes(ref ArrayBuilder<SynthesizedAttributeData> attributes)
        {
            base.AddSynthesizedAttributes(ref attributes);

            var compilation = this.DeclaringCompilation;

            if (this.IsParams)
            {
                AddSynthesizedAttribute(ref attributes, compilation.SynthesizeAttribute(WellKnownMember.System_ParamArrayAttribute__ctor));
            }

            // Synthesize DecimalConstantAttribute if we don't have an explicit custom attribute already:
            var defaultValue = this.ExplicitDefaultConstantValue;
            if (defaultValue != ConstantValue.NotAvailable &&
                defaultValue.SpecialType == SpecialType.System_Decimal &&
                DefaultValueFromAttributes == ConstantValue.NotAvailable)
            {
                AddSynthesizedAttribute(ref attributes, compilation.SynthesizeDecimalConstantAttribute(defaultValue.DecimalValue));
            }

            if (this.Type.ContainsDynamic())
            {
                AddSynthesizedAttribute(ref attributes, compilation.SynthesizeDynamicAttribute(this.Type, this.CustomModifiers.Length, this.RefKind));
            }
        }

        internal override bool HasByRefBeforeCustomModifiers
        {
            get
            {
                return false;
            }
        }

        internal abstract ParameterSymbol WithCustomModifiersAndParams(TypeSymbol newType, ImmutableArray<CustomModifier> newCustomModifiers, bool hasByRefBeforeCustomModifiers, bool newIsParams);
    }
}
