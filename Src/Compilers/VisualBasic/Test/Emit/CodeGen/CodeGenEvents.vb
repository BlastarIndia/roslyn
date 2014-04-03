﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests.Emit
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests
    Public Class CodeGenEvents
        Inherits BasicTestBase

        <Fact()>
        Public Sub SimpleAddHandler()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
Imports System

Module MyClass1
    Sub Main(args As String())
        Dim del As System.EventHandler =
            Sub(sender As Object, a As EventArgs) Console.Write("unload")

        Dim v = AppDomain.CreateDomain("qq")

        AddHandler (v.DomainUnload), del

        AppDomain.Unload(v)    
    End Sub
End Module
    </file>
    </compilation>).
                VerifyIL("MyClass1.Main",
            <![CDATA[
{
  // Code size       56 (0x38)
  .maxstack  3
  .locals init (System.EventHandler V_0) //del
  IL_0000:  ldsfld     "MyClass1._ClosureCache$__2 As System.EventHandler"
  IL_0005:  brfalse.s  IL_000e
  IL_0007:  ldsfld     "MyClass1._ClosureCache$__2 As System.EventHandler"
  IL_000c:  br.s       IL_0020
  IL_000e:  ldnull
  IL_000f:  ldftn      "Sub MyClass1._Lambda$__1(Object, System.EventArgs)"
  IL_0015:  newobj     "Sub System.EventHandler..ctor(Object, System.IntPtr)"
  IL_001a:  dup
  IL_001b:  stsfld     "MyClass1._ClosureCache$__2 As System.EventHandler"
  IL_0020:  stloc.0
  IL_0021:  ldstr      "qq"
  IL_0026:  call       "Function System.AppDomain.CreateDomain(String) As System.AppDomain"
  IL_002b:  dup
  IL_002c:  ldloc.0
  IL_002d:  callvirt   "Sub System.AppDomain.add_DomainUnload(System.EventHandler)"
  IL_0032:  call       "Sub System.AppDomain.Unload(System.AppDomain)"
  IL_0037:  ret
}
]]>)
        End Sub

        <Fact()>
        Public Sub SimpleRemoveHandler()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
Imports System

Module MyClass1
    Sub Main(args As String())
        Dim del As System.EventHandler =
            Sub(sender As Object, a As EventArgs) Console.Write("unload")

        Dim v = AppDomain.CreateDomain("qq")

        AddHandler (v.DomainUnload), del
        RemoveHandler (v.DomainUnload), del

        AppDomain.Unload(v)    
    End Sub
End Module
    </file>
    </compilation>).
                VerifyIL("MyClass1.Main",
            <![CDATA[
{
  // Code size       63 (0x3f)
  .maxstack  3
  .locals init (System.EventHandler V_0) //del
  IL_0000:  ldsfld     "MyClass1._ClosureCache$__2 As System.EventHandler"
  IL_0005:  brfalse.s  IL_000e
  IL_0007:  ldsfld     "MyClass1._ClosureCache$__2 As System.EventHandler"
  IL_000c:  br.s       IL_0020
  IL_000e:  ldnull
  IL_000f:  ldftn      "Sub MyClass1._Lambda$__1(Object, System.EventArgs)"
  IL_0015:  newobj     "Sub System.EventHandler..ctor(Object, System.IntPtr)"
  IL_001a:  dup
  IL_001b:  stsfld     "MyClass1._ClosureCache$__2 As System.EventHandler"
  IL_0020:  stloc.0
  IL_0021:  ldstr      "qq"
  IL_0026:  call       "Function System.AppDomain.CreateDomain(String) As System.AppDomain"
  IL_002b:  dup
  IL_002c:  ldloc.0
  IL_002d:  callvirt   "Sub System.AppDomain.add_DomainUnload(System.EventHandler)"
  IL_0032:  dup
  IL_0033:  ldloc.0
  IL_0034:  callvirt   "Sub System.AppDomain.remove_DomainUnload(System.EventHandler)"
  IL_0039:  call       "Sub System.AppDomain.Unload(System.AppDomain)"
  IL_003e:  ret
}
    ]]>)
        End Sub

        <Fact()>
        Public Sub AddHandlerWithLambdaConversion()
            Dim csdllCompilation = CreateCSharpCompilation("CSDll",
            <![CDATA[
public class CSDllClass
{
    public event System.Action E1;

    public void Raise()
    {
        E1();
    }
}

public class CSDllClasDerived : CSDllClass
{

}

]]>,
                compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            csdllCompilation.VerifyDiagnostics()

            Dim vbexeCompilation = CreateVisualBasicCompilation("VBExe",
            <![CDATA[Imports System

Public Class VBExeClass

End Class

Public Module Program
    Sub Main(args As String())
        Dim o As New CSDllClasDerived

        AddHandler o.E1, Sub() Console.Write("hi ")
        AddHandler o.E1, AddressOf H1

        o.Raise
    End Sub

    Sub H1()
        Console.Write("bye")
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication),
                referencedCompilations:={csdllCompilation})
            Dim vbexeVerifier = CompileAndVerify(vbexeCompilation,
                expectedOutput:=<![CDATA[hi bye]]>)
            vbexeVerifier.VerifyDiagnostics()
        End Sub

        <Fact()>
        Public Sub AddHandlerWithDelegateRelaxation()
            Dim csdllCompilation = CreateCSharpCompilation("CSDll",
            <![CDATA[
public class CSDllClass
{
    public event System.Action<int> E1;

    public void Raise()
    {
        E1(42);
    }
}

public class CSDllClasDerived : CSDllClass
{

}

]]>,
                compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            csdllCompilation.VerifyDiagnostics()

            Dim vbexeCompilation = CreateVisualBasicCompilation("VBExe",
            <![CDATA[
Imports System

Public Class VBExeClass

End Class

Public Module Program
    Sub Main(args As String())
        Dim o As New CSDllClasDerived

        AddHandler o.E1, AddressOf H1

        o.Raise
    End Sub

    Sub H1()
        Console.Write("bye")
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication),
                referencedCompilations:={csdllCompilation})
            Dim vbexeVerifier = CompileAndVerify(vbexeCompilation,
                expectedOutput:=<![CDATA[bye]]>)
            vbexeVerifier.VerifyDiagnostics()
        End Sub

        <Fact()>
        Public Sub SimpleEvent()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
Imports System

Module Module1

    Private Event e1 As Action

    Sub Main(args As String())
        Dim h As Action = Sub() Console.Write("hello ")
        AddHandler e1, h
        e1Event.Invoke()

        RemoveHandler e1, h
        Console.Write(e1Event Is Nothing)
    End Sub
End Module

    </file>
    </compilation>, expectedOutput:="hello True").
                VerifyIL("Module1.Main",
            <![CDATA[
{
  // Code size       67 (0x43)
  .maxstack  2
  IL_0000:  ldsfld     "Module1._ClosureCache$__2 As System.Action"
  IL_0005:  brfalse.s  IL_000e
  IL_0007:  ldsfld     "Module1._ClosureCache$__2 As System.Action"
  IL_000c:  br.s       IL_0020
  IL_000e:  ldnull
  IL_000f:  ldftn      "Sub Module1._Lambda$__1()"
  IL_0015:  newobj     "Sub System.Action..ctor(Object, System.IntPtr)"
  IL_001a:  dup
  IL_001b:  stsfld     "Module1._ClosureCache$__2 As System.Action"
  IL_0020:  dup
  IL_0021:  call       "Sub Module1.add_e1(System.Action)"
  IL_0026:  ldsfld     "Module1.e1Event As System.Action"
  IL_002b:  callvirt   "Sub System.Action.Invoke()"
  IL_0030:  call       "Sub Module1.remove_e1(System.Action)"
  IL_0035:  ldsfld     "Module1.e1Event As System.Action"
  IL_003a:  ldnull
  IL_003b:  ceq
  IL_003d:  call       "Sub System.Console.Write(Boolean)"
  IL_0042:  ret
}
    ]]>)
        End Sub

        <Fact()>
        Public Sub SimpleEvent1()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
Imports System

Module Module1

    Class Nested
    End Class

    Private Event e1

    Private field as e1EventHandler

    Sub Main(args As String())
        Dim h As e1EventHandler = Sub() Console.Write("hello ")
        AddHandler e1, h
        e1Event.Invoke()

        RemoveHandler e1, h
        Console.Write(e1Event Is Nothing)
    End Sub
End Module

    </file>
    </compilation>, expectedOutput:="hello True").
                VerifyIL("Module1.Main",
            <![CDATA[
{
  // Code size       67 (0x43)
  .maxstack  2
  IL_0000:  ldsfld     "Module1._ClosureCache$__2 As Module1.e1EventHandler"
  IL_0005:  brfalse.s  IL_000e
  IL_0007:  ldsfld     "Module1._ClosureCache$__2 As Module1.e1EventHandler"
  IL_000c:  br.s       IL_0020
  IL_000e:  ldnull
  IL_000f:  ldftn      "Sub Module1._Lambda$__1()"
  IL_0015:  newobj     "Sub Module1.e1EventHandler..ctor(Object, System.IntPtr)"
  IL_001a:  dup
  IL_001b:  stsfld     "Module1._ClosureCache$__2 As Module1.e1EventHandler"
  IL_0020:  dup
  IL_0021:  call       "Sub Module1.add_e1(Module1.e1EventHandler)"
  IL_0026:  ldsfld     "Module1.e1Event As Module1.e1EventHandler"
  IL_002b:  callvirt   "Sub Module1.e1EventHandler.Invoke()"
  IL_0030:  call       "Sub Module1.remove_e1(Module1.e1EventHandler)"
  IL_0035:  ldsfld     "Module1.e1Event As Module1.e1EventHandler"
  IL_003a:  ldnull
  IL_003b:  ceq
  IL_003d:  call       "Sub System.Console.Write(Boolean)"
  IL_0042:  ret
}
]]>)
        End Sub

        <Fact()>
        Public Sub SimpleEvent2()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
Imports System

Module Module1

    Class Nested
    End Class

    Private Event e1(x As e1EventHandler)

    Sub Main(args As String())
        Dim h As e1EventHandler = Sub() Console.Write("hello ")
        AddHandler e1, h
        e1Event.Invoke(h)

        RemoveHandler e1, h
        Console.Write(e1Event Is Nothing)
    End Sub
End Module

    </file>
    </compilation>, expectedOutput:="hello True").
                VerifyIL("Module1.Main",
            <![CDATA[
{
  // Code size       70 (0x46)
  .maxstack  2
  .locals init (Module1.e1EventHandler V_0) //h
  IL_0000:  ldsfld     "Module1._ClosureCache$__4 As Module1.e1EventHandler"
  IL_0005:  brfalse.s  IL_000e
  IL_0007:  ldsfld     "Module1._ClosureCache$__4 As Module1.e1EventHandler"
  IL_000c:  br.s       IL_0020
  IL_000e:  ldnull
  IL_000f:  ldftn      "Sub Module1._Lambda$__1(Module1.e1EventHandler)"
  IL_0015:  newobj     "Sub Module1.e1EventHandler..ctor(Object, System.IntPtr)"
  IL_001a:  dup
  IL_001b:  stsfld     "Module1._ClosureCache$__4 As Module1.e1EventHandler"
  IL_0020:  stloc.0
  IL_0021:  ldloc.0
  IL_0022:  call       "Sub Module1.add_e1(Module1.e1EventHandler)"
  IL_0027:  ldsfld     "Module1.e1Event As Module1.e1EventHandler"
  IL_002c:  ldloc.0
  IL_002d:  callvirt   "Sub Module1.e1EventHandler.Invoke(Module1.e1EventHandler)"
  IL_0032:  ldloc.0
  IL_0033:  call       "Sub Module1.remove_e1(Module1.e1EventHandler)"
  IL_0038:  ldsfld     "Module1.e1Event As Module1.e1EventHandler"
  IL_003d:  ldnull
  IL_003e:  ceq
  IL_0040:  call       "Sub System.Console.Write(Boolean)"
  IL_0045:  ret
}
    ]]>)
        End Sub

        <Fact()>
        Public Sub SimpleRaiseHandlerWithBlockEvent()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
Imports System

Module Program

    Delegate Sub del1(ByRef x As Integer)

    Custom Event E As del1
        AddHandler(value As del1)
            System.Console.Write("Add")
        End AddHandler

        RemoveHandler(value As del1)
            System.Console.Write("Remove")
        End RemoveHandler

        RaiseEvent(ByRef x As Integer)
            System.Console.Write("Raise")
        End RaiseEvent
    End Event

    Sub Main(args As String())
        AddHandler E, Nothing
        RemoveHandler E, Nothing
        RaiseEvent E(42)
    End Sub
End Module

    </file>
    </compilation>, expectedOutput:="AddRemoveRaise").
                VerifyIL("Program.Main",
            <![CDATA[
{
  // Code size       23 (0x17)
  .maxstack  1
  .locals init (Integer V_0)
  IL_0000:  ldnull
  IL_0001:  call       "Sub Program.add_E(Program.del1)"
  IL_0006:  ldnull
  IL_0007:  call       "Sub Program.remove_E(Program.del1)"
  IL_000c:  ldc.i4.s   42
  IL_000e:  stloc.0
  IL_000f:  ldloca.s   V_0
  IL_0011:  call       "Sub Program.raise_E(ByRef Integer)"
  IL_0016:  ret
}
    ]]>)
        End Sub

        <Fact()>
        Public Sub SimpleRaiseHandlerWithFieldEvent()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">

Imports System

Module Program

    Delegate Sub del1(ByRef x As String)
    Event E As del1
    Property str As String

    Sub Main(args As String())
        Dim lambda As del1 =
            Sub(ByRef s As String)
                Console.Write(s)
                s = "bye"
            End Sub

        str = "hello "
        RaiseEvent E(str)
        AddHandler E, lambda
        RaiseEvent E(x:=str)
        RemoveHandler E, lambda
        RaiseEvent E(str)

        Console.Write(str)
    End Sub
End Module

    </file>
    </compilation>, expectedOutput:="hello bye").
                VerifyIL("Program.Main",
            <![CDATA[
{
  // Code size      151 (0x97)
  .maxstack  3
  .locals init (Program.del1 V_0,
  String V_1,
  Program.del1 V_2,
  Program.del1 V_3)
  IL_0000:  ldsfld     "Program._ClosureCache$__2 As Program.del1"
  IL_0005:  brfalse.s  IL_000e
  IL_0007:  ldsfld     "Program._ClosureCache$__2 As Program.del1"
  IL_000c:  br.s       IL_0020
  IL_000e:  ldnull
  IL_000f:  ldftn      "Sub Program._Lambda$__1(ByRef String)"
  IL_0015:  newobj     "Sub Program.del1..ctor(Object, System.IntPtr)"
  IL_001a:  dup
  IL_001b:  stsfld     "Program._ClosureCache$__2 As Program.del1"
  IL_0020:  ldstr      "hello "
  IL_0025:  call       "Sub Program.set_str(String)"
  IL_002a:  ldsfld     "Program.EEvent As Program.del1"
  IL_002f:  stloc.0
  IL_0030:  ldloc.0
  IL_0031:  brfalse.s  IL_0047
  IL_0033:  ldloc.0
  IL_0034:  call       "Function Program.get_str() As String"
  IL_0039:  stloc.1
  IL_003a:  ldloca.s   V_1
  IL_003c:  callvirt   "Sub Program.del1.Invoke(ByRef String)"
  IL_0041:  ldloc.1
  IL_0042:  call       "Sub Program.set_str(String)"
  IL_0047:  dup
  IL_0048:  call       "Sub Program.add_E(Program.del1)"
  IL_004d:  ldsfld     "Program.EEvent As Program.del1"
  IL_0052:  stloc.2
  IL_0053:  ldloc.2
  IL_0054:  brfalse.s  IL_006a
  IL_0056:  ldloc.2
  IL_0057:  call       "Function Program.get_str() As String"
  IL_005c:  stloc.1
  IL_005d:  ldloca.s   V_1
  IL_005f:  callvirt   "Sub Program.del1.Invoke(ByRef String)"
  IL_0064:  ldloc.1
  IL_0065:  call       "Sub Program.set_str(String)"
  IL_006a:  call       "Sub Program.remove_E(Program.del1)"
  IL_006f:  ldsfld     "Program.EEvent As Program.del1"
  IL_0074:  stloc.3
  IL_0075:  ldloc.3
  IL_0076:  brfalse.s  IL_008c
  IL_0078:  ldloc.3
  IL_0079:  call       "Function Program.get_str() As String"
  IL_007e:  stloc.1
  IL_007f:  ldloca.s   V_1
  IL_0081:  callvirt   "Sub Program.del1.Invoke(ByRef String)"
  IL_0086:  ldloc.1
  IL_0087:  call       "Sub Program.set_str(String)"
  IL_008c:  call       "Function Program.get_str() As String"
  IL_0091:  call       "Sub System.Console.Write(String)"
  IL_0096:  ret
}
    ]]>)
        End Sub

        <Fact()>
        Public Sub SimpleRaiseHandlerWithFieldEventInStruct()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">


Imports System


Module Program
    Delegate Sub del1()

    Event E As del1

    Sub Main(args As String())
        Dim s As New s1(Nothing)
    End Sub
End Module

Structure s1
    Delegate Sub del1(x As Object)

    Event E As del1

    Sub New(args As String())
        RaiseEvent E(1)
    End Sub
End Structure

    </file>
    </compilation>, expectedOutput:="", emitPdb:=True).
                VerifyIL("s1..ctor",
            <![CDATA[
{
  // Code size       36 (0x24)
  .maxstack  2
  .locals init (s1.del1 V_0,
  Boolean V_1)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  initobj    "s1"
  IL_0008:  ldarg.0
  IL_0009:  ldfld      "s1.EEvent As s1.del1"
  IL_000e:  stloc.0
  IL_000f:  ldloc.0
  IL_0010:  ldnull
  IL_0011:  ceq
  IL_0013:  stloc.1
  IL_0014:  ldloc.1
  IL_0015:  brtrue.s   IL_0023
  IL_0017:  ldloc.0
  IL_0018:  ldc.i4.1
  IL_0019:  box        "Integer"
  IL_001e:  callvirt   "Sub s1.del1.Invoke(Object)"
  IL_0023:  ret
}
    ]]>)
        End Sub


        <Fact()>
        Public Sub SimpleRaiseHandlerWithImplementedEvent()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">

Imports System

Module Program

    Interface i1
        Delegate Sub del1(ByRef x As String)
        Event E As del1

        Sub Raise(ByRef x As String)
    End Interface

    Class cls1
        Implements i1

        Private Event E as i1.del1 Implements i1.E

        Private Sub Raise(ByRef x As String) Implements i1.Raise
            RaiseEvent E(x)
        End Sub
    End Class

    Sub Main()

        Dim i As i1 = New cls1

        AddHandler i.E, Sub(ByRef s As String)
                            Console.Write(s)
                            s = "bye"
                        End Sub

        Dim Str As String = "hello "
        i.Raise(Str)

        Console.Write(Str)
    End Sub
End Module


    </file>
    </compilation>, expectedOutput:="hello bye").
                VerifyIL("Program.Main",
            <![CDATA[
{
  // Code size       63 (0x3f)
  .maxstack  4
  .locals init (String V_0) //Str
  IL_0000:  newobj     "Sub Program.cls1..ctor()"
  IL_0005:  dup
  IL_0006:  ldsfld     "Program._ClosureCache$__2 As Program.i1.del1"
  IL_000b:  brfalse.s  IL_0014
  IL_000d:  ldsfld     "Program._ClosureCache$__2 As Program.i1.del1"
  IL_0012:  br.s       IL_0026
  IL_0014:  ldnull
  IL_0015:  ldftn      "Sub Program._Lambda$__1(ByRef String)"
  IL_001b:  newobj     "Sub Program.i1.del1..ctor(Object, System.IntPtr)"
  IL_0020:  dup
  IL_0021:  stsfld     "Program._ClosureCache$__2 As Program.i1.del1"
  IL_0026:  callvirt   "Sub Program.i1.add_E(Program.i1.del1)"
  IL_002b:  ldstr      "hello "
  IL_0030:  stloc.0
  IL_0031:  ldloca.s   V_0
  IL_0033:  callvirt   "Sub Program.i1.Raise(ByRef String)"
  IL_0038:  ldloc.0
  IL_0039:  call       "Sub System.Console.Write(String)"
  IL_003e:  ret
}
]]>)
        End Sub

        <Fact()>
        Public Sub SimpleRaiseHandlerWithTwoImplementedEvents()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">

Imports System

Module Program

    Interface i1
        Delegate Sub del1(ByRef x As String)
        Event E As del1
        Event E1 As del1

        Sub Raise(ByRef x As String)
    End Interface

    Class cls1
        Implements i1

        Private Event E(ByRef x As String) Implements i1.E, i1.E1

        Private Sub Raise(ByRef x As String) Implements i1.Raise
            RaiseEvent E(x)
        End Sub
    End Class

    Sub Main()

        Dim i As i1 = New cls1

        ' NOTE!!   Adding to E1
        AddHandler i.E1, Sub(ByRef s As String)
                             Console.Write(s)
                             s = "bye"
                         End Sub

        Dim Str As String = "hello "
        i.Raise(Str)

        Console.Write(Str)
    End Sub
End Module

    </file>
    </compilation>, expectedOutput:="hello bye").
                VerifyIL("Program.Main",
            <![CDATA[
{
  // Code size       63 (0x3f)
  .maxstack  4
  .locals init (String V_0) //Str
  IL_0000:  newobj     "Sub Program.cls1..ctor()"
  IL_0005:  dup
  IL_0006:  ldsfld     "Program._ClosureCache$__2 As Program.i1.del1"
  IL_000b:  brfalse.s  IL_0014
  IL_000d:  ldsfld     "Program._ClosureCache$__2 As Program.i1.del1"
  IL_0012:  br.s       IL_0026
  IL_0014:  ldnull
  IL_0015:  ldftn      "Sub Program._Lambda$__1(ByRef String)"
  IL_001b:  newobj     "Sub Program.i1.del1..ctor(Object, System.IntPtr)"
  IL_0020:  dup
  IL_0021:  stsfld     "Program._ClosureCache$__2 As Program.i1.del1"
  IL_0026:  callvirt   "Sub Program.i1.add_E1(Program.i1.del1)"
  IL_002b:  ldstr      "hello "
  IL_0030:  stloc.0
  IL_0031:  ldloca.s   V_0
  IL_0033:  callvirt   "Sub Program.i1.Raise(ByRef String)"
  IL_0038:  ldloc.0
  IL_0039:  call       "Sub System.Console.Write(String)"
  IL_003e:  ret
}
]]>)
        End Sub

        <Fact()>
        Public Sub EventOverridesInCS()
            Dim csdllCompilation = CreateCSharpCompilation("CSDll",
            <![CDATA[
using System;

public abstract class CSDllClass
{
    public abstract event System.Action E1;

    public abstract void Raise();
}

public class CSDllClasDerived : CSDllClass
{
    public override event Action E1;
    
    public override void Raise()
    {
        E1();
    }
}
]]>,
                compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            csdllCompilation.VerifyDiagnostics()

            Dim vbexeCompilation = CreateVisualBasicCompilation("VBExe",
            <![CDATA[
Imports System

Public Class VBExeClass
    inherits CSDllClasDerived
End Class

Public Module Program
    Sub Main(args As String())
        Dim o As New VBExeClass

        AddHandler o.E1, Sub() Console.Write("hi ")
        AddHandler o.E1, AddressOf H1

        o.Raise
    End Sub

    Sub H1()
        Console.Write("bye")
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication),
                referencedCompilations:={csdllCompilation})

            vbexeCompilation.VerifyDiagnostics()

            Dim treeA = CompilationUtils.GetTree(vbexeCompilation, "")
            Dim bindingsA = vbexeCompilation.GetSemanticModel(treeA)

            ' Find "Class VBExeClass".
            Dim typeVBExeClass = CompilationUtils.GetTypeSymbol(vbexeCompilation, bindingsA, "", "VBExeClass")
            Dim VBExeClassBase1 = typeVBExeClass.BaseType

            Dim ev = DirectCast(VBExeClassBase1.GetMembers("E1").First, EventSymbol)
            Assert.Equal(True, ev.IsOverrides)

            Dim overrideList = ev.OverriddenOrHiddenMembers.OverriddenMembers
            Assert.Equal("Action", DirectCast(ev.OverriddenMember, EventSymbol).Type.Name)

            Dim vbexeVerifier = CompileAndVerify(vbexeCompilation,
                expectedOutput:=<![CDATA[hi bye]]>)

            vbexeVerifier.VerifyDiagnostics()

        End Sub

        <Fact(), WorkItem(543612)>
        Public Sub CallEventHandlerThroughWithEvent01()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
Option Explicit On
Imports System

Module Program
    Sub Main()
        Dim Var8_7 As New InitOnly8_7()
        Var8_7.x.Spark()
        Console.Write(Var8_7.y)
    End Sub
End Module

Public Class InitOnly8_7
    Public Class InitOnly8_7_1
        Public Event Flash()

        Public Sub Spark()
            RaiseEvent Flash()
        End Sub
    End Class

    Public WithEvents x As InitOnly8_7_1

    Sub New()
        x = New InitOnly8_7_1()
    End Sub

    Public y As Boolean = False
    Public Sub Blink() Handles x.Flash
        y = True
    End Sub
End Class
    </file>
    </compilation>, expectedOutput:="True").
                VerifyIL("Program.Main",
            <![CDATA[
{
  // Code size       27 (0x1b)
  .maxstack  2
  IL_0000:  newobj     "Sub InitOnly8_7..ctor()"
  IL_0005:  dup
  IL_0006:  callvirt   "Function InitOnly8_7.get_x() As InitOnly8_7.InitOnly8_7_1"
  IL_000b:  callvirt   "Sub InitOnly8_7.InitOnly8_7_1.Spark()"
  IL_0010:  ldfld      "InitOnly8_7.y As Boolean"
  IL_0015:  call       "Sub System.Console.Write(Boolean)"
  IL_001a:  ret
}
    ]]>)
        End Sub

        <Fact(), WorkItem(543612)>
        Public Sub CallEventHandlerThroughWithEvent02()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
Option Explicit On
Imports System

Module Program
    Sub Main()
        Dim var4_14 As New InitOnly4_14()
    End Sub
End Module

Class InitOnly4_14
    ReadOnly x As Byte = 0
    Event Explosion(ByRef b As Byte)
    Dim WithEvents Cl As InitOnly4_14
    Sub New()
        Cl = Me
        RaiseEvent Explosion(x)
        Console.Write(x) ' "shared member on shared new
    End Sub

    Sub Bang(ByRef b As Byte) Handles Cl.Explosion
        b = 1
    End Sub
End Class
    </file>
    </compilation>, expectedOutput:="1")
        End Sub

        <Fact(), WorkItem(543612)>
        Public Sub ObsoleteRaiseEvent()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
            <![CDATA[
Option Explicit On
Imports System

Delegate Sub D1(ByVal a As Integer)
Class C0
    Class C1
        Inherits C0
        <Obsolete("Event Obsolete")>
        Custom Event E3 As D1
            AddHandler(ByVal value As D1)
            End AddHandler
            RemoveHandler(ByVal value As D1)
            End RemoveHandler
            RaiseEvent(ByVal a As Integer)
            End RaiseEvent
        End Event
    End Class
End Class
Module Module1
    Sub Main()
        Dim o1 As New C0.C1
        Dim E3Info As Reflection.EventInfo = GetType(C0.C1).GetEvent("E3")
        If E3Info.GetRaiseMethod(True) Is Nothing Then
            Console.WriteLine("FAILED")
        Else
            For Each Attr As Attribute In E3Info.GetRaiseMethod(True).GetCustomAttributes(False)
                Console.WriteLine("Raise - " & Attr.ToString & CType(Attr, ObsoleteAttribute).Message)
            Next
        End If
    End Sub
End Module
]]>
        </file>
    </compilation>, expectedOutput:="")
        End Sub

        <Fact(), WorkItem(545428)>
        Public Sub AddHandlerConflictingLocal()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
            <![CDATA[
Option Explicit On
Imports System

Module Module1
    Event e As Action(Of String)
 
    Sub Main()
        ' Handler Case
        AddHandler e, New Action(Of Object)(Function(o As Object) o)
 
        Dim e As Integer
        Console.WriteLine(e.GetType)
    End Sub
End Module

]]>
        </file>
    </compilation>, expectedOutput:="System.Int32")
        End Sub

        <Fact(), WorkItem(546055)>
        Public Sub AddHandlerEventNameLookupViaImport()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
            <![CDATA[
Option Explicit Off
Option Strict Off

Imports System
Imports cls

Class cls
    Public Shared Event ev1(x As String)

    Public Shared Sub RaiseEv1()
        RaiseEvent ev1("hello from ev1")
    End Sub
End Class

Module Program
    Sub foo(ByVal x As String)
        Console.WriteLine("{0}", x)
    End Sub

    Sub Main(args As String())
        AddHandler ev1, AddressOf foo 
        AddHandler ev1, AddressOf foo
        RaiseEv1()
    End Sub
End Module

]]>
        </file>
    </compilation>, expectedOutput:=<![CDATA[
hello from ev1
hello from ev1
]]>)
        End Sub

        <Fact(Skip:="529574")>
        Public Sub TestCrossLanguageOptionalAndParamarray1()
            Dim csCompilation = CreateCSharpCompilation("CS",
            <![CDATA[public class CSClass
{
    public delegate int bar(string x = "", params int[] y);
    public event bar ev;
    public void raise()
    {
        ev("hi", 1, 2, 3);
    }
}]]>, compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))

            csCompilation.VerifyDiagnostics()
            Dim vbCompilation = CreateVisualBasicCompilation("VB",
            <![CDATA[
option strict off

Imports System
Public Class VBClass : Inherits CSClass
    Public WithEvents w As CSClass = New CSClass
    Function Foo(x As String) Handles w.ev, MyBase.ev, MyClass.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
    Function Foo(x As String, ParamArray y() As Integer) Handles w.ev, MyBase.ev, MyClass.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
    Function Foo2(Optional x As String = "") Handles w.ev, MyBase.ev, MyClass.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
    Function Foo2(x As String, y() As Integer) Handles w.ev, MyBase.ev, MyClass.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
End Class
Public Module Program
    Sub Main()
        Dim x = New VBClass
        x.raise()
        x.w.raise()
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication),
                referencedCompilations:={csCompilation})
            ' WARNING: Roslyn compiler produced errors while Native compiler didn't.
            vbCompilation.VerifyDiagnostics()
        End Sub

        <Fact()>
        Public Sub TestCrossLanguageOptionalAndParamarray2()
            Dim csCompilation = CreateCSharpCompilation("CS",
            <![CDATA[public class CSClass
{
    public delegate int bar(string x = "");
    public event bar ev;
    public void raise()
    {
        ev("hi");
    }
}]]>,
                compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            csCompilation.VerifyDiagnostics()
            Dim vbCompilation = CreateVisualBasicCompilation("VB",
            <![CDATA[Imports System
Public Class VBClass : Inherits CSClass
    Public WithEvents w As CSClass = New CSClass
    Function Foo(x As String) Handles w.ev, MyBase.ev, MyClass.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
    Function Foo(x As String, ParamArray y() As Integer) Handles w.ev, MyBase.ev, MyClass.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
    Function Foo2(Optional x As String = "") Handles w.ev, MyBase.ev, MyClass.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
    Function Foo2(ParamArray x() As String) Handles w.ev, MyBase.ev, MyClass.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
    Function Foo2(x As String, Optional y As Integer = 0) Handles w.ev, MyBase.ev, MyClass.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
    Function Foo3(Optional x As String = "", Optional y As Integer = 0) Handles w.ev, MyBase.ev, MyClass.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
End Class
Public Module Program
    Sub Main()
        Dim x = New VBClass
        x.raise()
        x.w.raise()
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication),
                referencedCompilations:={csCompilation})
            ' WARNING: Binaries compiled with Native and Roslyn compilers produced different outputs.
            ' Below baseline is the output produced by the binary compiled with the Roslyn since
            ' in the native case output is not documented (depends on hashtable ordering).
            Dim vbVerifier = CompileAndVerify(vbCompilation,
                expectedOutput:=<![CDATA[hi
PASS
hi
PASS
hi
PASS
hi
PASS
hi
PASS
hi
PASS
System.String[]
PASS
System.String[]
PASS
hi
PASS
hi
PASS
hi
PASS
hi
PASS
hi
PASS
hi
PASS
hi
PASS
System.String[]
PASS
hi
PASS
hi
PASS
]]>)
            vbVerifier.VerifyDiagnostics()
        End Sub

        <Fact()>
        Public Sub TestCrossLanguageOptionalAndPAramarray3()
            Dim csCompilation = CreateCSharpCompilation("CS",
            <![CDATA[public class CSClass
{
    public delegate int bar(params int[] y);
    public event bar ev;
    public void raise()
    {
        ev(1, 2, 3);
    }
}]]>,
                compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            csCompilation.VerifyDiagnostics()
            Dim vbCompilation = CreateVisualBasicCompilation("VB",
            <![CDATA[Imports System
Public Class VBClass : Inherits CSClass
    Public WithEvents w As CSClass = New CSClass
    Function Foo(x As Integer()) Handles w.ev, MyBase.ev, Me.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
    Function Foo2(ParamArray x As Integer()) Handles w.ev, MyBase.ev, Me.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
End Class
Public Module Program
    Sub Main()
        Dim x = New VBClass
        x.raise()
        x.w.Raise()
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication),
                referencedCompilations:={csCompilation})
            Dim vbVerifier = CompileAndVerify(vbCompilation,
                expectedOutput:=<![CDATA[System.Int32[]
PASS
System.Int32[]
PASS
System.Int32[]
PASS
System.Int32[]
PASS
System.Int32[]
PASS
System.Int32[]
PASS
]]>)
            vbVerifier.VerifyDiagnostics()
        End Sub

        <Fact, WorkItem(545257)>
        Public Sub TestCrossLanguageOptionalAndParamarray_Error1()
            Dim csCompilation = CreateCSharpCompilation("CS",
            <![CDATA[public class CSClass
{
    public delegate int bar(params int[] y);
    public event bar ev;
    public void raise()
    {
        ev(1, 2, 3);
    }
}]]>,
                compilationOptions:=New Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            csCompilation.VerifyDiagnostics()
            Dim vbCompilation = CreateVisualBasicCompilation("VB",
            <![CDATA[Imports System
Public Class VBClass : Inherits CSClass
    Public WithEvents w As CSClass = New CSClass
    Function Foo2(x As Integer) Handles w.ev, MyBase.ev, Me.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
    Function Foo2(x As Integer, Optional y As Integer = 1) Handles w.ev, MyBase.ev, Me.ev
        Console.WriteLine(x)
        Console.WriteLine("PASS")
        Return 0
    End Function
End Class
Public Module Program
    Sub Main()
        Dim x = New VBClass
        x.raise()
        x.w.Raise()
    End Sub
End Module]]>,
                compilationOptions:=New VisualBasicCompilationOptions(OutputKind.ConsoleApplication),
                referencedCompilations:={csCompilation})
            vbCompilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo2", "ev"),
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo2", "ev"),
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo2", "ev"),
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo2", "ev"),
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo2", "ev"),
                Diagnostic(ERRID.ERR_EventHandlerSignatureIncompatible2, "ev").WithArguments("Foo2", "ev"))
        End Sub


        <Fact()>
        Public Sub WithEventsProperty()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">

Imports System
Imports System.ComponentModel

Namespace Project1
    Module m1
        Public Sub main()
            Dim c = New Sink
            Dim s = New OuterClass
            c.x = s
            s.Test()
        End Sub
    End Module

    Class EventSource
        Public Event MyEvent()
        Sub test()
            RaiseEvent MyEvent()
        End Sub
    End Class


    Class OuterClass

        Private SubObject As New EventSource

        &lt;DesignOnly(True)> _
        &lt;DesignerSerializationVisibility(DesignerSerializationVisibility.Content)> _
        Public Property SomeProperty() As EventSource
            Get
                Console.Write("#Get#")
                Return SubObject
            End Get
            Set(value As EventSource)

            End Set
        End Property


        Sub Test()
            SubObject.test()
        End Sub
    End Class

    Class Sink

        Public WithEvents x As OuterClass
        Sub foo() Handles x.SomeProperty.MyEvent

            Console.Write("Handled Event On SubObject!")
        End Sub

        Sub test()
            x.Test()
        End Sub
        Sub New()
            x = New OuterClass
        End Sub
    End Class
    '.....
End Namespace


    </file>
    </compilation>, expectedOutput:="#Get##Get##Get#Handled Event On SubObject!").
                VerifyIL("Project1.Sink.set_x(Project1.OuterClass)",
            <![CDATA[
{
  // Code size       65 (0x41)
  .maxstack  2
  .locals init (Project1.EventSource.MyEventEventHandler V_0,
  Project1.OuterClass V_1)
  IL_0000:  ldarg.0
  IL_0001:  ldftn      "Sub Project1.Sink.foo()"
  IL_0007:  newobj     "Sub Project1.EventSource.MyEventEventHandler..ctor(Object, System.IntPtr)"
  IL_000c:  stloc.0
  IL_000d:  ldarg.0
  IL_000e:  ldfld      "Project1.Sink._x As Project1.OuterClass"
  IL_0013:  stloc.1
  IL_0014:  ldloc.1
  IL_0015:  brfalse.s  IL_0023
  IL_0017:  ldloc.1
  IL_0018:  callvirt   "Function Project1.OuterClass.get_SomeProperty() As Project1.EventSource"
  IL_001d:  ldloc.0
  IL_001e:  callvirt   "Sub Project1.EventSource.remove_MyEvent(Project1.EventSource.MyEventEventHandler)"
  IL_0023:  ldarg.0
  IL_0024:  ldarg.1
  IL_0025:  stfld      "Project1.Sink._x As Project1.OuterClass"
  IL_002a:  ldarg.0
  IL_002b:  ldfld      "Project1.Sink._x As Project1.OuterClass"
  IL_0030:  stloc.1
  IL_0031:  ldloc.1
  IL_0032:  brfalse.s  IL_0040
  IL_0034:  ldloc.1
  IL_0035:  callvirt   "Function Project1.OuterClass.get_SomeProperty() As Project1.EventSource"
  IL_003a:  ldloc.0
  IL_003b:  callvirt   "Sub Project1.EventSource.add_MyEvent(Project1.EventSource.MyEventEventHandler)"
  IL_0040:  ret
}
    ]]>)
        End Sub

        <Fact()>
        Public Sub WithEventsPropertySharedEvent()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">

Imports System
Imports System.ComponentModel

Namespace Project1
    Module m1
        Public Sub main()
            Dim c = New Sink
            Dim s = New OuterClass
            c.x = s
            s.Test()
        End Sub
    End Module

    Class EventSource
        Public Shared Event MyEvent()
        Sub test()
            RaiseEvent MyEvent()
        End Sub
    End Class


    Class OuterClass

        Private SubObject As New EventSource

        &lt;DesignOnly(True)> _
        &lt;DesignerSerializationVisibility(DesignerSerializationVisibility.Content)> _
        Public Property SomeProperty() As EventSource
            Get
                Console.Write("#Get#")
                Return SubObject
            End Get
            Set(value As EventSource)

            End Set
        End Property


        Sub Test()
            SubObject.test()
        End Sub
    End Class

    Class Sink

        Public WithEvents x As OuterClass
        Sub foo() Handles x.SomeProperty.MyEvent

            Console.Write("Handled Event On SubObject!")
        End Sub

        Sub test()
            x.Test()
        End Sub
        Sub New()
            x = New OuterClass
        End Sub
    End Class
    '.....
End Namespace


    </file>
    </compilation>, expectedOutput:="Handled Event On SubObject!").
                VerifyIL("Project1.Sink.set_x(Project1.OuterClass)",
            <![CDATA[
{
  // Code size       49 (0x31)
  .maxstack  2
  .locals init (Project1.EventSource.MyEventEventHandler V_0)
  IL_0000:  ldarg.0
  IL_0001:  ldftn      "Sub Project1.Sink.foo()"
  IL_0007:  newobj     "Sub Project1.EventSource.MyEventEventHandler..ctor(Object, System.IntPtr)"
  IL_000c:  stloc.0
  IL_000d:  ldarg.0
  IL_000e:  ldfld      "Project1.Sink._x As Project1.OuterClass"
  IL_0013:  brfalse.s  IL_001b
  IL_0015:  ldloc.0
  IL_0016:  call       "Sub Project1.EventSource.remove_MyEvent(Project1.EventSource.MyEventEventHandler)"
  IL_001b:  ldarg.0
  IL_001c:  ldarg.1
  IL_001d:  stfld      "Project1.Sink._x As Project1.OuterClass"
  IL_0022:  ldarg.0
  IL_0023:  ldfld      "Project1.Sink._x As Project1.OuterClass"
  IL_0028:  brfalse.s  IL_0030
  IL_002a:  ldloc.0
  IL_002b:  call       "Sub Project1.EventSource.add_MyEvent(Project1.EventSource.MyEventEventHandler)"
  IL_0030:  ret
}
    ]]>)
        End Sub

        <Fact()>
        Public Sub WithEventsPropertySharedProperty()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">

Imports System
Imports System.ComponentModel

Namespace Project1
    Module m1
        Public Sub main()
            Dim c = New Sink
            Dim s = New OuterClass
            c.x = s
            s.Test()
        End Sub
    End Module

    Class EventSource
        Public Event MyEvent()
        Sub test()
            RaiseEvent MyEvent()
        End Sub
    End Class


    Class OuterClass

        Private shared SubObject As New EventSource

        &lt;DesignOnly(True)> _
        &lt;DesignerSerializationVisibility(DesignerSerializationVisibility.Content)> _
        Public shared Property SomeProperty() As EventSource
            Get
                Console.Write("#Get#")
                Return SubObject
            End Get
            Set(value As EventSource)

            End Set
        End Property


        Sub Test()
            SubObject.test()
        End Sub
    End Class

    Class Sink

        Public WithEvents x As OuterClass
        Sub foo() Handles x.SomeProperty.MyEvent

            Console.Write("Handled Event On SubObject!")
        End Sub

        Sub test()
            x.Test()
        End Sub
        Sub New()
            x = New OuterClass
        End Sub
    End Class
    '.....
End Namespace


    </file>
    </compilation>, expectedOutput:="#Get##Get##Get#Handled Event On SubObject!").
                VerifyIL("Project1.Sink.set_x(Project1.OuterClass)",
            <![CDATA[
{
  // Code size       59 (0x3b)
  .maxstack  2
  .locals init (Project1.EventSource.MyEventEventHandler V_0)
  IL_0000:  ldarg.0
  IL_0001:  ldftn      "Sub Project1.Sink.foo()"
  IL_0007:  newobj     "Sub Project1.EventSource.MyEventEventHandler..ctor(Object, System.IntPtr)"
  IL_000c:  stloc.0
  IL_000d:  ldarg.0
  IL_000e:  ldfld      "Project1.Sink._x As Project1.OuterClass"
  IL_0013:  brfalse.s  IL_0020
  IL_0015:  call       "Function Project1.OuterClass.get_SomeProperty() As Project1.EventSource"
  IL_001a:  ldloc.0
  IL_001b:  callvirt   "Sub Project1.EventSource.remove_MyEvent(Project1.EventSource.MyEventEventHandler)"
  IL_0020:  ldarg.0
  IL_0021:  ldarg.1
  IL_0022:  stfld      "Project1.Sink._x As Project1.OuterClass"
  IL_0027:  ldarg.0
  IL_0028:  ldfld      "Project1.Sink._x As Project1.OuterClass"
  IL_002d:  brfalse.s  IL_003a
  IL_002f:  call       "Function Project1.OuterClass.get_SomeProperty() As Project1.EventSource"
  IL_0034:  ldloc.0
  IL_0035:  callvirt   "Sub Project1.EventSource.add_MyEvent(Project1.EventSource.MyEventEventHandler)"
  IL_003a:  ret
}
    ]]>)
        End Sub

        <Fact()>
        Public Sub WithEventsPropertyAllShared()
            CompileAndVerify(
    <compilation>
        <file name="a.vb">

Imports System
Imports System.ComponentModel

Namespace Project1
    Module m1
        Public Sub main()
            Dim c = New Sink
            Dim s = New OuterClass
            c.x = s
            s.Test()
        End Sub
    End Module

    Class EventSource
        Public shared Event MyEvent()
        Sub test()
            RaiseEvent MyEvent()
        End Sub
    End Class


    Class OuterClass

        Private SubObject As New EventSource

        &lt;DesignOnly(True)> _
        &lt;DesignerSerializationVisibility(DesignerSerializationVisibility.Content)> _
        Public Property SomeProperty() As EventSource
            Get
                Console.Write("#Get#")
                Return SubObject
            End Get
            Set(value As EventSource)

            End Set
        End Property


        Sub Test()
            SubObject.test()
        End Sub
    End Class

    Class Sink

        Public WithEvents x As OuterClass
        Sub foo() Handles x.SomeProperty.MyEvent

            Console.Write("Handled Event On SubObject!")
        End Sub

        Sub test()
            x.Test()
        End Sub
        Sub New()
            x = New OuterClass
        End Sub
    End Class
    '.....
End Namespace


    </file>
    </compilation>, expectedOutput:="Handled Event On SubObject!").
                VerifyIL("Project1.Sink.set_x(Project1.OuterClass)",
            <![CDATA[
{
  // Code size       49 (0x31)
  .maxstack  2
  .locals init (Project1.EventSource.MyEventEventHandler V_0)
  IL_0000:  ldarg.0
  IL_0001:  ldftn      "Sub Project1.Sink.foo()"
  IL_0007:  newobj     "Sub Project1.EventSource.MyEventEventHandler..ctor(Object, System.IntPtr)"
  IL_000c:  stloc.0
  IL_000d:  ldarg.0
  IL_000e:  ldfld      "Project1.Sink._x As Project1.OuterClass"
  IL_0013:  brfalse.s  IL_001b
  IL_0015:  ldloc.0
  IL_0016:  call       "Sub Project1.EventSource.remove_MyEvent(Project1.EventSource.MyEventEventHandler)"
  IL_001b:  ldarg.0
  IL_001c:  ldarg.1
  IL_001d:  stfld      "Project1.Sink._x As Project1.OuterClass"
  IL_0022:  ldarg.0
  IL_0023:  ldfld      "Project1.Sink._x As Project1.OuterClass"
  IL_0028:  brfalse.s  IL_0030
  IL_002a:  ldloc.0
  IL_002b:  call       "Sub Project1.EventSource.add_MyEvent(Project1.EventSource.MyEventEventHandler)"
  IL_0030:  ret
}
    ]]>)
        End Sub

    End Class
End Namespace

