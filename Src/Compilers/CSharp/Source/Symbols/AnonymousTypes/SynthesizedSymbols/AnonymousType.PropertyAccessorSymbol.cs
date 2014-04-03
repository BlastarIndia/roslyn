﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal sealed partial class AnonymousTypeManager
    {
        /// <summary>
        /// Represents a getter for anonymous type property.
        /// </summary>
        private sealed partial class AnonymousTypePropertyGetAccessorSymbol : SynthesizedMethodBase
        {
            private readonly AnonymousTypePropertySymbol property;

            internal AnonymousTypePropertyGetAccessorSymbol(AnonymousTypePropertySymbol property)
                // winmdobj output only effects setters, so we can always set this to false
                : base(property.ContainingType, SourcePropertyAccessorSymbol.GetAccessorName(property.Name, getNotSet: true, isWinMdOutput: false))
            {
                this.property = property;
            }

            public override MethodKind MethodKind
            {
                get { return MethodKind.PropertyGet; }
            }

            public override bool ReturnsVoid
            {
                get { return false; }
            }

            public override TypeSymbol ReturnType
            {
                get { return this.property.Type; }
            }

            public override ImmutableArray<ParameterSymbol> Parameters
            {
                get { return ImmutableArray<ParameterSymbol>.Empty; }
            }

            public override Symbol AssociatedSymbol
            {
                get { return this.property; }
            }

            public override ImmutableArray<Location> Locations
            {
                get
                {
                    // The accessor for a anonymous type property has the same location as the property.
                    return this.property.Locations;
                }
            }

            public override bool IsOverride
            {
                get { return false; }
            }

            internal sealed override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false)
            {
                return false;
            }

            internal override bool IsMetadataFinal()
            {
                return false;
            }

            internal override void AddSynthesizedAttributes(ref ArrayBuilder<SynthesizedAttributeData> attributes)
            {
                // Do not call base.AddSynthesizedAttributes.
                // Dev11 does not emit DebuggerHiddenAttribute in property accessors
            }
        }
    }
}
