﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.IO
Imports System.Xml.Linq
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.Text
Imports Roslyn.Test.Utilities
Imports Microsoft.CodeAnalysis.CodeGen
Imports Xunit

Public MustInherit Class BasicTestBase
    Inherits BasicTestBaseBase

    Protected Shadows ReadOnly Property DefaultCompilationOptions As VisualBasicCompilationOptions
        Get
            Return DirectCast(MyBase.DefaultCompilationOptions, VisualBasicCompilationOptions)
        End Get
    End Property

    Protected Shadows ReadOnly Property OptionsDll As VisualBasicCompilationOptions
        Get
            Return DirectCast(MyBase.OptionsDll, VisualBasicCompilationOptions)
        End Get
    End Property

    Protected Overloads Function GetCompilationForEmit(
        source As IEnumerable(Of String),
        additionalRefs() As MetadataReference,
        options As VisualBasicCompilationOptions
    ) As VisualBasicCompilation
        Return DirectCast(MyBase.GetCompilationForEmit(source, additionalRefs, options), VisualBasicCompilation)
    End Function

    Private Function Translate(action As Action(Of ModuleSymbol)) As Action(Of IModuleSymbol, EmitOptions)
        If action IsNot Nothing Then
            Return Sub(m, _omitted) action(DirectCast(m, ModuleSymbol))
        Else
            Return Nothing
        End If
    End Function

    Private Function Translate(action As Action(Of PEAssembly)) As Action(Of PEAssembly, EmitOptions)
        If action IsNot Nothing Then
            Return Sub(a, _omitted) action(a)
        Else
            Return Nothing
        End If
    End Function

    ' TODO (tomat): EmitOptions.All
    Friend Shadows Function CompileAndVerify(
        source As XElement,
        expectedOutput As XCData,
        Optional additionalRefs() As MetadataReference = Nothing,
        Optional dependencies As IEnumerable(Of ModuleData) = Nothing,
        Optional emitOptions As EmitOptions = EmitOptions.CCI,
        Optional sourceSymbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional validator As Action(Of PEAssembly, EmitOptions) = Nothing,
        Optional symbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional expectedSignatures As SignatureDescription() = Nothing,
        Optional options As VisualBasicCompilationOptions = Nothing,
        Optional collectEmittedAssembly As Boolean = True,
        Optional emitPdb As Boolean = False,
        Optional parseOptions As VisualBasicParseOptions = Nothing,
        Optional verify As Boolean = True
    ) As CompilationVerifier
        Return CompileAndVerify(
            source,
            If(expectedOutput IsNot Nothing, expectedOutput.Value.Replace(vbLf, Environment.NewLine), Nothing),
            additionalRefs,
            dependencies,
            emitOptions,
            sourceSymbolValidator,
            validator,
            symbolValidator,
            expectedSignatures,
            options,
            collectEmittedAssembly,
            emitPdb,
            parseOptions,
            verify)
    End Function

    ' TODO (tomat): remove - here only to override EmitOptions default
    Friend Shadows Function CompileAndVerify(
        compilation As Compilation,
        Optional manifestResources As IEnumerable(Of ResourceDescription) = Nothing,
        Optional dependencies As IEnumerable(Of ModuleData) = Nothing,
        Optional emitOptions As EmitOptions = EmitOptions.CCI,
        Optional sourceSymbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional validator As Action(Of PEAssembly, EmitOptions) = Nothing,
        Optional symbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional expectedSignatures As SignatureDescription() = Nothing,
        Optional expectedOutput As String = Nothing,
        Optional collectEmittedAssembly As Boolean = True,
        Optional emitPdb As Boolean = False,
        Optional verify As Boolean = True) As CompilationVerifier

        Return MyBase.CompileAndVerify(
            compilation,
            manifestResources,
            dependencies,
            emitOptions,
            Translate(sourceSymbolValidator),
            validator,
            Translate(symbolValidator),
            expectedSignatures,
            expectedOutput,
            collectEmittedAssembly,
            emitPdb,
            verify)
    End Function

    Friend Shadows Function CompileAndVerify(
        compilation As Compilation,
        expectedOutput As XCData,
        Optional dependencies As IEnumerable(Of ModuleData) = Nothing,
        Optional emitOptions As EmitOptions = EmitOptions.CCI,
        Optional sourceSymbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional validator As Action(Of PEAssembly, EmitOptions) = Nothing,
        Optional symbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional expectedSignatures As SignatureDescription() = Nothing,
        Optional collectEmittedAssembly As Boolean = True,
        Optional emitPdb As Boolean = False,
        Optional verify As Boolean = True) As CompilationVerifier

        Return CompileAndVerify(
            compilation,
            Nothing,
            dependencies,
            emitOptions,
            sourceSymbolValidator,
            validator,
            symbolValidator,
            expectedSignatures,
            If(expectedOutput IsNot Nothing, expectedOutput.Value.Replace(vbLf, Environment.NewLine), Nothing),
            collectEmittedAssembly,
            emitPdb,
            verify)
    End Function

    ' TODO: EmitOptions.All
    Friend Shadows Function CompileAndVerify(
        source As XElement,
        Optional expectedOutput As String = Nothing,
        Optional additionalRefs() As MetadataReference = Nothing,
        Optional dependencies As IEnumerable(Of ModuleData) = Nothing,
        Optional emitOptions As EmitOptions = EmitOptions.CCI,
        Optional sourceSymbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional validator As Action(Of PEAssembly, EmitOptions) = Nothing,
        Optional symbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional expectedSignatures As SignatureDescription() = Nothing,
        Optional options As VisualBasicCompilationOptions = Nothing,
        Optional collectEmittedAssembly As Boolean = True,
        Optional emitPdb As Boolean = False,
        Optional parseOptions As VisualBasicParseOptions = Nothing,
        Optional verify As Boolean = True,
        Optional useLatestFramework As Boolean = False
    ) As CompilationVerifier

        Dim defaultRefs = If(useLatestFramework, LatestReferences, DefaultReferences)
        Dim allReferences = If(additionalRefs IsNot Nothing, defaultRefs.Concat(additionalRefs), defaultRefs)

        Return Me.CompileAndVerify(source,
                                   allReferences,
                                   expectedOutput,
                                   dependencies,
                                   emitOptions,
                                   sourceSymbolValidator,
                                   validator,
                                   symbolValidator,
                                   expectedSignatures,
                                   options,
                                   collectEmittedAssembly,
                                   emitPdb,
                                   parseOptions,
                                   verify)

    End Function

    ' TODO: EmitOptions.All
    Friend Shadows Function CompileAndVerify(
        source As XElement,
        allReferences As IEnumerable(Of MetadataReference),
        Optional expectedOutput As String = Nothing,
        Optional dependencies As IEnumerable(Of ModuleData) = Nothing,
        Optional emitOptions As EmitOptions = EmitOptions.CCI,
        Optional sourceSymbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional validator As Action(Of PEAssembly, EmitOptions) = Nothing,
        Optional symbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional expectedSignatures As SignatureDescription() = Nothing,
        Optional options As VisualBasicCompilationOptions = Nothing,
        Optional collectEmittedAssembly As Boolean = True,
        Optional emitPdb As Boolean = False,
        Optional parseOptions As VisualBasicParseOptions = Nothing,
        Optional verify As Boolean = True
    ) As CompilationVerifier

        If options Is Nothing Then
            options = DefaultCompilationOptions.WithOutputKind(If(expectedOutput IsNot Nothing, OutputKind.ConsoleApplication, OutputKind.DynamicallyLinkedLibrary))
        End If

        If emitPdb Then
            options = options.WithOptimizations(False)
        End If

        Dim compilation = CompilationUtils.CreateCompilationWithReferences(source, references:=allReferences, options:=options, parseOptions:=parseOptions)

        Return MyBase.CompileAndVerify(
            compilation,
            Nothing,
            dependencies,
            emitOptions,
            Translate(sourceSymbolValidator),
            validator,
            Translate(symbolValidator),
            expectedSignatures,
            expectedOutput,
            collectEmittedAssembly,
            emitPdb,
            verify)
    End Function

    Friend Shadows Function CompileAndVerifyOnWin8Only(
        source As XElement,
        allReferences As IEnumerable(Of MetadataReference),
        Optional expectedOutput As String = Nothing,
        Optional dependencies As IEnumerable(Of ModuleData) = Nothing,
        Optional emitOptions As EmitOptions = EmitOptions.CCI,
        Optional sourceSymbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional validator As Action(Of PEAssembly) = Nothing,
        Optional symbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional expectedSignatures As SignatureDescription() = Nothing,
        Optional options As VisualBasicCompilationOptions = Nothing,
        Optional collectEmittedAssembly As Boolean = True,
        Optional emitPdb As Boolean = False,
        Optional parseOptions As VisualBasicParseOptions = Nothing,
        Optional verify As Boolean = True
    ) As CompilationVerifier
        Return Me.CompileAndVerify(
            source,
            allReferences,
            If(OSVersion.IsWin8, expectedOutput, Nothing),
            dependencies,
            emitOptions,
            sourceSymbolValidator,
            Translate(validator),
            symbolValidator,
            expectedSignatures,
            options,
            collectEmittedAssembly,
            emitPdb,
            parseOptions,
            verify:=OSVersion.IsWin8)
    End Function

    ' TODO (tomat): EmitOptions.All
    Friend Shadows Function CompileAndVerifyOnWin8Only(
        source As XElement,
        expectedOutput As XCData,
        Optional allReferences() As MetadataReference = Nothing,
        Optional dependencies As IEnumerable(Of ModuleData) = Nothing,
        Optional emitOptions As EmitOptions = EmitOptions.CCI,
        Optional sourceSymbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional validator As Action(Of PEAssembly) = Nothing,
        Optional symbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional expectedSignatures As SignatureDescription() = Nothing,
        Optional options As VisualBasicCompilationOptions = Nothing,
        Optional collectEmittedAssembly As Boolean = True,
        Optional emitPdb As Boolean = False,
        Optional parseOptions As VisualBasicParseOptions = Nothing,
        Optional verify As Boolean = True
    ) As CompilationVerifier
        Return CompileAndVerifyOnWin8Only(
            source,
            allReferences,
            If(expectedOutput IsNot Nothing, expectedOutput.Value.Replace(vbLf, Environment.NewLine), Nothing),
            dependencies,
            emitOptions,
            sourceSymbolValidator,
            validator,
            symbolValidator,
            expectedSignatures,
            options,
            collectEmittedAssembly,
            emitPdb,
            parseOptions,
            verify)
    End Function

    Friend Shadows Function CompileAndVerifyOnWin8Only(
        source As XElement,
        Optional expectedOutput As String = Nothing,
        Optional additionalRefs() As MetadataReference = Nothing,
        Optional dependencies As IEnumerable(Of ModuleData) = Nothing,
        Optional emitOptions As EmitOptions = EmitOptions.CCI,
        Optional sourceSymbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional validator As Action(Of PEAssembly) = Nothing,
        Optional symbolValidator As Action(Of ModuleSymbol) = Nothing,
        Optional expectedSignatures As SignatureDescription() = Nothing,
        Optional options As VisualBasicCompilationOptions = Nothing,
        Optional collectEmittedAssembly As Boolean = True,
        Optional emitPdb As Boolean = False,
        Optional parseOptions As VisualBasicParseOptions = Nothing,
        Optional verify As Boolean = True,
        Optional useLatestFramework As Boolean = False
    ) As CompilationVerifier
        Return CompileAndVerify(
            source,
            expectedOutput:=If(OSVersion.IsWin8, expectedOutput, Nothing),
            additionalRefs:=additionalRefs,
            dependencies:=dependencies,
            emitOptions:=emitOptions,
            sourceSymbolValidator:=sourceSymbolValidator,
            validator:=Translate(validator),
            symbolValidator:=symbolValidator,
            expectedSignatures:=expectedSignatures,
            options:=options,
            collectEmittedAssembly:=collectEmittedAssembly,
            emitPdb:=emitPdb,
            parseOptions:=parseOptions,
            verify:=OSVersion.IsWin8 AndAlso verify,
            useLatestFramework:=useLatestFramework)
    End Function

    ''' <summary>
    ''' Compile sources and adds a custom reference using a custom IL
    ''' </summary>
    ''' <param name="sources">The sources compile according to the following schema        
    ''' &lt;compilation name="assemblyname[optional]"&gt;
    ''' &lt;file name="file1.vb[optional]"&gt;
    ''' source
    ''' &lt;/file&gt;
    ''' &lt;/compilation&gt;
    ''' </param>
    ''' <param name="ilSource"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Friend Function CompileWithCustomILSource(sources As XElement, ilSource As XCData) As CompilationVerifier
        Return CompileWithCustomILSource(sources, ilSource.Value)
    End Function

    ''' <summary>
    ''' Compile sources and adds a custom reference using a custom IL
    ''' </summary>
    ''' <param name="sources">The sources compile according to the following schema        
    ''' &lt;compilation name="assemblyname[optional]"&gt;
    ''' &lt;file name="file1.vb[optional]"&gt;
    ''' source
    ''' &lt;/file&gt;
    ''' &lt;/compilation&gt;
    ''' </param>
    Friend Function CompileWithCustomILSource(sources As XElement, ilSource As String, Optional options As VisualBasicCompilationOptions = Nothing, Optional compilationVerifier As Action(Of VisualBasicCompilation) = Nothing, Optional emitOptions As EmitOptions = EmitOptions.All, Optional expectedOutput As String = Nothing) As CompilationVerifier
        If expectedOutput IsNot Nothing Then
            options = options.WithOutputKind(OutputKind.ConsoleApplication)
        End If

        If ilSource = Nothing Then
            Return CompileAndVerify(sources)
        End If

        Dim reference As MetadataReference = Nothing
        Using tempAssembly = SharedCompilationUtils.IlasmTempAssembly(ilSource)
            reference = New MetadataImageReference(ReadFromFile(tempAssembly.Path))
        End Using

        Dim compilation = CreateCompilationWithReferences(sources, {MscorlibRef, MsvbRef, reference}, options)

        If compilationVerifier IsNot Nothing Then
            compilationVerifier(compilation)
        End If


        Return CompileAndVerify(compilation, emitOptions:=emitOptions, expectedOutput:=expectedOutput)
    End Function

    Friend Overloads Function CompileAndVerifyFieldMarshal(source As XElement,
                                                           expectedBlobs As Dictionary(Of String, Byte()),
                                                           Optional getExpectedBlob As Func(Of String, PEAssembly, EmitOptions, Byte()) = Nothing,
                                                           Optional expectedSignatures As SignatureDescription() = Nothing,
                                                           Optional isField As Boolean = True) As CompilationVerifier
        Return CompileAndVerifyFieldMarshal(source,
                                            Function(s, _omitted1, omitted2)
                                                Assert.True(expectedBlobs.ContainsKey(s), "Expecting marshalling blob for " & If(isField, "field ", "parameter ") & s)
                                                Return expectedBlobs(s)
                                            End Function,
                                            expectedSignatures,
                                            isField)
    End Function

    Friend Overloads Function CompileAndVerifyFieldMarshal(source As XElement,
                                                           getExpectedBlob As Func(Of String, PEAssembly, EmitOptions, Byte()),
                                                           Optional expectedSignatures As SignatureDescription() = Nothing,
                                                           Optional isField As Boolean = True) As CompilationVerifier
        Return CompileAndVerify(source,
                                options:=OptionsDll,
                                validator:=Sub(assembly, emitOptions) MarshalAsMetadataValidator(assembly, getExpectedBlob, emitOptions, isField),
                                expectedSignatures:=expectedSignatures)
    End Function

End Class

Public MustInherit Class BasicTestBaseBase
    Inherits CommonTestBase

    Friend Shared Function Diagnostic(code As ERRID) As DiagnosticDescription
        Dim syntaxNodePredicate As Func(Of VisualBasicSyntaxNode, Boolean) = Nothing
        Return New DiagnosticDescription(code:=CType(code, Integer),
                                         squiggledText:=CType(Nothing, String),
                                         arguments:=Nothing,
                                         startLocation:=Nothing,
                                         syntaxNodePredicate:=Nothing,
                                         argumentOrderDoesNotMatter:=False, errorCodeType:=GetType(ERRID))
    End Function

    Friend Shared Function Diagnostic(code As ERRID, squiggledText As String, Optional arguments As Object() = Nothing, Optional startLocation As LinePosition? = Nothing, Optional syntaxNodePredicate As Func(Of VisualBasicSyntaxNode, Boolean) = Nothing, Optional argumentOrderDoesNotMatter As Boolean = False) As DiagnosticDescription
        Return New DiagnosticDescription(code:=CType(code, Integer),
                                         squiggledText:=squiggledText,
                                         arguments:=arguments,
                                         startLocation:=startLocation,
                                         syntaxNodePredicate:=DirectCast(syntaxNodePredicate, Func(Of SyntaxNode, Boolean)),
                                         argumentOrderDoesNotMatter:=argumentOrderDoesNotMatter,
                                         errorCodeType:=GetType(ERRID))
    End Function

    Friend Shared Function Diagnostic(code As ERRID, squiggledText As XCData, Optional arguments As Object() = Nothing, Optional startLocation As LinePosition? = Nothing, Optional syntaxNodePredicate As Func(Of VisualBasicSyntaxNode, Boolean) = Nothing, Optional argumentOrderDoesNotMatter As Boolean = False) As DiagnosticDescription
        'Additional Overload taking XCData which needs to call NormalizeDiagnosticString to ensure that differences in end of line characters are normalized
        Return New DiagnosticDescription(code:=CType(code, Integer),
                                         squiggledText:=NormalizeDiagnosticString(squiggledText.Value),
                                         arguments:=arguments,
                                         startLocation:=startLocation,
                                         syntaxNodePredicate:=DirectCast(syntaxNodePredicate, Func(Of SyntaxNode, Boolean)),
                                         argumentOrderDoesNotMatter:=argumentOrderDoesNotMatter,
                                         errorCodeType:=GetType(ERRID))
    End Function

    Private Shared Function NormalizeDiagnosticString(inputString As String) As String
        Dim NormalizedString = ""

        If inputString.Contains(vbCrLf) = False Then
            If (inputString.Contains(vbLf)) Then
                NormalizedString = inputString.Replace(vbLf, vbCrLf)
            Else
                NormalizedString = inputString
            End If
        Else
            NormalizedString = inputString
        End If

        Return NormalizedString
    End Function

    Friend Overrides Function ReferencesToModuleSymbols(references As IEnumerable(Of MetadataReference), Optional importOptions As MetadataImportOptions = MetadataImportOptions.Public) As IEnumerable(Of IModuleSymbol)
        Dim options = DirectCast(OptionsDll, VisualBasicCompilationOptions).WithMetadataImportOptions(importOptions)
        Dim tc1 = VisualBasicCompilation.Create("Dummy", references:=references, options:=options)
        Return references.Select(
            Function(r)
                If r.Properties.Kind = MetadataImageKind.Assembly Then
                    Dim assemblySymbol = tc1.GetReferencedAssemblySymbol(r)
                    Return If(assemblySymbol Is Nothing, Nothing, assemblySymbol.Modules(0))
                Else
                    Return tc1.GetReferencedModuleSymbol(r)
                End If
            End Function)
    End Function

    Protected Overrides ReadOnly Property DefaultCompilationOptions As CompilationOptions
        Get
            Return New VisualBasicCompilationOptions(OutputKind.ConsoleApplication, optimize:=True)
        End Get
    End Property

    Protected Overrides ReadOnly Property OptionsDll As CompilationOptions
        Get
            Return New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimize:=True)
        End Get
    End Property

    Dim _lazyDefaultReferences As MetadataReference()
    Dim _lazyLatestReferences As MetadataReference()

    Protected ReadOnly Property DefaultReferences As MetadataReference()
        Get
            If _lazyDefaultReferences Is Nothing Then
                _lazyDefaultReferences = {MscorlibRef, SystemRef, SystemCoreRef, MsvbRef}
            End If

            Return _lazyDefaultReferences
        End Get
    End Property

    Protected ReadOnly Property LatestReferences As MetadataReference()
        Get
            If _lazyLatestReferences Is Nothing Then
                _lazyLatestReferences = {MscorlibRef_v4_0_30316_17626, SystemRef_v4_0_30319_17929, SystemCoreRef_v4_0_30319_17929, MsvbRef_v4_0_30319_17929}
            End If

            Return _lazyLatestReferences
        End Get
    End Property

    Protected Overrides Function GetCompilationForEmit(
        source As IEnumerable(Of String),
        additionalRefs() As MetadataReference,
        options As CompilationOptions
    ) As Compilation
        Return VisualBasicCompilation.Create(
            GetUniqueName(),
            syntaxTrees:=source.Select(Function(t) VisualBasicSyntaxTree.ParseText(t)),
            references:=If(additionalRefs IsNot Nothing, DefaultReferences.Concat(additionalRefs), DefaultReferences),
            options:=DirectCast(options, VisualBasicCompilationOptions))
    End Function

    Friend Shared Function GetAttributeNames(attributes As ImmutableArray(Of SynthesizedAttributeData)) As IEnumerable(Of String)
        Return attributes.Select(Function(a) a.AttributeClass.Name)
    End Function

    Friend Shared Function GetAttributeNames(attributes As ImmutableArray(Of VisualBasicAttributeData)) As IEnumerable(Of String)
        Return attributes.Select(Function(a) a.AttributeClass.Name)
    End Function

    Friend Overrides Function VisualizeRealIL(peModule As IModuleSymbol, methodData As CompilationTestData.MethodData) As String
        Throw New NotImplementedException()
    End Function

    Friend Function GetSymbolsFromBinaryReference(bytes() As Byte) As AssemblySymbol
        Return MetadataTestHelpers.GetSymbolsForReferences({bytes}).Single()
    End Function

    Public Shared Shadows Function GetPdbXml(compilation As VisualBasicCompilation, Optional methodName As String = "") As XElement
        Return XElement.Parse(TestBase.GetPdbXml(compilation, methodName))
    End Function

    Public Shared Shadows Function GetSequencePoints(pdbXml As XElement) As XElement
        Return <sequencePoints>
                   <%= From entry In pdbXml.<methods>.<method>.<sequencepoints>.<entry>
                       Select <entry start_row=<%= entry.@start_row %>
                                  start_column=<%= entry.@start_column %>
                                  end_row=<%= entry.@end_row %>
                                  end_column=<%= entry.@end_column %>/> %>
               </sequencePoints>
    End Function

    Public Shared ReadOnly ClassesWithReadWriteProperties As XCData = <![CDATA[
.class public auto ansi beforefieldinit B
       extends [mscorlib]System.Object
{
  .method public hidebysig newslot specialname virtual 
          instance int32  get_P_rw_r_w() cil managed
  {
    // Code size       2 (0x2)
    .maxstack  8
    IL_0000:  ldc.i4.1
    IL_0001:  ret
  } // end of method B::get_P_rw_r_w

  .method public hidebysig newslot specialname virtual 
          instance void  set_P_rw_r_w(int32 'value') cil managed
  {
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method B::set_P_rw_r_w

  .method public hidebysig newslot specialname virtual 
          instance int32  get_P_rw_rw_w() cil managed
  {
    // Code size       2 (0x2)
    .maxstack  8
    IL_0000:  ldc.i4.1
    IL_0001:  ret
  } // end of method B::get_P_rw_rw_w

  .method public hidebysig newslot specialname virtual 
          instance void  set_P_rw_rw_w(int32 'value') cil managed
  {
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method B::set_P_rw_rw_w

  .method public hidebysig newslot specialname virtual 
          instance int32  get_P_rw_rw_r() cil managed
  {
    // Code size       2 (0x2)
    .maxstack  8
    IL_0000:  ldc.i4.1
    IL_0001:  ret
  } // end of method B::get_P_rw_rw_r

  .method public hidebysig newslot specialname virtual 
          instance void  set_P_rw_rw_r(int32 'value') cil managed
  {
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method B::set_P_rw_rw_r

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
    IL_0006:  ret
  } // end of method B::.ctor

  .property instance int32 P_rw_r_w()
  {
    .get instance int32 B::get_P_rw_r_w()
    .set instance void B::set_P_rw_r_w(int32)
  } // end of property B::P_rw_r_w
  .property instance int32 P_rw_rw_w()
  {
    .set instance void B::set_P_rw_rw_w(int32)
    .get instance int32 B::get_P_rw_rw_w()
  } // end of property B::P_rw_rw_w
  .property instance int32 P_rw_rw_r()
  {
    .get instance int32 B::get_P_rw_rw_r()
    .set instance void B::set_P_rw_rw_r(int32)
  } // end of property B::P_rw_rw_r
} // end of class B

.class public auto ansi beforefieldinit D1
       extends B
{
  .method public hidebysig specialname virtual 
          instance int32  get_P_rw_r_w() cil managed
  {
    // Code size       2 (0x2)
    .maxstack  8
    IL_0000:  ldc.i4.1
    IL_0001:  ret
  } // end of method D1::get_P_rw_r_w

  .method public hidebysig specialname virtual 
          instance int32  get_P_rw_rw_w() cil managed
  {
    // Code size       2 (0x2)
    .maxstack  8
    IL_0000:  ldc.i4.1
    IL_0001:  ret
  } // end of method D1::get_P_rw_rw_w

  .method public hidebysig specialname virtual 
          instance void  set_P_rw_rw_w(int32 'value') cil managed
  {
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method D1::set_P_rw_rw_w

  .method public hidebysig specialname virtual 
          instance int32  get_P_rw_rw_r() cil managed
  {
    // Code size       2 (0x2)
    .maxstack  8
    IL_0000:  ldc.i4.1
    IL_0001:  ret
  } // end of method D1::get_P_rw_rw_r

  .method public hidebysig specialname virtual 
          instance void  set_P_rw_rw_r(int32 'value') cil managed
  {
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method D1::set_P_rw_rw_r

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void B::.ctor()
    IL_0006:  ret
  } // end of method D1::.ctor

  .property instance int32 P_rw_r_w()
  {
    .get instance int32 D1::get_P_rw_r_w()
  } // end of property D1::P_rw_r_w
  .property instance int32 P_rw_rw_w()
  {
    .get instance int32 D1::get_P_rw_rw_w()
    .set instance void D1::set_P_rw_rw_w(int32)
  } // end of property D1::P_rw_rw_w
  .property instance int32 P_rw_rw_r()
  {
    .get instance int32 D1::get_P_rw_rw_r()
    .set instance void D1::set_P_rw_rw_r(int32)
  } // end of property D1::P_rw_rw_r
} // end of class D1

.class public auto ansi beforefieldinit D2
       extends D1
{
  .method public hidebysig specialname virtual 
          instance void  set_P_rw_r_w(int32 'value') cil managed
  {
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method D2::set_P_rw_r_w

  .method public hidebysig specialname virtual 
          instance void  set_P_rw_rw_w(int32 'value') cil managed
  {
    // Code size       1 (0x1)
    .maxstack  8
    IL_0000:  ret
  } // end of method D2::set_P_rw_rw_w

  .method public hidebysig specialname virtual 
          instance int32  get_P_rw_rw_r() cil managed
  {
    // Code size       2 (0x2)
    .maxstack  8
    IL_0000:  ldc.i4.1
    IL_0001:  ret
  } // end of method D2::get_P_rw_rw_r

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void D1::.ctor()
    IL_0006:  ret
  } // end of method D2::.ctor

  .property instance int32 P_rw_r_w()
  {
    .set instance void D2::set_P_rw_r_w(int32)
  } // end of property D2::P_rw_r_w
  .property instance int32 P_rw_rw_w()
  {
    .set instance void D2::set_P_rw_rw_w(int32)
  } // end of property D2::P_rw_rw_w
  .property instance int32 P_rw_rw_r()
  {
    .get instance int32 D2::get_P_rw_rw_r()
  } // end of property D2::P_rw_rw_r
} // end of class D2
]]>

    Public Class NameSyntaxFinder
        Inherits VisualBasicSyntaxWalker

        Private Sub New()
            MyBase.New(SyntaxWalkerDepth.StructuredTrivia)
        End Sub

        Public Overrides Sub DefaultVisit(node As SyntaxNode)
            Dim name = TryCast(node, NameSyntax)
            If name IsNot Nothing Then
                Me._names.Add(name)
            End If

            MyBase.DefaultVisit(node)
        End Sub

        Private _names As New List(Of NameSyntax)

        Public Shared Function FindNames(node As SyntaxNode) As List(Of NameSyntax)
            Dim finder As New NameSyntaxFinder()
            finder.Visit(node)
            Return finder._names
        End Function
    End Class

    Public Class ExpressionSyntaxFinder
        Inherits VisualBasicSyntaxWalker

        Private Sub New()
            MyBase.New(SyntaxWalkerDepth.StructuredTrivia)
        End Sub

        Public Overrides Sub DefaultVisit(node As SyntaxNode)
            Dim expr = TryCast(node, ExpressionSyntax)
            If expr IsNot Nothing Then
                Me._expressions.Add(expr)
            End If

            MyBase.DefaultVisit(node)
        End Sub

        Private _expressions As New List(Of ExpressionSyntax)

        Public Shared Function FindExpression(node As SyntaxNode) As List(Of ExpressionSyntax)
            Dim finder As New ExpressionSyntaxFinder()
            finder.Visit(node)
            Return finder._expressions
        End Function
    End Class

    Public Class SyntaxNodeFinder
        Inherits VisualBasicSyntaxWalker

        Private Sub New()
            MyBase.New(SyntaxWalkerDepth.StructuredTrivia)
        End Sub

        Public Overrides Sub DefaultVisit(node As SyntaxNode)
            If node IsNot Nothing AndAlso Me._kinds.Contains(node.VisualBasicKind) Then
                Me._nodes.Add(node)
            End If

            MyBase.DefaultVisit(node)
        End Sub

        Private _nodes As New List(Of SyntaxNode)
        Private _kinds As New HashSet(Of SyntaxKind)

        Public Shared Function FindNodes(Of T As SyntaxNode)(node As SyntaxNode, ParamArray kinds() As SyntaxKind) As List(Of T)
            Return New List(Of T)(From s In FindNodes(node, kinds) Select DirectCast(s, T))
        End Function

        Public Shared Function FindNodes(node As SyntaxNode, ParamArray kinds() As SyntaxKind) As List(Of SyntaxNode)
            Dim finder As New SyntaxNodeFinder()
            finder._kinds.AddAll(kinds)
            finder.Visit(node)
            Return finder._nodes
        End Function
    End Class

    Public Class TypeComparer
        Implements IComparer(Of NamedTypeSymbol)

        Private Function Compare(x As NamedTypeSymbol, y As NamedTypeSymbol) As Integer Implements IComparer(Of NamedTypeSymbol).Compare
            Dim result As Integer = IdentifierComparison.Comparer.Compare(x.Name, y.Name)

            If result <> 0 Then
                Return result
            End If

            Return x.Arity - y.Arity
        End Function
    End Class

End Class
