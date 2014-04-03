﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Utilities
Imports TypeKind = Microsoft.CodeAnalysis.TypeKind

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Represents enum constant field in source.
    ''' </summary>
    Friend MustInherit Class SourceEnumConstantSymbol
        Inherits SourceFieldSymbol

        Private _constantTuple As EvaluatedConstant

        Public Shared Function CreateExplicitValuedConstant(containingEnum As SourceNamedTypeSymbol, bodyBinder As Binder, syntax As EnumMemberDeclarationSyntax, diagnostics As DiagnosticBag) As SourceEnumConstantSymbol
            Dim initializer = syntax.Initializer
            Debug.Assert(initializer IsNot Nothing)
            Return New ExplicitValuedEnumConstantSymbol(containingEnum, bodyBinder, syntax, initializer, diagnostics)
        End Function

        Public Shared Function CreateImplicitValuedConstant(containingEnum As SourceNamedTypeSymbol, bodyBinder As Binder, syntax As EnumMemberDeclarationSyntax, otherConstant As SourceEnumConstantSymbol, otherConstantOffset As Integer, diagnostics As DiagnosticBag) As SourceEnumConstantSymbol
            If otherConstant Is Nothing Then
                Debug.Assert(otherConstantOffset = 0)
                Return New ZeroValuedEnumConstantSymbol(containingEnum, bodyBinder, syntax, diagnostics)
            Else
                Debug.Assert(otherConstantOffset > 0)
                Return New ImplicitValuedEnumConstantSymbol(containingEnum, bodyBinder, syntax, otherConstant, CType(otherConstantOffset, UInteger), diagnostics)
            End If
        End Function

        Protected Sub New(containingEnum As SourceNamedTypeSymbol, bodyBinder As Binder, syntax As EnumMemberDeclarationSyntax, diagnostics As DiagnosticBag)
            MyBase.New(containingEnum,
                       bodyBinder.GetSyntaxReference(syntax),
                       name:=syntax.Identifier.ValueText,
                       memberFlags:=SourceMemberFlags.Const Or SourceMemberFlags.Shared Or SourceMemberFlags.AccessibilityPublic)

            If IdentifierComparison.Compare(Me.Name, WellKnownMemberNames.EnumBackingFieldName) = 0 Then
                diagnostics.Add(ERRID.ERR_ClashWithReservedEnumMember1, syntax.Identifier.GetLocation(), Me.Name)
            End If
        End Sub

        Friend NotOverridable Overrides ReadOnly Property DeclarationSyntax As VisualBasicSyntaxNode
            Get
                Return Syntax
            End Get
        End Property

        Friend NotOverridable Overrides ReadOnly Property GetAttributeDeclarations As OneOrMany(Of SyntaxList(Of AttributeListSyntax))
            Get
                Return OneOrMany.Create(DirectCast(Syntax, EnumMemberDeclarationSyntax).AttributeLists)
            End Get
        End Property

        Public NotOverridable Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return ImmutableArray.Create(DirectCast(Syntax, EnumMemberDeclarationSyntax).Identifier.GetLocation())
            End Get
        End Property

        Public NotOverridable Overrides ReadOnly Property Type As TypeSymbol
            Get
                Return ContainingType
            End Get
        End Property

        Friend Overrides Function GetConstantValue(inProgress As SymbolsInProgress(Of FieldSymbol)) As ConstantValue
            If _constantTuple Is Nothing Then
                Dim diagnostics = DiagnosticBag.GetInstance()
                Dim constantTuple = MakeConstantTuple(inProgress, diagnostics)
                Dim sourceModule = DirectCast(Me.ContainingModule, SourceModuleSymbol)

                sourceModule.AtomicStoreReferenceAndDiagnostics(_constantTuple, constantTuple, diagnostics, CompilationStage.Declare)
                diagnostics.Free()
            End If

            Return _constantTuple.Value
        End Function

        Friend Overrides ReadOnly Property MeParameter As ParameterSymbol
            Get
                Return Nothing
            End Get
        End Property

        Protected MustOverride Function MakeConstantTuple(inProgress As SymbolsInProgress(Of FieldSymbol), diagnostics As DiagnosticBag) As EvaluatedConstant

        ' There are implementations for:
        ' 1) enum constant that is zero
        ' 2) enum constant that has explicit value
        ' 3) enum constant that has a value incrementally dependent on another enum constant
#Region "Concrete constant symbol implementations"

        Private NotInheritable Class ZeroValuedEnumConstantSymbol
            Inherits SourceEnumConstantSymbol

            Public Sub New(containingEnum As SourceNamedTypeSymbol, bodyBinder As Binder, syntax As EnumMemberDeclarationSyntax, diagnostics As DiagnosticBag)
                MyBase.New(containingEnum, bodyBinder, syntax, diagnostics)
            End Sub

            Protected Overrides Function MakeConstantTuple(inProgress As SymbolsInProgress(Of FieldSymbol), diagnostics As DiagnosticBag) As EvaluatedConstant
                Dim underlyingType = ContainingType.EnumUnderlyingType
                Return New EvaluatedConstant(Microsoft.CodeAnalysis.ConstantValue.Default(underlyingType.SpecialType), underlyingType, Nothing)
            End Function
        End Class

        Private NotInheritable Class ExplicitValuedEnumConstantSymbol
            Inherits SourceEnumConstantSymbol

            Private ReadOnly equalsValueNodeRef As SyntaxReference

            Public Sub New(containingEnum As SourceNamedTypeSymbol, bodyBinder As Binder, syntax As EnumMemberDeclarationSyntax, initializer As EqualsValueSyntax, diagnostics As DiagnosticBag)
                MyBase.New(containingEnum, bodyBinder, syntax, diagnostics)
                Me.equalsValueNodeRef = bodyBinder.GetSyntaxReference(initializer)
            End Sub

            Protected Overrides Function MakeConstantTuple(inProgress As SymbolsInProgress(Of FieldSymbol), diagnostics As DiagnosticBag) As EvaluatedConstant
                Return ConstantValueUtils.EvaluateFieldConstant(Me, Me.equalsValueNodeRef, inProgress, diagnostics)
            End Function
        End Class

        Private NotInheritable Class ImplicitValuedEnumConstantSymbol
            Inherits SourceEnumConstantSymbol

            Private ReadOnly _otherConstant As SourceEnumConstantSymbol
            Private ReadOnly _otherConstantOffset As UInteger

            Public Sub New(containingEnum As SourceNamedTypeSymbol, bodyBinder As Binder, syntax As EnumMemberDeclarationSyntax, otherConstant As SourceEnumConstantSymbol, otherConstantOffset As UInteger, diagnostics As DiagnosticBag)
                MyBase.New(containingEnum, bodyBinder, syntax, diagnostics)
                Debug.Assert(otherConstant IsNot Nothing)
                Debug.Assert(otherConstantOffset > 0)
                Me._otherConstant = otherConstant
                Me._otherConstantOffset = otherConstantOffset
            End Sub

            Protected Overrides Function MakeConstantTuple(inProgress As SymbolsInProgress(Of FieldSymbol), diagnostics As DiagnosticBag) As EvaluatedConstant
                Return EvaluateImplicitEnumConstant(Me, inProgress, diagnostics)
            End Function

            Private Shared Function EvaluateImplicitEnumConstant(symbol As ImplicitValuedEnumConstantSymbol, inProgress As SymbolsInProgress(Of FieldSymbol), diagnostics As DiagnosticBag) As EvaluatedConstant
                Debug.Assert(inProgress IsNot Nothing)
                Dim value As ConstantValue = Microsoft.CodeAnalysis.ConstantValue.Bad
                Dim errorField = inProgress.GetStartOfCycleIfAny(symbol)

                If errorField IsNot Nothing Then
                    diagnostics.Add(ERRID.ERR_CircularEvaluation1, errorField.Locations(0), errorField)
                Else
                    Dim otherValue = symbol._otherConstant.GetConstantValue(inProgress.Add(symbol))
                    If Not otherValue.IsBad Then
                        Dim overflowKind = EnumConstantHelper.OffsetValue(otherValue, symbol._otherConstantOffset, value)
                        If overflowKind = EnumOverflowKind.OverflowReport Then
                            diagnostics.Add(ERRID.ERR_ExpressionOverflow1, symbol.Locations(0), symbol)
                        End If
                    End If
                End If

                ' Note: EvaluatedConstant.Diagnostics is not used.
                Return New EvaluatedConstant(value, symbol.Type, Nothing)
            End Function
        End Class

#End Region

    End Class
End Namespace


