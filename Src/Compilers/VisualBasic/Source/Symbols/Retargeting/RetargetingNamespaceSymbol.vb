﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System
Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.Threading
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols.Retargeting

    ''' <summary>
    ''' Represents a namespace of a RetargetingModuleSymbol. Essentially this is a wrapper around 
    ''' another NamespaceSymbol that is responsible for retargeting symbols from one assembly to another. 
    ''' It can retarget symbols for multiple assemblies at the same time.
    ''' </summary>
    Friend NotInheritable Class RetargetingNamespaceSymbol
        Inherits NamespaceSymbol

        ''' <summary>
        ''' Owning RetargetingModuleSymbol.
        ''' </summary>
        Private ReadOnly m_RetargetingModule As RetargetingModuleSymbol

        ''' <summary>
        ''' The underlying NamespaceSymbol, cannot be another RetargetingNamespaceSymbol.
        ''' </summary>
        Private ReadOnly m_UnderlyingNamespace As NamespaceSymbol

        Public Sub New(retargetingModule As RetargetingModuleSymbol, underlyingNamespace As NamespaceSymbol)
            Debug.Assert(retargetingModule IsNot Nothing)
            Debug.Assert(underlyingNamespace IsNot Nothing)

            If TypeOf underlyingNamespace Is RetargetingNamespaceSymbol Then
                Throw New ArgumentException()
            End If

            m_RetargetingModule = retargetingModule
            m_UnderlyingNamespace = underlyingNamespace
        End Sub

        Private ReadOnly Property RetargetingTranslator As RetargetingModuleSymbol.RetargetingSymbolTranslator
            Get
                Return m_RetargetingModule.RetargetingTranslator
            End Get
        End Property

        Public ReadOnly Property UnderlyingNamespace As NamespaceSymbol
            Get
                Return m_UnderlyingNamespace
            End Get
        End Property

        Friend Overrides ReadOnly Property Extent As NamespaceExtent
            Get
                Return New NamespaceExtent(m_RetargetingModule)
            End Get
        End Property

        Public Overrides Function GetMembers() As ImmutableArray(Of Symbol)
            Return RetargetMembers(m_UnderlyingNamespace.GetMembers())
        End Function

        Private Function RetargetMembers(underlyingMembers As ImmutableArray(Of Symbol)) As ImmutableArray(Of Symbol)
            Dim builder = ArrayBuilder(Of Symbol).GetInstance()
            For Each s In underlyingMembers
                ' Skip explicitly declared local types.
                If s.Kind = SymbolKind.NamedType AndAlso DirectCast(s, NamedTypeSymbol).IsExplicitDefinitionOfNoPiaLocalType Then
                    Continue For
                End If
                builder.Add(RetargetingTranslator.Retarget(s))
            Next
            Return builder.ToImmutableAndFree()
        End Function

        Friend Overrides Function GetMembersUnordered() As ImmutableArray(Of Symbol)
            Return RetargetMembers(m_UnderlyingNamespace.GetMembersUnordered())
        End Function

        Public Overrides Function GetMembers(name As String) As ImmutableArray(Of Symbol)
            Return RetargetMembers(m_UnderlyingNamespace.GetMembers(name))
        End Function

        Friend Overrides Function GetTypeMembersUnordered() As ImmutableArray(Of NamedTypeSymbol)
            Return RetargetTypeMembers(m_UnderlyingNamespace.GetTypeMembersUnordered())
        End Function

        Public Overrides Function GetTypeMembers() As ImmutableArray(Of NamedTypeSymbol)
            Return RetargetTypeMembers(m_UnderlyingNamespace.GetTypeMembers())
        End Function

        Private Function RetargetTypeMembers(underlyingMembers As ImmutableArray(Of NamedTypeSymbol)) As ImmutableArray(Of NamedTypeSymbol)
            Dim builder = ArrayBuilder(Of NamedTypeSymbol).GetInstance()
            For Each t In underlyingMembers
                ' Skip explicitly declared local types.
                If t.IsExplicitDefinitionOfNoPiaLocalType Then
                    Continue For
                End If
                Debug.Assert(t.PrimitiveTypeCode = Cci.PrimitiveTypeCode.NotPrimitive)
                builder.Add(RetargetingTranslator.Retarget(t, RetargetOptions.RetargetPrimitiveTypesByName))
            Next
            Return builder.ToImmutableAndFree()
        End Function

        Public Overrides Function GetTypeMembers(name As String) As ImmutableArray(Of NamedTypeSymbol)
            Return RetargetTypeMembers(m_UnderlyingNamespace.GetTypeMembers(name))
        End Function

        Public Overrides Function GetTypeMembers(name As String, arity As Integer) As ImmutableArray(Of NamedTypeSymbol)
            Return RetargetTypeMembers(m_UnderlyingNamespace.GetTypeMembers(name, arity))
        End Function

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return RetargetingTranslator.Retarget(m_UnderlyingNamespace.ContainingSymbol)
            End Get
        End Property

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return m_UnderlyingNamespace.Locations
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return m_UnderlyingNamespace.DeclaringSyntaxReferences
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingAssembly As AssemblySymbol
            Get
                Return m_RetargetingModule.ContainingAssembly
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingModule As ModuleSymbol
            Get
                Return m_RetargetingModule
            End Get
        End Property

        Public Overrides ReadOnly Property IsGlobalNamespace As Boolean
            Get
                Return m_UnderlyingNamespace.IsGlobalNamespace
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return m_UnderlyingNamespace.Name
            End Get
        End Property

        Public Overrides ReadOnly Property MetadataName As String
            Get
                Return m_UnderlyingNamespace.MetadataName
            End Get
        End Property

        Friend Overrides Function LookupMetadataType(ByRef fullEmittedName As MetadataTypeName) As NamedTypeSymbol
            ' This method is invoked when looking up a type by metadata type
            ' name through a RetargetingAssemblySymbol. For instance, in
            ' UnitTests.Symbols.Metadata.PE.NoPia.LocalTypeSubstitution2.

            Dim underlying As NamedTypeSymbol = m_UnderlyingNamespace.LookupMetadataType(fullEmittedName)

            Debug.Assert(underlying.ContainingModule Is m_RetargetingModule.UnderlyingModule)

            If Not underlying.IsErrorType() AndAlso underlying.IsExplicitDefinitionOfNoPiaLocalType Then
                ' Explicitly defined local types should be hidden.
                Return New MissingMetadataTypeSymbol.TopLevel(m_RetargetingModule, fullEmittedName)
            End If

            Return RetargetingTranslator.Retarget(underlying, RetargetOptions.RetargetPrimitiveTypesByName)
        End Function

        Public Overrides Function GetModuleMembers() As ImmutableArray(Of NamedTypeSymbol)
            Return RetargetingTranslator.Retarget(m_UnderlyingNamespace.GetModuleMembers())
        End Function

        Public Overrides Function GetModuleMembers(name As String) As ImmutableArray(Of NamedTypeSymbol)
            Return RetargetingTranslator.Retarget(m_UnderlyingNamespace.GetModuleMembers(name))
        End Function

        ''' <summary>
        ''' Calculate declared accessibility of most accessible type within this namespace or within a containing namespace recursively.
        ''' Expected to be called at most once per namespace symbol, unless there is a race condition.
        ''' 
        ''' Valid return values:
        '''     Friend,
        '''     Public,
        '''     NotApplicable - if there are no types.
        ''' </summary>
        <System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage()>
        Protected Overrides Function GetDeclaredAccessibilityOfMostAccessibleDescendantType() As Accessibility
            Debug.Assert(False, "Unexpected!") ' We should not be calling this method for Retargeting namespace.
            Return m_UnderlyingNamespace.DeclaredAccessibilityOfMostAccessibleDescendantType
        End Function

        Friend Overrides ReadOnly Property DeclaredAccessibilityOfMostAccessibleDescendantType As Accessibility
            Get
                Return m_UnderlyingNamespace.DeclaredAccessibilityOfMostAccessibleDescendantType
            End Get
        End Property

        ''' <summary>
        ''' This method is called directly by a Binder when it uses this module level namespace.
        ''' </summary>
        Friend Overrides Sub AppendProbableExtensionMethods(name As String, methods As ArrayBuilder(Of MethodSymbol))
            Dim oldCount As Integer = methods.Count

            ' Delegate work to the underlying namespace in order to take advantage of its
            ' map of extension methods.
            m_UnderlyingNamespace.AppendProbableExtensionMethods(name, methods)

            ' Retarget all method symbols appended by the underlying namespace.
            For i As Integer = oldCount To methods.Count - 1
                methods(i) = RetargetingTranslator.Retarget(methods(i))
            Next
        End Sub

        <System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage()>
        Friend Overrides ReadOnly Property TypesToCheckForExtensionMethods As ImmutableArray(Of NamedTypeSymbol)
            Get
                ' We should override all callers of this function and go through implementation
                ' provided by the underlying namespace symbol.
                Throw ExceptionUtilities.Unreachable
            End Get
        End Property

        ''' <summary>
        ''' This method is called when this namespace is part of a merged namespace and we are trying to build
        ''' a map of extension methods for the whole merged namespace.
        ''' </summary>
        Friend Overrides Sub BuildExtensionMethodsMap(map As Dictionary(Of String, ArrayBuilder(Of MethodSymbol)))
            ' Delegate work to the types of the underlying namespace.
            For Each underlyingContainedType As NamedTypeSymbol In m_UnderlyingNamespace.TypesToCheckForExtensionMethods
                underlyingContainedType.BuildExtensionMethodsMap(map, appendThrough:=Me)
            Next
        End Sub

        Friend Overrides Sub GetExtensionMethods(methods As ArrayBuilder(Of MethodSymbol), name As String)
            ' Delegate work to the types of the underlying namespace.
            For Each underlyingContainedType As NamedTypeSymbol In m_UnderlyingNamespace.TypesToCheckForExtensionMethods
                underlyingContainedType.GetExtensionMethods(methods, appendThrough:=Me, Name:=name)
            Next
        End Sub

        ''' <summary>
        ''' Make sure we retarget methods when types of the underlying namespace add them to the map.
        ''' </summary>
        Friend Overrides Sub BuildExtensionMethodsMapBucket(bucket As ArrayBuilder(Of MethodSymbol), method As MethodSymbol)
            bucket.Add(RetargetingTranslator.Retarget(method))
        End Sub

        ''' <summary>
        ''' This method is called directly by a Binder when it uses this module level namespace.
        ''' </summary>
        Friend Overrides Sub AddExtensionMethodLookupSymbolsInfo(nameSet As LookupSymbolsInfo,
                                                                  options As LookupOptions,
                                                                  originalBinder As Binder)
            ' Delegate work to the underlying namespace in order to take advantage of its
            ' map of extension methods.
            m_UnderlyingNamespace.AddExtensionMethodLookupSymbolsInfo(nameSet, options, originalBinder, appendThrough:=Me)
        End Sub

        ''' <summary>
        ''' Make sure we retarget methods when underlying namespace checks their viability.
        ''' </summary>
        Friend Overrides Function AddExtensionMethodLookupSymbolsInfoViabilityCheck(method As MethodSymbol, options As LookupOptions, originalBinder As Binder) As Boolean
            Return MyBase.AddExtensionMethodLookupSymbolsInfoViabilityCheck(RetargetingTranslator.Retarget(method), options, originalBinder)
        End Function

        <System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage()>
        Friend Overrides Sub AddExtensionMethodLookupSymbolsInfo(nameSet As LookupSymbolsInfo,
                                                                  options As LookupOptions,
                                                                  originalBinder As Binder,
                                                                  appendThrough As NamespaceSymbol)
            ' We should override all callers of this function and go through implementation
            ' provided by the underlying namespace symbol.
            Throw ExceptionUtilities.Unreachable
        End Sub

        ''' <remarks>
        ''' This is for perf, not for correctness.
        ''' </remarks>
        Friend Overrides ReadOnly Property DeclaringCompilation As VisualBasicCompilation
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides Function GetDocumentationCommentXml(Optional preferredCulture As CultureInfo = Nothing, Optional expandIncludes As Boolean = False, Optional cancellationToken As CancellationToken = Nothing) As String
            Return m_UnderlyingNamespace.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken)
        End Function
    End Class
End Namespace