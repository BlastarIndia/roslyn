﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols

Namespace Microsoft.CodeAnalysis.VisualBasic
    Partial Friend NotInheritable Class DocumentationCommentIdVisitor
        Private NotInheritable Class PartVisitor
            Inherits VisualBasicSymbolVisitor(Of StringBuilder, Object)

            Public Shared ReadOnly Instance As New PartVisitor(inParameterOrReturnType:=False)

            Private Shared ReadOnly ParameterOrReturnTypeInstance As New PartVisitor(inParameterOrReturnType:=True)

            Private ReadOnly _inParameterOrReturnType As Boolean

            Private Sub New(inParameterOrReturnType As Boolean)
                _inParameterOrReturnType = inParameterOrReturnType
            End Sub

            Public Overrides Function VisitArrayType(symbol As ArrayTypeSymbol, builder As StringBuilder) As Object
                Visit(symbol.ElementType, builder)

                ' Rank-one arrays are displayed different than rectangular arrays
                If symbol.Rank = 1 Then
                    builder.Append("[]")
                Else
                    builder.Append("[0:")
                    For i = 1 To symbol.Rank - 1
                        builder.Append(",0:")
                    Next
                    builder.Append("]"c)
                End If

                Return Nothing
            End Function

            Public Overrides Function VisitEvent(symbol As EventSymbol, builder As StringBuilder) As Object
                Visit(symbol.ContainingType, builder)
                builder.Append("."c)
                builder.Append(symbol.Name)

                Return Nothing
            End Function

            Public Overrides Function VisitField(symbol As FieldSymbol, builder As StringBuilder) As Object
                Visit(symbol.ContainingType, builder)
                builder.Append("."c)
                builder.Append(symbol.Name)

                Return Nothing
            End Function

            Private Sub VisitParameters(parameters As ImmutableArray(Of ParameterSymbol), builder As StringBuilder)
                builder.Append("("c)

                Dim needsComma As Boolean = False
                For Each parameter In parameters
                    If needsComma Then
                        builder.Append(","c)
                    End If
                    Visit(parameter, builder)
                    needsComma = True
                Next

                builder.Append(")"c)
            End Sub

            Public Overrides Function VisitMethod(symbol As MethodSymbol, builder As StringBuilder) As Object
                Visit(symbol.ContainingType, builder)
                builder.Append("."c)
                builder.Append(symbol.MetadataName.Replace("."c, "#"c))

                If symbol.Arity <> 0 Then
                    builder.Append("``")
                    builder.Append(symbol.Arity)
                End If

                If symbol.Parameters.Any() Then
                    ParameterOrReturnTypeInstance.VisitParameters(symbol.Parameters, builder)
                End If

                If symbol.MethodKind = MethodKind.Conversion Then
                    builder.Append("~"c)
                    ParameterOrReturnTypeInstance.Visit(symbol.ReturnType, builder)
                End If

                Return Nothing
            End Function

            Public Overrides Function VisitProperty(symbol As PropertySymbol, builder As StringBuilder) As Object
                Visit(symbol.ContainingType, builder)
                builder.Append("."c)
                builder.Append(symbol.MetadataName)

                If symbol.Parameters.Any() Then
                    ParameterOrReturnTypeInstance.VisitParameters(symbol.Parameters, builder)
                End If

                Return Nothing
            End Function

            Public Overrides Function VisitTypeParameter(symbol As TypeParameterSymbol, builder As StringBuilder) As Object
                If symbol.ContainingSymbol.Kind = SymbolKind.NamedType Then
                    builder.Append("`"c)
                ElseIf symbol.ContainingSymbol.Kind = SymbolKind.Method Then
                    builder.Append("``")
                Else
                    Throw ExceptionUtilities.UnexpectedValue(symbol.ContainingSymbol.Kind)
                End If

                builder.Append(symbol.Ordinal)

                Return Nothing
            End Function

            Public Overrides Function VisitNamedType(symbol As NamedTypeSymbol, builder As StringBuilder) As Object
                If symbol.ContainingSymbol IsNot Nothing AndAlso symbol.ContainingSymbol.Name.Length <> 0 Then
                    Visit(symbol.ContainingSymbol, builder)
                    builder.Append("."c)
                End If

                builder.Append(symbol.Name)

                If symbol.Arity <> 0 Then
                    ' Special case: dev11 treats types instances of the declaring type in the parameter list
                    ' (And return type, for conversions) as constructed with its own type parameters.
                    If Not _inParameterOrReturnType AndAlso symbol = symbol.ConstructedFrom Then
                        builder.Append(MetadataHelpers.GenericTypeNameManglingChar)
                        builder.Append(symbol.Arity)
                    Else
                        builder.Append("{"c)

                        Dim needsComma As Boolean = False
                        For Each typeArgument In symbol.TypeArgumentsNoUseSiteDiagnostics
                            If needsComma Then
                                builder.Append(","c)
                            End If
                            Visit(typeArgument, builder)
                            needsComma = True
                        Next

                        builder.Append("}"c)
                    End If
                End If

                Return Nothing
            End Function

            Public Overrides Function VisitNamespace(symbol As NamespaceSymbol, builder As StringBuilder) As Object
                If symbol.ContainingNamespace IsNot Nothing AndAlso symbol.ContainingNamespace.Name.Length <> 0 Then
                    Visit(symbol.ContainingNamespace, builder)
                    builder.Append("."c)
                End If

                builder.Append(symbol.Name)

                Return Nothing
            End Function

            Public Overrides Function VisitParameter(symbol As ParameterSymbol, builder As StringBuilder) As Object
                Debug.Assert(_inParameterOrReturnType)

                Visit(symbol.Type, builder)

                If symbol.IsByRef Then
                    builder.Append("@"c)
                End If

                Return Nothing
            End Function

            Public Overrides Function VisitErrorType(symbol As ErrorTypeSymbol, arg As StringBuilder) As Object
                Return VisitNamedType(symbol, arg)
            End Function
        End Class
    End Class
End Namespace