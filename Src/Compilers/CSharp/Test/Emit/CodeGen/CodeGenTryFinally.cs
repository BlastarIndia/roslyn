﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    public class CodeGenTryFinallyTests : CSharpTestBase
    {
        [Fact(Skip = "563799")]
        public void EmptyTryFinally()
        {
            var source =
@"class C
{
    static void EmptyTryFinally()
    {
        try { }
        finally { }
    }
    static void EmptyTryFinallyInTry()
    {
        try
        {
            try { }
            finally { }
        }
        finally { }
    }
    static void EmptyTryFinallyInFinally()
    {
        try { }
        finally
        {
            try { }
            finally { }
        }
    }
}";
            var compilation = CompileAndVerify(source);
            compilation.VerifyIL("C.EmptyTryFinally",
@"{
  // Code size        4 (0x4)
  .maxstack  0
  .try
  {
    IL_0000:  leave.s    IL_0003
  }
  finally
  {
    IL_0002:  endfinally
  }
  IL_0003:  ret       
}");
            compilation.VerifyIL("C.EmptyTryFinallyInTry",
@"{
  // Code size        5 (0x5)
  .maxstack  0
  .try
  {
    .try
    {
      IL_0000:  leave.s    IL_0004
    }
    finally
    {
      IL_0002:  endfinally
    }
  }
  finally
  {
    IL_0003:  endfinally
  }
  IL_0004:  ret       
}");
            compilation.VerifyIL("C.EmptyTryFinallyInFinally",
@"{
  // Code size        7 (0x7)
  .maxstack  0
  .try
  {
    IL_0000:  leave.s    IL_0006
  }
  finally
  {
    .try
    {
      IL_0002:  leave.s    IL_0005
    }
    finally
    {
      IL_0004:  endfinally
    }
    IL_0005:  endfinally
  }
  IL_0006:  ret       
}");
        }

        [Fact]
        public void TryFinally()
        {
            var source =
@"class C
{
    static void Main()
    {
        M(1);
        M(0);
    }
    static void M(int f)
    {
        try
        {
            System.Console.Write(""1, "");
            if (f > 0)
                goto End;
            System.Console.Write(""2, "");
            return;
        }
        finally
        {
            System.Console.Write(""3, "");
        }
    End:
        System.Console.Write(""4, "");
    }
}";
            var compilation = CompileAndVerify(source, expectedOutput: "1, 3, 4, 1, 2, 3, ");
            compilation.VerifyIL("C.M",
@"{
  // Code size       50 (0x32)
  .maxstack  2
  .try
{
  IL_0000:  ldstr      ""1, ""
  IL_0005:  call       ""void System.Console.Write(string)""
  IL_000a:  ldarg.0
  IL_000b:  ldc.i4.0
  IL_000c:  ble.s      IL_0010
  IL_000e:  leave.s    IL_0027
  IL_0010:  ldstr      ""2, ""
  IL_0015:  call       ""void System.Console.Write(string)""
  IL_001a:  leave.s    IL_0031
}
  finally
{
  IL_001c:  ldstr      ""3, ""
  IL_0021:  call       ""void System.Console.Write(string)""
  IL_0026:  endfinally
}
  IL_0027:  ldstr      ""4, ""
  IL_002c:  call       ""void System.Console.Write(string)""
  IL_0031:  ret
}");
        }

        [Fact]
        public void TryCatch()
        {
            var source =
@"using System;
class C
{
    static void Main()
    {
        M(1);
        M(0);
    }
    static void M(int f)
    {
        try
        {
            Console.Write(""before, "");
            N(f);
            Console.Write(""after, "");
        }
        catch (Exception)
        {
            Console.Write(""catch, "");
        }
    }
    static void N(int f)
    {
        if (f > 0)
            throw new Exception();
    }
}";
            var compilation = CompileAndVerify(source, expectedOutput: "before, catch, before, after,");
            compilation.VerifyIL("C.M",
@"{
  // Code size       42 (0x2a)
  .maxstack  1
  .try
  {
    IL_0000:  ldstr      ""before, ""
    IL_0005:  call       ""void System.Console.Write(string)""
    IL_000a:  ldarg.0   
    IL_000b:  call       ""void C.N(int)""
    IL_0010:  ldstr      ""after, ""
    IL_0015:  call       ""void System.Console.Write(string)""
    IL_001a:  leave.s    IL_0029
  }
  catch System.Exception
  {
    IL_001c:  pop       
    IL_001d:  ldstr      ""catch, ""
    IL_0022:  call       ""void System.Console.Write(string)""
    IL_0027:  leave.s    IL_0029
  }
  IL_0029:  ret       
}");
        }

        [Fact]
        public void TryCatch001()
        {
            var source =
@"using System;
class C
{
    static void Main()
    {
    }

    static void M()
    {
        try
        {
            System.Object o = null;
            o.ToString();//null Exception throw

        }
        catch (System.NullReferenceException NullException)//null Exception caught
        {
            try
            {
                throw new System.ApplicationException();//app Exception throw
            }
            catch (System.ApplicationException AppException)//app Exception caught
            {
                throw new System.DivideByZeroException();//Zero Exception throw
            }
        }
    }
}";
            var compilation = CompileAndVerify(source, expectedOutput: "", emitPdb: true);
            compilation.VerifyIL("C.M",
@"{
  // Code size       32 (0x20)
  .maxstack  1
  .locals init (object V_0, //o
  System.NullReferenceException V_1, //NullException
  System.ApplicationException V_2) //AppException
  IL_0000:  nop
  .try
{
  IL_0001:  nop
  IL_0002:  ldnull
  IL_0003:  stloc.0
  IL_0004:  ldloc.0
  IL_0005:  callvirt   ""string object.ToString()""
  IL_000a:  pop
  IL_000b:  nop
  IL_000c:  leave.s    IL_001f
}
  catch System.NullReferenceException
{
  IL_000e:  stloc.1
  IL_000f:  nop
  .try
{
  IL_0010:  nop
  IL_0011:  newobj     ""System.ApplicationException..ctor()""
  IL_0016:  throw
}
  catch System.ApplicationException
{
  IL_0017:  stloc.2
  IL_0018:  nop
  IL_0019:  newobj     ""System.DivideByZeroException..ctor()""
  IL_001e:  throw
}
}
  IL_001f:  ret
}");
        }

        [Fact]
        public void TryCatch002()
        {
            var source =
@"using System;
class C
{
    static void Main()
    {
    }

    static void M()
    {
        try
        {
            System.Object o = null;
            o.ToString();//null Exception throw
            goto l1;
        }
        catch (System.NullReferenceException NullException)//null Exception caught
        {
            try
            {
                throw new System.ApplicationException();//app Exception throw
            }
            catch (System.ApplicationException AppException)//app Exception caught
            {
                throw new System.DivideByZeroException();//Zero Exception throw
            }
        }

        l1:
        ;
        ;
        ;
    }
}";
            var compilation = CompileAndVerify(source, expectedOutput: "", emitPdb: true);
            compilation.VerifyIL("C.M",
@"{
  // Code size       35 (0x23)
  .maxstack  1
  .locals init (object V_0, //o
  System.NullReferenceException V_1, //NullException
  System.ApplicationException V_2) //AppException
  IL_0000:  nop
  .try
{
  IL_0001:  nop
  IL_0002:  ldnull
  IL_0003:  stloc.0
  IL_0004:  ldloc.0
  IL_0005:  callvirt   ""string object.ToString()""
  IL_000a:  pop
  IL_000b:  leave.s    IL_001e
}
  catch System.NullReferenceException
{
  IL_000d:  stloc.1
  IL_000e:  nop
  .try
{
  IL_000f:  nop
  IL_0010:  newobj     ""System.ApplicationException..ctor()""
  IL_0015:  throw
}
  catch System.ApplicationException
{
  IL_0016:  stloc.2
  IL_0017:  nop
  IL_0018:  newobj     ""System.DivideByZeroException..ctor()""
  IL_001d:  throw
}
}
  IL_001e:  nop
  IL_001f:  nop
  IL_0020:  nop
  IL_0021:  nop
  IL_0022:  ret
}");
        }

        [WorkItem(813428)]
        [Fact]
        public void TryCatchOptimized001()
        {
            var source =
@"

using System;
using System.Diagnostics;

class Program
{
    static void Main()
    {
        try
        {
            throw new Exception(""debug only"");
        }
        catch (Exception ex)
        {
            Debug.Write(ex.Message);
        }

        try
        {
            throw new Exception(""hello"");
        }
        catch (Exception ex)
        {
            Console.Write(ex.Message);
        }

        try
        {
            throw new Exception(""bye"");
        }
        catch (Exception ex)
        {
            Console.Write(ex.Message);
            Console.Write(ex.Message);
        }
    }
}
";
            var compilation = CompileAndVerify(source, additionalRefs: new MetadataReference[] { SystemRef }, expectedOutput: "hellobyebye");
            compilation.VerifyIL("Program.Main",
@"
{
  // Code size       74 (0x4a)
  .maxstack  2
  .try
{
  IL_0000:  ldstr      ""debug only""
  IL_0005:  newobj     ""System.Exception..ctor(string)""
  IL_000a:  throw
}
  catch System.Exception
{
  IL_000b:  pop
  IL_000c:  leave.s    IL_000e
}
  IL_000e:  nop
  .try
{
  IL_000f:  ldstr      ""hello""
  IL_0014:  newobj     ""System.Exception..ctor(string)""
  IL_0019:  throw
}
  catch System.Exception
{
  IL_001a:  callvirt   ""string System.Exception.Message.get""
  IL_001f:  call       ""void System.Console.Write(string)""
  IL_0024:  leave.s    IL_0026
}
  IL_0026:  nop
  .try
{
  IL_0027:  ldstr      ""bye""
  IL_002c:  newobj     ""System.Exception..ctor(string)""
  IL_0031:  throw
}
  catch System.Exception
{
  IL_0032:  dup
  IL_0033:  callvirt   ""string System.Exception.Message.get""
  IL_0038:  call       ""void System.Console.Write(string)""
  IL_003d:  callvirt   ""string System.Exception.Message.get""
  IL_0042:  call       ""void System.Console.Write(string)""
  IL_0047:  leave.s    IL_0049
}
  IL_0049:  ret
}
");
        }

        [Fact]
        public void TryFilterOptimized001()
        {
            var source =
@"
using System;

class Program
{
    static void Main()
    {
        try
        {
            throw new Exception(""hello"");
        }
        catch (Exception ex1) if (ex1.Message == null)
        {
        }

        try
        {
            throw new Exception(""bye"");
        }
        catch (Exception ex2) if (F(ex2, ex2))
        {
        }
    }

    private static bool F(Exception e1, Exception e2) 
    {
        return false;
    }
}
";
            var compilation = CompileAndVerify(source);
            compilation.VerifyIL("Program.Main", @"
{
  // Code size       78 (0x4e)
  .maxstack  2
  .try
{
  IL_0000:  ldstr      ""hello""
  IL_0005:  newobj     ""System.Exception..ctor(string)""
  IL_000a:  throw
}
  filter
{
  IL_000b:  isinst     ""System.Exception""
  IL_0010:  dup
  IL_0011:  brtrue.s   IL_0017
  IL_0013:  pop
  IL_0014:  ldc.i4.0
  IL_0015:  br.s       IL_0022
  IL_0017:  callvirt   ""string System.Exception.Message.get""
  IL_001c:  ldnull
  IL_001d:  ceq
  IL_001f:  ldc.i4.0
  IL_0020:  cgt.un
  IL_0022:  endfilter
}  // end filter
{  // handler
  IL_0024:  pop
  IL_0025:  leave.s    IL_0027
}
  IL_0027:  nop
  .try
{
  IL_0028:  ldstr      ""bye""
  IL_002d:  newobj     ""System.Exception..ctor(string)""
  IL_0032:  throw
}
  filter
{
  IL_0033:  isinst     ""System.Exception""
  IL_0038:  dup
  IL_0039:  brtrue.s   IL_003f
  IL_003b:  pop
  IL_003c:  ldc.i4.0
  IL_003d:  br.s       IL_0048
  IL_003f:  dup
  IL_0040:  call       ""bool Program.F(System.Exception, System.Exception)""
  IL_0045:  ldc.i4.0
  IL_0046:  cgt.un
  IL_0048:  endfilter
}  // end filter
{  // handler
  IL_004a:  pop
  IL_004b:  leave.s    IL_004d
}
  IL_004d:  ret
}
");
        }

        [Fact]
        public void TryFilterOptimized002()
        {
            var source =
@"
using System;

class Program
{
    static void Main()
    {
        try
        {
            throw new Exception(""bye"");
        }
        catch (Exception ex) if (F(ex, ex))
        {
            Console.WriteLine(ex);
        }
    }

    private static bool F(Exception e1, Exception e2) 
    {
        return false;
    }
}
";
            var compilation = CompileAndVerify(source);
            compilation.VerifyIL("Program.Main", @"
{
  // Code size       46 (0x2e)
  .maxstack  2
  .locals init (System.Exception V_0) //ex
  .try
{
  IL_0000:  ldstr      ""bye""
  IL_0005:  newobj     ""System.Exception..ctor(string)""
  IL_000a:  throw
}
  filter
{
  IL_000b:  isinst     ""System.Exception""
  IL_0010:  dup
  IL_0011:  brtrue.s   IL_0017
  IL_0013:  pop
  IL_0014:  ldc.i4.0
  IL_0015:  br.s       IL_0022
  IL_0017:  stloc.0
  IL_0018:  ldloc.0
  IL_0019:  dup
  IL_001a:  call       ""bool Program.F(System.Exception, System.Exception)""
  IL_001f:  ldc.i4.0
  IL_0020:  cgt.un
  IL_0022:  endfilter
}  // end filter
{  // handler
  IL_0024:  pop
  IL_0025:  ldloc.0
  IL_0026:  call       ""void System.Console.WriteLine(object)""
  IL_002b:  leave.s    IL_002d
}
  IL_002d:  ret
}
");
        }

        [Fact]
        public void TryFilterOptimized003()
        {
            var source =
@"
using System;

class Program
{
    static void Main()
    {
        try
        {
            throw new Exception(""bye"");
        }
        catch (Exception ex) if (F(ref ex))
        {
            Console.WriteLine(ex);
        }
    }

    private static bool F(ref Exception e) 
    {
        e = null;
        return true;
    }
}
";
            var compilation = CompileAndVerify(source);
            compilation.VerifyIL("Program.Main", @"
{
  // Code size       46 (0x2e)
  .maxstack  2
  .locals init (System.Exception V_0) //ex
  .try
{
  IL_0000:  ldstr      ""bye""
  IL_0005:  newobj     ""System.Exception..ctor(string)""
  IL_000a:  throw
}
  filter
{
  IL_000b:  isinst     ""System.Exception""
  IL_0010:  dup
  IL_0011:  brtrue.s   IL_0017
  IL_0013:  pop
  IL_0014:  ldc.i4.0
  IL_0015:  br.s       IL_0022
  IL_0017:  stloc.0
  IL_0018:  ldloca.s   V_0
  IL_001a:  call       ""bool Program.F(ref System.Exception)""
  IL_001f:  ldc.i4.0
  IL_0020:  cgt.un
  IL_0022:  endfilter
}  // end filter
{  // handler
  IL_0024:  pop
  IL_0025:  ldloc.0
  IL_0026:  call       ""void System.Console.WriteLine(object)""
  IL_002b:  leave.s    IL_002d
}
  IL_002d:  ret
}");
        }

        [Fact]
        public void TryFilterOptimized004()
        {
            var source =
@"
using System;

class Program
{
    static void F<T>() where T : Exception
    {
        try
        {
            throw new Exception(""bye"");
        }
        catch (T ex) if (F(ex, ex))
        {
            Console.WriteLine(ex);
        }
    }

    private static bool F<S>(S e1, S e2) 
    {
        return false;
    }
}
";
            var compilation = CompileAndVerify(source);
            compilation.VerifyIL("Program.F<T>", @"
{
  // Code size       56 (0x38)
  .maxstack  2
  .locals init (T V_0) //ex
  .try
{
  IL_0000:  ldstr      ""bye""
  IL_0005:  newobj     ""System.Exception..ctor(string)""
  IL_000a:  throw
}
  filter
{
  IL_000b:  isinst     ""T""
  IL_0010:  dup
  IL_0011:  brtrue.s   IL_0017
  IL_0013:  pop
  IL_0014:  ldc.i4.0
  IL_0015:  br.s       IL_0027
  IL_0017:  unbox.any  ""T""
  IL_001c:  stloc.0
  IL_001d:  ldloc.0
  IL_001e:  dup
  IL_001f:  call       ""bool Program.F<T>(T, T)""
  IL_0024:  ldc.i4.0
  IL_0025:  cgt.un
  IL_0027:  endfilter
}  // end filter
{  // handler
  IL_0029:  pop
  IL_002a:  ldloc.0
  IL_002b:  box        ""T""
  IL_0030:  call       ""void System.Console.WriteLine(object)""
  IL_0035:  leave.s    IL_0037
}
  IL_0037:  ret
}
");
        }

        [Fact, WorkItem(854935)]
        public void LiftedExceptionVariableInGenericIterator()
        {
            var source = @"
using System;
using System.IO;
using System.Collections.Generic;

class C 
{
	public IEnumerable<int> Iter2<T>() 
	{
		try
		{
			throw new IOException(""Hi"");
        }
		catch (Exception e)
		{
			( (Action) delegate { Console.WriteLine(e.Message); })();
		}
        yield return 1;
	}
 
	static void Main()
    {
        foreach (var x in new C().Iter2<object>()) { }
    }
}";
            CompileAndVerify(source, expectedOutput: "Hi");
        }

        [Fact, WorkItem(854935)]
        public void GenericLiftedExceptionVariableInGenericIterator()
        {
            var source = @"
using System;
using System.IO;
using System.Collections.Generic;

class C
{
	public IEnumerable<int> Iter2<T, E>() where E : Exception
	{
		try
		{
			throw new IOException(""Hi"");
        }
		catch (E e) if (new Func<bool>(() => e.Message != null)())
		{
			( (Action) delegate { Console.WriteLine(e.Message); })();
		}
        yield return 1;
	}
 
	static void Main()
    {
        foreach (var x in new C().Iter2<object, IOException>()) { }
    }
}";
            CompileAndVerify(source, expectedOutput: "Hi");
        }

        [WorkItem(579778)]
        [Fact]
        public void Regression579778()
        {
            var source =
@"
using System;
using System.Security;

[assembly: SecurityTransparent()]

class C
{
    static void Main()
    {
            try
            {
                throw null;
            }
            catch (Exception)
            {
            }
            finally
            {
                try { }
                catch { }
            }


    }
    
}";
            var compilation = CompileAndVerify(source, expectedOutput: "");
            compilation.VerifyIL("C.Main",
@"
{
  // Code size       13 (0xd)
  .maxstack  1
  .try
{
  .try
{
  IL_0000:  ldnull
  IL_0001:  throw
}
  catch System.Exception
{
  IL_0002:  pop
  IL_0003:  leave.s    IL_000c
}
}
  finally
{
  IL_0005:  nop
  .try
{
  IL_0006:  leave.s    IL_000b
}
  catch object
{
  IL_0008:  pop
  IL_0009:  leave.s    IL_000b
}
  IL_000b:  endfinally
}
  IL_000c:  ret
}
");
        }


        [Fact]
        public void NestedExceptionHandlers()
        {
            var source =
@"using System;
class C
{
    static void F(int i)
    {
        if (i == 0)
        {
            throw new Exception(""i == 0"");
        }
        throw new InvalidOperationException(""i != 0"");
    }
    static void M(int i)
    {
        try
        {
            try
            {
                F(i);
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine(""InvalidOperationException: {0}"", e.Message);
                F(i + 1);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(""Exception: {0}"", e.Message);
        }
    }
    static void Main()
    {
        M(0);
        M(1);
    }
}";
            var compilation = CompileAndVerify(source, expectedOutput:
@"Exception: i == 0
InvalidOperationException: i != 0
Exception: i != 0");
            compilation.VerifyIL("C.M",
@"{
  // Code size       55 (0x37)
  .maxstack  2
  .locals init (System.InvalidOperationException V_0, //e
           System.Exception V_1) //e
  .try
  {
    .try
    {
      IL_0000:  ldarg.0   
      IL_0001:  call       ""void C.F(int)""
      IL_0006:  leave.s    IL_0036
    }
    catch System.InvalidOperationException
    {
      IL_0008:  stloc.0   
      IL_0009:  ldstr      ""InvalidOperationException: {0}""
      IL_000e:  ldloc.0   
      IL_000f:  callvirt   ""string System.Exception.Message.get""
      IL_0014:  call       ""void System.Console.WriteLine(string, object)""
      IL_0019:  ldarg.0   
      IL_001a:  ldc.i4.1  
      IL_001b:  add       
      IL_001c:  call       ""void C.F(int)""
      IL_0021:  leave.s    IL_0036
    }
  }
  catch System.Exception
  {
    IL_0023:  stloc.1   
    IL_0024:  ldstr      ""Exception: {0}""
    IL_0029:  ldloc.1   
    IL_002a:  callvirt   ""string System.Exception.Message.get""
    IL_002f:  call       ""void System.Console.WriteLine(string, object)""
    IL_0034:  leave.s    IL_0036
  }
  IL_0036:  ret       
}");
        }

        [Fact]
        public void ThrowInTry()
        {
            var source =
@"using System;
class C
{
    static void ThrowInTry()
    {
        try { throw new Exception(); }
        finally { }
    }
    static void ThrowInTryInTry()
    {
        try
        {
            try { throw new Exception(); }
            finally { }
        }
        finally { }
    }
    static void ThrowInTryInFinally()
    {
        try { }
        finally
        {
            try { throw new Exception(); }
            finally { }
        }
    }
}
class D
{
    static void ThrowInTry()
    {
        try { throw new Exception(); }
        catch { }
        finally { }
    }
    static void ThrowInTryInTry()
    {
        try
        {
            try { throw new Exception(); }
            catch { }
            finally { }
        }
        finally { }
    }

    static void ThrowInTryInFinally()
    {
        try { }
        catch { }
        finally
        {
            try { throw new Exception(); }
            catch { }
            finally { }
        }
    }
}";
            var compilation = CompileAndVerify(source);
            compilation.VerifyIL("C.ThrowInTry",
@"{
  // Code size        7 (0x7)
  .maxstack  1
  .try
  {
    IL_0000:  newobj     ""System.Exception..ctor()""
    IL_0005:  throw     
  }
  finally
  {
    IL_0006:  endfinally
  }
}");
            compilation.VerifyIL("C.ThrowInTryInTry",
@"{
  // Code size        8 (0x8)
  .maxstack  1
  .try
{
  .try
{
  IL_0000:  newobj     ""System.Exception..ctor()""
  IL_0005:  throw
}
  finally
{
  IL_0006:  endfinally
}
}
  finally
{
  IL_0007:  endfinally
}
}
");
            compilation.VerifyIL("C.ThrowInTryInFinally",
@"{
  // Code size       12 (0xc)
  .maxstack  1
  .try
{
  IL_0000:  leave.s    IL_000a
}
  finally
{
  IL_0002:  nop
  .try
{
  IL_0003:  newobj     ""System.Exception..ctor()""
  IL_0008:  throw
}
  finally
{
  IL_0009:  endfinally
}
}
  IL_000a:  br.s       IL_000a
}");
            compilation.VerifyIL("D.ThrowInTry",
@"{
  // Code size       11 (0xb)
  .maxstack  1
  .try
  {
    .try
    {
      IL_0000:  newobj     ""System.Exception..ctor()""
      IL_0005:  throw     
    }
    catch object
    {
      IL_0006:  pop       
      IL_0007:  leave.s    IL_000a
    }
  }
  finally
  {
    IL_0009:  endfinally
  }
  IL_000a:  ret       
}");
            compilation.VerifyIL("D.ThrowInTryInTry",
@"{
  // Code size       12 (0xc)
  .maxstack  1
  .try
  {
    .try
    {
      .try
      {
        IL_0000:  newobj     ""System.Exception..ctor()""
        IL_0005:  throw     
      }
      catch object
      {
        IL_0006:  pop       
        IL_0007:  leave.s    IL_000b
      }
    }
    finally
    {
      IL_0009:  endfinally
    }
  }
  finally
  {
    IL_000a:  endfinally
  }
  IL_000b:  ret       
}");
            compilation.VerifyIL("D.ThrowInTryInFinally",
@"{
  // Code size       18 (0x12)
  .maxstack  1
  .try
{
  .try
{
  IL_0000:  leave.s    IL_0011
}
  catch object
{
  IL_0002:  pop
  IL_0003:  leave.s    IL_0011
}
}
  finally
{
  IL_0005:  nop
  .try
{
  .try
{
  IL_0006:  newobj     ""System.Exception..ctor()""
  IL_000b:  throw
}
  catch object
{
  IL_000c:  pop
  IL_000d:  leave.s    IL_0010
}
}
  finally
{
  IL_000f:  endfinally
}
  IL_0010:  endfinally
}
  IL_0011:  ret
}
");
        }

        [WorkItem(540716)]
        [Fact(Skip = "563799")]
        public void ThrowInFinally()
        {
            var source =
@"using System;
class C
{
    static void ThrowInFinally()
    {
        try { }
        finally { throw new Exception(); }
    }
    static void ThrowInFinallyInTry()
    {
        try
        {
            try { }
            finally { throw new Exception(); }
        }
        finally { }
    }
    static int ThrowInFinallyInFinally()
    {
        try { }
        finally
        {
            try { }
            finally { throw new Exception(); }
        }

        // unreachable and has no return
        System.Console.WriteLine();
    }
}
class D
{
    static void ThrowInFinally()
    {
        try { }
        catch { }
        finally { throw new Exception(); }
    }
    static void ThrowInFinallyInTry()
    {
        try
        {
            try { }
            catch { }
            finally { throw new Exception(); }
        }
        catch { }
        finally { }
    }
    static void ThrowInFinallyInFinally()
    {
        try { }
        catch { }
        finally
        {
            try { }
            catch { }
            finally { throw new Exception(); }
        }
    }
}";
            var compilation = CompileAndVerify(source);
            compilation.VerifyIL("C.ThrowInFinally",
@"{
  // Code size       10 (0xa)
  .maxstack  1
  .try
  {
    IL_0000:  leave.s    IL_0008
  }
  finally
  {
    IL_0002:  newobj     ""System.Exception..ctor()""
    IL_0007:  throw     
  }
  IL_0008:  br.s       IL_0008
}");
            compilation.VerifyIL("C.ThrowInFinallyInTry",
@"{
  // Code size       11 (0xb)
  .maxstack  1
  .try
  {
    .try
    {
      IL_0000:  leave.s    IL_0008
    }
    finally
    {
      IL_0002:  newobj     ""System.Exception..ctor()""
      IL_0007:  throw     
    }
    IL_0008:  br.s       IL_0008
  }
  finally
  {
    IL_000a:  endfinally
  }
}");
            compilation.VerifyIL("C.ThrowInFinallyInFinally",
@"{
  // Code size       14 (0xe)
  .maxstack  1
  .try
  {
    IL_0000:  leave.s    IL_000c
  }
  finally
  {
    .try
    {
      IL_0002:  leave.s    IL_000a
    }
    finally
    {
      IL_0004:  newobj     ""System.Exception..ctor()""
      IL_0009:  throw     
    }
    IL_000a:  br.s       IL_000a
  }
  IL_000c:  br.s       IL_000c
}");
            compilation.VerifyIL("D.ThrowInFinally",
@"{
  // Code size       13 (0xd)
  .maxstack  1
  .try
  {
    .try
    {
      IL_0000:  leave.s    IL_000b
    }
    catch object
    {
      IL_0002:  pop       
      IL_0003:  leave.s    IL_000b
    }
  }
  finally
  {
    IL_0005:  newobj     ""System.Exception..ctor()""
    IL_000a:  throw     
  }
  IL_000b:  br.s       IL_000b
}");
            compilation.VerifyIL("D.ThrowInFinallyInTry",
@"{
  // Code size       18 (0x12)
  .maxstack  1
  .try
  {
    .try
    {
      .try
      {
        .try
        {
          IL_0000:  leave.s    IL_000b
        }
        catch object
        {
          IL_0002:  pop       
          IL_0003:  leave.s    IL_000b
        }
      }
      finally
      {
        IL_0005:  newobj     ""System.Exception..ctor()""
        IL_000a:  throw     
      }
      IL_000b:  br.s       IL_000b
    }
    catch object
    {
      IL_000d:  pop       
      IL_000e:  leave.s    IL_0011
    }
  }
  finally
  {
    IL_0010:  endfinally
  }
  IL_0011:  ret       
}");
            compilation.VerifyIL("D.ThrowInFinallyInFinally",
@"{
  // Code size       20 (0x14)
  .maxstack  1
  .try
  {
    .try
    {
      IL_0000:  leave.s    IL_0012
    }
    catch object
    {
      IL_0002:  pop       
      IL_0003:  leave.s    IL_0012
    }
  }
  finally
  {
    .try
    {
      .try
      {
        IL_0005:  leave.s    IL_0010
      }
      catch object
      {
        IL_0007:  pop       
        IL_0008:  leave.s    IL_0010
      }
    }
    finally
    {
      IL_000a:  newobj     ""System.Exception..ctor()""
      IL_000f:  throw     
    }
    IL_0010:  br.s       IL_0010
  }
  IL_0012:  br.s       IL_0012
}
");
        }

        [Fact]
        public void TryFilterSimple()
        {
            var src = @"
using System;
class C
{
    static bool Filter()
    {
        Console.Write(""Filter"");
        return true;
    }

    static void Main()
    {
        int x = 0;
        
        try
        {
            Console.Write(""Try"");
            x = x / x;
        }
        catch if (Filter())
        {
            Console.Write(""Catch"");
        }
        finally
        {
            Console.Write(""Finally"");
        }
    }
}";
            var comp = CompileAndVerify(src,
                expectedOutput: "TryFilterCatchFinally");
            comp.VerifyIL("C.Main", @"
{
  // Code size       66 (0x42)
  .maxstack  2
  .locals init (int V_0) //x
  IL_0000:  ldc.i4.0
  IL_0001:  stloc.0
  .try
{
  .try
{
  IL_0002:  ldstr      ""Try""
  IL_0007:  call       ""void System.Console.Write(string)""
  IL_000c:  ldloc.0
  IL_000d:  dup
  IL_000e:  div
  IL_000f:  stloc.0
  IL_0010:  leave.s    IL_0041
}
  filter
{
  IL_0012:  isinst     ""object""
  IL_0017:  dup
  IL_0018:  brtrue.s   IL_001e
  IL_001a:  pop
  IL_001b:  ldc.i4.0
  IL_001c:  br.s       IL_0027
  IL_001e:  pop
  IL_001f:  call       ""bool C.Filter()""
  IL_0024:  ldc.i4.0
  IL_0025:  cgt.un
  IL_0027:  endfilter
}  // end filter
{  // handler
  IL_0029:  pop
  IL_002a:  ldstr      ""Catch""
  IL_002f:  call       ""void System.Console.Write(string)""
  IL_0034:  leave.s    IL_0041
}
}
  finally
{
  IL_0036:  ldstr      ""Finally""
  IL_003b:  call       ""void System.Console.Write(string)""
  IL_0040:  endfinally
}
  IL_0041:  ret
}");
        }

        [Fact]
        public void TryFilterUseException()
        {
            var src = @"
using System;
class C
{
    static void Main()
    {
        int x = 0;

        try
        {
            Console.Write(""Try"");
            x = x / x;
        }
        catch (DivideByZeroException e)
              if (e.Message == null)
        {
            Console.Write(""Catch1"");
        }
        catch (DivideByZeroException e)
               if(e.Message != null)
        {
            Console.Write(""Catch2"" + e.Message.Length);
        }
        finally
        {
            Console.Write(""Finally"");
        }
    }
}";
        CompileAndVerify(src, expectedOutput: "TryCatch228Finally").
            VerifyIL("C.Main", @"
{
  // Code size      129 (0x81)
  .maxstack  2
  .locals init (int V_0, //x
  System.DivideByZeroException V_1) //e
  IL_0000:  ldc.i4.0
  IL_0001:  stloc.0
  .try
{
  .try
{
  IL_0002:  ldstr      ""Try""
  IL_0007:  call       ""void System.Console.Write(string)""
  IL_000c:  ldloc.0
  IL_000d:  dup
  IL_000e:  div
  IL_000f:  stloc.0
  IL_0010:  leave.s    IL_0080
}
  filter
{
  IL_0012:  isinst     ""System.DivideByZeroException""
  IL_0017:  dup
  IL_0018:  brtrue.s   IL_001e
  IL_001a:  pop
  IL_001b:  ldc.i4.0
  IL_001c:  br.s       IL_0029
  IL_001e:  callvirt   ""string System.Exception.Message.get""
  IL_0023:  ldnull
  IL_0024:  ceq
  IL_0026:  ldc.i4.0
  IL_0027:  cgt.un
  IL_0029:  endfilter
}  // end filter
{  // handler
  IL_002b:  pop
  IL_002c:  ldstr      ""Catch1""
  IL_0031:  call       ""void System.Console.Write(string)""
  IL_0036:  leave.s    IL_0080
}
  filter
{
  IL_0038:  isinst     ""System.DivideByZeroException""
  IL_003d:  dup
  IL_003e:  brtrue.s   IL_0044
  IL_0040:  pop
  IL_0041:  ldc.i4.0
  IL_0042:  br.s       IL_0051
  IL_0044:  stloc.1
  IL_0045:  ldloc.1
  IL_0046:  callvirt   ""string System.Exception.Message.get""
  IL_004b:  ldnull
  IL_004c:  cgt.un
  IL_004e:  ldc.i4.0
  IL_004f:  cgt.un
  IL_0051:  endfilter
}  // end filter
{  // handler
  IL_0053:  pop
  IL_0054:  ldstr      ""Catch2""
  IL_0059:  ldloc.1
  IL_005a:  callvirt   ""string System.Exception.Message.get""
  IL_005f:  callvirt   ""int string.Length.get""
  IL_0064:  box        ""int""
  IL_0069:  call       ""string string.Concat(object, object)""
  IL_006e:  call       ""void System.Console.Write(string)""
  IL_0073:  leave.s    IL_0080
}
}
  finally
{
  IL_0075:  ldstr      ""Finally""
  IL_007a:  call       ""void System.Console.Write(string)""
  IL_007f:  endfinally
}
  IL_0080:  ret
}
");
        }

        [Fact]
        public void TryFilterScoping()
        {
            var src = @"
using System;
class C
{
    private static string str = ""S1"";

    static void Main()
    {
        int x = 0;

        try
        {
            Console.Write(""Try"");
            x = x / x;
        }
        catch if (new Func<bool>(() => str.Length == 2)())
        {
            Console.Write(""Catch"" + str);
        }
        finally
        {
            Console.Write(""Finally"");
        }
    }
}";
            var comp = CompileAndVerify(src, expectedOutput: "TryCatchS1Finally");
            comp.VerifyIL("C.Main", @"
{
  // Code size      103 (0x67)
  .maxstack  2
  .locals init (int V_0) //x
  IL_0000:  ldc.i4.0
  IL_0001:  stloc.0
  .try
{
  .try
{
  IL_0002:  ldstr      ""Try""
  IL_0007:  call       ""void System.Console.Write(string)""
  IL_000c:  ldloc.0
  IL_000d:  dup
  IL_000e:  div
  IL_000f:  stloc.0
  IL_0010:  leave.s    IL_0066
}
  filter
{
  IL_0012:  isinst     ""object""
  IL_0017:  dup
  IL_0018:  brtrue.s   IL_001e
  IL_001a:  pop
  IL_001b:  ldc.i4.0
  IL_001c:  br.s       IL_0042
  IL_001e:  pop
  IL_001f:  ldsfld     ""System.Func<bool> C.CS$<>9__CachedAnonymousMethodDelegate1""
  IL_0024:  dup
  IL_0025:  brtrue.s   IL_003a
  IL_0027:  pop
  IL_0028:  ldnull
  IL_0029:  ldftn      ""bool C.<Main>b__0()""
  IL_002f:  newobj     ""System.Func<bool>..ctor(object, System.IntPtr)""
  IL_0034:  dup
  IL_0035:  stsfld     ""System.Func<bool> C.CS$<>9__CachedAnonymousMethodDelegate1""
  IL_003a:  callvirt   ""bool System.Func<bool>.Invoke()""
  IL_003f:  ldc.i4.0
  IL_0040:  cgt.un
  IL_0042:  endfilter
}  // end filter
{  // handler
  IL_0044:  pop
  IL_0045:  ldstr      ""Catch""
  IL_004a:  ldsfld     ""string C.str""
  IL_004f:  call       ""string string.Concat(string, string)""
  IL_0054:  call       ""void System.Console.Write(string)""
  IL_0059:  leave.s    IL_0066
}
}
  finally
{
  IL_005b:  ldstr      ""Finally""
  IL_0060:  call       ""void System.Console.Write(string)""
  IL_0065:  endfinally
}
  IL_0066:  ret
}");
        }

        [Fact]
        public void TryCatchWithReturnValue()
        {
            var source =
@"using System;
class C
{
    static int F(int i)
    {
        if (i == 0)
        {
            throw new InvalidOperationException();
        }
        else if (i == 1)
        {
            throw new InvalidCastException();
        }
        return i;
    }
    static int M(int i)
    {
        try
        {
            return F(i) * 3;
        }
        catch (InvalidOperationException)
        {
            return (i - 1) * 4;
        }
        catch
        {
        }
        finally
        {
            i = i + 10;
        }
        return i;
    }
    static void Main()
    {
        Console.WriteLine(""M(0)={0}, M(1)={1}, M(2)={2}"", M(0), M(1), M(2));
    }
}";
            var compilation = CompileAndVerify(source, expectedOutput: "M(0)=-4, M(1)=11, M(2)=6");
            compilation.VerifyIL("C.M",
@"{
  // Code size       34 (0x22)
  .maxstack  2
  .locals init (int V_0)
  .try
  {
    .try
    {
      IL_0000:  ldarg.0   
      IL_0001:  call       ""int C.F(int)""
      IL_0006:  ldc.i4.3  
      IL_0007:  mul       
      IL_0008:  stloc.0   
      IL_0009:  leave.s    IL_0020
    }
    catch System.InvalidOperationException
    {
      IL_000b:  pop       
      IL_000c:  ldarg.0   
      IL_000d:  ldc.i4.1  
      IL_000e:  sub       
      IL_000f:  ldc.i4.4  
      IL_0010:  mul       
      IL_0011:  stloc.0   
      IL_0012:  leave.s    IL_0020
    }
    catch object
    {
      IL_0014:  pop       
      IL_0015:  leave.s    IL_001e
    }
  }
  finally
  {
    IL_0017:  ldarg.0   
    IL_0018:  ldc.i4.s   10
    IL_001a:  add       
    IL_001b:  starg.s    V_0
    IL_001d:  endfinally
  }
  IL_001e:  ldarg.0   
  IL_001f:  ret       
  IL_0020:  ldloc.0   
  IL_0021:  ret       
}");
        }

        [Fact]
        public void Rethrow()
        {
            var source =
@"using System.IO;
class C
{
    static int F = 0;
    static void M()
    {
        try
        {
        }
        catch (FileNotFoundException e)
        {
            if (F > 0)
            {
                throw;
            }
            else
            {
                throw e;
            }
        }
        catch (IOException)
        {
            throw;
        }
        catch
        {
            throw;
        }
    }
}";
            var compilation = CompileAndVerify(source);
            compilation.VerifyIL("C.M",
@"{
  // Code size       22 (0x16)
  .maxstack  2
  .locals init (System.IO.FileNotFoundException V_0) //e
  .try
  {
    IL_0000:  leave.s    IL_0015
  }
  catch System.IO.FileNotFoundException
  {
    IL_0002:  stloc.0   
    IL_0003:  ldsfld     ""int C.F""
    IL_0008:  ldc.i4.0  
    IL_0009:  ble.s      IL_000d
    IL_000b:  rethrow   
    IL_000d:  ldloc.0   
    IL_000e:  throw     
  }
  catch System.IO.IOException
  {
    IL_000f:  pop       
    IL_0010:  rethrow   
  }
  catch object
  {
    IL_0012:  pop       
    IL_0013:  rethrow   
  }
  IL_0015:  ret       
}");
        }

        [WorkItem(541494)]
        [Fact]
        public void CatchT()
        {
            var source =
@"using System;
class C
{
    internal static void TryCatch<T>() where T : Exception
    {
        try
        {
            Throw();
        }
        catch (T t)
        {
            Catch(t);
        }
    }
    static void Throw() { throw new NotImplementedException(); }
    static void Catch(Exception e)
    {
        Console.WriteLine(""Handled"");
    }
    static void Main()
    {
        try
        {
            TryCatch<NotImplementedException>();
            TryCatch<InvalidOperationException>();
        }
        catch (Exception)
        {
            Console.WriteLine(""Unhandled"");
        }
    }
}";
            var compilation = CompileAndVerify(source, expectedOutput:
@"Handled
Unhandled");
            compilation.VerifyIL("C.TryCatch<T>()",
@"
{
  // Code size       25 (0x19)
  .maxstack  1
  .try
{
  IL_0000:  call       ""void C.Throw()""
  IL_0005:  leave.s    IL_0018
}
  catch T
{
  IL_0007:  unbox.any  ""T""
  IL_000c:  box        ""T""
  IL_0011:  call       ""void C.Catch(System.Exception)""
  IL_0016:  leave.s    IL_0018
}
  IL_0018:  ret
}
");
        }

        [WorkItem(540664)]
        [Fact]
        public void ExceptionAlreadyCaught1()
        {
            var source =
@"class C
{
    static void M()
    {
        try { }
        catch (System.Exception) { }
        catch { }
    }
}";
            CompileAndVerify(source).VerifyDiagnostics(
                Diagnostic(ErrorCode.WRN_UnreachableGeneralCatch, "catch").WithLocation(7, 9));
        }

        [Fact]
        public void ExceptionAlreadyCaught2()
        {
            var text = @"
class Program
{
    static void M()
    {
        int a = 1;
        try { }
        catch (System.Exception) { }
        catch if (a == 1) { }
    }
}
";
            CreateCompilationWithMscorlib(text).VerifyDiagnostics(
                // (9,9): warning CS1058: A previous catch clause already catches all exceptions. All non-exceptions thrown will be wrapped in a System.Runtime.CompilerServices.RuntimeWrappedException.
                //         catch if (a == 1) { }
                Diagnostic(ErrorCode.WRN_UnreachableGeneralCatch, "catch").WithLocation(9, 9));
        }

        [Fact]
        public void ExceptionAlreadyCaught3()
        {
            var text = @"
class Program
{
    static void M()
    {
        int a = 1;
        try { }
        catch if (a == 2) { }
        catch (System.Exception) if (a == 3) { }
        catch { }
    }
}
";
            CreateCompilationWithMscorlib(text).VerifyDiagnostics();
        }

        [WorkItem(540666)]
        [Fact]
        public void EmptyTryFinally_Simple()
        {
            var source =
@"class C
{
    static void M()
    {
        try { }
        finally { }
    }
}";
            CompileAndVerify(source);
        }

        [WorkItem(542002)]
        [Fact]
        public void ConditionInTry()
        {
            var source =
@"using System;
class Program
{
    static void Main()
    {
        int a = 10;
        try
        {
            if (a == 3)
            {
            }
        }
        catch (System.Exception)
        {
            
        }
    }
}";

            var compilation = CompileAndVerify(source, expectedOutput: "");
            compilation.VerifyIL("Program.Main",
@"
{
  // Code size       13 (0xd)
  .maxstack  2
  .locals init (int V_0) //a
  IL_0000:  ldc.i4.s   10
  IL_0002:  stloc.0
  .try
{
  IL_0003:  ldloc.0
  IL_0004:  ldc.i4.3
  IL_0005:  pop
  IL_0006:  pop
  IL_0007:  leave.s    IL_000c
}
  catch System.Exception
{
  IL_0009:  pop
  IL_000a:  leave.s    IL_000c
}
  IL_000c:  ret
}
");
        }

        [Fact(), WorkItem(544911)]
        public void UnreachableAfterTryFinally()
        {
            var source = @"
using System;

class Program
{
    static int Result = 0;
    static void F()
    {
        Result = 1;
    }

    static void T1()
    {
        try
        {
            goto L;
        }
        finally
        {
            for (; ; ) { }
        }
    L:
        F();        //unreachable
    }

    static void Main()
    {
        Console.Write(Result);
    }
}
";
            var compilation = CompileAndVerify(source, expectedOutput: "0");
            compilation.VerifyIL("Program.T1",
@"
{
  // Code size        6 (0x6)
  .maxstack  0
  .try
{
  IL_0000:  leave.s    IL_0004
}
  finally
{
  IL_0002:  br.s       IL_0002
}
  IL_0004:  br.s       IL_0004
}
");
        }

        [Fact(), WorkItem(544911)]
        public void ReachableAfterBlockingCatch()
        {
            var source =
@"using System;

    class Program
    {
        static void F()
        {
            Console.WriteLine(""hello"");
        }

        static void T1()
        {
            try
            {
                
            }
            catch 
            {
                for (; ; ) { }
            }

            F();        //reachable
        }

        static void Main()
        {
            T1();
        }
    }
";

            var compilation = CompileAndVerify(source, expectedOutput: "hello");
            compilation.VerifyIL("Program.T1",
@"{
  // Code size       11 (0xb)
  .maxstack  1
  .try
{
  IL_0000:  leave.s    IL_0005
}
  catch object
{
  IL_0002:  pop
  IL_0003:  br.s       IL_0003
}
  IL_0005:  call       ""void Program.F()""
  IL_000a:  ret
}
");
        }

        [Fact(), WorkItem(544911)]
        public void UnreachableAfterTryFinallyConditional()
        {
            var source = @"
using System;

class Program
{
    static int Result = 0;
    static void F()
    {
        Result = 1;
    }

    static void T1()
    {
        try
        {
            if (Result == 0)
                goto L;
        }
        finally
        {
            for (; ; ) { }
        }
    L:
        F();        //unreachable
    }

    static void Main()
    {
        Console.Write(Result);
    }
}
";
            var compilation = CompileAndVerify(source, expectedOutput: "0");
            compilation.VerifyIL("Program.T1",
@"
{
  // Code size       12 (0xc)
  .maxstack  1
  .try
{
  IL_0000:  ldsfld     ""int Program.Result""
  IL_0005:  pop
  IL_0006:  leave.s    IL_000a
}
  finally
{
  IL_0008:  br.s       IL_0008
}
  IL_000a:  br.s       IL_000a
}
");
        }

        [Fact()]
        public void ReachableAfterFinallyButNotFromTryConditional01()
        {
            var source = @"
using System;

class Program
{
    static int Result = 0;
    static void F()
    {
        Result = 1;
    }

    static void T1()
    {
        if (Result == 1)
            goto L;

        try
        {
            try
            {
                if (Result == 0)
                    goto L;

                goto L;
            }
            finally
            {
                for (; ; ) { }
            }

            if (Result == 0)
                goto L;
        }
        finally
        {
            for (; ; ) { }
        }

    L:
        F();        //reachable but not from the try
    }

    static void Main()
    {
        Console.Write(Result);
    }
}
";
            var compilation = CompileAndVerify(source, expectedOutput: "0");
            compilation.VerifyIL("Program.T1",
@"
{
  // Code size       29 (0x1d)
  .maxstack  2
  IL_0000:  ldsfld     ""int Program.Result""
  IL_0005:  ldc.i4.1
  IL_0006:  beq.s      IL_0017
  IL_0008:  nop
  .try
{
  .try
{
  IL_0009:  ldsfld     ""int Program.Result""
  IL_000e:  pop
  IL_000f:  leave.s    IL_0013
}
  finally
{
  IL_0011:  br.s       IL_0011
}
  IL_0013:  br.s       IL_0013
}
  finally
{
  IL_0015:  br.s       IL_0015
}
  IL_0017:  call       ""void Program.F()""
  IL_001c:  ret
}
");
        }


        [Fact(), WorkItem(544911)]
        public void ReachableAfterFinallyButNotFromTryConditional()
        {
            var source = @"
using System;

class Program
{
    static int Result = 0;
    static void F()
    {
        Result = 1;
    }

    static void T1()
    {
        if (Result == 1)
            goto L;

        try
        {
            try
            {
                if (Result == 0)
                    goto L;
            }
            finally
            {
                for (; ; ) { }
            }

            if (Result == 0)
                goto L;
        }
        finally
        {
            for (; ; ) { }
        }

    L:
        F();        //reachable but not from the try
    }

    static void Main()
    {
        Console.Write(Result);
    }
}
";
            var compilation = CompileAndVerify(source, expectedOutput: "0");
            compilation.VerifyIL("Program.T1",
@"
{
  // Code size       29 (0x1d)
  .maxstack  2
  IL_0000:  ldsfld     ""int Program.Result""
  IL_0005:  ldc.i4.1
  IL_0006:  beq.s      IL_0017
  IL_0008:  nop
  .try
{
  .try
{
  IL_0009:  ldsfld     ""int Program.Result""
  IL_000e:  pop
  IL_000f:  leave.s    IL_0013
}
  finally
{
  IL_0011:  br.s       IL_0011
}
  IL_0013:  br.s       IL_0013
}
  finally
{
  IL_0015:  br.s       IL_0015
}
  IL_0017:  call       ""void Program.F()""
  IL_001c:  ret
}
");
        }

        [Fact(), WorkItem(713418)]
        public void ConditionalUnconditionalBranches()
        {
            var source = @"
using System;

    class Program
    {
        private static int x = 123;

        static void Main(string[] args)
        {
            try
            {
                if (x == 10)
                {
                    goto l2;;
                }


                if (x != 0)
                {
                    Console.WriteLine(""2"");
                }
                goto l1;

l2:
                Console.WriteLine(""l2"");

l1:
                goto lOut;
            }
            finally
            {
                Console.WriteLine(""Finally"");
            }

lOut:
            Console.WriteLine(""Out"");
        }
    }
";
            var compilation = CompileAndVerify(source, expectedOutput: @"2
Finally
Out");
            compilation.VerifyIL("Program.Main",
@"
{
  // Code size       62 (0x3e)
  .maxstack  2
  .try
{
  IL_0000:  ldsfld     ""int Program.x""
  IL_0005:  ldc.i4.s   10
  IL_0007:  beq.s      IL_001c
  IL_0009:  ldsfld     ""int Program.x""
  IL_000e:  brfalse.s  IL_0026
  IL_0010:  ldstr      ""2""
  IL_0015:  call       ""void System.Console.WriteLine(string)""
  IL_001a:  leave.s    IL_0033
  IL_001c:  ldstr      ""l2""
  IL_0021:  call       ""void System.Console.WriteLine(string)""
  IL_0026:  leave.s    IL_0033
}
  finally
{
  IL_0028:  ldstr      ""Finally""
  IL_002d:  call       ""void System.Console.WriteLine(string)""
  IL_0032:  endfinally
}
  IL_0033:  ldstr      ""Out""
  IL_0038:  call       ""void System.Console.WriteLine(string)""
  IL_003d:  ret
}
");
        }


        [Fact(), WorkItem(713418)]
        public void ConditionalUnconditionalBranches001()
        {
            var source = @"
using System;

    class Program
    {
        private static int x = 123;

        static void Main(string[] args)
        {
            try
            {
                if (x == 10)
                {
                    goto l2;;
                }


                if (x != 0)
                {
                    Console.WriteLine(""2"");
                }
                goto l1;

l2:
                // Console.WriteLine(""l2"");

l1:
                goto lOut;
            }
            finally
            {
                Console.WriteLine(""Finally"");
            }

lOut:
            Console.WriteLine(""Out"");
        }
    }
";
            var compilation = CompileAndVerify(source, expectedOutput: @"2
Finally
Out");
            compilation.VerifyIL("Program.Main",
@"
{
  // Code size       52 (0x34)
  .maxstack  2
  .try
{
  IL_0000:  ldsfld     ""int Program.x""
  IL_0005:  ldc.i4.s   10
  IL_0007:  bne.un.s   IL_000b
  IL_0009:  leave.s    IL_0029
  IL_000b:  ldsfld     ""int Program.x""
  IL_0010:  brfalse.s  IL_001c
  IL_0012:  ldstr      ""2""
  IL_0017:  call       ""void System.Console.WriteLine(string)""
  IL_001c:  leave.s    IL_0029
}
  finally
{
  IL_001e:  ldstr      ""Finally""
  IL_0023:  call       ""void System.Console.WriteLine(string)""
  IL_0028:  endfinally
}
  IL_0029:  ldstr      ""Out""
  IL_002e:  call       ""void System.Console.WriteLine(string)""
  IL_0033:  ret
}
");
        }


    }
}
