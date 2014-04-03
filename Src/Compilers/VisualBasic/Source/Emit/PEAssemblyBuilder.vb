﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Reflection
Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic.Emit

    Friend MustInherit Class PEAssemblyBuilderBase
        Inherits PEModuleBuilder
        Implements Cci.IAssembly

        Private ReadOnly m_SourceAssembly As SourceAssemblySymbol
        Private m_LazyFiles As ImmutableArray(Of Cci.IFileReference)

        ''' <summary>
        ''' This value will override m_SourceModule.MetadataName.
        ''' </summary>
        ''' <remarks>
        ''' This functionality exists for parity with C#, which requires it for
        ''' legacy reasons (see Microsoft.CodeAnalysis.CSharp.Emit.PEAssemblyBuilderBase.metadataName).
        ''' </remarks>
        Private ReadOnly m_MetadataName As String

        Public Sub New(sourceAssembly As SourceAssemblySymbol,
                       outputName As String,
                       outputKind As OutputKind,
                       serializationProperties As ModulePropertiesForSerialization,
                       manifestResources As IEnumerable(Of ResourceDescription),
                       assemblySymbolMapper As Func(Of AssemblySymbol, AssemblyIdentity))

            MyBase.New(DirectCast(sourceAssembly.Modules(0), SourceModuleSymbol), outputName, outputKind, serializationProperties, manifestResources, assemblySymbolMapper)

            Debug.Assert(sourceAssembly IsNot Nothing)
            Debug.Assert(manifestResources IsNot Nothing)

            Me.m_SourceAssembly = sourceAssembly
            Me.m_MetadataName = If(outputName Is Nothing, sourceAssembly.MetadataName, PathUtilities.RemoveExtension(outputName))
            m_AssemblyOrModuleSymbolToModuleRefMap.Add(sourceAssembly, Me)
        End Sub

        Public Overrides Sub Dispatch(visitor As Cci.MetadataVisitor)
            visitor.Visit(DirectCast(Me, Cci.IAssembly))
        End Sub

        Private Function IAssemblyGetFiles(context As Microsoft.CodeAnalysis.Emit.Context) As IEnumerable(Of Cci.IFileReference) Implements Cci.IAssembly.GetFiles
            If m_LazyFiles.IsDefault Then
                Dim builder = ArrayBuilder(Of Cci.IFileReference).GetInstance()
                Try
                    Dim modules = m_SourceAssembly.Modules

                    For i As Integer = 1 To modules.Length - 1
                        builder.Add(DirectCast(Translate(modules(i), context.Diagnostics), Cci.IFileReference))
                    Next

                    For Each resource In ManifestResources
                        If Not resource.IsEmbedded Then
                            builder.Add(resource)
                        End If
                    Next

                    ' Dev12 compilers don't report ERR_CryptoHashFailed if there are no files to be hashed.
                    If ImmutableInterlocked.InterlockedInitialize(m_LazyFiles, builder.ToImmutable()) AndAlso m_LazyFiles.Length > 0 Then
                        If Not CryptographicHashProvider.IsSupportedAlgorithm(m_SourceAssembly.AssemblyHashAlgorithm) Then
                            context.Diagnostics.Add(New VBDiagnostic(ErrorFactory.ErrorInfo(ERRID.ERR_CryptoHashFailed), NoLocation.Singleton))
                        End If
                    End If
                Finally
                    ' Clean up so we don't get a leak report from the unit tests.
                    builder.Free()
                End Try
            End If

            Return m_LazyFiles
        End Function

        Private Shared Function Free(builder As ArrayBuilder(Of Cci.IFileReference)) As Boolean
            builder.Free()
            Return False
        End Function

        Private ReadOnly Property IAssemblyFlags As UInteger Implements Cci.IAssembly.Flags
            Get
                Dim result As System.Reflection.AssemblyNameFlags = m_SourceAssembly.Flags And Not System.Reflection.AssemblyNameFlags.PublicKey

                If Not m_SourceAssembly.PublicKey.IsDefaultOrEmpty Then
                    result = result Or System.Reflection.AssemblyNameFlags.PublicKey
                End If

                Return CUInt(result)
            End Get
        End Property

        Private ReadOnly Property IAssemblySignatureKey As String Implements Cci.IAssembly.SignatureKey
            Get
                Return m_SourceAssembly.AssemblySignatureKeyAttributeSetting
            End Get
        End Property

        Private ReadOnly Property IAssemblyPublicKey As IEnumerable(Of Byte) Implements Cci.IAssembly.PublicKey
            Get
                Return m_SourceAssembly.Identity.PublicKey
            End Get
        End Property

        Protected Overrides Sub AddEmbeddedResourcesFromAddedModules(builder As ArrayBuilder(Of Cci.ManagedResource), diagnostics As DiagnosticBag)
            Dim modules = m_SourceAssembly.Modules

            For i As Integer = 1 To modules.Length - 1
                Dim file = DirectCast(Translate(modules(i), diagnostics), Cci.IFileReference)

                Try
                    For Each resource In DirectCast(modules(i), Symbols.Metadata.PE.PEModuleSymbol).Module.GetEmbeddedResourcesOrThrow()
                        builder.Add(New Cci.ManagedResource(
                            resource.Name,
                            (resource.Attributes And ManifestResourceAttributes.Public) <> 0,
                            Nothing,
                            file,
                            resource.Offset))
                    Next
                Catch mrEx As BadImageFormatException
                    diagnostics.Add(ERRID.ERR_UnsupportedModule1, NoLocation.Singleton, modules(i))
                End Try
            Next
        End Sub

        Private ReadOnly Property IAssemblyReferenceCulture As String Implements Cci.IAssemblyReference.Culture
            Get
                Return m_SourceAssembly.Identity.CultureName
            End Get
        End Property

        Private ReadOnly Property IAssemblyReferenceIsRetargetable As Boolean Implements Cci.IAssemblyReference.IsRetargetable
            Get
                Return m_SourceAssembly.Identity.IsRetargetable
            End Get
        End Property

        Private ReadOnly Property IAssemblyReferenceContentType As AssemblyContentType Implements Cci.IAssemblyReference.ContentType
            Get
                Return m_SourceAssembly.Identity.ContentType
            End Get
        End Property

        Private ReadOnly Property IAssemblyReferencePublicKeyToken As IEnumerable(Of Byte) Implements Cci.IAssemblyReference.PublicKeyToken
            Get
                Return m_SourceAssembly.Identity.PublicKeyToken
            End Get
        End Property

        Private ReadOnly Property IAssemblyReferenceVersion As Version Implements Cci.IAssemblyReference.Version
            Get
                Return m_SourceAssembly.Identity.Version
            End Get
        End Property

        Friend Overrides ReadOnly Property Name As String
            Get
                Return m_MetadataName
            End Get
        End Property

        Private ReadOnly Property IAssemblyHashAlgorithm As AssemblyHashAlgorithm Implements Cci.IAssembly.HashAlgorithm
            Get
                Return m_SourceAssembly.AssemblyHashAlgorithm
            End Get
        End Property
    End Class

    Friend NotInheritable Class PEAssemblyBuilder
        Inherits PEAssemblyBuilderBase

        Public Sub New(sourceAssembly As SourceAssemblySymbol,
                       outputName As String,
                       outputKind As OutputKind,
                       serializationProperties As ModulePropertiesForSerialization,
                       manifestResources As IEnumerable(Of ResourceDescription),
                       Optional assemblySymbolMapper As Func(Of AssemblySymbol, AssemblyIdentity) = Nothing)

            MyBase.New(sourceAssembly, outputName, outputKind, serializationProperties, manifestResources, assemblySymbolMapper)
        End Sub
    End Class

End Namespace
