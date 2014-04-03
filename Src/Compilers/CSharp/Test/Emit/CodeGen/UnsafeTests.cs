﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.CSharp.UnitTests.Emit;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    public class UnsafeTests : EmitMetadataTestBase
    {
        #region AddressOf tests

        [Fact]
        public void AddressOfLocal_Unused()
        {
            var text = @"
unsafe class C
{
    void M()
    {
        int x;
        int* p = &x;
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeDll);
            compVerifier.VerifyIL("C.M", @"
{
  // Code size        4 (0x4)
  .maxstack  1
  .locals init (int V_0) //x
  IL_0000:  ldloca.s   V_0
  IL_0002:  pop
  IL_0003:  ret
}
");
        }

        [Fact]
        public void AddressOfLocal_Used()
        {
            var text = @"
unsafe class C
{
    void M(int* param)
    {
        int x;
        int* p = &x;
        M(p);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeDll);
            compVerifier.VerifyIL("C.M", @"
{
  // Code size       12 (0xc)
  .maxstack  2
  .locals init (int V_0, //x
  int* V_1) //p
  IL_0000:  ldloca.s   V_0
  IL_0002:  conv.u
  IL_0003:  stloc.1
  IL_0004:  ldarg.0
  IL_0005:  ldloc.1
  IL_0006:  call       ""void C.M(int*)""
  IL_000b:  ret
}
");
        }

        [Fact]
        public void AddressOfParameter_Unused()
        {
            var text = @"
unsafe class C
{
    void M(int x)
    {
        int* p = &x;
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeDll);
            compVerifier.VerifyIL("C.M", @"
{
  // Code size        4 (0x4)
  .maxstack  1
  IL_0000:  ldarga.s   V_1
  IL_0002:  pop
  IL_0003:  ret
}
");
        }

        [Fact]
        public void AddressOfParameter_Used()
        {
            var text = @"
unsafe class C
{
    void M(int x, int* param)
    {
        int* p = &x;
        M(x, p);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeDll);
            compVerifier.VerifyIL("C.M", @"
{
  // Code size       13 (0xd)
  .maxstack  3
  .locals init (int* V_0) //p
  IL_0000:  ldarga.s   V_1
  IL_0002:  conv.u
  IL_0003:  stloc.0
  IL_0004:  ldarg.0
  IL_0005:  ldarg.1
  IL_0006:  ldloc.0
  IL_0007:  call       ""void C.M(int, int*)""
  IL_000c:  ret
}
");
        }

        [Fact]
        public void AddressOfStructField()
        {
            var text = @"
unsafe class C
{
    void M()
    {
        S1 s;
        S1* p1 = &s;
        S2* p2 = &s.s;
        int* p3 = &s.s.x;

        Foo(s, p1, p2, p3);
    }

    void Foo(S1 s, S1* p1, S2* p2, int* p3) { }
}

struct S1
{
    public S2 s;
}

struct S2
{
    public int x;
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeDll);
            compVerifier.VerifyIL("C.M", @"
{
  // Code size       38 (0x26)
  .maxstack  5
  .locals init (S1 V_0, //s
  S1* V_1, //p1
  S2* V_2, //p2
  int* V_3) //p3
  IL_0000:  ldloca.s   V_0
  IL_0002:  conv.u
  IL_0003:  stloc.1
  IL_0004:  ldloca.s   V_0
  IL_0006:  ldflda     ""S2 S1.s""
  IL_000b:  conv.u
  IL_000c:  stloc.2
  IL_000d:  ldloca.s   V_0
  IL_000f:  ldflda     ""S2 S1.s""
  IL_0014:  ldflda     ""int S2.x""
  IL_0019:  conv.u
  IL_001a:  stloc.3
  IL_001b:  ldarg.0
  IL_001c:  ldloc.0
  IL_001d:  ldloc.1
  IL_001e:  ldloc.2
  IL_001f:  ldloc.3
  IL_0020:  call       ""void C.Foo(S1, S1*, S2*, int*)""
  IL_0025:  ret
}
");
        }

        [Fact]
        public void AddressOfSuppressOptimization()
        {
            var text = @"
unsafe class C
{
    static void M()
    {
        int x = 123;
        Foo(&x); // should not optimize into 'Foo(&123)'
    }

    static void Foo(int* p) { }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeDll);
            compVerifier.VerifyIL("C.M", @"
{
  // Code size       12 (0xc)
  .maxstack  1
  .locals init (int V_0) //x
  IL_0000:  ldc.i4.s   123
  IL_0002:  stloc.0
  IL_0003:  ldloca.s   V_0
  IL_0005:  conv.u
  IL_0006:  call       ""void C.Foo(int*)""
  IL_000b:  ret
}
");
        }

        #endregion AddressOf tests

        #region Dereference tests

        [Fact]
        public void DereferenceLocal()
        {
            var text = @"
unsafe class C
{
    static void Main()
    {
        int x = 123;
        int* p = &x;
        System.Console.WriteLine(*p);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "123");

            // NOTE: p is optimized away, but & and * aren't.
            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       13 (0xd)
  .maxstack  1
  .locals init (int V_0) //x
  IL_0000:  ldc.i4.s   123
  IL_0002:  stloc.0
  IL_0003:  ldloca.s   V_0
  IL_0005:  conv.u
  IL_0006:  ldind.i4
  IL_0007:  call       ""void System.Console.WriteLine(int)""
  IL_000c:  ret
}
");
        }

        [Fact]
        public void DereferenceParameter()
        {
            var text = @"
unsafe class C
{
    static void Main()
    {
        long x = 456;
        System.Console.WriteLine(Dereference(&x));
    }

    static long Dereference(long* p)
    {
        return *p;
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "456");

            compVerifier.VerifyIL("C.Dereference", @"
{
  // Code size        3 (0x3)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  ldind.i8
  IL_0002:  ret
}
");
        }

        [Fact]
        public void DereferenceWrite()
        {
            var text = @"
unsafe class C
{
    static void Main()
    {
        int x = 1;
        int* p = &x;
        *p = 2;
        System.Console.WriteLine(x);
    }
}
";
            var compVerifierOptimized = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "2");

            // NOTE: p is optimized away, but & and * aren't.
            compVerifierOptimized.VerifyIL("C.Main", @"
{
  // Code size       14 (0xe)
  .maxstack  2
  .locals init (int V_0) //x
  IL_0000:  ldc.i4.1
  IL_0001:  stloc.0
  IL_0002:  ldloca.s   V_0
  IL_0004:  conv.u
  IL_0005:  ldc.i4.2
  IL_0006:  stind.i4
  IL_0007:  ldloc.0
  IL_0008:  call       ""void System.Console.WriteLine(int)""
  IL_000d:  ret
}
");
            var compVerifierUnoptimized = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "2");

            compVerifierUnoptimized.VerifyIL("C.Main", @"
{
  // Code size       18 (0x12)
  .maxstack  2
  .locals init (int V_0, //x
  int* V_1) //p
  IL_0000:  nop
  IL_0001:  ldc.i4.1
  IL_0002:  stloc.0
  IL_0003:  ldloca.s   V_0
  IL_0005:  conv.u
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  ldc.i4.2
  IL_0009:  stind.i4
  IL_000a:  ldloc.0
  IL_000b:  call       ""void System.Console.WriteLine(int)""
  IL_0010:  nop
  IL_0011:  ret
}
");
        }

        [Fact]
        public void DereferenceStruct()
        {
            var text = @"
unsafe struct S
{
    S* p;
    byte x;

    static void Main()
    {
        S s;
        S* sp = &s;
        (*sp).p = sp;
        (*sp).x = 1;
        System.Console.WriteLine((*(s.p)).x);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "1");

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       35 (0x23)
  .maxstack  2
  .locals init (S V_0, //s
  S* V_1) //sp
  IL_0000:  ldloca.s   V_0
  IL_0002:  conv.u
  IL_0003:  stloc.1
  IL_0004:  ldloc.1
  IL_0005:  ldloc.1
  IL_0006:  stfld      ""S* S.p""
  IL_000b:  ldloc.1
  IL_000c:  ldc.i4.1
  IL_000d:  stfld      ""byte S.x""
  IL_0012:  ldloc.0
  IL_0013:  ldfld      ""S* S.p""
  IL_0018:  ldfld      ""byte S.x""
  IL_001d:  call       ""void System.Console.WriteLine(int)""
  IL_0022:  ret
}
");
        }

        [Fact]
        public void DereferenceSwap()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        byte b1 = 2;
        byte b2 = 7;

        Console.WriteLine(""Before: {0} {1}"", b1, b2);
        Swap(&b1, &b2);
        Console.WriteLine(""After: {0} {1}"", b1, b2);
    }

    static void Swap(byte* p1, byte* p2)
    {
        byte tmp = *p1;
        *p1 = *p2;
        *p2 = tmp;
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"Before: 2 7
After: 7 2");

            compVerifier.VerifyIL("C.Swap", @"
{
  // Code size       11 (0xb)
  .maxstack  2
  .locals init (byte V_0) //tmp
  IL_0000:  ldarg.0
  IL_0001:  ldind.u1
  IL_0002:  stloc.0
  IL_0003:  ldarg.0
  IL_0004:  ldarg.1
  IL_0005:  ldind.u1
  IL_0006:  stind.i1
  IL_0007:  ldarg.1
  IL_0008:  ldloc.0
  IL_0009:  stind.i1
  IL_000a:  ret
}
");
        }

        [Fact]
        public void DereferenceIsLValue1()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        char c = 'a';
        char* p = &c;

        Console.Write(c);
        Incr(ref *p);
        Console.Write(c);
    }

    static void Incr(ref char c)
    {
        c++;
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"ab");

            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       30 (0x1e)
  .maxstack  1
  .locals init (char V_0, //c
  char* V_1) //p
  IL_0000:  nop
  IL_0001:  ldc.i4.s   97
  IL_0003:  stloc.0
  IL_0004:  ldloca.s   V_0
  IL_0006:  conv.u
  IL_0007:  stloc.1
  IL_0008:  ldloc.0
  IL_0009:  call       ""void System.Console.Write(char)""
  IL_000e:  nop
  IL_000f:  ldloc.1
  IL_0010:  call       ""void C.Incr(ref char)""
  IL_0015:  nop
  IL_0016:  ldloc.0
  IL_0017:  call       ""void System.Console.Write(char)""
  IL_001c:  nop
  IL_001d:  ret
}
");
        }

        [Fact]
        public void DereferenceIsLValue2()
        {
            var text = @"
using System;

unsafe struct S
{

    int x;

    static void Main()
    {
        S s;
        s.x = 1;
        S* p = &s;
        Console.Write(s.x);
        (*p).Mutate();
        Console.Write(s.x);
    }

    void Mutate()
    {
        x++;
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"12");

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       45 (0x2d)
  .maxstack  2
  .locals init (S V_0, //s
  S* V_1) //p
  IL_0000:  nop
  IL_0001:  ldloca.s   V_0
  IL_0003:  ldc.i4.1
  IL_0004:  stfld      ""int S.x""
  IL_0009:  ldloca.s   V_0
  IL_000b:  conv.u
  IL_000c:  stloc.1
  IL_000d:  ldloc.0
  IL_000e:  ldfld      ""int S.x""
  IL_0013:  call       ""void System.Console.Write(int)""
  IL_0018:  nop
  IL_0019:  ldloc.1
  IL_001a:  call       ""void S.Mutate()""
  IL_001f:  nop
  IL_0020:  ldloc.0
  IL_0021:  ldfld      ""int S.x""
  IL_0026:  call       ""void System.Console.Write(int)""
  IL_002b:  nop
  IL_002c:  ret
}
");
        }

        #endregion Dereference tests

        #region Pointer member access tests

        [Fact]
        public void PointerMemberAccessRead()
        {
            var text = @"
using System;

unsafe struct S
{
    int x;
    
    static void Main()
    {
        S s;
        s.x = 3;
        S* p = &s;
        Console.Write(p->x);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"3");

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       26 (0x1a)
  .maxstack  2
  .locals init (S V_0, //s
  S* V_1) //p
  IL_0000:  nop
  IL_0001:  ldloca.s   V_0
  IL_0003:  ldc.i4.3
  IL_0004:  stfld      ""int S.x""
  IL_0009:  ldloca.s   V_0
  IL_000b:  conv.u
  IL_000c:  stloc.1
  IL_000d:  ldloc.1
  IL_000e:  ldfld      ""int S.x""
  IL_0013:  call       ""void System.Console.Write(int)""
  IL_0018:  nop
  IL_0019:  ret
}
");

            compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: false, expectedOutput: @"3");

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       22 (0x16)
  .maxstack  2
  .locals init (S V_0) //s
  IL_0000:  ldloca.s   V_0
  IL_0002:  ldc.i4.3
  IL_0003:  stfld      ""int S.x""
  IL_0008:  ldloca.s   V_0
  IL_000a:  conv.u
  IL_000b:  ldfld      ""int S.x""
  IL_0010:  call       ""void System.Console.Write(int)""
  IL_0015:  ret
}
");
        }

        [Fact]
        public void PointerMemberAccessWrite()
        {
            var text = @"
using System;

unsafe struct S
{
    int x;
    
    static void Main()
    {
        S s;
        s.x = 3;
        S* p = &s;
        Console.Write(s.x);
        p->x = 4;
        Console.Write(s.x);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"34");

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       45 (0x2d)
  .maxstack  2
  .locals init (S V_0, //s
  S* V_1) //p
  IL_0000:  nop
  IL_0001:  ldloca.s   V_0
  IL_0003:  ldc.i4.3
  IL_0004:  stfld      ""int S.x""
  IL_0009:  ldloca.s   V_0
  IL_000b:  conv.u
  IL_000c:  stloc.1
  IL_000d:  ldloc.0
  IL_000e:  ldfld      ""int S.x""
  IL_0013:  call       ""void System.Console.Write(int)""
  IL_0018:  nop
  IL_0019:  ldloc.1
  IL_001a:  ldc.i4.4
  IL_001b:  stfld      ""int S.x""
  IL_0020:  ldloc.0
  IL_0021:  ldfld      ""int S.x""
  IL_0026:  call       ""void System.Console.Write(int)""
  IL_002b:  nop
  IL_002c:  ret
}
");

            compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: false, expectedOutput: @"34");

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       40 (0x28)
  .maxstack  2
  .locals init (S V_0) //s
  IL_0000:  ldloca.s   V_0
  IL_0002:  ldc.i4.3
  IL_0003:  stfld      ""int S.x""
  IL_0008:  ldloca.s   V_0
  IL_000a:  conv.u
  IL_000b:  ldloc.0
  IL_000c:  ldfld      ""int S.x""
  IL_0011:  call       ""void System.Console.Write(int)""
  IL_0016:  ldc.i4.4
  IL_0017:  stfld      ""int S.x""
  IL_001c:  ldloc.0
  IL_001d:  ldfld      ""int S.x""
  IL_0022:  call       ""void System.Console.Write(int)""
  IL_0027:  ret
}
");
        }

        [Fact]
        public void PointerMemberAccessInvoke()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        S s;
        S* p = &s;
        p->M();
        p->M(1);
        p->M(1, 2);
    }

    void M() { Console.Write(1); }
    void M(int x) { Console.Write(2); }
}

static class Extensions
{
    public static void M(this S s, int x, int y) { Console.Write(3); }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, additionalRefs: new[] { LinqAssemblyRef }, emitPdb: true, expectedOutput: @"123");

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       35 (0x23)
  .maxstack  3
  .locals init (S V_0, //s
      S* V_1) //p
  IL_0000:  nop
  IL_0001:  ldloca.s   V_0
  IL_0003:  conv.u
  IL_0004:  stloc.1
  IL_0005:  ldloc.1
  IL_0006:  call       ""void S.M()""
  IL_000b:  nop
  IL_000c:  ldloc.1
  IL_000d:  ldc.i4.1
  IL_000e:  call       ""void S.M(int)""
  IL_0013:  nop
  IL_0014:  ldloc.1
  IL_0015:  ldobj      ""S""
  IL_001a:  ldc.i4.1
  IL_001b:  ldc.i4.2
  IL_001c:  call       ""void Extensions.M(S, int, int)""
  IL_0021:  nop
  IL_0022:  ret
}
");

            compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, additionalRefs: new[] { LinqAssemblyRef }, emitPdb: false, expectedOutput: @"123");

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       29 (0x1d)
  .maxstack  3
  .locals init (S V_0) //s
  IL_0000:  ldloca.s   V_0
  IL_0002:  conv.u
  IL_0003:  dup
  IL_0004:  call       ""void S.M()""
  IL_0009:  dup
  IL_000a:  ldc.i4.1
  IL_000b:  call       ""void S.M(int)""
  IL_0010:  ldobj      ""S""
  IL_0015:  ldc.i4.1
  IL_0016:  ldc.i4.2
  IL_0017:  call       ""void Extensions.M(S, int, int)""
  IL_001c:  ret
}
");
        }

        [Fact]
        public void PointerMemberAccessInvoke001()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        S s;
        S* p = &s;
        Test(ref p);
    }

    static void Test(ref S* p)
    {
        p->M();
        p->M(1);
        p->M(1, 2);
    }

    void M() { Console.Write(1); }
    void M(int x) { Console.Write(2); }
}

static class Extensions
{
    public static void M(this S s, int x, int y) { Console.Write(3); }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, additionalRefs: new[] { LinqAssemblyRef }, emitPdb: true, expectedOutput: @"123");

            compVerifier.VerifyIL("S.Test(ref S*)", @"
{
  // Code size       34 (0x22)
  .maxstack  3
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldind.i
  IL_0003:  call       ""void S.M()""
  IL_0008:  nop
  IL_0009:  ldarg.0
  IL_000a:  ldind.i
  IL_000b:  ldc.i4.1
  IL_000c:  call       ""void S.M(int)""
  IL_0011:  nop
  IL_0012:  ldarg.0
  IL_0013:  ldind.i
  IL_0014:  ldobj      ""S""
  IL_0019:  ldc.i4.1
  IL_001a:  ldc.i4.2
  IL_001b:  call       ""void Extensions.M(S, int, int)""
  IL_0020:  nop
  IL_0021:  ret
}
");

            compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, additionalRefs: new[] { LinqAssemblyRef }, emitPdb: false, expectedOutput: @"123");

            compVerifier.VerifyIL("S.Test(ref S*)", @"
{
  // Code size       30 (0x1e)
  .maxstack  3
  IL_0000:  ldarg.0
  IL_0001:  ldind.i
  IL_0002:  call       ""void S.M()""
  IL_0007:  ldarg.0
  IL_0008:  ldind.i
  IL_0009:  ldc.i4.1
  IL_000a:  call       ""void S.M(int)""
  IL_000f:  ldarg.0
  IL_0010:  ldind.i
  IL_0011:  ldobj      ""S""
  IL_0016:  ldc.i4.1
  IL_0017:  ldc.i4.2
  IL_0018:  call       ""void Extensions.M(S, int, int)""
  IL_001d:  ret
}
");
        }

        [Fact]
        public void PointerMemberAccessMutate()
        {
            var text = @"
using System;

unsafe struct S
{
    int x;
    
    static void Main()
    {
        S s;
        s.x = 3;
        S* p = &s;
        Console.Write((p->x)++);
        Console.Write((p->x)++);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true , expectedOutput: @"34");

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       58 (0x3a)
  .maxstack  3
  .locals init (S V_0, //s
  S* V_1, //p
  int& V_2,
  int V_3)
  IL_0000:  nop
  IL_0001:  ldloca.s   V_0
  IL_0003:  ldc.i4.3
  IL_0004:  stfld      ""int S.x""
  IL_0009:  ldloca.s   V_0
  IL_000b:  conv.u
  IL_000c:  stloc.1
  IL_000d:  ldloc.1
  IL_000e:  ldflda     ""int S.x""
  IL_0013:  stloc.2
  IL_0014:  ldloc.2
  IL_0015:  ldloc.2
  IL_0016:  ldind.i4
  IL_0017:  stloc.3
  IL_0018:  ldloc.3
  IL_0019:  ldc.i4.1
  IL_001a:  add
  IL_001b:  stind.i4
  IL_001c:  ldloc.3
  IL_001d:  call       ""void System.Console.Write(int)""
  IL_0022:  nop
  IL_0023:  ldloc.1
  IL_0024:  ldflda     ""int S.x""
  IL_0029:  stloc.2
  IL_002a:  ldloc.2
  IL_002b:  ldloc.2
  IL_002c:  ldind.i4
  IL_002d:  stloc.3
  IL_002e:  ldloc.3
  IL_002f:  ldc.i4.1
  IL_0030:  add
  IL_0031:  stind.i4
  IL_0032:  ldloc.3
  IL_0033:  call       ""void System.Console.Write(int)""
  IL_0038:  nop
  IL_0039:  ret
}
");

            compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: false, expectedOutput: @"34");

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       49 (0x31)
  .maxstack  4
  .locals init (S V_0, //s
  int V_1)
  IL_0000:  ldloca.s   V_0
  IL_0002:  ldc.i4.3
  IL_0003:  stfld      ""int S.x""
  IL_0008:  ldloca.s   V_0
  IL_000a:  conv.u
  IL_000b:  dup
  IL_000c:  ldflda     ""int S.x""
  IL_0011:  dup
  IL_0012:  ldind.i4
  IL_0013:  stloc.1
  IL_0014:  ldloc.1
  IL_0015:  ldc.i4.1
  IL_0016:  add
  IL_0017:  stind.i4
  IL_0018:  ldloc.1
  IL_0019:  call       ""void System.Console.Write(int)""
  IL_001e:  ldflda     ""int S.x""
  IL_0023:  dup
  IL_0024:  ldind.i4
  IL_0025:  stloc.1
  IL_0026:  ldloc.1
  IL_0027:  ldc.i4.1
  IL_0028:  add
  IL_0029:  stind.i4
  IL_002a:  ldloc.1
  IL_002b:  call       ""void System.Console.Write(int)""
  IL_0030:  ret
}
");
        }

        #endregion Pointer member access tests

        #region Pointer element access tests

        [Fact]
        public void PointerElementAccessCheckedAndUnchecked()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        S s = new S();
        S* p = &s;
        int i = (int)p;
        uint ui = (uint)p;
        long l = (long)p;
        ulong ul = (ulong)p;
        checked
        {
            s = p[i];
            s = p[ui];
            s = p[l];
            s = p[ul];
        }
        unchecked
        {
            s = p[i];
            s = p[ui];
            s = p[l];
            s = p[ul];
        }
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true);

            // The conversions differ from dev10 in the same way as for numeric addition.
            // Note that, unlike for numeric addition, the add operation is never checked.
            compVerifier.VerifyIL("S.Main", @"
{
  // Code size      180 (0xb4)
  .maxstack  3
  .locals init (S V_0, //s
  S* V_1, //p
  int V_2, //i
  uint V_3, //ui
  long V_4, //l
  ulong V_5) //ul
  IL_0000:  nop
  IL_0001:  ldloca.s   V_0
  IL_0003:  initobj    ""S""
  IL_0009:  ldloca.s   V_0
  IL_000b:  conv.u
  IL_000c:  stloc.1
  IL_000d:  ldloc.1
  IL_000e:  conv.i4
  IL_000f:  stloc.2
  IL_0010:  ldloc.1
  IL_0011:  conv.u4
  IL_0012:  stloc.3
  IL_0013:  ldloc.1
  IL_0014:  conv.u8
  IL_0015:  stloc.s    V_4
  IL_0017:  ldloc.1
  IL_0018:  conv.u8
  IL_0019:  stloc.s    V_5
  IL_001b:  nop
  IL_001c:  ldloc.1
  IL_001d:  ldloc.2
  IL_001e:  conv.i
  IL_001f:  sizeof     ""S""
  IL_0025:  mul.ovf
  IL_0026:  add
  IL_0027:  ldobj      ""S""
  IL_002c:  stloc.0
  IL_002d:  ldloc.1
  IL_002e:  ldloc.3
  IL_002f:  conv.u8
  IL_0030:  sizeof     ""S""
  IL_0036:  conv.i8
  IL_0037:  mul.ovf
  IL_0038:  conv.i
  IL_0039:  add
  IL_003a:  ldobj      ""S""
  IL_003f:  stloc.0
  IL_0040:  ldloc.1
  IL_0041:  ldloc.s    V_4
  IL_0043:  sizeof     ""S""
  IL_0049:  conv.i8
  IL_004a:  mul.ovf
  IL_004b:  conv.i
  IL_004c:  add
  IL_004d:  ldobj      ""S""
  IL_0052:  stloc.0
  IL_0053:  ldloc.1
  IL_0054:  ldloc.s    V_5
  IL_0056:  sizeof     ""S""
  IL_005c:  conv.ovf.u8
  IL_005d:  mul.ovf.un
  IL_005e:  conv.u
  IL_005f:  add
  IL_0060:  ldobj      ""S""
  IL_0065:  stloc.0
  IL_0066:  nop
  IL_0067:  nop
  IL_0068:  ldloc.1
  IL_0069:  ldloc.2
  IL_006a:  conv.i
  IL_006b:  sizeof     ""S""
  IL_0071:  mul
  IL_0072:  add
  IL_0073:  ldobj      ""S""
  IL_0078:  stloc.0
  IL_0079:  ldloc.1
  IL_007a:  ldloc.3
  IL_007b:  conv.u8
  IL_007c:  sizeof     ""S""
  IL_0082:  conv.i8
  IL_0083:  mul
  IL_0084:  conv.i
  IL_0085:  add
  IL_0086:  ldobj      ""S""
  IL_008b:  stloc.0
  IL_008c:  ldloc.1
  IL_008d:  ldloc.s    V_4
  IL_008f:  sizeof     ""S""
  IL_0095:  conv.i8
  IL_0096:  mul
  IL_0097:  conv.i
  IL_0098:  add
  IL_0099:  ldobj      ""S""
  IL_009e:  stloc.0
  IL_009f:  ldloc.1
  IL_00a0:  ldloc.s    V_5
  IL_00a2:  sizeof     ""S""
  IL_00a8:  conv.i8
  IL_00a9:  mul
  IL_00aa:  conv.u
  IL_00ab:  add
  IL_00ac:  ldobj      ""S""
  IL_00b1:  stloc.0
  IL_00b2:  nop
  IL_00b3:  ret
}
");
        }

        [Fact]
        public void PointerElementAccessWrite()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        int* p = null;
        p[1] = 2;
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true);

            compVerifier.VerifyIL("S.Main", @"
{
  // Code size       10 (0xa)
  .maxstack  2
  .locals init (int* V_0) //p
  IL_0000:  nop
  IL_0001:  ldc.i4.0
  IL_0002:  conv.u
  IL_0003:  stloc.0
  IL_0004:  ldloc.0
  IL_0005:  ldc.i4.4
  IL_0006:  add
  IL_0007:  ldc.i4.2
  IL_0008:  stind.i4
  IL_0009:  ret
}
");
        }

        [Fact]
        public void PointerElementAccessMutate()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        int[] array = new int[3];
        fixed (int* p = array)
        {
            p[1] += ++p[0];
            p[2] -= p[1]--;
        }

        foreach (int element in array)
        {
            Console.WriteLine(element);
        }
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"
1
0
-1");
        }

        [Fact]
        public void PointerElementAccessNested()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        fixed (int* q = new int[3])
        {
            q[0] = 2;
            q[1] = 0;
            q[2] = 1;

            Console.Write(q[q[q[q[q[q[*q]]]]]]);
            Console.Write(q[q[q[q[q[q[q[*q]]]]]]]);
            Console.Write(q[q[q[q[q[q[q[q[*q]]]]]]]]);
        }
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "210");
        }

        [Fact]
        public void PointerElementAccessZero()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        int x = 1;
        int* p = &x;
        Console.WriteLine(p[0]);
    }
}
";
            // NOTE: no pointer arithmetic - just dereference p.
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "1").VerifyIL("C.Main", @"
{
  // Code size       16 (0x10)
  .maxstack  1
  .locals init (int V_0, //x
    int* V_1) //p
  IL_0000:  nop
  IL_0001:  ldc.i4.1
  IL_0002:  stloc.0
  IL_0003:  ldloca.s   V_0
  IL_0005:  conv.u
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  ldind.i4
  IL_0009:  call       ""void System.Console.WriteLine(int)""
  IL_000e:  nop
  IL_000f:  ret
}
");
        }

        #endregion Pointer element access tests

        #region Fixed statement tests

        [Fact]
        public void FixedStatementField()
        {
            var text = @"
using System;

unsafe class C
{
    int x;
    
    static void Main()
    {
        C c = new C();
        fixed (int* p = &c.x)
        {
            *p = 1;
        }
        Console.WriteLine(c.x);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"1");

            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       36 (0x24)
  .maxstack  2
  .locals init (C V_0, //c
      pinned int& V_1) //p
  IL_0000:  nop
  IL_0001:  newobj     ""C..ctor()""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  ldflda     ""int C.x""
  IL_000d:  stloc.1
  IL_000e:  nop
  IL_000f:  ldloc.1
  IL_0010:  conv.i
  IL_0011:  ldc.i4.1
  IL_0012:  stind.i4
  IL_0013:  nop
  IL_0014:  ldc.i4.0
  IL_0015:  conv.u
  IL_0016:  stloc.1
  IL_0017:  ldloc.0
  IL_0018:  ldfld      ""int C.x""
  IL_001d:  call       ""void System.Console.WriteLine(int)""
  IL_0022:  nop
  IL_0023:  ret
}
");
        }

        [Fact]
        public void FixedStatementMultipleFields()
        {
            var text = @"
using System;

unsafe class C
{
    int x;
    int y;
    
    static void Main()
    {
        C c = new C();
        fixed (int* p = &c.x, q = &c.y)
        {
            *p = 1;
            *q = 2;
        }
        Console.Write(c.x);
        Console.Write(c.y);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"12");

            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       62 (0x3e)
  .maxstack  2
  .locals init (C V_0, //c
      pinned int& V_1, //p
      pinned int& V_2) //q
  IL_0000:  nop
  IL_0001:  newobj     ""C..ctor()""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  ldflda     ""int C.x""
  IL_000d:  stloc.1
  IL_000e:  ldloc.0
  IL_000f:  ldflda     ""int C.y""
  IL_0014:  stloc.2
  IL_0015:  nop
  IL_0016:  ldloc.1
  IL_0017:  conv.i
  IL_0018:  ldc.i4.1
  IL_0019:  stind.i4
  IL_001a:  ldloc.2
  IL_001b:  conv.i
  IL_001c:  ldc.i4.2
  IL_001d:  stind.i4
  IL_001e:  nop
  IL_001f:  ldc.i4.0
  IL_0020:  conv.u
  IL_0021:  stloc.1
  IL_0022:  ldc.i4.0
  IL_0023:  conv.u
  IL_0024:  stloc.2
  IL_0025:  ldloc.0
  IL_0026:  ldfld      ""int C.x""
  IL_002b:  call       ""void System.Console.Write(int)""
  IL_0030:  nop
  IL_0031:  ldloc.0
  IL_0032:  ldfld      ""int C.y""
  IL_0037:  call       ""void System.Console.Write(int)""
  IL_003c:  nop
  IL_003d:  ret
}
");
        }

        [WorkItem(546866)]
        [Fact]
        public void FixedStatementProperty()
        {
            var text =
@"class C
{
    string P { get { return null; } }
    char[] Q { get { return null; } }
    unsafe static void M(C c)
    {
        fixed (char* o = c.P)
        {
        }
        fixed (char* o = c.Q)
        {
        }
    }
}";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeDll);
            compVerifier.VerifyIL("C.M(C)",
@"{
  // Code size       56 (0x38)
  .maxstack  2
  .locals init (char* V_0, //o
  pinned string V_1,
  pinned char& V_2, //o
  char[] V_3)
  IL_0000:  ldarg.0
  IL_0001:  callvirt   ""string C.P.get""
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  conv.i
  IL_0009:  stloc.0
  IL_000a:  ldloc.1
  IL_000b:  conv.i
  IL_000c:  brfalse.s  IL_0016
  IL_000e:  ldloc.0
  IL_000f:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0014:  add
  IL_0015:  stloc.0
  IL_0016:  ldnull
  IL_0017:  stloc.1
  IL_0018:  ldarg.0
  IL_0019:  callvirt   ""char[] C.Q.get""
  IL_001e:  dup
  IL_001f:  stloc.3
  IL_0020:  brfalse.s  IL_0027
  IL_0022:  ldloc.3
  IL_0023:  ldlen
  IL_0024:  conv.i4
  IL_0025:  brtrue.s   IL_002c
  IL_0027:  ldc.i4.0
  IL_0028:  conv.u
  IL_0029:  stloc.2
  IL_002a:  br.s       IL_0034
  IL_002c:  ldloc.3
  IL_002d:  ldc.i4.0
  IL_002e:  ldelema    ""char""
  IL_0033:  stloc.2
  IL_0034:  ldc.i4.0
  IL_0035:  conv.u
  IL_0036:  stloc.2
  IL_0037:  ret
}");
        }

        [Fact]
        public void FixedStatementMultipleOptimized()
        {
            var text = @"
using System;

unsafe class C
{
    int x;
    int y;
    
    static void Main()
    {
        C c = new C();
        fixed (int* p = &c.x, q = &c.y)
        {
            *p = 1;
            *q = 2;
        }
        Console.Write(c.x);
        Console.Write(c.y);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"12");

            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       55 (0x37)
  .maxstack  3
  .locals init (pinned int& V_0, //p
      pinned int& V_1) //q
  IL_0000:  newobj     ""C..ctor()""
  IL_0005:  dup
  IL_0006:  ldflda     ""int C.x""
  IL_000b:  stloc.0
  IL_000c:  dup
  IL_000d:  ldflda     ""int C.y""
  IL_0012:  stloc.1
  IL_0013:  ldloc.0
  IL_0014:  conv.i
  IL_0015:  ldc.i4.1
  IL_0016:  stind.i4
  IL_0017:  ldloc.1
  IL_0018:  conv.i
  IL_0019:  ldc.i4.2
  IL_001a:  stind.i4
  IL_001b:  ldc.i4.0
  IL_001c:  conv.u
  IL_001d:  stloc.0
  IL_001e:  ldc.i4.0
  IL_001f:  conv.u
  IL_0020:  stloc.1
  IL_0021:  dup
  IL_0022:  ldfld      ""int C.x""
  IL_0027:  call       ""void System.Console.Write(int)""
  IL_002c:  ldfld      ""int C.y""
  IL_0031:  call       ""void System.Console.Write(int)""
  IL_0036:  ret
}
");
        }

        [Fact]
        public void FixedStatementReferenceParameter()
        {
            var text = @"
using System;

class C
{
    static void Main()
    {
        char ch;
        M(out ch);
        Console.WriteLine(ch);
    }

    unsafe static void M(out char ch)
    {
        fixed (char* p = &ch)
        {
            *p = 'a';
        }
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"a");

            compVerifier.VerifyIL("C.M", @"
{
  // Code size       14 (0xe)
  .maxstack  2
  .locals init (pinned char& V_0) //p
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  nop
  IL_0004:  ldloc.0
  IL_0005:  conv.i
  IL_0006:  ldc.i4.s   97
  IL_0008:  stind.i2
  IL_0009:  nop
  IL_000a:  ldc.i4.0
  IL_000b:  conv.u
  IL_000c:  stloc.0
  IL_000d:  ret
}
");
        }

        [Fact]
        public void FixedStatementStringLiteral()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        fixed (char* p = ""hello"")
        {
            Console.WriteLine(*p);
        }
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"h");

            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       42 (0x2a)
  .maxstack  2
  .locals init (char* V_0, //p
      pinned string V_1,
      bool V_2)
  IL_0000:  nop
  IL_0001:  ldstr      ""hello""
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  conv.i
  IL_0009:  stloc.0
  IL_000a:  ldloc.1
  IL_000b:  conv.i
  IL_000c:  ldnull
  IL_000d:  ceq
  IL_000f:  stloc.2
  IL_0010:  ldloc.2
  IL_0011:  brtrue.s   IL_001d
  IL_0013:  ldloc.0
  IL_0014:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0019:  add
  IL_001a:  stloc.0
  IL_001b:  br.s       IL_001d
  IL_001d:  nop
  IL_001e:  ldloc.0
  IL_001f:  ldind.u2
  IL_0020:  call       ""void System.Console.WriteLine(char)""
  IL_0025:  nop
  IL_0026:  nop
  IL_0027:  ldnull
  IL_0028:  stloc.1
  IL_0029:  ret
}
");
        }

        [Fact]
        public void FixedStatementStringVariable()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        string s = ""hello"";
        fixed (char* p = s)
        {
            Console.Write(*p);
        }

        s = null;
        fixed (char* p = s)
        {
            Console.Write(p == null);
        }
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"hTrue");

            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       89 (0x59)
  .maxstack  2
  .locals init (string V_0, //s
      char* V_1, //p
      pinned string V_2,
      bool V_3,
      char* V_4) //p
  IL_0000:  nop
  IL_0001:  ldstr      ""hello""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  stloc.2
  IL_0009:  ldloc.2
  IL_000a:  conv.i
  IL_000b:  stloc.1
  IL_000c:  ldloc.2
  IL_000d:  conv.i
  IL_000e:  ldnull
  IL_000f:  ceq
  IL_0011:  stloc.3
  IL_0012:  ldloc.3
  IL_0013:  brtrue.s   IL_001f
  IL_0015:  ldloc.1
  IL_0016:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_001b:  add
  IL_001c:  stloc.1
  IL_001d:  br.s       IL_001f
  IL_001f:  nop
  IL_0020:  ldloc.1
  IL_0021:  ldind.u2
  IL_0022:  call       ""void System.Console.Write(char)""
  IL_0027:  nop
  IL_0028:  nop
  IL_0029:  ldnull
  IL_002a:  stloc.2
  IL_002b:  ldnull
  IL_002c:  stloc.0
  IL_002d:  ldloc.0
  IL_002e:  stloc.2
  IL_002f:  ldloc.2
  IL_0030:  conv.i
  IL_0031:  stloc.s    V_4
  IL_0033:  ldloc.2
  IL_0034:  conv.i
  IL_0035:  ldnull
  IL_0036:  ceq
  IL_0038:  stloc.3
  IL_0039:  ldloc.3
  IL_003a:  brtrue.s   IL_0048
  IL_003c:  ldloc.s    V_4
  IL_003e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0043:  add
  IL_0044:  stloc.s    V_4
  IL_0046:  br.s       IL_0048
  IL_0048:  nop
  IL_0049:  ldloc.s    V_4
  IL_004b:  ldc.i4.0
  IL_004c:  conv.u
  IL_004d:  ceq
  IL_004f:  call       ""void System.Console.Write(bool)""
  IL_0054:  nop
  IL_0055:  nop
  IL_0056:  ldnull
  IL_0057:  stloc.2
  IL_0058:  ret
}
");
        }

        [Fact]
        public void FixedStatementStringVariableOptimized()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        string s = ""hello"";
        fixed (char* p = s)
        {
            Console.Write(*p);
        }

        s = null;
        fixed (char* p = s)
        {
            Console.Write(p == null);
        }
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"hTrue");

            // Null checks and branches are much simpler, but string temps are NOT optimized away.
            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       60 (0x3c)
  .maxstack  2
  .locals init (char* V_0, //p
      pinned string V_1,
      char* V_2) //p
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  ldloc.0
  IL_0016:  ldind.u2
  IL_0017:  call       ""void System.Console.Write(char)""
  IL_001c:  ldnull
  IL_001d:  stloc.1
  IL_001e:  ldnull
  IL_001f:  stloc.1
  IL_0020:  ldloc.1
  IL_0021:  conv.i
  IL_0022:  stloc.2
  IL_0023:  ldloc.1
  IL_0024:  conv.i
  IL_0025:  brfalse.s  IL_002f
  IL_0027:  ldloc.2
  IL_0028:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_002d:  add
  IL_002e:  stloc.2
  IL_002f:  ldloc.2
  IL_0030:  ldc.i4.0
  IL_0031:  conv.u
  IL_0032:  ceq
  IL_0034:  call       ""void System.Console.Write(bool)""
  IL_0039:  ldnull
  IL_003a:  stloc.1
  IL_003b:  ret
}
");
        }

        [Fact]
        public void FixedStatementOneDimensionalArray()
        {
            var text = @"
using System;

unsafe class C
{
    int[] a = new int[1];

    static void Main()
    {
        C c = new C();
        Console.Write(c.a[0]);
        fixed (int* p = c.a)
        {
            *p = 1;
        }
        Console.Write(c.a[0]);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: @"01");

            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       73 (0x49)
  .maxstack  2
  .locals init (C V_0, //c
  pinned int& V_1, //p
  int[] V_2)
  IL_0000:  nop
  IL_0001:  newobj     ""C..ctor()""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  ldfld      ""int[] C.a""
  IL_000d:  ldc.i4.0
  IL_000e:  ldelem.i4
  IL_000f:  call       ""void System.Console.Write(int)""
  IL_0014:  nop
  IL_0015:  ldloc.0
  IL_0016:  ldfld      ""int[] C.a""
  IL_001b:  dup
  IL_001c:  stloc.2
  IL_001d:  brfalse.s  IL_0024
  IL_001f:  ldloc.2
  IL_0020:  ldlen
  IL_0021:  conv.i4
  IL_0022:  brtrue.s   IL_0029
  IL_0024:  ldc.i4.0
  IL_0025:  conv.u
  IL_0026:  stloc.1
  IL_0027:  br.s       IL_0031
  IL_0029:  ldloc.2
  IL_002a:  ldc.i4.0
  IL_002b:  ldelema    ""int""
  IL_0030:  stloc.1
  IL_0031:  nop
  IL_0032:  ldloc.1
  IL_0033:  conv.i
  IL_0034:  ldc.i4.1
  IL_0035:  stind.i4
  IL_0036:  nop
  IL_0037:  ldc.i4.0
  IL_0038:  conv.u
  IL_0039:  stloc.1
  IL_003a:  ldloc.0
  IL_003b:  ldfld      ""int[] C.a""
  IL_0040:  ldc.i4.0
  IL_0041:  ldelem.i4
  IL_0042:  call       ""void System.Console.Write(int)""
  IL_0047:  nop
  IL_0048:  ret
}
");
        }

        [Fact]
        public void FixedStatementOneDimensionalArrayOptimized()
        {
            var text = @"
using System;

unsafe class C
{
    int[] a = new int[1];

    static void Main()
    {
        C c = new C();
        Console.Write(c.a[0]);
        fixed (int* p = c.a)
        {
            *p = 1;
        }
        Console.Write(c.a[0]);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"01");

            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       66 (0x42)
  .maxstack  3
  .locals init (pinned int& V_0, //p
  int[] V_1)
  IL_0000:  newobj     ""C..ctor()""
  IL_0005:  dup
  IL_0006:  ldfld      ""int[] C.a""
  IL_000b:  ldc.i4.0
  IL_000c:  ldelem.i4
  IL_000d:  call       ""void System.Console.Write(int)""
  IL_0012:  dup
  IL_0013:  ldfld      ""int[] C.a""
  IL_0018:  dup
  IL_0019:  stloc.1
  IL_001a:  brfalse.s  IL_0021
  IL_001c:  ldloc.1
  IL_001d:  ldlen
  IL_001e:  conv.i4
  IL_001f:  brtrue.s   IL_0026
  IL_0021:  ldc.i4.0
  IL_0022:  conv.u
  IL_0023:  stloc.0
  IL_0024:  br.s       IL_002e
  IL_0026:  ldloc.1
  IL_0027:  ldc.i4.0
  IL_0028:  ldelema    ""int""
  IL_002d:  stloc.0
  IL_002e:  ldloc.0
  IL_002f:  conv.i
  IL_0030:  ldc.i4.1
  IL_0031:  stind.i4
  IL_0032:  ldc.i4.0
  IL_0033:  conv.u
  IL_0034:  stloc.0
  IL_0035:  ldfld      ""int[] C.a""
  IL_003a:  ldc.i4.0
  IL_003b:  ldelem.i4
  IL_003c:  call       ""void System.Console.Write(int)""
  IL_0041:  ret
}
");
        }

        [Fact]
        public void FixedStatementMultiDimensionalArrayOptimized()
        {
            var text = @"
using System;

unsafe class C
{
    int[,] a = new int[1,1];

    static void Main()
    {
        C c = new C();
        Console.Write(c.a[0, 0]);
        fixed (int* p = c.a)
        {
            *p = 1;
        }
        Console.Write(c.a[0, 0]);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"01");

            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       80 (0x50)
  .maxstack  4
  .locals init (pinned int& V_0, //p
      int[,] V_1)
  IL_0000:  newobj     ""C..ctor()""
  IL_0005:  dup
  IL_0006:  ldfld      ""int[,] C.a""
  IL_000b:  ldc.i4.0
  IL_000c:  ldc.i4.0
  IL_000d:  call       ""int[*,*].Get""
  IL_0012:  call       ""void System.Console.Write(int)""
  IL_0017:  dup
  IL_0018:  ldfld      ""int[,] C.a""
  IL_001d:  dup
  IL_001e:  stloc.1
  IL_001f:  brfalse.s  IL_0029
  IL_0021:  ldloc.1
  IL_0022:  callvirt   ""int System.Array.Length.get""
  IL_0027:  brtrue.s   IL_002e
  IL_0029:  ldc.i4.0
  IL_002a:  conv.u
  IL_002b:  stloc.0
  IL_002c:  br.s       IL_0037
  IL_002e:  ldloc.1
  IL_002f:  ldc.i4.0
  IL_0030:  ldc.i4.0
  IL_0031:  call       ""int[*,*].Address""
  IL_0036:  stloc.0
  IL_0037:  ldloc.0
  IL_0038:  conv.i
  IL_0039:  ldc.i4.1
  IL_003a:  stind.i4
  IL_003b:  ldc.i4.0
  IL_003c:  conv.u
  IL_003d:  stloc.0
  IL_003e:  ldfld      ""int[,] C.a""
  IL_0043:  ldc.i4.0
  IL_0044:  ldc.i4.0
  IL_0045:  call       ""int[*,*].Get""
  IL_004a:  call       ""void System.Console.Write(int)""
  IL_004f:  ret
}
");
        }

        [Fact]
        public void FixedStatementMixed()
        {
            var text = @"
using System;

unsafe class C
{
    char c = 'a';
    char[] a = new char[1];

    static void Main()
    {
        C c = new C();
        fixed (char* p = &c.c, q = c.a, r = ""hello"")
        {
            Console.Write((int)*p);
            Console.Write((int)*q);
            Console.Write((int)*r);
        }
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"970104");

            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       96 (0x60)
  .maxstack  2
  .locals init (pinned char& V_0, //p
  pinned char& V_1, //q
  char* V_2, //r
  char[] V_3,
  pinned string V_4)
  IL_0000:  newobj     ""C..ctor()""
  IL_0005:  dup
  IL_0006:  ldflda     ""char C.c""
  IL_000b:  stloc.0
  IL_000c:  ldfld      ""char[] C.a""
  IL_0011:  dup
  IL_0012:  stloc.3
  IL_0013:  brfalse.s  IL_001a
  IL_0015:  ldloc.3
  IL_0016:  ldlen
  IL_0017:  conv.i4
  IL_0018:  brtrue.s   IL_001f
  IL_001a:  ldc.i4.0
  IL_001b:  conv.u
  IL_001c:  stloc.1
  IL_001d:  br.s       IL_0027
  IL_001f:  ldloc.3
  IL_0020:  ldc.i4.0
  IL_0021:  ldelema    ""char""
  IL_0026:  stloc.1
  IL_0027:  ldstr      ""hello""
  IL_002c:  stloc.s    V_4
  IL_002e:  ldloc.s    V_4
  IL_0030:  conv.i
  IL_0031:  stloc.2
  IL_0032:  ldloc.s    V_4
  IL_0034:  conv.i
  IL_0035:  brfalse.s  IL_003f
  IL_0037:  ldloc.2
  IL_0038:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_003d:  add
  IL_003e:  stloc.2
  IL_003f:  ldloc.0
  IL_0040:  conv.i
  IL_0041:  ldind.u2
  IL_0042:  call       ""void System.Console.Write(int)""
  IL_0047:  ldloc.1
  IL_0048:  conv.i
  IL_0049:  ldind.u2
  IL_004a:  call       ""void System.Console.Write(int)""
  IL_004f:  ldloc.2
  IL_0050:  ldind.u2
  IL_0051:  call       ""void System.Console.Write(int)""
  IL_0056:  ldc.i4.0
  IL_0057:  conv.u
  IL_0058:  stloc.0
  IL_0059:  ldc.i4.0
  IL_005a:  conv.u
  IL_005b:  stloc.1
  IL_005c:  ldnull
  IL_005d:  stloc.s    V_4
  IL_005f:  ret
}
");
        }

        [Fact]
        public void FixedStatementInTryOfTryFinally()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        try
        {
            fixed (char* p = ""hello"")
            {
            }
        }
        finally
        {
        }
    }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       28 (0x1c)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
{
  .try
{
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  leave.s    IL_001b
}
  finally
{
  IL_0017:  ldnull
  IL_0018:  stloc.1
  IL_0019:  endfinally
}
}
  finally
{
  IL_001a:  endfinally
}
  IL_001b:  ret
}
");
        }

        [Fact]
        public void FixedStatementInTryOfTryCatch()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        try
        {
            fixed (char* p = ""hello"")
            {
            }
        }
        catch
        {
        }
    }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       30 (0x1e)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
{
  .try
{
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  leave.s    IL_001d
}
  finally
{
  IL_0017:  ldnull
  IL_0018:  stloc.1
  IL_0019:  endfinally
}
}
  catch object
{
  IL_001a:  pop
  IL_001b:  leave.s    IL_001d
}
  IL_001d:  ret
}
");
        }

        [Fact]
        public void FixedStatementInFinally()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        try
        {
        }
        finally
        {
            fixed (char* p = ""hello"")
            {
            }
        }
    }
}
";
            // Cleanup not in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       27 (0x1b)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
  {
    IL_0000:  leave.s    IL_001a
  }
  finally
  {
    IL_0002:  ldstr      ""hello""
    IL_0007:  stloc.1
    IL_0008:  ldloc.1
    IL_0009:  conv.i
    IL_000a:  stloc.0
    IL_000b:  ldloc.1
    IL_000c:  conv.i
    IL_000d:  brfalse.s  IL_0017
    IL_000f:  ldloc.0
    IL_0010:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
    IL_0015:  add
    IL_0016:  stloc.0
    IL_0017:  ldnull
    IL_0018:  stloc.1
    IL_0019:  endfinally
  }
  IL_001a:  ret
}
");
        }

        [Fact]
        public void FixedStatementInCatchOfTryCatch()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        try
        {
        }
        catch
        {
            fixed (char* p = ""hello"")
            {
            }
        }
    }
}
";
            // Cleanup not in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       29 (0x1d)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
  {
    IL_0000:  leave.s    IL_001c
  }
  catch object
  {
    IL_0002:  pop
    IL_0003:  ldstr      ""hello""
    IL_0008:  stloc.1
    IL_0009:  ldloc.1
    IL_000a:  conv.i
    IL_000b:  stloc.0
    IL_000c:  ldloc.1
    IL_000d:  conv.i
    IL_000e:  brfalse.s  IL_0018
    IL_0010:  ldloc.0
    IL_0011:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
    IL_0016:  add
    IL_0017:  stloc.0
    IL_0018:  ldnull
    IL_0019:  stloc.1
    IL_001a:  leave.s    IL_001c
  }
  IL_001c:  ret
}
");
        }

        [Fact]
        public void FixedStatementInCatchOfTryCatchFinally()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        try
        {
        }
        catch
        {
            fixed (char* p = ""hello"")
            {
            }
        }
        finally
        {
        }
    }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       31 (0x1f)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
{
  .try
{
  IL_0000:  leave.s    IL_001e
}
  catch object
{
  IL_0002:  pop
  .try
{
  IL_0003:  ldstr      ""hello""
  IL_0008:  stloc.1
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  stloc.0
  IL_000c:  ldloc.1
  IL_000d:  conv.i
  IL_000e:  brfalse.s  IL_0018
  IL_0010:  ldloc.0
  IL_0011:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0016:  add
  IL_0017:  stloc.0
  IL_0018:  leave.s    IL_001e
}
  finally
{
  IL_001a:  ldnull
  IL_001b:  stloc.1
  IL_001c:  endfinally
}
}
}
  finally
{
  IL_001d:  endfinally
}
  IL_001e:  ret
}
");
        }

        [Fact]
        public void FixedStatementInFixed_NoBranch()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* q = ""goodbye"")
        {
            fixed (char* p = ""hello"")
            {
            }
        }
    }
}
";
            // Neither inner nor outer has finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       47 (0x2f)
  .maxstack  2
  .locals init (char* V_0, //q
  pinned string V_1,
  char* V_2, //p
  pinned string V_3)
  IL_0000:  ldstr      ""goodbye""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  ldstr      ""hello""
  IL_001a:  stloc.3
  IL_001b:  ldloc.3
  IL_001c:  conv.i
  IL_001d:  stloc.2
  IL_001e:  ldloc.3
  IL_001f:  conv.i
  IL_0020:  brfalse.s  IL_002a
  IL_0022:  ldloc.2
  IL_0023:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0028:  add
  IL_0029:  stloc.2
  IL_002a:  ldnull
  IL_002b:  stloc.3
  IL_002c:  ldnull
  IL_002d:  stloc.1
  IL_002e:  ret
}
");
        }

        [Fact]
        public void FixedStatementInFixed_InnerBranch()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* q = ""goodbye"")
        {
            fixed (char* p = ""hello"")
            {
                goto label;
            }
        }
      label: ;
    }
}
";
            // Inner and outer both have finally blocks.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       52 (0x34)
  .maxstack  2
  .locals init (char* V_0, //q
  pinned string V_1,
  char* V_2, //p
  pinned string V_3)
  .try
{
  IL_0000:  ldstr      ""goodbye""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  nop
  .try
{
  IL_0016:  ldstr      ""hello""
  IL_001b:  stloc.3
  IL_001c:  ldloc.3
  IL_001d:  conv.i
  IL_001e:  stloc.2
  IL_001f:  ldloc.3
  IL_0020:  conv.i
  IL_0021:  brfalse.s  IL_002b
  IL_0023:  ldloc.2
  IL_0024:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0029:  add
  IL_002a:  stloc.2
  IL_002b:  leave.s    IL_0033
}
  finally
{
  IL_002d:  ldnull
  IL_002e:  stloc.3
  IL_002f:  endfinally
}
}
  finally
{
  IL_0030:  ldnull
  IL_0031:  stloc.1
  IL_0032:  endfinally
}
  IL_0033:  ret
}
");
        }

        [Fact]
        public void FixedStatementInFixed_OuterBranch()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* q = ""goodbye"")
        {
            fixed (char* p = ""hello"")
            {
            }
            goto label;
        }
      label: ;
    }
}
";
            // Outer has finally, inner does not.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       50 (0x32)
  .maxstack  2
  .locals init (char* V_0, //q
  pinned string V_1,
  char* V_2, //p
  pinned string V_3)
  .try
  {
    IL_0000:  ldstr      ""goodbye""
    IL_0005:  stloc.1
    IL_0006:  ldloc.1
    IL_0007:  conv.i
    IL_0008:  stloc.0
    IL_0009:  ldloc.1
    IL_000a:  conv.i
    IL_000b:  brfalse.s  IL_0015
    IL_000d:  ldloc.0
    IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
    IL_0013:  add
    IL_0014:  stloc.0
    IL_0015:  ldstr      ""hello""
    IL_001a:  stloc.3
    IL_001b:  ldloc.3
    IL_001c:  conv.i
    IL_001d:  stloc.2
    IL_001e:  ldloc.3
    IL_001f:  conv.i
    IL_0020:  brfalse.s  IL_002a
    IL_0022:  ldloc.2
    IL_0023:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
    IL_0028:  add
    IL_0029:  stloc.2
    IL_002a:  ldnull
    IL_002b:  stloc.3
    IL_002c:  leave.s    IL_0031
  }
  finally
  {
    IL_002e:  ldnull
    IL_002f:  stloc.1
    IL_0030:  endfinally
  }
  IL_0031:  ret
}
");
        }

        [Fact]
        public void FixedStatementInFixed_Nesting()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* p1 = ""A"")
        {
            fixed (char* p2 = ""B"")
            {
                fixed (char* p3 = ""C"")
                {
                }
                fixed (char* p4 = ""D"")
                {
                }
            }
            fixed (char* p5 = ""E"")
            {
                fixed (char* p6 = ""F"")
                {
                }
                fixed (char* p7 = ""G"")
                {
                }
            }
        }
    }
}
";
            // This test checks two things:
            //   1) nothing blows up with triple-nesting, and
            //   2) none of the fixed statements has a try-finally.
            // CONSIDER: Shorter test that performs the same checks.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size      193 (0xc1)
  .maxstack  2
  .locals init (char* V_0, //p1
    pinned string V_1,
    char* V_2, //p2
    pinned string V_3,
    char* V_4, //p3
    pinned string V_5,
    char* V_6, //p4
    char* V_7, //p5
    char* V_8, //p6
    char* V_9) //p7
  IL_0000:  ldstr      ""A""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  ldstr      ""B""
  IL_001a:  stloc.3
  IL_001b:  ldloc.3
  IL_001c:  conv.i
  IL_001d:  stloc.2
  IL_001e:  ldloc.3
  IL_001f:  conv.i
  IL_0020:  brfalse.s  IL_002a
  IL_0022:  ldloc.2
  IL_0023:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0028:  add
  IL_0029:  stloc.2
  IL_002a:  ldstr      ""C""
  IL_002f:  stloc.s    V_5
  IL_0031:  ldloc.s    V_5
  IL_0033:  conv.i
  IL_0034:  stloc.s    V_4
  IL_0036:  ldloc.s    V_5
  IL_0038:  conv.i
  IL_0039:  brfalse.s  IL_0045
  IL_003b:  ldloc.s    V_4
  IL_003d:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0042:  add
  IL_0043:  stloc.s    V_4
  IL_0045:  ldnull
  IL_0046:  stloc.s    V_5
  IL_0048:  ldstr      ""D""
  IL_004d:  stloc.s    V_5
  IL_004f:  ldloc.s    V_5
  IL_0051:  conv.i
  IL_0052:  stloc.s    V_6
  IL_0054:  ldloc.s    V_5
  IL_0056:  conv.i
  IL_0057:  brfalse.s  IL_0063
  IL_0059:  ldloc.s    V_6
  IL_005b:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0060:  add
  IL_0061:  stloc.s    V_6
  IL_0063:  ldnull
  IL_0064:  stloc.s    V_5
  IL_0066:  ldnull
  IL_0067:  stloc.3
  IL_0068:  ldstr      ""E""
  IL_006d:  stloc.3
  IL_006e:  ldloc.3
  IL_006f:  conv.i
  IL_0070:  stloc.s    V_7
  IL_0072:  ldloc.3
  IL_0073:  conv.i
  IL_0074:  brfalse.s  IL_0080
  IL_0076:  ldloc.s    V_7
  IL_0078:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_007d:  add
  IL_007e:  stloc.s    V_7
  IL_0080:  ldstr      ""F""
  IL_0085:  stloc.s    V_5
  IL_0087:  ldloc.s    V_5
  IL_0089:  conv.i
  IL_008a:  stloc.s    V_8
  IL_008c:  ldloc.s    V_5
  IL_008e:  conv.i
  IL_008f:  brfalse.s  IL_009b
  IL_0091:  ldloc.s    V_8
  IL_0093:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0098:  add
  IL_0099:  stloc.s    V_8
  IL_009b:  ldnull
  IL_009c:  stloc.s    V_5
  IL_009e:  ldstr      ""G""
  IL_00a3:  stloc.s    V_5
  IL_00a5:  ldloc.s    V_5
  IL_00a7:  conv.i
  IL_00a8:  stloc.s    V_9
  IL_00aa:  ldloc.s    V_5
  IL_00ac:  conv.i
  IL_00ad:  brfalse.s  IL_00b9
  IL_00af:  ldloc.s    V_9
  IL_00b1:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_00b6:  add
  IL_00b7:  stloc.s    V_9
  IL_00b9:  ldnull
  IL_00ba:  stloc.s    V_5
  IL_00bc:  ldnull
  IL_00bd:  stloc.3
  IL_00be:  ldnull
  IL_00bf:  stloc.1
  IL_00c0:  ret
}
");
        }

        [Fact]
        public void FixedStatementInUsing()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        using (System.IDisposable d = null)
        {
            fixed (char* p = ""hello"")
            {
            }
        }
    }
}
";
            // CONSIDER: This is sort of silly since the using is optimized away.
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       27 (0x1b)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
{
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  leave.s    IL_001a
}
  finally
{
  IL_0017:  ldnull
  IL_0018:  stloc.1
  IL_0019:  endfinally
}
  IL_001a:  ret
}
");
        }

        [Fact]
        public void FixedStatementInLock()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        lock (this)
        {
            fixed (char* p = ""hello"")
            {
            }
        }
    }
}
";
            // Cleanup not in finally (matches dev11, but not clear why).
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       48 (0x30)
  .maxstack  2
  .locals init (C V_0,
  bool V_1,
  char* V_2, //p
  pinned string V_3)
  IL_0000:  ldc.i4.0
  IL_0001:  stloc.1
  .try
  {
    IL_0002:  ldarg.0
    IL_0003:  stloc.0
    IL_0004:  ldloc.0
    IL_0005:  ldloca.s   V_1
    IL_0007:  call       ""void System.Threading.Monitor.Enter(object, ref bool)""
    IL_000c:  ldstr      ""hello""
    IL_0011:  stloc.3
    IL_0012:  ldloc.3
    IL_0013:  conv.i
    IL_0014:  stloc.2
    IL_0015:  ldloc.3
    IL_0016:  conv.i
    IL_0017:  brfalse.s  IL_0021
    IL_0019:  ldloc.2
    IL_001a:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
    IL_001f:  add
    IL_0020:  stloc.2
    IL_0021:  ldnull
    IL_0022:  stloc.3
    IL_0023:  leave.s    IL_002f
  }
  finally
  {
    IL_0025:  ldloc.1
    IL_0026:  brfalse.s  IL_002e
    IL_0028:  ldloc.0
    IL_0029:  call       ""void System.Threading.Monitor.Exit(object)""
    IL_002e:  endfinally
  }
  IL_002f:  ret
}
");
        }

        [Fact]
        public void FixedStatementInForEach_NoDispose()
        {
            var text = @"
unsafe class C
{
    void Test(int[] array)
    {
        foreach (int i in array)
        {
            fixed (char* p = ""hello"")
            {
            }
        }
    }
}
";
            // Cleanup in finally.
            // CONSIDER: dev11 is smarter and skips the try-finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       47 (0x2f)
  .maxstack  2
  .locals init (int[] V_0,
  int V_1,
  char* V_2, //p
  pinned string V_3)
  IL_0000:  ldarg.1
  IL_0001:  stloc.0
  IL_0002:  ldc.i4.0
  IL_0003:  stloc.1
  IL_0004:  br.s       IL_0028
  IL_0006:  ldloc.0
  IL_0007:  ldloc.1
  IL_0008:  ldelem.i4
  IL_0009:  pop
  .try
{
  IL_000a:  ldstr      ""hello""
  IL_000f:  stloc.3
  IL_0010:  ldloc.3
  IL_0011:  conv.i
  IL_0012:  stloc.2
  IL_0013:  ldloc.3
  IL_0014:  conv.i
  IL_0015:  brfalse.s  IL_001f
  IL_0017:  ldloc.2
  IL_0018:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_001d:  add
  IL_001e:  stloc.2
  IL_001f:  leave.s    IL_0024
}
  finally
{
  IL_0021:  ldnull
  IL_0022:  stloc.3
  IL_0023:  endfinally
}
  IL_0024:  ldloc.1
  IL_0025:  ldc.i4.1
  IL_0026:  add
  IL_0027:  stloc.1
  IL_0028:  ldloc.1
  IL_0029:  ldloc.0
  IL_002a:  ldlen
  IL_002b:  conv.i4
  IL_002c:  blt.s      IL_0006
  IL_002e:  ret
}
");
        }

        [Fact]
        public void FixedStatementInForEach_Dispose()
        {
            var text = @"
unsafe class C
{
    void Test(Enumerable e)
    {
        foreach (var x in e)
        {
            fixed (char* p = ""hello"")
            {
            }
        }
    }
}

class Enumerable
{
    public Enumerator GetEnumerator() { return new Enumerator(); }
}

class Enumerator : System.IDisposable
{
    int x;
    public int Current { get { return x; } }
    public bool MoveNext() { return ++x < 4; }
    void System.IDisposable.Dispose() { }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       63 (0x3f)
  .maxstack  2
  .locals init (Enumerator V_0,
  char* V_1, //p
  pinned string V_2)
  IL_0000:  ldarg.1
  IL_0001:  callvirt   ""Enumerator Enumerable.GetEnumerator()""
  IL_0006:  stloc.0
  .try
{
  IL_0007:  br.s       IL_002a
  IL_0009:  ldloc.0
  IL_000a:  callvirt   ""int Enumerator.Current.get""
  IL_000f:  pop
  .try
{
  IL_0010:  ldstr      ""hello""
  IL_0015:  stloc.2
  IL_0016:  ldloc.2
  IL_0017:  conv.i
  IL_0018:  stloc.1
  IL_0019:  ldloc.2
  IL_001a:  conv.i
  IL_001b:  brfalse.s  IL_0025
  IL_001d:  ldloc.1
  IL_001e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0023:  add
  IL_0024:  stloc.1
  IL_0025:  leave.s    IL_002a
}
  finally
{
  IL_0027:  ldnull
  IL_0028:  stloc.2
  IL_0029:  endfinally
}
  IL_002a:  ldloc.0
  IL_002b:  callvirt   ""bool Enumerator.MoveNext()""
  IL_0030:  brtrue.s   IL_0009
  IL_0032:  leave.s    IL_003e
}
  finally
{
  IL_0034:  ldloc.0
  IL_0035:  brfalse.s  IL_003d
  IL_0037:  ldloc.0
  IL_0038:  callvirt   ""void System.IDisposable.Dispose()""
  IL_003d:  endfinally
}
  IL_003e:  ret
}
");
        }

        [Fact]
        public void FixedStatementInLambda1()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        System.Action a = () =>
        {
            try
            {
                fixed (char* p = ""hello"")
                {
                }
            }
            finally
            {
            }
        };
    }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.<Test>b__0", @"
{
  // Code size       28 (0x1c)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
{
  .try
{
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  leave.s    IL_001b
}
  finally
{
  IL_0017:  ldnull
  IL_0018:  stloc.1
  IL_0019:  endfinally
}
}
  finally
{
  IL_001a:  endfinally
}
  IL_001b:  ret
}
");
        }

        [Fact]
        public void FixedStatementInLambda2()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        try
        {
            System.Action a = () =>
            {
                    fixed (char* p = ""hello"")
                    {
                    }
            };
        }
        finally
        {
        }
    }
}
";
            // Cleanup not in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.<Test>b__0", @"
{
  // Code size       24 (0x18)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  ldnull
  IL_0016:  stloc.1
  IL_0017:  ret
}
");
        }

        [Fact]
        public void FixedStatementInLambda3()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        System.Action a = () =>
        {
            fixed (char* p = ""hello"")
            {
                goto label;
            }
          label: ;
        };
    }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.<Test>b__0", @"
{
  // Code size       27 (0x1b)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
{
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  leave.s    IL_001a
}
  finally
{
  IL_0017:  ldnull
  IL_0018:  stloc.1
  IL_0019:  endfinally
}
  IL_001a:  ret
}
");
        }

        [Fact]
        public void FixedStatementInFieldInitializer1()
        {
            var text = @"
unsafe class C
{
    System.Action a = () =>
        {
            try
            {
                fixed (char* p = ""hello"")
                {
                }
            }
            finally
            {
            }
        };
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.<.ctor>b__0", @"
{
  // Code size       28 (0x1c)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
{
  .try
{
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  leave.s    IL_001b
}
  finally
{
  IL_0017:  ldnull
  IL_0018:  stloc.1
  IL_0019:  endfinally
}
}
  finally
{
  IL_001a:  endfinally
}
  IL_001b:  ret
}
");
        }

        [Fact]
        public void FixedStatementInFieldInitializer2()
        {
            var text = @"
unsafe class C
{
    System.Action a = () =>
        {
            fixed (char* p = ""hello"")
            {
                goto label;
            }
            label: ;
        };
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.<.ctor>b__0", @"
{
  // Code size       27 (0x1b)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
{
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  leave.s    IL_001a
}
  finally
{
  IL_0017:  ldnull
  IL_0018:  stloc.1
  IL_0019:  endfinally
}
  IL_001a:  ret
}
");
        }

        [Fact]
        public void FixedStatementWithBranchOut_LoopBreak()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        while(true)
        {
            fixed (char* p = ""hello"")
            {
                break;
            }
        }
    }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       28 (0x1c)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  IL_0000:  nop
  .try
{
  IL_0001:  ldstr      ""hello""
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  conv.i
  IL_0009:  stloc.0
  IL_000a:  ldloc.1
  IL_000b:  conv.i
  IL_000c:  brfalse.s  IL_0016
  IL_000e:  ldloc.0
  IL_000f:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0014:  add
  IL_0015:  stloc.0
  IL_0016:  leave.s    IL_001b
}
  finally
{
  IL_0018:  ldnull
  IL_0019:  stloc.1
  IL_001a:  endfinally
}
  IL_001b:  ret
}
");
        }

        [Fact]
        public void FixedStatementWithBranchOut_LoopContinue()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        while(true)
        {
            fixed (char* p = ""hello"")
            {
                continue;
            }
        }
    }
}
";
            // Cleanup in finally.
            // CONSIDER: dev11 doesn't have a finally here, but that seems incorrect.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       27 (0x1b)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  IL_0000:  nop
  .try
{
  IL_0001:  ldstr      ""hello""
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  conv.i
  IL_0009:  stloc.0
  IL_000a:  ldloc.1
  IL_000b:  conv.i
  IL_000c:  brfalse.s  IL_0016
  IL_000e:  ldloc.0
  IL_000f:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0014:  add
  IL_0015:  stloc.0
  IL_0016:  leave.s    IL_0000
}
  finally
{
  IL_0018:  ldnull
  IL_0019:  stloc.1
  IL_001a:  endfinally
}
}
");
        }

        [Fact]
        public void FixedStatementWithBranchOut_SwitchBreak()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        switch (1)
        {
            case 1:
                fixed (char* p = ""hello"")
                {
                    break;
                }
        }
    }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       28 (0x1c)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  IL_0000:  nop
  .try
{
  IL_0001:  ldstr      ""hello""
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  conv.i
  IL_0009:  stloc.0
  IL_000a:  ldloc.1
  IL_000b:  conv.i
  IL_000c:  brfalse.s  IL_0016
  IL_000e:  ldloc.0
  IL_000f:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0014:  add
  IL_0015:  stloc.0
  IL_0016:  leave.s    IL_001b
}
  finally
{
  IL_0018:  ldnull
  IL_0019:  stloc.1
  IL_001a:  endfinally
}
  IL_001b:  ret
}
");
        }

        [Fact]
        public void FixedStatementWithBranchOut_SwitchGoto()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        switch (1)
        {
            case 1:
                fixed (char* p = ""hello"")
                {
                    goto case 1;
                }
        }
    }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       27 (0x1b)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  IL_0000:  nop
  .try
{
  IL_0001:  ldstr      ""hello""
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  conv.i
  IL_0009:  stloc.0
  IL_000a:  ldloc.1
  IL_000b:  conv.i
  IL_000c:  brfalse.s  IL_0016
  IL_000e:  ldloc.0
  IL_000f:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0014:  add
  IL_0015:  stloc.0
  IL_0016:  leave.s    IL_0000
}
  finally
{
  IL_0018:  ldnull
  IL_0019:  stloc.1
  IL_001a:  endfinally
}
}
");
        }

        [Fact]
        public void FixedStatementWithBranchOut_BackwardGoto()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
      label:
        fixed (char* p = ""hello"")
        {
            goto label;
        }
    }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       27 (0x1b)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  IL_0000:  nop
  .try
{
  IL_0001:  ldstr      ""hello""
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  conv.i
  IL_0009:  stloc.0
  IL_000a:  ldloc.1
  IL_000b:  conv.i
  IL_000c:  brfalse.s  IL_0016
  IL_000e:  ldloc.0
  IL_000f:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0014:  add
  IL_0015:  stloc.0
  IL_0016:  leave.s    IL_0000
}
  finally
{
  IL_0018:  ldnull
  IL_0019:  stloc.1
  IL_001a:  endfinally
}
}
");
        }

        [Fact]
        public void FixedStatementWithBranchOut_ForwardGoto()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* p = ""hello"")
        {
            goto label;
        }
      label: ;
    }
}
";
            // Cleanup in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       27 (0x1b)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  .try
{
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  leave.s    IL_001a
}
  finally
{
  IL_0017:  ldnull
  IL_0018:  stloc.1
  IL_0019:  endfinally
}
  IL_001a:  ret
}
");
        }

        [Fact]
        public void FixedStatementWithBranchOut_Throw()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* p = ""hello"")
        {
            throw null;
        }
    }
}
";
            // Cleanup not in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       23 (0x17)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  ldnull
  IL_0016:  throw
}
");
        }

        [Fact]
        public void FixedStatementWithBranchOut_Return()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* p = ""hello"")
        {
            return;
        }
    }
}
";
            // Cleanup not in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       22 (0x16)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  ret
}
");
        }

        [Fact]
        public void FixedStatementWithNoBranchOut_Loop()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* p = ""hello"")
        {
            for (int i = 0; i < 10; i++)
            {
            }
        }
    }
}
";
            // Cleanup not in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       37 (0x25)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1,
  int V_2) //i
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  ldc.i4.0
  IL_0016:  stloc.2
  IL_0017:  br.s       IL_001d
  IL_0019:  ldloc.2
  IL_001a:  ldc.i4.1
  IL_001b:  add
  IL_001c:  stloc.2
  IL_001d:  ldloc.2
  IL_001e:  ldc.i4.s   10
  IL_0020:  blt.s      IL_0019
  IL_0022:  ldnull
  IL_0023:  stloc.1
  IL_0024:  ret
}
");
        }

        [Fact]
        public void FixedStatementWithNoBranchOut_InternalGoto()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* p = ""hello"")
        {
            goto label;
          label: ;
        }
    }
}
";
            // NOTE: Dev11 uses a finally here, but it's unnecessary.
            // From GotoChecker::VisitGOTO:
            //      We have an unrealized goto, so we do not know whether it
            //      branches out or not.  We should be conservative and assume that
            //      it does.
            // Cleanup not in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       24 (0x18)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  ldnull
  IL_0016:  stloc.1
  IL_0017:  ret
}
");
        }

        [Fact]
        public void FixedStatementWithNoBranchOut_Switch()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* p = ""hello"")
        {
            switch(*p)
            {
                case 'a':
                    Test();
                    goto case 'b';
                case 'b':
                    Test();
                    goto case 'c';
                case 'c':
                    Test();
                    goto case 'd';
                case 'd':
                    Test();
                    goto case 'e';
                case 'e':
                    Test();
                    goto case 'f';
                case 'f':
                    Test();
                    goto default;
                default:
                    Test();
                    break;
            }
        }
    }
}
";
            // Cleanup not in finally.
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size      104 (0x68)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1,
  char V_2)
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  ldloc.0
  IL_0016:  ldind.u2
  IL_0017:  stloc.2
  IL_0018:  ldloc.2
  IL_0019:  ldc.i4.s   97
  IL_001b:  sub
  IL_001c:  switch    (
  IL_003b,
  IL_0041,
  IL_0047,
  IL_004d,
  IL_0053,
  IL_0059)
  IL_0039:  br.s       IL_005f
  IL_003b:  ldarg.0
  IL_003c:  call       ""void C.Test()""
  IL_0041:  ldarg.0
  IL_0042:  call       ""void C.Test()""
  IL_0047:  ldarg.0
  IL_0048:  call       ""void C.Test()""
  IL_004d:  ldarg.0
  IL_004e:  call       ""void C.Test()""
  IL_0053:  ldarg.0
  IL_0054:  call       ""void C.Test()""
  IL_0059:  ldarg.0
  IL_005a:  call       ""void C.Test()""
  IL_005f:  ldarg.0
  IL_0060:  call       ""void C.Test()""
  IL_0065:  ldnull
  IL_0066:  stloc.1
  IL_0067:  ret
}
");
        }

        [Fact]
        public void FixedStatementWithParenthesizedStringExpression()
        {
            var text = @"
unsafe class C
{
    void Test()
    {
        fixed (char* p = ((""hello"")))
        {
        }
    }
}";
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitOptions: EmitOptions.All).
                VerifyIL("C.Test", @"
{
  // Code size       24 (0x18)
  .maxstack  2
  .locals init (char* V_0, //p
  pinned string V_1)
  IL_0000:  ldstr      ""hello""
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  conv.i
  IL_0008:  stloc.0
  IL_0009:  ldloc.1
  IL_000a:  conv.i
  IL_000b:  brfalse.s  IL_0015
  IL_000d:  ldloc.0
  IL_000e:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0013:  add
  IL_0014:  stloc.0
  IL_0015:  ldnull
  IL_0016:  stloc.1
  IL_0017:  ret
}
");
        }

        #endregion Fixed statement tests

        #region Pointer conversion tests

        [Fact]
        public void ConvertNullToPointer()
        {
            var template = @"
using System;

unsafe class C
{{
    static void Main()
    {{
        {0}
        {{
            char ch = 'a';
            char* p = &ch;
            Console.WriteLine(p == null);

            p = null;
            Console.WriteLine(p == null);
        }}
    }}
}}
";

            var expectedIL = @"
{
  // Code size       36 (0x24)
  .maxstack  2
  .locals init (char V_0, //ch
      char* V_1) //p
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  ldc.i4.s   97
  IL_0004:  stloc.0
  IL_0005:  ldloca.s   V_0
  IL_0007:  conv.u
  IL_0008:  stloc.1
  IL_0009:  ldloc.1
  IL_000a:  ldc.i4.0
  IL_000b:  conv.u
  IL_000c:  ceq
  IL_000e:  call       ""void System.Console.WriteLine(bool)""
  IL_0013:  nop
  IL_0014:  ldc.i4.0
  IL_0015:  conv.u
  IL_0016:  stloc.1
  IL_0017:  ldloc.1
  IL_0018:  ldc.i4.0
  IL_0019:  conv.u
  IL_001a:  ceq
  IL_001c:  call       ""void System.Console.WriteLine(bool)""
  IL_0021:  nop
  IL_0022:  nop
  IL_0023:  ret
}
";
            var expectedOutput = @"False
True";

            CompileAndVerify(string.Format(template, "unchecked"), options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: expectedOutput).VerifyIL("C.Main", expectedIL);
            CompileAndVerify(string.Format(template, "checked"), options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: expectedOutput).VerifyIL("C.Main", expectedIL);
        }

        [Fact]
        public void ConvertPointerToPointerOrVoid()
        {
            var template = @"
using System;

unsafe class C
{{
    static void Main()
    {{
        {0}
        {{
            char ch = 'a';
            char* c1 = &ch;
            void* v1 = c1;
            void* v2 = (void**)v1;
            char* c2 = (char*)v2;
            Console.WriteLine(*c2);
        }}
    }}
}}
";
            var expectedIL = @"
{
  // Code size       27 (0x1b)
  .maxstack  1
  .locals init (char V_0, //ch
  char* V_1, //c1
  void* V_2, //v1
  void* V_3, //v2
  char* V_4) //c2
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  ldc.i4.s   97
  IL_0004:  stloc.0
  IL_0005:  ldloca.s   V_0
  IL_0007:  conv.u
  IL_0008:  stloc.1
  IL_0009:  ldloc.1
  IL_000a:  stloc.2
  IL_000b:  ldloc.2
  IL_000c:  stloc.3
  IL_000d:  ldloc.3
  IL_000e:  stloc.s    V_4
  IL_0010:  ldloc.s    V_4
  IL_0012:  ldind.u2
  IL_0013:  call       ""void System.Console.WriteLine(char)""
  IL_0018:  nop
  IL_0019:  nop
  IL_001a:  ret
}
";
            var expectedOutput = @"a";

            CompileAndVerify(string.Format(template, "unchecked"), options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: expectedOutput).VerifyIL("C.Main", expectedIL);
            CompileAndVerify(string.Format(template, "checked"), options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: expectedOutput).VerifyIL("C.Main", expectedIL);
        }

        [Fact]
        public void ConvertPointerToNumericUnchecked()
        {
            var text = @"
using System;

unsafe class C
{
    void M(int* pi, void* pv, sbyte sb, byte b, short s, ushort us, int i, uint ui, long l, ulong ul)
    {
        unchecked
        {
            sb = (sbyte)pi;
            b = (byte)pi;
            s = (short)pi;
            us = (ushort)pi;
            i = (int)pi;
            ui = (uint)pi;
            l = (long)pi;
            ul = (ulong)pi;

            sb = (sbyte)pv;
            b = (byte)pv;
            s = (short)pv;
            us = (ushort)pv;
            i = (int)pv;
            ui = (uint)pv;
            l = (long)pv;
            ul = (ulong)pv;
        }
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitPdb: true).VerifyIL("C.M", @"
{
  // Code size       68 (0x44)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  ldarg.1
  IL_0003:  conv.i1
  IL_0004:  starg.s    V_3
  IL_0006:  ldarg.1
  IL_0007:  conv.u1
  IL_0008:  starg.s    V_4
  IL_000a:  ldarg.1
  IL_000b:  conv.i2
  IL_000c:  starg.s    V_5
  IL_000e:  ldarg.1
  IL_000f:  conv.u2
  IL_0010:  starg.s    V_6
  IL_0012:  ldarg.1
  IL_0013:  conv.i4
  IL_0014:  starg.s    V_7
  IL_0016:  ldarg.1
  IL_0017:  conv.u4
  IL_0018:  starg.s    V_8
  IL_001a:  ldarg.1
  IL_001b:  conv.u8
  IL_001c:  starg.s    V_9
  IL_001e:  ldarg.1
  IL_001f:  conv.u8
  IL_0020:  starg.s    V_10
  IL_0022:  ldarg.2
  IL_0023:  conv.i1
  IL_0024:  starg.s    V_3
  IL_0026:  ldarg.2
  IL_0027:  conv.u1
  IL_0028:  starg.s    V_4
  IL_002a:  ldarg.2
  IL_002b:  conv.i2
  IL_002c:  starg.s    V_5
  IL_002e:  ldarg.2
  IL_002f:  conv.u2
  IL_0030:  starg.s    V_6
  IL_0032:  ldarg.2
  IL_0033:  conv.i4
  IL_0034:  starg.s    V_7
  IL_0036:  ldarg.2
  IL_0037:  conv.u4
  IL_0038:  starg.s    V_8
  IL_003a:  ldarg.2
  IL_003b:  conv.u8
  IL_003c:  starg.s    V_9
  IL_003e:  ldarg.2
  IL_003f:  conv.u8
  IL_0040:  starg.s    V_10
  IL_0042:  nop
  IL_0043:  ret
}
");
        }

        [Fact]
        public void ConvertPointerToNumericChecked()
        {
            var text = @"
using System;

unsafe class C
{
    void M(int* pi, void* pv, sbyte sb, byte b, short s, ushort us, int i, uint ui, long l, ulong ul)
    {
        checked
        {
            sb = (sbyte)pi;
            b = (byte)pi;
            s = (short)pi;
            us = (ushort)pi;
            i = (int)pi;
            ui = (uint)pi;
            l = (long)pi;
            ul = (ulong)pi;

            sb = (sbyte)pv;
            b = (byte)pv;
            s = (short)pv;
            us = (ushort)pv;
            i = (int)pv;
            ui = (uint)pv;
            l = (long)pv;
            ul = (ulong)pv;
        }
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitPdb: true).VerifyIL("C.M", @"
{
  // Code size       68 (0x44)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  ldarg.1
  IL_0003:  conv.ovf.i1.un
  IL_0004:  starg.s    V_3
  IL_0006:  ldarg.1
  IL_0007:  conv.ovf.u1.un
  IL_0008:  starg.s    V_4
  IL_000a:  ldarg.1
  IL_000b:  conv.ovf.i2.un
  IL_000c:  starg.s    V_5
  IL_000e:  ldarg.1
  IL_000f:  conv.ovf.u2.un
  IL_0010:  starg.s    V_6
  IL_0012:  ldarg.1
  IL_0013:  conv.ovf.i4.un
  IL_0014:  starg.s    V_7
  IL_0016:  ldarg.1
  IL_0017:  conv.ovf.u4.un
  IL_0018:  starg.s    V_8
  IL_001a:  ldarg.1
  IL_001b:  conv.ovf.i8.un
  IL_001c:  starg.s    V_9
  IL_001e:  ldarg.1
  IL_001f:  conv.u8
  IL_0020:  starg.s    V_10
  IL_0022:  ldarg.2
  IL_0023:  conv.ovf.i1.un
  IL_0024:  starg.s    V_3
  IL_0026:  ldarg.2
  IL_0027:  conv.ovf.u1.un
  IL_0028:  starg.s    V_4
  IL_002a:  ldarg.2
  IL_002b:  conv.ovf.i2.un
  IL_002c:  starg.s    V_5
  IL_002e:  ldarg.2
  IL_002f:  conv.ovf.u2.un
  IL_0030:  starg.s    V_6
  IL_0032:  ldarg.2
  IL_0033:  conv.ovf.i4.un
  IL_0034:  starg.s    V_7
  IL_0036:  ldarg.2
  IL_0037:  conv.ovf.u4.un
  IL_0038:  starg.s    V_8
  IL_003a:  ldarg.2
  IL_003b:  conv.ovf.i8.un
  IL_003c:  starg.s    V_9
  IL_003e:  ldarg.2
  IL_003f:  conv.u8
  IL_0040:  starg.s    V_10
  IL_0042:  nop
  IL_0043:  ret
}
");
        }

        [Fact]
        public void ConvertNumericToPointerUnchecked()
        {
            var text = @"
using System;

unsafe class C
{
    void M(int* pi, void* pv, sbyte sb, byte b, short s, ushort us, int i, uint ui, long l, ulong ul)
    {
        unchecked
        {
            pi = (int*)sb;
            pi = (int*)b;
            pi = (int*)s;
            pi = (int*)us;
            pi = (int*)i;
            pi = (int*)ui;
            pi = (int*)l;
            pi = (int*)ul;

            pv = (void*)sb;
            pv = (void*)b;
            pv = (void*)s;
            pv = (void*)us;
            pv = (void*)i;
            pv = (void*)ui;
            pv = (void*)l;
            pv = (void*)ul;
        }
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitPdb: true).VerifyIL("C.M", @"
{
  // Code size       82 (0x52)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  ldarg.3
  IL_0003:  conv.i
  IL_0004:  starg.s    V_1
  IL_0006:  ldarg.s    V_4
  IL_0008:  conv.u
  IL_0009:  starg.s    V_1
  IL_000b:  ldarg.s    V_5
  IL_000d:  conv.i
  IL_000e:  starg.s    V_1
  IL_0010:  ldarg.s    V_6
  IL_0012:  conv.u
  IL_0013:  starg.s    V_1
  IL_0015:  ldarg.s    V_7
  IL_0017:  conv.i
  IL_0018:  starg.s    V_1
  IL_001a:  ldarg.s    V_8
  IL_001c:  conv.u
  IL_001d:  starg.s    V_1
  IL_001f:  ldarg.s    V_9
  IL_0021:  conv.u
  IL_0022:  starg.s    V_1
  IL_0024:  ldarg.s    V_10
  IL_0026:  conv.u
  IL_0027:  starg.s    V_1
  IL_0029:  ldarg.3
  IL_002a:  conv.i
  IL_002b:  starg.s    V_2
  IL_002d:  ldarg.s    V_4
  IL_002f:  conv.u
  IL_0030:  starg.s    V_2
  IL_0032:  ldarg.s    V_5
  IL_0034:  conv.i
  IL_0035:  starg.s    V_2
  IL_0037:  ldarg.s    V_6
  IL_0039:  conv.u
  IL_003a:  starg.s    V_2
  IL_003c:  ldarg.s    V_7
  IL_003e:  conv.i
  IL_003f:  starg.s    V_2
  IL_0041:  ldarg.s    V_8
  IL_0043:  conv.u
  IL_0044:  starg.s    V_2
  IL_0046:  ldarg.s    V_9
  IL_0048:  conv.u
  IL_0049:  starg.s    V_2
  IL_004b:  ldarg.s    V_10
  IL_004d:  conv.u
  IL_004e:  starg.s    V_2
  IL_0050:  nop
  IL_0051:  ret
}
");
        }

        [Fact]
        public void ConvertNumericToPointerChecked()
        {
            var text = @"
using System;

unsafe class C
{
    void M(int* pi, void* pv, sbyte sb, byte b, short s, ushort us, int i, uint ui, long l, ulong ul)
    {
        checked
        {
            pi = (int*)sb;
            pi = (int*)b;
            pi = (int*)s;
            pi = (int*)us;
            pi = (int*)i;
            pi = (int*)ui;
            pi = (int*)l;
            pi = (int*)ul;

            pv = (void*)sb;
            pv = (void*)b;
            pv = (void*)s;
            pv = (void*)us;
            pv = (void*)i;
            pv = (void*)ui;
            pv = (void*)l;
            pv = (void*)ul;
        }
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeDll, emitPdb: true).VerifyIL("C.M", @"
{
  // Code size       82 (0x52)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  ldarg.3
  IL_0003:  conv.ovf.u
  IL_0004:  starg.s    V_1
  IL_0006:  ldarg.s    V_4
  IL_0008:  conv.u
  IL_0009:  starg.s    V_1
  IL_000b:  ldarg.s    V_5
  IL_000d:  conv.ovf.u
  IL_000e:  starg.s    V_1
  IL_0010:  ldarg.s    V_6
  IL_0012:  conv.u
  IL_0013:  starg.s    V_1
  IL_0015:  ldarg.s    V_7
  IL_0017:  conv.ovf.u
  IL_0018:  starg.s    V_1
  IL_001a:  ldarg.s    V_8
  IL_001c:  conv.u
  IL_001d:  starg.s    V_1
  IL_001f:  ldarg.s    V_9
  IL_0021:  conv.ovf.u
  IL_0022:  starg.s    V_1
  IL_0024:  ldarg.s    V_10
  IL_0026:  conv.ovf.u.un
  IL_0027:  starg.s    V_1
  IL_0029:  ldarg.3
  IL_002a:  conv.ovf.u
  IL_002b:  starg.s    V_2
  IL_002d:  ldarg.s    V_4
  IL_002f:  conv.u
  IL_0030:  starg.s    V_2
  IL_0032:  ldarg.s    V_5
  IL_0034:  conv.ovf.u
  IL_0035:  starg.s    V_2
  IL_0037:  ldarg.s    V_6
  IL_0039:  conv.u
  IL_003a:  starg.s    V_2
  IL_003c:  ldarg.s    V_7
  IL_003e:  conv.ovf.u
  IL_003f:  starg.s    V_2
  IL_0041:  ldarg.s    V_8
  IL_0043:  conv.u
  IL_0044:  starg.s    V_2
  IL_0046:  ldarg.s    V_9
  IL_0048:  conv.ovf.u
  IL_0049:  starg.s    V_2
  IL_004b:  ldarg.s    V_10
  IL_004d:  conv.ovf.u.un
  IL_004e:  starg.s    V_2
  IL_0050:  nop
  IL_0051:  ret
}
");
        }

        [Fact]
        public void ConvertClassToPointerUDC()
        {
            var template = @"
using System;

unsafe class C
{{
    void M(int* pi, void* pv, Explicit e, Implicit i)
    {{
        {0}
        {{
            e = (Explicit)pi;
            e = (Explicit)pv;

            i = pi;
            i = pv;

            pi = (int*)e;
            pv = (int*)e;

            pi = i;
            pv = i;
        }}
    }}
}}

unsafe class Explicit
{{
    public static explicit operator Explicit(void* p)
    {{
        return null;
    }}

    public static explicit operator int*(Explicit e)
    {{
        return null;
    }}
}}

unsafe class Implicit
{{
    public static implicit operator Implicit(void* p)
    {{
        return null;
    }}

    public static implicit operator int*(Implicit e)
    {{
        return null;
    }}
}}
";
            var expectedIL = @"
{
  // Code size       70 (0x46)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  ldarg.1
  IL_0003:  call       ""Explicit Explicit.op_Explicit(void*)""
  IL_0008:  starg.s    V_3
  IL_000a:  ldarg.2
  IL_000b:  call       ""Explicit Explicit.op_Explicit(void*)""
  IL_0010:  starg.s    V_3
  IL_0012:  ldarg.1
  IL_0013:  call       ""Implicit Implicit.op_Implicit(void*)""
  IL_0018:  starg.s    V_4
  IL_001a:  ldarg.2
  IL_001b:  call       ""Implicit Implicit.op_Implicit(void*)""
  IL_0020:  starg.s    V_4
  IL_0022:  ldarg.3
  IL_0023:  call       ""int* Explicit.op_Explicit(Explicit)""
  IL_0028:  starg.s    V_1
  IL_002a:  ldarg.3
  IL_002b:  call       ""int* Explicit.op_Explicit(Explicit)""
  IL_0030:  starg.s    V_2
  IL_0032:  ldarg.s    V_4
  IL_0034:  call       ""int* Implicit.op_Implicit(Implicit)""
  IL_0039:  starg.s    V_1
  IL_003b:  ldarg.s    V_4
  IL_003d:  call       ""int* Implicit.op_Implicit(Implicit)""
  IL_0042:  starg.s    V_2
  IL_0044:  nop
  IL_0045:  ret
}
";
            CompileAndVerify(string.Format(template, "unchecked"), options: TestOptions.UnsafeDll, emitPdb: true).VerifyIL("C.M", expectedIL);
            CompileAndVerify(string.Format(template, "checked"), options: TestOptions.UnsafeDll, emitPdb: true).VerifyIL("C.M", expectedIL);
        }

        [Fact]
        public void ConvertIntPtrToPointer()
        {
            var template = @"
using System;

unsafe class C
{{
    void M(int* pi, void* pv, IntPtr i, UIntPtr u)
    {{
        {0}
        {{
            i = (IntPtr)pi;
            i = (IntPtr)pv;

            u = (UIntPtr)pi;
            u = (UIntPtr)pv;

            pi = (int*)i;
            pv = (int*)i;

            pi = (int*)u;
            pv = (int*)u;
        }}
    }}
}}
";
            // Nothing special here - just more UDCs.
            var expectedIL = @"
{
  // Code size       70 (0x46)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  ldarg.1
  IL_0003:  call       ""System.IntPtr System.IntPtr.op_Explicit(void*)""
  IL_0008:  starg.s    V_3
  IL_000a:  ldarg.2
  IL_000b:  call       ""System.IntPtr System.IntPtr.op_Explicit(void*)""
  IL_0010:  starg.s    V_3
  IL_0012:  ldarg.1
  IL_0013:  call       ""System.UIntPtr System.UIntPtr.op_Explicit(void*)""
  IL_0018:  starg.s    V_4
  IL_001a:  ldarg.2
  IL_001b:  call       ""System.UIntPtr System.UIntPtr.op_Explicit(void*)""
  IL_0020:  starg.s    V_4
  IL_0022:  ldarg.3
  IL_0023:  call       ""void* System.IntPtr.op_Explicit(System.IntPtr)""
  IL_0028:  starg.s    V_1
  IL_002a:  ldarg.3
  IL_002b:  call       ""void* System.IntPtr.op_Explicit(System.IntPtr)""
  IL_0030:  starg.s    V_2
  IL_0032:  ldarg.s    V_4
  IL_0034:  call       ""void* System.UIntPtr.op_Explicit(System.UIntPtr)""
  IL_0039:  starg.s    V_1
  IL_003b:  ldarg.s    V_4
  IL_003d:  call       ""void* System.UIntPtr.op_Explicit(System.UIntPtr)""
  IL_0042:  starg.s    V_2
  IL_0044:  nop
  IL_0045:  ret
}
";
            CompileAndVerify(string.Format(template, "unchecked"), options: TestOptions.UnsafeDll, emitPdb: true).VerifyIL("C.M", expectedIL);
            CompileAndVerify(string.Format(template, "checked"), options: TestOptions.UnsafeDll, emitPdb: true).VerifyIL("C.M", expectedIL);
        }

        [Fact]
        public void FixedStatementConversion()
        {
            var template = @"
using System;

unsafe class C
{{
    char c = 'a';
    char[] a = new char[1];

    static void Main()
    {{
        {0}
        {{
            C c = new C();
            fixed (void* p = &c.c, q = c.a, r = ""hello"")
            {{
                Console.Write((int)*(char*)p);
                Console.Write((int)*(char*)q);
                Console.Write((int)*(char*)r);
            }}
        }}
    }}
}}
";
            // NB: "pinned System.IntPtr&" (which ildasm displays as "pinned native int&"), not void.
            var expectedIL = @"
{
  // Code size      118 (0x76)
  .maxstack  2
  .locals init (C V_0, //c
  pinned System.IntPtr& V_1, //p
  pinned System.IntPtr& V_2, //q
  void* V_3, //r
  char[] V_4,
  pinned string V_5,
  bool V_6)
  IL_0000:  nop
  IL_0001:  nop
  IL_0002:  newobj     ""C..ctor()""
  IL_0007:  stloc.0
  IL_0008:  ldloc.0
  IL_0009:  ldflda     ""char C.c""
  IL_000e:  stloc.1
  IL_000f:  ldloc.0
  IL_0010:  ldfld      ""char[] C.a""
  IL_0015:  dup
  IL_0016:  stloc.s    V_4
  IL_0018:  brfalse.s  IL_0020
  IL_001a:  ldloc.s    V_4
  IL_001c:  ldlen
  IL_001d:  conv.i4
  IL_001e:  brtrue.s   IL_0025
  IL_0020:  ldc.i4.0
  IL_0021:  conv.u
  IL_0022:  stloc.2
  IL_0023:  br.s       IL_002e
  IL_0025:  ldloc.s    V_4
  IL_0027:  ldc.i4.0
  IL_0028:  ldelema    ""char""
  IL_002d:  stloc.2
  IL_002e:  ldstr      ""hello""
  IL_0033:  stloc.s    V_5
  IL_0035:  ldloc.s    V_5
  IL_0037:  conv.i
  IL_0038:  stloc.3
  IL_0039:  ldloc.s    V_5
  IL_003b:  conv.i
  IL_003c:  ldnull
  IL_003d:  ceq
  IL_003f:  stloc.s    V_6
  IL_0041:  ldloc.s    V_6
  IL_0043:  brtrue.s   IL_004f
  IL_0045:  ldloc.3
  IL_0046:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_004b:  add
  IL_004c:  stloc.3
  IL_004d:  br.s       IL_004f
  IL_004f:  nop
  IL_0050:  ldloc.1
  IL_0051:  conv.i
  IL_0052:  ldind.u2
  IL_0053:  call       ""void System.Console.Write(int)""
  IL_0058:  nop
  IL_0059:  ldloc.2
  IL_005a:  conv.i
  IL_005b:  ldind.u2
  IL_005c:  call       ""void System.Console.Write(int)""
  IL_0061:  nop
  IL_0062:  ldloc.3
  IL_0063:  ldind.u2
  IL_0064:  call       ""void System.Console.Write(int)""
  IL_0069:  nop
  IL_006a:  nop
  IL_006b:  ldc.i4.0
  IL_006c:  conv.u
  IL_006d:  stloc.1
  IL_006e:  ldc.i4.0
  IL_006f:  conv.u
  IL_0070:  stloc.2
  IL_0071:  ldnull
  IL_0072:  stloc.s    V_5
  IL_0074:  nop
  IL_0075:  ret
}
";
            var expectedOutput = @"970104";
            CompileAndVerify(string.Format(template, "unchecked"), options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: expectedOutput).VerifyIL("C.Main", expectedIL);
            CompileAndVerify(string.Format(template, "checked"), options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: expectedOutput).VerifyIL("C.Main", expectedIL);
        }

        [Fact]
        public void FixedStatementVoidPointerPointer()
        {
            var template = @"
using System;

unsafe class C
{{
    void* v;

    static void Main()
    {{
        {0}
        {{
            char ch = 'a';
            C c = new C();
            c.v = &ch;
            fixed (void** p = &c.v)
            {{
                Console.Write(*(char*)*p);
            }}
        }}
    }}
}}
";
            // NB: "pinned void*&", as in Dev10.
            var expectedIL = @"
{
  // Code size       36 (0x24)
  .maxstack  3
  .locals init (char V_0, //ch
  pinned void*& V_1) //p
  IL_0000:  ldc.i4.s   97
  IL_0002:  stloc.0
  IL_0003:  newobj     ""C..ctor()""
  IL_0008:  dup
  IL_0009:  ldloca.s   V_0
  IL_000b:  conv.u
  IL_000c:  stfld      ""void* C.v""
  IL_0011:  ldflda     ""void* C.v""
  IL_0016:  stloc.1
  IL_0017:  ldloc.1
  IL_0018:  conv.i
  IL_0019:  ldind.i
  IL_001a:  ldind.u2
  IL_001b:  call       ""void System.Console.Write(char)""
  IL_0020:  ldc.i4.0
  IL_0021:  conv.u
  IL_0022:  stloc.1
  IL_0023:  ret
}
";
            var expectedOutput = @"a";
            CompileAndVerify(string.Format(template, "unchecked"), options: TestOptions.UnsafeExe, expectedOutput: expectedOutput).VerifyIL("C.Main", expectedIL);
            CompileAndVerify(string.Format(template, "checked"), options: TestOptions.UnsafeExe, expectedOutput: expectedOutput).VerifyIL("C.Main", expectedIL);
        }

        [Fact]
        public void PointerArrayConversion()
        {
            var template = @"
using System;

unsafe class C
{{
    void M(int*[] api, void*[] apv, Array a)
    {{
        {0}
        {{
            a = api;
            a = apv;

            api = (int*[])a;
            apv = (void*[])a;
        }}
    }}
}}
";
            var expectedIL = @"
{
  // Code size       23 (0x17)
  .maxstack  1
  IL_0000:  ldarg.1
  IL_0001:  starg.s    V_3
  IL_0003:  ldarg.2
  IL_0004:  starg.s    V_3
  IL_0006:  ldarg.3
  IL_0007:  castclass  ""int*[]""
  IL_000c:  starg.s    V_1
  IL_000e:  ldarg.3
  IL_000f:  castclass  ""void*[]""
  IL_0014:  starg.s    V_2
  IL_0016:  ret
}
";
            CompileAndVerify(string.Format(template, "unchecked"), options: TestOptions.UnsafeDll).VerifyIL("C.M", expectedIL);
            CompileAndVerify(string.Format(template, "checked"), options: TestOptions.UnsafeDll).VerifyIL("C.M", expectedIL);
        }

        [Fact]
        public void PointerArrayConversionRuntimeError()
        {
            var text = @"
unsafe class C
{
    static void Main()
    {
        int*[] api = new int*[1];
        System.Array a = api;
        a.GetValue(0);
    }
}
";
            CompileAndVerifyException<NotSupportedException>(text, "Type is not supported.", allowUnsafe: true);
        }

        [Fact]
        public void PointerArrayEnumerableConversion()
        {
            var template = @"
using System.Collections;

unsafe class C
{{
    void M(int*[] api, void*[] apv, IEnumerable e)
    {{
        {0}
        {{
            e = api;
            e = apv;

            api = (int*[])e;
            apv = (void*[])e;
        }}
    }}
}}
";
            var expectedIL = @"
{
  // Code size       23 (0x17)
  .maxstack  1
  IL_0000:  ldarg.1
  IL_0001:  starg.s    V_3
  IL_0003:  ldarg.2
  IL_0004:  starg.s    V_3
  IL_0006:  ldarg.3
  IL_0007:  castclass  ""int*[]""
  IL_000c:  starg.s    V_1
  IL_000e:  ldarg.3
  IL_000f:  castclass  ""void*[]""
  IL_0014:  starg.s    V_2
  IL_0016:  ret
}
";
            CompileAndVerify(string.Format(template, "unchecked"), options: TestOptions.UnsafeDll).VerifyIL("C.M", expectedIL);
            CompileAndVerify(string.Format(template, "checked"), options: TestOptions.UnsafeDll).VerifyIL("C.M", expectedIL);
        }

        [Fact]
        public void PointerArrayEnumerableConversionRuntimeError()
        {
            var text = @"
unsafe class C
{
    static void Main()
    {
        int*[] api = new int*[1];
        System.Collections.IEnumerable e = api;
        var enumerator = e.GetEnumerator();
        enumerator.MoveNext();
        var current = enumerator.Current;
    }
}
";
            CompileAndVerifyException<NotSupportedException>(text, "Type is not supported.", allowUnsafe: true);
        }

        [Fact]
        public void PointerArrayForeachSingle()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        int*[] array = new []
        {
            (int*)1,
            (int*)2,
        };
        foreach (var element in array)
        {
            Console.Write((int)element);
        }
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "12").VerifyIL("C.Main", @"
{
  // Code size       41 (0x29)
  .maxstack  4
  .locals init (int*[] V_0,
  int V_1)
  IL_0000:  ldc.i4.2
  IL_0001:  newarr     ""int*""
  IL_0006:  dup
  IL_0007:  ldc.i4.0
  IL_0008:  ldc.i4.1
  IL_0009:  conv.i
  IL_000a:  stelem.i
  IL_000b:  dup
  IL_000c:  ldc.i4.1
  IL_000d:  ldc.i4.2
  IL_000e:  conv.i
  IL_000f:  stelem.i
  IL_0010:  stloc.0
  IL_0011:  ldc.i4.0
  IL_0012:  stloc.1
  IL_0013:  br.s       IL_0022
  IL_0015:  ldloc.0
  IL_0016:  ldloc.1
  IL_0017:  ldelem.i
  IL_0018:  conv.i4
  IL_0019:  call       ""void System.Console.Write(int)""
  IL_001e:  ldloc.1
  IL_001f:  ldc.i4.1
  IL_0020:  add
  IL_0021:  stloc.1
  IL_0022:  ldloc.1
  IL_0023:  ldloc.0
  IL_0024:  ldlen
  IL_0025:  conv.i4
  IL_0026:  blt.s      IL_0015
  IL_0028:  ret
}
");
        }

        [Fact]
        public void PointerArrayForeachMultiple()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        int*[,] array = new [,]
        {
            { (int*)1, (int*)2, },
            { (int*)3, (int*)4, },
        };
        foreach (var element in array)
        {
            Console.Write((int)element);
        }
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "1234").VerifyIL("C.Main", @"
{
  // Code size      120 (0x78)
  .maxstack  5
  .locals init (int*[,] V_0,
  int V_1,
  int V_2,
  int V_3,
  int V_4)
  IL_0000:  ldc.i4.2
  IL_0001:  ldc.i4.2
  IL_0002:  newobj     ""int*[*,*]..ctor""
  IL_0007:  dup
  IL_0008:  ldc.i4.0
  IL_0009:  ldc.i4.0
  IL_000a:  ldc.i4.1
  IL_000b:  conv.i
  IL_000c:  call       ""int*[*,*].Set""
  IL_0011:  dup
  IL_0012:  ldc.i4.0
  IL_0013:  ldc.i4.1
  IL_0014:  ldc.i4.2
  IL_0015:  conv.i
  IL_0016:  call       ""int*[*,*].Set""
  IL_001b:  dup
  IL_001c:  ldc.i4.1
  IL_001d:  ldc.i4.0
  IL_001e:  ldc.i4.3
  IL_001f:  conv.i
  IL_0020:  call       ""int*[*,*].Set""
  IL_0025:  dup
  IL_0026:  ldc.i4.1
  IL_0027:  ldc.i4.1
  IL_0028:  ldc.i4.4
  IL_0029:  conv.i
  IL_002a:  call       ""int*[*,*].Set""
  IL_002f:  stloc.0
  IL_0030:  ldloc.0
  IL_0031:  ldc.i4.0
  IL_0032:  callvirt   ""int System.Array.GetUpperBound(int)""
  IL_0037:  stloc.1
  IL_0038:  ldloc.0
  IL_0039:  ldc.i4.1
  IL_003a:  callvirt   ""int System.Array.GetUpperBound(int)""
  IL_003f:  stloc.2
  IL_0040:  ldloc.0
  IL_0041:  ldc.i4.0
  IL_0042:  callvirt   ""int System.Array.GetLowerBound(int)""
  IL_0047:  stloc.3
  IL_0048:  br.s       IL_0073
  IL_004a:  ldloc.0
  IL_004b:  ldc.i4.1
  IL_004c:  callvirt   ""int System.Array.GetLowerBound(int)""
  IL_0051:  stloc.s    V_4
  IL_0053:  br.s       IL_006a
  IL_0055:  ldloc.0
  IL_0056:  ldloc.3
  IL_0057:  ldloc.s    V_4
  IL_0059:  call       ""int*[*,*].Get""
  IL_005e:  conv.i4
  IL_005f:  call       ""void System.Console.Write(int)""
  IL_0064:  ldloc.s    V_4
  IL_0066:  ldc.i4.1
  IL_0067:  add
  IL_0068:  stloc.s    V_4
  IL_006a:  ldloc.s    V_4
  IL_006c:  ldloc.2
  IL_006d:  ble.s      IL_0055
  IL_006f:  ldloc.3
  IL_0070:  ldc.i4.1
  IL_0071:  add
  IL_0072:  stloc.3
  IL_0073:  ldloc.3
  IL_0074:  ldloc.1
  IL_0075:  ble.s      IL_004a
  IL_0077:  ret
}
");
        }

        [Fact]
        public void PointerArrayForeachEnumerable()
        {
            var text = @"
using System;
using System.Collections;

unsafe class C
{
    static void Main()
    {
        int*[] array = new []
        {
            (int*)1,
            (int*)2,
        };
        foreach (var element in (IEnumerable)array)
        {
            Console.Write((int)element);
        }
    }
}
";
            CompileAndVerifyException<NotSupportedException>(text, "Type is not supported.", allowUnsafe: true);
        }

        #endregion Pointer conversion tests

        #region sizeof tests

        [Fact]
        public void SizeOfConstant()
        {
            var text = @"
using System;

class C
{
    static void Main()
    {
        Console.WriteLine(sizeof(sbyte));
        Console.WriteLine(sizeof(byte));
        Console.WriteLine(sizeof(short));
        Console.WriteLine(sizeof(ushort));
        Console.WriteLine(sizeof(int));
        Console.WriteLine(sizeof(uint));
        Console.WriteLine(sizeof(long));
        Console.WriteLine(sizeof(ulong));
        Console.WriteLine(sizeof(char));
        Console.WriteLine(sizeof(float));
        Console.WriteLine(sizeof(double));
        Console.WriteLine(sizeof(bool));
        Console.WriteLine(sizeof(decimal)); //Supported by dev10, but not spec.
    }
}
";
            var expectedOutput = @"
1
1
2
2
4
4
8
8
2
4
8
1
16
".Trim();
            CompileAndVerify(text, options: TestOptions.Exe, expectedOutput: expectedOutput).VerifyIL("C.Main", @"
{
  // Code size       80 (0x50)
  .maxstack  1
  IL_0000:  ldc.i4.1
  IL_0001:  call       ""void System.Console.WriteLine(int)""
  IL_0006:  ldc.i4.1
  IL_0007:  call       ""void System.Console.WriteLine(int)""
  IL_000c:  ldc.i4.2
  IL_000d:  call       ""void System.Console.WriteLine(int)""
  IL_0012:  ldc.i4.2
  IL_0013:  call       ""void System.Console.WriteLine(int)""
  IL_0018:  ldc.i4.4
  IL_0019:  call       ""void System.Console.WriteLine(int)""
  IL_001e:  ldc.i4.4
  IL_001f:  call       ""void System.Console.WriteLine(int)""
  IL_0024:  ldc.i4.8
  IL_0025:  call       ""void System.Console.WriteLine(int)""
  IL_002a:  ldc.i4.8
  IL_002b:  call       ""void System.Console.WriteLine(int)""
  IL_0030:  ldc.i4.2
  IL_0031:  call       ""void System.Console.WriteLine(int)""
  IL_0036:  ldc.i4.4
  IL_0037:  call       ""void System.Console.WriteLine(int)""
  IL_003c:  ldc.i4.8
  IL_003d:  call       ""void System.Console.WriteLine(int)""
  IL_0042:  ldc.i4.1
  IL_0043:  call       ""void System.Console.WriteLine(int)""
  IL_0048:  ldc.i4.s   16
  IL_004a:  call       ""void System.Console.WriteLine(int)""
  IL_004f:  ret
}
");
        }

        [Fact]
        public void SizeOfNonConstant()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        Console.WriteLine(sizeof(S));
        Console.WriteLine(sizeof(Outer.Inner));
        Console.WriteLine(sizeof(int*));
        Console.WriteLine(sizeof(void*));
    }
}

struct S
{
    public byte b;
}

class Outer
{
    public struct Inner
    {
        public char c;
    }
}
";
            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
1
2
4
4
".Trim();
            }
            else
            {
                expectedOutput = @"
1
2
8
8
".Trim();
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput).VerifyIL("C.Main", @"
{
  // Code size       45 (0x2d)
  .maxstack  1
  IL_0000:  sizeof     ""S""
  IL_0006:  call       ""void System.Console.WriteLine(int)""
  IL_000b:  sizeof     ""Outer.Inner""
  IL_0011:  call       ""void System.Console.WriteLine(int)""
  IL_0016:  sizeof     ""int*""
  IL_001c:  call       ""void System.Console.WriteLine(int)""
  IL_0021:  sizeof     ""void*""
  IL_0027:  call       ""void System.Console.WriteLine(int)""
  IL_002c:  ret
}
");
        }

        [Fact]
        public void SizeOfEnum()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        Console.WriteLine(sizeof(E1));
        Console.WriteLine(sizeof(E2));
        Console.WriteLine(sizeof(E3));
    }
}

enum E1 { A }
enum E2 : byte { A }
enum E3 : long { A }
";
            var expectedOutput = @"
4
1
8
".Trim();
            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput).VerifyIL("C.Main", @"
{
  // Code size       19 (0x13)
  .maxstack  1
  IL_0000:  ldc.i4.4
  IL_0001:  call       ""void System.Console.WriteLine(int)""
  IL_0006:  ldc.i4.1
  IL_0007:  call       ""void System.Console.WriteLine(int)""
  IL_000c:  ldc.i4.8
  IL_000d:  call       ""void System.Console.WriteLine(int)""
  IL_0012:  ret
}
");
        }

        #endregion sizeof tests

        #region Pointer arithmetic tests

        [Fact]
        public void NumericAdditionChecked()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        checked
        {
            S s = new S();
            S* p = &s;
            p = p + 2;
            p = p + 3u;
            p = p + 4l;
            p = p + 5ul;
        }
    }
}
";

            // Dev10 has conv.i after IL_000d and conv.i8 in place of conv.u8 at IL_0017.
            CompileAndVerify(text, options: TestOptions.UnsafeDll).VerifyIL("S.Main", @"
{
  // Code size       59 (0x3b)
  .maxstack  3
  .locals init (S V_0) //s
  IL_0000:  ldloca.s   V_0
  IL_0002:  initobj    ""S""
  IL_0008:  ldloca.s   V_0
  IL_000a:  conv.u
  IL_000b:  ldc.i4.2
  IL_000c:  conv.i
  IL_000d:  sizeof     ""S""
  IL_0013:  mul.ovf
  IL_0014:  add.ovf.un
  IL_0015:  ldc.i4.3
  IL_0016:  conv.u8
  IL_0017:  sizeof     ""S""
  IL_001d:  conv.i8
  IL_001e:  mul.ovf
  IL_001f:  conv.i
  IL_0020:  add.ovf.un
  IL_0021:  ldc.i4.4
  IL_0022:  conv.i8
  IL_0023:  sizeof     ""S""
  IL_0029:  conv.i8
  IL_002a:  mul.ovf
  IL_002b:  conv.i
  IL_002c:  add.ovf.un
  IL_002d:  ldc.i4.5
  IL_002e:  conv.i8
  IL_002f:  sizeof     ""S""
  IL_0035:  conv.ovf.u8
  IL_0036:  mul.ovf.un
  IL_0037:  conv.u
  IL_0038:  add.ovf.un
  IL_0039:  pop
  IL_003a:  ret
}
");
        }

        [Fact]
        public void NumericAdditionUnchecked()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        unchecked
        {
            S s = new S();
            S* p = &s;
            p = p + 2;
            p = p + 3u;
            p = p + 4l;
            p = p + 5ul;
        }
    }
}
";

            // Dev10 has conv.i after IL_000d and conv.i8 in place of conv.u8 at IL_0017.
            CompileAndVerify(text, options: TestOptions.UnsafeDll).VerifyIL("S.Main", @"
{
  // Code size       59 (0x3b)
  .maxstack  3
  .locals init (S V_0) //s
  IL_0000:  ldloca.s   V_0
  IL_0002:  initobj    ""S""
  IL_0008:  ldloca.s   V_0
  IL_000a:  conv.u
  IL_000b:  ldc.i4.2
  IL_000c:  conv.i
  IL_000d:  sizeof     ""S""
  IL_0013:  mul
  IL_0014:  add
  IL_0015:  ldc.i4.3
  IL_0016:  conv.u8
  IL_0017:  sizeof     ""S""
  IL_001d:  conv.i8
  IL_001e:  mul
  IL_001f:  conv.i
  IL_0020:  add
  IL_0021:  ldc.i4.4
  IL_0022:  conv.i8
  IL_0023:  sizeof     ""S""
  IL_0029:  conv.i8
  IL_002a:  mul
  IL_002b:  conv.i
  IL_002c:  add
  IL_002d:  pop
  IL_002e:  ldc.i4.5
  IL_002f:  conv.i8
  IL_0030:  sizeof     ""S""
  IL_0036:  conv.i8
  IL_0037:  mul
  IL_0038:  conv.u
  IL_0039:  pop
  IL_003a:  ret
}
");
        }

        [Fact]
        public void NumericSubtractionChecked()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        checked
        {
            S s = new S();
            S* p = &s;
            p = p - 2;
            p = p - 3u;
            p = p - 4l;
            p = p - 5ul;
        }
    }
}
";

            // Dev10 has conv.i after IL_000d and conv.i8 in place of conv.u8 at IL_0017.
            CompileAndVerify(text, options: TestOptions.UnsafeDll).VerifyIL("S.Main", @"
{
  // Code size       59 (0x3b)
  .maxstack  3
  .locals init (S V_0) //s
  IL_0000:  ldloca.s   V_0
  IL_0002:  initobj    ""S""
  IL_0008:  ldloca.s   V_0
  IL_000a:  conv.u
  IL_000b:  ldc.i4.2
  IL_000c:  conv.i
  IL_000d:  sizeof     ""S""
  IL_0013:  mul.ovf
  IL_0014:  sub.ovf.un
  IL_0015:  ldc.i4.3
  IL_0016:  conv.u8
  IL_0017:  sizeof     ""S""
  IL_001d:  conv.i8
  IL_001e:  mul.ovf
  IL_001f:  conv.i
  IL_0020:  sub.ovf.un
  IL_0021:  ldc.i4.4
  IL_0022:  conv.i8
  IL_0023:  sizeof     ""S""
  IL_0029:  conv.i8
  IL_002a:  mul.ovf
  IL_002b:  conv.i
  IL_002c:  sub.ovf.un
  IL_002d:  ldc.i4.5
  IL_002e:  conv.i8
  IL_002f:  sizeof     ""S""
  IL_0035:  conv.ovf.u8
  IL_0036:  mul.ovf.un
  IL_0037:  conv.u
  IL_0038:  sub.ovf.un
  IL_0039:  pop
  IL_003a:  ret
}
");
        }

        [Fact]
        public void NumericSubtractionUnchecked()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        unchecked
        {
            S s = new S();
            S* p = &s;
            p = p - 2;
            p = p - 3u;
            p = p - 4l;
            p = p - 5ul;
        }
    }
}
";

            // Dev10 has conv.i after IL_000d and conv.i8 in place of conv.u8 at IL_0017.
            CompileAndVerify(text, options: TestOptions.UnsafeDll).VerifyIL("S.Main", @"
{
  // Code size       59 (0x3b)
  .maxstack  3
  .locals init (S V_0) //s
  IL_0000:  ldloca.s   V_0
  IL_0002:  initobj    ""S""
  IL_0008:  ldloca.s   V_0
  IL_000a:  conv.u
  IL_000b:  ldc.i4.2
  IL_000c:  conv.i
  IL_000d:  sizeof     ""S""
  IL_0013:  mul
  IL_0014:  sub
  IL_0015:  ldc.i4.3
  IL_0016:  conv.u8
  IL_0017:  sizeof     ""S""
  IL_001d:  conv.i8
  IL_001e:  mul
  IL_001f:  conv.i
  IL_0020:  sub
  IL_0021:  ldc.i4.4
  IL_0022:  conv.i8
  IL_0023:  sizeof     ""S""
  IL_0029:  conv.i8
  IL_002a:  mul
  IL_002b:  conv.i
  IL_002c:  sub
  IL_002d:  pop
  IL_002e:  ldc.i4.5
  IL_002f:  conv.i8
  IL_0030:  sizeof     ""S""
  IL_0036:  conv.i8
  IL_0037:  mul
  IL_0038:  conv.u
  IL_0039:  pop
  IL_003a:  ret
}
");
        }

        [WorkItem(546750)]
        [Fact]
        public void NumericAdditionUnchecked_SizeOne()
        {
            var text = @"
using System;

unsafe class C
{
    void Test(int i, uint u, long l, ulong ul)
    {
        unchecked
        {
            byte b = 3;
            byte* p = &b;
            p = p + 2;
            p = p + 3u;
            p = p + 4l;
            p = p + 5ul;
            p = p + i;
            p = p + u;
            p = p + l;
            p = p + ul;
        }
    }
}
";
            // NOTE: even when not optimized.
            // NOTE: additional conversions applied to constants of type int and uint.
            CompileAndVerify(text, options: TestOptions.UnsafeDll.WithOptimizations(false)).VerifyIL("C.Test", @"
{
  // Code size       47 (0x2f)
  .maxstack  2
  .locals init (byte V_0, //b
  byte* V_1) //p
  IL_0000:  ldc.i4.3
  IL_0001:  stloc.0
  IL_0002:  ldloca.s   V_0
  IL_0004:  conv.u
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  ldc.i4.2
  IL_0008:  add
  IL_0009:  stloc.1
  IL_000a:  ldloc.1
  IL_000b:  ldc.i4.3
  IL_000c:  add
  IL_000d:  stloc.1
  IL_000e:  ldloc.1
  IL_000f:  ldc.i4.4
  IL_0010:  conv.i8
  IL_0011:  conv.i
  IL_0012:  add
  IL_0013:  stloc.1
  IL_0014:  ldloc.1
  IL_0015:  ldc.i4.5
  IL_0016:  conv.i8
  IL_0017:  conv.u
  IL_0018:  add
  IL_0019:  stloc.1
  IL_001a:  ldloc.1
  IL_001b:  ldarg.1
  IL_001c:  add
  IL_001d:  stloc.1
  IL_001e:  ldloc.1
  IL_001f:  ldarg.2
  IL_0020:  conv.u
  IL_0021:  add
  IL_0022:  stloc.1
  IL_0023:  ldloc.1
  IL_0024:  ldarg.3
  IL_0025:  conv.i
  IL_0026:  add
  IL_0027:  stloc.1
  IL_0028:  ldloc.1
  IL_0029:  ldarg.s    V_4
  IL_002b:  conv.u
  IL_002c:  add
  IL_002d:  stloc.1
  IL_002e:  ret
}
");
        }

        [WorkItem(546750)]
        [Fact]
        public void NumericAdditionChecked_SizeOne()
        {
            var text = @"
using System;

unsafe class C
{
    void Test(int i, uint u, long l, ulong ul)
    {
        checked
        {
            byte b = 3;
            byte* p = &b;
            p = p + 2;
            p = p + 3u;
            p = p + 4l;
            p = p + 5ul;
            p = p + i;
            p = p + u;
            p = p + l;
            p = p + ul;
        }
    }
}
";
            // NOTE: even when not optimized.
            // NOTE: additional conversions applied to constants of type int and uint.
            // NOTE: identical to unchecked except "add" becomes "add.ovf.un".
            CompileAndVerify(text, options: TestOptions.UnsafeDll.WithOptimizations(false)).VerifyIL("C.Test", @"
{
  // Code size       47 (0x2f)
  .maxstack  2
  .locals init (byte V_0, //b
  byte* V_1) //p
  IL_0000:  ldc.i4.3
  IL_0001:  stloc.0
  IL_0002:  ldloca.s   V_0
  IL_0004:  conv.u
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  ldc.i4.2
  IL_0008:  add.ovf.un
  IL_0009:  stloc.1
  IL_000a:  ldloc.1
  IL_000b:  ldc.i4.3
  IL_000c:  add.ovf.un
  IL_000d:  stloc.1
  IL_000e:  ldloc.1
  IL_000f:  ldc.i4.4
  IL_0010:  conv.i8
  IL_0011:  conv.i
  IL_0012:  add.ovf.un
  IL_0013:  stloc.1
  IL_0014:  ldloc.1
  IL_0015:  ldc.i4.5
  IL_0016:  conv.i8
  IL_0017:  conv.u
  IL_0018:  add.ovf.un
  IL_0019:  stloc.1
  IL_001a:  ldloc.1
  IL_001b:  ldarg.1
  IL_001c:  add.ovf.un
  IL_001d:  stloc.1
  IL_001e:  ldloc.1
  IL_001f:  ldarg.2
  IL_0020:  conv.u
  IL_0021:  add.ovf.un
  IL_0022:  stloc.1
  IL_0023:  ldloc.1
  IL_0024:  ldarg.3
  IL_0025:  conv.i
  IL_0026:  add.ovf.un
  IL_0027:  stloc.1
  IL_0028:  ldloc.1
  IL_0029:  ldarg.s    V_4
  IL_002b:  conv.u
  IL_002c:  add.ovf.un
  IL_002d:  stloc.1
  IL_002e:  ret
}
");
        }

        [Fact]
        public void Increment()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        S* p = (S*)0;
        checked
        {
            p++;
        }
        checked
        {
            ++p;
        }
        unchecked
        {
            p++;
        }
        unchecked
        {
            ++p;
        }
        Console.WriteLine((int)p);
    }
}
";

            CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "4").VerifyIL("S.Main", @"
{
  // Code size       65 (0x41)
  .maxstack  2
  .locals init (S* V_0, //p
      S* V_1)
  IL_0000:  nop
  IL_0001:  ldc.i4.0
  IL_0002:  conv.i
  IL_0003:  stloc.0
  IL_0004:  nop
  IL_0005:  ldloc.0
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  sizeof     ""S""
  IL_000e:  add.ovf.un
  IL_000f:  stloc.0
  IL_0010:  nop
  IL_0011:  nop
  IL_0012:  ldloc.0
  IL_0013:  sizeof     ""S""
  IL_0019:  add.ovf.un
  IL_001a:  stloc.1
  IL_001b:  ldloc.1
  IL_001c:  stloc.0
  IL_001d:  nop
  IL_001e:  nop
  IL_001f:  ldloc.0
  IL_0020:  stloc.1
  IL_0021:  ldloc.1
  IL_0022:  sizeof     ""S""
  IL_0028:  add
  IL_0029:  stloc.0
  IL_002a:  nop
  IL_002b:  nop
  IL_002c:  ldloc.0
  IL_002d:  sizeof     ""S""
  IL_0033:  add
  IL_0034:  stloc.1
  IL_0035:  ldloc.1
  IL_0036:  stloc.0
  IL_0037:  nop
  IL_0038:  ldloc.0
  IL_0039:  conv.i4
  IL_003a:  call       ""void System.Console.WriteLine(int)""
  IL_003f:  nop
  IL_0040:  ret
}
");
        }

        [Fact]
        public void Decrement()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        S* p = (S*)8;
        checked
        {
            p--;
        }
        checked
        {
            --p;
        }
        unchecked
        {
            p--;
        }
        unchecked
        {
            --p;
        }
        Console.WriteLine((int)p);
    }
}
";

            CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "4").VerifyIL("S.Main", @"
{
  // Code size       65 (0x41)
  .maxstack  2
  .locals init (S* V_0, //p
      S* V_1)
  IL_0000:  nop
  IL_0001:  ldc.i4.8
  IL_0002:  conv.i
  IL_0003:  stloc.0
  IL_0004:  nop
  IL_0005:  ldloc.0
  IL_0006:  stloc.1
  IL_0007:  ldloc.1
  IL_0008:  sizeof     ""S""
  IL_000e:  sub.ovf.un
  IL_000f:  stloc.0
  IL_0010:  nop
  IL_0011:  nop
  IL_0012:  ldloc.0
  IL_0013:  sizeof     ""S""
  IL_0019:  sub.ovf.un
  IL_001a:  stloc.1
  IL_001b:  ldloc.1
  IL_001c:  stloc.0
  IL_001d:  nop
  IL_001e:  nop
  IL_001f:  ldloc.0
  IL_0020:  stloc.1
  IL_0021:  ldloc.1
  IL_0022:  sizeof     ""S""
  IL_0028:  sub
  IL_0029:  stloc.0
  IL_002a:  nop
  IL_002b:  nop
  IL_002c:  ldloc.0
  IL_002d:  sizeof     ""S""
  IL_0033:  sub
  IL_0034:  stloc.1
  IL_0035:  ldloc.1
  IL_0036:  stloc.0
  IL_0037:  nop
  IL_0038:  ldloc.0
  IL_0039:  conv.i4
  IL_003a:  call       ""void System.Console.WriteLine(int)""
  IL_003f:  nop
  IL_0040:  ret
}
");
        }

        [Fact]
        public void IncrementProperty()
        {
            var text = @"
using System;

unsafe struct S
{
    S* P { get; set; }
    S* this[int x] { get { return P; } set { P = value; } }

    static void Main()
    {
        S s = new S();
        s.P++;
        --s[GetIndex()];
        Console.Write((int)s.P);
    }

    static int GetIndex()
    {
        Console.Write(""I"");
        return 1;
    }
}
";

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "I0").VerifyIL("S.Main", @"
{
  // Code size       74 (0x4a)
  .maxstack  3
  .locals init (S V_0, //s
  S* V_1,
  int V_2)
  IL_0000:  ldloca.s   V_0
  IL_0002:  initobj    ""S""
  IL_0008:  ldloca.s   V_0
  IL_000a:  dup
  IL_000b:  call       ""S* S.P.get""
  IL_0010:  stloc.1
  IL_0011:  ldloc.1
  IL_0012:  sizeof     ""S""
  IL_0018:  add
  IL_0019:  call       ""void S.P.set""
  IL_001e:  ldloca.s   V_0
  IL_0020:  call       ""int S.GetIndex()""
  IL_0025:  stloc.2
  IL_0026:  dup
  IL_0027:  ldloc.2
  IL_0028:  call       ""S* S.this[int].get""
  IL_002d:  sizeof     ""S""
  IL_0033:  sub
  IL_0034:  stloc.1
  IL_0035:  ldloc.2
  IL_0036:  ldloc.1
  IL_0037:  call       ""void S.this[int].set""
  IL_003c:  ldloca.s   V_0
  IL_003e:  call       ""S* S.P.get""
  IL_0043:  conv.i4
  IL_0044:  call       ""void System.Console.Write(int)""
  IL_0049:  ret
}
");
        }

        [Fact]
        public void CompoundAssignment()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        S* p = (S*)8;
        checked
        {
            p += 1;
            p += 2U;
            p -= 1L;
            p -= 2UL;
        }
        unchecked
        {
            p += 1;
            p += 2U;
            p -= 1L;
            p -= 2UL;
        }
        Console.WriteLine((int)p);
    }
}
";

            CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "8").VerifyIL("S.Main", @"
{
  // Code size      109 (0x6d)
  .maxstack  3
  .locals init (S* V_0) //p
  IL_0000:  nop
  IL_0001:  ldc.i4.8
  IL_0002:  conv.i
  IL_0003:  stloc.0
  IL_0004:  nop
  IL_0005:  ldloc.0
  IL_0006:  sizeof     ""S""
  IL_000c:  add.ovf.un
  IL_000d:  stloc.0
  IL_000e:  ldloc.0
  IL_000f:  ldc.i4.2
  IL_0010:  conv.u8
  IL_0011:  sizeof     ""S""
  IL_0017:  conv.i8
  IL_0018:  mul.ovf
  IL_0019:  conv.i
  IL_001a:  add.ovf.un
  IL_001b:  stloc.0
  IL_001c:  ldloc.0
  IL_001d:  sizeof     ""S""
  IL_0023:  sub.ovf.un
  IL_0024:  stloc.0
  IL_0025:  ldloc.0
  IL_0026:  ldc.i4.2
  IL_0027:  conv.i8
  IL_0028:  sizeof     ""S""
  IL_002e:  conv.ovf.u8
  IL_002f:  mul.ovf.un
  IL_0030:  conv.u
  IL_0031:  sub.ovf.un
  IL_0032:  stloc.0
  IL_0033:  nop
  IL_0034:  nop
  IL_0035:  ldloc.0
  IL_0036:  sizeof     ""S""
  IL_003c:  add
  IL_003d:  stloc.0
  IL_003e:  ldloc.0
  IL_003f:  ldc.i4.2
  IL_0040:  conv.u8
  IL_0041:  sizeof     ""S""
  IL_0047:  conv.i8
  IL_0048:  mul
  IL_0049:  conv.i
  IL_004a:  add
  IL_004b:  stloc.0
  IL_004c:  ldloc.0
  IL_004d:  sizeof     ""S""
  IL_0053:  sub
  IL_0054:  stloc.0
  IL_0055:  ldloc.0
  IL_0056:  ldc.i4.2
  IL_0057:  conv.i8
  IL_0058:  sizeof     ""S""
  IL_005e:  conv.i8
  IL_005f:  mul
  IL_0060:  conv.u
  IL_0061:  sub
  IL_0062:  stloc.0
  IL_0063:  nop
  IL_0064:  ldloc.0
  IL_0065:  conv.i4
  IL_0066:  call       ""void System.Console.WriteLine(int)""
  IL_006b:  nop
  IL_006c:  ret
}
");
        }

        [Fact]
        public void CompoundAssignProperty()
        {
            var text = @"
using System;

unsafe struct S
{
    S* P { get; set; }
    S* this[int x] { get { return P; } set { P = value; } }

    static void Main()
    {
        S s = new S();
        s.P += 3;
        s[GetIndex()] -= 2;
        Console.Write((int)s.P);
    }

    static int GetIndex()
    {
        Console.Write(""I"");
        return 1;
    }
}
";

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"I4";
            }
            else
            {
                expectedOutput = @"I8";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput).VerifyIL("S.Main", @"
{
  // Code size       78 (0x4e)
  .maxstack  5
  .locals init (S V_0, //s
  S& V_1,
  int V_2)
  IL_0000:  ldloca.s   V_0
  IL_0002:  initobj    ""S""
  IL_0008:  ldloca.s   V_0
  IL_000a:  dup
  IL_000b:  call       ""S* S.P.get""
  IL_0010:  ldc.i4.3
  IL_0011:  conv.i
  IL_0012:  sizeof     ""S""
  IL_0018:  mul
  IL_0019:  add
  IL_001a:  call       ""void S.P.set""
  IL_001f:  ldloca.s   V_0
  IL_0021:  stloc.1
  IL_0022:  call       ""int S.GetIndex()""
  IL_0027:  stloc.2
  IL_0028:  ldloc.1
  IL_0029:  ldloc.2
  IL_002a:  ldloc.1
  IL_002b:  ldloc.2
  IL_002c:  call       ""S* S.this[int].get""
  IL_0031:  ldc.i4.2
  IL_0032:  conv.i
  IL_0033:  sizeof     ""S""
  IL_0039:  mul
  IL_003a:  sub
  IL_003b:  call       ""void S.this[int].set""
  IL_0040:  ldloca.s   V_0
  IL_0042:  call       ""S* S.P.get""
  IL_0047:  conv.i4
  IL_0048:  call       ""void System.Console.Write(int)""
  IL_004d:  ret
}
");
        }

        [Fact]
        public void PointerSubtraction_EmptyStruct()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        S* p = (S*)8;
        S* q = (S*)4;
        checked
        {
            Console.Write(p - q);
        }
        unchecked
        {
            Console.Write(p - q);
        }
    }
}
";

            // NOTE: don't use checked subtraction or division in either case (matches dev10).
            CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "44").VerifyIL("S.Main", @"
{
  // Code size       46 (0x2e)
  .maxstack  2
  .locals init (S* V_0, //p
  S* V_1) //q
  IL_0000:  nop
  IL_0001:  ldc.i4.8
  IL_0002:  conv.i
  IL_0003:  stloc.0
  IL_0004:  ldc.i4.4
  IL_0005:  conv.i
  IL_0006:  stloc.1
  IL_0007:  nop
  IL_0008:  ldloc.0
  IL_0009:  ldloc.1
  IL_000a:  sub
  IL_000b:  sizeof     ""S""
  IL_0011:  div
  IL_0012:  conv.i8
  IL_0013:  call       ""void System.Console.Write(long)""
  IL_0018:  nop
  IL_0019:  nop
  IL_001a:  nop
  IL_001b:  ldloc.0
  IL_001c:  ldloc.1
  IL_001d:  sub
  IL_001e:  sizeof     ""S""
  IL_0024:  div
  IL_0025:  conv.i8
  IL_0026:  call       ""void System.Console.Write(long)""
  IL_002b:  nop
  IL_002c:  nop
  IL_002d:  ret
}
");
        }

        [Fact]
        public void PointerSubtraction_NonEmptyStruct()
        {
            var text = @"
using System;

unsafe struct S
{
    int x; //non-empty struct

    static void Main()
    {
        S* p = (S*)8;
        S* q = (S*)4;
        checked
        {
            Console.Write(p - q);
        }
        unchecked
        {
            Console.Write(p - q);
        }
    }
}
";

            // NOTE: don't use checked subtraction or division in either case (matches dev10).
            CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "11").VerifyIL("S.Main", @"
{
  // Code size       46 (0x2e)
  .maxstack  2
  .locals init (S* V_0, //p
      S* V_1) //q
  IL_0000:  nop
  IL_0001:  ldc.i4.8
  IL_0002:  conv.i
  IL_0003:  stloc.0
  IL_0004:  ldc.i4.4
  IL_0005:  conv.i
  IL_0006:  stloc.1
  IL_0007:  nop
  IL_0008:  ldloc.0
  IL_0009:  ldloc.1
  IL_000a:  sub
  IL_000b:  sizeof     ""S""
  IL_0011:  div
  IL_0012:  conv.i8
  IL_0013:  call       ""void System.Console.Write(long)""
  IL_0018:  nop
  IL_0019:  nop
  IL_001a:  nop
  IL_001b:  ldloc.0
  IL_001c:  ldloc.1
  IL_001d:  sub
  IL_001e:  sizeof     ""S""
  IL_0024:  div
  IL_0025:  conv.i8
  IL_0026:  call       ""void System.Console.Write(long)""
  IL_002b:  nop
  IL_002c:  nop
  IL_002d:  ret
}
");
        }

        [Fact]
        public void PointerSubtraction_ConstantSize()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        int* p = (int*)8; //size is known at compile-time
        int* q = (int*)4;
        checked
        {
            Console.Write(p - q);
        }
        unchecked
        {
            Console.Write(p - q);
        }
    }
}
";

            // NOTE: don't use checked subtraction or division in either case (matches dev10).
            CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "11").VerifyIL("S.Main", @"
{
  // Code size       36 (0x24)
  .maxstack  2
  .locals init (int* V_0, //p
      int* V_1) //q
  IL_0000:  nop
  IL_0001:  ldc.i4.8
  IL_0002:  conv.i
  IL_0003:  stloc.0
  IL_0004:  ldc.i4.4
  IL_0005:  conv.i
  IL_0006:  stloc.1
  IL_0007:  nop
  IL_0008:  ldloc.0
  IL_0009:  ldloc.1
  IL_000a:  sub
  IL_000b:  ldc.i4.4
  IL_000c:  div
  IL_000d:  conv.i8
  IL_000e:  call       ""void System.Console.Write(long)""
  IL_0013:  nop
  IL_0014:  nop
  IL_0015:  nop
  IL_0016:  ldloc.0
  IL_0017:  ldloc.1
  IL_0018:  sub
  IL_0019:  ldc.i4.4
  IL_001a:  div
  IL_001b:  conv.i8
  IL_001c:  call       ""void System.Console.Write(long)""
  IL_0021:  nop
  IL_0022:  nop
  IL_0023:  ret
}
");
        }

        [Fact]
        public void PointerSubtraction_IntegerDivision()
        {
            var text = @"
using System;

unsafe struct S
{
    int x; //size = 4

    static void Main()
    {
        S* p1 = (S*)7; //size is known at compile-time
        S* p2 = (S*)9; //size is known at compile-time
        S* q = (S*)4;
        checked
        {
            Console.Write(p1 - q);
        }
        unchecked
        {
            Console.Write(p2 - q);
        }
    }
}
";

            // NOTE: don't use checked subtraction or division in either case (matches dev10).
            CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "01").VerifyIL("S.Main", @"
{
  // Code size       50 (0x32)
  .maxstack  2
  .locals init (S* V_0, //p1
      S* V_1, //p2
      S* V_2) //q
  IL_0000:  nop
  IL_0001:  ldc.i4.7
  IL_0002:  conv.i
  IL_0003:  stloc.0
  IL_0004:  ldc.i4.s   9
  IL_0006:  conv.i
  IL_0007:  stloc.1
  IL_0008:  ldc.i4.4
  IL_0009:  conv.i
  IL_000a:  stloc.2
  IL_000b:  nop
  IL_000c:  ldloc.0
  IL_000d:  ldloc.2
  IL_000e:  sub
  IL_000f:  sizeof     ""S""
  IL_0015:  div
  IL_0016:  conv.i8
  IL_0017:  call       ""void System.Console.Write(long)""
  IL_001c:  nop
  IL_001d:  nop
  IL_001e:  nop
  IL_001f:  ldloc.1
  IL_0020:  ldloc.2
  IL_0021:  sub
  IL_0022:  sizeof     ""S""
  IL_0028:  div
  IL_0029:  conv.i8
  IL_002a:  call       ""void System.Console.Write(long)""
  IL_002f:  nop
  IL_0030:  nop
  IL_0031:  ret
}
");
        }

        [WorkItem(544155)]
        [Fact]
        public void SubtractPointerTypes()
        {
            var text = @"
using System;

class PointerArithmetic
{
    static unsafe void Main()
    {
        short ia1 = 10;
        short* ptr = &ia1;
        short* newPtr;
        newPtr = ptr - 2;        

        Console.WriteLine((int)(ptr - newPtr));
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "2");
        }



        #endregion Pointer arithmetic tests

        #region Checked pointer arithmetic overflow tests

        // 0 - operation name (e.g. "Add")
        // 1 - pointed at type name (e.g. "S")
        // 2 - operator (e.g. "+")
        // 3 - checked/unchecked
        private const string CheckedNumericHelperTemplate = @"
unsafe static class Helper
{{
    public static void {0}Int({1}* p, int num, string description)
    {{
        {3}
        {{
            try
            {{
                p = p {2} num;
                Console.WriteLine(""{0}Int: No exception at {{0}} (value = {{1}})"",
                    description, (ulong)p);
            }}
            catch (OverflowException)
            {{
                Console.WriteLine(""{0}Int: Exception at {{0}}"", description);
            }}
        }}
    }}

    public static void {0}UInt({1}* p, uint num, string description)
    {{
        {3}
        {{
            try
            {{
                p = p {2} num;
                Console.WriteLine(""{0}UInt: No exception at {{0}} (value = {{1}})"",
                    description, (ulong)p);
            }}
            catch (OverflowException)
            {{
                Console.WriteLine(""{0}UInt: Exception at {{0}}"", description);
            }}
        }}
    }}

    public static void {0}Long({1}* p, long num, string description)
    {{
        {3}
        {{
            try
            {{
                p = p {2} num;
                Console.WriteLine(""{0}Long: No exception at {{0}} (value = {{1}})"",
                    description, (ulong)p);
            }}
            catch (OverflowException)
            {{
                Console.WriteLine(""{0}Long: Exception at {{0}}"", description);
            }}
        }}
    }}

    public static void {0}ULong({1}* p, ulong num, string description)
    {{
        {3}
        {{
            try
            {{
                p = p {2} num;
                Console.WriteLine(""{0}ULong: No exception at {{0}} (value = {{1}})"",
                    description, (ulong)p);
            }}
            catch (OverflowException)
            {{
                Console.WriteLine(""{0}ULong: Exception at {{0}}"", description);
            }}
        }}
    }}
}}
";

        private const string SizedStructs = @"
//sizeof SXX is 2 ^ XX
struct S00 { }
struct S01 { S00 a, b; }
struct S02 { S01 a, b; }
struct S03 { S02 a, b; }
struct S04 { S03 a, b; }
struct S05 { S04 a, b; }
struct S06 { S05 a, b; }
struct S07 { S06 a, b; }
struct S08 { S07 a, b; }
struct S09 { S08 a, b; }
struct S10 { S09 a, b; }
struct S11 { S10 a, b; }
struct S12 { S11 a, b; }
struct S13 { S12 a, b; }
struct S14 { S13 a, b; }
struct S15 { S14 a, b; }
struct S16 { S15 a, b; }
struct S17 { S16 a, b; }
struct S18 { S17 a, b; }
struct S19 { S18 a, b; }
struct S20 { S19 a, b; }
struct S21 { S20 a, b; }
struct S22 { S21 a, b; }
struct S23 { S22 a, b; }
struct S24 { S23 a, b; }
struct S25 { S24 a, b; }
struct S26 { S25 a, b; }
struct S27 { S26 a, b; }
//struct S28 { S27 a, b; } //Can't load type
//struct S29 { S28 a, b; } //Can't load type
//struct S30 { S29 a, b; } //Can't load type
//struct S31 { S30 a, b; } //Can't load type
";

        // 0 - pointed-at type
        private const string PositiveNumericAdditionCasesTemplate = @"
            Helper.AddInt(({0}*)0, int.MaxValue, ""0 + int.MaxValue"");
            Helper.AddInt(({0}*)1, int.MaxValue, ""1 + int.MaxValue"");
            Helper.AddInt(({0}*)int.MaxValue, 0, ""int.MaxValue + 0"");
            Helper.AddInt(({0}*)int.MaxValue, 1, ""int.MaxValue + 1"");

            //Helper.AddInt(({0}*)0, uint.MaxValue, ""0 + uint.MaxValue"");
            //Helper.AddInt(({0}*)1, uint.MaxValue, ""1 + uint.MaxValue"");
            Helper.AddInt(({0}*)uint.MaxValue, 0, ""uint.MaxValue + 0"");
            Helper.AddInt(({0}*)uint.MaxValue, 1, ""uint.MaxValue + 1"");

            //Helper.AddInt(({0}*)0, long.MaxValue, ""0 + long.MaxValue"");
            //Helper.AddInt(({0}*)1, long.MaxValue, ""1 + long.MaxValue"");
            //Helper.AddInt(({0}*)long.MaxValue, 0, ""long.MaxValue + 0"");
            //Helper.AddInt(({0}*)long.MaxValue, 1, ""long.MaxValue + 1"");

            //Helper.AddInt(({0}*)0, ulong.MaxValue, ""0 + ulong.MaxValue"");
            //Helper.AddInt(({0}*)1, ulong.MaxValue, ""1 + ulong.MaxValue"");
            //Helper.AddInt(({0}*)ulong.MaxValue, 0, ""ulong.MaxValue + 0"");
            //Helper.AddInt(({0}*)ulong.MaxValue, 1, ""ulong.MaxValue + 1"");

            Console.WriteLine();

            Helper.AddUInt(({0}*)0, int.MaxValue, ""0 + int.MaxValue"");
            Helper.AddUInt(({0}*)1, int.MaxValue, ""1 + int.MaxValue"");
            Helper.AddUInt(({0}*)int.MaxValue, 0, ""int.MaxValue + 0"");
            Helper.AddUInt(({0}*)int.MaxValue, 1, ""int.MaxValue + 1"");

            Helper.AddUInt(({0}*)0, uint.MaxValue, ""0 + uint.MaxValue"");
            Helper.AddUInt(({0}*)1, uint.MaxValue, ""1 + uint.MaxValue"");
            Helper.AddUInt(({0}*)uint.MaxValue, 0, ""uint.MaxValue + 0"");
            Helper.AddUInt(({0}*)uint.MaxValue, 1, ""uint.MaxValue + 1"");

            //Helper.AddUInt(({0}*)0, long.MaxValue, ""0 + long.MaxValue"");
            //Helper.AddUInt(({0}*)1, long.MaxValue, ""1 + long.MaxValue"");
            //Helper.AddUInt(({0}*)long.MaxValue, 0, ""long.MaxValue + 0"");
            //Helper.AddUInt(({0}*)long.MaxValue, 1, ""long.MaxValue + 1"");

            //Helper.AddUInt(({0}*)0, ulong.MaxValue, ""0 + ulong.MaxValue"");
            //Helper.AddUInt(({0}*)1, ulong.MaxValue, ""1 + ulong.MaxValue"");
            //Helper.AddUInt(({0}*)ulong.MaxValue, 0, ""ulong.MaxValue + 0"");
            //Helper.AddUInt(({0}*)ulong.MaxValue, 1, ""ulong.MaxValue + 1"");

            Console.WriteLine();

            Helper.AddLong(({0}*)0, int.MaxValue, ""0 + int.MaxValue"");
            Helper.AddLong(({0}*)1, int.MaxValue, ""1 + int.MaxValue"");
            Helper.AddLong(({0}*)int.MaxValue, 0, ""int.MaxValue + 0"");
            Helper.AddLong(({0}*)int.MaxValue, 1, ""int.MaxValue + 1"");

            Helper.AddLong(({0}*)0, uint.MaxValue, ""0 + uint.MaxValue"");
            Helper.AddLong(({0}*)1, uint.MaxValue, ""1 + uint.MaxValue"");
            Helper.AddLong(({0}*)uint.MaxValue, 0, ""uint.MaxValue + 0"");
            Helper.AddLong(({0}*)uint.MaxValue, 1, ""uint.MaxValue + 1"");

            Helper.AddLong(({0}*)0, long.MaxValue, ""0 + long.MaxValue"");
            Helper.AddLong(({0}*)1, long.MaxValue, ""1 + long.MaxValue"");
            //Helper.AddLong(({0}*)long.MaxValue, 0, ""long.MaxValue + 0"");
            //Helper.AddLong(({0}*)long.MaxValue, 1, ""long.MaxValue + 1"");

            //Helper.AddLong(({0}*)0, ulong.MaxValue, ""0 + ulong.MaxValue"");
            //Helper.AddLong(({0}*)1, ulong.MaxValue, ""1 + ulong.MaxValue"");
            //Helper.AddLong(({0}*)ulong.MaxValue, 0, ""ulong.MaxValue + 0"");
            //Helper.AddLong(({0}*)ulong.MaxValue, 1, ""ulong.MaxValue + 1"");

            Console.WriteLine();

            Helper.AddULong(({0}*)0, int.MaxValue, ""0 + int.MaxValue"");
            Helper.AddULong(({0}*)1, int.MaxValue, ""1 + int.MaxValue"");
            Helper.AddULong(({0}*)int.MaxValue, 0, ""int.MaxValue + 0"");
            Helper.AddULong(({0}*)int.MaxValue, 1, ""int.MaxValue + 1"");

            Helper.AddULong(({0}*)0, uint.MaxValue, ""0 + uint.MaxValue"");
            Helper.AddULong(({0}*)1, uint.MaxValue, ""1 + uint.MaxValue"");
            Helper.AddULong(({0}*)uint.MaxValue, 0, ""uint.MaxValue + 0"");
            Helper.AddULong(({0}*)uint.MaxValue, 1, ""uint.MaxValue + 1"");

            Helper.AddULong(({0}*)0, long.MaxValue, ""0 + long.MaxValue"");
            Helper.AddULong(({0}*)1, long.MaxValue, ""1 + long.MaxValue"");
            //Helper.AddULong(({0}*)long.MaxValue, 0, ""long.MaxValue + 0"");
            //Helper.AddULong(({0}*)long.MaxValue, 1, ""long.MaxValue + 1"");

            Helper.AddULong(({0}*)0, ulong.MaxValue, ""0 + ulong.MaxValue"");
            Helper.AddULong(({0}*)1, ulong.MaxValue, ""1 + ulong.MaxValue"");
            //Helper.AddULong(({0}*)ulong.MaxValue, 0, ""ulong.MaxValue + 0"");
            //Helper.AddULong(({0}*)ulong.MaxValue, 1, ""ulong.MaxValue + 1"");
";

        // 0 - pointed-at type
        private const string NegativeNumericAdditionCasesTemplate = @"
            Helper.AddInt(({0}*)0, -1, ""0 + (-1)"");
            Helper.AddInt(({0}*)0, int.MinValue, ""0 + int.MinValue"");
            //Helper.AddInt(({0}*)0, long.MinValue, ""0 + long.MinValue"");

            Console.WriteLine();

            Helper.AddLong(({0}*)0, -1, ""0 + (-1)"");
            Helper.AddLong(({0}*)0, int.MinValue, ""0 + int.MinValue"");
            Helper.AddLong(({0}*)0, long.MinValue, ""0 + long.MinValue"");
";

        // 0 - pointed-at type
        private const string PositiveNumericSubtractionCasesTemplate = @"
            Helper.SubInt(({0}*)0, 1, ""0 - 1"");
            Helper.SubInt(({0}*)0, int.MaxValue, ""0 - int.MaxValue"");
            //Helper.SubInt(({0}*)0, uint.MaxValue, ""0 - uint.MaxValue"");
            //Helper.SubInt(({0}*)0, long.MaxValue, ""0 - long.MaxValue"");
            //Helper.SubInt(({0}*)0, ulong.MaxValue, ""0 - ulong.MaxValue"");

            Console.WriteLine();

            Helper.SubUInt(({0}*)0, 1, ""0 - 1"");
            Helper.SubUInt(({0}*)0, int.MaxValue, ""0 - int.MaxValue"");
            Helper.SubUInt(({0}*)0, uint.MaxValue, ""0 - uint.MaxValue"");
            //Helper.SubUInt(({0}*)0, long.MaxValue, ""0 - long.MaxValue"");
            //Helper.SubUInt(({0}*)0, ulong.MaxValue, ""0 - ulong.MaxValue"");

            Console.WriteLine();

            Helper.SubLong(({0}*)0, 1, ""0 - 1"");
            Helper.SubLong(({0}*)0, int.MaxValue, ""0 - int.MaxValue"");
            Helper.SubLong(({0}*)0, uint.MaxValue, ""0 - uint.MaxValue"");
            Helper.SubLong(({0}*)0, long.MaxValue, ""0 - long.MaxValue"");
            //Helper.SubLong(({0}*)0, ulong.MaxValue, ""0 - ulong.MaxValue"");

            Console.WriteLine();

            Helper.SubULong(({0}*)0, 1, ""0 - 1"");
            Helper.SubULong(({0}*)0, int.MaxValue, ""0 - int.MaxValue"");
            Helper.SubULong(({0}*)0, uint.MaxValue, ""0 - uint.MaxValue"");
            Helper.SubULong(({0}*)0, long.MaxValue, ""0 - long.MaxValue"");
            Helper.SubULong(({0}*)0, ulong.MaxValue, ""0 - ulong.MaxValue"");
";

        // 0 - pointed-at type
        private const string NegativeNumericSubtractionCasesTemplate = @"
            Helper.SubInt(({0}*)0, -1, ""0 - -1"");
            Helper.SubInt(({0}*)0, int.MinValue, ""0 - int.MinValue"");
            Helper.SubInt(({0}*)0, -1 * int.MaxValue, ""0 - -int.MaxValue"");

            Console.WriteLine();

            Helper.SubLong(({0}*)0, -1L, ""0 - -1"");
            Helper.SubLong(({0}*)0, int.MinValue, ""0 - int.MinValue"");
            Helper.SubLong(({0}*)0, long.MinValue, ""0 - long.MinValue"");
            Helper.SubLong(({0}*)0, -1L * int.MaxValue, ""0 - -int.MaxValue"");
            Helper.SubLong(({0}*)0, -1L * uint.MaxValue, ""0 - -uint.MaxValue"");
            Helper.SubLong(({0}*)0, -1L * long.MaxValue, ""0 - -long.MaxValue"");
            Helper.SubLong(({0}*)0, -1L * long.MaxValue, ""0 - -ulong.MaxValue"");
            Helper.SubLong(({0}*)0, -1L * int.MinValue, ""0 - -int.MinValue"");
            //Helper.SubLong(({0}*)0, -1L * long.MinValue, ""0 - -long.MinValue"");
";

        private static string MakeNumericOverflowTest(string casesTemplate, string pointedAtType, string operationName, string @operator, string checkedness)
        {
            const string mainClassTemplate = @"
using System;

unsafe class C
{{
    static void Main()
    {{
        {0}
        {{
{1}
        }}
    }}
}}

{2}

{3}
";
            return string.Format(mainClassTemplate,
                checkedness,
                string.Format(casesTemplate, pointedAtType),
                string.Format(CheckedNumericHelperTemplate, operationName, pointedAtType, @operator, checkedness),
                SizedStructs);
        }

        // Positive numbers, size = 1
        [Fact]
        public void CheckedNumericAdditionOverflow1()
        {
            var text = MakeNumericOverflowTest(PositiveNumericAdditionCasesTemplate, "S00", "Add", "+", "checked");
            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
AddInt: No exception at 0 + int.MaxValue (value = 2147483647)
AddInt: No exception at 1 + int.MaxValue (value = 2147483648)
AddInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddInt: No exception at int.MaxValue + 1 (value = 2147483648)
AddInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddInt: Exception at uint.MaxValue + 1

AddUInt: No exception at 0 + int.MaxValue (value = 2147483647)
AddUInt: No exception at 1 + int.MaxValue (value = 2147483648)
AddUInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddUInt: No exception at int.MaxValue + 1 (value = 2147483648)
AddUInt: No exception at 0 + uint.MaxValue (value = 4294967295)
AddUInt: Exception at 1 + uint.MaxValue
AddUInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddUInt: Exception at uint.MaxValue + 1

AddLong: No exception at 0 + int.MaxValue (value = 2147483647)
AddLong: No exception at 1 + int.MaxValue (value = 2147483648)
AddLong: No exception at int.MaxValue + 0 (value = 2147483647)
AddLong: No exception at int.MaxValue + 1 (value = 2147483648)
AddLong: No exception at 0 + uint.MaxValue (value = 4294967295)
AddLong: Exception at 1 + uint.MaxValue
AddLong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddLong: Exception at uint.MaxValue + 1
AddLong: No exception at 0 + long.MaxValue (value = 4294967295)
AddLong: Exception at 1 + long.MaxValue

AddULong: No exception at 0 + int.MaxValue (value = 2147483647)
AddULong: No exception at 1 + int.MaxValue (value = 2147483648)
AddULong: No exception at int.MaxValue + 0 (value = 2147483647)
AddULong: No exception at int.MaxValue + 1 (value = 2147483648)
AddULong: No exception at 0 + uint.MaxValue (value = 4294967295)
AddULong: Exception at 1 + uint.MaxValue
AddULong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddULong: Exception at uint.MaxValue + 1
AddULong: No exception at 0 + long.MaxValue (value = 4294967295)
AddULong: Exception at 1 + long.MaxValue
AddULong: No exception at 0 + ulong.MaxValue (value = 4294967295)
AddULong: Exception at 1 + ulong.MaxValue
";
            }
            else
            {
                expectedOutput = @"
AddInt: No exception at 0 + int.MaxValue (value = 2147483647)
AddInt: No exception at 1 + int.MaxValue (value = 2147483648)
AddInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddInt: No exception at int.MaxValue + 1 (value = 2147483648)
AddInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddInt: No exception at uint.MaxValue + 1 (value = 4294967296)

AddUInt: No exception at 0 + int.MaxValue (value = 2147483647)
AddUInt: No exception at 1 + int.MaxValue (value = 2147483648)
AddUInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddUInt: No exception at int.MaxValue + 1 (value = 2147483648)
AddUInt: No exception at 0 + uint.MaxValue (value = 4294967295)
AddUInt: No exception at 1 + uint.MaxValue (value = 4294967296)
AddUInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddUInt: No exception at uint.MaxValue + 1 (value = 4294967296)

AddLong: No exception at 0 + int.MaxValue (value = 2147483647)
AddLong: No exception at 1 + int.MaxValue (value = 2147483648)
AddLong: No exception at int.MaxValue + 0 (value = 2147483647)
AddLong: No exception at int.MaxValue + 1 (value = 2147483648)
AddLong: No exception at 0 + uint.MaxValue (value = 4294967295)
AddLong: No exception at 1 + uint.MaxValue (value = 4294967296)
AddLong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddLong: No exception at uint.MaxValue + 1 (value = 4294967296)
AddLong: No exception at 0 + long.MaxValue (value = 9223372036854775807)
AddLong: No exception at 1 + long.MaxValue (value = 9223372036854775808)

AddULong: No exception at 0 + int.MaxValue (value = 2147483647)
AddULong: No exception at 1 + int.MaxValue (value = 2147483648)
AddULong: No exception at int.MaxValue + 0 (value = 2147483647)
AddULong: No exception at int.MaxValue + 1 (value = 2147483648)
AddULong: No exception at 0 + uint.MaxValue (value = 4294967295)
AddULong: No exception at 1 + uint.MaxValue (value = 4294967296)
AddULong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddULong: No exception at uint.MaxValue + 1 (value = 4294967296)
AddULong: No exception at 0 + long.MaxValue (value = 9223372036854775807)
AddULong: No exception at 1 + long.MaxValue (value = 9223372036854775808)
AddULong: No exception at 0 + ulong.MaxValue (value = 18446744073709551615)
AddULong: Exception at 1 + ulong.MaxValue
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Positive numbers, size = 4
        [Fact]
        public void CheckedNumericAdditionOverflow2()
        {
            var text = MakeNumericOverflowTest(PositiveNumericAdditionCasesTemplate, "S02", "Add", "+", "checked");

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
AddInt: Exception at 0 + int.MaxValue
AddInt: Exception at 1 + int.MaxValue
AddInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddInt: No exception at int.MaxValue + 1 (value = 2147483651)
AddInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddInt: Exception at uint.MaxValue + 1

AddUInt: No exception at 0 + int.MaxValue (value = 4294967292)
AddUInt: No exception at 1 + int.MaxValue (value = 4294967293)
AddUInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddUInt: No exception at int.MaxValue + 1 (value = 2147483651)
AddUInt: No exception at 0 + uint.MaxValue (value = 4294967292)
AddUInt: No exception at 1 + uint.MaxValue (value = 4294967293)
AddUInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddUInt: Exception at uint.MaxValue + 1

AddLong: No exception at 0 + int.MaxValue (value = 4294967292)
AddLong: No exception at 1 + int.MaxValue (value = 4294967293)
AddLong: No exception at int.MaxValue + 0 (value = 2147483647)
AddLong: No exception at int.MaxValue + 1 (value = 2147483651)
AddLong: No exception at 0 + uint.MaxValue (value = 4294967292)
AddLong: No exception at 1 + uint.MaxValue (value = 4294967293)
AddLong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddLong: Exception at uint.MaxValue + 1
AddLong: Exception at 0 + long.MaxValue
AddLong: Exception at 1 + long.MaxValue

AddULong: No exception at 0 + int.MaxValue (value = 4294967292)
AddULong: No exception at 1 + int.MaxValue (value = 4294967293)
AddULong: No exception at int.MaxValue + 0 (value = 2147483647)
AddULong: No exception at int.MaxValue + 1 (value = 2147483651)
AddULong: No exception at 0 + uint.MaxValue (value = 4294967292)
AddULong: No exception at 1 + uint.MaxValue (value = 4294967293)
AddULong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddULong: Exception at uint.MaxValue + 1
AddULong: Exception at 0 + long.MaxValue
AddULong: Exception at 1 + long.MaxValue
AddULong: Exception at 0 + ulong.MaxValue
AddULong: Exception at 1 + ulong.MaxValue
";
            }
            else
            {
                expectedOutput = @"
AddInt: No exception at 0 + int.MaxValue (value = 8589934588)
AddInt: No exception at 1 + int.MaxValue (value = 8589934589)
AddInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddInt: No exception at int.MaxValue + 1 (value = 2147483651)
AddInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddInt: No exception at uint.MaxValue + 1 (value = 4294967299)

AddUInt: No exception at 0 + int.MaxValue (value = 8589934588)
AddUInt: No exception at 1 + int.MaxValue (value = 8589934589)
AddUInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddUInt: No exception at int.MaxValue + 1 (value = 2147483651)
AddUInt: No exception at 0 + uint.MaxValue (value = 17179869180)
AddUInt: No exception at 1 + uint.MaxValue (value = 17179869181)
AddUInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddUInt: No exception at uint.MaxValue + 1 (value = 4294967299)

AddLong: No exception at 0 + int.MaxValue (value = 8589934588)
AddLong: No exception at 1 + int.MaxValue (value = 8589934589)
AddLong: No exception at int.MaxValue + 0 (value = 2147483647)
AddLong: No exception at int.MaxValue + 1 (value = 2147483651)
AddLong: No exception at 0 + uint.MaxValue (value = 17179869180)
AddLong: No exception at 1 + uint.MaxValue (value = 17179869181)
AddLong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddLong: No exception at uint.MaxValue + 1 (value = 4294967299)
AddLong: Exception at 0 + long.MaxValue
AddLong: Exception at 1 + long.MaxValue

AddULong: No exception at 0 + int.MaxValue (value = 8589934588)
AddULong: No exception at 1 + int.MaxValue (value = 8589934589)
AddULong: No exception at int.MaxValue + 0 (value = 2147483647)
AddULong: No exception at int.MaxValue + 1 (value = 2147483651)
AddULong: No exception at 0 + uint.MaxValue (value = 17179869180)
AddULong: No exception at 1 + uint.MaxValue (value = 17179869181)
AddULong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddULong: No exception at uint.MaxValue + 1 (value = 4294967299)
AddULong: Exception at 0 + long.MaxValue
AddULong: Exception at 1 + long.MaxValue
AddULong: Exception at 0 + ulong.MaxValue
AddULong: Exception at 1 + ulong.MaxValue
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Negative numbers, size = 1
        [Fact]
        public void CheckedNumericAdditionOverflow3()
        {
            var text = MakeNumericOverflowTest(NegativeNumericAdditionCasesTemplate, "S00", "Add", "+", "checked");

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
AddInt: No exception at 0 + (-1) (value = 4294967295)
AddInt: No exception at 0 + int.MinValue (value = 2147483648)

AddLong: No exception at 0 + (-1) (value = 4294967295)
AddLong: No exception at 0 + int.MinValue (value = 2147483648)
AddLong: No exception at 0 + long.MinValue (value = 0)
";
            }
            else
            {
                expectedOutput = @"
AddInt: No exception at 0 + (-1) (value = 18446744073709551615)
AddInt: No exception at 0 + int.MinValue (value = 18446744071562067968)

AddLong: No exception at 0 + (-1) (value = 18446744073709551615)
AddLong: No exception at 0 + int.MinValue (value = 18446744071562067968)
AddLong: No exception at 0 + long.MinValue (value = 9223372036854775808)
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Negative numbers, size = 4
        [Fact]
        public void CheckedNumericAdditionOverflow4()
        {
            var text = MakeNumericOverflowTest(NegativeNumericAdditionCasesTemplate, "S02", "Add", "+", "checked");

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
AddInt: No exception at 0 + (-1) (value = 4294967292)
AddInt: Exception at 0 + int.MinValue

AddLong: No exception at 0 + (-1) (value = 4294967292)
AddLong: No exception at 0 + int.MinValue (value = 0)
AddLong: Exception at 0 + long.MinValue
";
            }
            else
            {
                expectedOutput = @"
AddInt: No exception at 0 + (-1) (value = 18446744073709551612)
AddInt: No exception at 0 + int.MinValue (value = 18446744065119617024)

AddLong: No exception at 0 + (-1) (value = 18446744073709551612)
AddLong: No exception at 0 + int.MinValue (value = 18446744065119617024)
AddLong: Exception at 0 + long.MinValue
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Positive numbers, size = 1
        [Fact]
        public void CheckedNumericSubtractionOverflow1()
        {
            var text = MakeNumericOverflowTest(PositiveNumericSubtractionCasesTemplate, "S00", "Sub", "-", "checked");

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"
SubInt: Exception at 0 - 1
SubInt: Exception at 0 - int.MaxValue

SubUInt: Exception at 0 - 1
SubUInt: Exception at 0 - int.MaxValue
SubUInt: Exception at 0 - uint.MaxValue

SubLong: Exception at 0 - 1
SubLong: Exception at 0 - int.MaxValue
SubLong: Exception at 0 - uint.MaxValue
SubLong: Exception at 0 - long.MaxValue

SubULong: Exception at 0 - 1
SubULong: Exception at 0 - int.MaxValue
SubULong: Exception at 0 - uint.MaxValue
SubULong: Exception at 0 - long.MaxValue
SubULong: Exception at 0 - ulong.MaxValue
");
        }

        // Positive numbers, size = 4
        [Fact]
        public void CheckedNumericSubtractionOverflow2()
        {
            var text = MakeNumericOverflowTest(PositiveNumericSubtractionCasesTemplate, "S02", "Sub", "-", "checked");

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"
SubInt: Exception at 0 - 1
SubInt: Exception at 0 - int.MaxValue

SubUInt: Exception at 0 - 1
SubUInt: Exception at 0 - int.MaxValue
SubUInt: Exception at 0 - uint.MaxValue

SubLong: Exception at 0 - 1
SubLong: Exception at 0 - int.MaxValue
SubLong: Exception at 0 - uint.MaxValue
SubLong: Exception at 0 - long.MaxValue

SubULong: Exception at 0 - 1
SubULong: Exception at 0 - int.MaxValue
SubULong: Exception at 0 - uint.MaxValue
SubULong: Exception at 0 - long.MaxValue
SubULong: Exception at 0 - ulong.MaxValue
");
        }

        // Negative numbers, size = 1
        [Fact]
        public void CheckedNumericSubtractionOverflow3()
        {
            var text = MakeNumericOverflowTest(NegativeNumericSubtractionCasesTemplate, "S00", "Sub", "-", "checked");
            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
SubInt: Exception at 0 - -1
SubInt: Exception at 0 - int.MinValue
SubInt: Exception at 0 - -int.MaxValue

SubLong: Exception at 0 - -1
SubLong: Exception at 0 - int.MinValue
SubLong: No exception at 0 - long.MinValue (value = 0)
SubLong: Exception at 0 - -int.MaxValue
SubLong: Exception at 0 - -uint.MaxValue
SubLong: Exception at 0 - -long.MaxValue
SubLong: Exception at 0 - -ulong.MaxValue
SubLong: Exception at 0 - -int.MinValue
";
            }
            else
            {
                expectedOutput = @"
SubInt: Exception at 0 - -1
SubInt: Exception at 0 - int.MinValue
SubInt: Exception at 0 - -int.MaxValue

SubLong: Exception at 0 - -1
SubLong: Exception at 0 - int.MinValue
SubLong: Exception at 0 - long.MinValue
SubLong: Exception at 0 - -int.MaxValue
SubLong: Exception at 0 - -uint.MaxValue
SubLong: Exception at 0 - -long.MaxValue
SubLong: Exception at 0 - -ulong.MaxValue
SubLong: Exception at 0 - -int.MinValue
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Negative numbers, size = 4
        [Fact]
        public void CheckedNumericSubtractionOverflow4()
        {
            var text = MakeNumericOverflowTest(NegativeNumericSubtractionCasesTemplate, "S02", "Sub", "-", "checked");            
            
            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
SubInt: Exception at 0 - -1
SubInt: Exception at 0 - int.MinValue
SubInt: Exception at 0 - -int.MaxValue

SubLong: Exception at 0 - -1
SubLong: No exception at 0 - int.MinValue (value = 0)
SubLong: Exception at 0 - long.MinValue
SubLong: Exception at 0 - -int.MaxValue
SubLong: Exception at 0 - -uint.MaxValue
SubLong: Exception at 0 - -long.MaxValue
SubLong: Exception at 0 - -ulong.MaxValue
SubLong: No exception at 0 - -int.MinValue (value = 0)
";
            }
            else
            {
                expectedOutput = @"
SubInt: Exception at 0 - -1
SubInt: Exception at 0 - int.MinValue
SubInt: Exception at 0 - -int.MaxValue

SubLong: Exception at 0 - -1
SubLong: Exception at 0 - int.MinValue
SubLong: Exception at 0 - long.MinValue
SubLong: Exception at 0 - -int.MaxValue
SubLong: Exception at 0 - -uint.MaxValue
SubLong: Exception at 0 - -long.MaxValue
SubLong: Exception at 0 - -ulong.MaxValue
SubLong: Exception at 0 - -int.MinValue
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        [Fact]
        public void CheckedNumericSubtractionQuirk()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        checked
        {
            S* p;
            p = (S*)0 + (-1);
            System.Console.WriteLine(""No exception from addition"");
            try
            {
                p = (S*)0 - 1;
            }
            catch (OverflowException)
            {
                System.Console.WriteLine(""Exception from subtraction"");
            }
        }
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"
No exception from addition
Exception from subtraction
");
        }

        [Fact]
        public void CheckedNumericAdditionQuirk()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        checked
        {
            S* p;
            p = (S*)1 + int.MaxValue;
            System.Console.WriteLine(""No exception for pointer + int"");
            try
            {
                p = int.MaxValue + (S*)1;
            }
            catch (OverflowException)
            {
                System.Console.WriteLine(""Exception for int + pointer"");
            }
        }
    }
}
";
            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
No exception for pointer + int
Exception for int + pointer
";
            }
            else
            {
                expectedOutput = @"
No exception for pointer + int
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        [Fact]
        public void CheckedPointerSubtractionQuirk()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        S* p = (S*)uint.MinValue;
        S* q = (S*)uint.MaxValue;
        checked
        {
            Console.Write(p - q);
        }
        unchecked
        {
            Console.Write(p - q);
        }
    }
}
";
            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"11";
            }
            else
            {
                expectedOutput = @"-4294967295-4294967295";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        [Fact]
        public void CheckedPointerElementAccessQuirk()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        fixed (byte* p = new byte[2])
        {
            p[0] = 12;

            // Take a pointer to the second element of the array.
            byte* q = p + 1;

            // Compute the offset that will wrap around all the way to the preceding byte of memory.
            // We do this so that we can overflow, but still end up in valid memory.
            ulong offset = sizeof(IntPtr) == sizeof(int) ? uint.MaxValue : ulong.MaxValue;

            checked
            {
                Console.WriteLine(q[offset]);
                System.Console.WriteLine(""No exception for element access"");
                try
                {
                    Console.WriteLine(*(q + offset));
                }
                catch (OverflowException)
                {
                    System.Console.WriteLine(""Exception for add-then-dereference"");
                }
            }
        }
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"
12
No exception for element access
Exception for add-then-dereference
");
        }

        #endregion Checked pointer arithmetic overflow tests

        #region Unchecked pointer arithmetic overflow tests

        // Positive numbers, size = 1
        [Fact]
        public void UncheckedNumericAdditionOverflow1()
        {
            var text = MakeNumericOverflowTest(PositiveNumericAdditionCasesTemplate, "S00", "Add", "+", "unchecked");

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
AddInt: No exception at 0 + int.MaxValue (value = 2147483647)
AddInt: No exception at 1 + int.MaxValue (value = 2147483648)
AddInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddInt: No exception at int.MaxValue + 1 (value = 2147483648)
AddInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddInt: No exception at uint.MaxValue + 1 (value = 0)

AddUInt: No exception at 0 + int.MaxValue (value = 2147483647)
AddUInt: No exception at 1 + int.MaxValue (value = 2147483648)
AddUInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddUInt: No exception at int.MaxValue + 1 (value = 2147483648)
AddUInt: No exception at 0 + uint.MaxValue (value = 4294967295)
AddUInt: No exception at 1 + uint.MaxValue (value = 0)
AddUInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddUInt: No exception at uint.MaxValue + 1 (value = 0)

AddLong: No exception at 0 + int.MaxValue (value = 2147483647)
AddLong: No exception at 1 + int.MaxValue (value = 2147483648)
AddLong: No exception at int.MaxValue + 0 (value = 2147483647)
AddLong: No exception at int.MaxValue + 1 (value = 2147483648)
AddLong: No exception at 0 + uint.MaxValue (value = 4294967295)
AddLong: No exception at 1 + uint.MaxValue (value = 0)
AddLong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddLong: No exception at uint.MaxValue + 1 (value = 0)
AddLong: No exception at 0 + long.MaxValue (value = 4294967295)
AddLong: No exception at 1 + long.MaxValue (value = 0)

AddULong: No exception at 0 + int.MaxValue (value = 2147483647)
AddULong: No exception at 1 + int.MaxValue (value = 2147483648)
AddULong: No exception at int.MaxValue + 0 (value = 2147483647)
AddULong: No exception at int.MaxValue + 1 (value = 2147483648)
AddULong: No exception at 0 + uint.MaxValue (value = 4294967295)
AddULong: No exception at 1 + uint.MaxValue (value = 0)
AddULong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddULong: No exception at uint.MaxValue + 1 (value = 0)
AddULong: No exception at 0 + long.MaxValue (value = 4294967295)
AddULong: No exception at 1 + long.MaxValue (value = 0)
AddULong: No exception at 0 + ulong.MaxValue (value = 4294967295)
AddULong: No exception at 1 + ulong.MaxValue (value = 0)
";
            }
            else
            {
                expectedOutput = @"
AddInt: No exception at 0 + int.MaxValue (value = 2147483647)
AddInt: No exception at 1 + int.MaxValue (value = 2147483648)
AddInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddInt: No exception at int.MaxValue + 1 (value = 2147483648)
AddInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddInt: No exception at uint.MaxValue + 1 (value = 4294967296)

AddUInt: No exception at 0 + int.MaxValue (value = 2147483647)
AddUInt: No exception at 1 + int.MaxValue (value = 2147483648)
AddUInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddUInt: No exception at int.MaxValue + 1 (value = 2147483648)
AddUInt: No exception at 0 + uint.MaxValue (value = 4294967295)
AddUInt: No exception at 1 + uint.MaxValue (value = 4294967296)
AddUInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddUInt: No exception at uint.MaxValue + 1 (value = 4294967296)

AddLong: No exception at 0 + int.MaxValue (value = 2147483647)
AddLong: No exception at 1 + int.MaxValue (value = 2147483648)
AddLong: No exception at int.MaxValue + 0 (value = 2147483647)
AddLong: No exception at int.MaxValue + 1 (value = 2147483648)
AddLong: No exception at 0 + uint.MaxValue (value = 4294967295)
AddLong: No exception at 1 + uint.MaxValue (value = 4294967296)
AddLong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddLong: No exception at uint.MaxValue + 1 (value = 4294967296)
AddLong: No exception at 0 + long.MaxValue (value = 9223372036854775807)
AddLong: No exception at 1 + long.MaxValue (value = 9223372036854775808)

AddULong: No exception at 0 + int.MaxValue (value = 2147483647)
AddULong: No exception at 1 + int.MaxValue (value = 2147483648)
AddULong: No exception at int.MaxValue + 0 (value = 2147483647)
AddULong: No exception at int.MaxValue + 1 (value = 2147483648)
AddULong: No exception at 0 + uint.MaxValue (value = 4294967295)
AddULong: No exception at 1 + uint.MaxValue (value = 4294967296)
AddULong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddULong: No exception at uint.MaxValue + 1 (value = 4294967296)
AddULong: No exception at 0 + long.MaxValue (value = 9223372036854775807)
AddULong: No exception at 1 + long.MaxValue (value = 9223372036854775808)
AddULong: No exception at 0 + ulong.MaxValue (value = 18446744073709551615)
AddULong: No exception at 1 + ulong.MaxValue (value = 0)
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Positive numbers, size = 4
        [Fact]
        public void UncheckedNumericAdditionOverflow2()
        {
            var text = MakeNumericOverflowTest(PositiveNumericAdditionCasesTemplate, "S02", "Add", "+", "unchecked");
            
            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
AddInt: No exception at 0 + int.MaxValue (value = 4294967292)
AddInt: No exception at 1 + int.MaxValue (value = 4294967293)
AddInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddInt: No exception at int.MaxValue + 1 (value = 2147483651)
AddInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddInt: No exception at uint.MaxValue + 1 (value = 3)

AddUInt: No exception at 0 + int.MaxValue (value = 4294967292)
AddUInt: No exception at 1 + int.MaxValue (value = 4294967293)
AddUInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddUInt: No exception at int.MaxValue + 1 (value = 2147483651)
AddUInt: No exception at 0 + uint.MaxValue (value = 4294967292)
AddUInt: No exception at 1 + uint.MaxValue (value = 4294967293)
AddUInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddUInt: No exception at uint.MaxValue + 1 (value = 3)

AddLong: No exception at 0 + int.MaxValue (value = 4294967292)
AddLong: No exception at 1 + int.MaxValue (value = 4294967293)
AddLong: No exception at int.MaxValue + 0 (value = 2147483647)
AddLong: No exception at int.MaxValue + 1 (value = 2147483651)
AddLong: No exception at 0 + uint.MaxValue (value = 4294967292)
AddLong: No exception at 1 + uint.MaxValue (value = 4294967293)
AddLong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddLong: No exception at uint.MaxValue + 1 (value = 3)
AddLong: No exception at 0 + long.MaxValue (value = 4294967292)
AddLong: No exception at 1 + long.MaxValue (value = 4294967293)

AddULong: No exception at 0 + int.MaxValue (value = 4294967292)
AddULong: No exception at 1 + int.MaxValue (value = 4294967293)
AddULong: No exception at int.MaxValue + 0 (value = 2147483647)
AddULong: No exception at int.MaxValue + 1 (value = 2147483651)
AddULong: No exception at 0 + uint.MaxValue (value = 4294967292)
AddULong: No exception at 1 + uint.MaxValue (value = 4294967293)
AddULong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddULong: No exception at uint.MaxValue + 1 (value = 3)
AddULong: No exception at 0 + long.MaxValue (value = 4294967292)
AddULong: No exception at 1 + long.MaxValue (value = 4294967293)
AddULong: No exception at 0 + ulong.MaxValue (value = 4294967292)
AddULong: No exception at 1 + ulong.MaxValue (value = 4294967293)
";
            }
            else
            {
                expectedOutput = @"
AddInt: No exception at 0 + int.MaxValue (value = 8589934588)
AddInt: No exception at 1 + int.MaxValue (value = 8589934589)
AddInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddInt: No exception at int.MaxValue + 1 (value = 2147483651)
AddInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddInt: No exception at uint.MaxValue + 1 (value = 4294967299)

AddUInt: No exception at 0 + int.MaxValue (value = 8589934588)
AddUInt: No exception at 1 + int.MaxValue (value = 8589934589)
AddUInt: No exception at int.MaxValue + 0 (value = 2147483647)
AddUInt: No exception at int.MaxValue + 1 (value = 2147483651)
AddUInt: No exception at 0 + uint.MaxValue (value = 17179869180)
AddUInt: No exception at 1 + uint.MaxValue (value = 17179869181)
AddUInt: No exception at uint.MaxValue + 0 (value = 4294967295)
AddUInt: No exception at uint.MaxValue + 1 (value = 4294967299)

AddLong: No exception at 0 + int.MaxValue (value = 8589934588)
AddLong: No exception at 1 + int.MaxValue (value = 8589934589)
AddLong: No exception at int.MaxValue + 0 (value = 2147483647)
AddLong: No exception at int.MaxValue + 1 (value = 2147483651)
AddLong: No exception at 0 + uint.MaxValue (value = 17179869180)
AddLong: No exception at 1 + uint.MaxValue (value = 17179869181)
AddLong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddLong: No exception at uint.MaxValue + 1 (value = 4294967299)
AddLong: No exception at 0 + long.MaxValue (value = 18446744073709551612)
AddLong: No exception at 1 + long.MaxValue (value = 18446744073709551613)

AddULong: No exception at 0 + int.MaxValue (value = 8589934588)
AddULong: No exception at 1 + int.MaxValue (value = 8589934589)
AddULong: No exception at int.MaxValue + 0 (value = 2147483647)
AddULong: No exception at int.MaxValue + 1 (value = 2147483651)
AddULong: No exception at 0 + uint.MaxValue (value = 17179869180)
AddULong: No exception at 1 + uint.MaxValue (value = 17179869181)
AddULong: No exception at uint.MaxValue + 0 (value = 4294967295)
AddULong: No exception at uint.MaxValue + 1 (value = 4294967299)
AddULong: No exception at 0 + long.MaxValue (value = 18446744073709551612)
AddULong: No exception at 1 + long.MaxValue (value = 18446744073709551613)
AddULong: No exception at 0 + ulong.MaxValue (value = 18446744073709551612)
AddULong: No exception at 1 + ulong.MaxValue (value = 18446744073709551613)
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Negative numbers, size = 1
        [Fact]
        public void UncheckedNumericAdditionOverflow3()
        {
            var text = MakeNumericOverflowTest(NegativeNumericAdditionCasesTemplate, "S00", "Add", "+", "unchecked");

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
AddInt: No exception at 0 + (-1) (value = 4294967295)
AddInt: No exception at 0 + int.MinValue (value = 2147483648)

AddLong: No exception at 0 + (-1) (value = 4294967295)
AddLong: No exception at 0 + int.MinValue (value = 2147483648)
AddLong: No exception at 0 + long.MinValue (value = 0)
";
            }
            else
            {
                expectedOutput = @"
AddInt: No exception at 0 + (-1) (value = 18446744073709551615)
AddInt: No exception at 0 + int.MinValue (value = 18446744071562067968)

AddLong: No exception at 0 + (-1) (value = 18446744073709551615)
AddLong: No exception at 0 + int.MinValue (value = 18446744071562067968)
AddLong: No exception at 0 + long.MinValue (value = 9223372036854775808)
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Negative numbers, size = 4
        [Fact]
        public void UncheckedNumericAdditionOverflow4()
        {
            var text = MakeNumericOverflowTest(NegativeNumericAdditionCasesTemplate, "S02", "Add", "+", "unchecked");

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
AddInt: No exception at 0 + (-1) (value = 4294967292)
AddInt: No exception at 0 + int.MinValue (value = 0)

AddLong: No exception at 0 + (-1) (value = 4294967292)
AddLong: No exception at 0 + int.MinValue (value = 0)
AddLong: No exception at 0 + long.MinValue (value = 0)
";
            }
            else
            {
                expectedOutput = @"
AddInt: No exception at 0 + (-1) (value = 18446744073709551612)
AddInt: No exception at 0 + int.MinValue (value = 18446744065119617024)

AddLong: No exception at 0 + (-1) (value = 18446744073709551612)
AddLong: No exception at 0 + int.MinValue (value = 18446744065119617024)
AddLong: No exception at 0 + long.MinValue (value = 0)
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Positive numbers, size = 1
        [Fact]
        public void UncheckedNumericSubtractionOverflow1()
        {
            var text = MakeNumericOverflowTest(PositiveNumericSubtractionCasesTemplate, "S00", "Sub", "-", "unchecked");

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
SubInt: No exception at 0 - 1 (value = 4294967295)
SubInt: No exception at 0 - int.MaxValue (value = 2147483649)

SubUInt: No exception at 0 - 1 (value = 4294967295)
SubUInt: No exception at 0 - int.MaxValue (value = 2147483649)
SubUInt: No exception at 0 - uint.MaxValue (value = 1)

SubLong: No exception at 0 - 1 (value = 4294967295)
SubLong: No exception at 0 - int.MaxValue (value = 2147483649)
SubLong: No exception at 0 - uint.MaxValue (value = 1)
SubLong: No exception at 0 - long.MaxValue (value = 1)

SubULong: No exception at 0 - 1 (value = 4294967295)
SubULong: No exception at 0 - int.MaxValue (value = 2147483649)
SubULong: No exception at 0 - uint.MaxValue (value = 1)
SubULong: No exception at 0 - long.MaxValue (value = 1)
SubULong: No exception at 0 - ulong.MaxValue (value = 1)
";
            }
            else
            {
                expectedOutput = @"
SubInt: No exception at 0 - 1 (value = 18446744073709551615)
SubInt: No exception at 0 - int.MaxValue (value = 18446744071562067969)

SubUInt: No exception at 0 - 1 (value = 18446744073709551615)
SubUInt: No exception at 0 - int.MaxValue (value = 18446744071562067969)
SubUInt: No exception at 0 - uint.MaxValue (value = 18446744069414584321)

SubLong: No exception at 0 - 1 (value = 18446744073709551615)
SubLong: No exception at 0 - int.MaxValue (value = 18446744071562067969)
SubLong: No exception at 0 - uint.MaxValue (value = 18446744069414584321)
SubLong: No exception at 0 - long.MaxValue (value = 9223372036854775809)

SubULong: No exception at 0 - 1 (value = 18446744073709551615)
SubULong: No exception at 0 - int.MaxValue (value = 18446744071562067969)
SubULong: No exception at 0 - uint.MaxValue (value = 18446744069414584321)
SubULong: No exception at 0 - long.MaxValue (value = 9223372036854775809)
SubULong: No exception at 0 - ulong.MaxValue (value = 1)
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Positive numbers, size = 4
        [Fact]
        public void UncheckedNumericSubtractionOverflow2()
        {
            var text = MakeNumericOverflowTest(PositiveNumericSubtractionCasesTemplate, "S02", "Sub", "-", "unchecked");

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
SubInt: No exception at 0 - 1 (value = 4294967292)
SubInt: No exception at 0 - int.MaxValue (value = 4)

SubUInt: No exception at 0 - 1 (value = 4294967292)
SubUInt: No exception at 0 - int.MaxValue (value = 4)
SubUInt: No exception at 0 - uint.MaxValue (value = 4)

SubLong: No exception at 0 - 1 (value = 4294967292)
SubLong: No exception at 0 - int.MaxValue (value = 4)
SubLong: No exception at 0 - uint.MaxValue (value = 4)
SubLong: No exception at 0 - long.MaxValue (value = 4)

SubULong: No exception at 0 - 1 (value = 4294967292)
SubULong: No exception at 0 - int.MaxValue (value = 4)
SubULong: No exception at 0 - uint.MaxValue (value = 4)
SubULong: No exception at 0 - long.MaxValue (value = 4)
SubULong: No exception at 0 - ulong.MaxValue (value = 4)
";
            }
            else
            {
                expectedOutput = @"
SubInt: No exception at 0 - 1 (value = 18446744073709551612)
SubInt: No exception at 0 - int.MaxValue (value = 18446744065119617028)

SubUInt: No exception at 0 - 1 (value = 18446744073709551612)
SubUInt: No exception at 0 - int.MaxValue (value = 18446744065119617028)
SubUInt: No exception at 0 - uint.MaxValue (value = 18446744056529682436)

SubLong: No exception at 0 - 1 (value = 18446744073709551612)
SubLong: No exception at 0 - int.MaxValue (value = 18446744065119617028)
SubLong: No exception at 0 - uint.MaxValue (value = 18446744056529682436)
SubLong: No exception at 0 - long.MaxValue (value = 4)

SubULong: No exception at 0 - 1 (value = 18446744073709551612)
SubULong: No exception at 0 - int.MaxValue (value = 18446744065119617028)
SubULong: No exception at 0 - uint.MaxValue (value = 18446744056529682436)
SubULong: No exception at 0 - long.MaxValue (value = 4)
SubULong: No exception at 0 - ulong.MaxValue (value = 4)
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Negative numbers, size = 1
        [Fact]
        public void UncheckedNumericSubtractionOverflow3()
        {
            var text = MakeNumericOverflowTest(NegativeNumericSubtractionCasesTemplate, "S00", "Sub", "-", "unchecked");

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
SubInt: No exception at 0 - -1 (value = 1)
SubInt: No exception at 0 - int.MinValue (value = 2147483648)
SubInt: No exception at 0 - -int.MaxValue (value = 2147483647)

SubLong: No exception at 0 - -1 (value = 1)
SubLong: No exception at 0 - int.MinValue (value = 2147483648)
SubLong: No exception at 0 - long.MinValue (value = 0)
SubLong: No exception at 0 - -int.MaxValue (value = 2147483647)
SubLong: No exception at 0 - -uint.MaxValue (value = 4294967295)
SubLong: No exception at 0 - -long.MaxValue (value = 4294967295)
SubLong: No exception at 0 - -ulong.MaxValue (value = 4294967295)
SubLong: No exception at 0 - -int.MinValue (value = 2147483648)
";
            }
            else
            {
                expectedOutput = @"
SubInt: No exception at 0 - -1 (value = 1)
SubInt: No exception at 0 - int.MinValue (value = 2147483648)
SubInt: No exception at 0 - -int.MaxValue (value = 2147483647)

SubLong: No exception at 0 - -1 (value = 1)
SubLong: No exception at 0 - int.MinValue (value = 2147483648)
SubLong: No exception at 0 - long.MinValue (value = 9223372036854775808)
SubLong: No exception at 0 - -int.MaxValue (value = 2147483647)
SubLong: No exception at 0 - -uint.MaxValue (value = 4294967295)
SubLong: No exception at 0 - -long.MaxValue (value = 9223372036854775807)
SubLong: No exception at 0 - -ulong.MaxValue (value = 9223372036854775807)
SubLong: No exception at 0 - -int.MinValue (value = 18446744071562067968)
";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        // Negative numbers, size = 4
        [Fact]
        public void UncheckedNumericSubtractionOverflow4()
        {
            var text = MakeNumericOverflowTest(NegativeNumericSubtractionCasesTemplate, "S02", "Sub", "-", "unchecked");
            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
SubInt: No exception at 0 - -1 (value = 4)
SubInt: No exception at 0 - int.MinValue (value = 0)
SubInt: No exception at 0 - -int.MaxValue (value = 4294967292)

SubLong: No exception at 0 - -1 (value = 4)
SubLong: No exception at 0 - int.MinValue (value = 0)
SubLong: No exception at 0 - long.MinValue (value = 0)
SubLong: No exception at 0 - -int.MaxValue (value = 4294967292)
SubLong: No exception at 0 - -uint.MaxValue (value = 4294967292)
SubLong: No exception at 0 - -long.MaxValue (value = 4294967292)
SubLong: No exception at 0 - -ulong.MaxValue (value = 4294967292)
SubLong: No exception at 0 - -int.MinValue (value = 0)";
            }
            else
            {
                expectedOutput = @"
SubInt: No exception at 0 - -1 (value = 4)
SubInt: No exception at 0 - int.MinValue (value = 8589934592)
SubInt: No exception at 0 - -int.MaxValue (value = 8589934588)

SubLong: No exception at 0 - -1 (value = 4)
SubLong: No exception at 0 - int.MinValue (value = 8589934592)
SubLong: No exception at 0 - long.MinValue (value = 0)
SubLong: No exception at 0 - -int.MaxValue (value = 8589934588)
SubLong: No exception at 0 - -uint.MaxValue (value = 17179869180)
SubLong: No exception at 0 - -long.MaxValue (value = 18446744073709551612)
SubLong: No exception at 0 - -ulong.MaxValue (value = 18446744073709551612)
SubLong: No exception at 0 - -int.MinValue (value = 18446744065119617024)";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        #endregion Unchecked pointer arithmetic overflow tests

        #region Pointer comparison tests

        [Fact]
        public void PointerComparisonSameType()
        {
            var text = @"
using System;

unsafe struct S
{
    static void Main()
    {
        S* p = (S*)0;
        S* q = (S*)1;

        unchecked
        {
            Write(p == q);
            Write(p != q);
            Write(p <= q);
            Write(p >= q);
            Write(p < q);
            Write(p > q);
        }

        checked
        {
            Write(p == q);
            Write(p != q);
            Write(p <= q);
            Write(p >= q);
            Write(p < q);
            Write(p > q);
        }
    }

    static void Write(bool b)
    {
        Console.Write(b ? 1 : 0);
    }
}
";
            // NOTE: all comparisons unsigned.
            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "011010011010", emitPdb: true).VerifyIL("S.Main", @"
{
  // Code size      150 (0x96)
  .maxstack  2
  .locals init (S* V_0, //p
      S* V_1) //q
  IL_0000:  nop
  IL_0001:  ldc.i4.0
  IL_0002:  conv.i
  IL_0003:  stloc.0
  IL_0004:  ldc.i4.1
  IL_0005:  conv.i
  IL_0006:  stloc.1
  IL_0007:  nop
  IL_0008:  ldloc.0
  IL_0009:  ldloc.1
  IL_000a:  ceq
  IL_000c:  call       ""void S.Write(bool)""
  IL_0011:  nop
  IL_0012:  ldloc.0
  IL_0013:  ldloc.1
  IL_0014:  ceq
  IL_0016:  ldc.i4.0
  IL_0017:  ceq
  IL_0019:  call       ""void S.Write(bool)""
  IL_001e:  nop
  IL_001f:  ldloc.0
  IL_0020:  ldloc.1
  IL_0021:  cgt.un
  IL_0023:  ldc.i4.0
  IL_0024:  ceq
  IL_0026:  call       ""void S.Write(bool)""
  IL_002b:  nop
  IL_002c:  ldloc.0
  IL_002d:  ldloc.1
  IL_002e:  clt.un
  IL_0030:  ldc.i4.0
  IL_0031:  ceq
  IL_0033:  call       ""void S.Write(bool)""
  IL_0038:  nop
  IL_0039:  ldloc.0
  IL_003a:  ldloc.1
  IL_003b:  clt.un
  IL_003d:  call       ""void S.Write(bool)""
  IL_0042:  nop
  IL_0043:  ldloc.0
  IL_0044:  ldloc.1
  IL_0045:  cgt.un
  IL_0047:  call       ""void S.Write(bool)""
  IL_004c:  nop
  IL_004d:  nop
  IL_004e:  nop
  IL_004f:  ldloc.0
  IL_0050:  ldloc.1
  IL_0051:  ceq
  IL_0053:  call       ""void S.Write(bool)""
  IL_0058:  nop
  IL_0059:  ldloc.0
  IL_005a:  ldloc.1
  IL_005b:  ceq
  IL_005d:  ldc.i4.0
  IL_005e:  ceq
  IL_0060:  call       ""void S.Write(bool)""
  IL_0065:  nop
  IL_0066:  ldloc.0
  IL_0067:  ldloc.1
  IL_0068:  cgt.un
  IL_006a:  ldc.i4.0
  IL_006b:  ceq
  IL_006d:  call       ""void S.Write(bool)""
  IL_0072:  nop
  IL_0073:  ldloc.0
  IL_0074:  ldloc.1
  IL_0075:  clt.un
  IL_0077:  ldc.i4.0
  IL_0078:  ceq
  IL_007a:  call       ""void S.Write(bool)""
  IL_007f:  nop
  IL_0080:  ldloc.0
  IL_0081:  ldloc.1
  IL_0082:  clt.un
  IL_0084:  call       ""void S.Write(bool)""
  IL_0089:  nop
  IL_008a:  ldloc.0
  IL_008b:  ldloc.1
  IL_008c:  cgt.un
  IL_008e:  call       ""void S.Write(bool)""
  IL_0093:  nop
  IL_0094:  nop
  IL_0095:  ret
}
");
        }

        #endregion Pointer comparison tests

        #region stackalloc tests

        [Fact]
        public void SimpleStackAlloc()
        {
            var text = @"
unsafe class C
{
    void M()
    {
        int count = 1;
        checked
        {
            int* p = stackalloc int[2];
            char* q = stackalloc char[count];
        }
        unchecked
        {
            int* p = stackalloc int[2];
            char* q = stackalloc char[count];
        }
    }
}
";
            // NOTE: conversion is always unchecked, multiplication is always checked.
            CompileAndVerify(text, emitPdb: true, options: TestOptions.UnsafeDll).VerifyIL("C.M", @"
{
  // Code size       37 (0x25)
  .maxstack  2
  .locals init (int V_0, //count
      int* V_1, //p
      char* V_2, //q
      int* V_3, //p
      char* V_4) //q
  IL_0000:  nop
  IL_0001:  ldc.i4.1
  IL_0002:  stloc.0
  IL_0003:  nop
  IL_0004:  ldc.i4.2
  IL_0005:  conv.u
  IL_0006:  ldc.i4.4
  IL_0007:  mul.ovf.un
  IL_0008:  localloc
  IL_000a:  stloc.1
  IL_000b:  ldloc.0
  IL_000c:  conv.u
  IL_000d:  ldc.i4.2
  IL_000e:  mul.ovf.un
  IL_000f:  localloc
  IL_0011:  stloc.2
  IL_0012:  nop
  IL_0013:  nop
  IL_0014:  ldc.i4.2
  IL_0015:  conv.u
  IL_0016:  ldc.i4.4
  IL_0017:  mul.ovf.un
  IL_0018:  localloc
  IL_001a:  stloc.3
  IL_001b:  ldloc.0
  IL_001c:  conv.u
  IL_001d:  ldc.i4.2
  IL_001e:  mul.ovf.un
  IL_001f:  localloc
  IL_0021:  stloc.s    V_4
  IL_0023:  nop
  IL_0024:  ret
}
");
        }

        [Fact]
        public void StackAllocConversion()
        {
            var text = @"
unsafe class C
{
    void M()
    {
        void* p = stackalloc int[2];
        C q = stackalloc int[2];
    }

    public static implicit operator C(int* p)
    {
        return null;
    }
}
";
            CompileAndVerify(text, emitPdb: true, options: TestOptions.UnsafeDll).VerifyIL("C.M", @"
{
  // Code size       21 (0x15)
  .maxstack  2
  .locals init (void* V_0, //p
      C V_1) //q
  IL_0000:  nop
  IL_0001:  ldc.i4.2
  IL_0002:  conv.u
  IL_0003:  ldc.i4.4
  IL_0004:  mul.ovf.un
  IL_0005:  localloc
  IL_0007:  stloc.0
  IL_0008:  ldc.i4.2
  IL_0009:  conv.u
  IL_000a:  ldc.i4.4
  IL_000b:  mul.ovf.un
  IL_000c:  localloc
  IL_000e:  call       ""C C.op_Implicit(int*)""
  IL_0013:  stloc.1
  IL_0014:  ret
}
");
        }

        [Fact]
        public void StackAllocSpecExample() //Section 18.8
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        Console.WriteLine(IntToString(123));
        Console.WriteLine(IntToString(-456));
    }

	static string IntToString(int value) {
		int n = value >= 0? value: -value;
		unsafe {
			char* buffer = stackalloc char[16];
			char* p = buffer + 16;
			do {
				*--p = (char)(n % 10 + '0');
				n /= 10;
			} while (n != 0);
			if (value < 0) *--p = '-';
			return new string(p, 0, (int)(buffer + 16 - p));
		}
	}
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"123
-456
");
        }

        // See MethodToClassRewriter.VisitAssignmentOperator for an explanation.
        [Fact]
        public void StackAllocIntoHoistedLocal1()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main()
    {
        var p = stackalloc int[2];
        var q = stackalloc int[2];

        Action a = () =>
        {
            var r = stackalloc int[2];
            var s = stackalloc int[2];

            Action b = () =>
            {
                p = null; //capture p
                r = null; //capture r
            };
        };
    }
}
";
            var verifier = CompileAndVerify(text, emitPdb: true, options: TestOptions.UnsafeExe);

            // Note that the stackalloc for p is written into a temp *before* the receiver (i.e. "this")
            // for C.<>c__DisplayClass0.p is pushed onto the stack.
            verifier.VerifyIL("C.Main", @"
{
  // Code size       42 (0x2a)
  .maxstack  2
  .locals init (C.<>c__DisplayClass0 V_0, //CS$<>8__locals2
      int* V_1, //q
      System.Action V_2, //a
      int* V_3)
  IL_0000:  newobj     ""C.<>c__DisplayClass0..ctor()""
  IL_0005:  stloc.0
  IL_0006:  nop
  IL_0007:  ldc.i4.2
  IL_0008:  conv.u
  IL_0009:  ldc.i4.4
  IL_000a:  mul.ovf.un
  IL_000b:  localloc
  IL_000d:  stloc.3
  IL_000e:  ldloc.0
  IL_000f:  ldloc.3
  IL_0010:  stfld      ""int* C.<>c__DisplayClass0.p""
  IL_0015:  ldc.i4.2
  IL_0016:  conv.u
  IL_0017:  ldc.i4.4
  IL_0018:  mul.ovf.un
  IL_0019:  localloc
  IL_001b:  stloc.1
  IL_001c:  ldloc.0
  IL_001d:  ldftn      ""void C.<>c__DisplayClass0.<Main>b__3()""
  IL_0023:  newobj     ""System.Action..ctor(object, System.IntPtr)""
  IL_0028:  stloc.2
  IL_0029:  ret
}
");

            // Check that the same thing works inside a lambda.
            verifier.VerifyIL("C.<>c__DisplayClass0.<Main>b__3", @"
{
  // Code size       51 (0x33)
  .maxstack  2
  .locals init (C.<>c__DisplayClass1 V_0, //CS$<>8__locals4
  int* V_1, //s
  System.Action V_2, //b
  int* V_3)
  IL_0000:  newobj     ""C.<>c__DisplayClass1..ctor()""
  IL_0005:  stloc.0
  IL_0006:  ldloc.0
  IL_0007:  ldarg.0
  IL_0008:  stfld      ""C.<>c__DisplayClass0 C.<>c__DisplayClass1.CS$<>8__locals2""
  IL_000d:  nop
  IL_000e:  ldc.i4.2
  IL_000f:  conv.u
  IL_0010:  ldc.i4.4
  IL_0011:  mul.ovf.un
  IL_0012:  localloc
  IL_0014:  stloc.3
  IL_0015:  ldloc.0
  IL_0016:  ldloc.3
  IL_0017:  stfld      ""int* C.<>c__DisplayClass1.r""
  IL_001c:  ldc.i4.2
  IL_001d:  conv.u
  IL_001e:  ldc.i4.4
  IL_001f:  mul.ovf.un
  IL_0020:  localloc
  IL_0022:  stloc.1
  IL_0023:  ldloc.0
  IL_0024:  ldftn      ""void C.<>c__DisplayClass1.<Main>b__5()""
  IL_002a:  newobj     ""System.Action..ctor(object, System.IntPtr)""
  IL_002f:  stloc.2
  IL_0030:  br.s       IL_0032
  IL_0032:  ret
}
");
        }

        // See MethodToClassRewriter.VisitAssignmentOperator for an explanation.
        [Fact]
        public void StackAllocIntoHoistedLocal2()
        {
            // From native bug #59454 (in DevDiv collection)
            var text = @"
unsafe class T 
{ 
    delegate int D(); 

    static void Main() 
    { 
        int* v = stackalloc int[1]; 
        D d = delegate { return *v; }; 
        System.Console.WriteLine(d()); 
    } 
} 
";
            CompileAndVerify(text, emitPdb: true, options: TestOptions.UnsafeExe, expectedOutput: "0").VerifyIL("T.Main", @"
{
  // Code size       47 (0x2f)
  .maxstack  2
  .locals init (T.<>c__DisplayClass0 V_0, //CS$<>8__locals1
      T.D V_1, //d
      int* V_2)
  IL_0000:  newobj     ""T.<>c__DisplayClass0..ctor()""
  IL_0005:  stloc.0
  IL_0006:  nop
  IL_0007:  ldc.i4.1
  IL_0008:  conv.u
  IL_0009:  ldc.i4.4
  IL_000a:  mul.ovf.un
  IL_000b:  localloc
  IL_000d:  stloc.2
  IL_000e:  ldloc.0
  IL_000f:  ldloc.2
  IL_0010:  stfld      ""int* T.<>c__DisplayClass0.v""
  IL_0015:  ldloc.0
  IL_0016:  ldftn      ""int T.<>c__DisplayClass0.<Main>b__2()""
  IL_001c:  newobj     ""T.D..ctor(object, System.IntPtr)""
  IL_0021:  stloc.1
  IL_0022:  ldloc.1
  IL_0023:  callvirt   ""int T.D.Invoke()""
  IL_0028:  call       ""void System.Console.WriteLine(int)""
  IL_002d:  nop
  IL_002e:  ret
}
");
            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "0").VerifyIL("T.Main", @"
{
  // Code size       43 (0x2b)
  .maxstack  2
  .locals init (T.<>c__DisplayClass0 V_0, //CS$<>8__locals1
      int* V_1)
  IL_0000:  newobj     ""T.<>c__DisplayClass0..ctor()""
  IL_0005:  stloc.0
  IL_0006:  ldc.i4.1
  IL_0007:  conv.u
  IL_0008:  ldc.i4.4
  IL_0009:  mul.ovf.un
  IL_000a:  localloc
  IL_000c:  stloc.1
  IL_000d:  ldloc.0
  IL_000e:  ldloc.1
  IL_000f:  stfld      ""int* T.<>c__DisplayClass0.v""
  IL_0014:  ldloc.0
  IL_0015:  ldftn      ""int T.<>c__DisplayClass0.<Main>b__2()""
  IL_001b:  newobj     ""T.D..ctor(object, System.IntPtr)""
  IL_0020:  callvirt   ""int T.D.Invoke()""
  IL_0025:  call       ""void System.Console.WriteLine(int)""
  IL_002a:  ret
}
");
        }

        [Fact]
        public void CSLegacyStackallocUse32bitChecked()
        {
            // This is from C# Legacy test where it uses Perl script to call ildasm and check 'mul.ovf' emitted
            // $Roslyn\Main\LegacyTest\CSharp\Source\csharp\Source\Conformance\unsafecode\stackalloc\regr001.cs
            var text = @"// <Title>Should checked affect stackalloc?</Title>
// <Description>
// The lower level localloc MSIL instruction takes an unsigned native int as input; however the higher level 
// stackalloc uses only 32-bits. The example shows the operation overflowing the 32-bit multiply which leads to 
// a curious edge condition.
// If compile with /checked we insert a mul.ovf instruction, and this causes a system overflow exception at runtime.
// </Description>
// <RelatedBugs>VSW:489857</RelatedBugs>

using System;

public class C
{
    private static unsafe int Main()
    {
        Int64* intArray = stackalloc Int64[0x7fffffff];
        return 0;
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeDll);
            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       13 (0xd)
  .maxstack  2
  IL_0000:  ldc.i4     0x7fffffff
  IL_0005:  conv.u
  IL_0006:  ldc.i4.8
  IL_0007:  mul.ovf.un
  IL_0008:  localloc
  IL_000a:  pop
  IL_000b:  ldc.i4.0
  IL_000c:  ret
}
");
        }

        #endregion stackalloc tests

        #region Functional tests

        [Fact]
        public void BubbleSort()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main() 
    {
        BubbleSort();
        BubbleSort(1);
        BubbleSort(2, 1);
        BubbleSort(3, 1, 2);
        BubbleSort(3, 1, 4, 2);
    }

    static void BubbleSort(params int[] array)
    {
        if (array == null)
        {
            return;
        }

        fixed (int* begin = array)
        {
            BubbleSort(begin, end: begin + array.Length);
        }

        Console.WriteLine(string.Join("", "", array));
    }

    private static void BubbleSort(int* begin, int* end)
    {
        for (int* firstUnsorted = begin; firstUnsorted < end; firstUnsorted++)
        {
            for (int* current = firstUnsorted; current + 1 < end; current++)
            {
                if (current[0] > current[1])
                {
                    SwapWithNext(current);
                }
            }
        }
    }

    static void SwapWithNext(int* p)
    {
        int temp = *p;
        p[0] = p[1];
        p[1] = temp;
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"
1
1, 2
1, 2, 3
1, 2, 3, 4");
        }

        [Fact]
        public void BigStructs()
        {
            var text = @"
unsafe class C
{
    static void Main()
    {
        void* v;

        CheckOverflow(""(S15*)0 + sizeof(S15)"", () => v = checked((S15*)0 + sizeof(S15)));
        CheckOverflow(""(S15*)0 + sizeof(S16)"", () => v = checked((S15*)0 + sizeof(S16)));
        CheckOverflow(""(S16*)0 + sizeof(S15)"", () => v = checked((S16*)0 + sizeof(S15)));
    }

    static void CheckOverflow(string description, System.Action operation)
    {
        try
        {
            operation();
            System.Console.WriteLine(""No overflow from {0}"", description);
        }
        catch (System.OverflowException)
        {
            System.Console.WriteLine(""Overflow from {0}"", description);
        }
    }
}
" + SizedStructs;

            bool isx86 = (IntPtr.Size == 4);
            string expectedOutput;

            if (isx86)
            {
                expectedOutput = @"
No overflow from (S15*)0 + sizeof(S15)
Overflow from (S15*)0 + sizeof(S16)
Overflow from (S16*)0 + sizeof(S15)";
            }
            else
            {
                expectedOutput = @"
No overflow from (S15*)0 + sizeof(S15)
No overflow from (S15*)0 + sizeof(S16)
No overflow from (S16*)0 + sizeof(S15)";
            }

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: expectedOutput);
        }

        [Fact]
        public void LambdaConversion()
        {
            var text = @"
using System;

class Program
{
    static void Main()
    {
        Foo(x => { });
    }

    static void Foo(F1 f) { Console.WriteLine(1); }
    static void Foo(F2 f) { Console.WriteLine(2); }
}

unsafe delegate void F1(int* x);
delegate void F2(int x);
";

            CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"2");
        }

        [Fact]
        public void LocalVariableReuse()
        {
            var text = @"
unsafe class C
{
    int this[string s] { get { return 0; } set { } }

    void Test()
    {
        {
            this[""not pinned"".ToString()] += 2; //creates an unpinned string local (for the argument)
        }

        fixed (char* p = ""pinned"") //creates a pinned string local
        {
        }

        {
            this[""not pinned"".ToString()] += 2; //reuses the unpinned string local
        }

        fixed (char* p = ""pinned"") //reuses the pinned string local
        {
        }
    }
}
";
            // NOTE: one pinned string temp and one unpinned string temp.
            // That is, pinned temps are reused in by other pinned temps
            // but not by unpinned temps and vice versa.
            CompileAndVerify(text, options: TestOptions.UnsafeDll).VerifyIL("C.Test", @"
{
  // Code size      101 (0x65)
  .maxstack  4
  .locals init (string V_0,
  char* V_1, //p
  pinned string V_2,
  char* V_3) //p
  IL_0000:  ldstr      ""not pinned""
  IL_0005:  callvirt   ""string object.ToString()""
  IL_000a:  stloc.0
  IL_000b:  ldarg.0
  IL_000c:  ldloc.0
  IL_000d:  ldarg.0
  IL_000e:  ldloc.0
  IL_000f:  call       ""int C.this[string].get""
  IL_0014:  ldc.i4.2
  IL_0015:  add
  IL_0016:  call       ""void C.this[string].set""
  IL_001b:  ldstr      ""pinned""
  IL_0020:  stloc.2
  IL_0021:  ldloc.2
  IL_0022:  conv.i
  IL_0023:  stloc.1
  IL_0024:  ldloc.2
  IL_0025:  conv.i
  IL_0026:  brfalse.s  IL_0030
  IL_0028:  ldloc.1
  IL_0029:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_002e:  add
  IL_002f:  stloc.1
  IL_0030:  ldnull
  IL_0031:  stloc.2
  IL_0032:  ldstr      ""not pinned""
  IL_0037:  callvirt   ""string object.ToString()""
  IL_003c:  stloc.0
  IL_003d:  ldarg.0
  IL_003e:  ldloc.0
  IL_003f:  ldarg.0
  IL_0040:  ldloc.0
  IL_0041:  call       ""int C.this[string].get""
  IL_0046:  ldc.i4.2
  IL_0047:  add
  IL_0048:  call       ""void C.this[string].set""
  IL_004d:  ldstr      ""pinned""
  IL_0052:  stloc.2
  IL_0053:  ldloc.2
  IL_0054:  conv.i
  IL_0055:  stloc.3
  IL_0056:  ldloc.2
  IL_0057:  conv.i
  IL_0058:  brfalse.s  IL_0062
  IL_005a:  ldloc.3
  IL_005b:  call       ""int System.Runtime.CompilerServices.RuntimeHelpers.OffsetToStringData.get""
  IL_0060:  add
  IL_0061:  stloc.3
  IL_0062:  ldnull
  IL_0063:  stloc.2
  IL_0064:  ret
}");
        }

        [WorkItem(544229)]
        [Fact]
        public void UnsafeTypeAsAttributeArgument()
        {
            var template = @"
using System;
 
namespace System
{{
    class Int32 {{ }}
}}
 
 
[A(Type = typeof({0}))]
class A : Attribute
{{
    public Type Type;
    static void Main()
    {{
        var a = (A)typeof(A).GetCustomAttributes(false)[0];
        Console.WriteLine(a.Type == typeof({0}));
    }}
}}
";
            CompileAndVerify(string.Format(template, "int"), options: TestOptions.UnsafeExe, emitOptions: EmitOptions.RefEmitBug, expectedOutput: @"True");
            CompileAndVerify(string.Format(template, "int*"), options: TestOptions.UnsafeExe, emitOptions: EmitOptions.RefEmitBug, expectedOutput: @"True");
            CompileAndVerify(string.Format(template, "int**"), options: TestOptions.UnsafeExe, emitOptions: EmitOptions.RefEmitBug, expectedOutput: @"True");
            CompileAndVerify(string.Format(template, "int[]"), options: TestOptions.UnsafeExe, emitOptions: EmitOptions.RefEmitBug, expectedOutput: @"True");
            CompileAndVerify(string.Format(template, "int[][]"), options: TestOptions.UnsafeExe, emitOptions: EmitOptions.RefEmitBug, expectedOutput: @"True");
            CompileAndVerify(string.Format(template, "int*[]"), options: TestOptions.UnsafeExe, emitOptions: EmitOptions.RefEmitBug, expectedOutput: @"True");
        }

        #endregion Functional tests

        #region Regression tests

        [WorkItem(545026)]
        [Fact]
        public void MixedSafeAndUnsafeFields()
        {
            var text =
@"struct Perf_Contexts
{
    int data;
    private int SuppressUnused(int x) { data = x; return data; }
}

public sealed class ChannelServices
{
    static unsafe Perf_Contexts* GetPrivateContextsPerfCounters() { return null; }
    private static int I1 = 12;
    unsafe private static Perf_Contexts* perf_Contexts = GetPrivateContextsPerfCounters();
    private static int I2 = 13;
    private static int SuppressUnused(int x) { return I1 + I2; }
}

public class Test
{
    public static void Main()
    {
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeExe, verify: false).VerifyDiagnostics();
        }

        [WorkItem(545026)]
        [Fact]
        public void SafeFieldBeforeUnsafeField()
        {
            var text = @"
class C
{
    int x = 1;
    unsafe int* p = (int*)2;
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeDll, verify: false).VerifyDiagnostics(
                // (4,9): warning CS0414: The field 'C.x' is assigned but its value is never used
                //     int x = 1;
                Diagnostic(ErrorCode.WRN_UnreferencedFieldAssg, "x").WithArguments("C.x"));
        }

        [WorkItem(545026)]
        [Fact]
        public void SafeFieldAfterUnsafeField()
        {
            var text = @"
class C
{
    unsafe int* p = (int*)2;
    int x = 1;
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeDll, verify: false).VerifyDiagnostics(
                // (5,9): warning CS0414: The field 'C.x' is assigned but its value is never used
                //     int x = 1;
                Diagnostic(ErrorCode.WRN_UnreferencedFieldAssg, "x").WithArguments("C.x"));
        }

        [WorkItem(545026), WorkItem(598170)]
        [Fact]
        public void FixedPassByRef()
        {
            var text = @"
class Test
{
    unsafe static int printAddress(out int* pI)
    {
        pI = null;
        System.Console.WriteLine((ulong)pI);
        return 1;
    }

    unsafe static int printAddress1(ref int* pI)
    {
        pI = null;
        System.Console.WriteLine((ulong)pI);
        return 1;
    }

    static int Main()
    {
        int retval = 0;
        S s = new S();
        unsafe
        {
            retval = Test.printAddress(out s.i);
            retval = Test.printAddress1(ref s.i);
        }

        if (retval == 0)
            System.Console.WriteLine(""Failed."");

        return retval;
    }
}
unsafe struct S
{
    public fixed int i[1];
}

";
            var comp = CreateCompilationWithMscorlib(text, compOptions: DefaultCompilationOptions.WithAllowUnsafe(true));
            comp.VerifyDiagnostics(
                // (24,44): error CS1510: A ref or out argument must be an assignable variable
                //             retval = Test.printAddress(out s.i);
    Diagnostic(ErrorCode.ERR_RefLvalueExpected, "s.i"),
                // (25,45): error CS1510: A ref or out argument must be an assignable variable
                //             retval = Test.printAddress1(ref s.i);
    Diagnostic(ErrorCode.ERR_RefLvalueExpected, "s.i")
    );
        }

        [Fact, WorkItem(545293), WorkItem(881188)]
        public void EmptyAndFixedBufferStructIsInitialized()
        {
            var text = @"
public struct EmptyStruct { }
unsafe public struct FixedStruct { fixed char c[10]; }

public struct OuterStruct 
{
    EmptyStruct ES;
    FixedStruct FS;
    override public string ToString() { return (ES.ToString() + FS.ToString()); }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeDll, verify: false).VerifyDiagnostics(
                // (8,17): warning CS0649: Field 'OuterStruct.FS' is never assigned to, and will always have its default value 
                //     FixedStruct FS;
                Diagnostic(ErrorCode.WRN_UnassignedInternalField, "FS").WithArguments("OuterStruct.FS", "").WithLocation(8, 17),
                // (7,17): warning CS0649: Field 'OuterStruct.ES' is never assigned to, and will always have its default value 
                //     EmptyStruct ES;
                Diagnostic(ErrorCode.WRN_UnassignedInternalField, "ES").WithArguments("OuterStruct.ES", "").WithLocation(7, 17)
                );
        }

        [Fact, WorkItem(545296), WorkItem(545999)]
        public void FixedBufferAndStatementWithFixedArrayElementAsInitializer()
        {
            var text = @"
unsafe public struct FixedStruct 
{
    fixed int i[1];
    fixed char c[10];
    override public string ToString()  { 
        fixed (char* pc = this.c) { return pc[0].ToString(); } 
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeDll, verify: false).VerifyDiagnostics();
        }

        [Fact, WorkItem(545299)]
        public void FixedStatementInlambda()
        {
            var text = @"
using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

unsafe class C<T> where T : struct
{
    public void Foo()
    {
        Func<T, char> d = delegate
        {
            fixed (char* p = ""blah"")
            {
                for (char* pp = p; pp != null; pp++)
                    return *pp;
            }
            return 'c';
        };

        Console.WriteLine(d(default(T)));
    }
}

class A
{
    static void Main()
    {
        new C<int>().Foo();
    }
}
";
            CompileAndVerify(text, options: TestOptions.UnsafeExe, emitPdb: true, expectedOutput: "b");
        }

        [Fact, WorkItem(546865)]
        public void DontStackScheduleLocalPerformingPointerConversion()
        {
            var text = @"
using System;

unsafe struct S1
{
    public char* charPointer;
}

unsafe class Test
{
    static void Main()
    {
        S1 s1 = new S1();
        fixed (char* p = ""hello"")
        {
            s1.charPointer = p;
            ulong UserData = (ulong)&s1;
            Test1(UserData);
        }
    }

    static void Test1(ulong number)
    {
        S1* structPointer = (S1*)number;
        Console.WriteLine(new string(structPointer->charPointer));
    }

    static ulong Test2()
    {
        S1* structPointer = (S1*)null; // null to pointer
        int* intPointer = (int*)structPointer; // pointer to pointer
        void* voidPointer = (void*)intPointer; // pointer to void
        ulong number = (ulong)voidPointer; // pointer to integer
        return number;
    }
}
";

            var verifier = CompileAndVerify(text, options: TestOptions.UnsafeExe.WithOptimizations(true), emitPdb: true, expectedOutput: "hello");

            // Note that the pointer local is not scheduled on the stack.
            verifier.VerifyIL("Test.Test1", @"
{
  // Code size       22 (0x16)
  .maxstack  1
  .locals init (S1* V_0) //structPointer
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  conv.u
  IL_0003:  stloc.0
  IL_0004:  ldloc.0
  IL_0005:  ldfld      ""char* S1.charPointer""
  IL_000a:  newobj     ""string..ctor(char*)""
  IL_000f:  call       ""void System.Console.WriteLine(string)""
  IL_0014:  nop
  IL_0015:  ret
}");

            // All locals retained.
            verifier.VerifyIL("Test.Test2", @"
{
  // Code size       19 (0x13)
  .maxstack  1
  .locals init (S1* V_0, //structPointer
  int* V_1, //intPointer
  void* V_2, //voidPointer
  ulong V_3, //number
  ulong V_4)
  IL_0000:  nop
  IL_0001:  ldc.i4.0
  IL_0002:  conv.u
  IL_0003:  stloc.0
  IL_0004:  ldloc.0
  IL_0005:  stloc.1
  IL_0006:  ldloc.1
  IL_0007:  stloc.2
  IL_0008:  ldloc.2
  IL_0009:  conv.u8
  IL_000a:  stloc.3
  IL_000b:  ldloc.3
  IL_000c:  stloc.s    V_4
  IL_000e:  br.s       IL_0010
  IL_0010:  ldloc.s    V_4
  IL_0012:  ret
}");
        }

        [Fact, WorkItem(546807)]
        public void PointerMemberAccessReadonlyField()
        {
            var text = @"
using System;

unsafe class C
{
    public S1* S1;
}

unsafe struct S1
{
    public readonly int* X;
    public int* Y;
}

unsafe class Test
{
    static void Main()
    {
        S1 s1 = new S1();
        C c = new C();
        c.S1 = &s1;
        Console.WriteLine(null == c.S1->X);
        Console.WriteLine(null == c.S1->Y);
    }
}
";

            var verifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: @"
True
True");

            // NOTE: ldobj before ldfld S1.X, but not before ldfld S1.Y.
            verifier.VerifyIL("Test.Main", @"
{
  // Code size       64 (0x40)
  .maxstack  2
  .locals init (S1 V_0, //s1
  C V_1) //c
  IL_0000:  ldloca.s   V_0
  IL_0002:  initobj    ""S1""
  IL_0008:  newobj     ""C..ctor()""
  IL_000d:  stloc.1
  IL_000e:  ldloc.1
  IL_000f:  ldloca.s   V_0
  IL_0011:  conv.u
  IL_0012:  stfld      ""S1* C.S1""
  IL_0017:  ldc.i4.0
  IL_0018:  conv.u
  IL_0019:  ldloc.1
  IL_001a:  ldfld      ""S1* C.S1""
  IL_001f:  ldfld      ""int* S1.X""
  IL_0024:  ceq
  IL_0026:  call       ""void System.Console.WriteLine(bool)""
  IL_002b:  ldc.i4.0
  IL_002c:  conv.u
  IL_002d:  ldloc.1
  IL_002e:  ldfld      ""S1* C.S1""
  IL_0033:  ldfld      ""int* S1.Y""
  IL_0038:  ceq
  IL_003a:  call       ""void System.Console.WriteLine(bool)""
  IL_003f:  ret
}
");
        }

        [Fact, WorkItem(546807)]
        public void PointerMemberAccessCall()
        {
            var text = @"
using System;

unsafe class C
{
    public S1* S1;
}

unsafe struct S1
{
    public int X;

    public void Instance()
    {
        Console.WriteLine(this.X);
    }
}

static class Extensions
{
    public static void Extension(this S1 s1)
    {
        Console.WriteLine(s1.X);
    }
}

unsafe class Test
{
    static void Main()
    {
        S1 s1 = new S1 { X = 2 };
        C c = new C();
        c.S1 = &s1;
        c.S1->Instance();
        c.S1->Extension();
    }
}
";

            var verifier = CompileAndVerify(text, new[] { SystemCoreRef }, options: TestOptions.UnsafeExe, expectedOutput: @"
2
2");

            // NOTE: ldobj before extension call, but not before instance call.
            verifier.VerifyIL("Test.Main", @"
{
  // Code size       59 (0x3b)
  .maxstack  3
  .locals init (S1 V_0, //s1
  S1 V_1)
  IL_0000:  ldloca.s   V_1
  IL_0002:  initobj    ""S1""
  IL_0008:  ldloca.s   V_1
  IL_000a:  ldc.i4.2
  IL_000b:  stfld      ""int S1.X""
  IL_0010:  ldloc.1
  IL_0011:  stloc.0
  IL_0012:  newobj     ""C..ctor()""
  IL_0017:  dup
  IL_0018:  ldloca.s   V_0
  IL_001a:  conv.u
  IL_001b:  stfld      ""S1* C.S1""
  IL_0020:  dup
  IL_0021:  ldfld      ""S1* C.S1""
  IL_0026:  call       ""void S1.Instance()""
  IL_002b:  ldfld      ""S1* C.S1""
  IL_0030:  ldobj      ""S1""
  IL_0035:  call       ""void Extensions.Extension(S1)""
  IL_003a:  ret
}");
        }

        [Fact, WorkItem(531327)]
        public void PointerParameter()
        {
            var text = @"
using System;

unsafe struct S1
{
    static void M(N.S2* ps2){}
}
namespace N
{
  public struct S2
  {
    public int F;
  }
}
";

            var verifier = CompileAndVerify(text, new[] { SystemCoreRef }, options: TestOptions.UnsafeDll.WithConcurrentBuild(false) );
        }

        [Fact, WorkItem(531327)]
        public void PointerReturn()
        {
            var text = @"
using System;

namespace N
{
  public struct S2
  {
    public int F;
  }
}

unsafe struct S1
{
    static N.S2* M(int ps2){return null;}
}

";

            var verifier = CompileAndVerify(text, new[] { SystemCoreRef }, options: TestOptions.UnsafeDll.WithConcurrentBuild(false));
        }

        [Fact, WorkItem(748530)]
        public void Repro748530()
        {
            var text = @"
unsafe class A
{
    public unsafe struct ListNode
    {
        internal ListNode(int data, ListNode* pNext)
        {
        }
    }
}
";
            
            var comp = CreateCompilationWithMscorlib(text, compOptions: TestOptions.UnsafeDll);
            Assert.DoesNotThrow(() => comp.VerifyDiagnostics());
        }

        [WorkItem(682584)]
        [Fact]
        public void UnsafeMathConv()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main(string[] args)
    {
        byte* data = (byte*)0x76543210;
        uint offset = 0x80000000;
        byte* wrong = data + offset;
        Console.WriteLine(""{0:X}"", (ulong)wrong);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "F6543210");
            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       36 (0x24)
  .maxstack  2
  .locals init (byte* V_0, //data
  uint V_1, //offset
  byte* V_2) //wrong
  IL_0000:  ldc.i4     0x76543210
  IL_0005:  conv.i
  IL_0006:  stloc.0
  IL_0007:  ldc.i4     0x80000000
  IL_000c:  stloc.1
  IL_000d:  ldloc.0
  IL_000e:  ldloc.1
  IL_000f:  conv.u
  IL_0010:  add
  IL_0011:  stloc.2
  IL_0012:  ldstr      ""{0:X}""
  IL_0017:  ldloc.2
  IL_0018:  conv.u8
  IL_0019:  box        ""ulong""
  IL_001e:  call       ""void System.Console.WriteLine(string, object)""
  IL_0023:  ret
}
");
        }

        [WorkItem(682584)]
        [Fact]
        public void UnsafeMathConv001()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main(string[] args)
    {
        short* data = (short*)0x76543210;
        uint offset = 0x40000000;
        short* wrong = data + offset;
        Console.WriteLine(""{0:X}"", (ulong)wrong);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "F6543210");
            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       40 (0x28)
  .maxstack  3
  .locals init (short* V_0, //data
  uint V_1, //offset
  short* V_2) //wrong
  IL_0000:  ldc.i4     0x76543210
  IL_0005:  conv.i
  IL_0006:  stloc.0
  IL_0007:  ldc.i4     0x40000000
  IL_000c:  stloc.1
  IL_000d:  ldloc.0
  IL_000e:  ldloc.1
  IL_000f:  conv.u8
  IL_0010:  ldc.i4.2
  IL_0011:  conv.i8
  IL_0012:  mul
  IL_0013:  conv.i
  IL_0014:  add
  IL_0015:  stloc.2
  IL_0016:  ldstr      ""{0:X}""
  IL_001b:  ldloc.2
  IL_001c:  conv.u8
  IL_001d:  box        ""ulong""
  IL_0022:  call       ""void System.Console.WriteLine(string, object)""
  IL_0027:  ret
}
");
        }

        [WorkItem(682584)]
        [Fact]
        public void UnsafeMathConv002()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main(string[] args)
    {
        byte* data = (byte*)0x76543210;
        byte* wrong = data + 0x80000000u;
        Console.WriteLine(""{0:X}"", (ulong)wrong);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "F6543210");
            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       34 (0x22)
  .maxstack  2
  .locals init (byte* V_0, //data
  byte* V_1) //wrong
  IL_0000:  ldc.i4     0x76543210
  IL_0005:  conv.i
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  ldc.i4     0x80000000
  IL_000d:  conv.u
  IL_000e:  add
  IL_000f:  stloc.1
  IL_0010:  ldstr      ""{0:X}""
  IL_0015:  ldloc.1
  IL_0016:  conv.u8
  IL_0017:  box        ""ulong""
  IL_001c:  call       ""void System.Console.WriteLine(string, object)""
  IL_0021:  ret
}
");
        }

        [WorkItem(682584)]
        [Fact]
        public void UnsafeMathConv002a()
        {
            var text = @"
using System;

unsafe class C
{
    static void Main(string[] args)
    {
        byte* data = (byte*)0x76543210;
        byte* wrong = data + 0x7FFFFFFFu;
        Console.WriteLine(""{0:X}"", (ulong)wrong);
    }
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "F654320F");
            compVerifier.VerifyIL("C.Main", @"
{
  // Code size       33 (0x21)
  .maxstack  2
  .locals init (byte* V_0, //data
  byte* V_1) //wrong
  IL_0000:  ldc.i4     0x76543210
  IL_0005:  conv.i
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  ldc.i4     0x7fffffff
  IL_000d:  add
  IL_000e:  stloc.1
  IL_000f:  ldstr      ""{0:X}""
  IL_0014:  ldloc.1
  IL_0015:  conv.u8
  IL_0016:  box        ""ulong""
  IL_001b:  call       ""void System.Console.WriteLine(string, object)""
  IL_0020:  ret
}
");
        }

        [WorkItem(857598)]
        [Fact]
        public void VoidToNullable()
        {
            var text = @"
unsafe class C
{    
	public int? x = (int?)(void*)0;
}

class c1
{
	public static void Main()
	{
		var x = new C();
		System.Console.WriteLine(x.x);
	}
}
";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput:"0");
            compVerifier.VerifyIL("C..ctor", @"
{
  // Code size       21 (0x15)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldc.i4.0
  IL_0002:  conv.i
  IL_0003:  conv.i4
  IL_0004:  newobj     ""int?..ctor(int)""
  IL_0009:  stfld      ""int? C.x""
  IL_000e:  ldarg.0
  IL_000f:  call       ""object..ctor()""
  IL_0014:  ret
}
");
        }

        [WorkItem(907771)]
        [Fact]
        public void UnsafeBeforeReturn001()
        {
            var text = @"

using System;
 
public unsafe class C
{
    private static readonly byte[] _emptyArray = new byte[0];
 
    public static void Main()
    {
        System.Console.WriteLine(ToManagedByteArray(2));
    }
 
    public static byte[] ToManagedByteArray(uint byteCount)
    {
        if (byteCount == 0)
        {
            return _emptyArray; // degenerate case
        }
        else
        {
            byte[] bytes = new byte[byteCount];
            fixed (byte* pBytes = bytes)
            {
 
            }
            return bytes;
        }
    }
}

";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe, expectedOutput: "System.Byte[]");
            compVerifier.VerifyIL("C.ToManagedByteArray", @"
{
  // Code size       42 (0x2a)
  .maxstack  3
  .locals init (pinned byte& V_0, //pBytes
  byte[] V_1)
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_0009
  IL_0003:  ldsfld     ""byte[] C._emptyArray""
  IL_0008:  ret
  IL_0009:  ldarg.0
  IL_000a:  newarr     ""byte""
  IL_000f:  dup
  IL_0010:  dup
  IL_0011:  stloc.1
  IL_0012:  brfalse.s  IL_0019
  IL_0014:  ldloc.1
  IL_0015:  ldlen
  IL_0016:  conv.i4
  IL_0017:  brtrue.s   IL_001e
  IL_0019:  ldc.i4.0
  IL_001a:  conv.u
  IL_001b:  stloc.0
  IL_001c:  br.s       IL_0026
  IL_001e:  ldloc.1
  IL_001f:  ldc.i4.0
  IL_0020:  ldelema    ""byte""
  IL_0025:  stloc.0
  IL_0026:  ldc.i4.0
  IL_0027:  conv.u
  IL_0028:  stloc.0
  IL_0029:  ret
}
");
        }

        [WorkItem(907771)]
        [Fact]
        public void UnsafeBeforeReturn002()
        {
            var text = @"

using System;
 
public unsafe class C
{
    private static readonly byte[] _emptyArray = new byte[0];
 
    public static void Main()
    {
        System.Console.WriteLine(ToManagedByteArray(2));
    }
 
    public static byte[] ToManagedByteArray(uint byteCount)
    {
        if (byteCount == 0)
        {
            return _emptyArray; // degenerate case
        }
        else
        {
            byte[] bytes = new byte[byteCount];
            fixed (byte* pBytes = bytes)
            {
 
            }
            return bytes;
        }
    }
}

";
            var compVerifier = CompileAndVerify(text, options: TestOptions.UnsafeExe.WithOptimizations(false), expectedOutput: "System.Byte[]");
            compVerifier.VerifyIL("C.ToManagedByteArray", @"
{
  // Code size       58 (0x3a)
  .maxstack  2
  .locals init (bool V_0,
  byte[] V_1,
  byte[] V_2, //bytes
  pinned byte& V_3, //pBytes
  byte[] V_4)
  IL_0000:  ldarg.0
  IL_0001:  ldc.i4.0
  IL_0002:  cgt.un
  IL_0004:  stloc.0
  IL_0005:  ldloc.0
  IL_0006:  brtrue.s   IL_0010
  IL_0008:  ldsfld     ""byte[] C._emptyArray""
  IL_000d:  stloc.1
  IL_000e:  br.s       IL_0038
  IL_0010:  ldarg.0
  IL_0011:  newarr     ""byte""
  IL_0016:  stloc.2
  IL_0017:  ldloc.2
  IL_0018:  dup
  IL_0019:  stloc.s    V_4
  IL_001b:  brfalse.s  IL_0023
  IL_001d:  ldloc.s    V_4
  IL_001f:  ldlen
  IL_0020:  conv.i4
  IL_0021:  brtrue.s   IL_0028
  IL_0023:  ldc.i4.0
  IL_0024:  conv.u
  IL_0025:  stloc.3
  IL_0026:  br.s       IL_0031
  IL_0028:  ldloc.s    V_4
  IL_002a:  ldc.i4.0
  IL_002b:  ldelema    ""byte""
  IL_0030:  stloc.3
  IL_0031:  ldc.i4.0
  IL_0032:  conv.u
  IL_0033:  stloc.3
  IL_0034:  ldloc.2
  IL_0035:  stloc.1
  IL_0036:  br.s       IL_0038
  IL_0038:  ldloc.1
  IL_0039:  ret
}
");
        }

        #endregion
    }
}