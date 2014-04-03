﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.CSharp.UnitTests.Emit;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    public class CodeGenShortCircuitOperatorTests : CSharpTestBase
    {
        [Fact]
        public void TestShortCircuitAnd()
        {
            var source = @"
class C 
{ 
    public static bool Test(char ch, bool result)
    {
        System.Console.WriteLine(ch);
        return result;
    }

    public static void Main() 
    { 
        const bool c1 = true;
        const bool c2 = false;
        bool v1 = true;
        bool v2 = false;

        System.Console.WriteLine(true && true);
        System.Console.WriteLine(true && false);
        System.Console.WriteLine(false && true);
        System.Console.WriteLine(false && false);

        System.Console.WriteLine(c1 && c1);
        System.Console.WriteLine(c1 && c2);
        System.Console.WriteLine(c2 && c1);
        System.Console.WriteLine(c2 && c2);

        System.Console.WriteLine(v1 && v1);
        System.Console.WriteLine(v1 && v2);
        System.Console.WriteLine(v2 && v1);
        System.Console.WriteLine(v2 && v2);

        System.Console.WriteLine(Test('L', true) && Test('R', true));
        System.Console.WriteLine(Test('L', true) && Test('R', false));
        System.Console.WriteLine(Test('L', false) && Test('R', true));
        System.Console.WriteLine(Test('L', false) && Test('R', false));
    }
}
";
            var compilation = CompileAndVerify(source, expectedOutput: @"
True
False
False
False
True
False
False
False
True
False
False
False
L
R
True
L
R
False
L
False
L
False
");

            compilation.VerifyIL("C.Main", @"
{
  // Code size      189 (0xbd)
  .maxstack  2
  .locals init (bool V_0, //v1
  bool V_1) //v2
  IL_0000:  ldc.i4.1
  IL_0001:  stloc.0
  IL_0002:  ldc.i4.0
  IL_0003:  stloc.1
  IL_0004:  ldc.i4.1
  IL_0005:  call       ""void System.Console.WriteLine(bool)""
  IL_000a:  ldc.i4.0
  IL_000b:  call       ""void System.Console.WriteLine(bool)""
  IL_0010:  ldc.i4.0
  IL_0011:  call       ""void System.Console.WriteLine(bool)""
  IL_0016:  ldc.i4.0
  IL_0017:  call       ""void System.Console.WriteLine(bool)""
  IL_001c:  ldc.i4.1
  IL_001d:  call       ""void System.Console.WriteLine(bool)""
  IL_0022:  ldc.i4.0
  IL_0023:  call       ""void System.Console.WriteLine(bool)""
  IL_0028:  ldc.i4.0
  IL_0029:  call       ""void System.Console.WriteLine(bool)""
  IL_002e:  ldc.i4.0
  IL_002f:  call       ""void System.Console.WriteLine(bool)""
  IL_0034:  ldloc.0
  IL_0035:  dup
  IL_0036:  and
  IL_0037:  call       ""void System.Console.WriteLine(bool)""
  IL_003c:  ldloc.0
  IL_003d:  ldloc.1
  IL_003e:  and
  IL_003f:  call       ""void System.Console.WriteLine(bool)""
  IL_0044:  ldloc.1
  IL_0045:  ldloc.0
  IL_0046:  and
  IL_0047:  call       ""void System.Console.WriteLine(bool)""
  IL_004c:  ldloc.1
  IL_004d:  dup
  IL_004e:  and
  IL_004f:  call       ""void System.Console.WriteLine(bool)""
  IL_0054:  ldc.i4.s   76
  IL_0056:  ldc.i4.1
  IL_0057:  call       ""bool C.Test(char, bool)""
  IL_005c:  brfalse.s  IL_0068
  IL_005e:  ldc.i4.s   82
  IL_0060:  ldc.i4.1
  IL_0061:  call       ""bool C.Test(char, bool)""
  IL_0066:  br.s       IL_0069
  IL_0068:  ldc.i4.0
  IL_0069:  call       ""void System.Console.WriteLine(bool)""
  IL_006e:  ldc.i4.s   76
  IL_0070:  ldc.i4.1
  IL_0071:  call       ""bool C.Test(char, bool)""
  IL_0076:  brfalse.s  IL_0082
  IL_0078:  ldc.i4.s   82
  IL_007a:  ldc.i4.0
  IL_007b:  call       ""bool C.Test(char, bool)""
  IL_0080:  br.s       IL_0083
  IL_0082:  ldc.i4.0
  IL_0083:  call       ""void System.Console.WriteLine(bool)""
  IL_0088:  ldc.i4.s   76
  IL_008a:  ldc.i4.0
  IL_008b:  call       ""bool C.Test(char, bool)""
  IL_0090:  brfalse.s  IL_009c
  IL_0092:  ldc.i4.s   82
  IL_0094:  ldc.i4.1
  IL_0095:  call       ""bool C.Test(char, bool)""
  IL_009a:  br.s       IL_009d
  IL_009c:  ldc.i4.0
  IL_009d:  call       ""void System.Console.WriteLine(bool)""
  IL_00a2:  ldc.i4.s   76
  IL_00a4:  ldc.i4.0
  IL_00a5:  call       ""bool C.Test(char, bool)""
  IL_00aa:  brfalse.s  IL_00b6
  IL_00ac:  ldc.i4.s   82
  IL_00ae:  ldc.i4.0
  IL_00af:  call       ""bool C.Test(char, bool)""
  IL_00b4:  br.s       IL_00b7
  IL_00b6:  ldc.i4.0
  IL_00b7:  call       ""void System.Console.WriteLine(bool)""
  IL_00bc:  ret
}");
        }

        [Fact]
        public void TestShortCircuitOr()
        {
            var source = @"
class C 
{ 
    public static bool Test(char ch, bool result)
    {
        System.Console.WriteLine(ch);
        return result;
    }

    public static void Main() 
    { 
        const bool c1 = true;
        const bool c2 = false;
        bool v1 = true;
        bool v2 = false;

        System.Console.WriteLine(true || true);
        System.Console.WriteLine(true || false);
        System.Console.WriteLine(false || true);
        System.Console.WriteLine(false || false);

        System.Console.WriteLine(c1 || c1);
        System.Console.WriteLine(c1 || c2);
        System.Console.WriteLine(c2 || c1);
        System.Console.WriteLine(c2 || c2);

        System.Console.WriteLine(v1 || v1);
        System.Console.WriteLine(v1 || v2);
        System.Console.WriteLine(v2 || v1);
        System.Console.WriteLine(v2 || v2);

        System.Console.WriteLine(Test('L', true) || Test('R', true));
        System.Console.WriteLine(Test('L', true) || Test('R', false));
        System.Console.WriteLine(Test('L', false) || Test('R', true));
        System.Console.WriteLine(Test('L', false) || Test('R', false));
    }
}
";
            var compilation = CompileAndVerify(source, expectedOutput: @"
True
True
True
False
True
True
True
False
True
True
True
False
L
True
L
True
L
R
True
L
R
False
");

            compilation.VerifyIL("C.Main", @"
{
  // Code size      189 (0xbd)
  .maxstack  2
  .locals init (bool V_0, //v1
  bool V_1) //v2
  IL_0000:  ldc.i4.1
  IL_0001:  stloc.0
  IL_0002:  ldc.i4.0
  IL_0003:  stloc.1
  IL_0004:  ldc.i4.1
  IL_0005:  call       ""void System.Console.WriteLine(bool)""
  IL_000a:  ldc.i4.1
  IL_000b:  call       ""void System.Console.WriteLine(bool)""
  IL_0010:  ldc.i4.1
  IL_0011:  call       ""void System.Console.WriteLine(bool)""
  IL_0016:  ldc.i4.0
  IL_0017:  call       ""void System.Console.WriteLine(bool)""
  IL_001c:  ldc.i4.1
  IL_001d:  call       ""void System.Console.WriteLine(bool)""
  IL_0022:  ldc.i4.1
  IL_0023:  call       ""void System.Console.WriteLine(bool)""
  IL_0028:  ldc.i4.1
  IL_0029:  call       ""void System.Console.WriteLine(bool)""
  IL_002e:  ldc.i4.0
  IL_002f:  call       ""void System.Console.WriteLine(bool)""
  IL_0034:  ldloc.0
  IL_0035:  dup
  IL_0036:  or
  IL_0037:  call       ""void System.Console.WriteLine(bool)""
  IL_003c:  ldloc.0
  IL_003d:  ldloc.1
  IL_003e:  or
  IL_003f:  call       ""void System.Console.WriteLine(bool)""
  IL_0044:  ldloc.1
  IL_0045:  ldloc.0
  IL_0046:  or
  IL_0047:  call       ""void System.Console.WriteLine(bool)""
  IL_004c:  ldloc.1
  IL_004d:  dup
  IL_004e:  or
  IL_004f:  call       ""void System.Console.WriteLine(bool)""
  IL_0054:  ldc.i4.s   76
  IL_0056:  ldc.i4.1
  IL_0057:  call       ""bool C.Test(char, bool)""
  IL_005c:  brtrue.s   IL_0068
  IL_005e:  ldc.i4.s   82
  IL_0060:  ldc.i4.1
  IL_0061:  call       ""bool C.Test(char, bool)""
  IL_0066:  br.s       IL_0069
  IL_0068:  ldc.i4.1
  IL_0069:  call       ""void System.Console.WriteLine(bool)""
  IL_006e:  ldc.i4.s   76
  IL_0070:  ldc.i4.1
  IL_0071:  call       ""bool C.Test(char, bool)""
  IL_0076:  brtrue.s   IL_0082
  IL_0078:  ldc.i4.s   82
  IL_007a:  ldc.i4.0
  IL_007b:  call       ""bool C.Test(char, bool)""
  IL_0080:  br.s       IL_0083
  IL_0082:  ldc.i4.1
  IL_0083:  call       ""void System.Console.WriteLine(bool)""
  IL_0088:  ldc.i4.s   76
  IL_008a:  ldc.i4.0
  IL_008b:  call       ""bool C.Test(char, bool)""
  IL_0090:  brtrue.s   IL_009c
  IL_0092:  ldc.i4.s   82
  IL_0094:  ldc.i4.1
  IL_0095:  call       ""bool C.Test(char, bool)""
  IL_009a:  br.s       IL_009d
  IL_009c:  ldc.i4.1
  IL_009d:  call       ""void System.Console.WriteLine(bool)""
  IL_00a2:  ldc.i4.s   76
  IL_00a4:  ldc.i4.0
  IL_00a5:  call       ""bool C.Test(char, bool)""
  IL_00aa:  brtrue.s   IL_00b6
  IL_00ac:  ldc.i4.s   82
  IL_00ae:  ldc.i4.0
  IL_00af:  call       ""bool C.Test(char, bool)""
  IL_00b4:  br.s       IL_00b7
  IL_00b6:  ldc.i4.1
  IL_00b7:  call       ""void System.Console.WriteLine(bool)""
  IL_00bc:  ret
}");
        }

        [Fact]
        public void TestChainedShortCircuitOperators()
        {
            var source = @"
class C 
{ 
    public static bool Test(char ch, bool result)
    {
        System.Console.WriteLine(ch);
        return result;
    }

    public static void Main() 
    { 
        // AND AND
        System.Console.WriteLine(Test('A', true) && Test('B', true) && Test('C' , true));
        System.Console.WriteLine(Test('A', true) && Test('B', true) && Test('C' , false));
        System.Console.WriteLine(Test('A', true) && Test('B', false) && Test('C' , true));
        System.Console.WriteLine(Test('A', true) && Test('B', false) && Test('C' , false));
        System.Console.WriteLine(Test('A', false) && Test('B', true) && Test('C' , true));
        System.Console.WriteLine(Test('A', false) && Test('B', true) && Test('C' , false));
        System.Console.WriteLine(Test('A', false) && Test('B', false) && Test('C' , true));
        System.Console.WriteLine(Test('A', false) && Test('B', false) && Test('C' , false));

        // AND OR
        System.Console.WriteLine(Test('A', true) && Test('B', true) || Test('C' , true));
        System.Console.WriteLine(Test('A', true) && Test('B', true) || Test('C' , false));
        System.Console.WriteLine(Test('A', true) && Test('B', false) || Test('C' , true));
        System.Console.WriteLine(Test('A', true) && Test('B', false) || Test('C' , false));
        System.Console.WriteLine(Test('A', false) && Test('B', true) || Test('C' , true));
        System.Console.WriteLine(Test('A', false) && Test('B', true) || Test('C' , false));
        System.Console.WriteLine(Test('A', false) && Test('B', false) || Test('C' , true));
        System.Console.WriteLine(Test('A', false) && Test('B', false) || Test('C' , false));

        // OR AND
        System.Console.WriteLine(Test('A', true) || Test('B', true) && Test('C' , true));
        System.Console.WriteLine(Test('A', true) || Test('B', true) && Test('C' , false));
        System.Console.WriteLine(Test('A', true) || Test('B', false) && Test('C' , true));
        System.Console.WriteLine(Test('A', true) || Test('B', false) && Test('C' , false));
        System.Console.WriteLine(Test('A', false) || Test('B', true) && Test('C' , true));
        System.Console.WriteLine(Test('A', false) || Test('B', true) && Test('C' , false));
        System.Console.WriteLine(Test('A', false) || Test('B', false) && Test('C' , true));
        System.Console.WriteLine(Test('A', false) || Test('B', false) && Test('C' , false));

        // OR OR
        System.Console.WriteLine(Test('A', true) || Test('B', true) || Test('C' , true));
        System.Console.WriteLine(Test('A', true) || Test('B', true) || Test('C' , false));
        System.Console.WriteLine(Test('A', true) || Test('B', false) || Test('C' , true));
        System.Console.WriteLine(Test('A', true) || Test('B', false) || Test('C' , false));
        System.Console.WriteLine(Test('A', false) || Test('B', true) || Test('C' , true));
        System.Console.WriteLine(Test('A', false) || Test('B', true) || Test('C' , false));
        System.Console.WriteLine(Test('A', false) || Test('B', false) || Test('C' , true));
        System.Console.WriteLine(Test('A', false) || Test('B', false) || Test('C' , false));
    }
}
";
            var compilation = CompileAndVerify(source, expectedOutput: @"
A
B
C
True
A
B
C
False
A
B
False
A
B
False
A
False
A
False
A
False
A
False
A
B
True
A
B
True
A
B
C
True
A
B
C
False
A
C
True
A
C
False
A
C
True
A
C
False
A
True
A
True
A
True
A
True
A
B
C
True
A
B
C
False
A
B
False
A
B
False
A
True
A
True
A
True
A
True
A
B
True
A
B
True
A
B
C
True
A
B
C
False
");

            compilation.VerifyIL("C.Main", @"{
  // Code size     1177 (0x499)
  .maxstack  2
  IL_0000:  ldc.i4.s   65
  IL_0002:  ldc.i4.1  
  IL_0003:  call       ""bool C.Test(char, bool)""
  IL_0008:  brfalse.s  IL_001e
  IL_000a:  ldc.i4.s   66
  IL_000c:  ldc.i4.1  
  IL_000d:  call       ""bool C.Test(char, bool)""
  IL_0012:  brfalse.s  IL_001e
  IL_0014:  ldc.i4.s   67
  IL_0016:  ldc.i4.1  
  IL_0017:  call       ""bool C.Test(char, bool)""
  IL_001c:  br.s       IL_001f
  IL_001e:  ldc.i4.0  
  IL_001f:  call       ""void System.Console.WriteLine(bool)""
  IL_0024:  ldc.i4.s   65
  IL_0026:  ldc.i4.1  
  IL_0027:  call       ""bool C.Test(char, bool)""
  IL_002c:  brfalse.s  IL_0042
  IL_002e:  ldc.i4.s   66
  IL_0030:  ldc.i4.1  
  IL_0031:  call       ""bool C.Test(char, bool)""
  IL_0036:  brfalse.s  IL_0042
  IL_0038:  ldc.i4.s   67
  IL_003a:  ldc.i4.0  
  IL_003b:  call       ""bool C.Test(char, bool)""
  IL_0040:  br.s       IL_0043
  IL_0042:  ldc.i4.0  
  IL_0043:  call       ""void System.Console.WriteLine(bool)""
  IL_0048:  ldc.i4.s   65
  IL_004a:  ldc.i4.1  
  IL_004b:  call       ""bool C.Test(char, bool)""
  IL_0050:  brfalse.s  IL_0066
  IL_0052:  ldc.i4.s   66
  IL_0054:  ldc.i4.0  
  IL_0055:  call       ""bool C.Test(char, bool)""
  IL_005a:  brfalse.s  IL_0066
  IL_005c:  ldc.i4.s   67
  IL_005e:  ldc.i4.1  
  IL_005f:  call       ""bool C.Test(char, bool)""
  IL_0064:  br.s       IL_0067
  IL_0066:  ldc.i4.0  
  IL_0067:  call       ""void System.Console.WriteLine(bool)""
  IL_006c:  ldc.i4.s   65
  IL_006e:  ldc.i4.1  
  IL_006f:  call       ""bool C.Test(char, bool)""
  IL_0074:  brfalse.s  IL_008a
  IL_0076:  ldc.i4.s   66
  IL_0078:  ldc.i4.0  
  IL_0079:  call       ""bool C.Test(char, bool)""
  IL_007e:  brfalse.s  IL_008a
  IL_0080:  ldc.i4.s   67
  IL_0082:  ldc.i4.0  
  IL_0083:  call       ""bool C.Test(char, bool)""
  IL_0088:  br.s       IL_008b
  IL_008a:  ldc.i4.0  
  IL_008b:  call       ""void System.Console.WriteLine(bool)""
  IL_0090:  ldc.i4.s   65
  IL_0092:  ldc.i4.0  
  IL_0093:  call       ""bool C.Test(char, bool)""
  IL_0098:  brfalse.s  IL_00ae
  IL_009a:  ldc.i4.s   66
  IL_009c:  ldc.i4.1  
  IL_009d:  call       ""bool C.Test(char, bool)""
  IL_00a2:  brfalse.s  IL_00ae
  IL_00a4:  ldc.i4.s   67
  IL_00a6:  ldc.i4.1  
  IL_00a7:  call       ""bool C.Test(char, bool)""
  IL_00ac:  br.s       IL_00af
  IL_00ae:  ldc.i4.0  
  IL_00af:  call       ""void System.Console.WriteLine(bool)""
  IL_00b4:  ldc.i4.s   65
  IL_00b6:  ldc.i4.0  
  IL_00b7:  call       ""bool C.Test(char, bool)""
  IL_00bc:  brfalse.s  IL_00d2
  IL_00be:  ldc.i4.s   66
  IL_00c0:  ldc.i4.1  
  IL_00c1:  call       ""bool C.Test(char, bool)""
  IL_00c6:  brfalse.s  IL_00d2
  IL_00c8:  ldc.i4.s   67
  IL_00ca:  ldc.i4.0  
  IL_00cb:  call       ""bool C.Test(char, bool)""
  IL_00d0:  br.s       IL_00d3
  IL_00d2:  ldc.i4.0  
  IL_00d3:  call       ""void System.Console.WriteLine(bool)""
  IL_00d8:  ldc.i4.s   65
  IL_00da:  ldc.i4.0  
  IL_00db:  call       ""bool C.Test(char, bool)""
  IL_00e0:  brfalse.s  IL_00f6
  IL_00e2:  ldc.i4.s   66
  IL_00e4:  ldc.i4.0  
  IL_00e5:  call       ""bool C.Test(char, bool)""
  IL_00ea:  brfalse.s  IL_00f6
  IL_00ec:  ldc.i4.s   67
  IL_00ee:  ldc.i4.1  
  IL_00ef:  call       ""bool C.Test(char, bool)""
  IL_00f4:  br.s       IL_00f7
  IL_00f6:  ldc.i4.0  
  IL_00f7:  call       ""void System.Console.WriteLine(bool)""
  IL_00fc:  ldc.i4.s   65
  IL_00fe:  ldc.i4.0  
  IL_00ff:  call       ""bool C.Test(char, bool)""
  IL_0104:  brfalse.s  IL_011a
  IL_0106:  ldc.i4.s   66
  IL_0108:  ldc.i4.0  
  IL_0109:  call       ""bool C.Test(char, bool)""
  IL_010e:  brfalse.s  IL_011a
  IL_0110:  ldc.i4.s   67
  IL_0112:  ldc.i4.0  
  IL_0113:  call       ""bool C.Test(char, bool)""
  IL_0118:  br.s       IL_011b
  IL_011a:  ldc.i4.0  
  IL_011b:  call       ""void System.Console.WriteLine(bool)""
  IL_0120:  ldc.i4.s   65
  IL_0122:  ldc.i4.1  
  IL_0123:  call       ""bool C.Test(char, bool)""
  IL_0128:  brfalse.s  IL_0134
  IL_012a:  ldc.i4.s   66
  IL_012c:  ldc.i4.1  
  IL_012d:  call       ""bool C.Test(char, bool)""
  IL_0132:  brtrue.s   IL_013e
  IL_0134:  ldc.i4.s   67
  IL_0136:  ldc.i4.1  
  IL_0137:  call       ""bool C.Test(char, bool)""
  IL_013c:  br.s       IL_013f
  IL_013e:  ldc.i4.1  
  IL_013f:  call       ""void System.Console.WriteLine(bool)""
  IL_0144:  ldc.i4.s   65
  IL_0146:  ldc.i4.1  
  IL_0147:  call       ""bool C.Test(char, bool)""
  IL_014c:  brfalse.s  IL_0158
  IL_014e:  ldc.i4.s   66
  IL_0150:  ldc.i4.1  
  IL_0151:  call       ""bool C.Test(char, bool)""
  IL_0156:  brtrue.s   IL_0162
  IL_0158:  ldc.i4.s   67
  IL_015a:  ldc.i4.0  
  IL_015b:  call       ""bool C.Test(char, bool)""
  IL_0160:  br.s       IL_0163
  IL_0162:  ldc.i4.1  
  IL_0163:  call       ""void System.Console.WriteLine(bool)""
  IL_0168:  ldc.i4.s   65
  IL_016a:  ldc.i4.1  
  IL_016b:  call       ""bool C.Test(char, bool)""
  IL_0170:  brfalse.s  IL_017c
  IL_0172:  ldc.i4.s   66
  IL_0174:  ldc.i4.0  
  IL_0175:  call       ""bool C.Test(char, bool)""
  IL_017a:  brtrue.s   IL_0186
  IL_017c:  ldc.i4.s   67
  IL_017e:  ldc.i4.1  
  IL_017f:  call       ""bool C.Test(char, bool)""
  IL_0184:  br.s       IL_0187
  IL_0186:  ldc.i4.1  
  IL_0187:  call       ""void System.Console.WriteLine(bool)""
  IL_018c:  ldc.i4.s   65
  IL_018e:  ldc.i4.1  
  IL_018f:  call       ""bool C.Test(char, bool)""
  IL_0194:  brfalse.s  IL_01a0
  IL_0196:  ldc.i4.s   66
  IL_0198:  ldc.i4.0  
  IL_0199:  call       ""bool C.Test(char, bool)""
  IL_019e:  brtrue.s   IL_01aa
  IL_01a0:  ldc.i4.s   67
  IL_01a2:  ldc.i4.0  
  IL_01a3:  call       ""bool C.Test(char, bool)""
  IL_01a8:  br.s       IL_01ab
  IL_01aa:  ldc.i4.1  
  IL_01ab:  call       ""void System.Console.WriteLine(bool)""
  IL_01b0:  ldc.i4.s   65
  IL_01b2:  ldc.i4.0  
  IL_01b3:  call       ""bool C.Test(char, bool)""
  IL_01b8:  brfalse.s  IL_01c4
  IL_01ba:  ldc.i4.s   66
  IL_01bc:  ldc.i4.1  
  IL_01bd:  call       ""bool C.Test(char, bool)""
  IL_01c2:  brtrue.s   IL_01ce
  IL_01c4:  ldc.i4.s   67
  IL_01c6:  ldc.i4.1  
  IL_01c7:  call       ""bool C.Test(char, bool)""
  IL_01cc:  br.s       IL_01cf
  IL_01ce:  ldc.i4.1  
  IL_01cf:  call       ""void System.Console.WriteLine(bool)""
  IL_01d4:  ldc.i4.s   65
  IL_01d6:  ldc.i4.0  
  IL_01d7:  call       ""bool C.Test(char, bool)""
  IL_01dc:  brfalse.s  IL_01e8
  IL_01de:  ldc.i4.s   66
  IL_01e0:  ldc.i4.1  
  IL_01e1:  call       ""bool C.Test(char, bool)""
  IL_01e6:  brtrue.s   IL_01f2
  IL_01e8:  ldc.i4.s   67
  IL_01ea:  ldc.i4.0  
  IL_01eb:  call       ""bool C.Test(char, bool)""
  IL_01f0:  br.s       IL_01f3
  IL_01f2:  ldc.i4.1  
  IL_01f3:  call       ""void System.Console.WriteLine(bool)""
  IL_01f8:  ldc.i4.s   65
  IL_01fa:  ldc.i4.0  
  IL_01fb:  call       ""bool C.Test(char, bool)""
  IL_0200:  brfalse.s  IL_020c
  IL_0202:  ldc.i4.s   66
  IL_0204:  ldc.i4.0  
  IL_0205:  call       ""bool C.Test(char, bool)""
  IL_020a:  brtrue.s   IL_0216
  IL_020c:  ldc.i4.s   67
  IL_020e:  ldc.i4.1  
  IL_020f:  call       ""bool C.Test(char, bool)""
  IL_0214:  br.s       IL_0217
  IL_0216:  ldc.i4.1  
  IL_0217:  call       ""void System.Console.WriteLine(bool)""
  IL_021c:  ldc.i4.s   65
  IL_021e:  ldc.i4.0  
  IL_021f:  call       ""bool C.Test(char, bool)""
  IL_0224:  brfalse.s  IL_0230
  IL_0226:  ldc.i4.s   66
  IL_0228:  ldc.i4.0  
  IL_0229:  call       ""bool C.Test(char, bool)""
  IL_022e:  brtrue.s   IL_023a
  IL_0230:  ldc.i4.s   67
  IL_0232:  ldc.i4.0  
  IL_0233:  call       ""bool C.Test(char, bool)""
  IL_0238:  br.s       IL_023b
  IL_023a:  ldc.i4.1  
  IL_023b:  call       ""void System.Console.WriteLine(bool)""
  IL_0240:  ldc.i4.s   65
  IL_0242:  ldc.i4.1  
  IL_0243:  call       ""bool C.Test(char, bool)""
  IL_0248:  brtrue.s   IL_0261
  IL_024a:  ldc.i4.s   66
  IL_024c:  ldc.i4.1  
  IL_024d:  call       ""bool C.Test(char, bool)""
  IL_0252:  brfalse.s  IL_025e
  IL_0254:  ldc.i4.s   67
  IL_0256:  ldc.i4.1  
  IL_0257:  call       ""bool C.Test(char, bool)""
  IL_025c:  br.s       IL_0262
  IL_025e:  ldc.i4.0  
  IL_025f:  br.s       IL_0262
  IL_0261:  ldc.i4.1  
  IL_0262:  call       ""void System.Console.WriteLine(bool)""
  IL_0267:  ldc.i4.s   65
  IL_0269:  ldc.i4.1  
  IL_026a:  call       ""bool C.Test(char, bool)""
  IL_026f:  brtrue.s   IL_0288
  IL_0271:  ldc.i4.s   66
  IL_0273:  ldc.i4.1  
  IL_0274:  call       ""bool C.Test(char, bool)""
  IL_0279:  brfalse.s  IL_0285
  IL_027b:  ldc.i4.s   67
  IL_027d:  ldc.i4.0  
  IL_027e:  call       ""bool C.Test(char, bool)""
  IL_0283:  br.s       IL_0289
  IL_0285:  ldc.i4.0  
  IL_0286:  br.s       IL_0289
  IL_0288:  ldc.i4.1  
  IL_0289:  call       ""void System.Console.WriteLine(bool)""
  IL_028e:  ldc.i4.s   65
  IL_0290:  ldc.i4.1  
  IL_0291:  call       ""bool C.Test(char, bool)""
  IL_0296:  brtrue.s   IL_02af
  IL_0298:  ldc.i4.s   66
  IL_029a:  ldc.i4.0  
  IL_029b:  call       ""bool C.Test(char, bool)""
  IL_02a0:  brfalse.s  IL_02ac
  IL_02a2:  ldc.i4.s   67
  IL_02a4:  ldc.i4.1  
  IL_02a5:  call       ""bool C.Test(char, bool)""
  IL_02aa:  br.s       IL_02b0
  IL_02ac:  ldc.i4.0  
  IL_02ad:  br.s       IL_02b0
  IL_02af:  ldc.i4.1  
  IL_02b0:  call       ""void System.Console.WriteLine(bool)""
  IL_02b5:  ldc.i4.s   65
  IL_02b7:  ldc.i4.1  
  IL_02b8:  call       ""bool C.Test(char, bool)""
  IL_02bd:  brtrue.s   IL_02d6
  IL_02bf:  ldc.i4.s   66
  IL_02c1:  ldc.i4.0  
  IL_02c2:  call       ""bool C.Test(char, bool)""
  IL_02c7:  brfalse.s  IL_02d3
  IL_02c9:  ldc.i4.s   67
  IL_02cb:  ldc.i4.0  
  IL_02cc:  call       ""bool C.Test(char, bool)""
  IL_02d1:  br.s       IL_02d7
  IL_02d3:  ldc.i4.0  
  IL_02d4:  br.s       IL_02d7
  IL_02d6:  ldc.i4.1  
  IL_02d7:  call       ""void System.Console.WriteLine(bool)""
  IL_02dc:  ldc.i4.s   65
  IL_02de:  ldc.i4.0  
  IL_02df:  call       ""bool C.Test(char, bool)""
  IL_02e4:  brtrue.s   IL_02fd
  IL_02e6:  ldc.i4.s   66
  IL_02e8:  ldc.i4.1  
  IL_02e9:  call       ""bool C.Test(char, bool)""
  IL_02ee:  brfalse.s  IL_02fa
  IL_02f0:  ldc.i4.s   67
  IL_02f2:  ldc.i4.1  
  IL_02f3:  call       ""bool C.Test(char, bool)""
  IL_02f8:  br.s       IL_02fe
  IL_02fa:  ldc.i4.0  
  IL_02fb:  br.s       IL_02fe
  IL_02fd:  ldc.i4.1  
  IL_02fe:  call       ""void System.Console.WriteLine(bool)""
  IL_0303:  ldc.i4.s   65
  IL_0305:  ldc.i4.0  
  IL_0306:  call       ""bool C.Test(char, bool)""
  IL_030b:  brtrue.s   IL_0324
  IL_030d:  ldc.i4.s   66
  IL_030f:  ldc.i4.1  
  IL_0310:  call       ""bool C.Test(char, bool)""
  IL_0315:  brfalse.s  IL_0321
  IL_0317:  ldc.i4.s   67
  IL_0319:  ldc.i4.0  
  IL_031a:  call       ""bool C.Test(char, bool)""
  IL_031f:  br.s       IL_0325
  IL_0321:  ldc.i4.0  
  IL_0322:  br.s       IL_0325
  IL_0324:  ldc.i4.1  
  IL_0325:  call       ""void System.Console.WriteLine(bool)""
  IL_032a:  ldc.i4.s   65
  IL_032c:  ldc.i4.0  
  IL_032d:  call       ""bool C.Test(char, bool)""
  IL_0332:  brtrue.s   IL_034b
  IL_0334:  ldc.i4.s   66
  IL_0336:  ldc.i4.0  
  IL_0337:  call       ""bool C.Test(char, bool)""
  IL_033c:  brfalse.s  IL_0348
  IL_033e:  ldc.i4.s   67
  IL_0340:  ldc.i4.1  
  IL_0341:  call       ""bool C.Test(char, bool)""
  IL_0346:  br.s       IL_034c
  IL_0348:  ldc.i4.0  
  IL_0349:  br.s       IL_034c
  IL_034b:  ldc.i4.1  
  IL_034c:  call       ""void System.Console.WriteLine(bool)""
  IL_0351:  ldc.i4.s   65
  IL_0353:  ldc.i4.0  
  IL_0354:  call       ""bool C.Test(char, bool)""
  IL_0359:  brtrue.s   IL_0372
  IL_035b:  ldc.i4.s   66
  IL_035d:  ldc.i4.0  
  IL_035e:  call       ""bool C.Test(char, bool)""
  IL_0363:  brfalse.s  IL_036f
  IL_0365:  ldc.i4.s   67
  IL_0367:  ldc.i4.0  
  IL_0368:  call       ""bool C.Test(char, bool)""
  IL_036d:  br.s       IL_0373
  IL_036f:  ldc.i4.0  
  IL_0370:  br.s       IL_0373
  IL_0372:  ldc.i4.1  
  IL_0373:  call       ""void System.Console.WriteLine(bool)""
  IL_0378:  ldc.i4.s   65
  IL_037a:  ldc.i4.1  
  IL_037b:  call       ""bool C.Test(char, bool)""
  IL_0380:  brtrue.s   IL_0396
  IL_0382:  ldc.i4.s   66
  IL_0384:  ldc.i4.1  
  IL_0385:  call       ""bool C.Test(char, bool)""
  IL_038a:  brtrue.s   IL_0396
  IL_038c:  ldc.i4.s   67
  IL_038e:  ldc.i4.1  
  IL_038f:  call       ""bool C.Test(char, bool)""
  IL_0394:  br.s       IL_0397
  IL_0396:  ldc.i4.1  
  IL_0397:  call       ""void System.Console.WriteLine(bool)""
  IL_039c:  ldc.i4.s   65
  IL_039e:  ldc.i4.1  
  IL_039f:  call       ""bool C.Test(char, bool)""
  IL_03a4:  brtrue.s   IL_03ba
  IL_03a6:  ldc.i4.s   66
  IL_03a8:  ldc.i4.1  
  IL_03a9:  call       ""bool C.Test(char, bool)""
  IL_03ae:  brtrue.s   IL_03ba
  IL_03b0:  ldc.i4.s   67
  IL_03b2:  ldc.i4.0  
  IL_03b3:  call       ""bool C.Test(char, bool)""
  IL_03b8:  br.s       IL_03bb
  IL_03ba:  ldc.i4.1  
  IL_03bb:  call       ""void System.Console.WriteLine(bool)""
  IL_03c0:  ldc.i4.s   65
  IL_03c2:  ldc.i4.1  
  IL_03c3:  call       ""bool C.Test(char, bool)""
  IL_03c8:  brtrue.s   IL_03de
  IL_03ca:  ldc.i4.s   66
  IL_03cc:  ldc.i4.0  
  IL_03cd:  call       ""bool C.Test(char, bool)""
  IL_03d2:  brtrue.s   IL_03de
  IL_03d4:  ldc.i4.s   67
  IL_03d6:  ldc.i4.1  
  IL_03d7:  call       ""bool C.Test(char, bool)""
  IL_03dc:  br.s       IL_03df
  IL_03de:  ldc.i4.1  
  IL_03df:  call       ""void System.Console.WriteLine(bool)""
  IL_03e4:  ldc.i4.s   65
  IL_03e6:  ldc.i4.1  
  IL_03e7:  call       ""bool C.Test(char, bool)""
  IL_03ec:  brtrue.s   IL_0402
  IL_03ee:  ldc.i4.s   66
  IL_03f0:  ldc.i4.0  
  IL_03f1:  call       ""bool C.Test(char, bool)""
  IL_03f6:  brtrue.s   IL_0402
  IL_03f8:  ldc.i4.s   67
  IL_03fa:  ldc.i4.0  
  IL_03fb:  call       ""bool C.Test(char, bool)""
  IL_0400:  br.s       IL_0403
  IL_0402:  ldc.i4.1  
  IL_0403:  call       ""void System.Console.WriteLine(bool)""
  IL_0408:  ldc.i4.s   65
  IL_040a:  ldc.i4.0  
  IL_040b:  call       ""bool C.Test(char, bool)""
  IL_0410:  brtrue.s   IL_0426
  IL_0412:  ldc.i4.s   66
  IL_0414:  ldc.i4.1  
  IL_0415:  call       ""bool C.Test(char, bool)""
  IL_041a:  brtrue.s   IL_0426
  IL_041c:  ldc.i4.s   67
  IL_041e:  ldc.i4.1  
  IL_041f:  call       ""bool C.Test(char, bool)""
  IL_0424:  br.s       IL_0427
  IL_0426:  ldc.i4.1  
  IL_0427:  call       ""void System.Console.WriteLine(bool)""
  IL_042c:  ldc.i4.s   65
  IL_042e:  ldc.i4.0  
  IL_042f:  call       ""bool C.Test(char, bool)""
  IL_0434:  brtrue.s   IL_044a
  IL_0436:  ldc.i4.s   66
  IL_0438:  ldc.i4.1  
  IL_0439:  call       ""bool C.Test(char, bool)""
  IL_043e:  brtrue.s   IL_044a
  IL_0440:  ldc.i4.s   67
  IL_0442:  ldc.i4.0  
  IL_0443:  call       ""bool C.Test(char, bool)""
  IL_0448:  br.s       IL_044b
  IL_044a:  ldc.i4.1  
  IL_044b:  call       ""void System.Console.WriteLine(bool)""
  IL_0450:  ldc.i4.s   65
  IL_0452:  ldc.i4.0  
  IL_0453:  call       ""bool C.Test(char, bool)""
  IL_0458:  brtrue.s   IL_046e
  IL_045a:  ldc.i4.s   66
  IL_045c:  ldc.i4.0  
  IL_045d:  call       ""bool C.Test(char, bool)""
  IL_0462:  brtrue.s   IL_046e
  IL_0464:  ldc.i4.s   67
  IL_0466:  ldc.i4.1  
  IL_0467:  call       ""bool C.Test(char, bool)""
  IL_046c:  br.s       IL_046f
  IL_046e:  ldc.i4.1  
  IL_046f:  call       ""void System.Console.WriteLine(bool)""
  IL_0474:  ldc.i4.s   65
  IL_0476:  ldc.i4.0  
  IL_0477:  call       ""bool C.Test(char, bool)""
  IL_047c:  brtrue.s   IL_0492
  IL_047e:  ldc.i4.s   66
  IL_0480:  ldc.i4.0  
  IL_0481:  call       ""bool C.Test(char, bool)""
  IL_0486:  brtrue.s   IL_0492
  IL_0488:  ldc.i4.s   67
  IL_048a:  ldc.i4.0  
  IL_048b:  call       ""bool C.Test(char, bool)""
  IL_0490:  br.s       IL_0493
  IL_0492:  ldc.i4.1  
  IL_0493:  call       ""void System.Console.WriteLine(bool)""
  IL_0498:  ret       
}
");
        }
    }
}