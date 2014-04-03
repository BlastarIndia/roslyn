// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Instrumentation;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    /// <summary>
    /// The compilation object is an immutable representation of a single invocation of the
    /// compiler. Although immutable, a compilation is also on-demand, and will realize and cache
    /// data as necessary. A compilation can produce a new compilation from existing compilation
    /// with the application of small deltas. In many cases, it is more efficient than creating a
    /// new compilation from scratch, as the new compilation can reuse information from the old
    /// compilation.
    /// </summary>
    public sealed partial class CSharpCompilation : Compilation
    {
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        //
        // Changes to the public interface of this class should remain synchronized with the VB
        // version. Do not make any changes to the public interface without making the corresponding
        // change to the VB version.
        //
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! 

        private readonly CSharpCompilationOptions options;
        private readonly ImmutableArray<SyntaxTree> syntaxTrees; // In ordinal order.
        private readonly ImmutableDictionary<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>> rootNamespaces;
        private readonly DeclarationTable declarationTable;
        private readonly Lazy<Imports> globalImports;
        private readonly Lazy<AliasSymbol> globalNamespaceAlias;  // alias symbol used to resolve "global::".
        private readonly Lazy<ImplicitNamedTypeSymbol> scriptClass;
        private readonly CSharpCompilation previousSubmission;

        // All imports (using directives and extern aliases) in syntax trees in this compilation.
        // NOTE: We need to de-dup since the Imports objects that populate the list may be GC'd
        // and re-created.
        private ConcurrentSet<ImportInfo> lazyImportInfos;

        // Cache the CLS diagnostics for the whole compilation so they aren't computed repeatedly.
        // NOTE: Presently, we do not cache the per-tree diagnostics.
        private ImmutableArray<Diagnostic> lazyClsComplianceDiagnostics;

        /// <summary>
        /// Used for test purposes only to immulate missing members.
        /// </summary>
        private SmallDictionary<int, bool> lazyMakeMemberMissingMap;

        private Conversions conversions;
        internal Conversions Conversions
        {
            get
            {
                if (conversions == null)
                {
                    Interlocked.CompareExchange(ref conversions, new BuckStopsHereBinder(this).Conversions, null);
                }

                return conversions;
            }
        }

        /// <summary>
        /// Manages anonymous types declared in this compilation. Unifies types that are structurally equivalent.
        /// </summary>
        private AnonymousTypeManager anonymousTypeManager;

        private NamespaceSymbol lazyGlobalNamespace;

        internal readonly BuiltInOperators builtInOperators;

        /// <summary>
        /// The <see cref="SourceAssemblySymbol"/> for this compilation. Do not access directly, use Assembly property
        /// instead. This field is lazily initialized by ReferenceManager, ReferenceManager.CacheLockObject must be locked
        /// while ReferenceManager "calculates" the value and assigns it, several threads must not perform duplicate
        /// "calculation" simultaneously.
        /// </summary>
        private SourceAssemblySymbol lazyAssemblySymbol;

        /// <summary>
        /// Holds onto data related to reference binding.
        /// The manager is shared among multiple compilations that we expect to have the same result of reference binding.
        /// In most cases this can be determined without performing the binding. If the compilation however contains a circular 
        /// metadata reference (a metadata reference that refers back to the compilation) we need to avoid sharing of the binding results.
        /// We do so by creating a new reference manager for such compilation. 
        /// </summary>
        private ReferenceManager referenceManager;

        /// <summary>
        /// Contains the main method of this assembly, if there is one.
        /// </summary>
        private EntryPoint lazyEntryPoint;

        /// <summary>
        /// The set of trees for which a <see cref="CompilationEvent.CompilationCompleted"/> has been added to the queue.
        /// </summary>
        private HashSet<SyntaxTree> lazyCompilationUnitCompletedTrees;

        public override string Language
        {
            get
            {
                return LanguageNames.CSharp;
            }
        }

        public override bool IsCaseSensitive
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// The options the compilation was created with. 
        /// </summary>
        public new CSharpCompilationOptions Options
        {
            get
            {
                return options;
            }
        }

        internal AnonymousTypeManager AnonymousTypeManager
        {
            get
            {
                return anonymousTypeManager;
            }
        }

        public override INamedTypeSymbol CreateErrorTypeSymbol(INamespaceOrTypeSymbol container, string name, int arity)
        {
            return new ExtendedErrorTypeSymbol((NamespaceOrTypeSymbol)container, name, arity, null);
        }

        Dictionary<string, string> Features;
        internal string Feature(string p)
        {
            if (this.Features == null)
            {
                var set = new Dictionary<string, string>();

                if (options.Features != null)
                {
                    foreach (var s in options.Features)
                    {
                        int colon = s.IndexOf(':');
                        if (colon > 0)
                        {
                            string name = s.Substring(0, colon);
                            string value = s.Substring(colon + 1);
                            set.Add(name, value);
                }
                        else
                        {
                            set.Add(s, "true");
                        }
                    }
                }

                Interlocked.CompareExchange(ref this.Features, set, null);
            }

            string v;
            return this.Features.TryGetValue(p, out v) ? v : null;
        }

        #region Constructors and Factories

        private static CSharpCompilationOptions DefaultOptions = new CSharpCompilationOptions(OutputKind.ConsoleApplication);
        private static CSharpCompilationOptions DefaultSubmissionOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        /// <summary>
        /// Creates a new compilation from scratch. Methods such as AddSyntaxTrees or AddReferences
        /// on the returned object will allow to continue building up the Compilation incrementally.
        /// </summary>
        /// <param name="assemblyName">Simple assembly name.</param>
        /// <param name="syntaxTrees">The syntax trees with the source code for the new compilation.</param>
        /// <param name="references">The references for the new compilation.</param>
        /// <param name="options">The compiler options to use.</param>
        /// <returns>A new compilation.</returns>
        public static CSharpCompilation Create(
            string assemblyName,
            IEnumerable<SyntaxTree> syntaxTrees = null,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions options = null)
        {
            return Create(
                assemblyName, 
                options ?? DefaultOptions,
                (syntaxTrees != null) ? syntaxTrees.Cast<SyntaxTree>() : null,
                references, 
                previousSubmission: null, 
                returnType: null, 
                hostObjectType: null, 
                isSubmission: false);
        }

        /// <summary>
        /// Creates a new compilation that can be used in scripting.
        /// </summary>
        public static CSharpCompilation CreateSubmission(
            string assemblyName,
            SyntaxTree syntaxTree = null,
            IEnumerable<MetadataReference> references = null,
            CSharpCompilationOptions options = null,
            Compilation previousSubmission = null,
            Type returnType = null,
            Type hostObjectType = null)
        {
            CheckSubmissionOptions(options);

            return Create(
                assemblyName,
                options ?? DefaultSubmissionOptions,
                (syntaxTree != null) ? new[] { syntaxTree } : SpecializedCollections.EmptyEnumerable<SyntaxTree>(),
                references,
                (CSharpCompilation)previousSubmission,
                returnType,
                hostObjectType,
                isSubmission: true);
        }

        private static CSharpCompilation Create(
            string assemblyName,
            CSharpCompilationOptions options,
            IEnumerable<SyntaxTree> syntaxTrees,
            IEnumerable<MetadataReference> references,
            CSharpCompilation previousSubmission,
            Type returnType,
            Type hostObjectType,
            bool isSubmission)
        {
            Debug.Assert(options != null);
            CheckAssemblyName(assemblyName);

            var validatedReferences = ValidateReferences<CSharpCompilationReference>(references);
            ValidateSubmissionParameters(previousSubmission, returnType, ref hostObjectType);

            var compilation = new CSharpCompilation(
                assemblyName,
                options,
                validatedReferences,
                ImmutableArray<SyntaxTree>.Empty,
                ImmutableDictionary.Create<SyntaxTree, int>(ReferenceEqualityComparer.Instance),
                ImmutableDictionary.Create<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>>(),
                DeclarationTable.Empty,
                previousSubmission,
                returnType,
                hostObjectType,
                isSubmission,
                referenceManager: null,
                reuseReferenceManager: false);

            if (syntaxTrees != null)
            {
                compilation = compilation.AddSyntaxTrees(syntaxTrees);
            }

            Debug.Assert((object)compilation.lazyAssemblySymbol == null);
            return compilation;
        }

        private CSharpCompilation(
            string assemblyName,
            CSharpCompilationOptions options,
            ImmutableArray<MetadataReference> references,
            ImmutableArray<SyntaxTree> syntaxTrees,
            ImmutableDictionary<SyntaxTree, int> syntaxTreeOrdinalMap,
            ImmutableDictionary<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>> rootNamespaces,
            DeclarationTable declarationTable,
            CSharpCompilation previousSubmission,
            Type submissionReturnType,
            Type hostObjectType,
            bool isSubmission,
            ReferenceManager referenceManager,
            bool reuseReferenceManager,
            AsyncQueue<CompilationEvent> eventQueue = null)
            : base(assemblyName, references, submissionReturnType, hostObjectType, isSubmission, syntaxTreeOrdinalMap, eventQueue)
        {
            using (Logger.LogBlock(FunctionId.CSharp_Compilation_Create, message: assemblyName))
            {
                this.wellKnownMemberSignatureComparer = new WellKnownMembersSignatureComparer(this);
                this.options = options;
                this.syntaxTrees = syntaxTrees;

                this.rootNamespaces = rootNamespaces;
                this.declarationTable = declarationTable;

                Debug.Assert(syntaxTrees.All(tree => syntaxTrees[syntaxTreeOrdinalMap[tree]] == tree));
                Debug.Assert(syntaxTrees.SetEquals(rootNamespaces.Keys.AsImmutable(), EqualityComparer<SyntaxTree>.Default));

                this.builtInOperators = new BuiltInOperators(this);
                this.scriptClass = new Lazy<ImplicitNamedTypeSymbol>(BindScriptClass);
                this.globalImports = new Lazy<Imports>(BindGlobalUsings);
                this.globalNamespaceAlias = new Lazy<AliasSymbol>(CreateGlobalNamespaceAlias);
                this.anonymousTypeManager = new AnonymousTypeManager(this);

                if (isSubmission)
                {
                    Debug.Assert(previousSubmission == null || previousSubmission.HostObjectType == hostObjectType);

                    this.previousSubmission = previousSubmission;
                }
                else
                {
                    Debug.Assert(previousSubmission == null && submissionReturnType == null && hostObjectType == null);
                }

                if (reuseReferenceManager)
                {
                    referenceManager.AssertCanReuseForCompilation(this);
                    this.referenceManager = referenceManager;
                }
                else
                {
                    this.referenceManager = new ReferenceManager(
                        MakeSourceAssemblySimpleName(), 
                        options.AssemblyIdentityComparer,
                        (referenceManager != null) ? referenceManager.ObservedMetadata : null);
                }

                Debug.Assert((object)this.lazyAssemblySymbol == null);
                if (EventQueue != null) EventQueue.Enqueue(new CompilationEvent.CompilationStarted(this));
            }
        }

        /// <summary>
        /// Create a duplicate of this compilation with different symbol instances.
        /// </summary>
        public new CSharpCompilation Clone()
        {
            return new CSharpCompilation(
                this.AssemblyName,
                this.options,
                this.ExternalReferences,
                this.SyntaxTrees,
                this.syntaxTreeOrdinalMap,
                this.rootNamespaces,
                this.declarationTable,
                this.previousSubmission,
                this.SubmissionReturnType,
                this.HostObjectType,
                this.IsSubmission,
                this.referenceManager,
                reuseReferenceManager: true);
        }

        private CSharpCompilation UpdateSyntaxTrees(
            ImmutableArray<SyntaxTree> syntaxTrees,
            ImmutableDictionary<SyntaxTree, int> syntaxTreeOrdinalMap,
            ImmutableDictionary<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>> rootNamespaces,
            DeclarationTable declarationTable,
            bool referenceDirectivesChanged)
        {
            return new CSharpCompilation(
                this.AssemblyName,
                this.options,
                this.ExternalReferences,
                syntaxTrees,
                syntaxTreeOrdinalMap,
                rootNamespaces,
                declarationTable,
                this.previousSubmission,
                this.SubmissionReturnType,
                this.HostObjectType,
                this.IsSubmission,
                this.referenceManager,
                reuseReferenceManager: !referenceDirectivesChanged);
        }

        /// <summary>
        /// Creates a new compilation with the specified name.
        /// </summary>
        public new CSharpCompilation WithAssemblyName(string assemblyName)
        {
            CheckAssemblyName(assemblyName);

            // Can't reuse references since the source assembly name changed and the referenced symbols might 
            // have internals-visible-to relationship with this compilation or they might had a circular reference 
            // to this compilation.

            return new CSharpCompilation(
                assemblyName,
                this.options,
                this.ExternalReferences,
                this.SyntaxTrees,
                this.syntaxTreeOrdinalMap,
                this.rootNamespaces,
                this.declarationTable,
                this.previousSubmission,
                this.SubmissionReturnType,
                this.HostObjectType,
                this.IsSubmission,
                this.referenceManager,
                reuseReferenceManager: assemblyName == this.AssemblyName);
        }

        /// <summary>
        /// Creates a new compilation with the specified references.
        /// </summary>
        /// <remarks>
        /// The new <see cref="CSharpCompilation"/> will query the given <see cref="MetadataReference"/> for the underlying 
        /// metadata as soon as the are needed. 
        /// 
        /// The new compilation uses whatever metadata is currently being provided by the <see cref="MetadataReference"/>.
        /// E.g. if the current compilation references a metadata file that has changed since the creation of the compilation
        /// the new compilation is going to use the updated version, while the current compilation will be using the previous (it doesn't change).
        /// </remarks>
        public new CSharpCompilation WithReferences(IEnumerable<MetadataReference> references)
        {
            // References might have changed, don't reuse reference manager.
            // Don't even reuse observed metadata - let the manager query for the metadata again.

            return new CSharpCompilation(
                this.AssemblyName,
                this.options,
                ValidateReferences<CSharpCompilationReference>(references),
                this.SyntaxTrees,
                this.syntaxTreeOrdinalMap,
                this.rootNamespaces,
                this.declarationTable,
                this.previousSubmission,
                this.SubmissionReturnType,
                this.HostObjectType,
                this.IsSubmission,
                referenceManager: null,
                reuseReferenceManager: false);
        }

        /// <summary>
        /// Creates a new compilation with the specified references.
        /// </summary>
        public new CSharpCompilation WithReferences(params MetadataReference[] references)
        {
            return this.WithReferences((IEnumerable<MetadataReference>)references);
        }

        /// <summary>
        /// Creates a new compilation with the specified compilation options.
        /// </summary>
        public CSharpCompilation WithOptions(CSharpCompilationOptions options)
        {
            // Checks to see if the new options support reusing the reference manager
            bool reuseReferenceManager = this.Options.CanReuseCompilationReferenceManager(options);

            return new CSharpCompilation(
                this.AssemblyName,
                options,
                this.ExternalReferences,
                this.syntaxTrees,
                this.syntaxTreeOrdinalMap,
                this.rootNamespaces,
                this.declarationTable,
                this.previousSubmission,
                this.SubmissionReturnType,
                this.HostObjectType,
                this.IsSubmission,
                this.referenceManager,
                reuseReferenceManager);
        }

        /// <summary>
        /// Returns a new compilation with the given compilation set as the previous submission.
        /// </summary>
        internal CSharpCompilation WithPreviousSubmission(CSharpCompilation newPreviousSubmission)
        {
            if (!this.IsSubmission)
            {
                throw new NotSupportedException("Can't have a previousSubmission when not a submission");
            }

            // Reference binding doesn't depend on previous submission so we can reuse it.

            return new CSharpCompilation(
                this.AssemblyName,
                this.options,
                this.ExternalReferences,
                this.SyntaxTrees,
                this.syntaxTreeOrdinalMap,
                this.rootNamespaces,
                this.declarationTable,
                newPreviousSubmission,
                this.SubmissionReturnType,
                this.HostObjectType,
                this.IsSubmission,
                this.referenceManager,
                reuseReferenceManager: true);
        }

        /// <summary>
        /// Returns a new compilation with a given event queue.
        /// </summary>
        internal CSharpCompilation WithEventQueue(AsyncQueue<CompilationEvent> eventQueue)
        {
            return new CSharpCompilation(
                this.AssemblyName,
                this.options,
                this.ExternalReferences,
                this.SyntaxTrees,
                this.syntaxTreeOrdinalMap,
                this.rootNamespaces,
                this.declarationTable,
                this.previousSubmission,
                this.SubmissionReturnType,
                this.HostObjectType,
                this.IsSubmission,
                this.referenceManager,
                reuseReferenceManager: true,
                eventQueue: eventQueue);
        }

        #endregion

        #region Submission

        internal new CSharpCompilation PreviousSubmission
        {
            get { return previousSubmission; }
        }

        // TODO (tomat): consider moving this method to SemanticModel

        /// <summary>
        /// Returns the type of the submission return value. 
        /// </summary>
        /// <returns>
        /// The type of the last expression of the submission. 
        /// Null if the type of the last expression is unknown (null).
        /// Void type if the type of the last expression statement is void or 
        /// the submission ends with a declaration or statement that is not an expression statement.
        /// </returns>
        /// <remarks>
        /// Note that the return type is System.Void for both compilations "System.Console.WriteLine();" and "System.Console.WriteLine()", 
        /// and <paramref name="hasValue"/> is <c>False</c> for the former and <c>True</c> for the latter.
        /// </remarks>
        /// <param name="hasValue">True if the submission has value, i.e. if it ends with a statement that is an expression statement.</param>
        /// <exception cref="InvalidOperationException">The compilation doesn't represent a submission (<see cref="P:IsSubmission"/> return false).</exception>
        internal new TypeSymbol GetSubmissionResultType(out bool hasValue)
        {
            if (!IsSubmission)
            {
                throw new InvalidOperationException(CSharpResources.ThisCompilationNotInteractive);
            }

            hasValue = false;

            // submission can be empty or comprise of a script file
            SyntaxTree tree = SyntaxTrees.SingleOrDefault();
            if (tree == null || tree.Options.Kind != SourceCodeKind.Interactive)
            {
                return GetSpecialType(SpecialType.System_Void);
            }

            var lastStatement = (GlobalStatementSyntax)tree.GetCompilationUnitRoot().Members.LastOrDefault(decl => decl.Kind == SyntaxKind.GlobalStatement);
            if (lastStatement == null || lastStatement.Statement.Kind != SyntaxKind.ExpressionStatement)
            {
                return GetSpecialType(SpecialType.System_Void);
            }

            var expressionStatement = (ExpressionStatementSyntax)lastStatement.Statement;
            if (!expressionStatement.SemicolonToken.IsMissing)
            {
                return GetSpecialType(SpecialType.System_Void);
            }

            var model = GetSemanticModel(tree);
            hasValue = true;
            var expression = expressionStatement.Expression;
            var info = model.GetTypeInfo(expression);
            return (TypeSymbol)info.ConvertedType;
        }

        #endregion

        #region Syntax Trees (maintain an ordered list)

        /// <summary>
        /// The syntax trees (parsed from source code) that this compilation was created with.
        /// </summary>
        public new ImmutableArray<SyntaxTree> SyntaxTrees
        {
            get { return this.syntaxTrees; }
        }

        /// <summary>
        /// Returns true if this compilation contains the specified tree.  False otherwise.
        /// </summary>
        public new bool ContainsSyntaxTree(SyntaxTree syntaxTree)
        {
            var cstree = syntaxTree as SyntaxTree;
            return cstree != null && rootNamespaces.ContainsKey((cstree));
        }

        /// <summary>
        /// Creates a new compilation with additional syntax trees.
        /// </summary>
        public new CSharpCompilation AddSyntaxTrees(params SyntaxTree[] trees)
        {
            return AddSyntaxTrees((IEnumerable<SyntaxTree>)trees);
        }

        /// <summary>
        /// Creates a new compilation with additional syntax trees.
        /// </summary>
        public new CSharpCompilation AddSyntaxTrees(IEnumerable<SyntaxTree> trees)
        {
            using (Logger.LogBlock(FunctionId.CSharp_Compilation_AddSyntaxTrees, message: this.AssemblyName))
            {
                if (trees == null)
                {
                    throw new ArgumentNullException("trees");
                }

                if (trees.IsEmpty())
                {
                    return this;
                }

                // We're using a try-finally for this builder because there's a test that 
                // specifically checks for one or more of the argument exceptions below
                // and we don't want to see console spew (even though we don't generally
                // care about pool "leaks" in exceptional cases).  Alternatively, we
                // could create a new ArrayBuilder.
                var builder = ArrayBuilder<SyntaxTree>.GetInstance();
                try
                {
                    builder.AddRange(this.SyntaxTrees);

                    bool referenceDirectivesChanged = false;
                    var oldTreeCount = this.SyntaxTrees.Length;
                    var ordinalMap = this.syntaxTreeOrdinalMap;
                    var declMap = rootNamespaces;
                    var declTable = declarationTable;
                    int i = 0;
                    foreach (var tree in trees.Cast<CSharpSyntaxTree>())
                    {
                        if (tree == null)
                        {
                            throw new ArgumentNullException("trees[" + i + "]");
                        }

                        if (!tree.HasCompilationUnitRoot)
                        {
                            throw new ArgumentException(String.Format(CSharpResources.TreeMustHaveARootNodeWith, i));
                        }

                        if (declMap.ContainsKey(tree))
                        {
                            throw new ArgumentException(CSharpResources.SyntaxTreeAlreadyPresent, String.Format(CSharpResources.Trees0, i));
                        }

                        if (IsSubmission && tree.Options.Kind == SourceCodeKind.Regular)
                        {
                            throw new ArgumentException(CSharpResources.SubmissionCanOnlyInclude, String.Format(CSharpResources.Trees0, i));
                        }

                        AddSyntaxTreeToDeclarationMapAndTable(tree, options, IsSubmission, ref declMap, ref declTable, ref referenceDirectivesChanged);
                        builder.Add(tree);
                        ordinalMap = ordinalMap.Add(tree, oldTreeCount + i);

                        i++;
                    }

                    if (IsSubmission && declMap.Count > 1)
                    {
                        throw new ArgumentException(CSharpResources.SubmissionCanHaveAtMostOne, "trees");
                    }

                    return UpdateSyntaxTrees(builder.ToImmutable(), ordinalMap, declMap, declTable, referenceDirectivesChanged);
                }
                finally
                {
                    builder.Free();
                }
            }
        }

        private static void AddSyntaxTreeToDeclarationMapAndTable(
            SyntaxTree tree,
            CSharpCompilationOptions options,
            bool isSubmission,
            ref ImmutableDictionary<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>> declMap,
            ref DeclarationTable declTable,
            ref bool referenceDirectivesChanged)
        {
            var lazyRoot = new Lazy<RootSingleNamespaceDeclaration>(() => DeclarationTreeBuilder.ForTree(tree, options.ScriptClassName ?? "", isSubmission));
            declMap = declMap.SetItem(tree, lazyRoot);
            declTable = declTable.AddRootDeclaration(lazyRoot);
            referenceDirectivesChanged = referenceDirectivesChanged || tree.HasReferenceDirectives();
        }

        /// <summary>
        /// Creates a new compilation without the specified syntax trees. Preserves metadata info for use with trees
        /// added later. 
        /// </summary>
        public new CSharpCompilation RemoveSyntaxTrees(params SyntaxTree[] trees)
        {
            return RemoveSyntaxTrees((IEnumerable<SyntaxTree>)trees);
        }

        /// <summary>
        /// Creates a new compilation without the specified syntax trees. Preserves metadata info for use with trees
        /// added later. 
        /// </summary>
        public new CSharpCompilation RemoveSyntaxTrees(IEnumerable<SyntaxTree> trees)
        {
            using (Logger.LogBlock(FunctionId.CSharp_Compilation_RemoveSyntaxTrees, message: this.AssemblyName))
            {
                if (trees == null)
                {
                    throw new ArgumentNullException("trees");
                }

                if (trees.IsEmpty())
                {
                    return this;
                }

                bool referenceDirectivesChanged = false;
                var removeSet = new HashSet<SyntaxTree>();
                var declMap = rootNamespaces;
                var declTable = declarationTable;
                foreach (var tree in trees.Cast<CSharpSyntaxTree>())
                {
                    RemoveSyntaxTreeFromDeclarationMapAndTable(tree, ref declMap, ref declTable, ref referenceDirectivesChanged);
                    removeSet.Add(tree);
                }

                Debug.Assert(!removeSet.IsEmpty());

                // We're going to have to revise the ordinals of all
                // trees after the first one removed, so just build
                // a new map.
                var ordinalMap = ImmutableDictionary.Create<SyntaxTree, int>();
                var builder = ArrayBuilder<SyntaxTree>.GetInstance();
                int i = 0;
                foreach (var tree in this.SyntaxTrees)
                {
                    if (!removeSet.Contains(tree))
                    {
                        builder.Add(tree);
                        ordinalMap = ordinalMap.Add(tree, i++);
                    }
                }

                return UpdateSyntaxTrees(builder.ToImmutableAndFree(), ordinalMap, declMap, declTable, referenceDirectivesChanged);
            }
        }

        private static void RemoveSyntaxTreeFromDeclarationMapAndTable(
            SyntaxTree tree,
            ref ImmutableDictionary<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>> declMap,
            ref DeclarationTable declTable,
            ref bool referenceDirectivesChanged)
        {
            Lazy<RootSingleNamespaceDeclaration> lazyRoot;
            if (!declMap.TryGetValue(tree, out lazyRoot))
            {
                throw new ArgumentException(string.Format(CSharpResources.SyntaxTreeNotFoundTo, tree), "trees");
            }

            declTable = declTable.RemoveRootDeclaration(lazyRoot);
            declMap = declMap.Remove(tree);
            referenceDirectivesChanged = referenceDirectivesChanged || tree.HasReferenceDirectives();
        }

        /// <summary>
        /// Creates a new compilation without any syntax trees. Preserves metadata info
        /// from this compilation for use with trees added later. 
        /// </summary>
        public new CSharpCompilation RemoveAllSyntaxTrees()
        {
            return UpdateSyntaxTrees(
                ImmutableArray<SyntaxTree>.Empty,
                ImmutableDictionary.Create<SyntaxTree, int>(),
                ImmutableDictionary.Create<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>>(),
                DeclarationTable.Empty,
                referenceDirectivesChanged: declarationTable.ReferenceDirectives.Any());
        }

        /// <summary>
        /// Creates a new compilation without the old tree but with the new tree.
        /// </summary>
        public new CSharpCompilation ReplaceSyntaxTree(SyntaxTree oldTree, SyntaxTree newTree)
        {
            using (Logger.LogBlock(FunctionId.CSharp_Compilation_ReplaceSyntaxTree, message: this.AssemblyName))
            {
                // this is just to force a cast exception
                oldTree = (CSharpSyntaxTree)oldTree;
                newTree = (CSharpSyntaxTree)newTree;

                if (oldTree == null)
                {
                    throw new ArgumentNullException("oldTree");
                }

                if (newTree == null)
                {
                    return this.RemoveSyntaxTrees(oldTree);
                }
                else if (newTree == oldTree)
                {
                    return this;
                }

                if (!newTree.HasCompilationUnitRoot)
                {
                    throw new ArgumentException(CSharpResources.TreeMustHaveARootNodeWith, "newTree");
                }

                var declMap = rootNamespaces;
                var declTable = declarationTable;
                bool referenceDirectivesChanged = false;

                // TODO(tomat): Consider comparing #r's of the old and the new tree. If they are exactly the same we could still reuse.
                // This could be a perf win when editing a script file in the IDE. The services create a new compilation every keystroke 
                // that replaces the tree with a new one.

                RemoveSyntaxTreeFromDeclarationMapAndTable(oldTree, ref declMap, ref declTable, ref referenceDirectivesChanged);
                AddSyntaxTreeToDeclarationMapAndTable(newTree, options, this.IsSubmission, ref declMap, ref declTable, ref referenceDirectivesChanged);

                var ordinalMap = this.syntaxTreeOrdinalMap;

                Debug.Assert(ordinalMap.ContainsKey(oldTree)); // Checked by RemoveSyntaxTreeFromDeclarationMapAndTable
                var oldOrdinal = ordinalMap[oldTree];

                var newArray = this.SyntaxTrees.SetItem(oldOrdinal, newTree);

                // CONSIDER: should this be an operation on ImmutableDictionary?
                ordinalMap = ordinalMap.Remove(oldTree);
                ordinalMap = ordinalMap.SetItem(newTree, oldOrdinal);

                return UpdateSyntaxTrees(newArray, ordinalMap, declMap, declTable, referenceDirectivesChanged);
            }
        }

        #endregion

        #region References

        internal override CommonReferenceManager CommonGetBoundReferenceManager()
        {
            return GetBoundReferenceManager();
        }

        internal new ReferenceManager GetBoundReferenceManager()
        {
            if ((object)lazyAssemblySymbol == null)
            {
                referenceManager.CreateSourceAssemblyForCompilation(this);
                Debug.Assert((object)lazyAssemblySymbol != null);
            }

            // referenceManager can only be accessed after we initialized the lazyAssemblySymbol.
            // In fact, initialization of the assembly symbol might change the reference manager.
            return referenceManager;
        }

        // for testing only:
        internal bool ReferenceManagerEquals(CSharpCompilation other)
        {
            return ReferenceEquals(this.referenceManager, other.referenceManager);
        }

        public override ImmutableArray<MetadataReference> DirectiveReferences
        {
            get
            {
                return GetBoundReferenceManager().DirectiveReferences;
            }
        }

        internal override IDictionary<string, MetadataReference> ReferenceDirectiveMap
        {
            get
            {
                return GetBoundReferenceManager().ReferenceDirectiveMap;
            }
        }

        // for testing purposes
        internal IEnumerable<string> ExternAliases
        {
            get
            {
                return GetBoundReferenceManager().ExternAliases;
            }
        }

        /// <summary>
        /// Gets the <see cref="AssemblySymbol"/> or <see cref="ModuleSymbol"/> for a metadata reference used to create this compilation.
        /// </summary>
        /// <returns><see cref="AssemblySymbol"/> or <see cref="ModuleSymbol"/> corresponding to the given reference or null if there is none.</returns>
        /// <remarks>
        /// Uses object identity when comparing two references. 
        /// </remarks>
        internal new Symbol GetAssemblyOrModuleSymbol(MetadataReference reference)
        {
            if (reference == null)
            {
                throw new ArgumentNullException("reference");
            }

            if (reference.Properties.Kind == MetadataImageKind.Assembly)
            {
                return GetBoundReferenceManager().GetReferencedAssemblySymbol(reference);
            }
            else
            {
                Debug.Assert(reference.Properties.Kind == MetadataImageKind.Module);
                int index = GetBoundReferenceManager().GetReferencedModuleIndex(reference);
                return index < 0 ? null : this.Assembly.Modules[index];
            }
        }

        public override IEnumerable<AssemblyIdentity> ReferencedAssemblyNames
        {
            get
            {
                return Assembly.Modules.SelectMany(module => module.GetReferencedAssemblies());
            }
        }

        /// <summary>
        /// All reference directives used in this compilation.
        /// </summary>
        internal override IEnumerable<ReferenceDirective> ReferenceDirectives
        {
            get { return declarationTable.ReferenceDirectives; }
        }

        /// <summary>
        /// Returns a metadata reference that a given #r resolves to.
        /// </summary>
        /// <param name="directive">#r directive.</param>
        /// <returns>Metadata reference the specified directive resolves to.</returns>
        public MetadataReference GetDirectiveReference(ReferenceDirectiveTriviaSyntax directive)
        {
            return ReferenceDirectiveMap[directive.File.ValueText];
        }

        /// <summary>
        /// Creates a new compilation with additional metadata references.
        /// </summary>
        public new CSharpCompilation AddReferences(params MetadataReference[] references)
        {
            return (CSharpCompilation)base.AddReferences(references);
        }

        /// <summary>
        /// Creates a new compilation with additional metadata references.
        /// </summary>
        public new CSharpCompilation AddReferences(IEnumerable<MetadataReference> references)
        {
            return (CSharpCompilation)base.AddReferences(references);
        }

        /// <summary>
        /// Creates a new compilation without the specified metadata references.
        /// </summary>
        public new CSharpCompilation RemoveReferences(params MetadataReference[] references)
        {
            return (CSharpCompilation)base.RemoveReferences(references);
        }

        /// <summary>
        /// Creates a new compilation without the specified metadata references.
        /// </summary>
        public new CSharpCompilation RemoveReferences(IEnumerable<MetadataReference> references)
        {
            return (CSharpCompilation)base.RemoveReferences(references);
        }

        /// <summary>
        /// Creates a new compilation without any metadata references
        /// </summary>
        public new CSharpCompilation RemoveAllReferences()
        {
            return (CSharpCompilation)base.RemoveAllReferences();
        }

        /// <summary>
        /// Creates a new compilation with an old metadata reference replaced with a new metadata reference.
        /// </summary>
        public new CSharpCompilation ReplaceReference(MetadataReference oldReference, MetadataReference newReference)
        {
            return (CSharpCompilation)base.ReplaceReference(oldReference, newReference);
        }

        public override CompilationReference ToMetadataReference(ImmutableArray<string> aliases = default(ImmutableArray<string>), bool embedInteropTypes = false)
        {
            return new CSharpCompilationReference(this, aliases, embedInteropTypes);
        }

        // Get all modules in this compilation, including the source module, added modules, and all
        // modules of referenced assemblies that do not come from an assembly with an extern alias.
        // Metadata imported from aliased assemblies is not visible at the source level except through 
        // the use of an extern alias directive. So exclude them from this list which is used to construct
        // the global namespace.
        private IEnumerable<ModuleSymbol> GetAllUnaliasedModules()
        {
            // Get all assemblies in this compilation, including the source assembly and all referenced assemblies.
            ArrayBuilder<ModuleSymbol> modules = new ArrayBuilder<ModuleSymbol>();

            // NOTE: This includes referenced modules - they count as modules of the compilation assembly.
            modules.AddRange(this.Assembly.Modules);

            foreach (var pair in GetBoundReferenceManager().ReferencedAssembliesMap)
            {
                MetadataReference reference = pair.Key;
                ReferenceManager.ReferencedAssembly referencedAssembly = pair.Value;
                if (reference.Properties.Kind == MetadataImageKind.Assembly) // Already handled modules above.
                {
                    if (referencedAssembly.DeclarationsAccessibleWithoutAlias())
                    {
                        modules.AddRange(referencedAssembly.Symbol.Modules);
                    }
                }
            }

            return modules;
        }

        /// <summary>
        /// Gets the <see cref="MetadataReference"/> that corresponds to the assembly symbol. 
        /// </summary>
        public new MetadataReference GetMetadataReference(IAssemblySymbol assemblySymbol)
        {
            return this.GetBoundReferenceManager().ReferencedAssembliesMap.Where(kvp => object.ReferenceEquals(kvp.Value.Symbol, assemblySymbol)).Select(kvp => kvp.Key).FirstOrDefault();
        }

        #endregion

        #region Symbols

        /// <summary>
        /// The AssemblySymbol that represents the assembly being created.
        /// </summary>
        internal SourceAssemblySymbol SourceAssembly
        {
            get
            {
                GetBoundReferenceManager();
                return lazyAssemblySymbol;
            }
        }

        /// <summary>
        /// The AssemblySymbol that represents the assembly being created.
        /// </summary>
        internal new AssemblySymbol Assembly
        {
            get
            {
                return SourceAssembly;
            }
        }

        /// <summary>
        /// Get a ModuleSymbol that refers to the module being created by compiling all of the code.
        /// By getting the GlobalNamespace property of that module, all of the namespaces and types
        /// defined in source code can be obtained.
        /// </summary>
        internal new ModuleSymbol SourceModule
        {
            get
            {
                return Assembly.Modules[0];
            }
        }

        /// <summary>
        /// Gets the root namespace that contains all namespaces and types defined in source code or in 
        /// referenced metadata, merged into a single namespace hierarchy.
        /// </summary>
        internal new NamespaceSymbol GlobalNamespace
        {
            get
            {
                if ((object)lazyGlobalNamespace == null)
                {
                    using (Logger.LogBlock(FunctionId.CSharp_Compilation_GetGlobalNamespace, message: this.AssemblyName))
                    {
                        // Get the root namespace from each module, and merge them all together
                        HashSet<NamespaceSymbol> allGlobalNamespaces = new HashSet<NamespaceSymbol>();
                        foreach (ModuleSymbol module in GetAllUnaliasedModules())
                        {
                            allGlobalNamespaces.Add(module.GlobalNamespace);
                        }

                        var result = MergedNamespaceSymbol.Create(new NamespaceExtent(this),
                            null,
                            allGlobalNamespaces.AsImmutable());
                        Interlocked.CompareExchange(ref lazyGlobalNamespace, result, null);
                    }
                }

                return lazyGlobalNamespace;
            }
        }

        /// <summary>
        /// Given for the specified module or assembly namespace, gets the corresponding compilation
        /// namespace (merged namespace representation for all namespace declarations and references
        /// with contributions for the namespaceSymbol).  Can return null if no corresponding
        /// namespace can be bound in this compilation with the same name.
        /// </summary>
        internal new NamespaceSymbol GetCompilationNamespace(INamespaceSymbol namespaceSymbol)
        {
            if (namespaceSymbol is NamespaceSymbol &&
                namespaceSymbol.NamespaceKind == NamespaceKind.Compilation &&
                namespaceSymbol.ContainingCompilation == this)
            {
                return (NamespaceSymbol)namespaceSymbol;
            }

            var containingNamespace = namespaceSymbol.ContainingNamespace;
            if (containingNamespace == null)
            {
                return this.GlobalNamespace;
            }

            var current = GetCompilationNamespace(containingNamespace);
            if ((object)current != null)
            {
                return current.GetNestedNamespace(namespaceSymbol.Name);
            }

            return null;
        }

        private ConcurrentDictionary<string, NamespaceSymbol> externAliasTargets;

        internal bool GetExternAliasTarget(string aliasName, out NamespaceSymbol @namespace)
        {
            if (externAliasTargets == null)
            {
                Interlocked.CompareExchange(ref this.externAliasTargets, new ConcurrentDictionary<string, NamespaceSymbol>(), null);
            }
            else if (externAliasTargets.TryGetValue(aliasName, out @namespace))
            {
                return !(@namespace is MissingNamespaceSymbol);
            }

            ArrayBuilder<NamespaceSymbol> builder = null;
            foreach (var referencedAssembly in GetBoundReferenceManager().ReferencedAssembliesMap.Values)
            {
                if (referencedAssembly.Aliases.Contains(aliasName))
                {
                    builder = builder ?? ArrayBuilder<NamespaceSymbol>.GetInstance();
                    builder.Add(referencedAssembly.Symbol.GlobalNamespace);
                }
            }

            bool foundNamespace = builder != null;

            // We want to cache failures as well as successes so that subsequent incorrect extern aliases with the
            // same alias will have the same target.
            @namespace = foundNamespace
                ? MergedNamespaceSymbol.Create(new NamespaceExtent(this), namespacesToMerge: builder.ToImmutableAndFree(), containingNamespace: null, nameOpt: null)
                : new MissingNamespaceSymbol(new MissingModuleSymbol(new MissingAssemblySymbol(new AssemblyIdentity(System.Guid.NewGuid().ToString())), ordinal: -1));

            // Use GetOrAdd in case another thread beat us to the punch (i.e. should return the same object for the same alias, every time).
            @namespace = externAliasTargets.GetOrAdd(aliasName, @namespace);

            Debug.Assert(foundNamespace == !(@namespace is MissingNamespaceSymbol));

            return foundNamespace;
        }

        /// <summary>
        /// A symbol representing the implicit Script class. This is null if the class is not
        /// defined in the compilation.
        /// </summary>
        internal new NamedTypeSymbol ScriptClass
        {
            get { return scriptClass.Value; }
        }

        /// <summary>
        /// Resolves a symbol that represents script container (Script class). Uses the
        /// full name of the container class stored in <see cref="P:CompilationOptions.ScriptClassName"/> to find the symbol.
        /// </summary>
        /// <returns>The Script class symbol or null if it is not defined.</returns>
        private ImplicitNamedTypeSymbol BindScriptClass()
        {
            if (options.ScriptClassName == null || !options.ScriptClassName.IsValidClrTypeName())
            {
                return null;
            }

            var namespaceOrType = this.Assembly.GlobalNamespace.GetNamespaceOrTypeByQualifiedName(options.ScriptClassName.Split('.')).AsSingleton();
            return namespaceOrType as ImplicitNamedTypeSymbol;
        }

        internal Imports GlobalImports
        {
            get { return globalImports.Value; }
        }

        internal IEnumerable<NamespaceOrTypeSymbol> GlobalUsings
        {
            get
            {
                return GlobalImports.Usings.Select(u => u.NamespaceOrType);
            }
        }

        internal AliasSymbol GlobalNamespaceAlias
        {
            get
            {
                return globalNamespaceAlias.Value;
            }
        }

        /// <summary>
        /// Get the symbol for the predefined type from the COR Library referenced by this compilation.
        /// </summary>
        internal new NamedTypeSymbol GetSpecialType(SpecialType specialType)
        {
            if (specialType <= SpecialType.None || specialType > SpecialType.Count)
            {
                throw new ArgumentOutOfRangeException("specialType");
            }

            var result = Assembly.GetSpecialType(specialType);
            Debug.Assert(result.SpecialType == specialType);
            return result;
        }

        /// <summary>
        /// Get the symbol for the predefined type member from the COR Library referenced by this compilation.
        /// </summary>
        internal Symbol GetSpecialTypeMember(SpecialMember specialMember)
        {
            return Assembly.GetSpecialTypeMember(specialMember);
        }

        internal TypeSymbol GetTypeByReflectionType(Type type, DiagnosticBag diagnostics)
        {
            var result = Assembly.GetTypeByReflectionType(type, includeReferences: true);
            if ((object)result == null)
            {
                var errorType = new ExtendedErrorTypeSymbol(this, type.Name, 0, CreateReflectionTypeNotFoundError(type));
                diagnostics.Add(errorType.ErrorInfo, NoLocation.Singleton);
                result = errorType;
            }

            return result;
        }

        private static CSDiagnosticInfo CreateReflectionTypeNotFoundError(Type type)
        {
            // The type or namespace name '{0}' could not be found in the global namespace (are you missing an assembly reference?)
            return new CSDiagnosticInfo(
                ErrorCode.ERR_GlobalSingleTypeNameNotFound,
                new object[] { type.AssemblyQualifiedName },
                ImmutableArray<Symbol>.Empty,
                ImmutableArray<Location>.Empty
            );
        }

        // The type of host object model if available.
        private TypeSymbol lazyHostObjectTypeSymbol;

        internal TypeSymbol GetHostObjectTypeSymbol()
        {
            if (HostObjectType != null && (object)lazyHostObjectTypeSymbol == null)
            {
                TypeSymbol symbol = Assembly.GetTypeByReflectionType(HostObjectType, includeReferences: true);

                if ((object)symbol == null)
                {
                    MetadataTypeName mdName = MetadataTypeName.FromNamespaceAndTypeName(HostObjectType.Namespace ?? String.Empty,
                                                                                        HostObjectType.Name,
                                                                                        useCLSCompliantNameArityEncoding: true);

                    symbol = new MissingMetadataTypeSymbol.TopLevelWithCustomErrorInfo(
                        new MissingAssemblySymbol(AssemblyIdentity.FromAssemblyDefinition(HostObjectType.GetTypeInfo().Assembly)).Modules[0],
                        ref mdName,
                        CreateReflectionTypeNotFoundError(HostObjectType),
                        SpecialType.None);
                }

                Interlocked.CompareExchange(ref lazyHostObjectTypeSymbol, symbol, null);
            }

            return lazyHostObjectTypeSymbol;
        }

        internal TypeSymbol GetSubmissionReturnType()
        {
            if (IsSubmission && (object)ScriptClass != null)
            {
                // the second parameter of Script class instance constructor is the submission return value:
                return ((MethodSymbol)ScriptClass.GetMembers(WellKnownMemberNames.InstanceConstructorName)[0]).Parameters[1].Type;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the type within the compilation's assembly and all referenced assemblies (other than
        /// those that can only be referenced via an extern alias) using its canonical CLR metadata name.
        /// </summary>
        internal new NamedTypeSymbol GetTypeByMetadataName(string fullyQualifiedMetadataName)
        {
            return this.Assembly.GetTypeByMetadataName(fullyQualifiedMetadataName, includeReferences: true, isWellKnownType: false);
        }

        /// <summary>
        /// The TypeSymbol for the type 'dynamic' in this Compilation.
        /// </summary>
        internal new TypeSymbol DynamicType
        {
            get
            {
                return AssemblySymbol.DynamicType;
            }
        }

        /// <summary>
        /// The NamedTypeSymbol for the .NET System.Object type, which could have a TypeKind of
        /// Error if there was no COR Library in this Compilation.
        /// </summary>
        internal new NamedTypeSymbol ObjectType
        {
            get
            {
                return this.Assembly.ObjectType;
            }
        }

        internal bool DeclaresTheObjectClass
        {
            get
            {
                return SourceAssembly.DeclaresTheObjectClass;
            }
        }

        internal new MethodSymbol GetEntryPoint(CancellationToken cancellationToken)
        {
            EntryPoint entryPoint = GetEntryPointAndDiagnostics(cancellationToken);
            return entryPoint == null ? null : entryPoint.MethodSymbol;
        }

        internal EntryPoint GetEntryPointAndDiagnostics(CancellationToken cancellationToken)
        {
            if (!this.Options.OutputKind.IsApplication())
            {
                return null;
            }

            Debug.Assert(!this.IsSubmission);

            if (this.Options.MainTypeName != null && !this.Options.MainTypeName.IsValidClrTypeName())
            {
                Debug.Assert(!this.Options.Errors.IsDefaultOrEmpty);
                return new EntryPoint(null, ImmutableArray<Diagnostic>.Empty);
            }

            if (this.lazyEntryPoint == null)
            {
                MethodSymbol entryPoint;
                ImmutableArray<Diagnostic> diagnostics;
                FindEntryPoint(cancellationToken, out entryPoint, out diagnostics);

                Interlocked.CompareExchange(ref this.lazyEntryPoint, new EntryPoint(entryPoint, diagnostics), null);
            }

            return this.lazyEntryPoint;
        }

        private void FindEntryPoint(CancellationToken cancellationToken, out MethodSymbol entryPoint, out ImmutableArray<Diagnostic> sealedDiagnostics)
        {
            using (Logger.LogBlock(FunctionId.CSharp_Compilation_FindEntryPoint, message: this.AssemblyName, cancellationToken: cancellationToken))
            {
                DiagnosticBag diagnostics = DiagnosticBag.GetInstance();

                try
                {
                    entryPoint = null;

                    ArrayBuilder<MethodSymbol> entryPointCandidates;
                    NamedTypeSymbol mainType;

                    string mainTypeName = this.Options.MainTypeName;
                    NamespaceSymbol globalNamespace = this.SourceModule.GlobalNamespace;

                    if (mainTypeName != null)
                    {
                        // Global code is the entry point, ignore all other Mains.
                        // TODO: don't special case scripts (DevDiv #13119).
                        if ((object)this.ScriptClass != null)
                        {
                            // CONSIDER: we could use the symbol instead of just the name.
                            diagnostics.Add(ErrorCode.WRN_MainIgnored, NoLocation.Singleton, mainTypeName);
                            return;
                        }

                        var mainTypeOrNamespace = globalNamespace.GetNamespaceOrTypeByQualifiedName(mainTypeName.Split('.')).OfMinimalArity();
                        if ((object)mainTypeOrNamespace == null)
                        {
                            diagnostics.Add(ErrorCode.ERR_MainClassNotFound, NoLocation.Singleton, mainTypeName);
                            return;
                        }

                        mainType = mainTypeOrNamespace as NamedTypeSymbol;
                        if ((object)mainType == null || mainType.IsGenericType || (mainType.TypeKind != TypeKind.Class && mainType.TypeKind != TypeKind.Struct))
                        {
                            diagnostics.Add(ErrorCode.ERR_MainClassNotClass, mainTypeOrNamespace.Locations.First(), mainTypeOrNamespace);
                            return;
                        }

                        entryPointCandidates = ArrayBuilder<MethodSymbol>.GetInstance();
                        EntryPointCandidateFinder.FindCandidatesInSingleType(mainType, entryPointCandidates, cancellationToken);

                        // NOTE: Any return after this point must free entryPointCandidates.
                    }
                    else
                    {
                        mainType = null;

                        entryPointCandidates = ArrayBuilder<MethodSymbol>.GetInstance();
                        EntryPointCandidateFinder.FindCandidatesInNamespace(globalNamespace, entryPointCandidates, cancellationToken);

                        // NOTE: Any return after this point must free entryPointCandidates.

                        // global code is the entry point, ignore all other Mains:
                        if ((object)this.ScriptClass != null)
                        {
                            foreach (var main in entryPointCandidates)
                            {
                                diagnostics.Add(ErrorCode.WRN_MainIgnored, main.Locations.First(), main);
                            }

                            entryPointCandidates.Free();
                            return;
                        }
                    }

                    DiagnosticBag warnings = DiagnosticBag.GetInstance();
                    var viableEntryPoints = ArrayBuilder<MethodSymbol>.GetInstance();
                    foreach (var candidate in entryPointCandidates)
                    {
                        if (!candidate.HasEntryPointSignature())
                        {
                            // a single error for partial methods:
                            warnings.Add(ErrorCode.WRN_InvalidMainSig, candidate.Locations.First(), candidate);
                            continue;
                        }

                        if (candidate.IsGenericMethod || candidate.ContainingType.IsGenericType)
                        {
                            // a single error for partial methods:
                            warnings.Add(ErrorCode.WRN_MainCantBeGeneric, candidate.Locations.First(), candidate);
                            continue;
                        }

                        if (candidate.IsAsync)
                        {
                            diagnostics.Add(ErrorCode.ERR_MainCantBeAsync, candidate.Locations.First(), candidate);
                        }

                        viableEntryPoints.Add(candidate);
                    }

                    if ((object)mainType == null || viableEntryPoints.Count == 0)
                    {
                        diagnostics.AddRange(warnings);
                    }

                    warnings.Free();

                    if (viableEntryPoints.Count == 0)
                    {
                        if ((object)mainType == null)
                        {
                            diagnostics.Add(ErrorCode.ERR_NoEntryPoint, NoLocation.Singleton);
                        }
                        else
                        {
                            diagnostics.Add(ErrorCode.ERR_NoMainInClass, mainType.Locations.First(), mainType);
                        }

                    }
                    else if (viableEntryPoints.Count > 1)
                    {
                        viableEntryPoints.Sort(LexicalOrderSymbolComparer.Instance);
                        var info = new CSDiagnosticInfo(
                             ErrorCode.ERR_MultipleEntryPoints,
                             args: SpecializedCollections.EmptyArray<object>(),
                             symbols: viableEntryPoints.OfType<Symbol>().AsImmutable(),
                             additionalLocations: viableEntryPoints.Select(m => m.Locations.First()).OfType<Location>().AsImmutable());

                        diagnostics.Add(new CSDiagnostic(info, viableEntryPoints.First().Locations.First()));
                    }
                    else
                    {
                        entryPoint = viableEntryPoints[0];
                    }

                    viableEntryPoints.Free();
                    entryPointCandidates.Free();
                }
                finally
                {
                    sealedDiagnostics = diagnostics.ToReadOnlyAndFree();
                }
            }
        }

        internal class EntryPoint
        {
            public readonly MethodSymbol MethodSymbol;
            public readonly ImmutableArray<Diagnostic> Diagnostics;

            public EntryPoint(MethodSymbol methodSymbol, ImmutableArray<Diagnostic> diagnostics)
            {
                this.MethodSymbol = methodSymbol;
                this.Diagnostics = diagnostics;
            }
        }

        internal bool MightContainNoPiaLocalTypes()
        {
            return SourceAssembly.MightContainNoPiaLocalTypes();
        }

        // NOTE(cyrusn): There is a bit of a discoverability problem with this method and the same
        // named method in SyntaxTreeSemanticModel.  Technically, i believe these are the appropriate
        // locations for these methods.  This method has no dependencies on anything but the
        // compilation, while the other method needs a bindings object to determine what bound node
        // an expression syntax binds to.  Perhaps when we document these methods we should explain
        // where a user can find the other.
        public Conversion ClassifyConversion(ITypeSymbol source, ITypeSymbol destination)
        {
            using (Logger.LogBlock(FunctionId.CSharp_Compilation_ClassifyConversion, message: this.AssemblyName))
            {
                // Note that it is possible for there to be both an implicit user-defined conversion
                // and an explicit built-in conversion from source to destination. In that scenario
                // this method returns the implicit conversion.

                if ((object)source == null)
                {
                    throw new ArgumentNullException("source");
                }

                if ((object)destination == null)
                {
                    throw new ArgumentNullException("destination");
                }

                var cssource = source.EnsureCSharpSymbolOrNull<ITypeSymbol, TypeSymbol>("source");
                var csdest = destination.EnsureCSharpSymbolOrNull<ITypeSymbol, TypeSymbol>("destination");

                HashSet<DiagnosticInfo> useSiteDiagnostics = null;
                return Conversions.ClassifyConversion(cssource, csdest, ref useSiteDiagnostics);
            }
        }

        /// <summary>
        /// Returns a new ArrayTypeSymbol representing an array type tied to the base types of the
        /// COR Library in this Compilation.
        /// </summary>
        internal ArrayTypeSymbol CreateArrayTypeSymbol(TypeSymbol elementType, int rank = 1)
        {
            if ((object)elementType == null)
            {
                throw new ArgumentNullException("elementType");
            }

            return new ArrayTypeSymbol(this.Assembly, elementType, ImmutableArray<CustomModifier>.Empty, rank);
        }

        /// <summary>
        /// Returns a new PointerTypeSymbol representing a pointer type tied to a type in this Compilation.
        /// </summary>
        internal PointerTypeSymbol CreatePointerTypeSymbol(TypeSymbol elementType)
        {
            if ((object)elementType == null)
            {
                throw new ArgumentNullException("elementType");
            }

            return new PointerTypeSymbol(elementType);
        }

        #endregion

        #region Binding

        /// <summary>
        /// Gets a new SyntaxTreeSemanticModel for the specified syntax tree.
        /// </summary>
        public new SemanticModel GetSemanticModel(SyntaxTree syntaxTree)
        {
            if (syntaxTree == null)
            {
                throw new ArgumentNullException("tree");
            }

            if (!this.SyntaxTrees.Contains((SyntaxTree)syntaxTree))
            {
                throw new ArgumentException("tree");
            }

            return new SyntaxTreeSemanticModel(this, (SyntaxTree)syntaxTree);
        }

        // When building symbols from the declaration table (lazily), or inside a type, or when
        // compiling a method body, we may not have a BinderContext in hand for the enclosing
        // scopes.  Therefore, we build them when needed (and cache them) using a ContextBuilder.
        // Since a ContextBuilder is only a cache, and the identity of the ContextBuilders and
        // BinderContexts have no semantic meaning, we can reuse them or rebuild them, whichever is
        // most convenient.  We store them using weak references so that GC pressure will cause them
        // to be recycled.
        private WeakReference<BinderFactory>[] binderFactories;

        internal BinderFactory GetBinderFactory(SyntaxTree syntaxTree)
        {
            var treeNum = GetSyntaxTreeOrdinal(syntaxTree);
            var binderFactories = this.binderFactories;
            if (binderFactories == null)
            {
                binderFactories = new WeakReference<BinderFactory>[this.syntaxTrees.Length];
                binderFactories = Interlocked.CompareExchange(ref this.binderFactories, binderFactories, null) ?? binderFactories;
            }

            BinderFactory previousFactory;
            var previousWeakReference = binderFactories[treeNum];
            if (previousWeakReference != null && previousWeakReference.TryGetTarget(out previousFactory))
            {
                return previousFactory;
            }

            return AddNewFactory(syntaxTree, ref binderFactories[treeNum]);
        }

        private BinderFactory AddNewFactory(SyntaxTree syntaxTree, ref WeakReference<BinderFactory> slot)
        {
            var newFactory = new BinderFactory(this, syntaxTree);
            var newWeakReference = new WeakReference<BinderFactory>(newFactory);

            while (true)
            {
                BinderFactory previousFactory;
                WeakReference<BinderFactory> previousWeakReference = slot;
                if (previousWeakReference != null && previousWeakReference.TryGetTarget(out previousFactory))
                {
                    return previousFactory;
                }

                if (Interlocked.CompareExchange(ref slot, newWeakReference, previousWeakReference) == previousWeakReference)
                {
                    return newFactory;
                }
            }
        }

        internal Binder GetBinder(SyntaxReference reference)
        {
            return GetBinderFactory(reference.SyntaxTree).GetBinder((CSharpSyntaxNode)reference.GetSyntax());
        }

        internal Binder GetBinder(CSharpSyntaxNode syntax)
        {
            return GetBinderFactory(syntax.SyntaxTree).GetBinder(syntax);
        }

        /// <summary>
        /// Returns imported symbols for the given declaration.
        /// </summary>
        internal Imports GetImports(SingleNamespaceDeclaration declaration)
        {
            return GetBinderFactory(declaration.SyntaxReference.SyntaxTree).GetImportsBinder((CSharpSyntaxNode)declaration.SyntaxReference.GetSyntax()).GetImports();
        }

        internal Imports GetSubmissionImports()
        {
            return ((SourceNamespaceSymbol)SourceModule.GlobalNamespace).GetBoundImportsMerged().SingleOrDefault() ?? Imports.Empty;
        }

        internal InteractiveUsingsBinder GetInteractiveUsingsBinder()
        {
            Debug.Assert(IsSubmission);

            // empty compilation:
            if ((object)ScriptClass == null)
            {
                Debug.Assert(SyntaxTrees.Length == 0);
                return null;
            }

            return GetBinderFactory(SyntaxTrees.Single()).GetInteractiveUsingsBinder();
        }

        private Imports BindGlobalUsings()
        {
            return Imports.FromGlobalUsings(this);
        }

        private AliasSymbol CreateGlobalNamespaceAlias()
        {
            return AliasSymbol.CreateGlobalNamespaceAlias(this.GlobalNamespace, new InContainerBinder(this.GlobalNamespace, new BuckStopsHereBinder(this)));
        }

        void CompleteTree(SyntaxTree tree)
        {
            bool completedCompilationUnit = false;
            bool completedCompilation = false;

            if (lazyCompilationUnitCompletedTrees == null) Interlocked.CompareExchange(ref lazyCompilationUnitCompletedTrees, new HashSet<SyntaxTree>(), null);
            lock (lazyCompilationUnitCompletedTrees)
            {
                if (lazyCompilationUnitCompletedTrees.Add(tree))
                {
                    completedCompilationUnit = true;
                    if (lazyCompilationUnitCompletedTrees.Count == SyntaxTrees.Length)
                    {
                        completedCompilation = true;
                    }
                }
            }

            if (completedCompilationUnit)
            {
                EventQueue.Enqueue(new CompilationEvent.CompilationUnitCompleted(this, null, tree));
            }

            if (completedCompilation)
            {
                EventQueue.Enqueue(new CompilationEvent.CompilationCompleted(this));
                EventQueue.Complete(); // signal the end of compilation events
            }
        }

        internal void ReportUnusedImports(DiagnosticBag diagnostics, CancellationToken cancellationToken, SyntaxTree filterTree = null)
        {
            if (this.lazyImportInfos != null)
            {
                foreach (ImportInfo info in this.lazyImportInfos)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    SyntaxTree infoTree = info.Tree;
                    if (filterTree == null || filterTree == infoTree)
                    {
                        TextSpan infoSpan = info.Span;
                        if (!this.IsImportDirectiveUsed(infoTree, infoSpan.Start))
                        {
                            ErrorCode code = info.Kind == SyntaxKind.ExternAliasDirective
                                ? ErrorCode.INF_UnusedExternAlias
                                : ErrorCode.INF_UnusedUsingDirective;
                            diagnostics.Add(code, infoTree.GetLocation(infoSpan));
                        }
                    }
                }
            }

            // By definition, a tree is complete when all of its compiler diagnostics have been reported.
            // Since unused imports are the last thing we compute and report, a tree is complete when
            // the unused imports have been reported.
            if (EventQueue != null)
            {
                if (filterTree != null)
                {
                    CompleteTree(filterTree);
                }
                else
                {
                    foreach (var tree in SyntaxTrees)
                    {
                        CompleteTree(tree);
                    }
                }
            }
        }

        internal void RecordImport(UsingDirectiveSyntax syntax)
        {
            RecordImportInternal(syntax);
        }

        internal void RecordImport(ExternAliasDirectiveSyntax syntax)
        {
            RecordImportInternal(syntax);
        }

        private void RecordImportInternal(CSharpSyntaxNode syntax)
        {
            LazyInitializer.EnsureInitialized(ref this.lazyImportInfos).
                Add(new ImportInfo(syntax.SyntaxTree, syntax.Kind, syntax.Span));
        }

        private struct ImportInfo
        {
            public readonly SyntaxTree Tree;
            public readonly SyntaxKind Kind;
            public readonly TextSpan Span;

            public ImportInfo(SyntaxTree tree, SyntaxKind kind, TextSpan span)
            {
                this.Tree = tree;
                this.Kind = kind;
                this.Span = span;
            }

            public override bool Equals(object obj)
            {
                if (obj is ImportInfo)
                {
                    ImportInfo other = (ImportInfo)obj;
                    return
                        other.Kind == this.Kind &&
                        other.Tree == this.Tree && 
                        other.Span == this.Span;
                }
                
                return false;
            }

            public override int GetHashCode()
            {
                return Hash.Combine(Tree, Span.Start);
            }
        }

        #endregion

        #region Diagnostics

        internal override CommonMessageProvider MessageProvider
        {
            get { return CSharp.MessageProvider.Instance; }
        }

        /// <summary>
        /// The bag in which semantic analysis should deposit its diagnostics.
        /// </summary>
        internal DiagnosticBag SemanticDiagnostics
        {
            get
            {
                if (this.lazySemanticDiagnostics == null)
                {
                    var diagnostics = new DiagnosticBag();
                    Interlocked.CompareExchange(ref this.lazySemanticDiagnostics, diagnostics, null);
                }

                return this.lazySemanticDiagnostics;
            }
        }

        private DiagnosticBag lazySemanticDiagnostics;

        /// <summary>
        /// A bag in which diagnostics that should be reported after code gen can be deposited.
        /// </summary>
        internal DiagnosticBag AdditionalCodegenWarnings
        {
            get
            {
                return this.additionalCodegenWarnings;
            }
        }

        private DiagnosticBag additionalCodegenWarnings = new DiagnosticBag();

        internal DeclarationTable Declarations
        {
            get
            {
                return this.declarationTable;
            }
        }

        /// <summary>
        /// Gets the diagnostics produced during the parsing stage of a compilation. There are no diagnostics for declarations or accessor or
        /// method bodies, for example.
        /// </summary>
        public override ImmutableArray<Diagnostic> GetParseDiagnostics(CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetDiagnostics(CompilationStage.Parse, false, cancellationToken);
        }

        /// <summary>
        /// Gets the diagnostics produced during symbol declaration headers.  There are no diagnostics for accessor or
        /// method bodies, for example.
        /// </summary>
        public override ImmutableArray<Diagnostic> GetDeclarationDiagnostics(CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetDiagnostics(CompilationStage.Declare, false, cancellationToken);
        }

        /// <summary>
        /// Gets the diagnostics produced during the analysis of method bodies and field initializers.
        /// </summary>
        public override ImmutableArray<Diagnostic> GetMethodBodyDiagnostics(CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetDiagnostics(CompilationStage.Compile, false, cancellationToken);
        }

        /// <summary>
        /// Gets the all the diagnostics for the compilation, including syntax, declaration, and binding. Does not
        /// include any diagnostics that might be produced during emit.
        /// </summary>
        public override ImmutableArray<Diagnostic> GetDiagnostics(CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetDiagnostics(DefaultDiagnosticsStage, true, cancellationToken);
        }

        internal ImmutableArray<Diagnostic> GetDiagnostics(CompilationStage stage, bool includeEarlierStages, CancellationToken cancellationToken)
        {
            using (Logger.LogBlock(FunctionId.CSharp_Compilation_GetDiagnostics, message: this.AssemblyName, cancellationToken: cancellationToken))
            {
                var builder = DiagnosticBag.GetInstance();

                if (stage == CompilationStage.Parse || (stage > CompilationStage.Parse && includeEarlierStages))
                {
                    if (this.Options.ConcurrentBuild)
                    {
                        var parallelOptions = cancellationToken.CanBeCanceled
                                            ? new ParallelOptions() { CancellationToken = cancellationToken }
                                            : Compiler.defaultParallelOptions;

                        Parallel.For(0, this.SyntaxTrees.Length, parallelOptions,
                            i => builder.AddRange(this.SyntaxTrees[i].GetDiagnostics(cancellationToken)));
                    }
                    else
                    {
                        foreach (var syntaxTree in this.SyntaxTrees)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            builder.AddRange(syntaxTree.GetDiagnostics(cancellationToken));
                        }
                    }
                }

                if (stage == CompilationStage.Declare || stage > CompilationStage.Declare && includeEarlierStages)
                {
                    builder.AddRange(Options.Errors);

                    cancellationToken.ThrowIfCancellationRequested();

                    // the set of diagnostics related to establishing references.
                    builder.AddRange(GetBoundReferenceManager().Diagnostics);

                    cancellationToken.ThrowIfCancellationRequested();

                    builder.AddRange(GetSourceDeclarationDiagnostics(cancellationToken: cancellationToken));
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (stage == CompilationStage.Compile || stage > CompilationStage.Compile && includeEarlierStages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    builder.AddRange(Compiler.GetAllMethodBodyDiagnostics(this, cancellationToken: cancellationToken));
                }

                // Before returning diagnostics, we filter warnings
                // to honor the compiler options (e.g., /nowarn, /warnaserror and /warn) and the pragmas.
                var result = DiagnosticBag.GetInstance();
                FilterAndAppendAndFreeDiagnostics(result, ref builder);
                return result.ToReadOnlyAndFree<Diagnostic>();
            }
        }

        /// <summary>
        /// Filter out warnings based on the compiler options (/nowarn, /warn and /warnaserror) and the pragma warning directives.
        /// 'incoming' is freed.
        /// </summary>
        /// <returns>True when there is no error or warning treated as an error.</returns>
        internal override bool FilterAndAppendAndFreeDiagnostics(DiagnosticBag accumulator, ref DiagnosticBag incoming)
        {
            bool result = FilterAndAppendDiagnostics(accumulator, incoming.AsEnumerableWithoutResolution(), this.options);
            incoming.Free();
            incoming = null;
            return result;
        }

        static ErrorCode[] AlinkWarnings = { ErrorCode.WRN_ConflictingMachineAssembly, 
                                               ErrorCode.WRN_RefCultureMismatch, 
                                               ErrorCode.WRN_InvalidVersionFormat };

        /// <summary>
        /// Filter out warnings based on the compiler options (/nowarn, /warn and /warnaserror) and the pragma warning directives.
        /// </summary>
        /// <returns>True when there is no error or warning treated as an error.</returns>
        private static bool FilterAndAppendDiagnostics(DiagnosticBag accumulator, IEnumerable<Diagnostic> incoming, CSharpCompilationOptions options)
        {
            bool hasErrorOrWarningAsError = false;

            foreach (Diagnostic d in incoming)
            {
                // Filter void diagnostics so that our callers don't have to perform resolution
                // (which might copy the list of diagnostics).
                if (d.Severity == InternalDiagnosticSeverity.Void)
                {
                    continue;
                }

                // If it is an error, return it as it is.
                if (d.Severity == DiagnosticSeverity.Error)
                {
                    hasErrorOrWarningAsError = true;
                    accumulator.Add(d);
                    continue;
                }

                //In the native compiler, all warnings originating from alink.dll were issued
                //under the id WRN_ALinkWarn - 1607. If a customer used nowarn:1607 they would get
                //none of those warnings. In Roslyn, we've given each of these warnings their
                //own number, so that they may be configured independently. To preserve compatibility
                //if a user has specifically configured 1607 and we are reporting one of the alink warnings, use
                //the configuration specified for 1607. As implemented, this could result in customers 
                //specifying warnaserror:1607 and getting a message saying "warning as error CS8012..."
                //We don't permit configuring 1607 and independently configuring the new warnings.

                ReportDiagnostic reportAction;

                if (AlinkWarnings.Contains((ErrorCode)d.Code) && 
                    options.SpecificDiagnosticOptions.Keys.Contains(CSharp.MessageProvider.Instance.GetIdForErrorCode((int)ErrorCode.WRN_ALinkWarn)))
                {
                    reportAction = GetDiagnosticReport(ErrorFacts.GetSeverity(ErrorCode.WRN_ALinkWarn), 
                                                        CSharp.MessageProvider.Instance.GetIdForErrorCode((int)ErrorCode.WRN_ALinkWarn), 
                                                        ErrorFacts.GetWarningLevel(ErrorCode.WRN_ALinkWarn), 
                                                        d.Location as Location, 
                                                        options,
                                                        d.Category);
                }
                else
                {
                    reportAction = GetDiagnosticReport(d.Severity, d.Id, d.WarningLevel, d.Location as Location, options, d.Category);
                }

                switch (reportAction)
                {
                    case ReportDiagnostic.Suppress:
                        continue;
                    case ReportDiagnostic.Error:
                        Debug.Assert(d.Severity == DiagnosticSeverity.Warning);
                        hasErrorOrWarningAsError = true;
                        if (d.IsWarningAsError)
                        {
                            // If the flag has already been set, return it without creating new one. 
                            accumulator.Add(d);
                        }
                        else
                        {
                            // For a warning treated as an error, we replace the existing one 
                            // with a new dianostic setting a WarningAsError flag to be true 
                            accumulator.Add(d.WithWarningAsError(true));
                        }
                        continue;
                    case ReportDiagnostic.Default:
                    case ReportDiagnostic.Warn:
                        accumulator.Add(d);
                        continue;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(reportAction);
                }
            }

            return !hasErrorOrWarningAsError;
        }

        // Take a warning and return the final deposition of the given warning,
        // based on both command line options and pragmas
        internal static ReportDiagnostic GetDiagnosticReport(DiagnosticSeverity severity, string id, int warningLevel, Location location, CompilationOptions options, string kind)
        {
            switch (severity)
            {
                case InternalDiagnosticSeverity.Void:
                    // If this is a deleted diagnostic, suppress it.
                    return ReportDiagnostic.Suppress;
                case DiagnosticSeverity.Info:
                    // Don't modify Info diagnostics.
                    return ReportDiagnostic.Default;
                case DiagnosticSeverity.Warning:
                    // Process warnings below.
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(severity);
            }

            // Read options (e.g., /nowarn or /warnaserror)
            ReportDiagnostic report = ReportDiagnostic.Default;
            options.SpecificDiagnosticOptions.TryGetValue(id, out report);

            // Compute if the reporting should be suppressed.
            if (warningLevel > options.WarningLevel  // honor the warning level
                || report == ReportDiagnostic.Suppress)                // check options (/nowarn)
            {
                return ReportDiagnostic.Suppress;
            }

            // If location is available, check out pragmas
            if (location != null &&
                location.SourceTree != null &&
                ((SyntaxTree)location.SourceTree).GetPragmaDirectiveWarningState(id, location.SourceSpan.Start) == ReportDiagnostic.Suppress)
            {
                return ReportDiagnostic.Suppress;
            }

            // Unless specific warning options are defined (/warnaserror[+|-]:<n> or /nowarn:<n>, 
            // follow the global option (/warnaserror[+|-] or /nowarn).
            if (report == ReportDiagnostic.Default)
            {
                report = options.GeneralDiagnosticOption;
            }

            return report;
        }

        private ImmutableArray<Diagnostic> GetSourceDeclarationDiagnostics(SyntaxTree syntaxTree = null, TextSpan? filterSpanWithinTree = null, Func<IEnumerable<Diagnostic>, SyntaxTree, TextSpan?, IEnumerable<Diagnostic>> locationFilterOpt = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            // global imports diagnostics (specified via compilation options):
            GlobalImports.Complete(cancellationToken);

            SourceLocation location = null;
            if (syntaxTree != null)
            {
                var root = syntaxTree.GetRoot(cancellationToken);
                location = filterSpanWithinTree.HasValue ?
                    new SourceLocation(syntaxTree, filterSpanWithinTree.Value) :
                    new SourceLocation(root);
            }

            Assembly.ForceComplete(location, cancellationToken);

            var result = this.SemanticDiagnostics.AsEnumerable().Concat(
                ((SourceModuleSymbol)this.SourceModule).Diagnostics);

            if (locationFilterOpt != null)
            {
                Debug.Assert(syntaxTree != null);
                result = locationFilterOpt(result, syntaxTree, filterSpanWithinTree);
            }

            // NOTE: Concatenate the CLS diagnostics *after* filtering by tree/span, because they're already filtered.
            ImmutableArray<Diagnostic> clsDiagnostics = GetClsComplianceDiagnostics(syntaxTree, filterSpanWithinTree, cancellationToken);

            return result.AsImmutable().Concat(clsDiagnostics);
        }

        private ImmutableArray<Diagnostic> GetClsComplianceDiagnostics(SyntaxTree syntaxTree, TextSpan? filterSpanWithinTree, CancellationToken cancellationToken)
        {
            if (syntaxTree != null)
            {
                var builder = DiagnosticBag.GetInstance();
                ClsComplianceChecker.CheckCompliance(this, builder, cancellationToken, syntaxTree, filterSpanWithinTree);
                return builder.ToReadOnlyAndFree();
            }

            if (this.lazyClsComplianceDiagnostics.IsDefault)
            {
                var builder = DiagnosticBag.GetInstance();
                ClsComplianceChecker.CheckCompliance(this, builder, cancellationToken);
                ImmutableInterlocked.InterlockedInitialize(ref this.lazyClsComplianceDiagnostics, builder.ToReadOnlyAndFree());
            }

            Debug.Assert(!this.lazyClsComplianceDiagnostics.IsDefault);
            return this.lazyClsComplianceDiagnostics;
        }

        private static IEnumerable<Diagnostic> FilterDiagnosticsByLocation(IEnumerable<Diagnostic> diagnostics, SyntaxTree tree, TextSpan? filterSpanWithinTree)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.ContainsLocation(tree, filterSpanWithinTree))
                {
                    yield return diagnostic;
                }
            }
        }

        internal ImmutableArray<Diagnostic> GetDiagnosticsForSyntaxTree(
            CompilationStage stage,
            SyntaxTree syntaxTree,
            TextSpan? filterSpanWithinTree,
            bool includeEarlierStages,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var builder = DiagnosticBag.GetInstance();
            if (stage == CompilationStage.Parse || (stage > CompilationStage.Parse && includeEarlierStages))
            {
                var syntaxDiagnostics = syntaxTree.GetDiagnostics();
                syntaxDiagnostics = FilterDiagnosticsByLocation(syntaxDiagnostics, syntaxTree, filterSpanWithinTree);
                builder.AddRange(syntaxDiagnostics);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (stage == CompilationStage.Declare || (stage > CompilationStage.Declare && includeEarlierStages))
            {
                var declarationDiagnostics = GetSourceDeclarationDiagnostics(syntaxTree, filterSpanWithinTree, FilterDiagnosticsByLocation, cancellationToken);
                Debug.Assert(declarationDiagnostics.All(d => d.ContainsLocation(syntaxTree, filterSpanWithinTree)));
                builder.AddRange(declarationDiagnostics);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (stage == CompilationStage.Compile || (stage > CompilationStage.Compile && includeEarlierStages))
            {
                //remove some errors that don't have locations in the tree, like "no suitable main method."
                //Members in trees other than the one being examined are not compiled. This includes field
                //initializers which can result in 'field is never initialized' warnings for fields in partial 
                //types when the field is in a different source file than the one for which we're getting diagnostics. 
                //For that reason the bag must be also filtered by tree.
                IEnumerable<Diagnostic> methodBodyDiagnostics = Compiler.GetMethodBodyDiagnosticsForTree(this, syntaxTree, filterSpanWithinTree, cancellationToken);

                // TODO: Enable the below commented assert and remove the filtering code in the next line.
                //       GetMethodBodyDiagnosticsForTree seems to be returning diagnostics with locations that don't satisfy the filter tree/span, this must be fixed.
                // Debug.Assert(methodBodyDiagnostics.All(d => DiagnosticContainsLocation(d, syntaxTree, filterSpanWithinTree)));
                methodBodyDiagnostics = FilterDiagnosticsByLocation(methodBodyDiagnostics, syntaxTree, filterSpanWithinTree);

                builder.AddRange(methodBodyDiagnostics);
            }

            // Before returning diagnostics, we filter warnings
            // to honor the compiler options (/nowarn, /warnaserror and /warn) and the pragmas.
            var result = DiagnosticBag.GetInstance();
            FilterAndAppendAndFreeDiagnostics(result, ref builder);
            return result.ToReadOnlyAndFree<Diagnostic>();
        }

        #endregion

        #region Resources

        protected override void AppendDefaultVersionResource(Stream resourceStream)
        {
            var sourceAssembly = SourceAssembly;
            string fileVersion = sourceAssembly.FileVersion ?? sourceAssembly.Identity.Version.ToString();

            Win32ResourceConversions.AppendVersionToResourceStream(resourceStream,
                !this.Options.OutputKind.IsApplication(),
                fileVersion: fileVersion,
                originalFileName: this.SourceModule.Name,
                internalName: this.SourceModule.Name,
                productVersion: sourceAssembly.InformationalVersion ?? fileVersion,
                fileDescription: sourceAssembly.Title ?? " ", //alink would give this a blank if nothing was supplied.
                assemblyVersion: sourceAssembly.Identity.Version,
                legalCopyright: sourceAssembly.Copyright ?? " ", //alink would give this a blank if nothing was supplied.
                legalTrademarks: sourceAssembly.Trademark,
                productName: sourceAssembly.Product,
                comments: sourceAssembly.Description,
                companyName: sourceAssembly.Company);

        }

        #endregion

        #region Emit

        internal override bool IsDelaySign
        {
            get { return SourceAssembly.IsDelaySign; }
        }

        internal override StrongNameKeys StrongNameKeys
        {
            get { return SourceAssembly.StrongNameKeys; }
        }

        internal override FunctionId EmitFunctionId
        {
            get { return FunctionId.CSharp_Compilation_Emit; }
        }

        internal override EmitResult MakeEmitResult(bool success, ImmutableArray<Diagnostic> diagnostics)
        {
            return new EmitResult(success, diagnostics.Cast<Diagnostic>().AsImmutable());
        }

        /// <summary>
        /// Emit the IL for the compilation into the specified stream.
        /// </summary>
        /// <param name="outputStream">Stream to which the compilation will be written.</param>
        /// <param name="outputName">Name of the module or assembly. Null to use the existing compilation name.
        /// CAUTION: If this is set to a (non-null) value other than the existing compilation output name, then internals-visible-to
        /// and assembly references may not work as expected.  In particular, things that were visible at bind time, based on the 
        /// name of the compilation, may not be visible at runtime and vice-versa.
        /// </param>
        /// <param name="pdbFileName">The name of the PDB file - embedded in the output.  Null to infer from the stream or the compilation.
        /// Ignored unless pdbStream is non-null.
        /// </param>
        /// <param name="pdbStream">Stream to which the compilation's debug info will be written.  Null to forego PDB generation.</param>
        /// <param name="xmlDocStream">Stream to which the compilation's XML documentation will be written.  Null to forego XML generation.</param>
        /// <param name="cancellationToken">To cancel the emit process.</param>
        /// <param name="win32Resources">Stream from which the compilation's Win32 resources will be read (in RES format).  
        /// Null to indicate that there are none.</param>
        /// <param name="manifestResources">List of the compilation's managed resources.  Null to indicate that there are none.</param>
        public new EmitResult Emit(
            Stream outputStream,
            string outputName = null,
            string pdbFileName = null,
            Stream pdbStream = null,
            Stream xmlDocStream = null,
            CancellationToken cancellationToken = default(CancellationToken),
            Stream win32Resources = null,
            IEnumerable<ResourceDescription> manifestResources = null)
        {
            return base.Emit(outputStream, outputName, pdbFileName, pdbStream, xmlDocStream, cancellationToken, win32Resources, manifestResources);
        }

        /// <summary>
        /// Emit the IL for the compilation into the specified stream.
        /// </summary>
        /// <param name="outputPath">Path of the file to which the compilation will be written.</param>
        /// <param name="pdbPath">Path of the file to which the compilation's debug info will be written.
        /// Also embedded in the output file.  Null to forego PDB generation.
        /// </param>
        /// <param name="xmlDocPath">Path of the file to which the compilation's XML documentation will be written.  Null to forego XML generation.</param>
        /// <param name="cancellationToken">To cancel the emit process.</param>
        /// <param name="win32ResourcesPath">Path of the file from which the compilation's Win32 resources will be read (in RES format).  
        /// Null to indicate that there are none.</param>
        /// <param name="manifestResources">List of the compilation's managed resources.  Null to indicate that there are none.</param>
        public new EmitResult Emit(
            string outputPath,
            string pdbPath = null,
            string xmlDocPath = null,
            CancellationToken cancellationToken = default(CancellationToken),
            string win32ResourcesPath = null,
            IEnumerable<ResourceDescription> manifestResources = null)
        {
            return base.Emit(outputPath, pdbPath, xmlDocPath, cancellationToken, win32ResourcesPath, manifestResources);
        }

        internal void EnsureAnonymousTypeTemplates(CancellationToken cancellationToken)
        {
            if (this.GetSubmissionSlotIndex() >= 0 && HasCodeToEmit())
            {
                if (!this.AnonymousTypeManager.AreTemplatesSealed)
                {
                    DiagnosticBag discardedDiagnostics = DiagnosticBag.GetInstance();
                    Compile(
                        outputName: null,
                        moduleVersionId: Guid.NewGuid(),
                        xmlDocStream: null,
                        assemblySymbolMapper: null,
                        cancellationToken: cancellationToken,
                        manifestResources: null,
                        win32Resources: null,
                        testData: null,
                        metadataOnly: false,
                        generateDebugInfo: false,
                        diagnostics: discardedDiagnostics);
                    discardedDiagnostics.Free();
                }

                Debug.Assert(this.AnonymousTypeManager.AreTemplatesSealed);
            }
            else if (this.PreviousSubmission != null)
            {
                this.PreviousSubmission.EnsureAnonymousTypeTemplates(cancellationToken);
            }
        }

        /// <summary>
        /// Emits the IL for the symbol declarations into the specified stream.  Useful for emitting information for
        /// cross-language modeling of code.  This emits what it can even if there are errors.
        /// </summary>
        /// <param name="metadataStream">Stream to which the compilation's metadata will be written.</param>
        /// <param name="outputName">Name of the compilation: file name and extension.  Null to use the existing output name.
        /// CAUTION: If this is set to a (non-null) value other than the existing compilation output name, then internals-visible-to
        /// and assembly references may not work as expected.  In particular, things that were visible at bind time, based on the 
        /// name of the compilation, may not be visible at runtime and vice-versa.
        /// </param>
        /// <param name="xmlDocStream">Stream to which the compilation's XML documentation will be written.  Null to forego XML generation.</param>
        /// <param name="cancellationToken">To cancel the emit process.</param>
        public new EmitResult EmitMetadataOnly(
            Stream metadataStream,
            string outputName = null,
            Stream xmlDocStream = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return base.EmitMetadataOnly(metadataStream, outputName, xmlDocStream, cancellationToken);
        }

        internal override CommonPEModuleBuilder CreateModuleBuilder(
            string outputName,
            Guid moduleVersionId,
            IEnumerable<ResourceDescription> manifestResources,
            Func<IAssemblySymbol, AssemblyIdentity> assemblySymbolMapper,
            CancellationToken cancellationToken,
            CompilationTestData testData,
            DiagnosticBag diagnostics,
            ref bool hasDeclarationErrors)
        {
            return this.CreateModuleBuilder(
                outputName,
                moduleVersionId,
                manifestResources,
                assemblySymbolMapper,
                ImmutableArray<NamedTypeSymbol>.Empty,
                cancellationToken,
                testData,
                diagnostics,
                ref hasDeclarationErrors);
        }

        internal CommonPEModuleBuilder CreateModuleBuilder(
            string outputName,
            Guid moduleVersionId,
            IEnumerable<ResourceDescription> manifestResources,
            Func<IAssemblySymbol, AssemblyIdentity> assemblySymbolMapper,
            ImmutableArray<NamedTypeSymbol> additionalTypes,
            CancellationToken cancellationToken,
            CompilationTestData testData,
            DiagnosticBag diagnostics,
            ref bool hasDeclarationErrors)
        {
            // The diagnostics should include syntax and declaration errors also. We insert these before calling Emitter.Emit, so that the emitter
            // does not attempt to emit if there are declaration errors (but we do insert all errors from method body binding...)
            if (!FilterAndAppendDiagnostics(
                diagnostics,
                GetDiagnostics(CompilationStage.Declare, true, cancellationToken),
                this.options))
            {
                hasDeclarationErrors = true;
            }

            // Do not waste a slot in the submission chain for submissions that contain no executable code
            // (they may only contain #r directives, usings, etc.)
            if (IsSubmission && !HasCodeToEmit())
            {
                return null;
            }

            string runtimeMDVersion = GetRuntimeMetadataVersion(diagnostics);
            if (runtimeMDVersion == null)
            {
                return null;
            }

            var moduleProps = ConstructModuleSerializationProperties(runtimeMDVersion, moduleVersionId);

            if (manifestResources == null)
            {
                manifestResources = SpecializedCollections.EmptyEnumerable<ResourceDescription>();
            }

            PEModuleBuilder moduleBeingBuilt;
            if (options.OutputKind.IsNetModule())
            {
                Debug.Assert(additionalTypes.IsEmpty);

                moduleBeingBuilt = new PENetModuleBuilder(
                    (SourceModuleSymbol)SourceModule,
                    outputName,
                    moduleProps,
                    manifestResources);
            }
            else
            {
                var kind = options.OutputKind.IsValid() ? options.OutputKind : OutputKind.DynamicallyLinkedLibrary;
                moduleBeingBuilt = new PEAssemblyBuilder(
                    SourceAssembly,
                    outputName,
                    kind,
                    moduleProps,
                    manifestResources,
                    assemblySymbolMapper,
                    additionalTypes);
            }

            // testData is only passed when running tests.
            if (testData != null)
            {
                moduleBeingBuilt.SetMethodTestData(testData.Methods);
                testData.Module = moduleBeingBuilt;
            }

            return moduleBeingBuilt;
        }

        internal override bool Compile(
            CommonPEModuleBuilder moduleBuilder,
            string outputName,
            IEnumerable<ResourceDescription> manifestResources,
            Stream win32Resources,
            Stream xmlDocStream,
            CancellationToken cancellationToken,
            bool metadataOnly,
            bool generateDebugInfo,
            DiagnosticBag diagnostics,
            Predicate<ISymbol> filter,
            bool hasDeclarationErrors)
        {
            // TODO (tomat): NoPIA:
            // EmbeddedSymbolManager.MarkAllDeferredSymbolsAsReferenced(this)

            var moduleBeingBuilt = (PEModuleBuilder)moduleBuilder;

            if (metadataOnly)
            {
                if (hasDeclarationErrors)
                {
                    return false;
                }

                Compiler.CompileSynthesizedMethodMetadata(this, moduleBeingBuilt, cancellationToken);
            }
            else
            {
                // start generating PDB checksums if we need to emit PDBs
                if (generateDebugInfo && moduleBeingBuilt != null)
                {
                    // Add debug documents for all trees with distinct paths.
                    foreach (var tree in this.syntaxTrees)
                    {
                        var path = tree.FilePath;
                        if (!string.IsNullOrEmpty(path))
                        {
                            // compilation does not guarantee that all trees will have distinct paths.
                            // Do not attempt adding a document for a particular path if we already added one.
                            string normalizedPath = moduleBeingBuilt.NormalizeDebugDocumentPath(path, basePath: null);
                            var existingDoc = moduleBeingBuilt.TryGetDebugDocumentForNormalizedPath(normalizedPath);
                            if (existingDoc == null)
                            {
                                moduleBeingBuilt.AddDebugDocument(MakeDebugSourceDocumentForTree(normalizedPath, tree));
                            }
                        }
                    }

                    // Add debug documents for all pragmas. 
                    // If there are clashes with already processed directives, report warnings.
                    // If there are clashes with debug documents that came from actual trees, ignore the pragma.
                    foreach (var tree in this.syntaxTrees)
                    {
                        AddDebugSourceDocumentsForChecksumDirectives(moduleBeingBuilt, tree, diagnostics);
                    }
                }

                // EDMAURER perform initial bind of method bodies in spite of earlier errors. This is the same
                // behavior as when calling GetDiagnostics()

                // Use a temporary bag so we don't have to refilter pre-existing diagnostics.
                DiagnosticBag methodBodyDiagnosticBag = DiagnosticBag.GetInstance();

                Compiler.CompileMethodBodies(
                    this,
                    moduleBeingBuilt,
                    generateDebugInfo,
                    hasDeclarationErrors,
                    filter: filter,
                    filterTree: null,
                    filterSpanWithinTree: null,
                    diagnostics: methodBodyDiagnosticBag,
                    cancellationToken: cancellationToken);
                SetupWin32Resources(moduleBeingBuilt, win32Resources, methodBodyDiagnosticBag);

                ReportManifestResourceDuplicates(
                    manifestResources,
                    SourceAssembly.Modules.Skip(1).Select((m) => m.Name),   //all modules except the first one
                    AddedModulesResourceNames(methodBodyDiagnosticBag),
                    methodBodyDiagnosticBag);

                bool hasMethodBodyErrorOrWarningAsError = !FilterAndAppendAndFreeDiagnostics(diagnostics, ref methodBodyDiagnosticBag);

                if (hasDeclarationErrors || hasMethodBodyErrorOrWarningAsError)
                {
                    return false;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
          
            // Use a temporary bag so we don't have to refilter pre-existing diagnostics.
            DiagnosticBag xmlDiagnostics = DiagnosticBag.GetInstance();
            DocumentationCommentCompiler.WriteDocumentationCommentXml(this, outputName, xmlDocStream, xmlDiagnostics, cancellationToken);

            if (!FilterAndAppendAndFreeDiagnostics(diagnostics, ref xmlDiagnostics))
            {
                return false;
            }

            // Use a temporary bag so we don't have to refilter pre-existing diagnostics.
            DiagnosticBag importDiagnostics = DiagnosticBag.GetInstance();
            this.ReportUnusedImports(importDiagnostics, cancellationToken);

            if (!FilterAndAppendAndFreeDiagnostics(diagnostics, ref importDiagnostics))
            {
                Debug.Assert(false, "Should never produce an error");
                return false;
            }

            return true;
        }

        private IEnumerable<string> AddedModulesResourceNames(DiagnosticBag diagnostics)
        {
            ImmutableArray<ModuleSymbol> modules = SourceAssembly.Modules;

            for (int i = 1; i < modules.Length; i++)
            {
                var m = (Symbols.Metadata.PE.PEModuleSymbol)modules[i];
                ImmutableArray<EmbeddedResource> resources;

                try
                {
                    resources = m.Module.GetEmbeddedResourcesOrThrow();
                }
                catch (BadImageFormatException)
                {
                    diagnostics.Add(new CSDiagnosticInfo(ErrorCode.ERR_BindToBogus, m), NoLocation.Singleton);
                    continue;
                }

                foreach (var resource in resources)
                {
                    yield return resource.Name;
                }
            }
        }

        internal override EmitDifferenceResult EmitDifference(
            EmitBaseline baseline,
            IEnumerable<SemanticEdit> edits,
            Stream metadataStream,
            Stream ilStream,
            Stream pdbStream,
            ICollection<uint> updatedMethodTokens,
            CompilationTestData testData,
            CancellationToken cancellationToken)
        {
            return EmitHelpers.EmitDifference(
                this,
                baseline,
                edits,
                metadataStream,
                ilStream,
                pdbStream,
                updatedMethodTokens,
                testData,
                cancellationToken);
        }

        internal string GetRuntimeMetadataVersion(DiagnosticBag diagnostics)
        {
            string runtimeMDVersion = GetRuntimeMetadataVersion();
            if (runtimeMDVersion != null)
            {
                return runtimeMDVersion;
            }

            DiagnosticBag runtimeMDVersionDiagnostics = DiagnosticBag.GetInstance();
            runtimeMDVersionDiagnostics.Add(ErrorCode.WRN_NoRuntimeMetadataVersion, NoLocation.Singleton);
            if (!FilterAndAppendAndFreeDiagnostics(diagnostics, ref runtimeMDVersionDiagnostics))
            {
                return null;
            }

            return string.Empty; //prevent emitter from crashing.
        }

        private string GetRuntimeMetadataVersion()
        {
            var corAssembly = Assembly.CorLibrary as Symbols.Metadata.PE.PEAssemblySymbol;

            if ((object)corAssembly != null)
            {
                return corAssembly.Assembly.ManifestModule.MetadataVersion;
            }

            return Options.RuntimeMetadataVersion;
        }

        private static void AddDebugSourceDocumentsForChecksumDirectives(
            PEModuleBuilder moduleBeingBuilt, 
            SyntaxTree tree, 
            DiagnosticBag diagnostics)
        {
            var checksumDirectives = tree.GetRoot().GetDirectives(d => d.Kind == SyntaxKind.PragmaChecksumDirectiveTrivia && 
                                                                 !d.ContainsDiagnostics);

            foreach (var directive in checksumDirectives)
            {
                var checkSumDirective = (PragmaChecksumDirectiveTriviaSyntax)directive;
                var path = checkSumDirective.File.ValueText;

                var checkSumText = checkSumDirective.Bytes.ValueText;
                var normalizedPath = moduleBeingBuilt.NormalizeDebugDocumentPath(path, basePath: tree.FilePath);
                var existingDoc = moduleBeingBuilt.TryGetDebugDocumentForNormalizedPath(normalizedPath);

                // duplicate checksum pragmas are valid as long as values match
                // if we have seen this document already, check for matching values.
                if (existingDoc != null)
                {
                    // pragma matches a file path on an actual tree.
                    // Dev12 compiler just ignores the pragma in this case which means that
                    // checksum of the actual tree always wins and no warning is given.
                    // We will continue doing the same.
                    if (existingDoc.IsComputedChecksum)
                    {
                        continue;
                    } 

                    if (CheckSumMatches(checkSumText, existingDoc.SourceHash))
                    {
                        var guid = Guid.Parse(checkSumDirective.Guid.ValueText);
                        if (guid == existingDoc.SourceHashKind)
                        {
                            // all parts match, nothing to do
                            continue;
                        }
                    }

                    // did not match to an existing document
                    // produce a warning and ignore the pragma
                    diagnostics.Add(ErrorCode.WRN_ConflictingChecksum, new SourceLocation(checkSumDirective), path);
                }
                else
                {
                    var newDocument = new Cci.DebugSourceDocument(
                        normalizedPath,
                        Cci.DebugSourceDocument.CorSymLanguageTypeCSharp,
                        MakeCheckSumBytes(checkSumDirective.Bytes.ValueText),
                        Guid.Parse(checkSumDirective.Guid.ValueText));

                    moduleBeingBuilt.AddDebugDocument(newDocument);
                }
            }
        }

        private static bool CheckSumMatches(string bytesText, ImmutableArray<byte> bytes)
        {
            if (bytesText.Length != bytes.Length * 2)
            {
                return false;
            }

            for (int i = 0, len = bytesText.Length / 2; i < len; i++)
            {
                // 1A  in text becomes   0x1A
                var b = SyntaxFacts.HexValue(bytesText[i * 2]) * 16 +
                        SyntaxFacts.HexValue(bytesText[i * 2 + 1]);

                if (b != bytes[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static ImmutableArray<byte> MakeCheckSumBytes(string bytesText)
        {
            ArrayBuilder<byte> builder = ArrayBuilder<byte>.GetInstance();

            for (int i = 0, len = bytesText.Length / 2; i < len; i++)
            {
                // 1A  in text becomes   0x1A
                var b = SyntaxFacts.HexValue(bytesText[i * 2]) * 16 +
                        SyntaxFacts.HexValue(bytesText[i * 2 + 1]);

                builder.Add((byte)b);
            }

            return builder.ToImmutableAndFree();
        }

        private static Cci.DebugSourceDocument MakeDebugSourceDocumentForTree(string normalizedPath, SyntaxTree tree)
        {
            Func<ImmutableArray<byte>> checkSumSha1 = () => tree.GetSha1Checksum();
            return new Cci.DebugSourceDocument(normalizedPath, Cci.DebugSourceDocument.CorSymLanguageTypeCSharp, checkSumSha1);
        }

        private void SetupWin32Resources(PEModuleBuilder moduleBeingBuilt, Stream win32Resources, DiagnosticBag diagnostics)
        {
            if (win32Resources == null)
                return;

            switch (DetectWin32ResourceForm(win32Resources))
            {
                case Win32ResourceForm.COFF:
                    moduleBeingBuilt.Win32ResourceSection = MakeWin32ResourcesFromCOFF(win32Resources, diagnostics);
                    break;
                case Win32ResourceForm.RES:
                    moduleBeingBuilt.Win32Resources = MakeWin32ResourceList(win32Resources, diagnostics);
                    break;
                default:
                    diagnostics.Add(ErrorCode.ERR_BadWin32Res, NoLocation.Singleton, "Unrecognized file format.");
                    break;
            }
        }

        protected override bool HasCodeToEmit()
        {
            foreach (var syntaxTree in SyntaxTrees)
            {
                var unit = syntaxTree.GetCompilationUnitRoot();
                if (unit.Members.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Common Members

        protected override Compilation CommonWithReferences(IEnumerable<MetadataReference> newReferences)
        {
            return WithReferences(newReferences);
        }

        protected override Compilation CommonWithAssemblyName(string assemblyName)
        {
            return WithAssemblyName(assemblyName);
        }

        protected override ITypeSymbol CommonGetSubmissionResultType(out bool hasValue)
        {
            return GetSubmissionResultType(out hasValue);
        }

        protected override IAssemblySymbol CommonAssembly
        {
            get { return this.Assembly; }
        }

        protected override INamespaceSymbol CommonGlobalNamespace
        {
            get { return this.GlobalNamespace; }
        }

        protected override CompilationOptions CommonOptions
        {
            get { return options; }
        }

        protected override Compilation CommonPreviousSubmission
        {
            get { return previousSubmission; }
        }

        protected override SemanticModel CommonGetSemanticModel(SyntaxTree syntaxTree)
        {
            return this.GetSemanticModel((SyntaxTree)syntaxTree);
        }

        protected override IEnumerable<SyntaxTree> CommonSyntaxTrees
        {
            get
            {
                return this.SyntaxTrees;
            }
        }

        protected override Compilation CommonAddSyntaxTrees(IEnumerable<SyntaxTree> trees)
        {
            var array = trees as SyntaxTree[];
            if (array != null)
            {
                return this.AddSyntaxTrees(array);
            }

            if (trees == null)
            {
                throw new ArgumentNullException("trees");
            }

            return this.AddSyntaxTrees(trees.Cast<SyntaxTree>());
        }

        protected override Compilation CommonRemoveSyntaxTrees(IEnumerable<SyntaxTree> trees)
        {
            var array = trees as SyntaxTree[];
            if (array != null)
            {
                return this.RemoveSyntaxTrees(array);
            }

            if (trees == null)
            {
                throw new ArgumentNullException("trees");
            }

            return this.RemoveSyntaxTrees(trees.Cast<SyntaxTree>());
        }

        protected override Compilation CommonRemoveAllSyntaxTrees()
        {
            return this.RemoveAllSyntaxTrees();
        }

        protected override Compilation CommonReplaceSyntaxTree(SyntaxTree oldTree, SyntaxTree newTree)
        {
            return this.ReplaceSyntaxTree((SyntaxTree)oldTree, (SyntaxTree)newTree);
        }

        protected override Compilation CommonWithOptions(CompilationOptions options)
        {
            return this.WithOptions((CSharpCompilationOptions)options);
        }

        protected override Compilation CommonWithPreviousSubmission(Compilation newPreviousSubmission)
        {
            return this.WithPreviousSubmission((CSharpCompilation)newPreviousSubmission);
        }

        protected override bool CommonContainsSyntaxTree(SyntaxTree syntaxTree)
        {
            return this.ContainsSyntaxTree((SyntaxTree)syntaxTree);
        }

        protected override ISymbol CommonGetAssemblyOrModuleSymbol(MetadataReference reference)
        {
            return this.GetAssemblyOrModuleSymbol(reference);
        }

        protected override Compilation CommonClone()
        {
            return this.Clone();
        }

        protected override IModuleSymbol CommonSourceModule
        {
            get { return this.SourceModule; }
        }

        protected override INamedTypeSymbol CommonGetSpecialType(SpecialType specialType)
        {
            return this.GetSpecialType(specialType);
        }

        protected override INamespaceSymbol CommonGetCompilationNamespace(INamespaceSymbol namespaceSymbol)
        {
            return this.GetCompilationNamespace(namespaceSymbol);
        }

        protected override INamedTypeSymbol CommonGetTypeByMetadataName(string metadataName)
        {
            return this.GetTypeByMetadataName(metadataName);
        }

        protected override INamedTypeSymbol CommonScriptClass
        {
            get { return this.ScriptClass; }
        }

        protected override IArrayTypeSymbol CommonCreateArrayTypeSymbol(ITypeSymbol elementType, int rank)
        {
            return CreateArrayTypeSymbol(elementType.EnsureCSharpSymbolOrNull<ITypeSymbol, TypeSymbol>("elementType"), rank);
        }

        protected override IPointerTypeSymbol CommonCreatePointerTypeSymbol(ITypeSymbol elementType)
        {
            return CreatePointerTypeSymbol(elementType.EnsureCSharpSymbolOrNull<ITypeSymbol, TypeSymbol>("elementType"));
        }

        protected override ITypeSymbol CommonDynamicType
        {
            get { return DynamicType; }
        }

        protected override INamedTypeSymbol CommonObjectType
        {
            get { return this.ObjectType; }
        }

        protected override MetadataReference CommonGetMetadataReference(IAssemblySymbol assemblySymbol)
        {
            var symbol = assemblySymbol as AssemblySymbol;
            if ((object)symbol != null)
            {
                return this.GetMetadataReference(symbol);
            }
            else
            {
                return null;
            }
        }

        protected override IMethodSymbol CommonGetEntryPoint(CancellationToken cancellationToken)
        {
            return this.GetEntryPoint(cancellationToken);
        }

        internal override int CompareSourceLocations(Location loc1, Location loc2)
        {
            Debug.Assert(loc1.IsInSource);
            Debug.Assert(loc2.IsInSource);

            var comparison = CompareSyntaxTreeOrdering(loc1.SourceTree, loc2.SourceTree);
            if (comparison != 0)
            {
                return comparison;
            }

            return loc1.SourceSpan.Start - loc2.SourceSpan.Start;
        }

        #endregion

        internal void MakeMemberMissing(WellKnownMember member)
        {
            MakeMemberMissing((int)member);
        }

        internal void MakeMemberMissing(SpecialMember member)
        {
            MakeMemberMissing(-(int)member - 1);
        }

        internal bool IsMemberMissing(WellKnownMember member)
        {
            return IsMemberMissing((int)member);
        }

        internal bool IsMemberMissing(SpecialMember member)
        {
            return IsMemberMissing(-(int)member - 1);
        }

        private void MakeMemberMissing(int member)
        {
            if (lazyMakeMemberMissingMap == null)
            {
                lazyMakeMemberMissingMap = new SmallDictionary<int, bool>(); 
            }

            lazyMakeMemberMissingMap[member] = true;
        }

        private bool IsMemberMissing(int member)
        {
            return lazyMakeMemberMissingMap != null && lazyMakeMemberMissingMap.ContainsKey(member);
        }

        internal void SymbolDeclaredEvent(Symbol symbol)
        {
            if (EventQueue != null) EventQueue.Enqueue(new CompilationEvent.SymbolDeclared(this, symbol));
        }
    }
}
