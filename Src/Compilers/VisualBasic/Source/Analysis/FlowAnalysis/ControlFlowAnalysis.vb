﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System
Imports System.Collections.Generic
Imports System.Threading
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' This class implements the region control flow analysis operations.  Region control flow analysis provides
    ''' information about statements which enter and leave a region. The analysis done lazily. When created, it performs
    ''' no analysis, but simply caches the arguments. Then, the first time one of the analysis results is used it
    ''' computes that one result and caches it. Each result is computed using a custom algorithm.
    ''' </summary>
    Friend Class VisualBasicControlFlowAnalysis
        Inherits ControlFlowAnalysis

        Private ReadOnly context As RegionAnalysisContext

        Private _entryPoints As IEnumerable(Of SyntaxNode)
        Private _exitPoints As IEnumerable(Of SyntaxNode)
        Private _regionStartPointIsReachable As Object
        Private _regionEndPointIsReachable As Object
        Private _returnStatements As IEnumerable(Of SyntaxNode)
        Private _succeeded As Boolean?

        Friend Sub New(_context As RegionAnalysisContext)
            Me.context = _context
        End Sub

        ''' <summary>
        ''' A collection of statements outside the region that jump into the region.
        ''' </summary>
        Public Overrides ReadOnly Property EntryPoints As IEnumerable(Of SyntaxNode)
            Get
                If _entryPoints Is Nothing Then
                    Me._succeeded = Not Me.context.Failed
                    Dim result = If(Me.context.Failed, Enumerable.Empty(Of SyntaxNode)(),
                                    EntryPointsWalker.Analyze(context.AnalysisInfo, context.RegionInfo, _succeeded))
                    Interlocked.CompareExchange(_entryPoints, result, Nothing)
                End If
                Return _entryPoints
            End Get
        End Property

        ''' <summary>
        ''' A collection of statements inside the region that jump to locations outside the region.
        ''' </summary>
        Public Overrides ReadOnly Property ExitPoints As IEnumerable(Of SyntaxNode)
            Get
                If _exitPoints Is Nothing Then
                    Dim result = If(Me.context.Failed, Enumerable.Empty(Of SyntaxNode)(),
                                    ExitPointsWalker.Analyze(context.AnalysisInfo, context.RegionInfo))
                    Interlocked.CompareExchange(_exitPoints, result, Nothing)
                End If
                Return _exitPoints
            End Get
        End Property

        ''' <summary>
        ''' Returns true if and only if the last statement in the region can complete normally or the region contains no
        ''' statements.
        ''' </summary>
        Public NotOverridable Overrides ReadOnly Property EndPointIsReachable As Boolean
            Get
                If _regionStartPointIsReachable Is Nothing Then
                    ComputeReachability()
                End If
                Return DirectCast(_regionEndPointIsReachable, Boolean)
            End Get
        End Property

        Public NotOverridable Overrides ReadOnly Property StartPointIsReachable As Boolean
            Get
                If _regionStartPointIsReachable Is Nothing Then
                    ComputeReachability()
                End If
                Return DirectCast(_regionStartPointIsReachable, Boolean)
            End Get
        End Property

        Private Sub ComputeReachability()
            Dim startPointIsReachable As Boolean = False
            Dim endPointIsReachable As Boolean = False

            If Me.context.Failed Then
                startPointIsReachable = True
                endPointIsReachable = True
            Else
                RegionReachableWalker.Analyze(context.AnalysisInfo, context.RegionInfo, startPointIsReachable, endPointIsReachable)
            End If

            Interlocked.CompareExchange(_regionStartPointIsReachable, startPointIsReachable, Nothing)
            Interlocked.CompareExchange(_regionEndPointIsReachable, endPointIsReachable, Nothing)
        End Sub

        ''' <summary>
        ''' A collection of return, exit sub, exit function, exit operator and exit property statements found within the region that return to the enclosing method.
        ''' </summary>
        Public Overrides ReadOnly Property ReturnStatements As IEnumerable(Of SyntaxNode)
            ' <Obsolete("The return statements in a region are now included in the result of ExitPoints.", False)>
            Get
                Return ExitPoints.Where(Function(s As SyntaxNode) As Boolean
                                            Return s.IsKind(SyntaxKind.ReturnStatement) Or
                                                s.IsKind(SyntaxKind.ExitSubStatement) Or
                                                s.IsKind(SyntaxKind.ExitFunctionStatement) Or
                                                s.IsKind(SyntaxKind.ExitOperatorStatement) Or
                                                s.IsKind(SyntaxKind.ExitPropertyStatement)
                                        End Function)
            End Get
        End Property

        Public NotOverridable Overrides ReadOnly Property Succeeded As Boolean
            Get
                If Me._succeeded Is Nothing Then
                    Dim discarded = EntryPoints
                End If

                Return Me._succeeded.Value
            End Get
        End Property

    End Class

End Namespace
