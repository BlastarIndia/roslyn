﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Test.Utilities

Public Class VBParser : Implements IParser
    Private m_Options As VisualBasicParseOptions

    Public Sub New(Optional options As VisualBasicParseOptions = Nothing)
        m_Options = options
    End Sub

    Public Function Parse(code As String) As SyntaxTree Implements IParser.Parse
        Dim tree = VisualBasicSyntaxTree.ParseText(code, "", m_Options)
        Return tree
    End Function
End Class

'TODO: We need this only temporarily until 893565 is fixed.
Public Class VBKindProvider : Implements ISyntaxNodeKindProvider
    Public Function Kind(node As Object) As String Implements ISyntaxNodeKindProvider.Kind
        Return node.GetType().GetProperty("Kind").GetValue(node, Nothing).ToString()
    End Function
End Class