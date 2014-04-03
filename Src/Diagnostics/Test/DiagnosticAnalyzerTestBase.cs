﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public abstract class DiagnosticAnalyzerTestBase
    {
        private static readonly MetadataReference CorlibReference = new MetadataFileReference(typeof(object).Assembly.Location, MetadataImageKind.Assembly);
        private static readonly MetadataReference CSharpSymbolsReference = new MetadataFileReference(typeof(CSharpCompilation).Assembly.Location, MetadataImageKind.Assembly);

        private static readonly MetadataReference[] DefaultMetadataReferences = new[]
        {
            CorlibReference,
            CSharpSymbolsReference,
            TestBase.SystemRef
        };

        internal static string DefaultFilePathPrefix = "Test";
        internal static string CSharpDefaultFileExt = "cs";
        internal static string VisualBasicDefaultExt = "vb";
        internal static string CSharpDefaultFilePath = DefaultFilePathPrefix + 0 + "." + CSharpDefaultFileExt;
        internal static string VisualBasicDefaultFilePath = DefaultFilePathPrefix + 0 + "." + VisualBasicDefaultExt;
        internal static string TestProjectName = "TestProject";

        protected abstract IDiagnosticAnalyzer GetCSharpDiagnosticAnalyzer();
        protected abstract IDiagnosticAnalyzer GetBasicDiagnosticAnalyzer();

        protected static DiagnosticResult GetGlobalResult(string name, string message)
        {
            return new DiagnosticResult
            {
                Id = name,
                Severity = DiagnosticSeverity.Warning,
                Message = message
            };
        }

        protected static DiagnosticResult GetBasicResultAt(int line, int column, string name, string message)
        {
            var location = new DiagnosticResultLocation(VisualBasicDefaultFilePath, line, column);

            return new DiagnosticResult
            {
                Locations = new[] { location },
                Id = name,
                Severity = DiagnosticSeverity.Warning,
                Message = message
            };
        }

        protected static DiagnosticResult GetBasicResultAt(string name, string message, params string[] locationStrings)
        {
            return new DiagnosticResult
            {
                Locations = ParseResultLocations(VisualBasicDefaultFilePath, locationStrings),
                Id = name,
                Severity = DiagnosticSeverity.Warning,
                Message = message
            };
        }

        protected static DiagnosticResult GetCSharpResultAt(int line, int column, string name, string message)
        {
            var location = new DiagnosticResultLocation(CSharpDefaultFilePath, line, column);

            return new DiagnosticResult
            {
                Locations = new[] { location },
                Id = name,
                Severity = DiagnosticSeverity.Warning,
                Message = message
            };
        }

        protected static DiagnosticResult GetCSharpResultAt(string name, string message, params string[] locationStrings)
        {
            return new DiagnosticResult
            {
                Locations = ParseResultLocations(CSharpDefaultFilePath, locationStrings),
                Id = name,
                Severity = DiagnosticSeverity.Warning,
                Message = message
            };
        }

        protected static DiagnosticResultLocation[] ParseResultLocations(string defaultPath, string[] locationStrings)
        {
            var builder = new List<DiagnosticResultLocation>();

            foreach (var str in locationStrings)
            {
                var tokens = str.Split('(', ',', ')');
                Assert.True(tokens.Length == 4, "Location string must be of the format 'FileName.cs(line,column)' or just 'line,column' to use " + defaultPath + " as the file name.");

                string path = tokens[0] == "" ? defaultPath : tokens[0];

                int line;
                Assert.True(int.TryParse(tokens[1], out line) && line >= -1, "Line must be >= -1 in location string: " + str);

                int column;
                Assert.True(int.TryParse(tokens[2], out column) && line >= -1, "Column must be >= -1 in location string: " + str);

                builder.Add(new DiagnosticResultLocation(path, line, column));
            }

            return builder.ToArray();
        }

        protected void VerifyCSharp(string source, params DiagnosticResult[] expected)
        {
            Verify(source, LanguageNames.CSharp, GetCSharpDiagnosticAnalyzer(), expected);
        }

        protected void VerifyBasic(string source, params DiagnosticResult[] expected)
        {
            Verify(source, LanguageNames.VisualBasic, GetBasicDiagnosticAnalyzer(), expected);
        }

        protected void Verify(string source, string language, IDiagnosticAnalyzer analyzer, params DiagnosticResult[] expected)
        {
            Verify(new[] { source }, language, analyzer, expected);
        }

        protected void VerifyBasic(string[] sources, params DiagnosticResult[] expected)
        {
            Verify(sources, LanguageNames.VisualBasic, GetBasicDiagnosticAnalyzer(), expected);
        }

        protected void VerifyCSharp(string[] sources, params DiagnosticResult[] expected)
        {
            Verify(sources, LanguageNames.CSharp, GetCSharpDiagnosticAnalyzer(), expected);
        }

        protected void Verify(string[] sources, string language, IDiagnosticAnalyzer analyzer, params DiagnosticResult[] expected)
        {
            GetSortedDiagnostics(sources, language, analyzer).Verify(expected);
        }

        protected static Diagnostic[] GetSortedDiagnostics(string[] sources, string language, IDiagnosticAnalyzer analyzer)
        {
            var documentsAndUseSpan = VerifyAndGetDocumentsAndSpan(sources, language);
            var documents = documentsAndUseSpan.Item1;
            var useSpans = documentsAndUseSpan.Item2;
            var spans = documentsAndUseSpan.Item3;
            return GetSortedDiagnostics(analyzer, documents, useSpans ? spans : null);
        }

        protected static Tuple<Document[], bool, TextSpan?[]> VerifyAndGetDocumentsAndSpan(string[] sources, string language)
        {
            Assert.True(language == LanguageNames.CSharp || language == LanguageNames.VisualBasic, "Unsupported language");

            var spans = new TextSpan?[sources.Length];
            bool useSpans = false;

            for (int i = 0; i < sources.Length; i++)
            {
                string fileName = language == LanguageNames.CSharp ? "Test" + i + ".cs" : "Test" + i + ".vb";

                string source;
                int? pos;
                TextSpan? span;
                MarkupTestFile.GetPositionAndSpan(sources[i], out source, out pos, out span);

                sources[i] = source;
                spans[i] = span;

                if (span != null)
                {
                    useSpans = true;
                }
            }

            var project = CreateProject(sources, language);
            var documents = project.Documents.ToArray();
            Assert.Equal(sources.Length, documents.Length);

            return Tuple.Create(documents, useSpans, spans);
        }

        protected static Document CreateDocument(string source, string language = LanguageNames.CSharp)
        {
            return CreateProject(new[] { source }, language).Documents.First();
        }

        protected static Project CreateProject(string[] sources, string language = LanguageNames.CSharp)
        {
            string fileNamePrefix = DefaultFilePathPrefix;
            string fileExt = language == LanguageNames.CSharp ? CSharpDefaultFileExt : VisualBasicDefaultExt;

            var solutionId = SolutionId.CreateNewId("TestSolution");
            var projectId = ProjectId.CreateNewId(debugName: TestProjectName);

            var solution = new CustomWorkspace(solutionId)
                .CurrentSolution
                .AddProject(projectId, TestProjectName, TestProjectName, language)
                .AddMetadataReference(projectId, CorlibReference)
                .AddMetadataReference(projectId, CSharpSymbolsReference)
                .AddMetadataReference(projectId, TestBase.SystemRef);

            int count = 0;
            foreach (var source in sources)
            {
                var newFileName = fileNamePrefix + count + "." + fileExt;
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddDocument(documentId, newFileName, SourceText.From(source));
                count++;
            }

            return solution.GetProject(projectId);
        }

        protected static Diagnostic[] GetSortedDiagnostics(IDiagnosticAnalyzer analyzer, Document document, TextSpan?[] spans = null)
        {
            return GetSortedDiagnostics(analyzer, new[] { document }, spans);
        }

        protected static Diagnostic[] GetSortedDiagnostics(IDiagnosticAnalyzer analyzer, Document[] documents, TextSpan?[] spans = null)
        {
            var projects = new HashSet<Project>();
            foreach (var document in documents)
            {
                projects.Add(document.Project);
            }

            var diagnostics = DiagnosticBag.GetInstance();
            foreach (var project in projects)
            {
                var compilation = project.GetCompilationAsync().Result;

                var compilationStartedAnalyzer = analyzer as ICompilationStartedAnalyzer;
                ICompilationEndedAnalyzer compilationEndedAnalyzer = null;
                if (compilationStartedAnalyzer != null)
                {
                    compilationEndedAnalyzer = compilationStartedAnalyzer.OnCompilationStarted(compilation, diagnostics.Add, default(CancellationToken));
                }

                for (int i = 0; i < documents.Length; i++)
                {
                    var document = documents[i];
                    var span = spans != null ? spans[i] : null;
                    AnalyzeDocument(analyzer, document, diagnostics.Add, span);
                    if (compilationEndedAnalyzer != null)
                    {
                        AnalyzeDocumentCore(compilationEndedAnalyzer, document, diagnostics.Add, span);
                    }
                }

                if (compilationEndedAnalyzer != null)
                {
                    compilationEndedAnalyzer.OnCompilationEnded(compilation, diagnostics.Add, default(CancellationToken));
                }
            }

            var results = GetSortedNonCompilerDiagnostics(diagnostics.AsEnumerable());
            diagnostics.Free();
            return results;
        }

        private static void AnalyzeDocument(IDiagnosticAnalyzer analyzer, Document document, Action<Diagnostic> addDiagnostic, TextSpan? span = null)
        {
            Assert.True(analyzer.GetType().IsDefined(typeof(ExportDiagnosticAnalyzerAttribute)), "Top-level analyzers should have the ExportDiagnosticAnalyzerAttribute");
            Assert.True(analyzer.GetType().IsDefined(typeof(DiagnosticAnalyzerAttribute)), "Top-level analyzers should have the DiagnosticAnalyzerAttribute");

            AnalyzeDocumentCore(analyzer, document, addDiagnostic, span);
        }

        protected static void AnalyzeDocumentCore(IDiagnosticAnalyzer analyzer, Document document, Action<Diagnostic> addDiagnostic, TextSpan? span = null, bool continueOnError = false)
        {
            TextSpan spanToTest = span.HasValue ? span.Value : document.GetSyntaxRootAsync().Result.FullSpan;
            var semanticModel = document.GetSemanticModelAsync().Result;
            AnalyzerDriver.RunAnalyzers(semanticModel, spanToTest, ImmutableArray.Create(analyzer), addDiagnostic, continueOnError: continueOnError);
        }

        private static Diagnostic[] GetSortedNonCompilerDiagnostics(IEnumerable<Diagnostic> diagnostics)
        {
            return diagnostics.Where(d => d.Category != "Compiler").OrderBy(d => d.Location.SourceSpan.Start).ToArray();
        }
    }
}
