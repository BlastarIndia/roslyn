﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.CSharp
{
    partial class LocalRewriter
    {
        /// <summary>
        /// This is the entry point for foreach-loop lowering.  It delegates to
        ///   RewriteEnumeratorForEachStatement
        ///   RewriteSingleDimensionalArrayForEachStatement
        ///   RewriteMultiDimensionalArrayForEachStatement
        ///   RewriteStringForEachStatement
        /// </summary>
        /// <remarks>
        /// NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE
        /// We are diverging from the C# 4 spec (and Dev10) to follow the C# 5 spec.
        /// The iteration variable will be declared *inside* each loop iteration,
        /// rather than outside the loop.
        /// NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE NOTE
        /// </remarks>
        public override BoundNode VisitForEachStatement(BoundForEachStatement node)
        {
            // No point in performing this lowering if the node won't be emitted.
            if (node.HasErrors)
            {
                return node;
            }

            BoundExpression collectionExpression = GetUnconvertedCollectionExpression(node);
            TypeSymbol nodeExpressionType = collectionExpression.Type;
            if (nodeExpressionType.Kind == SymbolKind.ArrayType)
            {
                ArrayTypeSymbol arrayType = (ArrayTypeSymbol)nodeExpressionType;
                if (arrayType.Rank == 1)
                {
                    return RewriteSingleDimensionalArrayForEachStatement(node);
                }
                else
                {
                    return RewriteMultiDimensionalArrayForEachStatement(node);
                }
            }
            else if (nodeExpressionType.SpecialType == SpecialType.System_String)
            {
                return RewriteStringForEachStatement(node);
            }
            else
            {
                return RewriteEnumeratorForEachStatement(node);
            }
        }

        /// <summary>
        /// Lower a foreach loop that will enumerate a collection using an enumerator.
        /// 
        /// E e = ((C)(x)).GetEnumerator()
        /// try {
        ///     while (e.MoveNext()) {
        ///         V v = (V)(T)e.Current;
        ///         // body
        ///     }
        /// }
        /// finally {
        ///     // clean up e
        /// }
        /// </summary>
        private BoundStatement RewriteEnumeratorForEachStatement(BoundForEachStatement node)
        {
            ForEachStatementSyntax forEachSyntax = (ForEachStatementSyntax)node.Syntax;

            ForEachEnumeratorInfo enumeratorInfo = node.EnumeratorInfoOpt;
            Debug.Assert(enumeratorInfo != null);

            BoundExpression collectionExpression = GetUnconvertedCollectionExpression(node);
            BoundExpression rewrittenExpression = (BoundExpression)Visit(collectionExpression);
            BoundStatement rewrittenBody = (BoundStatement)Visit(node.Body);

            TypeSymbol enumeratorType = enumeratorInfo.GetEnumeratorMethod.ReturnType;
            TypeSymbol elementType = enumeratorInfo.ElementType;

            // E e
            LocalSymbol enumeratorVar = factory.SynthesizedLocal(enumeratorType, syntax: forEachSyntax, tempKind: TempKind.ForEachEnumerator);

            // Reference to e.
            BoundLocal boundEnumeratorVar = MakeBoundLocal(forEachSyntax, enumeratorVar, enumeratorType);

            // ((C)(x)).GetEnumerator() or (x).GetEnumerator();
            BoundExpression enumeratorVarInitValue = SynthesizeCall(forEachSyntax, rewrittenExpression, enumeratorInfo.GetEnumeratorMethod, enumeratorInfo.CollectionConversion, enumeratorInfo.CollectionType);

            // E e = ((C)(x)).GetEnumerator();
            BoundStatement enumeratorVarDecl = MakeLocalDeclaration(forEachSyntax, enumeratorVar, enumeratorVarInitValue);

            AddForEachExpressionSequencePoint(forEachSyntax, ref enumeratorVarDecl);

            // V v
            LocalSymbol iterationVar = node.IterationVariable;

            //(V)(T)e.Current
            BoundExpression iterationVarAssignValue = MakeConversion(
                syntax: forEachSyntax,
                rewrittenOperand: MakeConversion(
                    syntax: forEachSyntax,
                    rewrittenOperand: BoundCall.Synthesized(
                        syntax: forEachSyntax,
                        receiverOpt: boundEnumeratorVar,
                        method: enumeratorInfo.CurrentPropertyGetter),
                    conversion: enumeratorInfo.CurrentConversion,
                    rewrittenType: elementType,
                    @checked: node.Checked),
                conversion: node.ElementConversion,
                rewrittenType: iterationVar.Type,
                @checked: node.Checked);

            // V v = (V)(T)e.Current;
            BoundStatement iterationVarDecl = MakeLocalDeclaration(forEachSyntax, iterationVar, iterationVarAssignValue);

            AddForEachIterationVariableSequencePoint(forEachSyntax, ref iterationVarDecl);

            // while (e.MoveNext()) {
            //     V v = (V)(T)e.Current;
            //     /* node.Body */
            // }
            BoundStatement whileLoop = RewriteWhileStatement(
                syntax: forEachSyntax,
                rewrittenCondition: BoundCall.Synthesized(
                    syntax: forEachSyntax,
                    receiverOpt: boundEnumeratorVar,
                    method: enumeratorInfo.MoveNextMethod),
                conditionSequencePointSpan: forEachSyntax.InKeyword.Span,
                rewrittenBody: new BoundBlock(rewrittenBody.Syntax,
                    statements: ImmutableArray.Create<BoundStatement>(iterationVarDecl, rewrittenBody),
                    localsOpt: ImmutableArray.Create<LocalSymbol>(iterationVar)),
                breakLabel: node.BreakLabel,
                continueLabel: node.ContinueLabel,
                hasErrors: false);

            BoundStatement result;

            MethodSymbol disposeMethod;
            if (enumeratorInfo.NeedsDisposeMethod && TryGetSpecialTypeMember(forEachSyntax, SpecialMember.System_IDisposable__Dispose, out disposeMethod))
            {
                Binder.ReportDiagnosticsIfObsolete(diagnostics, disposeMethod, forEachSyntax,
                                                   hasBaseReceiver: false,
                                                   containingMember: this.factory.CurrentMethod,
                                                   containingType: this.factory.CurrentClass,
                                                   location: enumeratorInfo.Location);

                BoundBlock finallyBlockOpt;
                var idisposableTypeSymbol = disposeMethod.ContainingType;
                var conversions = new TypeConversions(this.factory.CurrentMethod.ContainingAssembly.CorLibrary);

                HashSet<DiagnosticInfo> useSiteDiagnostics = null;
                var isImplicit = conversions.ClassifyImplicitConversion(enumeratorType, idisposableTypeSymbol, ref useSiteDiagnostics).IsImplicit;
                diagnostics.Add(forEachSyntax, useSiteDiagnostics);

                if (isImplicit)
                {
                    Debug.Assert(enumeratorInfo.NeedsDisposeMethod);

                    Conversion receiverConversion = enumeratorType.IsStructType() ?
                        Conversion.Boxing :
                        Conversion.ImplicitReference;

                    // ((IDisposable)e).Dispose(); or e.Dispose();
                    BoundStatement disposeCall = new BoundExpressionStatement(forEachSyntax,
                        expression: SynthesizeCall(forEachSyntax, boundEnumeratorVar, disposeMethod, receiverConversion, idisposableTypeSymbol));

                    BoundStatement disposeStmt;
                    if (enumeratorType.IsValueType)
                    {
                        // No way for the struct to be nullable and disposable.
                        Debug.Assert(((TypeSymbol)enumeratorType.OriginalDefinition).SpecialType != SpecialType.System_Nullable_T);

                        // For non-nullable structs, no null check is required.
                        disposeStmt = disposeCall;
                    }
                    else
                    {
                        // NB: cast to object missing from spec.  Needed to ignore user-defined operators and box type parameters.
                        // if ((object)e != null) ((IDisposable)e).Dispose(); 
                        disposeStmt = RewriteIfStatement(
                            syntax: forEachSyntax,
                            rewrittenCondition: new BoundBinaryOperator(forEachSyntax,
                                operatorKind: BinaryOperatorKind.NotEqual,
                                left: MakeConversion(
                                    syntax: forEachSyntax,
                                    rewrittenOperand: boundEnumeratorVar,
                                    conversion: enumeratorInfo.EnumeratorConversion,
                                    rewrittenType: this.compilation.GetSpecialType(SpecialType.System_Object),
                                    @checked: false),
                                right: MakeLiteral(forEachSyntax,
                                    constantValue: ConstantValue.Null,
                                    type: null),
                                constantValueOpt: null,
                                methodOpt: null,
                                resultKind: LookupResultKind.Viable,
                                type: this.compilation.GetSpecialType(SpecialType.System_Boolean)),
                            rewrittenConsequence: disposeCall,
                            rewrittenAlternativeOpt: null,
                            hasErrors: false);
                    }

                    finallyBlockOpt = new BoundBlock(forEachSyntax,
                        localsOpt: default(ImmutableArray<LocalSymbol>),
                        statements: ImmutableArray.Create<BoundStatement>(disposeStmt));
                }
                else
                {
                    Debug.Assert(!enumeratorType.IsSealed);

                    // IDisposable d
                    LocalSymbol disposableVar = factory.SynthesizedLocal(idisposableTypeSymbol);

                    // Reference to d.
                    BoundLocal boundDisposableVar = MakeBoundLocal(forEachSyntax, disposableVar, idisposableTypeSymbol);

                    BoundTypeExpression boundIDisposableTypeExpr = new BoundTypeExpression(forEachSyntax,
                        aliasOpt: null,
                        type: idisposableTypeSymbol);

                    // e as IDisposable
                    BoundExpression disposableVarInitValue = new BoundAsOperator(forEachSyntax,
                        operand: boundEnumeratorVar,
                        targetType: boundIDisposableTypeExpr,
                        conversion: Conversion.ExplicitReference, // Explicit so the emitter won't optimize it away.
                        type: idisposableTypeSymbol);

                    // IDisposable d = e as IDisposable;
                    BoundStatement disposableVarDecl = MakeLocalDeclaration(forEachSyntax, disposableVar, disposableVarInitValue);

                    // if (d != null) d.Dispose();
                    BoundStatement ifStmt = RewriteIfStatement(
                        syntax: forEachSyntax,
                        rewrittenCondition: new BoundBinaryOperator(forEachSyntax,
                            operatorKind: BinaryOperatorKind.NotEqual, // reference equality
                            left: boundDisposableVar,
                            right: MakeLiteral(forEachSyntax,
                                constantValue: ConstantValue.Null,
                                type: null),
                            constantValueOpt: null,
                            methodOpt: null,
                            resultKind: LookupResultKind.Viable,
                            type: this.compilation.GetSpecialType(SpecialType.System_Boolean)),
                        rewrittenConsequence: new BoundExpressionStatement(forEachSyntax,
                            expression: BoundCall.Synthesized(
                                syntax: forEachSyntax,
                                receiverOpt: boundDisposableVar,
                                method: disposeMethod)),
                        rewrittenAlternativeOpt: null,
                        hasErrors: false);

                    // IDisposable d = e as IDisposable;
                    // if (d != null) d.Dispose();
                    finallyBlockOpt = new BoundBlock(forEachSyntax,
                        localsOpt: ImmutableArray.Create<LocalSymbol>(disposableVar),
                        statements: ImmutableArray.Create<BoundStatement>(disposableVarDecl, ifStmt));
                }

                // try {
                //     while (e.MoveNext()) {
                //         V v = (V)(T)e.Current;
                //         /* loop body */
                //     }
                // }
                // finally {
                //     /* dispose of e */
                // }
                BoundStatement tryFinally = new BoundTryStatement(forEachSyntax,
                    tryBlock: new BoundBlock(forEachSyntax,
                        localsOpt: ImmutableArray<LocalSymbol>.Empty,
                        statements: ImmutableArray.Create<BoundStatement>(whileLoop)),
                    catchBlocks: ImmutableArray<BoundCatchBlock>.Empty,
                    finallyBlockOpt: finallyBlockOpt);

                // E e = ((C)(x)).GetEnumerator();
                // try {
                //     /* as above */
                result = new BoundBlock(
                    syntax: forEachSyntax,
                    localsOpt: ImmutableArray.Create<LocalSymbol>(enumeratorVar),
                    statements: ImmutableArray.Create<BoundStatement>(enumeratorVarDecl, tryFinally));
            }
            else
            {
                // E e = ((C)(x)).GetEnumerator();
                // while (e.MoveNext()) {
                //     V v = (V)(T)e.Current;
                //     /* loop body */
                // }
                result = new BoundBlock(
                    syntax: forEachSyntax,
                    localsOpt: ImmutableArray.Create<LocalSymbol>(enumeratorVar),
                    statements: ImmutableArray.Create<BoundStatement>(enumeratorVarDecl, whileLoop));
            }

            AddForEachKeywordSequencePoint(forEachSyntax, ref result);

            return result;
        }

        /// <summary>
        /// Synthesize a no-argument call to a given method, possibly applying a conversion to the receiver.
        /// 
        /// If the receiver is of struct type and the method is an interface method, then skip the conversion
        /// and just call the interface method directly - the code generator will detect this and generate a 
        /// constrained virtual call.
        /// </summary>
        /// <param name="syntax">A syntax node to attach to the synthesized bound node.</param>
        /// <param name="receiver">Receiver of method call.</param>
        /// <param name="method">Method to invoke.</param>
        /// <param name="receiverConversion">Conversion to be applied to the receiver if not calling an interface method on a struct.</param>
        /// <param name="convertedReceiverType">Type of the receiver after applying the conversion.</param>
        /// <returns>A BoundExpression representing the call.</returns>
        private BoundExpression SynthesizeCall(CSharpSyntaxNode syntax, BoundExpression receiver, MethodSymbol method, Conversion receiverConversion, TypeSymbol convertedReceiverType)
        {
            if (receiver.Type.TypeKind == TypeKind.Struct && method.ContainingType.IsInterface)
            {
                Debug.Assert(receiverConversion.IsBoxing);

                // NOTE: The spec says that disposing of a struct enumerator won't cause any 
                // unnecessary boxing to occur.  However, Dev10 extends this improvement to the
                // GetEnumerator call as well.

                // We're going to let the emitter take care of avoiding the extra boxing. 
                // When it sees an interface call to a struct, it will generate a constrained
                // virtual call, which will skip boxing, if possible.

                // CONSIDER: In cases where the struct implicitly implements the interface method
                // (i.e. with a public method), we could save a few bytes of IL by creating a 
                // BoundCall to the struct method rather than the interface method (so that the
                // emitter wouldn't need to create a constrained virtual call).  It is not clear 
                // what effect this would have on back compat.

                // NOTE: This call does not correspond to anything that can be written in C# source.
                // We're invoking the interface method directly on the struct (which may have a private
                // explicit implementation).  The code generator knows how to handle it though.

                // receiver.InterfaceMethod()
                return BoundCall.Synthesized(syntax, receiver, method);
            }
            else
            {
                // ((Interface)receiver).InterfaceMethod()
                Debug.Assert(!receiverConversion.IsNumeric);

                return BoundCall.Synthesized(
                    syntax: syntax,
                    receiverOpt: MakeConversion(
                        syntax: syntax,
                        rewrittenOperand: receiver,
                        conversion: receiverConversion,
                        @checked: false,
                        rewrittenType: convertedReceiverType),
                    method: method);
            }
        }

        /// <summary>
        /// Lower a foreach loop that will enumerate the characters of a string.
        /// 
        /// string s = x;
        /// for (int p = 0; p &lt; s.Length; p = p + 1) {
        ///     V v = (V)s.Chars[p];
        ///     // body
        /// }
        /// </summary>
        /// <remarks>
        /// We will follow Dev10 in diverging from the C# 4 spec by ignoring string's 
        /// implementation of IEnumerable and just indexing into its characters.
        /// 
        /// NOTE: We're assuming that sequence points have already been generated.
        /// Otherwise, lowering to for-loops would generated spurious ones.
        /// </remarks>
        private BoundStatement RewriteStringForEachStatement(BoundForEachStatement node)
        {
            ForEachStatementSyntax forEachSyntax = (ForEachStatementSyntax)node.Syntax;

            BoundExpression collectionExpression = GetUnconvertedCollectionExpression(node);
            TypeSymbol stringType = collectionExpression.Type;
            Debug.Assert(stringType.SpecialType == SpecialType.System_String);

            TypeSymbol intType = compilation.GetSpecialType(SpecialType.System_Int32);
            TypeSymbol boolType = compilation.GetSpecialType(SpecialType.System_Boolean);

            BoundExpression rewrittenExpression = (BoundExpression)Visit(collectionExpression);
            BoundStatement rewrittenBody = (BoundStatement)Visit(node.Body);

            // string s;
            LocalSymbol stringVar = factory.SynthesizedLocal(stringType, syntax: forEachSyntax, tempKind: TempKind.ForEachArray);
            // int p;
            LocalSymbol positionVar = factory.SynthesizedLocal(intType, syntax: forEachSyntax, tempKind: TempKind.ForEachArrayIndex0);

            // Reference to s.
            BoundLocal boundStringVar = MakeBoundLocal(forEachSyntax, stringVar, stringType);

            // Reference to p.
            BoundLocal boundPositionVar = MakeBoundLocal(forEachSyntax, positionVar, intType);

            // string s = /*expr*/;
            BoundStatement stringVarDecl = MakeLocalDeclaration(forEachSyntax, stringVar, rewrittenExpression);

            AddForEachExpressionSequencePoint(forEachSyntax, ref stringVarDecl);

            // int p = 0;
            BoundStatement positionVariableDecl = MakeLocalDeclaration(forEachSyntax, positionVar,
                MakeLiteral(forEachSyntax, ConstantValue.Default(SpecialType.System_Int32), intType));

            // string s = /*node.Expression*/; int p = 0;
            BoundStatement initializer = new BoundStatementList(forEachSyntax,
                statements: ImmutableArray.Create<BoundStatement>(stringVarDecl, positionVariableDecl));

            MethodSymbol method = GetSpecialTypeMethod(forEachSyntax, SpecialMember.System_String__Length);
            BoundExpression stringLength = BoundCall.Synthesized(
                    syntax: forEachSyntax,
                    receiverOpt: boundStringVar,
                    method: method,
                    arguments: ImmutableArray<BoundExpression>.Empty);

            // p < s.Length
            BoundExpression exitCondition = new BoundBinaryOperator(
                syntax: forEachSyntax,
                operatorKind: BinaryOperatorKind.IntLessThan,
                left: boundPositionVar,
                right: stringLength,
                constantValueOpt: null,
                methodOpt: null,
                resultKind: LookupResultKind.Viable,
                type: boolType);

            // p = p + 1;
            BoundStatement positionIncrement = MakePositionIncrement(forEachSyntax, boundPositionVar, intType);

            LocalSymbol iterationVar = node.IterationVariable;
            TypeSymbol iterationVarType = iterationVar.Type;
            Debug.Assert(node.ElementConversion.IsValid);

            // (V)s.Chars[p]
            MethodSymbol chars = GetSpecialTypeMethod(forEachSyntax, SpecialMember.System_String__Chars);
            BoundExpression iterationVarInitValue = MakeConversion(
                syntax: forEachSyntax,
                rewrittenOperand: BoundCall.Synthesized(
                    syntax: forEachSyntax,
                    receiverOpt: boundStringVar,
                    method: chars,
                    arguments: ImmutableArray.Create<BoundExpression>(boundPositionVar)),
                conversion: node.ElementConversion,
                rewrittenType: iterationVarType,
                @checked: node.Checked);

            // V v = (V)s.Chars[p];
            BoundStatement iterationVarDecl = MakeLocalDeclaration(forEachSyntax, iterationVar, iterationVarInitValue);

            AddForEachIterationVariableSequencePoint(forEachSyntax, ref iterationVarDecl);

            // { V v = (V)s.Chars[p]; /*node.Body*/ }
            BoundStatement loopBody = new BoundBlock(forEachSyntax,
                localsOpt: ImmutableArray.Create<LocalSymbol>(iterationVar),
                statements: ImmutableArray.Create<BoundStatement>(iterationVarDecl, rewrittenBody));

            // for (string s = /*node.Expression*/, int p = 0; p < s.Length; p = p + 1) {
            //     V v = (V)s.Chars[p];
            //     /*node.Body*/
            // }
            BoundStatement result = RewriteForStatement(
                syntax: forEachSyntax,
                locals: ImmutableArray.Create<LocalSymbol>(stringVar, positionVar),
                rewrittenInitializer: initializer,
                rewrittenCondition: exitCondition,
                conditionSyntax: forEachSyntax.InKeyword,
                rewrittenIncrement: positionIncrement,
                rewrittenBody: loopBody,
                breakLabel: node.BreakLabel,
                continueLabel: node.ContinueLabel,
                hasErrors: node.HasErrors);

            AddForEachKeywordSequencePoint(forEachSyntax, ref result);

            return result;
        }

        /// <summary>
        /// Lower a foreach loop that will enumerate a single-dimensional array.
        /// 
        /// A[] a = x;
        /// for (int p = 0; p &lt; a.Length; p = p + 1) {
        ///     V v = (V)a[p];
        ///     // body
        /// }
        /// </summary>
        /// <remarks>
        /// We will follow Dev10 in diverging from the C# 4 spec by ignoring Array's 
        /// implementation of IEnumerable and just indexing into its elements.
        /// 
        /// NOTE: We're assuming that sequence points have already been generated.
        /// Otherwise, lowering to for-loops would generated spurious ones.
        /// </remarks>
        private BoundStatement RewriteSingleDimensionalArrayForEachStatement(BoundForEachStatement node)
        {
            ForEachStatementSyntax forEachSyntax = (ForEachStatementSyntax)node.Syntax;

            BoundExpression collectionExpression = GetUnconvertedCollectionExpression(node);
            Debug.Assert(collectionExpression.Type.IsArray());

            ArrayTypeSymbol arrayType = (ArrayTypeSymbol)collectionExpression.Type;

            Debug.Assert(arrayType.Rank == 1);

            TypeSymbol intType = compilation.GetSpecialType(SpecialType.System_Int32);
            TypeSymbol boolType = compilation.GetSpecialType(SpecialType.System_Boolean);

            BoundExpression rewrittenExpression = (BoundExpression)Visit(collectionExpression);
            BoundStatement rewrittenBody = (BoundStatement)Visit(node.Body);

            // A[] a
            LocalSymbol arrayVar = factory.SynthesizedLocal(arrayType, syntax: forEachSyntax, tempKind: TempKind.ForEachArray);

            // A[] a = /*node.Expression*/;
            BoundStatement arrayVarDecl = MakeLocalDeclaration(forEachSyntax, arrayVar, rewrittenExpression);

            AddForEachExpressionSequencePoint(forEachSyntax, ref arrayVarDecl);

            // Reference to a.
            BoundLocal boundArrayVar = MakeBoundLocal(forEachSyntax, arrayVar, arrayType);

            // int p
            LocalSymbol positionVar = factory.SynthesizedLocal(intType, syntax: forEachSyntax, tempKind: TempKind.ForEachArrayIndex0);

            // Reference to p.
            BoundLocal boundPositionVar = MakeBoundLocal(forEachSyntax, positionVar, intType);

            // int p = 0;
            BoundStatement positionVarDecl = MakeLocalDeclaration(forEachSyntax, positionVar,
                MakeLiteral(forEachSyntax, ConstantValue.Default(SpecialType.System_Int32), intType));

            // V v
            LocalSymbol iterationVar = node.IterationVariable;
            TypeSymbol iterationVarType = iterationVar.Type;

            // (V)a[p]
            BoundExpression iterationVarInitValue = MakeConversion(
                syntax: forEachSyntax,
                rewrittenOperand: new BoundArrayAccess(
                    syntax: forEachSyntax,
                    expression: boundArrayVar,
                    indices: ImmutableArray.Create<BoundExpression>(boundPositionVar),
                    type: arrayType.ElementType),
                conversion: node.ElementConversion,
                rewrittenType: iterationVarType,
                @checked: node.Checked);

            // V v = (V)a[p];
            BoundStatement iterationVariableDecl = MakeLocalDeclaration(forEachSyntax, iterationVar, iterationVarInitValue);

            AddForEachIterationVariableSequencePoint(forEachSyntax, ref iterationVariableDecl);

            BoundStatement initializer = new BoundStatementList(forEachSyntax,
                        statements: ImmutableArray.Create<BoundStatement>(arrayVarDecl, positionVarDecl));

            // a.Length
            BoundExpression arrayLength = new BoundArrayLength(
                syntax: forEachSyntax,
                expression: boundArrayVar,
                type: intType);

            // p < a.Length
            BoundExpression exitCondition = new BoundBinaryOperator(
                syntax: forEachSyntax,
                operatorKind: BinaryOperatorKind.IntLessThan,
                left: boundPositionVar,
                right: arrayLength,
                constantValueOpt: null,
                methodOpt: null,
                resultKind: LookupResultKind.Viable,
                type: boolType);

            // p = p + 1;
            BoundStatement positionIncrement = MakePositionIncrement(forEachSyntax, boundPositionVar, intType);

            // { V v = (V)a[p]; /* node.Body */ }
            BoundStatement loopBody = new BoundBlock(forEachSyntax,
                localsOpt: ImmutableArray.Create<LocalSymbol>(iterationVar),
                statements: ImmutableArray.Create<BoundStatement>(iterationVariableDecl, rewrittenBody));

            // for (A[] a = /*node.Expression*/, int p = 0; p < a.Length; p = p + 1) {
            //     V v = (V)a[p];
            //     /*node.Body*/
            // }
            BoundStatement result = RewriteForStatement(
                syntax: node.Syntax,
                locals: ImmutableArray.Create<LocalSymbol>(arrayVar, positionVar),
                rewrittenInitializer: initializer,
                rewrittenCondition: exitCondition,
                conditionSyntax: forEachSyntax.InKeyword,
                rewrittenIncrement: positionIncrement,
                rewrittenBody: loopBody,
                breakLabel: node.BreakLabel,
                continueLabel: node.ContinueLabel,
                hasErrors: node.HasErrors);

            AddForEachKeywordSequencePoint(forEachSyntax, ref result);

            return result;
        }

        /// <summary>
        /// Lower a foreach loop that will enumerate a multi-dimensional array.
        /// 
        /// A[...] a = x;
        /// int q_0 = a.GetUpperBound(0), q_1 = a.GetUpperBound(1), ...;
        /// for (int p_0 = a.GetLowerBound(0); p_0 &lt;= q_0; p_0 = p_0 + 1)
        ///     for (int p_1 = a.GetLowerBound(1); p_1 &lt;= q_1; p_1 = p_1 + 1)
        ///         ...
        ///             { V v = (V)a[p_0, p_1, ...]; /* body */ }
        /// </summary>
        /// <remarks>
        /// We will follow Dev10 in diverging from the C# 4 spec by ignoring Array's 
        /// implementation of IEnumerable and just indexing into its elements.
        /// 
        /// NOTE: We're assuming that sequence points have already been generated.
        /// Otherwise, lowering to nested for-loops would generated spurious ones.
        /// </remarks>
        private BoundStatement RewriteMultiDimensionalArrayForEachStatement(BoundForEachStatement node)
        {
            ForEachStatementSyntax forEachSyntax = (ForEachStatementSyntax)node.Syntax;

            BoundExpression collectionExpression = GetUnconvertedCollectionExpression(node);
            Debug.Assert(collectionExpression.Type.IsArray());

            ArrayTypeSymbol arrayType = (ArrayTypeSymbol)collectionExpression.Type;

            int rank = arrayType.Rank;
            Debug.Assert(rank > 1);

            TypeSymbol intType = compilation.GetSpecialType(SpecialType.System_Int32);
            TypeSymbol boolType = compilation.GetSpecialType(SpecialType.System_Boolean);

            // Values we'll use every iteration
            MethodSymbol getLowerBoundMethod = GetSpecialTypeMethod(forEachSyntax, SpecialMember.System_Array__GetLowerBound);
            MethodSymbol getUpperBoundMethod = GetSpecialTypeMethod(forEachSyntax, SpecialMember.System_Array__GetUpperBound);

            BoundExpression rewrittenExpression = (BoundExpression)Visit(collectionExpression);
            BoundStatement rewrittenBody = (BoundStatement)Visit(node.Body);

            // A[...] a
            LocalSymbol arrayVar = factory.SynthesizedLocal(arrayType, syntax: forEachSyntax, tempKind: TempKind.ForEachArray);
            BoundLocal boundArrayVar = MakeBoundLocal(forEachSyntax, arrayVar, arrayType);

            // A[...] a = /*node.Expression*/;
            BoundStatement arrayVarDecl = MakeLocalDeclaration(forEachSyntax, arrayVar, rewrittenExpression);

            AddForEachExpressionSequencePoint(forEachSyntax, ref arrayVarDecl);

            // NOTE: dev10 initializes all of the upper bound temps before entering the loop (as opposed to
            // initializing each one at the corresponding level of nesting).  Doing it at the same time as
            // the lower bound would make this code a bit simpler, but it would make it harder to compare
            // the roslyn and dev10 IL.

            // int q_0, q_1, ...
            LocalSymbol[] upperVar = new LocalSymbol[rank];
            BoundLocal[] boundUpperVar = new BoundLocal[rank];
            BoundStatement[] upperVarDecl = new BoundStatement[rank];
            for (int dimension = 0; dimension < rank; dimension++)
            {
                // int q_/*dimension*/
                upperVar[dimension] = factory.SynthesizedLocal(
                    intType,
                    syntax: forEachSyntax,
                    tempKind: (TempKind)((int)TempKind.ForEachArrayLimit0 + dimension));
                boundUpperVar[dimension] = MakeBoundLocal(forEachSyntax, upperVar[dimension], intType);

                ImmutableArray<BoundExpression> dimensionArgument = ImmutableArray.Create<BoundExpression>(
                    MakeLiteral(forEachSyntax,
                        constantValue: ConstantValue.Create(dimension, ConstantValueTypeDiscriminator.Int32),
                        type: intType));

                // a.GetUpperBound(/*dimension*/)
                BoundExpression currentDimensionUpperBound = BoundCall.Synthesized(forEachSyntax, boundArrayVar, getUpperBoundMethod, dimensionArgument);

                // int q_/*dimension*/ = a.GetUpperBound(/*dimension*/);
                upperVarDecl[dimension] = MakeLocalDeclaration(forEachSyntax, upperVar[dimension], currentDimensionUpperBound);
            }

            // int p_0, p_1, ...
            LocalSymbol[] positionVar = new LocalSymbol[rank];
            BoundLocal[] boundPositionVar = new BoundLocal[rank];
            for (int dimension = 0; dimension < rank; dimension++)
            {
                positionVar[dimension] = factory.SynthesizedLocal(
                    intType,
                    syntax: forEachSyntax,
                    tempKind: (TempKind)((int)TempKind.ForEachArrayIndex0 + dimension));
                boundPositionVar[dimension] = MakeBoundLocal(forEachSyntax, positionVar[dimension], intType);
            }

            // V v
            LocalSymbol iterationVar = node.IterationVariable;
            TypeSymbol iterationVarType = iterationVar.Type;

            // (V)a[p_0, p_1, ...]
            BoundExpression iterationVarInitValue = MakeConversion(
                syntax: forEachSyntax,
                rewrittenOperand: new BoundArrayAccess(forEachSyntax,
                    expression: boundArrayVar,
                    indices: ImmutableArray.Create<BoundExpression>((BoundExpression[])boundPositionVar),
                    type: arrayType.ElementType),
                conversion: node.ElementConversion,
                rewrittenType: iterationVarType,
                @checked: node.Checked);

            // V v = (V)a[p_0, p_1, ...];
            BoundStatement iterationVarDecl = MakeLocalDeclaration(forEachSyntax, iterationVar, iterationVarInitValue);

            AddForEachIterationVariableSequencePoint(forEachSyntax, ref iterationVarDecl);

            // { V v = (V)a[p_0, p_1, ...]; /* node.Body */ }
            BoundStatement innermostLoopBody = new BoundBlock(forEachSyntax,
                localsOpt: ImmutableArray.Create<LocalSymbol>(iterationVar),
                statements: ImmutableArray.Create<BoundStatement>(iterationVarDecl, rewrittenBody));

            // work from most-nested to least-nested
            // for (int p_0 = a.GetLowerBound(0); p_0 <= q_0; p_0 = p_0 + 1)
            //     for (int p_1 = a.GetLowerBound(0); p_1 <= q_1; p_1 = p_1 + 1)
            //         ...
            //             { V v = (V)a[p_0, p_1, ...]; /* node.Body */ }
            BoundStatement forLoop = null;
            for (int dimension = rank - 1; dimension >= 0; dimension--)
            {
                ImmutableArray<BoundExpression> dimensionArgument = ImmutableArray.Create<BoundExpression>(
                    MakeLiteral(forEachSyntax,
                        constantValue: ConstantValue.Create(dimension, ConstantValueTypeDiscriminator.Int32),
                        type: intType));

                // a.GetLowerBound(/*dimension*/)
                BoundExpression currentDimensionLowerBound = BoundCall.Synthesized(forEachSyntax, boundArrayVar, getLowerBoundMethod, dimensionArgument);

                // int p_/*dimension*/ = a.GetLowerBound(/*dimension*/);
                BoundStatement positionVarDecl = MakeLocalDeclaration(forEachSyntax, positionVar[dimension], currentDimensionLowerBound);

                GeneratedLabelSymbol breakLabel = dimension == 0 // outermost for-loop
                    ? node.BreakLabel // i.e. the one that break statements will jump to
                    : new GeneratedLabelSymbol("break"); // Should not affect emitted code since unused

                // p_/*dimension*/ <= q_/*dimension*/  //NB: OrEqual
                BoundExpression exitCondition = new BoundBinaryOperator(
                    syntax: forEachSyntax,
                    operatorKind: BinaryOperatorKind.IntLessThanOrEqual,
                    left: boundPositionVar[dimension],
                    right: boundUpperVar[dimension],
                    constantValueOpt: null,
                    methodOpt: null,
                    resultKind: LookupResultKind.Viable,
                    type: boolType);

                // p_/*dimension*/ = p_/*dimension*/ + 1;
                BoundStatement positionIncrement = MakePositionIncrement(forEachSyntax, boundPositionVar[dimension], intType);

                BoundStatement body;
                GeneratedLabelSymbol continueLabel;

                if (forLoop == null)
                {
                    // innermost for-loop
                    body = innermostLoopBody;
                    continueLabel = node.ContinueLabel; //i.e. the one continue statements will actually jump to
                }
                else
                {
                    body = forLoop;
                    continueLabel = new GeneratedLabelSymbol("continue"); // Should not affect emitted code since unused
                }

                forLoop = RewriteForStatement(
                    syntax: forEachSyntax,
                    locals: ImmutableArray.Create<LocalSymbol>(positionVar[dimension]),
                    rewrittenInitializer: positionVarDecl,
                    rewrittenCondition: exitCondition,
                    conditionSyntax: forEachSyntax.InKeyword,
                    rewrittenIncrement: positionIncrement,
                    rewrittenBody: body,
                    breakLabel: breakLabel,
                    continueLabel: continueLabel,
                    hasErrors: node.HasErrors);
            }

            Debug.Assert(forLoop != null);

            BoundStatement result = new BoundBlock(
                forEachSyntax,
                ImmutableArray.Create<LocalSymbol>(arrayVar).Concat(upperVar.AsImmutableOrNull()),
                ImmutableArray.Create<BoundStatement>(arrayVarDecl).Concat(upperVarDecl.AsImmutableOrNull()).Add(forLoop));

            AddForEachKeywordSequencePoint(forEachSyntax, ref result);

            return result;
        }

        /// <summary>
        /// So that the binding info can return an appropriate SemanticInfo.Converted type for the collection
        /// expression of a foreach node, it is wrapped in a BoundConversion to the collection type in the
        /// initial bound tree.  However, we may be able to optimize away (or entirely disregard) the conversion
        /// so we pull out the bound node for the underlying expression.
        /// </summary>
        private static BoundExpression GetUnconvertedCollectionExpression(BoundForEachStatement node)
        {
            var boundExpression = node.Expression;
            if (boundExpression.Kind == BoundKind.Conversion)
            {
                return ((BoundConversion)boundExpression).Operand;
            }

            // Conversion was an identity conversion and the LocalRewriter must have optimized away the
            // BoundConversion node, we can return the expression itself.
            return boundExpression;
        }

        private static BoundLocal MakeBoundLocal(CSharpSyntaxNode syntax, LocalSymbol local, TypeSymbol type)
        {
            return new BoundLocal(syntax,
                localSymbol: local,
                constantValueOpt: null,
                type: type);
        }

        private BoundStatement MakeLocalDeclaration(CSharpSyntaxNode syntax, LocalSymbol local, BoundExpression rewrittenInitialValue)
        {
            return RewriteLocalDeclaration(
                syntax: syntax,
                localSymbol: local,
                rewrittenInitializer: rewrittenInitialValue,
                wasCompilerGenerated: true);
        }

        // Used to increment integer index into an array or string.
        private BoundStatement MakePositionIncrement(CSharpSyntaxNode syntax, BoundLocal boundPositionVar, TypeSymbol intType)
        {
            // A normal for-loop would have a sequence point on the increment.  We don't want that since the code is synthesized,
            // but we add a hidden sequence point to avoid disrupting the stepping experience.
            return new BoundSequencePoint(null,
                statementOpt: new BoundExpressionStatement(syntax,
                    expression: new BoundAssignmentOperator(syntax,
                        left: boundPositionVar,
                        right: new BoundBinaryOperator(syntax,
                            operatorKind: BinaryOperatorKind.IntAddition, // unchecked, never overflows since array/string index can't be >= Int32.MaxValue
                            left: boundPositionVar,
                            right: MakeLiteral(syntax,
                                constantValue: ConstantValue.Create(1),
                                type: intType),
                            constantValueOpt: null,
                            methodOpt: null,
                            resultKind: LookupResultKind.Viable,
                            type: intType),
                        type: intType)));
        }

        /// <summary>
        /// Add sequence point |here|:
        /// 
        /// foreach (Type var in |expr|) { }
        /// </summary>
        /// <remarks>
        /// Hit once, before looping begins.
        /// </remarks>
        private void AddForEachExpressionSequencePoint(ForEachStatementSyntax forEachSyntax, ref BoundStatement collectionVarDecl)
        {
            if (this.generateDebugInfo)
            {
                // NOTE: This is slightly different from Dev10.  In Dev10, when you stop the debugger
                // on the collection expression, you can see the (uninitialized) iteration variable.
                // In Roslyn, you cannot because the iteration variable is re-declared in each iteration
                // of the loop and is, therefore, not yet in scope.
                collectionVarDecl = new BoundSequencePoint(forEachSyntax.Expression, collectionVarDecl);
            }
        }

        /// <summary>
        /// Add sequence point |here|:
        /// 
        /// foreach (|Type var| in expr) { }
        /// </summary>
        /// <remarks>
        /// Hit every iteration.
        /// </remarks>
        private void AddForEachIterationVariableSequencePoint(ForEachStatementSyntax forEachSyntax, ref BoundStatement iterationVarDecl)
        {
            if (this.generateDebugInfo)
            {
                TextSpan iterationVarDeclSpan = TextSpan.FromBounds(forEachSyntax.Type.SpanStart, forEachSyntax.Identifier.Span.End);
                iterationVarDecl = new BoundSequencePointWithSpan(forEachSyntax, iterationVarDecl, iterationVarDeclSpan);
            }
        }

        /// <summary>
        /// Add sequence point |here|:
        /// 
        /// |foreach| (Type var in expr) { }
        /// </summary>
        /// <remarks>
        /// Hit once, before looping begins.
        /// </remarks>
        private void AddForEachKeywordSequencePoint(ForEachStatementSyntax forEachSyntax, ref BoundStatement result)
        {
            if (this.generateDebugInfo)
            {
                BoundSequencePointWithSpan foreachKeywordSequencePoint = new BoundSequencePointWithSpan(forEachSyntax, null, forEachSyntax.ForEachKeyword.Span);
                result = new BoundStatementList(forEachSyntax, ImmutableArray.Create<BoundStatement>(foreachKeywordSequencePoint, result));
            }
        }
    }
}
