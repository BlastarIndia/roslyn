﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Xml.Linq
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols.Metadata.PE
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests.Symbols.Metadata
Imports Roslyn.Test.Utilities
Imports TypeKind = Microsoft.CodeAnalysis.TypeKind

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests

    Public Class EventSymbolTests
        Inherits BasicTestBase

        <WorkItem(542806)>
        <Fact()>
        Public Sub EmptyCustomEvent()
            Dim source = <compilation name="F">
                             <file name="F.vb">
Class C
    Public Custom Event Foo
End Class

                             </file>
                         </compilation>


            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertTheseParseDiagnostics(comp2,
<expected>
BC31122: 'Custom' modifier is not valid on events declared without explicit delegate types.
    Public Custom Event Foo
    ~~~~~~~~~~~~~~~~~~~~~~~~
</expected>)
        End Sub

        <WorkItem(542891)>
        <Fact()>
        Public Sub InterfaceImplements()
            Dim source = <compilation name="F">
                             <file name="F.vb">
Imports System.ComponentModel 

Class C    
    Implements INotifyPropertyChanged 
    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
End Class
                             </file>
                         </compilation>

            Dim comp1 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.Off))
            CompilationUtils.AssertNoErrors(comp1)
            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertNoErrors(comp2)
        End Sub

        <Fact()>
        Public Sub RaiseBaseEventedFromDerivedNestedTypes()
            Dim source =
<compilation>
    <file name="filename.vb">
Module Program
    Sub Main()
    End Sub
End Module
Class C1
    Event HelloWorld
    Class C2
        Inherits C1
        Sub t
            RaiseEvent HelloWorld
        End Sub
    End Class
End Class
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source).VerifyDiagnostics()
        End Sub

        <Fact()>
        Public Sub MultipleInterfaceImplements()
            Dim source =
            <compilation>
                <file name="filename.vb">
Option Infer On
Imports System
Imports System.Collections.Generic
Interface I
    Event E As Action(Of Integer)
    Event E2 As Action(Of String)
End Interface
Interface I2
    Event E As Action(Of Integer)
    Event E2 As Action(Of String)
End Interface


Class Base
    Implements I
    Implements I2
    Event E2(x As Integer) Implements I.E, I2.E

    Dim eventsList As List(Of Action(Of String)) = New List(Of Action(Of String))
    Public Custom Event E As Action(Of String) Implements I.E2, I2.E2
        AddHandler(e As Action(Of String))
            Console.Write("Add E|")
            eventsList.Add(e)
        End AddHandler

        RemoveHandler(e As Action(Of String))
            Console.Write("Remove E|")
            eventsList.Remove(e)
        End RemoveHandler

        RaiseEvent()
            Dim x As String = Nothing
            Console.Write("Raise E|")
            For Each ev In eventsList
                ev(x)
            Next
        End RaiseEvent
    End Event
    Sub R
        RaiseEvent E
    End Sub
End Class
Module Module1
    Sub Main(args As String())
        Dim b = New Base
        Dim a As Action(Of String) = Sub(x)
                                         Console.Write("Added from Base|")
                                     End Sub
        AddHandler b.E, a

        Dim i_1 As I = b
        Dim i_2 As I2 = b

        RemoveHandler i_1.E2, a

        AddHandler i_2.E2, Sub(x)
                               Console.Write("Added from I2|")
                           End Sub

        b.R
    End Sub
End Module
    </file>
            </compilation>
            CompileAndVerify(source,
                             expectedOutput:=
            <![CDATA[Add E|Remove E|Add E|Raise E|Added from I2|]]>.Value
)

        End Sub


        <WorkItem(543309)>
        <Fact()>
        Public Sub EventSyntheticDelegateShadows()
            Dim source = <compilation name="F">
                             <file name="F.vb">
Public MustInherit Class GameLauncher    
    Public Event Empty()
End Class 

Public Class MissileLauncher    
    Inherits GameLauncher 
    Public Shadows Event Empty()
End Class
                             </file>
                         </compilation>

            Dim comp1 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.Off))
            CompilationUtils.AssertNoErrors(comp1)
            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertNoErrors(comp2)
        End Sub


        <Fact()>
        Public Sub EventNoShadows()
            Dim source = <compilation name="F">
                             <file name="F.vb">
                                 <![CDATA[       
Public MustInherit Class GameLauncher    
    Public Event Empty()
End Class 

Public Class MissileLauncher    
    Inherits GameLauncher 
    Public Event Empty()
End Class
]]>
                             </file>
                         </compilation>


            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertTheseDiagnostics(comp2,
<expected>
    <![CDATA[   
BC40004: event 'Empty' conflicts with event 'Empty' in the base class 'GameLauncher' and should be declared 'Shadows'.
    Public Event Empty()
                 ~~~~~
]]>
</expected>)
        End Sub

        <Fact()>
        Public Sub EventAutoPropShadows()
            Dim source = <compilation name="F">
                             <file name="F.vb">
                                 <![CDATA[       
Public MustInherit Class GameLauncher    
    Public Event _Empty()
End Class

Public Class MissileLauncher
    Inherits GameLauncher
    Public Property EmptyEventhandler As Integer
End Class

Public MustInherit Class GameLauncher1
    Public Property EmptyEventhandler As Integer
End Class

Public Class MissileLauncher1
    Inherits GameLauncher1
    Public Event _Empty()
End Class
]]>
                             </file>
                         </compilation>


            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertTheseDiagnostics(comp2,
<expected>
    <![CDATA[   
BC40018: property 'EmptyEventhandler' implicitly declares '_EmptyEventhandler', which conflicts with a member implicitly declared for event '_Empty' in the base class 'GameLauncher'. property should be declared 'Shadows'.
    Public Property EmptyEventhandler As Integer
                    ~~~~~~~~~~~~~~~~~
]]>
</expected>)
        End Sub

        <Fact()>
        Public Sub EventAutoPropClash()
            Dim source = <compilation name="F">
                             <file name="F.vb">
                                 <![CDATA[       
Public Class MissileLauncher1
    Public Event _Empty()
    Public Property EmptyEventhandler As Integer
End Class

Public Class MissileLauncher2
    Public Property EmptyEventhandler As Integer
    Public Event _Empty()
End Class


]]>
                             </file>
                         </compilation>

            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertTheseDiagnostics(comp2,
<expected>
BC31059: event '_Empty' implicitly defines '_EmptyEventHandler', which conflicts with a member implicitly declared for property 'EmptyEventhandler' in class 'MissileLauncher1'.
    Public Event _Empty()
                 ~~~~~~
BC31059: event '_Empty' implicitly defines '_EmptyEventHandler', which conflicts with a member implicitly declared for property 'EmptyEventhandler' in class 'MissileLauncher2'.
    Public Event _Empty()
                 ~~~~~~
</expected>)
        End Sub

        <Fact()>
        Public Sub EventNoShadows1()
            Dim source = <compilation name="F">
                             <file name="F.vb">
                                 <![CDATA[       
Public MustInherit Class GameLauncher    
    Public EmptyEventHandler as integer
End Class 

Public Class MissileLauncher    
    Inherits GameLauncher 
    Public Event Empty()
End Class
]]>
                             </file>
                         </compilation>


            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertTheseDiagnostics(comp2,
<expected>
    <![CDATA[   
BC40012: event 'Empty' implicitly declares 'EmptyEventHandler', which conflicts with a member in the base class 'GameLauncher', and so the event should be declared 'Shadows'.
    Public Event Empty()
                 ~~~~~
]]>
</expected>)
        End Sub

        <Fact()>
        Public Sub EventsAreNotValues()
            Dim source = <compilation name="F">
                             <file name="F.vb">
                                 <![CDATA[       
Class cls1
    Event e1()
    Event e2()

    Sub foo()
        System.Console.WriteLine(e1)
        System.Console.WriteLine(e1 + (e2))
    End Sub
End Class
]]>
                             </file>
                         </compilation>


            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertTheseDiagnostics(comp2,
<expected>
    <![CDATA[   
BC32022: 'Public Event e1()' is an event, and cannot be called directly. Use a 'RaiseEvent' statement to raise an event.
        System.Console.WriteLine(e1)
                                 ~~
BC32022: 'Public Event e1()' is an event, and cannot be called directly. Use a 'RaiseEvent' statement to raise an event.
        System.Console.WriteLine(e1 + (e2))
                                 ~~
BC32022: 'Public Event e2()' is an event, and cannot be called directly. Use a 'RaiseEvent' statement to raise an event.
        System.Console.WriteLine(e1 + (e2))
                                       ~~
]]>
</expected>)
        End Sub

        <Fact()>
        Public Sub EventImplementsInInterfaceAndModule()
            Dim source = <compilation name="F">
                             <file name="F.vb">
                                 <![CDATA[       
Interface I1
    Event e1()
End Interface

Interface I2
    Inherits I1

    Event e2() Implements I1.e1
End Interface

Module m1
    Event e2() Implements I1.e1
End Module
]]>
                             </file>
                         </compilation>


            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertTheseDiagnostics(comp2,
<expected>
    <![CDATA[   
BC30688: Events in interfaces cannot be declared 'Implements'.
    Event e2() Implements I1.e1
               ~~~~~~~~~~
BC31083: Members in a Module cannot implement interface members.
    Event e2() Implements I1.e1
               ~~~~~~~~~~
]]>
</expected>)
        End Sub

        <Fact()>
        Public Sub AttributesInapplicable()
            Dim source = <compilation name="F">
                             <file name="F.vb">
                                 <![CDATA[       
Imports System
                        
Class cls0
    <System.ParamArray()>
    Event RegularEvent()

    <System.ParamArray()>
    Custom Event CustomEvent As Action
        AddHandler(value As Action)

        End AddHandler

        RemoveHandler(value As Action)

        End RemoveHandler

        RaiseEvent()

        End RaiseEvent
    End Event
End Class
]]>
                             </file>
                         </compilation>


            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertTheseDiagnostics(comp2,
<expected>
    <![CDATA[   
BC30662: Attribute 'ParamArrayAttribute' cannot be applied to 'RegularEvent' because the attribute is not valid on this declaration type.
    <System.ParamArray()>
     ~~~~~~~~~~~~~~~~~
BC30662: Attribute 'ParamArrayAttribute' cannot be applied to 'CustomEvent' because the attribute is not valid on this declaration type.
    <System.ParamArray()>
     ~~~~~~~~~~~~~~~~~
]]>
</expected>)
        End Sub

        <Fact()>
        Public Sub AttributesApplicable()
            Dim source = <compilation name="F">
                             <file name="F.vb">
                                 <![CDATA[       
Imports System
                        
Class cls0
    <Obsolete>
    Event RegularEvent()

    <Obsolete>
    Custom Event CustomEvent As Action
        AddHandler(value As Action)

        End AddHandler

        RemoveHandler(value As Action)

        End RemoveHandler

        RaiseEvent()

        End RaiseEvent
    End Event
End Class
]]>
                             </file>
                         </compilation>


            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertNoErrors(comp2)


            Dim attributeValidatorSource = Sub(m As ModuleSymbol)

                                               ' Event should have an Obsolete attribute
                                               Dim type = DirectCast(m.GlobalNamespace.GetMember("cls0"), NamedTypeSymbol)
                                               Dim member = type.GetMember("RegularEvent")
                                               Dim attrs = member.GetAttributes()
                                               Assert.Equal(1, attrs.Length)
                                               Assert.Equal("System.ObsoleteAttribute", attrs(0).AttributeClass.ToDisplayString)

                                               ' additional synthetic members (field, accesors and such) should not
                                               member = type.GetMember("RegularEventEvent")
                                               attrs = member.GetAttributes()
                                               Assert.Equal(0, attrs.Length)

                                               member = type.GetMember("RegularEventEventHandler")
                                               attrs = member.GetAttributes()
                                               Assert.Equal(0, attrs.Length)

                                               member = type.GetMember("add_RegularEvent")
                                               attrs = member.GetAttributes()
                                               Assert.Equal(0, attrs.Length)

                                               member = type.GetMember("remove_RegularEvent")
                                               attrs = member.GetAttributes()
                                               Assert.Equal(0, attrs.Length)

                                               ' Event should have an Obsolete attribute
                                               member = type.GetMember("CustomEvent")
                                               attrs = member.GetAttributes()
                                               Assert.Equal(1, attrs.Length)
                                               Assert.Equal("System.ObsoleteAttribute", attrs(0).AttributeClass.ToDisplayString)

                                               ' additional synthetic members (field, accesors and such) should not
                                               member = type.GetMember("add_CustomEvent")
                                               attrs = member.GetAttributes()
                                               Assert.Equal(0, attrs.Length)

                                               member = type.GetMember("remove_CustomEvent")
                                               attrs = member.GetAttributes()
                                               Assert.Equal(0, attrs.Length)

                                               member = type.GetMember("raise_CustomEvent")
                                               attrs = member.GetAttributes()
                                               Assert.Equal(0, attrs.Length)
                                           End Sub


            ' metadata verifier excludes private members as those are not loaded.
            Dim attributeValidatorMetadata = Sub(m As ModuleSymbol)

                                                 ' Event should have an Obsolete attribute
                                                 Dim type = DirectCast(m.GlobalNamespace.GetMember("cls0"), NamedTypeSymbol)
                                                 Dim member = type.GetMember("RegularEvent")
                                                 Dim attrs = member.GetAttributes()
                                                 Assert.Equal(1, attrs.Length)
                                                 Assert.Equal("System.ObsoleteAttribute", attrs(0).AttributeClass.ToDisplayString)

                                                 ' additional synthetic members (field, accesors and such) should not
                                                 'member = type.GetMember("RegularEventEvent")
                                                 'attrs = member.GetAttributes()
                                                 'Assert.Equal(0, attrs.Count)

                                                 member = type.GetMember("RegularEventEventHandler")
                                                 attrs = member.GetAttributes()
                                                 Assert.Equal(0, attrs.Length)

                                                 member = type.GetMember("add_RegularEvent")
                                                 attrs = member.GetAttributes()
                                                 Assert.Equal(1, attrs.Length)
                                                 Assert.Equal("CompilerGeneratedAttribute", attrs(0).AttributeClass.Name)

                                                 member = type.GetMember("remove_RegularEvent")
                                                 attrs = member.GetAttributes()
                                                 Assert.Equal(1, attrs.Length)
                                                 Assert.Equal("CompilerGeneratedAttribute", attrs(0).AttributeClass.Name)

                                                 ' Event should have an Obsolete attribute
                                                 member = type.GetMember("CustomEvent")
                                                 attrs = member.GetAttributes()
                                                 Assert.Equal(1, attrs.Length)
                                                 Assert.Equal("System.ObsoleteAttribute", attrs(0).AttributeClass.ToDisplayString)

                                                 ' additional synthetic members (field, accesors and such) should not
                                                 member = type.GetMember("add_CustomEvent")
                                                 attrs = member.GetAttributes()
                                                 Assert.Equal(0, attrs.Length)

                                                 member = type.GetMember("remove_CustomEvent")
                                                 attrs = member.GetAttributes()
                                                 Assert.Equal(0, attrs.Length)

                                                 'member = type.GetMember("raise_CustomEvent")
                                                 'attrs = member.GetAttributes()
                                                 'Assert.Equal(0, attrs.Count)
                                             End Sub


            ' Verify attributes from source and then load metadata to see attributes are written correctly.
            CompileAndVerify(source, sourceSymbolValidator:=attributeValidatorSource,
                             symbolValidator:=attributeValidatorMetadata)

        End Sub

        <WorkItem(543321)>
        <Fact()>
        Public Sub DeclareEventWithArgument()
            CompileAndVerify(
    <compilation name="DeclareEventWithArgument">
        <file name="a.vb">
Class Test
    Public Event Percent(ByVal Percent1 As Single)
    Public Shared Sub Main()
    End Sub
End Class
    </file>
    </compilation>)
        End Sub

        <WorkItem(543366)>
        <Fact()>
        Public Sub UseEventDelegateType()
            CompileAndVerify(
    <compilation name="DeclareEventWithArgument">
        <file name="a.vb">
Class C
    Event Hello()
End Class
Module Program
    Sub Main(args As String())
        Dim cc As C = New C
        Dim a As C.HelloEventHandler = AddressOf Handler
        AddHandler cc.Hello, a
    End Sub
    Sub Handler()
    End Sub
End Module
    </file>
    </compilation>)
        End Sub

        <WorkItem(543372)>
        <Fact()>
        Public Sub AddHandlerWithoutAddressOf()
            Dim source = <compilation name="F">
                             <file name="F.vb">
Class C
    Event Hello()
End Class

Module Program
    Sub Foo()
    End Sub
    Sub Main(args As String())
        Dim x As C
        AddHandler x.Hello, Foo
    End Sub
End Module

                             </file>
                         </compilation>


            Dim comp2 = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(source, OptionsDll.WithOptionStrict(OptionStrict.On))
            CompilationUtils.AssertTheseDiagnostics(comp2,
<expected>
BC42104: Variable 'x' is used before it has been assigned a value. A null reference exception could result at runtime.
        AddHandler x.Hello, Foo
                   ~
BC30491: Expression does not produce a value.
        AddHandler x.Hello, Foo
                            ~~~
</expected>)
        End Sub

        <Fact()>
        Public Sub EvnetPrivateAccessor()
            Dim ilSource = <![CDATA[
.class public auto ansi beforefieldinit ClassLibrary1.Class1
       extends [mscorlib]System.Object
{
  .field private class [mscorlib]System.Action E1
  .method private hidebysig specialname instance void 
          add_E1(class [mscorlib]System.Action 'value') cil managed
  {
    // Code size       41 (0x29)
    .maxstack  3
    .locals init (class [mscorlib]System.Action V_0,
             class [mscorlib]System.Action V_1,
             class [mscorlib]System.Action V_2)
    IL_0000:  ldarg.0
    IL_0001:  ldfld      class [mscorlib]System.Action ClassLibrary1.Class1::E1
    IL_0006:  stloc.0
    IL_0007:  ldloc.0
    IL_0008:  stloc.1
    IL_0009:  ldloc.1
    IL_000a:  ldarg.1
    IL_000b:  call       class [mscorlib]System.Delegate [mscorlib]System.Delegate::Combine(class [mscorlib]System.Delegate,
                                                                                            class [mscorlib]System.Delegate)
    IL_0010:  castclass  [mscorlib]System.Action
    IL_0015:  stloc.2
    IL_0016:  ldarg.0
    IL_0017:  ldflda     class [mscorlib]System.Action ClassLibrary1.Class1::E1
    IL_001c:  ldloc.2
    IL_001d:  ldloc.1
    IL_001e:  call       !!0 [mscorlib]System.Threading.Interlocked::CompareExchange<class [mscorlib]System.Action>(!!0&,
                                                                                                                    !!0,
                                                                                                                    !!0)
    IL_0023:  stloc.0
    IL_0024:  ldloc.0
    IL_0025:  ldloc.1
    IL_0026:  bne.un.s   IL_0007

    IL_0028:  ret
  } // end of method Class1::add_E1

  .method public hidebysig specialname instance void 
          remove_E1(class [mscorlib]System.Action 'value') cil managed
  {
    // Code size       41 (0x29)
    .maxstack  3
    .locals init (class [mscorlib]System.Action V_0,
             class [mscorlib]System.Action V_1,
             class [mscorlib]System.Action V_2)
    IL_0000:  ldarg.0
    IL_0001:  ldfld      class [mscorlib]System.Action ClassLibrary1.Class1::E1
    IL_0006:  stloc.0
    IL_0007:  ldloc.0
    IL_0008:  stloc.1
    IL_0009:  ldloc.1
    IL_000a:  ldarg.1
    IL_000b:  call       class [mscorlib]System.Delegate [mscorlib]System.Delegate::Remove(class [mscorlib]System.Delegate,
                                                                                           class [mscorlib]System.Delegate)
    IL_0010:  castclass  [mscorlib]System.Action
    IL_0015:  stloc.2
    IL_0016:  ldarg.0
    IL_0017:  ldflda     class [mscorlib]System.Action ClassLibrary1.Class1::E1
    IL_001c:  ldloc.2
    IL_001d:  ldloc.1
    IL_001e:  call       !!0 [mscorlib]System.Threading.Interlocked::CompareExchange<class [mscorlib]System.Action>(!!0&,
                                                                                                                    !!0,
                                                                                                                    !!0)
    IL_0023:  stloc.0
    IL_0024:  ldloc.0
    IL_0025:  ldloc.1
    IL_0026:  bne.un.s   IL_0007

    IL_0028:  ret
  } // end of method Class1::remove_E1

  .method public hidebysig instance void 
          Raise(int32 x) cil managed
  {
    // Code size       12 (0xc)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldfld      class [mscorlib]System.Action ClassLibrary1.Class1::E1
    IL_0006:  callvirt   instance void [mscorlib]System.Action::Invoke()
    IL_000b:  ret
  } // end of method Class1::Raise

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
    IL_0006:  ret
  } // end of method Class1::.ctor

  .event [mscorlib]System.Action E1
  {
    .addon instance void ClassLibrary1.Class1::add_E1(class [mscorlib]System.Action)
    .removeon instance void ClassLibrary1.Class1::remove_E1(class [mscorlib]System.Action)
  } // end of event Class1::E1
} // end of class ClassLibrary1.Class1

]]>

            Dim vbSource =
<compilation name="PublicParameterlessConstructorInMetadata_Private">
    <file name="a.vb">
Class Program
    Sub Main()
        Dim x = New ClassLibrary1.Class1

        Dim h as System.Action = Sub() System.Console.WriteLine("hello")

        AddHandler x.E1, h
        RemoveHandler x.E1, h

        x.Raise(1)
    End Sub
End Class
    </file>
</compilation>

            Dim comp2 = CreateCompilationWithCustomILSource(vbSource, ilSource.Value, OptionsDll)

            CompilationUtils.AssertTheseDiagnostics(comp2,
<expected>
    <![CDATA[   
BC30456: 'E1' is not a member of 'ClassLibrary1.Class1'.
        AddHandler x.E1, h
                   ~~~~
BC30456: 'E1' is not a member of 'ClassLibrary1.Class1'.
        RemoveHandler x.E1, h
                      ~~~~
]]>
</expected>)
        End Sub

        <Fact(Skip:="behaves as in Dev10 - unverifiable code. Should we do something more useful?")>
        Public Sub EvnetProtectedAccessor()
            Dim ilSource = <![CDATA[
.class public auto ansi beforefieldinit ClassLibrary1.Class1
       extends [mscorlib]System.Object
{
  .field private class [mscorlib]System.Action E1
  .method public hidebysig specialname instance void 
          add_E1(class [mscorlib]System.Action 'value') cil managed
  {
    // Code size       41 (0x29)
    .maxstack  3
    .locals init (class [mscorlib]System.Action V_0,
             class [mscorlib]System.Action V_1,
             class [mscorlib]System.Action V_2)
    IL_0000:  ldarg.0
    IL_0001:  ldfld      class [mscorlib]System.Action ClassLibrary1.Class1::E1
    IL_0006:  stloc.0
    IL_0007:  ldloc.0
    IL_0008:  stloc.1
    IL_0009:  ldloc.1
    IL_000a:  ldarg.1
    IL_000b:  call       class [mscorlib]System.Delegate [mscorlib]System.Delegate::Combine(class [mscorlib]System.Delegate,
                                                                                            class [mscorlib]System.Delegate)
    IL_0010:  castclass  [mscorlib]System.Action
    IL_0015:  stloc.2
    IL_0016:  ldarg.0
    IL_0017:  ldflda     class [mscorlib]System.Action ClassLibrary1.Class1::E1
    IL_001c:  ldloc.2
    IL_001d:  ldloc.1
    IL_001e:  call       !!0 [mscorlib]System.Threading.Interlocked::CompareExchange<class [mscorlib]System.Action>(!!0&,
                                                                                                                    !!0,
                                                                                                                    !!0)
    IL_0023:  stloc.0
    IL_0024:  ldloc.0
    IL_0025:  ldloc.1
    IL_0026:  bne.un.s   IL_0007

    IL_0028:  ret
  } // end of method Class1::add_E1

  .method family hidebysig specialname instance void 
          remove_E1(class [mscorlib]System.Action 'value') cil managed
  {
    // Code size       41 (0x29)
    .maxstack  3
    .locals init (class [mscorlib]System.Action V_0,
             class [mscorlib]System.Action V_1,
             class [mscorlib]System.Action V_2)
    IL_0000:  ldarg.0
    IL_0001:  ldfld      class [mscorlib]System.Action ClassLibrary1.Class1::E1
    IL_0006:  stloc.0
    IL_0007:  ldloc.0
    IL_0008:  stloc.1
    IL_0009:  ldloc.1
    IL_000a:  ldarg.1
    IL_000b:  call       class [mscorlib]System.Delegate [mscorlib]System.Delegate::Remove(class [mscorlib]System.Delegate,
                                                                                           class [mscorlib]System.Delegate)
    IL_0010:  castclass  [mscorlib]System.Action
    IL_0015:  stloc.2
    IL_0016:  ldarg.0
    IL_0017:  ldflda     class [mscorlib]System.Action ClassLibrary1.Class1::E1
    IL_001c:  ldloc.2
    IL_001d:  ldloc.1
    IL_001e:  call       !!0 [mscorlib]System.Threading.Interlocked::CompareExchange<class [mscorlib]System.Action>(!!0&,
                                                                                                                    !!0,
                                                                                                                    !!0)
    IL_0023:  stloc.0
    IL_0024:  ldloc.0
    IL_0025:  ldloc.1
    IL_0026:  bne.un.s   IL_0007

    IL_0028:  ret
  } // end of method Class1::remove_E1

  .method public hidebysig instance void 
          Raise(int32 x) cil managed
  {
    // Code size       12 (0xc)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldfld      class [mscorlib]System.Action ClassLibrary1.Class1::E1
    IL_0006:  callvirt   instance void [mscorlib]System.Action::Invoke()
    IL_000b:  ret
  } // end of method Class1::Raise

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
    IL_0006:  ret
  } // end of method Class1::.ctor

  .event [mscorlib]System.Action E1
  {
    .addon instance void ClassLibrary1.Class1::add_E1(class [mscorlib]System.Action)
    .removeon instance void ClassLibrary1.Class1::remove_E1(class [mscorlib]System.Action)
  } // end of event Class1::E1
} // end of class ClassLibrary1.Class1

]]>

            Dim vbSource =
<compilation name="PublicParameterlessConstructorInMetadata_Private">
    <file name="a.vb">
Class Program
    Sub Main()
        Dim x = New ClassLibrary1.Class1

        Dim h as System.Action = Sub() System.Console.WriteLine("hello")

        AddHandler x.E1, h
        RemoveHandler x.E1, h

        x.Raise(1)
    End Sub
End Class
    </file>
</compilation>

            CompileWithCustomILSource(vbSource, ilSource.Value, OptionsDll, emitOptions:=EmitOptions.RefEmitBug).
    VerifyIL("C.M",
            <![CDATA[

]]>)
        End Sub

        ' Check that both errors are reported
        <WorkItem(543504)>
        <Fact()>
        Public Sub TestEventWithParamArray()
            Dim source =
<compilation name="TestEventWithParamArray">
    <file name="a.vb">
        <![CDATA[
Class A
    Event E1(paramarray o() As object)
    Delegate Sub d(paramarray o() As object)
End Class

Module Program
    Sub Main(args As String())
    End Sub
End Module
]]>
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntimeAndReferences(source)
            comp.VerifyDiagnostics(Diagnostic(ERRID.ERR_ParamArrayIllegal1, "paramarray").WithArguments("Event"),
                                   Diagnostic(ERRID.ERR_ParamArrayIllegal1, "paramarray").WithArguments("Delegate"))
        End Sub

        'import abstract class with abstract event and attempt to override the event
        <Fact()>
        Public Sub EventOverridingAndInterop()

            Dim ilSource = <![CDATA[
// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi beforefieldinit AbsEvent
       extends [mscorlib]System.Object
{
  .field private class [mscorlib]System.Action E
  .method family hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       9 (0x9)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
    IL_0006:  br.s       IL_0008

    IL_0008:  ret
  } // end of method AbsEvent::.ctor

  .method public hidebysig newslot specialname abstract virtual 
          instance void  add_E(class [mscorlib]System.Action 'value') cil managed
  {
  } // end of method AbsEvent::add_E

  .method public hidebysig newslot specialname abstract virtual 
          instance void  remove_E(class [mscorlib]System.Action 'value') cil managed
  {
  } // end of method AbsEvent::remove_E

  .event [mscorlib]System.Action E
  {
    .addon instance void AbsEvent::add_E(class [mscorlib]System.Action)
    .removeon instance void AbsEvent::remove_E(class [mscorlib]System.Action)
  } // end of event AbsEvent::E
} // end of class AbsEvent


]]>

            Dim vbSource =
<compilation>
    <file name="b.vb">
Class B
    Inherits AbsEvent
    Overrides Public Event E As System.Action
End Class
    </file>
</compilation>

            Dim comp = CompilationUtils.CreateCompilationWithCustomILSource(vbSource, ilSource)

            'Error BC30243 'Overrides' is not valid on an event declaration.
            'warning BC40004: event 'E' conflicts with event 'E' in the base class 'AbsEvent' and should be declared 'Shadows'.
            'error BC30610: Class 'B' must either be declared 'MustInherit' or override the following inherited 'MustOverride' member(s): 
            '    AbsEvent: Public MustOverride Event E As System.Action.
            comp.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_BadEventFlags1, "Overrides").WithArguments("Overrides"),
                Diagnostic(ERRID.WRN_OverrideType5, "E").WithArguments("event", "E", "event", "class", "AbsEvent"),
                Diagnostic(ERRID.ERR_BaseOnlyClassesMustBeExplicit2, "B").WithArguments("B", "error BC0000: " & vbNewLine & "    AbsEvent: Public MustOverride Event E As System.Action")
                )
        End Sub

        <Fact()>
        Public Sub EventInGenericTypes()
            Dim vbSource =
<compilation>
    <file name="filename.vb">
Class A(Of T)
    Public Event E1(arg As T)
    Public Event E2 As System.Action(Of T, T)
End Class

Class B
    Sub S
        Dim x = New A(Of String)
        Dim a = New A(Of String).E1EventHandler(Sub(arg)
                                                End Sub)
        AddHandler x.E1, a
        AddHandler x.E2, Sub(a1, a2)
                         End Sub
    End Sub
End Class
    </file>
</compilation>
            CompileAndVerify(vbSource,
                             sourceSymbolValidator:=Sub(moduleSymbol As ModuleSymbol)
                                                        Dim tA = DirectCast(moduleSymbol.GlobalNamespace.GetMember("A"), NamedTypeSymbol)
                                                        Dim tB = DirectCast(moduleSymbol.GlobalNamespace.GetMember("B"), NamedTypeSymbol)
                                                        Dim member = tA.GetMember("E1Event")
                                                        Assert.NotNull(member)
                                                        Dim delegateTypeMember = DirectCast(tA.GetMember("E1EventHandler"), SynthesizedEventDelegateSymbol)
                                                        Assert.NotNull(delegateTypeMember)
                                                        Assert.Equal(delegateTypeMember.AssociatedSymbol, DirectCast(tA.GetMember("E1"), EventSymbol))
                                                    End Sub)

        End Sub
        <Fact()>
        Public Sub BindOnRegularEventParams()
            Dim compilation = CreateCompilationWithMscorlib(
<compilation>
    <file name="a.vb"><![CDATA[
Imports System
Imports System.Collections.Generic
Imports System.Linq

Class C
    Event E(arg1 As Integer, arg2 As String)'BIND:"Integer"
End Class

Module Program
    Sub Main(args As String())

    End Sub
End Module
    ]]></file>
</compilation>)

            Dim semanticSummary = CompilationUtils.GetSemanticInfoSummary(Of PredefinedTypeSyntax)(compilation, "a.vb")

            Assert.Equal("System.Int32", semanticSummary.Type.ToTestDisplayString())
            Assert.Equal(TypeKind.Structure, semanticSummary.Type.TypeKind)
            Assert.Equal("System.Int32", semanticSummary.ConvertedType.ToTestDisplayString())
            Assert.Equal(TypeKind.Structure, semanticSummary.ConvertedType.TypeKind)
            Assert.Equal(ConversionKind.Identity, semanticSummary.ImplicitConversion.Kind)

            Assert.Equal("System.Int32", semanticSummary.Symbol.ToTestDisplayString())
            Assert.Equal(SymbolKind.NamedType, semanticSummary.Symbol.Kind)
            Assert.Equal(0, semanticSummary.CandidateSymbols.Length)

            Assert.Null(semanticSummary.Alias)

            Assert.Equal(0, semanticSummary.MemberGroup.Length)

            Assert.False(semanticSummary.ConstantValue.HasValue)
        End Sub

        <Fact()>
        Public Sub BindOnEventHandlerAddHandler()
            Dim compilation = CreateCompilationWithMscorlib(
<compilation>
    <file name="a.vb"><![CDATA[
Class C
    Event E
End Class

Module Program
    Sub Main(args As String())
        Dim x = New C
        AddHandler x.E, Sub()'BIND:"E"
                        End Sub
    End Sub
End Module
    ]]></file>
</compilation>)

            Dim semanticSummary = CompilationUtils.GetSemanticInfoSummary(Of IdentifierNameSyntax)(compilation, "a.vb")

            Assert.Equal("C.EEventHandler", semanticSummary.Type.ToTestDisplayString())
            Assert.Equal(TypeKind.Delegate, semanticSummary.Type.TypeKind)
            Assert.Equal("C.EEventHandler", semanticSummary.ConvertedType.ToTestDisplayString())
            Assert.Equal(TypeKind.Delegate, semanticSummary.ConvertedType.TypeKind)
            Assert.Equal(ConversionKind.Identity, semanticSummary.ImplicitConversion.Kind)

            Assert.Equal("Event C.E()", semanticSummary.Symbol.ToTestDisplayString())
            Assert.Equal(SymbolKind.Event, semanticSummary.Symbol.Kind)
            Assert.Equal(0, semanticSummary.CandidateSymbols.Length)

            Assert.Null(semanticSummary.Alias)

            Assert.Equal(0, semanticSummary.MemberGroup.Length)

            Assert.False(semanticSummary.ConstantValue.HasValue)
        End Sub

        <Fact()>
        Public Sub BindOnEventPrivateField()
            Dim compilation = CreateCompilationWithMscorlib(
<compilation>
    <file name="a.vb"><![CDATA[
Class C
    Event E
End Class

Module Program
    Sub Main(args As String())
        Dim x = New C
        AddHandler x.EEvent, Sub()'BIND:"EEvent"
                             End Sub
    End Sub
End Module
    ]]></file>
</compilation>)

            Dim semanticSummary = CompilationUtils.GetSemanticInfoSummary(Of IdentifierNameSyntax)(compilation, "a.vb")

            Assert.Equal("C.EEventHandler", semanticSummary.Type.ToTestDisplayString())
            Assert.Equal(TypeKind.Delegate, semanticSummary.Type.TypeKind)
            Assert.Equal("C.EEventHandler", semanticSummary.ConvertedType.ToTestDisplayString())
            Assert.Equal(TypeKind.Delegate, semanticSummary.ConvertedType.TypeKind)
            Assert.Equal(ConversionKind.Identity, semanticSummary.ImplicitConversion.Kind)

            Assert.Null(semanticSummary.Symbol)
            Assert.Equal(CandidateReason.Inaccessible, semanticSummary.CandidateReason)
            Assert.Equal(1, semanticSummary.CandidateSymbols.Length)
            Dim sortedCandidates = semanticSummary.CandidateSymbols.AsEnumerable().OrderBy(Function(s) s.ToTestDisplayString()).ToArray()
            Assert.Equal("C.EEvent As C.EEventHandler", sortedCandidates(0).ToTestDisplayString())
            Assert.Equal(SymbolKind.Field, sortedCandidates(0).Kind)

            Assert.Null(semanticSummary.Alias)

            Assert.Equal(0, semanticSummary.MemberGroup.Length)

            Assert.False(semanticSummary.ConstantValue.HasValue)
        End Sub

        <WorkItem(543447)>
        <Fact()>
        Public Sub BindOnFieldOfRegularEventHandlerType()
            Dim compilation = CreateCompilationWithMscorlib(
<compilation>
    <file name="a.vb"><![CDATA[
Class C
    Dim ev As EEventHandler
    Event E
    Sub T
        ev = Nothing'BIND:"ev"

    End Sub
End Class

    ]]></file>
</compilation>)

            Dim semanticSummary = CompilationUtils.GetSemanticInfoSummary(Of IdentifierNameSyntax)(compilation, "a.vb")

            Assert.Equal("C.EEventHandler", semanticSummary.Type.ToTestDisplayString())
            Assert.Equal(TypeKind.Delegate, semanticSummary.Type.TypeKind)
            Assert.Equal("C.EEventHandler", semanticSummary.ConvertedType.ToTestDisplayString())
            Assert.Equal(TypeKind.Delegate, semanticSummary.ConvertedType.TypeKind)
            Assert.Equal(ConversionKind.Identity, semanticSummary.ImplicitConversion.Kind)

            Assert.Equal("C.ev As C.EEventHandler", semanticSummary.Symbol.ToTestDisplayString())
            Assert.Equal(SymbolKind.Field, semanticSummary.Symbol.Kind)
            Assert.Equal(0, semanticSummary.CandidateSymbols.Length)

            Assert.Null(semanticSummary.Alias)

            Assert.Equal(0, semanticSummary.MemberGroup.Length)

            Assert.False(semanticSummary.ConstantValue.HasValue)
        End Sub

        <WorkItem(543725)>
        <Fact()>
        Public Sub SynthesizedEventDelegateSymbolImplicit()
            Dim compilation = CreateCompilationWithMscorlib(
<compilation>
    <file name="a.vb"><![CDATA[
Imports System

Class C
    Event E()
End Class
    ]]></file>
</compilation>)

            Dim typeC = DirectCast(compilation.SourceModule.GlobalNamespace.GetMembers("C").SingleOrDefault(), NamedTypeSymbol)
            Dim mems = typeC.GetMembers().OrderBy(Function(s) s.ToDisplayString()).Select(Function(s) s)
            'Event Delegate Symbol
            Assert.Equal(TypeKind.Delegate, DirectCast(mems(0), NamedTypeSymbol).TypeKind)
            Assert.True(mems(0).IsImplicitlyDeclared)
            Assert.Equal("C.EEventHandler", mems(0).ToDisplayString())
            'Event Backing Field
            Assert.Equal(SymbolKind.Field, mems(1).Kind)
            Assert.True(mems(1).IsImplicitlyDeclared)
            Assert.Equal("Private EEvent As C.EEventHandler", mems(1).ToDisplayString())

            ' Source Event Symbol
            Assert.Equal(SymbolKind.Event, mems(3).Kind)
            Assert.False(mems(3).IsImplicitlyDeclared)
            Assert.Equal("Public Event E()", mems(3).ToDisplayString())

            ' Add Accessor
            Assert.Equal(MethodKind.EventAdd, DirectCast(mems(2), MethodSymbol).MethodKind)
            Assert.True(mems(2).IsImplicitlyDeclared)
            Assert.Equal("Public AddHandler Event E(obj As C.EEventHandler)", mems(2).ToDisplayString())
            'Remove Accessor
            Assert.Equal(MethodKind.EventRemove, DirectCast(mems(4), MethodSymbol).MethodKind)
            Assert.True(mems(4).IsImplicitlyDeclared)
            Assert.Equal("Public RemoveHandler Event E(obj As C.EEventHandler)", mems(4).ToDisplayString())

        End Sub

        <WorkItem(545200)>
        <Fact()>
        Public Sub TestBadlyFormattedEventCode()
            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(
<compilation>
    <file name="a.vb"><![CDATA[
Imports System<Serializable>Class c11    <NonSerialized()>
    Public Event Start(ByVal sender As Object, ByVal e As EventArgs)
    <NonSerialized>    Dim x As LongEnd Class
    ]]></file>
</compilation>)

            Dim typeMembers = compilation.SourceModule.GlobalNamespace.GetMembers().OfType(Of TypeSymbol)()
            Assert.Equal(1, typeMembers.Count)
            Dim implicitClass = typeMembers.First

            Assert.True(DirectCast(implicitClass, NamedTypeSymbol).IsImplicitClass)
            Assert.False(implicitClass.CanBeReferencedByName)

            Dim classMembers = implicitClass.GetMembers()
            Assert.Equal(7, classMembers.Length)

            Dim eventDelegate = classMembers.OfType(Of SynthesizedEventDelegateSymbol)().Single
            Assert.Equal("StartEventHandler", eventDelegate.Name)
        End Sub

        <WorkItem(545221)>
        <Fact()>
        Public Sub TestBadlyFormattedCustomEvent()
            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(
<compilation>
    <file name="a.vb"><![CDATA[
Imports System
Partial Class c1
    Private Custom Event E1 as
        AddHandler()
        End AddHandler
    End Event
    Partial Private Sub M(i As Integer) Handles Me.E1'BIND:"E1"
    End Sub
    Sub Raise()
        RaiseEvent E1(1)
    End Sub
    Shared Sub Main()
        Call New c1().Raise()
    End Sub
End Class
    ]]></file>
</compilation>)

            Dim semanticSummary = CompilationUtils.GetSemanticInfoSummary(Of IdentifierNameSyntax)(compilation, "a.vb")

            Assert.Null(semanticSummary.Type)
            Assert.Null(semanticSummary.ConvertedType)
            Assert.Equal(ConversionKind.Identity, semanticSummary.ImplicitConversion.Kind)

            Assert.Equal("Event c1.E1 As ?", semanticSummary.Symbol.ToTestDisplayString())
            Assert.Equal(SymbolKind.Event, semanticSummary.Symbol.Kind)
            Assert.Equal(0, semanticSummary.CandidateSymbols.Length)

            Assert.Null(semanticSummary.Alias)

            Assert.Equal(0, semanticSummary.MemberGroup.Length)

            Assert.False(semanticSummary.ConstantValue.HasValue)
        End Sub

        ''' <summary>
        ''' Avoid redundant errors from handlers when
        ''' a custom event type has errors.
        ''' </summary>
        <WorkItem(530406)>
        <Fact(Skip:="530406")>
        Public Sub CustomEventTypeDuplicateErrors()
            Dim compilation = CompilationUtils.CreateCompilationWithMscorlib(
<compilation>
    <file name="a.vb"><![CDATA[
Class C
    Public Custom Event E As D
        AddHandler(value As D)
        End AddHandler
        RemoveHandler(value As D)
        End RemoveHandler
        RaiseEvent()
        End RaiseEvent
    End Event
    Private Delegate Sub D()
End Class
   ]]></file>
</compilation>)
            compilation.AssertTheseDiagnostics(<errors><![CDATA[
BC30508: 'E' cannot expose type 'C.D' in namespace '<Default>' through class 'C'.
    Public Custom Event E As D
                        ~
     ]]></errors>)
        End Sub

        <Fact()>
        Public Sub MissingSystemTypes_Event()
            Dim compilation = CompilationUtils.CreateCompilationWithReferences(
<compilation>
    <file name="a.vb"><![CDATA[
Interface I
    Event E As Object
End Interface
   ]]></file>
</compilation>, references:=Nothing)
            compilation.AssertTheseDiagnostics(<errors><![CDATA[
BC30002: Type 'System.Void' is not defined.
    Event E As Object
          ~
BC30002: Type 'System.Object' is not defined.
    Event E As Object
               ~~~~~~
BC31044: Events declared with an 'As' clause must have a delegate type.
    Event E As Object
               ~~~~~~
     ]]></errors>)
        End Sub

        <Fact()>
        Public Sub MissingSystemTypes_WithEvents()
            Dim compilation = CompilationUtils.CreateCompilationWithReferences(
<compilation name="C">
    <file name="a.vb"><![CDATA[
Class C
    WithEvents F As Object
End Class
   ]]></file>
</compilation>, references:=Nothing)
            compilation.AssertTheseDiagnostics(<errors><![CDATA[
BC30002: Type 'System.Void' is not defined.
Class C
~~~~~~~~
BC31091: Import of type 'Object' from assembly or module 'C.dll' failed.
Class C
      ~
BC30002: Type 'System.Void' is not defined.
    WithEvents F As Object
               ~
BC35000: Requested operation is not available because the runtime library function 'System.Runtime.CompilerServices.AccessedThroughPropertyAttribute..ctor' is not defined.
    WithEvents F As Object
               ~
BC30002: Type 'System.Object' is not defined.
    WithEvents F As Object
                    ~~~~~~
     ]]></errors>)
        End Sub

        <WorkItem(780993)>
        <Fact()>
        Public Sub EventInMemberNames()
            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(
<compilation>
    <file name="a.vb"><![CDATA[
Class C
    Event X As EventHandler
End Class

    ]]></file>
</compilation>)

            Dim typeMembers = compilation.SourceModule.GlobalNamespace.GetMembers().OfType(Of NamedTypeSymbol)()
            Assert.Equal(1, typeMembers.Count)
            Dim c = typeMembers.First

            Dim classMembers = c.MemberNames
            Assert.Equal(1, classMembers.Count)

            Assert.Equal("X", classMembers(0))
        End Sub
    End Class
End Namespace
