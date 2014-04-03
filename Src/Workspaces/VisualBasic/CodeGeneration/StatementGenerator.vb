﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System
Imports Microsoft.CodeAnalysis.CodeGeneration
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeGeneration
    Partial Friend Class StatementGenerator
        Inherits AbstractVisualBasicCodeGenerator

        Friend Shared Function GenerateStatements(statements As IEnumerable(Of SyntaxNode)) As SyntaxList(Of StatementSyntax)
            Return SyntaxFactory.List(statements.OfType(Of StatementSyntax)())
        End Function

        Friend Shared Function GenerateStatements(method As IMethodSymbol) As SyntaxList(Of StatementSyntax)
            Return StatementGenerator.GenerateStatements(CodeGenerationMethodInfo.GetStatements(method))
        End Function
    End Class
End Namespace