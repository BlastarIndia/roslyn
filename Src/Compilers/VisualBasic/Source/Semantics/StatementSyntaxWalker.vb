﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Namespace Microsoft.CodeAnalysis.VisualBasic
    ''' <summary>
    ''' This class walks all the statements in some syntax, in order, except those statements that are contained
    ''' inside expressions (a statement can occur inside an expression if it is inside
    ''' a lambda.)
    ''' 
    ''' This is used when collecting the declarations and declaration spaces of a method body.
    ''' 
    ''' Typically the client overrides this class and overrides various Visit methods, being sure to always
    ''' delegate back to the base.
    ''' </summary>
    Friend Class StatementSyntaxWalker
        Inherits VisualBasicSyntaxVisitor

        Public Overridable Sub VisitList(list As IEnumerable(Of VisualBasicSyntaxNode))
            For Each n In list
                Visit(n)
            Next
        End Sub

        Public Overrides Sub VisitCompilationUnit(node As CompilationUnitSyntax)
            VisitList(node.Options)
            VisitList(node.Imports)
            VisitList(node.Attributes)
            VisitList(node.Members)
        End Sub

        Public Overrides Sub VisitNamespaceBlock(node As NamespaceBlockSyntax)
            Visit(node.NamespaceStatement)
            VisitList(node.Members)
            Visit(node.EndNamespaceStatement)
        End Sub

        Public Overrides Sub VisitModuleBlock(ByVal node As ModuleBlockSyntax)
            Visit(node.Begin)
            VisitList(node.Members)
            Visit(node.End)
        End Sub

        Public Overrides Sub VisitClassBlock(ByVal node As ClassBlockSyntax)
            Visit(node.Begin)
            VisitList(node.Inherits)
            VisitList(node.Implements)
            VisitList(node.Members)
            Visit(node.End)
        End Sub

        Public Overrides Sub VisitStructureBlock(ByVal node As StructureBlockSyntax)
            Visit(node.Begin)
            VisitList(node.Inherits)
            VisitList(node.Implements)
            VisitList(node.Members)
            Visit(node.End)
        End Sub

        Public Overrides Sub VisitInterfaceBlock(ByVal node As InterfaceBlockSyntax)
            Visit(node.Begin)
            VisitList(node.Inherits)
            VisitList(node.Members)
            Visit(node.End)
        End Sub

        Public Overrides Sub VisitEnumBlock(ByVal node As EnumBlockSyntax)
            Visit(node.EnumStatement)
            VisitList(node.Members)
            Visit(node.EndEnumStatement)
        End Sub

        Public Overrides Sub VisitMethodBlock(ByVal node As MethodBlockSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
            Visit(node.End)
        End Sub

        Public Overrides Sub VisitConstructorBlock(node As ConstructorBlockSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
            Visit(node.End)
        End Sub

        Public Overrides Sub VisitOperatorBlock(node As OperatorBlockSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
            Visit(node.End)
        End Sub

        Public Overrides Sub VisitAccessorBlock(node As AccessorBlockSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
            Visit(node.End)
        End Sub

        Public Overrides Sub VisitPropertyBlock(ByVal node As PropertyBlockSyntax)
            Visit(node.PropertyStatement)
            VisitList(node.Accessors)
            Visit(node.EndPropertyStatement)
        End Sub

        Public Overrides Sub VisitEventBlock(ByVal node As EventBlockSyntax)
            Visit(node.EventStatement)
            VisitList(node.Accessors)
            Visit(node.EndEventStatement)
        End Sub

        Public Overrides Sub VisitWhileBlock(ByVal node As WhileBlockSyntax)
            Visit(node.WhileStatement)
            VisitList(node.Statements)
            Visit(node.EndWhileStatement)
        End Sub

        Public Overrides Sub VisitUsingBlock(ByVal node As UsingBlockSyntax)
            Visit(node.UsingStatement)
            VisitList(node.Statements)
            Visit(node.EndUsingStatement)
        End Sub

        Public Overrides Sub VisitSyncLockBlock(ByVal node As SyncLockBlockSyntax)
            Visit(node.SyncLockStatement)
            VisitList(node.Statements)
            Visit(node.EndSyncLockStatement)
        End Sub

        Public Overrides Sub VisitWithBlock(ByVal node As WithBlockSyntax)
            Visit(node.WithStatement)
            VisitList(node.Statements)
            Visit(node.EndWithStatement)
        End Sub

        Public Overrides Sub VisitSingleLineIfStatement(ByVal node As SingleLineIfStatementSyntax)
            Visit(node.IfPart)
            Visit(node.ElsePart)
        End Sub


        Public Overrides Sub VisitSingleLineIfPart(ByVal node As SingleLineIfPartSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
        End Sub

        Public Overrides Sub VisitSingleLineElsePart(ByVal node As SingleLineElsePartSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
        End Sub

        Public Overrides Sub VisitMultiLineIfBlock(ByVal node As MultiLineIfBlockSyntax)
            Visit(node.IfPart)
            VisitList(node.ElseIfParts)
            Visit(node.ElsePart)
            Visit(node.End)
        End Sub

        Public Overrides Sub VisitIfPart(ByVal node As IfPartSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
        End Sub

        Public Overrides Sub VisitElsePart(ByVal node As ElsePartSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
        End Sub

        Public Overrides Sub VisitTryBlock(ByVal node As TryBlockSyntax)
            Visit(node.TryPart)
            VisitList(node.CatchParts)
            Visit(node.FinallyPart)
            Visit(node.End)
        End Sub

        Public Overrides Sub VisitTryPart(ByVal node As TryPartSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
        End Sub

        Public Overrides Sub VisitCatchPart(ByVal node As CatchPartSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
        End Sub

        Public Overrides Sub VisitFinallyPart(ByVal node As FinallyPartSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
        End Sub

        Public Overrides Sub VisitSelectBlock(ByVal node As SelectBlockSyntax)
            Visit(node.SelectStatement)
            VisitList(node.CaseBlocks)
            Visit(node.EndSelectStatement)
        End Sub

        Public Overrides Sub VisitCaseBlock(ByVal node As CaseBlockSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
        End Sub

        Public Overrides Sub VisitDoLoopBlock(ByVal node As DoLoopBlockSyntax)
            Visit(node.DoStatement)
            VisitList(node.Statements)
            Visit(node.LoopStatement)
        End Sub

        Public Overrides Sub VisitForBlock(ByVal node As ForBlockSyntax)
            Visit(node.Begin)
            VisitList(node.Statements)
        End Sub
    End Class
End Namespace