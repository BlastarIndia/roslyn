﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Reflection.Metadata
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic.Emit

    Friend NotInheritable Class PEDeltaAssemblyBuilder
        Inherits PEAssemblyBuilderBase
        Implements IPEDeltaAssemblyBuilder

        Private ReadOnly m_PreviousGeneration As EmitBaseline
        Private ReadOnly m_PreviousDefinitions As DefinitionMap
        Private ReadOnly m_Changes As SymbolChanges

        Public Sub New(sourceAssembly As SourceAssemblySymbol,
                       outputName As String,
                       outputKind As OutputKind,
                       serializationProperties As ModulePropertiesForSerialization,
                       manifestResources As IEnumerable(Of ResourceDescription),
                       assemblySymbolMapper As Func(Of AssemblySymbol, AssemblyIdentity),
                       previousGeneration As EmitBaseline,
                       edits As IEnumerable(Of SemanticEdit))

            MyBase.New(sourceAssembly, outputName, outputKind, serializationProperties, manifestResources, assemblySymbolMapper)

            Dim context = New Context(Me, Nothing, New DiagnosticBag())
            Dim [module] = previousGeneration.OriginalMetadata
            Dim compilation = sourceAssembly.DeclaringCompilation
            Dim metadataAssembly = compilation.GetBoundReferenceManager().CreatePEAssemblyForAssemblyMetadata(AssemblyMetadata.Create([module]), MetadataImportOptions.All)
            Dim metadataDecoder = New Microsoft.CodeAnalysis.VisualBasic.Symbols.Metadata.PE.MetadataDecoder(metadataAssembly.PrimaryModule)

            previousGeneration = EnsureInitialized(previousGeneration, metadataDecoder)

            Dim matchToMetadata = New SymbolMatcher(previousGeneration.AnonymousTypeMap, sourceAssembly, context, metadataAssembly)

            Dim matchToPrevious As SymbolMatcher = Nothing
            If previousGeneration.Ordinal > 0 Then
                Dim previousAssembly = DirectCast(previousGeneration.Compilation, VisualBasicCompilation).SourceAssembly
                Dim previousContext = New Context(DirectCast(previousGeneration.PEModuleBuilder, PEModuleBuilder), Nothing, New DiagnosticBag())
                matchToPrevious = New SymbolMatcher(previousGeneration.AnonymousTypeMap, sourceAssembly, context, previousAssembly, previousContext)
            End If

            Me.m_PreviousDefinitions = New DefinitionMap(previousGeneration.OriginalMetadata.Module, metadataDecoder, matchToMetadata, matchToPrevious, GenerateMethodMap(edits))
            Me.m_PreviousGeneration = previousGeneration
            Me.m_Changes = New SymbolChanges(m_PreviousDefinitions, edits)
        End Sub

        Private Overloads Shared Function GetAnonymousTypeMap(
                                                   reader As MetadataReader,
                                                   metadataDecoder As Microsoft.CodeAnalysis.VisualBasic.Symbols.Metadata.PE.MetadataDecoder) As IReadOnlyDictionary(Of AnonymousTypeKey, AnonymousTypeValue)
            Dim result = New Dictionary(Of AnonymousTypeKey, AnonymousTypeValue)
            For Each handle In reader.TypeDefinitions
                Dim def = reader.GetTypeDefinition(handle)
                If Not def.Namespace.IsNil Then
                    Continue For
                End If
                If Not reader.StringStartsWith(def.Name, GeneratedNames.AnonymousTypeOrDelegateCommonPrefix) Then
                    Continue For
                End If
                Dim metadataName = reader.GetString(def.Name)
                Dim arity As Short = 0
                Dim name = MetadataHelpers.InferTypeArityAndUnmangleMetadataName(metadataName, arity)
                Dim index As Integer = 0
                If GeneratedNames.TryParseAnonymousTypeTemplateName(GeneratedNames.AnonymousTypeTemplateNamePrefix, name, index) Then
                    Dim type = DirectCast(metadataDecoder.GetTypeOfToken(handle), NamedTypeSymbol)
                    Dim key = New AnonymousTypeKey(GetAnonymousTypeKeyFields(type))
                    Dim value = New AnonymousTypeValue(name, index, type)
                    result.Add(key, value)
                ElseIf GeneratedNames.TryParseAnonymousTypeTemplateName(GeneratedNames.AnonymousDelegateTemplateNamePrefix, name, index) Then
                    Dim type = DirectCast(metadataDecoder.GetTypeOfToken(handle), NamedTypeSymbol)
                    Dim key = GetAnonymousDelegateKey(type)
                    Dim value = New AnonymousTypeValue(name, index, type)
                    result.Add(key, value)
                End If
            Next
            Return result
        End Function

        Private Shared Function GetAnonymousTypeKeyFields(type As NamedTypeSymbol) As ImmutableArray(Of String)
            ' The key is the set of properties that correspond to type parameters.
            ' For each type parameter, get the name of the property of that type.
            Dim n = type.TypeParameters.Length
            If n = 0 Then
                Return ImmutableArray(Of String).Empty
            End If

            ' Names of properties indexed by type parameter ordinal.
            Dim propertyNames = New String(n - 1) {}
            For Each member In type.GetMembers()
                If member.Kind <> SymbolKind.Property Then
                    Continue For
                End If

                Dim [property] = DirectCast(member, PropertySymbol)
                Dim propertyType = [property].Type
                If propertyType.TypeKind = TypeKind.TypeParameter Then
                    Dim typeParameter = DirectCast(propertyType, TypeParameterSymbol)
                    Debug.Assert(typeParameter.ContainingSymbol = type)
                    Dim index = typeParameter.Ordinal
                    Debug.Assert(propertyNames(index) Is Nothing)
                    propertyNames(index) = [property].Name
                End If
            Next

            Debug.Assert(propertyNames.All(Function(f) Not String.IsNullOrEmpty(f)))
            Return ImmutableArray.Create(propertyNames)
        End Function

        Private Shared Function GetAnonymousDelegateKey(type As NamedTypeSymbol) As AnonymousTypeKey
            Debug.Assert(type.BaseTypeNoUseSiteDiagnostics.SpecialType = SpecialType.System_MulticastDelegate)

            ' The key is the set of parameter names to the Invoke method,
            ' where the parameters are of the type parameters.
            Dim members = type.GetMembers(WellKnownMemberNames.DelegateInvokeName)
            Debug.Assert(members.Length = 1 AndAlso members(0).Kind = SymbolKind.Method)
            Dim method = DirectCast(members(0), MethodSymbol)
            Debug.Assert(method.Parameters.Count + If(method.IsSub, 0, 1) = type.TypeParameters.Length)
            Dim parameterNames = ArrayBuilder(Of String).GetInstance()
            parameterNames.AddRange(method.Parameters.SelectAsArray(Function(p) p.Name))
            parameterNames.Add(AnonymousTypeDescriptor.GetReturnParameterName(Not method.IsSub))
            Return New AnonymousTypeKey(parameterNames.ToImmutableAndFree(), isDelegate:=True)
        End Function

        Private Shared Function EnsureInitialized(
                                                 previousGeneration As EmitBaseline,
                                                 metadataDecoder As Microsoft.CodeAnalysis.VisualBasic.Symbols.Metadata.PE.MetadataDecoder) As EmitBaseline
            If previousGeneration.AnonymousTypeMap IsNot Nothing Then
                Return previousGeneration
            End If

            Dim anonymousTypeMap = GetAnonymousTypeMap(previousGeneration.MetadataReader, metadataDecoder)
            Return previousGeneration.With(
                previousGeneration.Compilation,
                previousGeneration.PEModuleBuilder,
                previousGeneration.Ordinal,
                previousGeneration.EncId,
                previousGeneration.TypesAdded,
                previousGeneration.EventsAdded,
                previousGeneration.FieldsAdded,
                previousGeneration.MethodsAdded,
                previousGeneration.PropertiesAdded,
                previousGeneration.EventMapAdded,
                previousGeneration.PropertyMapAdded,
                previousGeneration.TableEntriesAdded,
                blobStreamLengthAdded:=previousGeneration.BlobStreamLengthAdded,
                stringStreamLengthAdded:=previousGeneration.StringStreamLengthAdded,
                userStringStreamLengthAdded:=previousGeneration.UserStringStreamLengthAdded,
                guidStreamLengthAdded:=previousGeneration.GuidStreamLengthAdded,
                anonymousTypeMap:=anonymousTypeMap,
                localsForMethodsAddedOrChanged:=previousGeneration.LocalsForMethodsAddedOrChanged,
                localNames:=previousGeneration.LocalNames)
        End Function

        Friend ReadOnly Property PreviousGeneration As EmitBaseline
            Get
                Return m_PreviousGeneration
            End Get
        End Property

        Friend ReadOnly Property PreviousDefinitions As DefinitionMap
            Get
                Return m_PreviousDefinitions
            End Get
        End Property

        Friend Overrides ReadOnly Property SupportsPrivateImplClass As Boolean
            Get
                ' Disable <PrivateImplementationDetails> in ENC since the
                ' CLR does Not support adding non-private members.
                Return False
            End Get
        End Property

        Friend Overloads Function GetAnonymousTypeMap() As IReadOnlyDictionary(Of AnonymousTypeKey, AnonymousTypeValue) Implements IPEDeltaAssemblyBuilder.GetAnonymousTypeMap
            Dim anonymousTypes = Compilation.AnonymousTypeManager.GetAnonymousTypeMap()
            ' Should contain all entries in previous generation.
            Debug.Assert(m_PreviousGeneration.AnonymousTypeMap.All(Function(p) anonymousTypes.ContainsKey(p.Key)))
            Return anonymousTypes
        End Function

        Friend Overrides Function CreateLocalSlotManager(method As MethodSymbol) As LocalSlotManager
            Dim previousLocals As ImmutableArray(Of EncLocalInfo) = Nothing
            Dim getPreviousLocalSlot As GetPreviousLocalSlot = Nothing
            If Not m_PreviousDefinitions.TryGetPreviousLocals(m_PreviousGeneration, method, previousLocals, getPreviousLocalSlot) Then
                previousLocals = ImmutableArray(Of EncLocalInfo).Empty
            End If
            Debug.Assert(getPreviousLocalSlot IsNot Nothing)
            Return New EncLocalSlotManager(previousLocals, getPreviousLocalSlot)
        End Function

        Friend Overrides Function GetPreviousAnonymousTypes() As ImmutableArray(Of AnonymousTypeKey)
            Return ImmutableArray.CreateRange(m_PreviousGeneration.AnonymousTypeMap.Keys)
        End Function

        Friend Overrides Function GetNextAnonymousTypeIndex(fromDelegates As Boolean) As Integer
            Return m_PreviousGeneration.GetNextAnonymousTypeIndex(fromDelegates)
        End Function

        Friend Overrides Function TryGetAnonymousTypeName(template As NamedTypeSymbol, <Out()> ByRef name As String, <Out()> ByRef index As Integer) As Boolean
            Debug.Assert(Compilation Is template.DeclaringCompilation)
            Return m_PreviousDefinitions.TryGetAnonymousTypeName(template, name, index)
        End Function

        Friend ReadOnly Property Changes As SymbolChanges
            Get
                Return m_Changes
            End Get
        End Property

        Friend Overrides Function GetTopLevelTypesCore(context As Context) As IEnumerable(Of Cci.INamespaceTypeDefinition)
            Return m_Changes.GetTopLevelTypes(context)
        End Function

        Friend Sub OnCreatedIndices(diagnostics As DiagnosticBag) Implements IPEDeltaAssemblyBuilder.OnCreatedIndices
            Dim embeddedTypesManager = Me.EmbeddedTypesManagerOpt
            If embeddedTypesManager IsNot Nothing Then
                For Each embeddedType In embeddedTypesManager.EmbeddedTypesMap.Keys
                    diagnostics.Add(ErrorFactory.ErrorInfo(ERRID.ERR_EnCNoPIAReference, embeddedType), Location.None)
                Next
            End If
        End Sub

        Private Shared Function GenerateMethodMap(edits As IEnumerable(Of SemanticEdit)) As IReadOnlyDictionary(Of MethodSymbol, MethodDefinitionEntry)
            Dim methodMap = New Dictionary(Of MethodSymbol, MethodDefinitionEntry)
            For Each edit In edits
                If edit.Kind = CodeAnalysis.Emit.SemanticEditKind.Update Then
                    Dim method = TryCast(edit.NewSymbol, MethodSymbol)
                    If method IsNot Nothing Then
                        methodMap.Add(method, New MethodDefinitionEntry(
                                      DirectCast(edit.OldSymbol, MethodSymbol),
                                      edit.PreserveLocalVariables,
                                      edit.SyntaxMap))
                    End If
                End If
            Next
            Return methodMap
        End Function
    End Class

End Namespace