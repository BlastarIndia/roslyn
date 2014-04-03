// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Instrumentation;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// The base class for representing command line arguments to a
    /// <see cref="CommonCompiler"/>.
    /// </summary>
    public abstract class CommandLineArguments
    {
        internal bool IsInteractive { get; set; }

        /// <summary>
        /// Directory used to resolve relative paths stored in the arguments.
        /// </summary>
        /// <remarks>
        /// Except for paths stored in <see cref="MetadataReferences"/>, all
        /// paths stored in the properties of this class are resolved and
        /// absolute. This is the directory that relative paths specified on
        /// command line were resolved against.
        /// </remarks>
        public string BaseDirectory { get; internal set; }

        /// <summary>
        /// Sequence of absolute paths used to search for references.
        /// </summary>
        public ImmutableArray<string> ReferencePaths { get; internal set; }

        /// <summary>
        /// Sequence of absolute paths used to search for key files.
        /// </summary>
        public ImmutableArray<string> KeyFileSearchPaths { get; internal set; }

        /// <summary>
        /// If true, use UTF8 for output.
        /// </summary>
        public bool Utf8Output { get; internal set; }

        /// <summary>
        /// Compilation name or null if not specified.
        /// </summary>
        public string CompilationName { get; internal set; }

        /// <summary>
        /// Name of the output file or null if not specified.
        /// </summary>
        public string OutputFileName { get; internal set; }

        /// <summary>
        /// Path of the PDB file or null if not specified.
        /// </summary>
        public string PdbPath { get; internal set; }

        /// <summary>
        /// Absolute path of the output directory.
        /// </summary>
        public string OutputDirectory { get; internal set; }

        /// <summary>
        /// Absolute path of the documentation comment XML file or null if not specified.
        /// </summary>
        public string DocumentationPath { get; internal set; }

        /// <summary>
        /// An absolute path of the App.config file or null if not specified.
        /// </summary>
        public string AppConfigPath { get; internal set; }

        /// <summary>
        /// Errors while parsing the command line arguments.
        /// </summary>
        public ImmutableArray<Diagnostic> Errors { get; internal set; }

        /// <summary>
        /// References to metadata supplied on the command line. 
        /// Includes assemblies specified via /r and netmodules specified via /addmodule.
        /// </summary>
        public ImmutableArray<CommandLineReference> MetadataReferences { get; internal set; }

        /// <summary>
        /// References to analyzers supplied on the command line.
        /// </summary>
        public ImmutableArray<CommandLineAnalyzerReference> AnalyzerReferences { get; internal set; }

        /// <summary>
        /// If true, prepend the command line header logo during 
        /// <see cref="CommonCompiler.Run"/>.
        /// </summary>
        public bool DisplayLogo { get; internal set; }

        /// <summary>
        /// If true, append the command line help during
        /// <see cref="CommonCompiler.Run"/>
        /// </summary>
        public bool DisplayHelp { get; internal set; }

        /// <summary>
        /// The path to a Win32 resource.
        /// </summary>
        public string Win32ResourceFile { get; internal set; }

        /// <summary>
        /// The path to a .ico icon file.
        /// </summary>
        public string Win32Icon { get; internal set; }

        /// <summary>
        /// The path to a Win32 manifest file to embed
        /// into the output portable executable (PE) file.
        /// </summary>
        public string Win32Manifest { get; internal set; }

        /// <summary>
        /// If true, do not embed any Win32 manifest, including
        /// one specified by <see cref="Win32Manifest"/> or any
        /// default manifest.
        /// </summary>
        public bool NoWin32Manifest { get; internal set; }

        /// <summary>
        /// Resources specified as arguments to the compilation.
        /// </summary>
        public ImmutableArray<ResourceDescription> ManifestResources { get; internal set; }

        /// <summary>
        /// Encoding to be used for source files or 'null' for autodetect/default.
        /// </summary>
        public Encoding Encoding { get; internal set; }

        /// <summary>
        /// Arguments following script argument separator "--" or null if <see cref="IsInteractive"/> is false.
        /// </summary>
        public ImmutableArray<string> ScriptArguments { get; internal set; }

        /// <summary>
        /// Source file paths.
        /// </summary>
        /// <remarks>
        /// Includes files specified directly on command line as well as files matching patterns specified 
        /// on command line using '*' and '?' wildcards or /recurse option.
        /// </remarks>
        public ImmutableArray<CommandLineSourceFile> SourceFiles { get; internal set; }

        /// <summary>
        /// Fule path of a log of file paths accessed by the compiler, or null if file logging should be suppressed.
        /// </summary>
        /// <remarks>
        /// Two log files will be created: 
        /// One with path <see cref="TouchedFilesPath"/> and extension ".read" logging the files read,
        /// and second with path <see cref="TouchedFilesPath"/> and extension ".write" logging the files written to  during compilation.
        /// </remarks>
        public string TouchedFilesPath { get; internal set; }

        /// <summary>
        /// If true, prints the full path of the file containing errors or
        /// warnings in diagnostics.
        /// </summary>
        public bool PrintFullPaths { get; internal set; }

        /// <summary>
        /// Options to the <see cref="CommandLineParser"/>.
        /// </summary>
        /// <returns></returns>
        public ParseOptions ParseOptions
        {
            get { return ParseOptionsCore; }
        }

        /// <summary>
        /// Options to the <see cref="Compilation"/>.
        /// </summary>
        public CompilationOptions CompilationOptions
        {
            get { return CompilationOptionsCore; }
        }

        protected abstract ParseOptions ParseOptionsCore { get; }
        protected abstract CompilationOptions CompilationOptionsCore { get; }

        /// <summary>
        /// Specify the preferred output language name.
        /// </summary>
        public CultureInfo PreferredUILang { get; internal set; }

        internal Guid SqmSessionGuid { get; set; }

        internal CommandLineArguments()
        {
        }

        #region Metadata References

        /// <summary>
        /// Resolves metadata references stored in <see cref="MetadataReferences"/> using given file resolver and metadata provider.
        /// </summary>
        /// <param name="metadataResolver"><see cref="MetadataReferenceResolver"/> to use for assembly name and relative path resolution.</param>
        /// <param name="metadataProvider"><see cref="MetadataReferenceProvider"/> to read metadata from resolved paths.</param>
        /// <returns>Yields resolved metadata references or <see cref="UnresolvedMetadataReference"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="metadataResolver"/> is null.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="metadataProvider"/> is null.</exception>
        public IEnumerable<MetadataReference> ResolveMetadataReferences(MetadataReferenceResolver metadataResolver, MetadataReferenceProvider metadataProvider)
        {
            if (metadataResolver == null)
            {
                throw new ArgumentNullException("metadataResolver");
            }

            if (metadataProvider == null)
            {
                throw new ArgumentNullException("metadataProvider");
            }

            return ResolveMetadataReferences(metadataResolver, metadataProvider, diagnosticsOpt: null, messageProviderOpt: null);
        }

        /// <summary>
        /// Resolves metadata references stored in <see cref="MetadataReferences"/> using given file resolver and metadata provider.
        /// If a non-null diagnostic bag <paramref name="diagnosticsOpt"/> is provided, it catches exceptions that may be generated while reading the metadata file and
        /// reports appropriate diagnostics.
        /// Otherwise, if <paramref name="diagnosticsOpt"/> is null, the exceptions are unhandled.
        /// </summary>
        internal IEnumerable<MetadataReference> ResolveMetadataReferences(MetadataReferenceResolver metadataResolver, MetadataReferenceProvider metadataProvider, List<DiagnosticInfo> diagnosticsOpt, CommonMessageProvider messageProviderOpt)
        {
            Debug.Assert(metadataResolver != null);
            Debug.Assert(metadataProvider != null);

            foreach (CommandLineReference cmdReference in MetadataReferences)
            {
                yield return ResolveMetadataReference(cmdReference, metadataResolver, metadataProvider, diagnosticsOpt, messageProviderOpt) ?? 
                    new UnresolvedMetadataReference(cmdReference.Reference, cmdReference.Properties);
            }
        }

        internal MetadataReference ResolveMetadataReference(CommandLineReference cmdReference, MetadataReferenceResolver metadataResolver, MetadataReferenceProvider metadataProvider, List<DiagnosticInfo> diagnosticsOpt, CommonMessageProvider messageProviderOpt)
        {
            Debug.Assert(metadataResolver != null);
            Debug.Assert(metadataProvider != null);
            Debug.Assert((diagnosticsOpt == null) == (messageProviderOpt == null));

            string resolvedPath = metadataResolver.ResolveReference(cmdReference.Reference, baseFilePath: null);
            if (resolvedPath == null)
            {
                if (diagnosticsOpt != null)
                {
                    diagnosticsOpt.Add(new DiagnosticInfo(messageProviderOpt, messageProviderOpt.ERR_MetadataFileNotFound, cmdReference.Reference));
                }

                return null;
            }

            try
            {
                return metadataProvider.GetReference(resolvedPath, cmdReference.Properties);
            }
            catch (Exception e) if (diagnosticsOpt != null && (e is BadImageFormatException || e is IOException))
            {
                var diagnostic = PortableExecutableReference.ExceptionToDiagnostic(e, messageProviderOpt, Location.None, cmdReference.Reference, cmdReference.Properties.Kind);
                diagnosticsOpt.Add(((DiagnosticWithInfo)diagnostic).Info);
                return null;
            }
        }

        #endregion

        #region Analyzer References

        /// <summary>
        /// Resolves analyzer references stored in <see cref="AnalyzerReferences"/> using given file resolver.
        /// </summary>
        /// <returns>Yields resolved <see cref="AnalyzerFileReference"/> or <see cref="UnresolvedAnalyzerReference"/>.</returns>
        public IEnumerable<AnalyzerReference> ResolveAnalyzerReferences()
        {
            foreach (CommandLineAnalyzerReference cmdLineReference in AnalyzerReferences)
            {
                yield return ResolveAnalyzerReference(cmdLineReference) ?? (AnalyzerReference)new UnresolvedAnalyzerReference(cmdLineReference.FilePath);
            }
        }

        internal ImmutableArray<IDiagnosticAnalyzer> ResolveAnalyzersFromArguments(List<DiagnosticInfo> diagnostics, CommonMessageProvider messageProvider, TouchedFileLogger touchedFiles)
        {
            var builder = ImmutableArray.CreateBuilder<IDiagnosticAnalyzer>();
            foreach (var reference in AnalyzerReferences)
            {
                var resolvedReference = ResolveAnalyzerReference(reference);
                if (resolvedReference != null)
                {
                    resolvedReference.AddAnalyzers(builder, diagnostics, messageProvider);
                }
                else
                {
                    diagnostics.Add(new DiagnosticInfo(messageProvider, messageProvider.ERR_MetadataFileNotFound, reference.FilePath));
                }
            }

            return builder.ToImmutable();
        }

        private AnalyzerFileReference ResolveAnalyzerReference(CommandLineAnalyzerReference reference)
        {
            string resolvedPath = FileUtilities.ResolveRelativePath(reference.FilePath, basePath: null, baseDirectory: BaseDirectory, searchPaths: ReferencePaths, fileExists: File.Exists);
            if (File.Exists(resolvedPath))
            {
                resolvedPath = FileUtilities.TryNormalizeAbsolutePath(resolvedPath);
            }
            else
            {
                resolvedPath = null;
            }

            if (resolvedPath != null)
            {
                return new AnalyzerFileReference(resolvedPath);
            }

            return null;
        }

        #endregion
    }
}
