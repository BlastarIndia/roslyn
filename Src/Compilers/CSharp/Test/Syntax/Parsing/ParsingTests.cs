﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

//#define PARSING_TESTS_DUMP

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public abstract class ParsingTests
    {
        private IEnumerator<SyntaxNodeOrToken> treeEnumerator;

        protected abstract SyntaxTree ParseTree(string text, CSharpParseOptions options);

        protected virtual CSharpSyntaxNode ParseNode(string text, CSharpParseOptions options)
        {
            return ParseTree(text, options).GetCompilationUnitRoot();
        }

        /// <summary>
        /// Parses given string and initializes a depth-first preorder enumerator.
        /// </summary>
        protected SyntaxTree UsingTree(string text, CSharpParseOptions options = null)
        {
            var tree = ParseTree(text, options);
            var nodes = EnumerateNodes(tree.GetCompilationUnitRoot());
#if PARSING_TESTS_DUMP
            nodes = nodes.ToArray(); //force eval to dump contents
#endif
            treeEnumerator = nodes.GetEnumerator();

            return tree;
        }

        /// <summary>
        /// Parses given string and initializes a depth-first preorder enumerator.
        /// </summary>
        protected CSharpSyntaxNode UsingNode(string text, CSharpParseOptions options = null)
        {
            var root = ParseNode(text, options);
            var nodes = EnumerateNodes(root);
#if PARSING_TESTS_DUMP
            nodes = nodes.ToArray(); //force eval to dump contents
#endif
            treeEnumerator = nodes.GetEnumerator();

            return root;
        }

        /// <summary>
        /// Moves the enumerator and asserts that the current node is of the given kind.
        /// </summary>
        [DebuggerHidden]
        protected SyntaxNodeOrToken N(SyntaxKind kind)
        {
            Assert.True(treeEnumerator.MoveNext());
            Assert.Equal(kind, treeEnumerator.Current.CSharpKind());
            return treeEnumerator.Current;
        }

        /// <summary>
        /// Moves the enumerator and asserts that the current node is of the given kind
        /// and is missing.
        /// </summary>
        [DebuggerHidden]
        protected SyntaxNodeOrToken M(SyntaxKind kind)
        {
            Assert.True(treeEnumerator.MoveNext());
            SyntaxNodeOrToken current = this.treeEnumerator.Current;
            Assert.Equal(kind, current.CSharpKind());
            Assert.True(current.IsMissing);
            return current;
        }

        /// <summary>
        /// Moves the enumerator and asserts that the current node is of the given kind
        /// and is missing.
        /// </summary>
        [DebuggerHidden]
        protected void EOF()
        {
            Assert.False(treeEnumerator.MoveNext());
        }

        private static IEnumerable<SyntaxNodeOrToken> EnumerateNodes(CSharpSyntaxNode node)
        {
            Print(node);
            yield return node;

            var stack = new Stack<ChildSyntaxList.Enumerator>(24);
            stack.Push(node.ChildNodesAndTokens().GetEnumerator());
            Open();

            while (stack.Count > 0)
            {
                var en = stack.Pop();
                if (!en.MoveNext())
                {
                    // no more down this branch
                    Close();
                    continue;
                }

                var current = en.Current;
                stack.Push(en); // put it back on stack (struct enumerator)

                Print(current);
                yield return current;

                if (current.IsNode)
                {
                    // not token, so consider children
                    stack.Push(current.ChildNodesAndTokens().GetEnumerator());
                    Open();
                    continue;
                }
            }

            Done();
        }

        [Conditional("PARSING_TESTS_DUMP")]
        private static void Print(SyntaxNodeOrToken node)
        {
            Debug.WriteLine("{0}(SyntaxKind.{1});", node.IsMissing ? "M" : "N", node.CSharpKind());
        }

        [Conditional("PARSING_TESTS_DUMP")]
        private static void Open()
        {
            Debug.WriteLine("{");
        }

        [Conditional("PARSING_TESTS_DUMP")]
        private static void Close()
        {
            Debug.WriteLine("}");
        }

        [Conditional("PARSING_TESTS_DUMP")]
        private static void Done()
        {
            Debug.WriteLine("EOF();");
        }
    }
}
