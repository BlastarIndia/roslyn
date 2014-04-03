﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    public class CodeGenConstructorInitTests : CSharpTestBase
    {
        [Fact]
        public void TestImplicitConstructor()
        {
            var source = @"
class C
{
    static void Main()
    {
        C c = new C();
    }
}
";
            CompileAndVerify(source, expectedOutput: string.Empty).
                VerifyIL("C..ctor", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0   
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ret       
}
");
        }

        [Fact]
        public void TestImplicitConstructorInitializer()
        {
            var source = @"
class C
{
    C()
    {
    }

    static void Main()
    {
        C c = new C();
    }
}
";
            CompileAndVerify(source, expectedOutput: string.Empty).
                VerifyIL("C..ctor", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0   
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ret       
}
");
        }

        [Fact]
        public void TestExplicitBaseConstructorInitializer()
        {
            var source = @"
class C
{
    C() : base()
    {
    }

    static void Main()
    {
        C c = new C();
    }
}
";
            CompileAndVerify(source, expectedOutput: string.Empty).
                VerifyIL("C..ctor", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0   
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ret       
}
");
        }

        [Fact]
        public void TestExplicitThisConstructorInitializer()
        {
            var source = @"
class C
{
    C() : this(1)
    {
    }    

    C(int x)
    {
    }

    static void Main()
    {
        C c = new C();
    }
}
";
            CompileAndVerify(source, expectedOutput: string.Empty).
                VerifyIL("C..ctor", @"
{
  // Code size        8 (0x8)
  .maxstack  2
  IL_0000:  ldarg.0   
  IL_0001:  ldc.i4.1  
  IL_0002:  call       ""C..ctor(int)""
  IL_0007:  ret       
}
");
        }

        [Fact]
        public void TestExplicitOverloadedBaseConstructorInitializer()
        {
            var source = @"
class B
{
    public B(int x)
    {
    }

    public B(string x)
    {
    }
}

class C : B
{
    C() : base(1)
    {
    }

    static void Main()
    {
        C c = new C();
    }
}
";
            CompileAndVerify(source, expectedOutput: string.Empty).
                VerifyIL("C..ctor", @"
{
  // Code size        8 (0x8)
  .maxstack  2
  IL_0000:  ldarg.0   
  IL_0001:  ldc.i4.1  
  IL_0002:  call       ""B..ctor(int)""
  IL_0007:  ret       
}
");
        }

        [Fact]
        public void TestExplicitOverloadedThisConstructorInitializer()
        {
            var source = @"
class C
{
    C() : this(1)
    {
    }    

    C(int x)
    {
    }    

    C(string x)
    {
    }

    static void Main()
    {
        C c = new C();
    }
}
";
            CompileAndVerify(source, expectedOutput: string.Empty).
                VerifyIL("C..ctor", @"
{
  // Code size        8 (0x8)
  .maxstack  2
  IL_0000:  ldarg.0   
  IL_0001:  ldc.i4.1  
  IL_0002:  call       ""C..ctor(int)""
  IL_0007:  ret       
}
");
        }

        [Fact]
        public void TestComplexInitialization()
        {
            var source = @"
class B
{
    private int f = E.Init(3, ""B.f"");

    public B()
    {
        System.Console.WriteLine(""B()"");
    }    

    public B(int x) : this (x.ToString())
    {
        System.Console.WriteLine(""B(int)"");
    }    

    public B(string x) : this()
    {
        System.Console.WriteLine(""B(string)"");
    }
}

class C : B
{
    private int f = E.Init(4, ""C.f"");

    public C() : this(1)
    {
        System.Console.WriteLine(""C()"");
    }    

    public C(int x) : this(x.ToString())
    {
        System.Console.WriteLine(""C(int)"");
    }    

    public C(string x) : base(x.Length)
    {
        System.Console.WriteLine(""C(string)"");
    }
}

class E
{
    static void Main()
    {
        C c = new C();
    }

    public static int Init(int value, string message)
    {
        System.Console.WriteLine(message);
        return value;
    }
}
";
            //interested in execution order and number of field initializations
            CompileAndVerify(source, expectedOutput: @"
C.f
B.f
B()
B(string)
B(int)
C(string)
C(int)
C()
");
        }

        // Successive Operator On Class
        [WorkItem(540992)]
        [Fact]
        public void TestSuccessiveOperatorOnClass()
        {
            var text = @"
using System;
class C
{
    public int num;
    public C(int i)
    {
        this.num = i;
    }
    static void Main(string[] args)
    {
        C c1 = new C(1);
        C c2 = new C(2);
        C c3 = new C(3);
        bool verify = c1.num == 1 && c2.num == 2 & c3.num == 3;
        Console.WriteLine(verify);
    }
}
";
            var expectedOutput = @"True";
            CompileAndVerify(text, expectedOutput: expectedOutput);
        }
    }
}
