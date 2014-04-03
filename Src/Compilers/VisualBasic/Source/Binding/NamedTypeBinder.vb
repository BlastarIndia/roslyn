﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.CodeAnalysis.RuntimeMembers
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Utilities
Imports TypeKind = Microsoft.CodeAnalysis.TypeKind

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' A NamedTypeBinder provides the context for a type symbol; e.g., looking up names
    ''' inside the type.
    ''' </summary>
    Friend Class NamedTypeBinder
        Inherits Binder

        Private ReadOnly m_typeSymbol As NamedTypeSymbol

        Public Sub New(containingBinder As Binder, typeSymbol As NamedTypeSymbol)
            MyBase.New(containingBinder)
            Debug.Assert(typeSymbol IsNot Nothing)
            Debug.Assert(Not (TypeOf typeSymbol Is Symbols.Metadata.PE.PENamedTypeSymbol))
            Debug.Assert(typeSymbol.IsFromCompilation(Me.Compilation))
            m_typeSymbol = typeSymbol
        End Sub

        ''' <summary>
        ''' Some nodes have special binder's for their contents 
        ''' </summary>
        Public Overrides Function GetBinder(node As VisualBasicSyntaxNode) As Binder
            ' TODO (tomat): this is a temporary workaround, we need a special script class binder
            ' Return Me so that identifiers in top-level statements bind to the members of the script class.
            Return If(m_typeSymbol.IsScriptClass, Me, m_containingBinder.GetBinder(node))
        End Function

        Public Overrides Function GetBinder(stmtList As SyntaxList(Of StatementSyntax)) As Binder
            ' TODO (tomat): this is a temporary workaround, we need a special script class binder
            ' Return Me so that identifiers in top-level statements bind to the members of the script class.
            Return If(m_typeSymbol.IsScriptClass, Me, m_containingBinder.GetBinder(stmtList))
        End Function

        Public Overrides ReadOnly Property ContainingNamespaceOrType As NamespaceOrTypeSymbol
            Get
                Return m_typeSymbol
            End Get
        End Property

        ''' <summary>
        ''' Gets all symbols of the particular name as 
        ''' a) members of this type
        ''' b) members of base types 
        ''' c) type parameters in this type (but not outer or base types)
        ''' In that order.
        ''' 
        ''' Note, that section "11.4.4 Simple Name Expression" of VB Language spec 
        ''' implies that type parameters are examined first, and only then members 
        ''' of the type are examined. But this is inconsistent with Dev10 behavior.
        ''' 
        ''' Returns all members of that name, or empty list if none.
        ''' </summary>
        Protected Overrides Sub LookupInSingleBinder(lookupResult As LookupResult,
                                                      name As String,
                                                      arity As Integer,
                                                      options As LookupOptions,
                                                      originalBinder As Binder,
                                                      <[In], Out> ByRef useSiteDiagnostics As HashSet(Of DiagnosticInfo))
            Debug.Assert(lookupResult.IsClear)

            ' 1. look for members. This call automatically gets members of base types.
            originalBinder.LookupMember(lookupResult, m_typeSymbol, name, arity, options, useSiteDiagnostics)
            If lookupResult.StopFurtherLookup Then
                Return ' short cut result
            End If

            ' 2. Lookup type parameter.
            Dim typeParameterLookupResult = lookupResult.GetInstance()
            LookupTypeParameter(typeParameterLookupResult, name, arity, options, originalBinder, useSiteDiagnostics)
            lookupResult.MergePrioritized(typeParameterLookupResult)
            typeParameterLookupResult.Free()

        End Sub

        ''' <summary>
        ''' Collect extension methods with the given name that are in scope in this binder.
        ''' The passed in ArrayBuilder must be empty. Extension methods from the same containing type
        ''' must be grouped together. 
        ''' </summary>
        Protected Overrides Sub CollectProbableExtensionMethodsInSingleBinder(name As String,
                                                                      methods As ArrayBuilder(Of MethodSymbol),
                                                                      originalBinder As Binder)
            Debug.Assert(methods.Count = 0)
            m_typeSymbol.AppendProbableExtensionMethods(name, methods)
        End Sub

        Protected Overrides Sub AddExtensionMethodLookupSymbolsInfoInSingleBinder(nameSet As LookupSymbolsInfo,
                                                                                   options As LookupOptions,
                                                                                   originalBinder As Binder)
            m_typeSymbol.AddExtensionMethodLookupSymbolsInfo(nameSet, options, originalBinder)
        End Sub

        Protected Overrides Sub AddLookupSymbolsInfoInSingleBinder(nameSet As LookupSymbolsInfo,
                                                                    options As LookupOptions,
                                                                    originalBinder As Binder)
            ' 1. Add all type parameters.
            ' UNDONE: filter using options.
            If m_typeSymbol.Arity > 0 Then
                For Each tp In m_typeSymbol.TypeParameters
                    If originalBinder.CanAddLookupSymbolInfo(tp, options, Nothing) Then
                        nameSet.AddSymbol(tp, tp.Name, 0)
                    End If
                Next
            End If

            ' 2. Add member names on the type.
            originalBinder.AddMemberLookupSymbolsInfo(nameSet, m_typeSymbol, options)
        End Sub

        ' Look in all type parameters of the given name in the instance type.
        ' Since there are typically just one or two, using a dictionary/ILookup would be overkill.
        Private Sub LookupTypeParameter(lookupResult As LookupResult,
                                        name As String,
                                        arity As Integer,
                                        options As LookupOptions,
                                        originalBinder As Binder,
                                        <[In], Out> ByRef useSiteDiagnostics As HashSet(Of DiagnosticInfo))
            Debug.Assert(name IsNot Nothing)
            Debug.Assert(lookupResult.IsClear)

            If m_typeSymbol.Arity > 0 Then
                For Each tp In m_typeSymbol.TypeParameters
                    If IdentifierComparison.Equals(tp.Name, name) Then
                        lookupResult.SetFrom(originalBinder.CheckViability(tp, arity, options, Nothing, useSiteDiagnostics))
                    End If
                Next
            End If

            Return
        End Sub

        Public Overrides Function CheckAccessibility(sym As Symbol,
                                                     <[In], Out> ByRef useSiteDiagnostics As HashSet(Of DiagnosticInfo),
                                                     Optional accessThroughType As TypeSymbol = Nothing,
                                                     Optional basesBeingResolved As ConsList(Of Symbol) = Nothing) As AccessCheckResult
            Return AccessCheck.CheckSymbolAccessibility(sym, m_typeSymbol, accessThroughType, useSiteDiagnostics, basesBeingResolved)
        End Function

        Public Overrides ReadOnly Property ContainingType As NamedTypeSymbol
            Get
                Return m_typeSymbol
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingMember As Symbol
            Get
                Return m_typeSymbol
            End Get
        End Property

        Public Overrides ReadOnly Property IsInQuery As Boolean
            Get
                Return False
            End Get
        End Property
    End Class

End Namespace
