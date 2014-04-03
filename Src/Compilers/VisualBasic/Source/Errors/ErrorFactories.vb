﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Globalization
Imports System.Runtime.Serialization
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    Friend Class ErrorFactory
        Public Shared ReadOnly EmptyErrorInfo As DiagnosticInfo = ErrorInfo(0)

        Public Shared ReadOnly VoidDiagnosticInfo As DiagnosticInfo = ErrorInfo(ERRID.Void)

        Public Shared ReadOnly GetErrorInfo_ERR_WithEventsRequiresClass As Func(Of DiagnosticInfo) =
            Function() ErrorFactory.ErrorInfo(ERRID.ERR_WithEventsRequiresClass)

        Public Shared ReadOnly GetErrorInfo_ERR_StrictDisallowImplicitObject As Func(Of DiagnosticInfo) =
            Function() ErrorFactory.ErrorInfo(ERRID.ERR_StrictDisallowImplicitObject)

        Public Shared ReadOnly GetErrorInfo_WRN_ObjectAssumedVar1_WRN_StaticLocalNoInference As Func(Of DiagnosticInfo) =
            Function() ErrorFactory.ErrorInfo(ERRID.WRN_ObjectAssumedVar1, ErrorFactory.ErrorInfo(ERRID.WRN_StaticLocalNoInference))

        Public Shared ReadOnly GetErrorInfo_WRN_ObjectAssumedVar1_WRN_MissingAsClauseinVarDecl As Func(Of DiagnosticInfo) =
            Function() ErrorFactory.ErrorInfo(ERRID.WRN_ObjectAssumedVar1, ErrorFactory.ErrorInfo(ERRID.WRN_MissingAsClauseinVarDecl))

        Public Shared ReadOnly GetErrorInfo_ERR_StrictDisallowsImplicitProc As Func(Of DiagnosticInfo) =
            Function() ErrorFactory.ErrorInfo(ERRID.ERR_StrictDisallowsImplicitProc)

        Public Shared ReadOnly GetErrorInfo_ERR_StrictDisallowsImplicitArgs As Func(Of DiagnosticInfo) =
            Function() ErrorFactory.ErrorInfo(ERRID.ERR_StrictDisallowsImplicitArgs)

        Public Shared ReadOnly GetErrorInfo_WRN_ObjectAssumed1_WRN_MissingAsClauseinFunction As Func(Of DiagnosticInfo) =
            Function() ErrorFactory.ErrorInfo(ERRID.WRN_ObjectAssumed1, ErrorFactory.ErrorInfo(ERRID.WRN_MissingAsClauseinFunction))

        Public Shared ReadOnly GetErrorInfo_WRN_ObjectAssumed1_WRN_MissingAsClauseinOperator As Func(Of DiagnosticInfo) =
            Function() ErrorFactory.ErrorInfo(ERRID.WRN_ObjectAssumed1, ErrorFactory.ErrorInfo(ERRID.WRN_MissingAsClauseinOperator))

        Public Shared ReadOnly GetErrorInfo_WRN_ObjectAssumedProperty1_WRN_MissingAsClauseinProperty As Func(Of DiagnosticInfo) =
            Function() ErrorFactory.ErrorInfo(ERRID.WRN_ObjectAssumedProperty1, ErrorFactory.ErrorInfo(ERRID.WRN_MissingAsClauseinProperty))

        Public Shared Function ErrorInfo(id As ERRID, ParamArray arguments As Object()) As DiagnosticInfo
            Return New DiagnosticInfo(MessageProvider.Instance, id, arguments)
        End Function

        Public Shared Function ErrorInfo(id As ERRID, ByRef syntaxToken As SyntaxToken) As DiagnosticInfo
            Return ErrorInfo(id, SyntaxFacts.GetText(syntaxToken.VisualBasicKind))
        End Function

        Public Shared Function ErrorInfo(id As ERRID, ByRef syntaxTokenKind As SyntaxKind) As DiagnosticInfo
            Return ErrorInfo(id, SyntaxFacts.GetText(syntaxTokenKind))
        End Function

        Public Shared Function ErrorInfo(id As ERRID, ByRef syntaxToken As SyntaxToken, type As TypeSymbol) As DiagnosticInfo
            Return ErrorInfo(id, SyntaxFacts.GetText(syntaxToken.VisualBasicKind), type)
        End Function

        Public Shared Function ErrorInfo(id As ERRID, ByRef syntaxToken As SyntaxToken, type1 As TypeSymbol, type2 As TypeSymbol) As DiagnosticInfo
            Return ErrorInfo(id, SyntaxFacts.GetText(syntaxToken.VisualBasicKind), type1, type2)
        End Function

        Private Shared s_resourceManager As System.Resources.ResourceManager
        Friend Shared ReadOnly Property ResourceManager As System.Resources.ResourceManager
            Get
                If s_resourceManager Is Nothing Then
                    s_resourceManager = New System.Resources.ResourceManager("VBResources", GetType(ERRID).Assembly)
                End If

                Return s_resourceManager
            End Get
        End Property

        'The function is a gigantic num->string switch, so verifying it is not interesting, but expensive.
        Friend Shared Function IdToString(id As ERRID) As String
            Return IdToString(id, CultureInfo.CurrentUICulture)
        End Function

        'The function is a gigantic num->string switch, so verifying it is not interesting, but expensive.
        Public Shared Function IdToString(id As ERRID, language As CultureInfo) As String

            Return ResourceManager.GetString(id.ToString(), language)
        End Function

    End Class


    ''' <summary>
    ''' Concatenates messages for a set of DiagnosticInfo.
    ''' </summary>
    <Serializable()>
    Friend Class CompoundDiagnosticInfo
        Inherits DiagnosticInfo

        Friend Sub New(arguments As DiagnosticInfo())
            MyBase.New(VisualBasic.MessageProvider.Instance, 0, arguments)
        End Sub

        Private Sub New(info As SerializationInfo, context As StreamingContext)
            MyBase.New(info, context)
        End Sub

        Public Overrides Function GetMessage(Optional culture As CultureInfo = Nothing) As String
            If culture Is Nothing Then
                culture = CultureInfo.InvariantCulture
            End If

            Dim builder = PooledStringBuilder.GetInstance()

            If Arguments IsNot Nothing Then
                For Each info As DiagnosticInfo In Arguments
                    builder.Builder.Append(info.GetMessage(culture))
                Next
            End If

            Dim message = builder.Builder.ToString()
            builder.Free()

            Return message
        End Function
    End Class

End Namespace
