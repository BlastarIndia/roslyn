﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public partial class CompilationEventTests : CompilingTestBase
    {
        internal static void VerifyEvents(AsyncQueue<CompilationEvent> queue, params string[] expectedEvents)
        {
            var expected = new HashSet<string>();
            foreach (var s in expectedEvents)
            {
                if (!expected.Add(s))
                {
                    Console.WriteLine("Expected duplicate " + s);
                }
            }

            var actual = ArrayBuilder<CompilationEvent>.GetInstance();
            while (queue.Count != 0 || !queue.IsCompleted)
            {
                var te = queue.DequeueAsync();
                Assert.True(te.IsCompleted);
                actual.Add(te.Result);
            }
            bool unexpected = false;
            foreach (var a in actual)
            {
                var eventString = a.ToString();
                if (!expected.Remove(eventString))
                {
                    if (!unexpected)
                    {
                        Console.WriteLine("UNEXPECTED EVENTS:");
                        unexpected = true;
                    }
                    Console.WriteLine(eventString);
                }
            }
            if (expected.Count != 0)
            {
                Console.WriteLine("MISSING EVENTS:");
            }
            foreach (var e in expected)
            {
                Console.WriteLine(e);
            }
            if (unexpected || expected.Count != 0)
            {
                bool first = true;
                Console.WriteLine("ACTUAL EVENTS:");
                foreach (var e in actual)
                {
                    if (!first)
                    {
                        Console.WriteLine(",");
                    }
                    first = false;
                    Console.Write("\"" + e.ToString() + "\"");
                }
                Console.WriteLine();
                Assert.True(false);
            }
        }

        [Fact]
        public void TestQueuedSymbols()
        {
            var source =
@"namespace N
{
  partial class C<T1>
  {
    partial void M(int x1);
    internal int P { get; private set; }
    int F = 12;
    void N<T2>(int y = 12) { F = F + 1; }
  }
  partial class C<T1>
  {
    partial void M(int x2) {}
  }
}";
            var q = new AsyncQueue<CompilationEvent>();
            CreateCompilationWithMscorlib45(source)
                .WithEventQueue(q)
                .VerifyDiagnostics()  // force diagnostics twice
                .VerifyDiagnostics();
            VerifyEvents(q,
                "CompilationStarted",
                "SymbolDeclared(P N.C<T1>.P @ : (5,4)-(5,40))",
                "SymbolDeclared(F N.C<T1>.F @ : (6,8)-(6,14))",
                "SymbolDeclared(M N.C<T1>.M(int) @ : (4,4)-(4,27))",
                "SymbolDeclared(C N.C<T1> @ : (2,2)-(8,3))",
                "SymbolDeclared(M N.C<T1>.M(int) @ : (11,4)-(11,29))",
                "SymbolDeclared(get_P N.C<T1>.P.get @ : (5,21)-(5,25))",
                "SymbolDeclared(set_P N.C<T1>.P.set @ : (5,26)-(5,38))",
                "SymbolDeclared(N N.C<T1>.N<T2>(int) @ : (7,4)-(7,41))",
                "CompilationUnitCompleted()",
                "CompilationCompleted"
                );
        }
    }
}
