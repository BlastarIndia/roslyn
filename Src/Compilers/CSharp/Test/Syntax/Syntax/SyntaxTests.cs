﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class SyntaxTests
    {
        private static void AssertIncompleteSubmission(string code)
        {
            AssertCompleteSubmission(code, script: false, interactive: false);
        }

        private static void AssertCompleteSubmission(string code, bool script = true, bool interactive = true)
        {
            Assert.Equal(script, SyntaxFactory.IsCompleteSubmission(SyntaxFactory.ParseSyntaxTree(code, options: TestOptions.Script)));
            Assert.Equal(interactive, SyntaxFactory.IsCompleteSubmission(SyntaxFactory.ParseSyntaxTree(code, options: TestOptions.Interactive)));
        }

        [Fact]
        public void TextIsCompleteSubmission()
        {
            Assert.Throws<ArgumentNullException>(() => SyntaxFactory.IsCompleteSubmission(null));
            AssertCompleteSubmission("");
            AssertCompleteSubmission("//hello");
            AssertCompleteSubmission("@");
            AssertCompleteSubmission("$");
            AssertCompleteSubmission("#");

            AssertIncompleteSubmission("#if F");
            AssertIncompleteSubmission("#region R");
            AssertCompleteSubmission("#r");
            AssertCompleteSubmission("#r \"");
            AssertCompleteSubmission("#define");
            AssertCompleteSubmission("#line \"");
            AssertCompleteSubmission("#pragma");

            AssertIncompleteSubmission("using X; /*");

            AssertIncompleteSubmission(@"
void foo() 
{
#if F
}
");

            AssertIncompleteSubmission(@"
void foo() 
{
#region R
}
");
          
            AssertCompleteSubmission("1", script: false, interactive: true);
            AssertCompleteSubmission("1;");

            AssertIncompleteSubmission("\"");
            AssertIncompleteSubmission("'");

            AssertIncompleteSubmission("@\"xxx");
            AssertIncompleteSubmission("/* ");

            AssertIncompleteSubmission("1.");
            AssertIncompleteSubmission("1+");
            AssertIncompleteSubmission("f(");
            AssertIncompleteSubmission("f,");
            AssertIncompleteSubmission("f(a");
            AssertIncompleteSubmission("f(a,");
            AssertIncompleteSubmission("f(a:");
            AssertIncompleteSubmission("new");
            AssertIncompleteSubmission("new T(");
            AssertIncompleteSubmission("new T {");
            AssertIncompleteSubmission("new T");
            AssertIncompleteSubmission("1 + new T");

            // invalid escape sequence in a string
            AssertCompleteSubmission("\"\\q\"", script: false, interactive: true);

            AssertIncompleteSubmission("void foo(");
            AssertIncompleteSubmission("void foo()");
            AssertIncompleteSubmission("void foo() {");
            AssertCompleteSubmission("void foo() {}");
            AssertCompleteSubmission("void foo() { int a = 1 }");

            AssertIncompleteSubmission("int foo {");
            AssertCompleteSubmission("int foo { }");
            AssertCompleteSubmission("int foo { get }");

            AssertIncompleteSubmission("enum foo {");
            AssertCompleteSubmission("enum foo {}");
            AssertCompleteSubmission("enum foo { a = }");
            AssertIncompleteSubmission("class foo {");
            AssertCompleteSubmission("class foo {}");
            AssertCompleteSubmission("class foo { void }");
            AssertIncompleteSubmission("struct foo {");
            AssertCompleteSubmission("struct foo {}");
            AssertCompleteSubmission("[A struct foo {}");
            AssertIncompleteSubmission("interface foo {");
            AssertCompleteSubmission("interface foo {}");
            AssertCompleteSubmission("interface foo : {}");

            AssertCompleteSubmission("partial", script: false, interactive: true);
            AssertIncompleteSubmission("partial class");

            AssertIncompleteSubmission("int x = 1");
            AssertCompleteSubmission("int x = 1;");

            AssertIncompleteSubmission("delegate T F()");
            AssertIncompleteSubmission("delegate T F<");
            AssertCompleteSubmission("delegate T F();");

            AssertIncompleteSubmission("using");
            AssertIncompleteSubmission("using X");
            AssertCompleteSubmission("using X;");

            AssertIncompleteSubmission("extern");
            AssertIncompleteSubmission("extern alias");
            AssertIncompleteSubmission("extern alias X");
            AssertCompleteSubmission("extern alias X;");

            AssertIncompleteSubmission("[");
            AssertIncompleteSubmission("[A");
            AssertCompleteSubmission("[assembly: A]");

            AssertIncompleteSubmission("try");
            AssertIncompleteSubmission("try {");
            AssertIncompleteSubmission("try { }");
            AssertIncompleteSubmission("try { } finally");
            AssertIncompleteSubmission("try { } finally {");
            AssertIncompleteSubmission("try { } catch");
            AssertIncompleteSubmission("try { } catch {");
            AssertIncompleteSubmission("try { } catch (");
            AssertIncompleteSubmission("try { } catch (Exception");
            AssertIncompleteSubmission("try { } catch (Exception e");
            AssertIncompleteSubmission("try { } catch (Exception e)");
            AssertIncompleteSubmission("try { } catch (Exception e) {");
        }

        [Fact]
        public void TestBug530094()
        {
            var t = SyntaxFactory.AccessorDeclaration(SyntaxKind.UnknownAccessorDeclaration);
        }
    }
}
