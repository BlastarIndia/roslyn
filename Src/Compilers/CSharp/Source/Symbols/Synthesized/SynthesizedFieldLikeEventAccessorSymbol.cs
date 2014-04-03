﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    /// <summary>
    /// Event accessor that has been synthesized for a field-like event declared in source.
    /// </summary>
    /// <remarks>
    /// Associated with <see cref="SourceFieldLikeEventSymbol"/>.
    /// </remarks>
    internal sealed class SynthesizedFieldLikeEventAccessorSymbol : SourceEventAccessorSymbol
    {
        // Since we don't have a syntax reference, we'll have to use another object for locking.
        private readonly object methodChecksLockObject = new object();

        private readonly string name;

        internal SynthesizedFieldLikeEventAccessorSymbol(SourceFieldLikeEventSymbol @event, bool isAdder)
            : base(@event, null, null, @event.Locations)
        {
            this.flags = MakeFlags(
                isAdder ? MethodKind.EventAdd : MethodKind.EventRemove,
                @event.Modifiers,
                returnsVoid: false, // until we learn otherwise (in LazyMethodChecks).
                isExtensionMethod: false,
                isMetadataVirtualIgnoringModifiers: false);

            this.name = GetOverriddenAccessorName(@event, isAdder) ??
                SourceEventSymbol.GetAccessorName(@event.Name, isAdder);
        }

        public override string Name
        {
            get { return this.name; }
        }

        public override bool IsImplicitlyDeclared
        {
            get { return true; }
        }

        internal override bool GenerateDebugInfo
        {
            get { return false; }
        }

        protected override SourceMethodSymbol BoundAttributesSource
        {
            get
            {
                return this.MethodKind == MethodKind.EventAdd
                    ? (SourceMethodSymbol)this.AssociatedEvent.RemoveMethod
                    : null;
            }
        }

        protected override IAttributeTargetSymbol AttributeOwner
        {
            get
            {
                // attributes for this accessor are specified on the associated event:
                return AssociatedEvent;
            }
        }

        internal override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations()
        {
            return OneOrMany.Create(this.AssociatedEvent.AttributeDeclarationSyntaxList);
        }

        internal override void AddSynthesizedAttributes(ref ArrayBuilder<SynthesizedAttributeData> attributes)
        {
            base.AddSynthesizedAttributes(ref attributes);

            var compilation = this.DeclaringCompilation;
            AddSynthesizedAttribute(ref attributes, compilation.SynthesizeAttribute(WellKnownMember.System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor));
        }

        protected override object MethodChecksLockObject
        {
            get { return methodChecksLockObject; }
        }
    }
}