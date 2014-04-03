﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;
using Microsoft.CodeAnalysis.CSharp.UnitTests;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.MetadataUtilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.Test.Utilities
{
    public abstract class CSharpTestBase : CSharpTestBaseBase
    {
        protected new CSharpCompilation GetCompilationForEmit(
            IEnumerable<string> source,
            MetadataReference[] additionalRefs,
            CompilationOptions options)
        {
            return (CSharpCompilation)base.GetCompilationForEmit(source, additionalRefs, options);
        }

        internal new IEnumerable<ModuleSymbol> ReferencesToModuleSymbols(IEnumerable<MetadataReference> references, MetadataImportOptions importOptions = MetadataImportOptions.Public)
        {
            return base.ReferencesToModuleSymbols(references, importOptions).Cast<ModuleSymbol>();
        }

        protected new CSharpCompilationOptions DefaultCompilationOptions
        {
            get { return (CSharpCompilationOptions)base.DefaultCompilationOptions; }
        }

        protected new CSharpCompilationOptions OptionsDll
        {
            get { return (CSharpCompilationOptions)base.OptionsDll; }
        }

        private Action<IModuleSymbol, EmitOptions> Translate2(Action<ModuleSymbol> action)
        {
            if (action != null)
            {
                return (m, _) => action((ModuleSymbol)m);
            }
            else
            {
                return null;
            }
        }

        private Action<IModuleSymbol> Translate(Action<ModuleSymbol> action)
        {
            if (action != null)
            {
                return m => action((ModuleSymbol)m);
            }
            else
            {
                return null;
            }
        }

        internal CompilationVerifier CompileAndVerify(
            string source,
            MetadataReference[] additionalRefs = null,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<ModuleSymbol> sourceSymbolValidator = null,
            Action<PEAssembly, EmitOptions> assemblyValidator = null,
            Action<ModuleSymbol> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            CompilationOptions options = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false,
            bool verify = true)
        {
            return base.CompileAndVerify(
                source: source,
                additionalRefs: additionalRefs,
                dependencies: dependencies,
                emitOptions: emitOptions,
                sourceSymbolValidator: Translate2(sourceSymbolValidator),
                assemblyValidator: assemblyValidator,
                symbolValidator: Translate2(symbolValidator),
                expectedSignatures: expectedSignatures,
                expectedOutput: expectedOutput,
                options: options,
                collectEmittedAssembly: collectEmittedAssembly,
                emitPdb: emitPdb,
                verify: verify);
        }

        internal CompilationVerifier CompileAndVerify(
            string[] sources,
            MetadataReference[] additionalRefs = null,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<ModuleSymbol> sourceSymbolValidator = null,
            Action<PEAssembly, EmitOptions> validator = null,
            Action<ModuleSymbol> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            CompilationOptions options = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false,
            bool verify = true)
        {
            return base.CompileAndVerify(
                sources,
                additionalRefs,
                dependencies,
                emitOptions,
                Translate2(sourceSymbolValidator),
                validator,
                Translate2(symbolValidator),
                expectedSignatures,
                expectedOutput,
                options,
                collectEmittedAssembly,
                emitPdb,
                verify);
        }

        internal CompilationVerifier CompileAndVerify(
            Compilation compilation,
            IEnumerable<ResourceDescription> manifestResources = null,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<ModuleSymbol> sourceSymbolValidator = null,
            Action<PEAssembly, EmitOptions> validator = null,
            Action<ModuleSymbol> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false,
            bool verify = true)
        {
            return base.CompileAndVerify(
                compilation,
                manifestResources,
                dependencies,
                emitOptions,
                Translate2(sourceSymbolValidator),
                validator,
                Translate2(symbolValidator),
                expectedSignatures,
                expectedOutput,
                collectEmittedAssembly,
                emitPdb,
                verify);
        }

        internal CompilationVerifier CompileAndVerifyOnWin8Only(
            string source,
            MetadataReference[] additionalRefs = null,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<ModuleSymbol> sourceSymbolValidator = null,
            Action<PEAssembly> validator = null,
            Action<ModuleSymbol> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            CompilationOptions options = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false)
        {
            return base.CompileAndVerifyOnWin8Only(
                source,
                additionalRefs,
                dependencies,
                emitOptions,
                Translate(sourceSymbolValidator),
                validator,
                Translate(symbolValidator),
                expectedSignatures,
                expectedOutput,
                options,
                collectEmittedAssembly,
                emitPdb);
        }

        internal CompilationVerifier CompileAndVerifyOnWin8Only(
            Compilation compilation,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<ModuleSymbol> sourceSymbolValidator = null,
            Action<PEAssembly> validator = null,
            Action<ModuleSymbol> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false)
        {
            return base.CompileAndVerifyOnWin8Only(
                compilation,
                dependencies,
                emitOptions,
                Translate(sourceSymbolValidator),
                validator,
                Translate(symbolValidator),
                expectedSignatures,
                expectedOutput,
                collectEmittedAssembly,
                emitPdb);
        }

        internal CompilationVerifier CompileAndVerifyOnWin8Only(
            string[] sources,
            MetadataReference[] additionalRefs = null,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<ModuleSymbol> sourceSymbolValidator = null,
            Action<PEAssembly> validator = null,
            Action<ModuleSymbol> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            CompilationOptions options = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false,
            bool verify = true)
        {
            return base.CompileAndVerifyOnWin8Only(
                sources,
                additionalRefs,
                dependencies,
                emitOptions,
                Translate(sourceSymbolValidator),
                validator,
                Translate(symbolValidator),
                expectedSignatures,
                expectedOutput,
                options,
                collectEmittedAssembly,
                emitPdb,
                verify);
        }
    }

    public abstract class CSharpTestBaseBase : CommonTestBase
    {
        public static CSharpCompilation CreateWinRtCompilation(string text)
        {
            return CSharpTestBase.CreateCompilationWithMscorlib(text, WinRtRefs, TestOptions.Exe);
        }

        internal static DiagnosticDescription Diagnostic(ErrorCode code, string squiggledText = null, object[] arguments = null,
            LinePosition? startLocation = null, Func<SyntaxNode, bool> syntaxNodePredicate = null, bool argumentOrderDoesNotMatter = false)
        {
            return new DiagnosticDescription((int)code, false, squiggledText, arguments, startLocation, syntaxNodePredicate, argumentOrderDoesNotMatter, typeof(ErrorCode));
        }

        internal static DiagnosticDescription Diagnostic(string code, string squiggledText = null, object[] arguments = null,
            LinePosition? startLocation = null, Func<SyntaxNode, bool> syntaxNodePredicate = null, bool argumentOrderDoesNotMatter = false)
        {
            return new DiagnosticDescription(
                code: code, isWarningAsError: false, squiggledText: squiggledText, arguments: arguments,
                startLocation: startLocation, syntaxNodePredicate: syntaxNodePredicate,
                argumentOrderDoesNotMatter: argumentOrderDoesNotMatter, errorCodeType: typeof(string));
        }

        internal override IEnumerable<IModuleSymbol> ReferencesToModuleSymbols(IEnumerable<MetadataReference> references, MetadataImportOptions importOptions = MetadataImportOptions.Public)
        {
            var options = TestOptions.Dll.WithMetadataImportOptions(importOptions);
            var tc1 = CSharpCompilation.Create("Dummy", new SyntaxTree[0], references, options);
            return references.Select(r =>
            {
                if (r.Properties.Kind == MetadataImageKind.Assembly)
                {
                    var assemblySymbol = tc1.GetReferencedAssemblySymbol(r);
                    return (object)assemblySymbol == null ? null : assemblySymbol.Modules[0];
                }
                else
                {
                    return tc1.GetReferencedModuleSymbol(r);
                }
            });
        }

        protected override CompilationOptions DefaultCompilationOptions
        {
            get { return new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimize: true); }
        }

        protected override CompilationOptions OptionsDll
        {
            get { return new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimize: true); }
        }

        #region SyntaxTree Factories

        public static SyntaxTree Parse(string text, string filename = "", CSharpParseOptions options = null)
        {
            if ((object)options == null)
            {
                options = TestOptions.Regular;
            }

            var stringText = StringText.From(text);
            return SyntaxFactory.ParseSyntaxTree(stringText, filename, options);
        }

        public static SyntaxTree ParseWithChecksum(string text, string filename = "", CSharpParseOptions options = null)
        {
            if ((object)options == null)
            {
                options = TestOptions.Regular;
            }

            var stringText = StringText.From(text, Hash.ComputeSha1(Encoding.UTF8.GetBytes(text)));
            return SyntaxFactory.ParseSyntaxTree(stringText, filename, options);
        }

        public static SyntaxTree[] Parse(IEnumerable<string> sources)
        {
            if (sources == null || !sources.Any())
            {
                return new SyntaxTree[] { };
            }

            return Parse(sources.ToArray());
        }

        public static SyntaxTree[] Parse(params string[] sources)
        {
            if (sources == null || (sources.Length == 1 && null == sources[0]))
            {
                return new SyntaxTree[] { };
            }

            return sources.Select(src => Parse(src)).ToArray();
        }

        public static SyntaxTree ParseWithRoundTripCheck(string text)
        {
            var tree = Parse(text);
            var parsedText = tree.GetRoot();
            // we validate the text roundtrips
            Assert.Equal(text, parsedText.ToFullString());
            return tree;
        }

        #endregion

        #region Compilation Factories

        public static CSharpCompilation CreateCompilationWithCustomILSource(
            string source,
            string ilSource,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            bool appendDefaultHeader = true)
        {
            if (string.IsNullOrEmpty(ilSource))
            {
                return CreateCompilationWithMscorlib(source, references, compOptions);
            }

            IEnumerable<MetadataReference> metadataReferences = new[] { CompileIL(ilSource, appendDefaultHeader) };
            if (references != null)
            {
                metadataReferences = metadataReferences.Concat(references);
            }

            return CreateCompilationWithMscorlib(source, metadataReferences, compOptions);
        }

        public static CSharpCompilation CreateCompilationWithMscorlib45(
            IEnumerable<SyntaxTree> source,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            string assemblyName = "")
        {
            var refs = new List<MetadataReference>();
            if (references != null)
            {
                refs.AddRange(references);
            }
            refs.Add(MscorlibRef_v4_0_30316_17626);
            return CreateCompilation(source, refs, compOptions, assemblyName);
        }

        public static CSharpCompilation CreateCompilationWithMscorlib45(
            string source,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            string sourceFileName = "",
            string assemblyName = "")
        {
            return CreateCompilationWithMscorlib45(
                new SyntaxTree[] { Parse(source, sourceFileName) },
                references,
                compOptions,
                assemblyName);
        }

        public static CSharpCompilation CreateCompilationWithMscorlib(
            string text,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            CSharpParseOptions parseOptions = null,
            string assemblyName = "",
            string sourceFileName = "")
        {
            return CreateCompilationWithMscorlib(
                new[] { Parse(text, sourceFileName, parseOptions) },
                references: references,
                compOptions: compOptions,
                assemblyName: assemblyName);
        }

        public static CSharpCompilation CreateCompilationWithMscorlib(
            IEnumerable<string> sources,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            string assemblyName = "")
        {
            return CreateCompilationWithMscorlib(Parse(sources), references, compOptions, assemblyName);
        }

        public static CSharpCompilation CreateCompilationWithMscorlib(
            SyntaxTree syntaxTree,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            string assemblyName = "")
        {
            return CreateCompilationWithMscorlib(new SyntaxTree[] { syntaxTree }, references, compOptions, assemblyName);
        }

        public static CSharpCompilation CreateCompilationWithMscorlib(
            IEnumerable<SyntaxTree> trees,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            string assemblyName = "")
        {
            return CreateCompilation(trees, (references != null) ? new[] { MscorlibRef }.Concat(references) : new[] { MscorlibRef }, compOptions, assemblyName);
        }

        public static CSharpCompilation CreateCompilationWithMscorlibAndSystemCore(
            IEnumerable<SyntaxTree> trees,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            string assemblyName = "")
        {
            return CreateCompilation(trees, (references != null) ? new[] { MscorlibRef, SystemCoreRef }.Concat(references) : new[] { MscorlibRef, SystemCoreRef }, compOptions, assemblyName);
        }

        public static CSharpCompilation CreateCompilationWithMscorlibAndSystemCore(
            string text,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            CSharpParseOptions parseOptions = null,
            string assemblyName = "")
        {
            references = (references != null) ? new[] { SystemCoreRef }.Concat(references) : new[] { SystemCoreRef };

            return CreateCompilationWithMscorlib(
                new[] { Parse(text, "", parseOptions) },
                references: references,
                compOptions: compOptions,
                assemblyName: assemblyName);
        }

        public static CSharpCompilation CreateCompilationWithMscorlibAndDocumentationComments(
            string text,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            string assemblyName = "Test")
        {
            return CreateCompilationWithMscorlib(
                new[] { Parse(text, options: TestOptions.RegularWithDocumentationComments) },
                references: references,
                compOptions: (compOptions ?? TestOptions.Dll).WithXmlReferenceResolver(XmlFileResolver.Default),
                assemblyName: assemblyName);
        }

        public static CSharpCompilation CreateCompilation(
            string source,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            string assemblyName = "")
        {
            return CreateCompilation(new[] { Parse(source) }, references, compOptions, assemblyName);
        }

        public static CSharpCompilation CreateCompilation(
            IEnumerable<string> sources,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            string assemblyName = "")
        {
            return CreateCompilation(Parse(sources), references, compOptions, assemblyName);
        }

        public static CSharpCompilation CreateCompilation(
            IEnumerable<SyntaxTree> trees,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions compOptions = null,
            string assemblyName = "")
        {
            if (compOptions == null)
            {
                compOptions = TestOptions.Dll.WithOptimizations(true);
            }

            // Using single-threaded build if debugger attached, to simplify debugging.
            if (Debugger.IsAttached)
            {
                compOptions = compOptions.WithConcurrentBuild(false);
            }

            return CSharpCompilation.Create(
                assemblyName == "" ? GetUniqueName() : assemblyName,
                trees,
                references,
                compOptions);
        }

        public static CSharpCompilation CreateCompilation(
            AssemblyIdentity identity,
            string[] sources,
            MetadataReference[] refs)
        {
            SyntaxTree[] trees = null;

            if (sources != null)
            {
                trees = new SyntaxTree[sources.Length];

                for (int i = 0; i < sources.Length; i++)
                {
                    trees[i] = Parse(sources[i]);
                }
            }

            var tc1 = CSharpCompilation.Create(identity.Name, options: TestOptions.Dll, references: refs, syntaxTrees: trees);
            Assert.NotNull(tc1.Assembly); // force creation of SourceAssemblySymbol

            ((SourceAssemblySymbol)tc1.Assembly).lazyAssemblyIdentity = identity;

            return tc1;
        }

        public CompilationVerifier CompileWithCustomILSource(string cSharpSource, string ilSource, Action<CSharpCompilation> compilationVerifier = null, bool importInternals = true, EmitOptions emitOptions = EmitOptions.All, string expectedOutput = null)
        {
            var compilationOptions = importInternals ?
                ((expectedOutput != null) ? TestOptions.ExeAlwaysImportInternals : TestOptions.DllAlwaysImportInternals) :
                ((expectedOutput != null) ? TestOptions.Exe : TestOptions.Dll);

            if (ilSource == null)
            {
                var c = CreateCompilationWithMscorlib(cSharpSource, compOptions: compilationOptions);
                return CompileAndVerify(c, emitOptions: emitOptions, expectedOutput: expectedOutput);
            }

            MetadataReference reference = null;
            using (var tempAssembly = SharedCompilationUtils.IlasmTempAssembly(ilSource))
            {
                reference = new MetadataImageReference(ReadFromFile(tempAssembly.Path));
            }

            var compilation = CreateCompilationWithMscorlib(cSharpSource, new[] { reference }, compilationOptions);
            if (compilationVerifier != null)
            {
                compilationVerifier(compilation);
            }

            return CompileAndVerify(compilation, emitOptions: emitOptions, expectedOutput: expectedOutput);
        }

        protected override Compilation GetCompilationForEmit(
            IEnumerable<string> source,
            MetadataReference[] additionalRefs,
            CompilationOptions options)
        {
            return CreateCompilationWithMscorlib(
                source,
                references: (additionalRefs != null) ? additionalRefs.ToList() : null,
                compOptions: (CSharpCompilationOptions)options,
                assemblyName: GetUniqueName());
        }

        /// <summary>
        /// Like CompileAndVerify, but confirms that execution raises an exception.
        /// </summary>
        /// <typeparam name="T">Expected type of the exception.</typeparam>
        /// <param name="source">Program to compile and execute.</param>
        /// <param name="expectedMessage">Ignored if null.</param>
        internal CompilationVerifier CompileAndVerifyException<T>(string source, string expectedMessage = null, bool allowUnsafe = false, EmitOptions emitOptions = EmitOptions.All) where T : Exception
        {
            var comp = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithAllowUnsafe(allowUnsafe).WithOptimizations(true));
            return CompileAndVerifyException<T>(comp, expectedMessage, emitOptions);
        }

        internal CompilationVerifier CompileAndVerifyException<T>(CSharpCompilation comp, string expectedMessage = null, EmitOptions emitOptions = EmitOptions.All) where T : Exception
        {
            try
            {
                CompileAndVerify(comp, emitOptions: emitOptions, expectedOutput: ""); //need expected output to force execution
                Assert.False(true, string.Format("Expected exception {0}({1})", typeof(T).Name, expectedMessage));
            }
            catch (ExecutionException x)
            {
                var e = x.InnerException;
                Assert.IsType<T>(e);
                if (expectedMessage != null)
                {
                    Assert.Equal(expectedMessage, e.Message);
                }
            }

            return CompileAndVerify(comp, emitOptions: emitOptions);
        }

        #endregion

        #region Semantic Model Helpers

        public Tuple<TNode, SemanticModel> GetBindingNodeAndModel<TNode>(CSharpCompilation compilation, int treeIndex = 0) where TNode : SyntaxNode
        {
            var node = GetBindingNode<TNode>(compilation, treeIndex);
            return new Tuple<TNode, SemanticModel>(node, compilation.GetSemanticModel(compilation.SyntaxTrees[treeIndex]));
        }

        public Tuple<IList<TNode>, SemanticModel> GetBindingNodesAndModel<TNode>(CSharpCompilation compilation, int treeIndex = 0, int which = -1) where TNode : SyntaxNode
        {
            var nodes = GetBindingNodes<TNode>(compilation, treeIndex, which);
            return new Tuple<IList<TNode>, SemanticModel>(nodes, compilation.GetSemanticModel(compilation.SyntaxTrees[treeIndex]));
        }

        /// <summary>
        /// This method handles one binding text with strong SyntaxNode type
        /// </summary>
        /// <typeparam name="TNode"></typeparam>
        /// <param name="compilation"></param>
        /// <param name="treeIndex"></param>
        /// <returns></returns>
        public TNode GetBindingNode<TNode>(CSharpCompilation compilation, int treeIndex = 0) where TNode : SyntaxNode
        {
            Assert.True(compilation.SyntaxTrees.Length > treeIndex, "Compilation has enough trees");
            var tree = compilation.SyntaxTrees[treeIndex];

            const string bindStart = "/*<bind>*/";
            const string bindEnd = "/*</bind>*/";
            return FindBindingNode<TNode>(tree, bindStart, bindEnd);
        }

        /// <summary>
        /// Find multiple binding nodes by looking for pair /*&lt;bind#&gt;*/ &amp; /*&lt;/bind#&gt;*/ in source text
        /// </summary>
        /// <param name="compilation"></param>
        /// <param name="treeIndex">which tree</param>
        /// <param name="which">
        ///     * if which &lt; 0, find ALL wrpaaed nodes
        ///     * if which &gt;=0, find a specific binding node wrapped by /*&lt;bind#&gt;*/ &amp; /*&lt;/bind#&gt;*/
        ///       e.g. if which = 1, find node wrapped by /*&lt;bind1&gt;*/ &amp; /*&lt;/bind1&gt;*/
        /// </param>
        /// <returns></returns>
        public IList<TNode> GetBindingNodes<TNode>(CSharpCompilation compilation, int treeIndex = 0, int which = -1) where TNode : SyntaxNode
        {
            Assert.True(compilation.SyntaxTrees.Length > treeIndex, "Compilation has enough trees");
            var tree = compilation.SyntaxTrees[treeIndex];

            var nodeList = new List<TNode>();
            string text = tree.GetRoot().ToFullString();

            const string bindStartFmt = "/*<bind{0}>*/";
            const string bindEndFmt = "/*</bind{0}>*/";
            // find all
            if (which < 0)
            {
                // assume tags with number are in increasing order, no jump
                for (byte i = 0; i < 255; i++)
                {
                    var start = String.Format(bindStartFmt, i);
                    var end = String.Format(bindEndFmt, i);

                    var bindNode = FindBindingNode<TNode>(tree, start, end);
                    // done
                    if (bindNode == null)
                        break;

                    nodeList.Add(bindNode);
                }
            }
            else
            {
                var start2 = String.Format(bindStartFmt, which);
                var end2 = String.Format(bindEndFmt, which);

                var bindNode = FindBindingNode<TNode>(tree, start2, end2);
                // done
                if (bindNode != null)
                    nodeList.Add(bindNode);
            }

            return nodeList;
        }

        private static TNode FindBindingNode<TNode>(SyntaxTree tree, string startTag, string endTag) where TNode : SyntaxNode
        {
            // =================
            // Get Binding Text
            string text = tree.GetRoot().ToFullString();
            int start = text.IndexOf(startTag);
            if (start < 0)
                return null;

            start += startTag.Length;
            int end = text.IndexOf(endTag);
            Assert.True(end > start, "Bind Pos: end > start");
            // get rid of white spaces if any
            var bindText = text.Substring(start, end - start).Trim();
            if (String.IsNullOrWhiteSpace(bindText))
                return null;

            // =================
            // Get Binding Node
            var node = tree.GetRoot().FindToken(start).Parent;
            while ((node != null && node.ToString() != bindText))
            {
                node = node.Parent;
            }
            // =================
            // Get Binding Node with match node type
            if (node != null)
            {
                while ((node as TNode) == null)
                {
                    if (node.Parent != null && node.Parent.ToString() == bindText)
                    {
                        node = node.Parent;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            Assert.NotNull(node); // If this trips, then node  wasn't found
            Assert.IsAssignableFrom(typeof(TNode), node);
            Assert.Equal(bindText, node.ToString());
            return ((TNode)node);
        }
        #endregion

        #region Attributes

        internal IEnumerable<string> GetAttributeNames(ImmutableArray<SynthesizedAttributeData> attributes)
        {
            return attributes.Select(a => a.AttributeClass.Name);
        }

        internal IEnumerable<string> GetAttributeNames(ImmutableArray<CSharpAttributeData> attributes)
        {
            return attributes.Select(a => a.AttributeClass.Name);
        }

        #endregion

        #region Documentation Comments

        internal static string GetDocumentationCommentText(CSharpCompilation compilation, params DiagnosticDescription[] expectedDiagnostics)
        {
            return GetDocumentationCommentText(compilation, outputName: null, filterTree: null, expectedDiagnostics: expectedDiagnostics);
        }

        internal static string GetDocumentationCommentText(CSharpCompilation compilation, string outputName = null, SyntaxTree filterTree = null, TextSpan? filterSpanWithinTree = null, params DiagnosticDescription[] expectedDiagnostics)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                DiagnosticBag diagnostics = DiagnosticBag.GetInstance();
                DocumentationCommentCompiler.WriteDocumentationCommentXml(compilation, outputName, stream, diagnostics, default(CancellationToken), filterTree, filterSpanWithinTree);
                if (expectedDiagnostics != null)
                {
                    diagnostics.Verify(expectedDiagnostics);
                }
                diagnostics.Free();

                string text = Encoding.UTF8.GetString(stream.GetBuffer());
                int length = text.IndexOf('\0');
                if (length >= 0)
                {
                    text = text.Substring(0, length);
                }
                return text.Trim();
            }
        }

        #endregion Documentation Comments

        #region IL Validation

        internal override string VisualizeRealIL(IModuleSymbol peModule, CompilationTestData.MethodData methodData)
        {
            return VisualizeRealIL((PEModuleSymbol)peModule, methodData);
        }

        /// <summary>
        /// Returns a string representation of IL read from metadata.
        /// </summary>
        /// <remarks>
        /// Currently unsupported IL decoding:
        /// - multidimentional arrays
        /// - vararg calls
        /// - winmd
        /// - global methods
        /// </remarks>
        internal unsafe static string VisualizeRealIL(PEModuleSymbol peModule, CompilationTestData.MethodData methodData)
        {
            var typeName = GetContainingTypeMetadataName(methodData.Method);
            // TODO (tomat): global methods (typeName == null)

            var type = peModule.ContainingAssembly.GetTypeByMetadataName(typeName);

            // TODO (tomat): overloaded methods
            var method = (PEMethodSymbol)type.GetMembers(methodData.Method.MetadataName).Single();

            var methodIL = peModule.Module.GetMethodILOrThrow(method.Handle);
            Assert.NotNull(methodIL);

            var moduleDecoder = new MetadataDecoder(peModule);
            var peMethod = (PEMethodSymbol)moduleDecoder.GetSymbolForILToken(method.Handle);

            StringBuilder sb = new StringBuilder();
            var ilBytes = methodIL.GetILBytes();

            var ehHandlerRegions = ToHandlerSpans(moduleDecoder, methodIL.ExceptionRegions);

            var methodDecoder = new MetadataDecoder(peModule, peMethod);

            ImmutableArray<ILVisualizer.LocalInfo> localDefinitions;
            if (!methodIL.LocalSignature.IsNil)
            {
                var signature = peModule.Module.MetadataReader.GetLocalSignature(methodIL.LocalSignature);
                var localInfos = methodDecoder.DecodeLocalSignatureOrThrow(signature);
                localDefinitions = ToLocalDefinitions(localInfos, methodData.ILBuilder);
            }
            else
            {
                localDefinitions = ImmutableArray.Create<ILVisualizer.LocalInfo>();
            }

            // TODO (tomat): the .maxstack in IL can't be less than 8, but many tests expect .maxstack < 8
            int maxStack = (methodIL.MaxStack == 8 && methodData.ILBuilder.MaxStack < 8) ? methodData.ILBuilder.MaxStack : methodIL.MaxStack;

            var visualizer = new Visualizer(new MetadataDecoder(peModule, peMethod));

            visualizer.DumpMethod(sb, maxStack, ilBytes, localDefinitions, ehHandlerRegions);

            return sb.ToString();
        }

        private static string GetContainingTypeMetadataName(IMethodSymbol method)
        {
            var type = method.ContainingType;
            if (type == null)
            {
                return null;
            }

            string ns = type.ContainingNamespace.MetadataName;
            var result = type.MetadataName;

            while ((type = type.ContainingType) != null)
            {
                result = type.MetadataName + "+" + result;
            }

            return (ns.Length > 0) ? ns + "." + result : result;
        }

        private static ImmutableArray<ILVisualizer.LocalInfo> ToLocalDefinitions(ImmutableArray<MetadataDecoder.LocalInfo> localInfos, ILBuilder builder)
        {
            if (localInfos.IsEmpty)
            {
                return ImmutableArray.Create<ILVisualizer.LocalInfo>();
            }

            var result = new ILVisualizer.LocalInfo[localInfos.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var typeRef = localInfos[i].Type;
                var builderLocal = builder.LocalSlotManager.LocalsInOrder()[i];
                result[i] = new ILVisualizer.LocalInfo(builderLocal.Name, typeRef, localInfos[i].IsPinned, localInfos[i].IsByRef);
            }

            return result.AsImmutableOrNull();
        }

        private static IReadOnlyList<ILVisualizer.HandlerSpan> ToHandlerSpans(MetadataDecoder decoder, ImmutableArray<ExceptionRegion> entries)
        {
            if (entries.Length == 0)
            {
                return SpecializedCollections.EmptyArray<ILVisualizer.HandlerSpan>();
            }

            var result = new List<ILVisualizer.HandlerSpan>();
            foreach (ExceptionRegion entry in entries)
            {
                int tryStartOffset = entry.TryOffset;
                int tryEndOffset = entry.TryOffset + entry.TryLength;
                var span = new ILVisualizer.HandlerSpan(ILVisualizer.HandlerKind.Try, null, (uint)tryStartOffset, (uint)tryEndOffset);

                if (result.Count == 0 || span.CompareTo(result[result.Count - 1]) != 0)
                {
                    result.Add(span);
                }
            }

            foreach (ExceptionRegion entry in entries)
            {
                int handlerStartOffset = entry.HandlerOffset;
                int handlerEndOffset = entry.HandlerOffset + entry.HandlerLength;

                ILVisualizer.HandlerSpan span;
                switch (entry.Kind)
                {
                    case ExceptionRegionKind.Catch:
                        var exceptionType = (Cci.ITypeReference)decoder.GetSymbolForILToken(entry.CatchType);
                        if (exceptionType == null)
                        {
                            throw new InvalidOperationException();
                        }

                        span = new ILVisualizer.HandlerSpan(ILVisualizer.HandlerKind.Catch, exceptionType, (uint)handlerStartOffset, (uint)handlerEndOffset);
                        break;

                    case ExceptionRegionKind.Fault:
                        span = new ILVisualizer.HandlerSpan(ILVisualizer.HandlerKind.Fault, null, (uint)handlerStartOffset, (uint)handlerEndOffset);
                        break;

                    case ExceptionRegionKind.Filter:
                        span = new ILVisualizer.HandlerSpan(ILVisualizer.HandlerKind.Filter, null, (uint)handlerStartOffset, (uint)handlerEndOffset, (uint)entry.FilterOffset);
                        break;

                    case ExceptionRegionKind.Finally:
                        span = new ILVisualizer.HandlerSpan(ILVisualizer.HandlerKind.Finally, null, (uint)handlerStartOffset, (uint)handlerEndOffset);
                        break;

                    default:
                        throw new InvalidOperationException();
                }

                result.Add(span);
            }

            return result;
        }

        private sealed class Visualizer : ILVisualizer
        {
            private readonly MetadataDecoder decoder;

            public Visualizer(MetadataDecoder decoder)
            {
                this.decoder = decoder;
            }

            public override string VisualizeUserString(uint token)
            {
                var reader = decoder.ModuleSymbol.Module.GetMetadataReader();
                return "\"" + reader.GetUserString((UserStringHandle)MetadataTokens.Handle((int)token)) + "\"";
            }

            public override string VisualizeSymbol(uint token)
            {
                Cci.IReference reference = decoder.GetSymbolForILToken(MetadataTokens.Handle((int)token));
                ISymbol symbol = reference as ISymbol;
                return string.Format("\"{0}\"", symbol == null ? (object)reference : symbol.ToDisplayString(SymbolDisplayFormat.ILVisualizationFormat));
            }

            public override string VisualizeLocalType(object type)
            {
                ISymbol symbol = type as ISymbol;
                return symbol == null ? type.ToString() : symbol.ToDisplayString(SymbolDisplayFormat.ILVisualizationFormat);
            }
        }

        #endregion

        #region PDB Validation

        public string GetPdbXml(string source, CSharpCompilationOptions compilationOptions, string methodName = "", IEnumerable<MetadataReference> references = null)
        {
            //Having a unique name here may be important. The infrastructure of the pdb to xml conversion
            //loads the assembly into the ReflectionOnlyLoadFrom context.
            //So it's probably a good idea to have a new name for each assembly.
            var compilation = CreateCompilationWithMscorlibAndSystemCore(source,
                references,
                assemblyName: GetUniqueName(),
                compOptions: compilationOptions);
            
            compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Verify();

            return GetPdbXml(compilation, methodName);
        }

        #endregion
    }
}
