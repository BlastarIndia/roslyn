﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.CodeGeneration
{
    internal class FieldGenerator : AbstractCSharpCodeGenerator
    {
        private static MemberDeclarationSyntax LastField(
            SyntaxList<MemberDeclarationSyntax> members,
            FieldDeclarationSyntax fieldDeclaration)
        {
            var lastConst = members.AsEnumerable()
                              .OfType<FieldDeclarationSyntax>()
                              .Where(f => f.Modifiers.Any(SyntaxKind.ConstKeyword)).LastOrDefault();

            // Place a const after the last existing const.
            if (fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword))
            {
                return lastConst;
            }

            // Place a field after the last field, or after the last const.
            return LastField(members) ?? lastConst;
        }

        internal static CompilationUnitSyntax AddFieldTo(
            CompilationUnitSyntax destination,
            IFieldSymbol field,
            CodeGenerationOptions options,
            IList<bool> availableIndices)
        {
            var declaration = GenerateFieldDeclaration(field, CodeGenerationDestination.CompilationUnit, options);

            // Place the field after the last field or const, or at the start of the type
            // declaration.
            var members = Insert(destination.Members, declaration, options, availableIndices,
                after: m => LastField(m, declaration), before: FirstMember);
            return destination.WithMembers(members.ToSyntaxList());
        }

        internal static TypeDeclarationSyntax AddFieldTo(
            TypeDeclarationSyntax destination,
            IFieldSymbol field,
            CodeGenerationOptions options,
            IList<bool> availableIndices)
        {
            var declaration = GenerateFieldDeclaration(field, GetDestination(destination), options);

            // Place the field after the last field or const, or at the start of the type
            // declaration.
            var members = Insert(destination.Members, declaration, options, availableIndices,
                after: m => LastField(m, declaration), before: FirstMember);

            return AddMembersTo(destination, members);
        }

        public static FieldDeclarationSyntax GenerateFieldDeclaration(
            IFieldSymbol field, CodeGenerationDestination destination, CodeGenerationOptions options)
        {
            var reusableSyntax = GetReuseableSyntaxNodeForSymbol<VariableDeclaratorSyntax>(field, options);
            if (reusableSyntax != null)
            {
                var variableDeclaration = reusableSyntax.Parent as VariableDeclarationSyntax;
                if (variableDeclaration != null)
                {
                    var newVariableDeclaratorsList = new SeparatedSyntaxList<VariableDeclaratorSyntax>().Add(reusableSyntax);
                    var newVariableDeclaration = variableDeclaration.WithVariables(newVariableDeclaratorsList);
                    var fieldDecl = variableDeclaration.Parent as FieldDeclarationSyntax;
                    if (fieldDecl != null)
                    {
                        return fieldDecl.WithDeclaration(newVariableDeclaration);
                    }
                }
            }

            var initializerNode = CodeGenerationFieldInfo.GetInitializer(field) as ExpressionSyntax;

            var initializer = initializerNode != null
                ? SyntaxFactory.EqualsValueClause(initializerNode)
                : GenerateEqualsValue(field);

            var fieldDeclaration = SyntaxFactory.FieldDeclaration(
                AttributeGenerator.GenerateAttributeLists(field.GetAttributes(), options),
                GenerateModifiers(field, options),
                SyntaxFactory.VariableDeclaration(
                    field.Type.GenerateTypeSyntax(),
                    SyntaxFactory.SingletonSeparatedList(
                        AddAnnotationsTo(field, SyntaxFactory.VariableDeclarator(field.Name.ToIdentifierToken(), null, initializer)))));

            return AddCleanupAnnotationsTo(
                ConditionallyAddDocumentationCommentTo(fieldDeclaration, field, options));
        }

        private static EqualsValueClauseSyntax GenerateEqualsValue(IFieldSymbol field)
        {
            if (field.HasConstantValue)
            {
                var canUseFieldReference = field.Type != null && !field.Type.Equals(field.ContainingType);
                return SyntaxFactory.EqualsValueClause(GenerateExpression(field.Type, field.ConstantValue, canUseFieldReference));
            }

            return null;
        }

        private static SyntaxTokenList GenerateModifiers(IFieldSymbol field, CodeGenerationOptions options)
        {
            var tokens = new List<SyntaxToken>();

            AddAccessibilityModifiers(field.DeclaredAccessibility, tokens, options, Accessibility.Private);
            if (field.IsConst)
            {
                tokens.Add(SyntaxFactory.Token(SyntaxKind.ConstKeyword));
            }
            else
            {
                if (field.IsStatic)
                {
                    tokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
                }

                if (field.IsReadOnly)
                {
                    tokens.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
                }
            }

            if (CodeGenerationFieldInfo.GetIsUnsafe(field))
            {
                tokens.Add(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword));
            }

            return tokens.ToSyntaxTokenList();
        }
    }
}