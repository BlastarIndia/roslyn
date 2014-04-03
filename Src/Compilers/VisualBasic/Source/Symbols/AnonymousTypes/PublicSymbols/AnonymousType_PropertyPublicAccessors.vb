﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    Partial Friend NotInheritable Class AnonymousTypeManager

        Private MustInherit Class AnonymousTypePropertyAccessorPublicSymbol
            Inherits SynthesizedPropertyAccessorBase(Of PropertySymbol)

            Private ReadOnly m_returnType As TypeSymbol

            Public Sub New([property] As PropertySymbol, returnType As TypeSymbol)
                MyBase.New([property].ContainingType, [property])
                m_returnType = returnType
            End Sub

            Friend NotOverridable Overrides ReadOnly Property BackingFieldSymbol As FieldSymbol
                Get
                    Throw ExceptionUtilities.Unreachable
                End Get
            End Property

            Public Overrides ReadOnly Property ReturnType As TypeSymbol
                Get
                    Return m_returnType
                End Get
            End Property
        End Class

        Private NotInheritable Class AnonymousTypePropertyGetAccessorPublicSymbol
            Inherits AnonymousTypePropertyAccessorPublicSymbol

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

        Private NotInheritable Class AnonymousTypePropertySetAccessorPublicSymbol
            Inherits AnonymousTypePropertyAccessorPublicSymbol

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