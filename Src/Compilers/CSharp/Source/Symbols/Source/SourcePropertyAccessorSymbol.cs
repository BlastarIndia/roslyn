﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal sealed class SourcePropertyAccessorSymbol : SourceMethodSymbol
    {
        private readonly SourcePropertySymbol property;
        private ImmutableArray<ParameterSymbol> lazyParameters;
        private TypeSymbol lazyReturnType;
        private ImmutableArray<CustomModifier> lazyReturnTypeCustomModifiers;
        private readonly ImmutableArray<MethodSymbol> explicitInterfaceImplementations;
        private readonly string name;
        private readonly bool isAutoPropertyAccessor;

        public static SourcePropertyAccessorSymbol CreateAccessorSymbol(
            NamedTypeSymbol containingType,
            SourcePropertySymbol property,
            DeclarationModifiers propertyModifiers,
            string propertyName,
            AccessorDeclarationSyntax syntax,
            PropertySymbol explicitlyImplementedPropertyOpt,
            string aliasQualifierOpt,
            bool isAutoPropertyAccessor,
            DiagnosticBag diagnostics)
        {
            Debug.Assert(syntax.Kind == SyntaxKind.GetAccessorDeclaration || syntax.Kind == SyntaxKind.SetAccessorDeclaration);

            bool isGetMethod = (syntax.Kind == SyntaxKind.GetAccessorDeclaration);
            bool isWinMd = property.IsCompilationOutputWinMdObj();
            string name;
            ImmutableArray<MethodSymbol> explicitInterfaceImplementations;
            if ((object)explicitlyImplementedPropertyOpt == null)
            {
                name = GetAccessorName(propertyName, isGetMethod, isWinMd);
                explicitInterfaceImplementations = ImmutableArray<MethodSymbol>.Empty;
            }
            else
            {
                MethodSymbol implementedAccessor = isGetMethod ? explicitlyImplementedPropertyOpt.GetMethod : explicitlyImplementedPropertyOpt.SetMethod;
                string accessorName = (object)implementedAccessor != null ? implementedAccessor.Name
                    : GetAccessorName(explicitlyImplementedPropertyOpt.MetadataName, isGetMethod, isWinMd); //Not name - could be indexer placeholder
                name = ExplicitInterfaceHelpers.GetMemberName(accessorName, explicitlyImplementedPropertyOpt.ContainingType, aliasQualifierOpt);
                explicitInterfaceImplementations =
                    (object)implementedAccessor == null ?
                        ImmutableArray<MethodSymbol>.Empty :
                        ImmutableArray.Create<MethodSymbol>(implementedAccessor);
            }

            var methodKind = isGetMethod ? MethodKind.PropertyGet : MethodKind.PropertySet;
            return new SourcePropertyAccessorSymbol(
                containingType,
                name,
                property,
                propertyModifiers,
                explicitInterfaceImplementations,
                syntax.Keyword.GetLocation(),
                syntax,
                methodKind,
                isAutoPropertyAccessor,
                diagnostics);
        }

        private SourcePropertyAccessorSymbol(
            NamedTypeSymbol containingType,
            string name,
            SourcePropertySymbol property,
            DeclarationModifiers propertyModifiers,
            ImmutableArray<MethodSymbol> explicitInterfaceImplementations,
            Location location,
            AccessorDeclarationSyntax syntax,
            MethodKind methodKind,
            bool isAutoPropertyAccessor,
            DiagnosticBag diagnostics) :
            base(containingType, syntax.GetReference(), syntax.Body.GetReferenceOrNull(), location)
        {
            this.property = property;
            this.explicitInterfaceImplementations = explicitInterfaceImplementations;
            this.name = name;
            this.isAutoPropertyAccessor = isAutoPropertyAccessor;

            bool modifierErrors;
            var declarationModifiers = this.MakeModifiers(syntax, location, diagnostics, out modifierErrors);

            // Include modifiers from the containing property.
            propertyModifiers &= ~DeclarationModifiers.AccessibilityMask;
            if ((declarationModifiers & DeclarationModifiers.Private) != 0)
            {
                // Private accessors cannot be virtual.
                propertyModifiers &= ~DeclarationModifiers.Virtual;
            }
            declarationModifiers |= propertyModifiers;

            // ReturnsVoid property is overridden in this class so
            // returnsVoid argument to MakeFlags is ignored.
            this.flags = MakeFlags(methodKind, declarationModifiers, returnsVoid: false, isExtensionMethod: false,
                isMetadataVirtualIgnoringModifiers: explicitInterfaceImplementations.Any());

            var bodyOpt = syntax.Body;
            if (bodyOpt != null)
            {
                if (containingType.IsInterface)
                {
                    diagnostics.Add(ErrorCode.ERR_InterfaceMemberHasBody, location, this);
                }
                else if (IsExtern && !IsAbstract)
                {
                    diagnostics.Add(ErrorCode.ERR_ExternHasBody, location, this);
                }
                else if (IsAbstract && !IsExtern)
                {
                    diagnostics.Add(ErrorCode.ERR_AbstractHasBody, location, this);
                }
                // Do not report error for IsAbstract && IsExtern. Dev10 reports CS0180 only
                // in that case ("member cannot be both extern and abstract").
            }

            var info = ModifierUtils.CheckAccessibility(this.DeclarationModifiers);
            if (info != null)
            {
                diagnostics.Add(info, location);
            }

            if (!modifierErrors)
            {
                this.CheckModifiers(location, isAutoPropertyAccessor, diagnostics);
            }

            if (this.IsOverride)
            {
                MethodSymbol overriddenMethod = this.OverriddenMethod;
                if ((object)overriddenMethod != null)
                {
                    // If this accessor is overriding a method from metadata, it is possible that
                    // the name of the overridden method doesn't follow the C# get_X/set_X pattern.
                    // We should copy the name so that the runtime will recognize this as an override.
                    this.name = overriddenMethod.Name;
                }
            }
        }

        protected override void MethodChecks(DiagnosticBag diagnostics)
        {
            // These values may not be final, but we need to have something set here in the
            // event that we need to find the overridden accessor.
            this.lazyParameters = ComputeParameters(diagnostics);
            this.lazyReturnType = ComputeReturnType(diagnostics);
            this.lazyReturnTypeCustomModifiers = ImmutableArray<CustomModifier>.Empty;

            if (this.explicitInterfaceImplementations.Length > 0)
            {
                Debug.Assert(this.explicitInterfaceImplementations.Length == 1);
                MethodSymbol implementedMethod = this.explicitInterfaceImplementations[0];
                CustomModifierUtils.CopyMethodCustomModifiers(implementedMethod, this, out this.lazyReturnType, out this.lazyReturnTypeCustomModifiers, out this.lazyParameters, alsoCopyParamsModifier: false);
            }
            else if (this.IsOverride)
            {
                // This will cause another call to SourceMethodSymbol.LazyMethodChecks, 
                // but that method already handles re-entrancy for exactly this case.
                MethodSymbol overriddenMethod = this.OverriddenMethod;
                if ((object)overriddenMethod != null)
                {
                    CustomModifierUtils.CopyMethodCustomModifiers(overriddenMethod, this, out this.lazyReturnType, out this.lazyReturnTypeCustomModifiers, out this.lazyParameters, alsoCopyParamsModifier: true);
                }
            }
            else if (this.lazyReturnType.SpecialType != SpecialType.System_Void)
            {
                PropertySymbol associatedProperty = this.property;
                this.lazyReturnType = CustomModifierUtils.CopyTypeCustomModifiers(associatedProperty.Type, this.lazyReturnType, RefKind.None, this.ContainingAssembly);
                this.lazyReturnTypeCustomModifiers = associatedProperty.TypeCustomModifiers;
            }
        }

        public override Accessibility DeclaredAccessibility
        {
            get
            {
                var accessibility = this.LocalAccessibility;
                if (accessibility != Accessibility.NotApplicable)
                {
                    return accessibility;
                }

                var propertyAccessibility = this.property.DeclaredAccessibility;
                Debug.Assert(propertyAccessibility != Accessibility.NotApplicable);
                return propertyAccessibility;
            }
        }

        public override Symbol AssociatedSymbol
        {
            get { return this.property; }
        }

        public override bool IsVararg
        {
            get { return false; }
        }

        public override bool ReturnsVoid
        {
            get { return this.ReturnType.SpecialType == SpecialType.System_Void; }
        }

        public override ImmutableArray<ParameterSymbol> Parameters
        {
            get
            {
                LazyMethodChecks();
                return this.lazyParameters;
            }
        }

        public override ImmutableArray<TypeParameterSymbol> TypeParameters
        {
            get { return ImmutableArray<TypeParameterSymbol>.Empty; }
        }

        public override TypeSymbol ReturnType
        {
            get
            {
                LazyMethodChecks();
                return this.lazyReturnType;
            }
        }

        private TypeSymbol ComputeReturnType(DiagnosticBag diagnostics)
        {
            if (this.MethodKind == MethodKind.PropertyGet)
            {
                var type = this.property.Type;
                if (!ContainingType.IsInterfaceType() && type.IsStatic)
                {
                    // '{0}': static types cannot be used as return types
                    diagnostics.Add(ErrorCode.ERR_ReturnTypeIsStaticClass, this.locations[0], type);
                }
                return type;
            }
            else
            {
                var binder = GetBinder();
                return binder.GetSpecialType(SpecialType.System_Void, diagnostics, this.SyntaxNode);
            }
        }

        private Binder GetBinder()
        {
            var compilation = this.DeclaringCompilation;
            var binderFactory = compilation.GetBinderFactory(this.SyntaxTree);
            return binderFactory.GetBinder(this.SyntaxNode);
        }

        public override ImmutableArray<CustomModifier> ReturnTypeCustomModifiers
        {
            get
            {
                LazyMethodChecks();
                return this.lazyReturnTypeCustomModifiers;
            }
        }

        /// <summary>
        /// Return Accessibility declared locally on the accessor, or
        /// NotApplicable if no accessibility was declared explicitly.
        /// </summary>
        internal Accessibility LocalAccessibility
        {
            get { return ModifierUtils.EffectiveAccessibility(this.DeclarationModifiers); }
        }

        private DeclarationModifiers MakeModifiers(AccessorDeclarationSyntax syntax, Location location, DiagnosticBag diagnostics, out bool modifierErrors)
        {
            // No default accessibility. If unset, accessibility
            // will be inherited from the property.
            const DeclarationModifiers defaultAccess = DeclarationModifiers.None;

            // Check that the set of modifiers is allowed
            const DeclarationModifiers allowedModifiers = DeclarationModifiers.AccessibilityMask;
            var mods = ModifierUtils.MakeAndCheckNontypeMemberModifiers(syntax.Modifiers, defaultAccess, allowedModifiers, location, diagnostics, out modifierErrors);

            // For interface, check there are no accessibility modifiers.
            // (This check is handled outside of MakeAndCheckModifiers
            // since a distinct error message is reported for interfaces.)
            if (this.ContainingType.IsInterface)
            {
                if ((mods & DeclarationModifiers.AccessibilityMask) != 0)
                {
                    diagnostics.Add(ErrorCode.ERR_PropertyAccessModInInterface, location, this);
                    mods = (mods & ~DeclarationModifiers.AccessibilityMask);
                }
            }

            return mods;
        }

        private void CheckModifiers(Location location, bool isAutoPropertyAccessor, DiagnosticBag diagnostics)
        {
            // Check accessibility against the accessibility declared on the accessor not the property.
            var localAccessibility = this.LocalAccessibility;

            if (IsAbstract && !ContainingType.IsAbstract && ContainingType.TypeKind == TypeKind.Class)
            {
                // '{0}' is abstract but it is contained in non-abstract class '{1}'
                diagnostics.Add(ErrorCode.ERR_AbstractInConcreteClass, location, this, ContainingType);
            }
            else if (IsVirtual && ContainingType.IsSealed)
            {
                // '{0}' is a new virtual member in sealed class '{1}'
                diagnostics.Add(ErrorCode.ERR_NewVirtualInSealed, location, this, ContainingType);
            }
            else if (blockSyntaxReference == null && !IsExtern && !IsAbstract && !isAutoPropertyAccessor)
            {
                diagnostics.Add(ErrorCode.ERR_ConcreteMissingBody, location, this);
            }
            else if (ContainingType.IsSealed &&
                (localAccessibility == Accessibility.Protected || localAccessibility == Accessibility.ProtectedOrInternal) &&
                !IsOverride)
            {
                diagnostics.Add(AccessCheck.GetProtectedMemberInSealedTypeError(ContainingType), location, this);
            }
        }

        /// <summary>
        /// If we are outputing a .winmdobj then the setter name is put_, not set_.
        /// </summary>
        internal static string GetAccessorName(string propertyName, bool getNotSet, bool isWinMdOutput)
        {
            var prefix = getNotSet ? "get_" : isWinMdOutput ? "put_" : "set_";
            return prefix + propertyName;
        }

        public override ImmutableArray<MethodSymbol> ExplicitInterfaceImplementations
        {
            get
            {
                return this.explicitInterfaceImplementations;
            }
        }

        internal override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations()
        {
            return OneOrMany.Create(((AccessorDeclarationSyntax)this.SyntaxNode).AttributeLists);
        }

        public override string Name
        {
            get
            {
                return this.name;
            }
        }

        public override bool IsImplicitlyDeclared
        {
            get
            {
                // Per design meeting resolution [see bug 11253], no source accessor is implicitly declared in C#,
                // because there is the "get" or "set" syntax.
                return false;
            }
        }

        internal override bool GenerateDebugInfo
        {
            get
            {
                return true;
            }
        }

        private ImmutableArray<ParameterSymbol> ComputeParameters(DiagnosticBag diagnostics)
        {
            bool isGetMethod = this.MethodKind == MethodKind.PropertyGet;
            var propertyParameters = property.Parameters;
            int nPropertyParameters = propertyParameters.Length;
            int nParameters = nPropertyParameters + (isGetMethod ? 0 : 1);

            if (nParameters == 0)
            {
                return ImmutableArray<ParameterSymbol>.Empty;
            }

            var parameters = ArrayBuilder<ParameterSymbol>.GetInstance(nParameters);

            // Clone the property parameters for the accessor method. The
            // parameters are cloned (rather than referenced from the property)
            // since the ContainingSymbol needs to be set to the accessor.
            foreach (SourceParameterSymbol propertyParam in propertyParameters)
            {
                parameters.Add(new SourceClonedParameterSymbol(propertyParam, this, propertyParam.Ordinal, suppressOptional: false));
            }

            if (!isGetMethod)
            {
                var propertyType = property.Type;
                if (!ContainingType.IsInterfaceType() && propertyType.IsStatic)
                {
                    // '{0}': static types cannot be used as parameters
                    diagnostics.Add(ErrorCode.ERR_ParameterIsStaticClass, this.locations[0], propertyType);
                }

                parameters.Add(new SynthesizedAccessorValueParameterSymbol(this, propertyType, parameters.Count, property.TypeCustomModifiers));
            }

            return parameters.ToImmutableAndFree();
        }

        internal override void AddSynthesizedAttributes(ref ArrayBuilder<SynthesizedAttributeData> attributes)
        {
            base.AddSynthesizedAttributes(ref attributes);

            if (isAutoPropertyAccessor)
            {
                var compilation = this.DeclaringCompilation;
                AddSynthesizedAttribute(ref attributes, compilation.SynthesizeAttribute(WellKnownMember.System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor));
            }
        }
    }
}
