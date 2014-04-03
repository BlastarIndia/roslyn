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
    Public Class CodeGenStopOrEnd
        Inherits BasicTestBase

        <Fact>
        Public Sub StopStatement_SimpleTestWithStop()
            Dim Source = <compilation>
                             <file name="a.vb">
                Imports System
                Imports  Microsoft.VisualBasic

                Public Module Module1
                    Public Sub Main()
                        Console.Writeline("Start")
                        Stop
                        Console.Writeline("End")
                    End Sub
                End Module
                    </file>
                         </compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(Source, OptionsExe)
            Dim compilationVerifier = CompileAndVerify(compilation).VerifyIL("Module1.Main",
            <![CDATA[{
  // Code size       26 (0x1a)
  .maxstack  1
  IL_0000:  ldstr      "Start"
  IL_0005:  call       "Sub System.Console.WriteLine(String)"
  IL_000a:  call       "Sub System.Diagnostics.Debugger.Break()"
  IL_000f:  ldstr      "End"
  IL_0014:  call       "Sub System.Console.WriteLine(String)"
  IL_0019:  ret
}]]>)

        End Sub

        <Fact>
        Public Sub StopStatement_SimpleTestWithEndOtherThanMain()
            Dim Source = <compilation>
                             <file name="a.vb">
                Imports System
                Imports  Microsoft.VisualBasic

                Public Module Module1
                    Public Sub Main()
                        Console.Writeline("Start")
                        Foo()
                        Console.Writeline("End")
                    End Sub

                    Sub Foo
                        Console.Writeline("Foo")
                        Stop
                    End Sub
                End Module
                    </file>
                         </compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(Source, OptionsExe)
            Dim compilationVerifier = CompileAndVerify(compilation).VerifyIL("Module1.Foo",
            <![CDATA[{
  // Code size       16 (0x10)
  .maxstack  1
  IL_0000:  ldstr      "Foo"
  IL_0005:  call       "Sub System.Console.WriteLine(String)"
  IL_000a:  call       "Sub System.Diagnostics.Debugger.Break()"
  IL_000f:  ret
}]]>)
        End Sub

        <Fact>
        Public Sub StopStatement_MultipleStatementsOnASingleLine()
            Dim TEMP = CompileAndVerify(
<compilation>
    <file name="a.vb">
                Imports System
                Imports  Microsoft.VisualBasic

                Public Module Module1
                    Public Sub Main()
                        Console.Writeline("Start")
                        Foo() : Stop
                        Console.Writeline("End")
                    End Sub

                    Sub Foo
                        Console.Writeline("Foo")
                    End Sub
                End Module
                    </file>
</compilation>).VerifyIL("Module1.Main",
            <![CDATA[{
  // Code size       31 (0x1f)
  .maxstack  1
  IL_0000:  ldstr      "Start"
  IL_0005:  call       "Sub System.Console.WriteLine(String)"
  IL_000a:  call       "Sub Module1.Foo()"
  IL_000f:  call       "Sub System.Diagnostics.Debugger.Break()"
  IL_0014:  ldstr      "End"
  IL_0019:  call       "Sub System.Console.WriteLine(String)"
  IL_001e:  ret
}]]>)
        End Sub

        <Fact()>
        Public Sub StopStatement_CodeGenVerify()
            ' Ensure that IL contains a call to System.Diagnostics.Debugger.Break
            CompileAndVerify(
    <compilation>
        <file name="a.vb">
        Imports System
        Imports  Microsoft.VisualBasic

        Public Module Module1
            Public Sub Main()
                Console.Writeline("Start")
                Foo()
                Console.Writeline("End")
            End Sub

            Sub Foo
                Console.Writeline("Foo")
                Stop
            End Sub
        End Module
            </file>
    </compilation>).VerifyIL("Module1.Foo",
            <![CDATA[{
  // Code size       16 (0x10)
  .maxstack  1
  IL_0000:  ldstr      "Foo"
  IL_0005:  call       "Sub System.Console.WriteLine(String)"
  IL_000a:  call       "Sub System.Diagnostics.Debugger.Break()"
  IL_000f:  ret
}
]]>)
        End Sub

        <Fact>
        Public Sub EndStatement_SimpleTestWithEnd()
            ' Ensure that IL contains a call to Microsoft.VisualBasic.CompilerServices.ProjectData.EndAp
            Dim Source = <compilation>
                             <file name="a.vb">
        Imports System
        Imports  Microsoft.VisualBasic

        Public Module Module1
            Public Sub Main()
                Console.Writeline("Start")
                End
                Console.Writeline("End")
            End Sub
        End Module
            </file>
                         </compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(Source, OptionsExe)
            Dim compilationVerifier = CompileAndVerify(compilation).VerifyIL("Module1.Main",
            <![CDATA[{
  // Code size       26 (0x1a)
  .maxstack  1
  IL_0000:  ldstr      "Start"
  IL_0005:  call       "Sub System.Console.WriteLine(String)"
  IL_000a:  call       "Sub Microsoft.VisualBasic.CompilerServices.ProjectData.EndApp()"
  IL_000f:  ldstr      "End"
  IL_0014:  call       "Sub System.Console.WriteLine(String)"
  IL_0019:  ret
}]]>)
        End Sub
    End Class

End Namespace