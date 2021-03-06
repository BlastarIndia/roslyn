﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Information decoded from early well-known custom attributes applied on a method.
    ''' </summary>
    Friend Class MethodEarlyWellKnownAttributeData
        Inherits CommonMethodEarlyWellKnownAttributeData

#Region "ExtensionAttribute"
        Private m_isExtensionMethod As Boolean = False
        Friend Property IsExtensionMethod As Boolean
            Get
                VerifySealed(expected:=True)
                Return Me.m_isExtensionMethod
            End Get
            Set(value As Boolean)
                VerifySealed(expected:=False)
                Me.m_isExtensionMethod = value
                SetDataStored()
            End Set
        End Property
#End Region

    End Class
End Namespace