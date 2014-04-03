﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    /// <summary>
    /// A binding for a field initializer, constructor initializer, or parameter default value.  
    /// Represents the result of binding an initial
    /// value expression rather than an block (for that, use a MethodBodySemanticModel).
    /// </summary>
    internal sealed class InitializerSemanticModel : MemberSemanticModel
    {
        // create a SemanticModel for:
        // (a) A true field initializer (field = value) of a named type (incl. Enums) OR
        // (b) A constructor initializer (": this(...)" or ": base(...)") OR
        // (c) A parameter default value
        private InitializerSemanticModel(CSharpCompilation compilation, CSharpSyntaxNode syntax, Symbol symbol, Binder rootBinder, SyntaxTreeSemanticModel parentSemanticModelOpt = null, int speculatedPosition = 0) :
            base(compilation, syntax, symbol, rootBinder, parentSemanticModelOpt, speculatedPosition)
        {
        }

        /// <summary>
        /// Creates a SemanticModel for a true field initializer (field = value) of a named type (incl. Enums).
        /// </summary>
        internal static InitializerSemanticModel Create(CSharpCompilation compilation, CSharpSyntaxNode syntax, FieldSymbol fieldSymbol, Binder rootBinder)
        {
            Debug.Assert(syntax.IsKind(SyntaxKind.VariableDeclarator) || syntax.IsKind(SyntaxKind.EnumMemberDeclaration));
            return new InitializerSemanticModel(compilation, syntax, fieldSymbol, rootBinder);
        }

        /// <summary>
        /// Creates a SemanticModel for a constructor initializer (": this(...)" or ": base(...)").
        /// </summary>
        internal static InitializerSemanticModel Create(CSharpCompilation compilation, ConstructorInitializerSyntax syntax, MethodSymbol methodSymbol, Binder rootBinder)
        {
            return new InitializerSemanticModel(compilation, syntax, methodSymbol, rootBinder);
        }

        /// <summary>
        /// Creates a SemanticModel for a a parameter default value.
        /// </summary>
        internal static InitializerSemanticModel Create(CSharpCompilation compilation, ParameterSyntax syntax, ParameterSymbol parameterSymbol, Binder rootBinder)
        {
            return new InitializerSemanticModel(compilation, syntax, parameterSymbol, rootBinder);
        }

        /// <summary>
        /// Creates a speculative SemanticModel for an initializer node (field initializer, constructor initializer, or parameter default value)
        /// that did not appear in the original source code.
        /// </summary>
        internal static InitializerSemanticModel CreateSpeculative(SyntaxTreeSemanticModel parentSemanticModel, Symbol owner, CSharpSyntaxNode syntax, Binder rootBinder, int position)
        {
            Debug.Assert(parentSemanticModel != null);
            Debug.Assert(syntax != null);
            Debug.Assert(syntax.IsKind(SyntaxKind.EqualsValueClause) || syntax.IsKind(SyntaxKind.ThisConstructorInitializer) || syntax.IsKind(SyntaxKind.BaseConstructorInitializer));
            Debug.Assert(rootBinder != null);
            Debug.Assert(rootBinder.IsSemanticModelBinder);

            return new InitializerSemanticModel(parentSemanticModel.Compilation, syntax, owner, rootBinder, parentSemanticModel, position);
        }

        internal protected override CSharpSyntaxNode GetBindableSyntaxNode(CSharpSyntaxNode node)
        {
            return IsBindableInitializer(node) ? node : base.GetBindableSyntaxNode(node);
        }

        internal override BoundNode GetBoundRoot()
        {
            CSharpSyntaxNode rootSyntax = this.Root;
            switch (rootSyntax.Kind)
            {
                case SyntaxKind.VariableDeclarator:
                    rootSyntax = ((VariableDeclaratorSyntax)rootSyntax).Initializer.Value;
                    break;

                case SyntaxKind.Parameter:
                    var paramDefault = ((ParameterSyntax)rootSyntax).Default;
                    rootSyntax = (paramDefault == null) ? null : paramDefault.Value;
                    break;

                case SyntaxKind.EqualsValueClause:
                    rootSyntax = ((EqualsValueClauseSyntax)rootSyntax).Value;
                    break;

                case SyntaxKind.EnumMemberDeclaration:
                    rootSyntax = ((EnumMemberDeclarationSyntax)rootSyntax).EqualsValue.Value;
                    break;

                case SyntaxKind.BaseConstructorInitializer:
                case SyntaxKind.ThisConstructorInitializer:
                    break;

                default:
                    throw ExceptionUtilities.UnexpectedValue(rootSyntax.Kind);
            }

            return GetUpperBoundNode(GetBindableSyntaxNode(rootSyntax));
        }

        internal override BoundNode Bind(Binder binder, CSharpSyntaxNode node, DiagnosticBag diagnostics)
        {
            EqualsValueClauseSyntax equalsValue = null;

            switch (node.Kind)
            {
                case SyntaxKind.EqualsValueClause:
                    equalsValue = (EqualsValueClauseSyntax)node;
                    break;

                case SyntaxKind.VariableDeclarator:
                    equalsValue = ((VariableDeclaratorSyntax)node).Initializer;
                    break;

                case SyntaxKind.Parameter:
                    equalsValue = ((ParameterSyntax)node).Default;
                    break;

                case SyntaxKind.EnumMemberDeclaration:
                    equalsValue = ((EnumMemberDeclarationSyntax)node).EqualsValue;
                    break;

                case SyntaxKind.BaseConstructorInitializer:
                case SyntaxKind.ThisConstructorInitializer:
                    ConstructorInitializerSyntax constructorInitializer = node as ConstructorInitializerSyntax;
                    if (constructorInitializer != null)
                    {
                        return binder.BindConstructorInitializer(constructorInitializer,
                            (MethodSymbol)MemberSymbol, diagnostics);
                    }
                    break;
            }

            if (equalsValue != null)
            {
                return BindEqualsValue(binder, equalsValue, diagnostics);
            }

            return base.Bind(binder, node, diagnostics);
        }

        private BoundNode BindEqualsValue(Binder binder, EqualsValueClauseSyntax equalsValue, DiagnosticBag diagnostics)
        {
            switch (this.MemberSymbol.Kind)
            {
                case SymbolKind.Field:
                    {
                        var enumField = this.MemberSymbol as SourceEnumConstantSymbol;
                        if ((object)enumField != null)
                        {
                            return binder.BindEnumConstantInitializer(enumField, equalsValue.Value, diagnostics);
                        }

                        var fieldType = ((FieldSymbol)this.MemberSymbol).GetFieldType(binder.FieldsBeingBound);
                        return binder.BindVariableInitializer(equalsValue, fieldType, diagnostics);
                    }

                case SymbolKind.Parameter:
                    {
                        BoundExpression unusedValueBeforeConversion; // not needed.
                        var parameter = (ParameterSymbol)this.MemberSymbol;
                        return binder.BindParameterDefaultValue(parameter.ContainingSymbol,
                            equalsValue,
                            parameter.Type,
                            diagnostics,
                            out unusedValueBeforeConversion);
                    }

                default:
                    Debug.Assert(false, "Unexpected member symbol kind: " + this.MemberSymbol.Kind);
                    return null;
            }
        }

        private bool IsBindableInitializer(CSharpSyntaxNode node)
        {
            // If we are being asked to bind the equals clause (the "=1" part of "double x=1,y=2;"),
            // that's our root and we know how to bind that thing even if it is not an 
            // expression or a statement.

            return (node.Kind == SyntaxKind.EqualsValueClause &&
                (/*enum or parameter initializer*/ this.Root == node || /*field initializer*/ this.Root == node.Parent)) ||
                    ((node.Kind == SyntaxKind.BaseConstructorInitializer || node.Kind == SyntaxKind.ThisConstructorInitializer) && this.Root == node);
        }

        internal override bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, EqualsValueClauseSyntax initializer, out SemanticModel speculativeModel)
        {
            return TryGetSpeculativeSemanticModelCore(parentModel, position, initializer, out speculativeModel);
        }

        internal override bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, ConstructorInitializerSyntax constructorInitializer, out SemanticModel speculativeModel)
        {
            return TryGetSpeculativeSemanticModelCore(parentModel, position, constructorInitializer, out speculativeModel);
        }

        private bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, CSharpSyntaxNode initializer, out SemanticModel speculativeModel)
        {
            Debug.Assert(initializer is EqualsValueClauseSyntax || initializer is ConstructorInitializerSyntax);

            var binder = this.GetEnclosingBinder(position);
            if (binder == null)
            {
                speculativeModel = null;
                return false;
            }

            speculativeModel = CreateSpeculative(parentModel, this.MemberSymbol, initializer, binder, position);
            return true;
        }

        internal override bool TryGetSpeculativeSemanticModelCore(SyntaxTreeSemanticModel parentModel, int position, StatementSyntax statement, out SemanticModel speculativeModel)
        {
            speculativeModel = null;
            return false;
        }

        internal override bool TryGetSpeculativeSemanticModelForMethodBodyCore(SyntaxTreeSemanticModel parentModel, int position, BaseMethodDeclarationSyntax method, out SemanticModel speculativeModel)
        {
            speculativeModel = null;
            return false;
        }

        internal override bool TryGetSpeculativeSemanticModelForMethodBodyCore(SyntaxTreeSemanticModel parentModel, int position, AccessorDeclarationSyntax accessor, out SemanticModel speculativeModel)
        {
            speculativeModel = null;
            return false;
        }
    }
}