﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

'-----------------------------------------------------------------------------------------------------------
'Reads the tree from an XML file into a ParseTree object and sub-objects. Reports many kinds of errors
'during the reading process.
'-----------------------------------------------------------------------------------------------------------

Imports System.IO
Imports System.Reflection
Imports System.Xml
Imports System.Xml.Schema
Imports <xmlns="http://schemas.microsoft.com/VisualStudio/Roslyn/Compiler">

Public Module ReadTree
    Dim currentFile As String

    ' Read an XML file and return the resulting ParseTree object.
    Public Function ReadTheTree(fileName As String, Optional ByRef xDoc As XDocument = Nothing) As ParseTree

        Console.WriteLine("Reading input file ""{0}""...", fileName)

        xDoc = GetXDocument(fileName)

        Dim tree As New ParseTree

        Dim x = xDoc.<define-parse-tree>

        tree.FileName = fileName
        tree.NamespaceName = xDoc.<define-parse-tree>.@namespace
        tree.VisitorName = xDoc.<define-parse-tree>.@visitor
        tree.RewriteVisitorName = xDoc.<define-parse-tree>.@<rewrite-visitor>
        tree.FactoryClassName = xDoc.<define-parse-tree>.@<factory-class>
        tree.ContextualFactoryClassName = xDoc.<define-parse-tree>.@<contextual-factory-class>

        Dim defs = xDoc.<define-parse-tree>.<definitions>

        For Each struct In defs.<node-structure>
            If tree.NodeStructures.ContainsKey(struct.@name) Then
                tree.ReportError(struct, "node-structure with name ""{0}"" already defined", struct.@name)
            Else
                tree.NodeStructures.Add(struct.@name, New ParseNodeStructure(struct, tree))
            End If
        Next

        For Each al In defs.<node-kind-alias>
            If tree.Aliases.ContainsKey(al.@name) Then
                tree.ReportError(al, "node-kind-alias with name ""{0}"" already defined", al.@name)
            Else
                tree.Aliases.Add(al.@name, New ParseNodeKindAlias(al, tree))
            End If
        Next

        For Each en In defs.<enumeration>
            If tree.Enumerations.ContainsKey(en.@name) Then
                tree.ReportError(en, "enumeration with name ""{0}"" already defined", en.@name)
            Else
                tree.Enumerations.Add(en.@name, New ParseEnumeration(en, tree))
            End If
        Next

        tree.FinishedReading()
        Return tree
    End Function

    ' Open the input XML file as an XDocument, using the reading options that we want.
    ' We use a schema to validate the input.
    Private Function GetXDocument(fileName As String) As XDocument
        currentFile = fileName

        Using schemaReader = XmlReader.Create(Assembly.GetExecutingAssembly().GetManifestResourceStream("VBSyntaxModelSchema.xsd"))

            Dim readerSettings As New XmlReaderSettings()
            readerSettings.Schemas.Add(Nothing, schemaReader)
            readerSettings.ValidationType = ValidationType.Schema
            AddHandler readerSettings.ValidationEventHandler, AddressOf ValidationError

            Dim fileStream As New FileStream(fileName, FileMode.Open, FileAccess.Read)
            Using reader = XmlReader.Create(fileStream, readerSettings)
                Return XDocument.Load(reader, LoadOptions.SetLineInfo Or LoadOptions.PreserveWhitespace)
            End Using
        End Using
    End Function

    ' A validation error occurred while reading the document. Tell the user.
    Private Sub ValidationError(sender As Object, e As ValidationEventArgs)
        Console.WriteLine("{0}({1},{2}): Invalid input: {3}", currentFile, e.Exception.LineNumber, e.Exception.LinePosition, e.Exception.Message)
    End Sub

End Module
