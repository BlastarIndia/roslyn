﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.CodeGen
{
    /// <summary>
    /// Some features of the compiler (such as anonymous types, pay-as-you-go, NoPIA, ...)
    /// rely on all referenced symbols to go through translate mechanism. Because by default
    /// symbol translator does not translate some of indirectly referenced symbols, such as 
    /// type argument, we have to force translation here
    /// 
    /// This class provides unified implementation for this functionality.
    /// </summary>
    internal static class ReferenceDependencyWalker
    {
        public static void VisitReference(Microsoft.Cci.IReference reference, Microsoft.CodeAnalysis.Emit.Context context)
        {
            var typeReference = reference as Microsoft.Cci.ITypeReference;
            if (typeReference != null)
            {
                VisitTypeReference(typeReference, context);
                return;
            }

            var methodReference = reference as Microsoft.Cci.IMethodReference;
            if (methodReference != null)
            {
                VisitMethodReference(methodReference, context);
                return;
            }

            var fieldReference = reference as Microsoft.Cci.IFieldReference;
            if (fieldReference != null)
            {
                VisitFieldReference(fieldReference, context);
                return;
            }
        }

        private static void VisitTypeReference(Microsoft.Cci.ITypeReference typeReference, Microsoft.CodeAnalysis.Emit.Context context)
        {
            Debug.Assert(typeReference != null);

            Microsoft.Cci.IArrayTypeReference arrayType = typeReference as Microsoft.Cci.IArrayTypeReference;
            if (arrayType != null)
            {
                VisitTypeReference(arrayType.GetElementType(context), context);
                return;
            }

            Microsoft.Cci.IPointerTypeReference pointerType = typeReference as Microsoft.Cci.IPointerTypeReference;
            if (pointerType != null)
            {
                VisitTypeReference(pointerType.GetTargetType(context), context);
                return;
            }

            Debug.Assert(!(typeReference is Microsoft.Cci.IManagedPointerTypeReference));
            //Microsoft.Cci.IManagedPointerTypeReference managedPointerType = typeReference as Microsoft.Cci.IManagedPointerTypeReference;
            //if (managedPointerType != null)
            //{
            //    VisitTypeReference(managedPointerType.GetTargetType(this.context));
            //    return;
            //}

            Microsoft.Cci.IModifiedTypeReference modifiedType = typeReference as Microsoft.Cci.IModifiedTypeReference;
            if (modifiedType != null)
            {
                foreach (var custModifier in modifiedType.CustomModifiers)
                {
                    VisitTypeReference(custModifier.GetModifier(context), context);
                }
                VisitTypeReference(modifiedType.UnmodifiedType, context);
                return;
            }

            // Visit containing type
            Microsoft.Cci.INestedTypeReference nestedType = typeReference.AsNestedTypeReference;
            if (nestedType != null)
            {
                VisitTypeReference(nestedType.GetContainingType(context), context);
            }

            // Visit generic arguments
            Microsoft.Cci.IGenericTypeInstanceReference genericInstance = typeReference.AsGenericTypeInstanceReference;
            if (genericInstance != null)
            {
                foreach (var arg in genericInstance.GetGenericArguments(context))
                {
                    VisitTypeReference(arg, context);
                }
            }
        }

        private static void VisitMethodReference(Microsoft.Cci.IMethodReference methodReference, Microsoft.CodeAnalysis.Emit.Context context)
        {
            Debug.Assert(methodReference != null);

            // Visit containing type
            VisitTypeReference(methodReference.GetContainingType(context), context);

            // Visit generic arguments if any
            Microsoft.Cci.IGenericMethodInstanceReference genericInstance = methodReference.AsGenericMethodInstanceReference;
            if (genericInstance != null)
            {
                foreach (var arg in genericInstance.GetGenericArguments(context))
                {
                    VisitTypeReference(arg, context);
                }
                methodReference = genericInstance.GetGenericMethod(context);
            }

            // Translate substituted method to original definition
            Microsoft.Cci.ISpecializedMethodReference specializedMethod = methodReference.AsSpecializedMethodReference;
            if (specializedMethod != null)
            {
                methodReference = specializedMethod.UnspecializedVersion;
            }

            // Visit parameter types
            VisitParameters(methodReference.GetParameters(context), context);

            if (methodReference.AcceptsExtraArguments)
            {
                VisitParameters(methodReference.ExtraParameters, context);
            }

            // Visit return value type
            VisitTypeReference(methodReference.GetType(context), context);
            if (methodReference.ReturnValueIsModified)
            {
                foreach (var typeModifier in methodReference.ReturnValueCustomModifiers)
                {
                    VisitTypeReference(typeModifier.GetModifier(context), context);
                }
            }
        }

        private static void VisitParameters(ImmutableArray<Microsoft.Cci.IParameterTypeInformation> parameters, Microsoft.CodeAnalysis.Emit.Context context)
        {
            foreach (var param in parameters)
            {
                VisitTypeReference(param.GetType(context), context);

                if (param.IsModified)
                {
                    foreach (var typeModifier in param.CustomModifiers)
                    {
                        VisitTypeReference(typeModifier.GetModifier(context), context);
                    }
                }
            }
        }

        private static void VisitFieldReference(Microsoft.Cci.IFieldReference fieldReference, Microsoft.CodeAnalysis.Emit.Context context)
        {
            Debug.Assert(fieldReference != null);

            // Visit containing type
            VisitTypeReference(fieldReference.GetContainingType(context), context);

            // Translate substituted field to original definition
            Microsoft.Cci.ISpecializedFieldReference specializedField = fieldReference.AsSpecializedFieldReference;
            if (specializedField != null)
            {
                fieldReference = specializedField.UnspecializedVersion;
            }

            // Visit field type
            VisitTypeReference(fieldReference.GetType(context), context);
        }
    }
}
