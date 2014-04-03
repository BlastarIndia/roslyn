﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Generic
Imports System.Threading
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' Data for Binder.BindImportClause that exposes dictionaries of
    ''' the members and aliases that have been bound during the
    ''' execution of BindImportClause. It is the responsibility of derived
    ''' classes to update the dictionaries in AddMember and AddAlias.
    ''' </summary>
    Friend MustInherit Class ImportData
        Protected Sub New(members As HashSet(Of NamespaceOrTypeSymbol),
                          aliases As Dictionary(Of String, AliasAndImportsClausePosition),
                          xmlNamespaces As Dictionary(Of String, XmlNamespaceAndImportsClausePosition))
            Me.Members = members
            Me.Aliases = aliases
            Me.XmlNamespaces = xmlNamespaces
        End Sub

        Public ReadOnly Members As HashSet(Of NamespaceOrTypeSymbol)
        Public ReadOnly Aliases As Dictionary(Of String, AliasAndImportsClausePosition)
        Public ReadOnly XmlNamespaces As Dictionary(Of String, XmlNamespaceAndImportsClausePosition)

        Public MustOverride Sub AddMember(syntaxRef As SyntaxReference, member As NamespaceOrTypeSymbol, importsClausePosition As Integer)
        Public MustOverride Sub AddAlias(syntaxRef As SyntaxReference, name As String, [alias] As AliasSymbol, importsClausePosition As Integer)
    End Class

    Partial Friend Class Binder
        ' Bind one import clause, add it to the correct collection of imports.
        ' Warnings and errors are emitted to the diagnostic bag, including detecting duplicates.
        ' Note that this binder should have been already been set up to only bind to things in the global namespace.
        Public Sub BindImportClause(importClauseSyntax As ImportsClauseSyntax,
                                          data As ImportData,
                                          diagBag As DiagnosticBag)
            ImportsBinder.BindImportClause(importClauseSyntax, Me, data, diagBag)
        End Sub

        ''' <summary>
        ''' The Imports binder handles binding of Imports statements in files, and also the project-level imports.
        ''' </summary>
        Private Class ImportsBinder
            ' Bind one import clause, add it to the correct collection of imports.
            ' Warnings and errors are emitted to the diagnostic bag, including detecting duplicates.
            ' Note that the binder should have been already been set up to only bind to things in the global namespace.
            Public Shared Sub BindImportClause(importClauseSyntax As ImportsClauseSyntax,
                                                     binder As Binder,
                                                     data As ImportData,
                                                     diagBag As DiagnosticBag)
                Select Case importClauseSyntax.Kind
                    Case SyntaxKind.AliasImportsClause
                        BindAliasImportsClause(DirectCast(importClauseSyntax, AliasImportsClauseSyntax), binder, data, diagBag)

                    Case SyntaxKind.MembersImportsClause
                        BindMembersImportsClause(DirectCast(importClauseSyntax, MembersImportsClauseSyntax), binder, data, diagBag)

                    Case SyntaxKind.XmlNamespaceImportsClause
                        BindXmlNamespaceImportsClause(DirectCast(importClauseSyntax, XmlNamespaceImportsClauseSyntax), binder, data, diagBag)
                    Case Else
                End Select

            End Sub

            ' Bind an alias imports clause. If it is OK, and also unique, add it to the dictionary.
            Private Shared Sub BindAliasImportsClause(aliasImportSyntax As AliasImportsClauseSyntax,
                                                      binder As Binder,
                                                      data As ImportData,
                                                      diagBag As DiagnosticBag)
                Dim aliasesName = aliasImportSyntax.Name
                Dim aliasTarget As NamespaceOrTypeSymbol = binder.BindNamespaceOrTypeSyntax(aliasesName, diagBag)

                If aliasTarget.Kind <> SymbolKind.Namespace Then
                    Dim type = TryCast(aliasTarget, TypeSymbol)

                    If type Is Nothing OrElse type.IsDelegateType Then
                        binder.ReportDiagnostic(diagBag,
                                                aliasImportSyntax,
                                                ERRID.ERR_InvalidTypeForAliasesImport2,
                                                aliasTarget,
                                                aliasTarget.Name)
                    End If
                End If

                If aliasTarget.Kind <> SymbolKind.ErrorType Then
                    Dim aliasIdentifier = aliasImportSyntax.Alias
                    Dim aliasText = aliasIdentifier.ValueText
                    ' Parser checks for type characters on alias text, so don't need to check again here.

                    ' Check for duplicate symbol.
                    If data.Aliases.ContainsKey(aliasText) Then
                        binder.ReportDiagnostic(diagBag, aliasIdentifier, ERRID.ERR_DuplicateNamedImportAlias1, aliasText)
                    Else
                        ' Make sure that the Import's alias doesn't have the same name as a type or a namespace in the global namespace
                        Dim conflictsWith = binder.Compilation.GlobalNamespace.GetMembers(aliasText)

                        If Not conflictsWith.IsEmpty Then
                            ' TODO: Note that symbol's name in this error message is supposed to include Class/Namespace word at the beginning.
                            '       Might need to use special format for that parameter in the error message. 
                            binder.ReportDiagnostic(diagBag,
                                                    aliasImportSyntax,
                                                    ERRID.ERR_ImportAliasConflictsWithType2,
                                                    aliasText,
                                                    conflictsWith(0))
                        Else
                            ' NOTE: we are mimicing Dev10 behavior where referencing type symbol in 
                            '       alias 'declaration' fails in case the symbol has errors.
                            Dim useSiteErrorInfo As DiagnosticInfo = aliasTarget.GetUseSiteErrorInfo()
                            If Not AllowAliasWithUseSiteError(useSiteErrorInfo) Then
                                binder.ReportDiagnostic(diagBag, aliasImportSyntax, useSiteErrorInfo)
                                ' NOTE: Do not create an alias!!!

                            Else
                                Dim aliasSymbol = New AliasSymbol(binder.Compilation,
                                                                 binder.ContainingNamespaceOrType,
                                                                 aliasText,
                                                                 aliasTarget,
                                                                 aliasIdentifier.GetLocation())

                                data.AddAlias(binder.GetSyntaxReference(aliasImportSyntax), aliasText, aliasSymbol, aliasImportSyntax.SpanStart)
                            End If
                        End If
                    End If
                End If
            End Sub

            ''' <summary>
            ''' Checks use site error and returns True in case the alias should still be created for the
            ''' type with this site error. In current implementation checks for errors ##36924, 36925
            ''' </summary>
            Private Shared Function AllowAliasWithUseSiteError(useSiteErrorInfo As DiagnosticInfo) As Boolean
                Return useSiteErrorInfo Is Nothing OrElse
                        useSiteErrorInfo.Code = ERRID.ERR_CannotUseGenericTypeAcrossAssemblyBoundaries OrElse
                        useSiteErrorInfo.Code = ERRID.ERR_CannotUseGenericBaseTypeAcrossAssemblyBoundaries
            End Function

            ' Bind a members imports clause. If it is OK, and also unique, add it to the members imports set.
            Private Shared Sub BindMembersImportsClause(membersImportsSyntax As MembersImportsClauseSyntax,
                                                        binder As Binder,
                                                        data As ImportData,
                                                        diagBag As DiagnosticBag)
                Dim importsName = membersImportsSyntax.Name
                Dim importedSymbol As NamespaceOrTypeSymbol = binder.BindNamespaceOrTypeSyntax(importsName, diagBag)

                If importedSymbol.Kind <> SymbolKind.Namespace Then
                    Dim type = TryCast(importedSymbol, TypeSymbol)

                    ' Non-aliased interface imports are disallowed
                    If type Is Nothing OrElse type.IsDelegateType OrElse type.IsInterfaceType Then
                        Binder.ReportDiagnostic(diagBag,
                                                membersImportsSyntax,
                                                ERRID.ERR_NonNamespaceOrClassOnImport2,
                                                importedSymbol,
                                                importedSymbol.Name)
                    End If
                End If

                If importedSymbol.Kind <> SymbolKind.ErrorType Then
                    ' Check for duplicate symbol.
                    If data.Members.Contains(importedSymbol) Then
                        ' NOTE: Dev10 doesn't report this error for project level imports. We still 
                        '       generate the error but filter it out when bind project level imports
                        Binder.ReportDiagnostic(diagBag, importsName, ERRID.ERR_DuplicateImport1, importedSymbol)
                    Else
                        Dim importedSymbolIsValid As Boolean = True

                        ' Disallow importing different instantiations of the same generic type.
                        If importedSymbol.Kind = SymbolKind.NamedType Then
                            Dim namedType = DirectCast(importedSymbol, NamedTypeSymbol)

                            If namedType.IsGenericType Then
                                namedType = namedType.OriginalDefinition

                                For Each contender In data.Members
                                    If contender.OriginalDefinition Is namedType Then
                                        importedSymbolIsValid = False
                                        Binder.ReportDiagnostic(diagBag, importsName, ERRID.ERR_DuplicateRawGenericTypeImport1, namedType)
                                        Exit For
                                    End If
                                Next
                            End If
                        End If

                        If importedSymbolIsValid Then
                            data.AddMember(binder.GetSyntaxReference(importsName), importedSymbol, membersImportsSyntax.SpanStart)
                        End If
                    End If
                End If
            End Sub

            Private Shared Sub BindXmlNamespaceImportsClause(syntax As XmlNamespaceImportsClauseSyntax,
                                                             binder As Binder,
                                                             data As ImportData,
                                                             diagBag As DiagnosticBag)
                Dim prefix As String = Nothing
                Dim namespaceName As String = Nothing
                Dim [namespace] As BoundExpression = Nothing
                Dim hasErrors As Boolean = False
                If binder.TryGetXmlnsAttribute(syntax.XmlNamespace, prefix, namespaceName, [namespace], hasErrors, fromImport:=True, diagnostics:=diagBag) AndAlso
                    Not hasErrors Then
                    Debug.Assert([namespace] Is Nothing) ' Binding should have been skipped.

                    If data.XmlNamespaces.ContainsKey(prefix) Then
                        ' "XML namespace prefix '{0}' is already declared."
                        Binder.ReportDiagnostic(diagBag, syntax, ERRID.ERR_DuplicatePrefix, prefix)
                    Else
                        data.XmlNamespaces.Add(prefix, New XmlNamespaceAndImportsClausePosition(namespaceName, syntax.SpanStart))
                    End If
                End If
            End Sub
        End Class
    End Class
End Namespace