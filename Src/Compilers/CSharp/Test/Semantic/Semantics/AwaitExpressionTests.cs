﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    /// <summary>
    /// Tests related to binding (but not lowering) await expressions.
    /// </summary>
    public class AwaitExpressionTests : CompilingTestBase
    {
        [Fact]
        [WorkItem(711413)]
        public void TestAwaitInfo()
        {
            var text =
@"using System.Threading.Tasks;

class C
{
    async void Foo(Task<int> t)
    {
        int c = 1 + await t;
    }
}";
            var info = GetAwaitExpressionInfo(text);
            Assert.Equal("System.Runtime.CompilerServices.TaskAwaiter<System.Int32> System.Threading.Tasks.Task<System.Int32>.GetAwaiter()", info.GetAwaiterMethod.ToTestDisplayString());
            Assert.Equal("System.Int32 System.Runtime.CompilerServices.TaskAwaiter<System.Int32>.GetResult()", info.GetResultMethod.ToTestDisplayString());
            Assert.Equal("System.Boolean System.Runtime.CompilerServices.TaskAwaiter<System.Int32>.IsCompleted { get; }", info.IsCompletedProperty.ToTestDisplayString());
        }

        private AwaitExpressionInfo GetAwaitExpressionInfo(string text, params DiagnosticDescription[] diagnostics)
        {
            var tree = Parse(text, options: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp5));
            var comp = CreateCompilationWithMscorlib45(new SyntaxTree[] { tree }, new MetadataReference[] { SystemRef });
            comp.VerifyDiagnostics(diagnostics);
            var syntaxNode = (PrefixUnaryExpressionSyntax)tree.FindNodeOrTokenByKind(SyntaxKind.AwaitExpression).AsNode();
            var treeModel = comp.GetSemanticModel(tree);
            return treeModel.GetAwaitExpressionInfo(syntaxNode);
        }

        [Fact]
        [WorkItem(711413)]
        public void TestAwaitInfoWrongSyntaxKind()
        {
            var text =
@"
class C
{
    int Foo(int t)
    {
        return + t;
    }
}";
            var tree = Parse(text, options: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp5));
            var comp = CreateCompilationWithMscorlib45(new SyntaxTree[] { tree }, new MetadataReference[] { SystemRef });
            comp.VerifyDiagnostics();
            var syntaxNode = (PrefixUnaryExpressionSyntax)tree.FindNodeOrTokenByKind(SyntaxKind.UnaryPlusExpression).AsNode();
            var treeModel = comp.GetSemanticModel(tree);
            Assert.Throws<System.ArgumentException>(() => {
                var info = treeModel.GetAwaitExpressionInfo(syntaxNode);
            });
        }

        [Fact]
        [WorkItem(748533)]
        public void Bug748533()
        {
            var text =
@"
using System;
using System.Threading;
using System.Threading.Tasks;
class A
{
    public async Task<T> GetVal<T>(T t)
    {
        await Task.Delay(10);
        return t;
    }
    public async void Run<T>(T t) where T : struct
    {
        int tests = 0;
        tests++;
        dynamic f = (await GetVal((Func<Task<int>>)(async () => 1)))();
        if (await f == 1)
            Driver.Count++;
        tests++;
        dynamic ff = new Func<Task<int>>((Func<Task<int>>)(async () => 1));
        if (await ff() == 1)
            Driver.Count++;
        Driver.Result = Driver.Count - tests;
        Driver.CompletedSignal.Set();
    }
}
class Driver
{
    public static int Result = -1;
    public static int Count = 0;
    public static AutoResetEvent CompletedSignal = new AutoResetEvent(false);
    static int Main()
    {
        var t = new A();
        t.Run(6);
        CompletedSignal.WaitOne();
        return Driver.Result;
    }
}
";
            var comp = CreateCompilationWithMscorlib45(text, compOptions: TestOptions.Dll);
            comp.VerifyEmitDiagnostics(
                // (16,53): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread.
                //         dynamic f = (await GetVal((Func<Task<int>>)(async () => 1)))();
                Diagnostic(ErrorCode.WRN_AsyncLacksAwaits, "async () => 1").WithLocation(16, 53),
                // (20,60): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread.
                //         dynamic ff = new Func<Task<int>>((Func<Task<int>>)(async () => 1));
                Diagnostic(ErrorCode.WRN_AsyncLacksAwaits, "async () => 1").WithLocation(20, 60),
                // (17,13): error CS0656: Missing compiler required member 'Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create'
                //         if (await f == 1)
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "await f").WithArguments("Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo", "Create").WithLocation(17, 13)
                );
        }
    }
}