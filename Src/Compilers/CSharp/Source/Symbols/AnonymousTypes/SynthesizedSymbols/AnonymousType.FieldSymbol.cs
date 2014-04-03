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
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal sealed partial class AnonymousTypeManager
    {
        /// <summary>
        /// Represents a baking field for an anonymous type template property symbol.
        /// </summary>
        private sealed class AnonymousTypeFieldSymbol : FieldSymbol
        {
            private readonly PropertySymbol property;

            public AnonymousTypeFieldSymbol(PropertySymbol property)
            {
                Debug.Assert((object)property != null);
                this.property = property;
            }

            internal override TypeSymbol GetFieldType(ConsList<FieldSymbol> fieldsBeingBound)
            {
                return this.property.Type;
            }

            public override string Name
            {
                get { return GeneratedNames.MakeAnonymousTypeBackingFieldName(this.property.Name); }
            }

            internal override bool HasSpecialName
            {
                get { return false; }
            }

            internal override bool HasRuntimeSpecialName
            {
                get { return false; }
            }

            internal override bool IsNotSerialized
            {
                get { return false; }
            }

            internal override MarshalPseudoCustomAttributeData MarshallingInformation
            {
                get { return null; }
            }

            internal override int? TypeLayoutOffset
            {
                get { return null; }
            }

            public override ImmutableArray<CustomModifier> CustomModifiers
            {
                get { return ImmutableArray<CustomModifier>.Empty; }
            }

            public override Symbol AssociatedSymbol
            {
                get
                {
                    return this.property;
                }
            }

            public override bool IsReadOnly
            {
                get { return true; }
            }

            public override bool IsVolatile
            {
                get { return false; }
            }

            public override bool IsConst
            {
                get { return false; }
            }

            internal sealed override ObsoleteAttributeData ObsoleteAttributeData
            {
                get { return null; }
            }

            internal override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress, bool earlyDecodingWellKnownAttributes)
            {
                return null;
            }

            public override Symbol ContainingSymbol
            {
                get { return this.property.ContainingType; }
            }

            public override NamedTypeSymbol ContainingType
            {
                get
                {
                    return this.property.ContainingType;
                }
            }

            public override ImmutableArray<Location> Locations
            {
                get { return ImmutableArray<Location>.Empty; }
            }

            public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
            {
                get
                {
                    return ImmutableArray<SyntaxReference>.Empty;
                }
            }

            public override Accessibility DeclaredAccessibility
            {
                get { return Accessibility.Private; }
            }

            public override bool IsStatic
            {
                get { return false; }
            }

            public override bool IsImplicitlyDeclared
            {
                get { return true; }
            }

            internal override void AddSynthesizedAttributes(ref ArrayBuilder<SynthesizedAttributeData> attributes)
            {
                base.AddSynthesizedAttributes(ref attributes);

                AnonymousTypeManager manager = ((AnonymousTypeTemplateSymbol)this.ContainingSymbol).Manager;

                AddSynthesizedAttribute(ref attributes, manager.Compilation.SynthesizeAttribute(
                    WellKnownMember.System_Diagnostics_DebuggerBrowsableAttribute__ctor,
                    ImmutableArray.Create(
                        new TypedConstant(manager.System_Diagnostics_DebuggerBrowsableState, TypedConstantKind.Enum, DebuggerBrowsableState.Never))));
            }
        }
    }
}