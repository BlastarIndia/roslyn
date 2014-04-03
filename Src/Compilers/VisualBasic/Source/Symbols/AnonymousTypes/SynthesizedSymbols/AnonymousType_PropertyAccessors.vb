﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    Partial Friend NotInheritable Class AnonymousTypeManager

        Private MustInherit Class AnonymousTypePropertyAccessorSymbol
            Inherits SynthesizedPropertyAccessorBase(Of PropertySymbol)

            Private ReadOnly m_returnType As TypeSymbol

            Public Sub New([property] As PropertySymbol, returnType As TypeSymbol)
                MyBase.New([property].ContainingType, [property])
                m_returnType = returnType
            End Sub

            Friend NotOverridable Overrides ReadOnly Property BackingFieldSymbol As FieldSymbol
                Get
                    Return DirectCast(Me.m_propertyOrEvent, AnonymousTypePropertySymbol).AssociatedField
                End Get
            End Property

            Friend Overrides Sub AddSynthesizedAttributes(ByRef attributes As ArrayBuilder(Of SynthesizedAttributeData))
                MyBase.AddSynthesizedAttributes(attributes)

                ' Dev11 adds DebuggerNonUserCode; there is no reason to do so since:
                ' - we emit no debug info for the body
                ' - the code doesn't call any user code that could inspect the stack and find the accessor's frame
                ' - the code doesn't throw exceptions whose stack frames we would need to hide
                ' 
                ' C# also doesn't add DebuggerHidden nor DebuggerNonUserCode attributes.
            End Sub

            Protected Overrides Function GenerateMetadataName() As String
                Return Binder.GetAccessorName(m_propertyOrEvent.MetadataName, Me.MethodKind, Me.IsCompilationOutputWinMdObj())
            End Function

            Public Overrides ReadOnly Property ReturnType As TypeSymbol
                Get
                    Return m_returnType
                End Get
            End Property
        End Class

        Private NotInheritable Class AnonymousTypePropertyGetAccessorSymbol
            Inherits AnonymousTypePropertyAccessorSymbol

            Public Sub New([property] As PropertySymbol)
                MyBase.New([property], [property].Type)
            End Sub

            Public Overrides ReadOnly Property IsSub As Boolean
                Get
                    Return False
                End Get
            End Property

            Public Overrides ReadOnly Property MethodKind As MethodKind
                Get
                    Return MethodKind.PropertyGet
                End Get
            End Property

        End Class

        Private NotInheritable Class AnonymousTypePropertySetAccessorSymbol
            Inherits AnonymousTypePropertyAccessorSymbol

            Private m_parameters As ImmutableArray(Of ParameterSymbol)

            Public Sub New([property] As PropertySymbol, voidTypeSymbol As TypeSymbol)
                MyBase.New([property], voidTypeSymbol)

                m_parameters = ImmutableArray.Create(Of ParameterSymbol)(
                    New SynthesizedParameterSymbol(Me, m_propertyOrEvent.Type, 0, False, StringConstants.ValueParameterName))
            End Sub

            Public Overrides ReadOnly Property IsSub As Boolean
                Get
                    Return True
                End Get
            End Property

            Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
                Get
                    Return m_parameters
                End Get
            End Property

            Public Overrides ReadOnly Property MethodKind As MethodKind
                Get
                    Return MethodKind.PropertySet
                End Get
            End Property

        End Class

    End Class

End Namespace