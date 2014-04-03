﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.IO
Imports System.Threading
Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.Emit

Namespace Microsoft.CodeAnalysis.VisualBasic.Emit

    Module EmitHelpers

        Friend Function EmitDifference(
            compilation As VisualBasicCompilation,
            baseline As EmitBaseline,
            edits As IEnumerable(Of SemanticEdit),
            metadataStream As Stream,
            ilStream As Stream,
            pdbStream As Stream,
            updatedMethodTokens As ICollection(Of UInteger),
            testData As CompilationTestData,
            cancellationToken As CancellationToken) As EmitDifferenceResult

            Dim moduleVersionId As Guid
            Try
                moduleVersionId = baseline.OriginalMetadata.GetModuleVersionId()
            Catch ex As BadImageFormatException
                ' Return MakeEmitResult(success:=False, diagnostics:= ..., baseline:=Nothing)
                Throw
            End Try

            Dim pdbName = PathUtilities.ChangeExtension(compilation.SourceModule.Name, "pdb")

            Using pdbWriter = New Cci.PdbWriter(pdbName, New ComStreamWrapper(pdbStream))
                Dim diagnostics = DiagnosticBag.GetInstance()
                Dim runtimeMDVersion = compilation.GetRuntimeMetadataVersion()
                Dim serializationProperties = compilation.ConstructModuleSerializationProperties(runtimeMDVersion, moduleVersionId)
                Dim manifestResources = SpecializedCollections.EmptyEnumerable(Of ResourceDescription)()

                Dim moduleBeingBuilt = New PEDeltaAssemblyBuilder(
                    compilation.SourceAssembly,
                    outputName:=Nothing,
                    outputKind:=compilation.Options.OutputKind,
                    serializationProperties:=serializationProperties,
                    manifestResources:=manifestResources,
                    assemblySymbolMapper:=Nothing,
                    previousGeneration:=baseline,
                    edits:=edits)

                If testData IsNot Nothing Then
                    moduleBeingBuilt.SetMethodTestData(testData.Methods)
                    testData.Module = moduleBeingBuilt
                End If

                baseline = moduleBeingBuilt.PreviousGeneration

                Dim definitionMap = moduleBeingBuilt.PreviousDefinitions
                Dim changes = moduleBeingBuilt.Changes

                If compilation.Compile(
                    moduleBeingBuilt,
                    outputName:=Nothing,
                    manifestResources:=manifestResources,
                    win32Resources:=Nothing,
                    xmlDocStream:=Nothing,
                    cancellationToken:=cancellationToken,
                    metadataOnly:=False,
                    generateDebugInfo:=True,
                    diagnosticBag:=diagnostics,
                    filter:=AddressOf changes.HasChanged,
                    hasDeclarationErrors:=False) Then

                    Dim context = New Context(DirectCast(moduleBeingBuilt, Cci.IModule), Nothing, diagnostics)

                    ' Map the definitions from the previous compilation to the current compilation.
                    ' This must be done after compiling above since synthesized definitions
                    ' (generated when compiling method bodies) may be required.
                    baseline = MapToCompilation(compilation, moduleBeingBuilt)

                    Dim encId = Guid.NewGuid()
                    Dim writer = New DeltaPeWriter(
                        context,
                        compilation.MessageProvider,
                        pdbWriter,
                        baseline,
                        encId,
                        definitionMap,
                        changes,
                        cancellationToken)
                    writer.WriteMetadataAndIL(metadataStream, ilStream)
                    writer.GetMethodTokens(updatedMethodTokens)

                    Return New EmitDifferenceResult(
                        success:=True,
                        diagnostics:=diagnostics.ToReadOnlyAndFree(),
                        baseline:=writer.GetDelta(baseline, compilation, encId))
                End If

                Return New EmitDifferenceResult(success:=False, diagnostics:=diagnostics.ToReadOnlyAndFree(), baseline:=Nothing)
            End Using
        End Function

        Friend Function MapToCompilation(
            compilation As VisualBasicCompilation,
            moduleBeingBuilt As PEDeltaAssemblyBuilder) As EmitBaseline

            Dim previousGeneration = moduleBeingBuilt.PreviousGeneration
            Debug.Assert(previousGeneration.Compilation IsNot compilation)

            If previousGeneration.Ordinal = 0 Then
                ' Initial generation, nothing to map. (Since the initial generation
                ' is always loaded from metadata in the context of the current
                ' compilation, there's no separate mapping step.)
                Return previousGeneration
            End If

            Dim map = New SymbolMatcher(
                moduleBeingBuilt.GetAnonymousTypeMap(),
                (DirectCast(previousGeneration.Compilation, VisualBasicCompilation)).SourceAssembly,
                New Context(DirectCast(previousGeneration.PEModuleBuilder, PEModuleBuilder), Nothing, New DiagnosticBag()),
                compilation.SourceAssembly,
                New Context(DirectCast(moduleBeingBuilt, Cci.IModule), Nothing, New DiagnosticBag()))

            ' Map all definitions to this compilation.
            Dim typesAdded = MapDefinitions(map, previousGeneration.TypesAdded)
            Dim eventsAdded = MapDefinitions(map, previousGeneration.EventsAdded)
            Dim fieldsAdded = MapDefinitions(map, previousGeneration.FieldsAdded)
            Dim methodsAdded = MapDefinitions(map, previousGeneration.MethodsAdded)
            Dim propertiesAdded = MapDefinitions(map, previousGeneration.PropertiesAdded)

            ' Map anonymous types to this compilation.
            Dim anonymousTypeMap As New Dictionary(Of AnonymousTypeKey, AnonymousTypeValue)
            For Each pair In previousGeneration.AnonymousTypeMap
                Dim key = pair.Key
                Dim value = pair.Value
                Dim type = DirectCast(map.MapDefinition(value.Type), Cci.ITypeDefinition)
                Debug.Assert(type IsNot Nothing)
                anonymousTypeMap.Add(key, New AnonymousTypeValue(value.Name, value.UniqueIndex, type))
            Next

            ' Map locals (specifically, local types) to this compilation.
            Dim locals As New Dictionary(Of UInt32, ImmutableArray(Of EncLocalInfo))
            For Each pair In previousGeneration.LocalsForMethodsAddedOrChanged
                locals.Add(pair.Key, pair.Value.SelectAsArray(Function(l, m) MapLocalInfo(m, l), map))
            Next

            Return previousGeneration.With(
                compilation,
                moduleBeingBuilt,
                previousGeneration.Ordinal,
                previousGeneration.EncId,
                typesAdded,
                eventsAdded,
                fieldsAdded,
                methodsAdded,
                propertiesAdded,
                previousGeneration.EventMapAdded,
                previousGeneration.PropertyMapAdded,
                previousGeneration.TableEntriesAdded,
                blobStreamLengthAdded:=previousGeneration.BlobStreamLengthAdded,
                stringStreamLengthAdded:=previousGeneration.StringStreamLengthAdded,
                userStringStreamLengthAdded:=previousGeneration.UserStringStreamLengthAdded,
                guidStreamLengthAdded:=previousGeneration.GuidStreamLengthAdded,
                anonymousTypeMap:=anonymousTypeMap,
                localsForMethodsAddedOrChanged:=locals,
                localNames:=previousGeneration.LocalNames)
        End Function

        Private Function MapDefinitions(Of K As Cci.IDefinition, V)(
            map As SymbolMatcher,
            items As IReadOnlyDictionary(Of K, V)) As IReadOnlyDictionary(Of K, V)

            Dim result As New Dictionary(Of K, V)
            For Each pair In items
                Dim key = DirectCast(map.MapDefinition(pair.Key), K)
                ' Result may be null if the definition was deleted, or if the definition
                ' was synthesized (e.g.: an iterator type) and the method that generated
                ' the synthesized definition was unchanged and not recompiled.
                If key IsNot Nothing Then
                    result.Add(key, pair.Value)
                End If
            Next
            Return result
        End Function

        Private Function MapLocalInfo(
            map As SymbolMatcher,
            localInfo As EncLocalInfo) As EncLocalInfo

            Debug.Assert(Not localInfo.IsDefault)
            Dim type = map.MapReference(localInfo.Type)
            Debug.Assert(type IsNot Nothing)
            Return New EncLocalInfo(localInfo.Offset, type, localInfo.Constraints, localInfo.TempKind)
        End Function

    End Module

End Namespace