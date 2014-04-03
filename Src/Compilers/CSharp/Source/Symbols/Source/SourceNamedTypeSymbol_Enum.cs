﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal partial class SourceNamedTypeSymbol
    {
        private SynthesizedEnumValueFieldSymbol lazyEnumValueField;
        private NamedTypeSymbol lazyEnumUnderlyingType = ErrorTypeSymbol.UnknownResultType;

        /// <summary>
        /// For enum types, gets the underlying type. Returns null on all other
        /// kinds of types.
        /// </summary>
        public override NamedTypeSymbol EnumUnderlyingType
        {
            get
            {
                if (ReferenceEquals(this.lazyEnumUnderlyingType, ErrorTypeSymbol.UnknownResultType))
                {
                    DiagnosticBag diagnostics = DiagnosticBag.GetInstance();
                    if ((object)Interlocked.CompareExchange(ref this.lazyEnumUnderlyingType, this.GetEnumUnderlyingType(diagnostics), ErrorTypeSymbol.UnknownResultType) ==
                        (object)ErrorTypeSymbol.UnknownResultType)
                    {
                        AddSemanticDiagnostics(diagnostics);
                        this.state.NotePartComplete(CompletionPart.EnumUnderlyingType);
                    }
                    diagnostics.Free();
                }

                return this.lazyEnumUnderlyingType;
            }
        }

        private NamedTypeSymbol GetEnumUnderlyingType(DiagnosticBag diagnostics)
        {
            if (this.TypeKind != TypeKind.Enum)
            {
                return null;
            }

            var compilation = this.DeclaringCompilation;
            var decl = this.declaration.Declarations[0];
            var bases = GetBaseListOpt(decl);
            if (bases != null)
            {
                var types = bases.Types;
                if (types.Count > 0)
                {
                    var typeSyntax = types[0];

                    var discardedDiagnostics = DiagnosticBag.GetInstance();
                    var baseBinder = compilation.GetBinder(bases);
                    var type = baseBinder.BindType(typeSyntax, discardedDiagnostics);

                    // We don't want to report diagnostics about types that don't belong in the position
                    // because the parser should already have reported them.  We'll handle use site
                    // diagnostics below.
                    discardedDiagnostics.Free();

                    // Error types are not exposed to the caller. In those
                    // cases, the underlying type is treated as int.
                    if (!type.SpecialType.IsValidEnumUnderlyingType())
                    {
                        type = compilation.GetSpecialType(SpecialType.System_Int32);
                    }

                    Binder.ReportUseSiteDiagnostics(type, diagnostics, typeSyntax);
                    return (NamedTypeSymbol)type;
                }
            }

            NamedTypeSymbol defaultUnderlyingType = compilation.GetSpecialType(SpecialType.System_Int32);
            Binder.ReportUseSiteDiagnostics(defaultUnderlyingType, diagnostics, this.Locations[0]);
            return defaultUnderlyingType;
        }

        /// <summary>
        /// For enum types, returns the synthesized instance field used
        /// for generating metadata. Returns null for non-enum types.
        /// </summary>
        internal FieldSymbol EnumValueField
        {
            get
            {
                if (this.TypeKind != TypeKind.Enum)
                {
                    return null;
                }

                if ((object)this.lazyEnumValueField == null)
                {
                    Debug.Assert((object)this.EnumUnderlyingType != null);
                    Interlocked.CompareExchange(ref this.lazyEnumValueField, new SynthesizedEnumValueFieldSymbol(this), null);
                }

                return this.lazyEnumValueField;
            }
        }

    }
}
