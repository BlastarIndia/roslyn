﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Class DataFlowPass

        ''' <summary> 
        ''' AmbiguousLocalsPseudoSymbol is a pseudo-symbol used in flow analysis representing 
        ''' a symbol of the implicit receiver in case Dim statement defines more than one variable,
        ''' but uses the same object initializer for all of them, like: 
        '''     Dim a,b As New C() With { .X = .Y } 
        ''' </summary>
        Protected NotInheritable Class AmbiguousLocalsPseudoSymbol
            Inherits LocalSymbol

            Private Sub New(container As Symbol, type As TypeSymbol, locals As ImmutableArray(Of LocalSymbol))
                MyBase.New(container, LocalDeclarationKind.AmbiguousLocals, type)

                Debug.Assert(type IsNot Nothing)
                Me.Locals = locals
            End Sub

            Friend Shared Shadows Function Create(locals As ImmutableArray(Of LocalSymbol)) As LocalSymbol
                Debug.Assert(Not locals.IsDefault AndAlso locals.Length > 1)
                Dim firstLocal As LocalSymbol = locals(0)
                Return New AmbiguousLocalsPseudoSymbol(firstLocal.ContainingSymbol, firstLocal.Type, locals)
            End Function

            Public ReadOnly Locals As ImmutableArray(Of LocalSymbol)

            Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
                Get
                    Return ImmutableArray(Of Location).Empty
                End Get
            End Property

            Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
                Get
                    Return ImmutableArray(Of SyntaxReference).Empty
                End Get
            End Property

            Friend Overrides ReadOnly Property IdentifierToken As SyntaxToken
                Get
                    Return Nothing
                End Get
            End Property

            Friend Overrides ReadOnly Property IdentifierLocation As Location
                Get
                    Return NoLocation.Singleton
                End Get
            End Property

            Public Overrides ReadOnly Property Name As String
                Get
                    Return Nothing
                End Get
            End Property

            Friend Overrides ReadOnly Property IsByRef As Boolean
                Get
                    Return False
                End Get
            End Property

        End Class

    End Class

End Namespace
