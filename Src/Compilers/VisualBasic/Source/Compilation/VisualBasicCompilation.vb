' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Concurrent
Imports System.Collections.Immutable
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.Instrumentation
Imports Microsoft.CodeAnalysis.InternalUtilities
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Emit
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' The Compilation object is an immutable representation of a single invocation of the
    ''' compiler. Although immutable, a Compilation is also on-demand, in that a compilation can be
    ''' created quickly, but will that compiler parts or all of the code in order to respond to
    ''' method or properties. Also, a compilation can produce a new compilation with a small change
    ''' from the current compilation. This is, in many cases, more efficient than creating a new
    ''' compilation from scratch, as the new compilation can share information from the old
    ''' compilation.
    ''' </summary>
    Public NotInheritable Class VisualBasicCompilation
        Inherits Compilation

        ' !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        '
        ' Changes to the public interface of this class should remain synchronized with the C#
        ' version. Do not make any changes to the public interface without making the corresponding
        ' change to the C# version.
        '
        ' !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        ''' <summary>
        ''' most of time all compilation would use same MyTemplate. no reason to create (reparse) one for each compilation
        ''' as long as its parse option is same
        ''' </summary>
        Private Shared ReadOnly s_myTemplateCache As ConcurrentLruCache(Of VisualBasicParseOptions, SyntaxTree) =
            New ConcurrentLruCache(Of VisualBasicParseOptions, SyntaxTree)(capacity:=5)

        ''' <summary>
        ''' The SourceAssemblySymbol for this compilation. Do not access directly, use Assembly
        ''' property instead. This field is lazily initialized by ReferenceManager,
        ''' ReferenceManager.CacheLockObject must be locked while ReferenceManager "calculates" the
        ''' value and assigns it, several threads must not perform duplicate "calculation"
        ''' simultaneously.
        ''' </summary>
        Private m_lazyAssemblySymbol As SourceAssemblySymbol

        ''' <summary>
        ''' Holds onto data related to reference binding.
        ''' The manager is shared among multiple compilations that we expect to have the same result of reference binding.
        ''' In most cases this can be determined without performing the binding. If the compilation however contains a circular 
        ''' metadata reference (a metadata reference that refers back to the compilation) we need to avoid sharing of the binding results.
        ''' We do so by creating a new reference manager for such compilation. 
        ''' </summary>
        Private m_referenceManager As ReferenceManager

        ''' <summary>
        ''' The options passed to the constructor of the Compilation
        ''' </summary>
        Private ReadOnly m_Options As VisualBasicCompilationOptions

        ''' <summary>
        ''' The global namespace symbol. Lazily populated on first access.
        ''' </summary>
        Private m_lazyGlobalNamespace As NamespaceSymbol

        ''' <summary>
        ''' The syntax trees explicitly given to the compilation at creation, in ordinal order.
        ''' </summary>
        Private ReadOnly m_syntaxTrees As ImmutableArray(Of SyntaxTree)

        ''' <summary>
        ''' The syntax trees of this compilation plus all 'hidden' trees 
        ''' added to the compilation by compiler, e.g. Vb Core Runtime.
        ''' </summary>
        Private m_lazyAllSyntaxTrees As ImmutableArray(Of SyntaxTree)

        ''' <summary>
        ''' A map between syntax trees and the root declarations in the declaration table.
        ''' Incrementally updated between compilation versions when source changes are made.
        ''' </summary>
        Private ReadOnly m_rootNamespaces As ImmutableDictionary(Of SyntaxTree, DeclarationTableEntry)

        ''' <summary>
        ''' Imports appearing in <see cref="SyntaxTree"/>s in this compilation.
        ''' </summary>
        ''' <remarks>
        ''' Unlike in C#, we don't need to use a set because the <see cref="SourceFile"/> objects
        ''' that record the imports are persisted.
        ''' </remarks>
        Private m_lazyImportInfos As ConcurrentQueue(Of ImportInfo)

        ''' <summary>
        ''' Cache the CLS diagnostics for the whole compilation so they aren't computed repeatedly.
        ''' </summary>
        ''' <remarks>
        ''' NOTE: Presently, we do not cache the per-tree diagnostics.
        ''' </remarks>
        Private m_lazyClsComplianceDiagnostics As ImmutableArray(Of Diagnostic)

        ''' <summary>
        ''' A SyntaxTree and the associated RootSingleNamespaceDeclaration for an embedded
        ''' syntax tree in the Compilation. Unlike the entries in m_rootNamespaces, the
        ''' SyntaxTree here is lazy since the tree cannot be evaluated until the references
        ''' have been resolved (as part of binding the source module), and at that point, the
        ''' SyntaxTree may be Nothing if the embedded tree is not needed for the Compilation.
        ''' </summary>
        Private Structure EmbeddedTreeAndDeclaration
            Public ReadOnly Tree As Lazy(Of SyntaxTree)
            Public ReadOnly DeclarationEntry As DeclarationTableEntry

            Public Sub New(treeOpt As Func(Of SyntaxTree), rootNamespaceOpt As Func(Of RootSingleNamespaceDeclaration))
                Me.Tree = New Lazy(Of SyntaxTree)(treeOpt)
                Me.DeclarationEntry = New DeclarationTableEntry(New Lazy(Of RootSingleNamespaceDeclaration)(rootNamespaceOpt), isEmbedded:=True)
            End Sub
        End Structure

        Private ReadOnly m_embeddedTrees As ImmutableArray(Of EmbeddedTreeAndDeclaration)

        ''' <summary>
        ''' The declaration table that holds onto declarations from source. Incrementally updated
        ''' between compilation versions when source changes are made.
        ''' </summary>
        ''' <remarks></remarks>
        Private ReadOnly m_declarationTable As DeclarationTable

        ''' <summary>
        ''' Manages anonymous types declared in this compilation. Unifies types that are structurally equivalent.
        ''' </summary>
        Private ReadOnly m_anonymousTypeManager As AnonymousTypeManager

        ''' <summary>
        ''' Manages automatically embedded content.
        ''' </summary>
        Private m_lazyEmbeddedSymbolManager As EmbeddedSymbolManager

        ''' <summary>
        ''' MyTemplate automatically embedded from resource in the compiler.
        ''' It doesn't feel like it should be managed by EmbeddedSymbolManager
        ''' because MyTemplate is treated as user code, i.e. can be extended via
        ''' partial declarations, doesn't require "on-demand" metadata generation, etc.
        ''' 
        ''' SyntaxTree.Dummy means uninitialized.
        ''' </summary>
        Private m_lazyMyTemplate As SyntaxTree = VisualBasicSyntaxTree.Dummy

        Private ReadOnly m_scriptClass As Lazy(Of ImplicitNamedTypeSymbol)
        Private ReadOnly m_previousSubmission As VisualBasicCompilation

        ''' <summary>
        ''' Contains the main method of this assembly, if there is one.
        ''' </summary>
        Private m_lazyEntryPoint As EntryPoint

        Private Shared AlinkWarnings As ERRID() = {ERRID.WRN_ConflictingMachineAssembly,
                                               ERRID.WRN_RefCultureMismatch,
                                               ERRID.WRN_InvalidVersionFormat}

        Public Overrides ReadOnly Property Language As String
            Get
                Return LanguageNames.VisualBasic
            End Get
        End Property

        Public Overrides ReadOnly Property IsCaseSensitive As Boolean
            Get
                Return False
            End Get
        End Property

        Friend ReadOnly Property Declarations As DeclarationTable
            Get
                Return m_declarationTable
            End Get
        End Property

        Public Shadows ReadOnly Property Options As VisualBasicCompilationOptions
            Get
                Return m_Options
            End Get
        End Property

        Friend ReadOnly Property AnonymousTypeManager As AnonymousTypeManager
            Get
                Return Me.m_anonymousTypeManager
            End Get
        End Property

        ''' <summary>
        ''' SyntaxTree of MyTemplate for the compilation. Settable for testing purposes only.
        ''' </summary>
        Friend Property MyTemplate As SyntaxTree
            Get
                If m_lazyMyTemplate Is VisualBasicSyntaxTree.Dummy Then
                    If Me.Options.EmbedVbCoreRuntime Then
                        m_lazyMyTemplate = Nothing
                    Else
                        ' first see whether we can use one from global cache
                        Dim parseOptions = If(Me.Options.ParseOptions, VisualBasicParseOptions.Default)

                        Dim tree As SyntaxTree = Nothing
                        If s_myTemplateCache.TryGetValue(parseOptions, tree) Then
                            Debug.Assert(tree IsNot Nothing)
                            Debug.Assert(tree IsNot VisualBasicSyntaxTree.Dummy)
                            Debug.Assert(tree.IsMyTemplate)

                            m_lazyMyTemplate = tree
                        Else
                            ' we need to make one.
                            Dim text As String = EmbeddedResources.VbMyTemplateText
                            tree = VisualBasicSyntaxTree.ParseText(text, options:=parseOptions, isMyTemplate:=True)

                            ' set global cache
                            s_myTemplateCache(parseOptions) = tree

                            m_lazyMyTemplate = tree
                        End If
                    End If

                    Debug.Assert(m_lazyMyTemplate Is Nothing OrElse m_lazyMyTemplate.IsMyTemplate)
                End If

                Return m_lazyMyTemplate
            End Get
            Set(value As SyntaxTree)
                Debug.Assert(m_lazyMyTemplate Is VisualBasicSyntaxTree.Dummy)
                Debug.Assert(value IsNot VisualBasicSyntaxTree.Dummy)
                Debug.Assert(value Is Nothing OrElse value.IsMyTemplate)

                m_lazyMyTemplate = value
            End Set
        End Property

        Friend ReadOnly Property EmbeddedSymbolManager As EmbeddedSymbolManager
            Get
                If m_lazyEmbeddedSymbolManager Is Nothing Then
                    Dim embedded = If(Options.EmbedVbCoreRuntime, EmbeddedSymbolKind.VbCore, EmbeddedSymbolKind.None) Or
                                        If(IncludeInternalXmlHelper(), EmbeddedSymbolKind.XmlHelper, EmbeddedSymbolKind.None)
                    If embedded <> EmbeddedSymbolKind.None Then
                        embedded = embedded Or EmbeddedSymbolKind.EmbeddedAttribute
                    End If
                    Interlocked.CompareExchange(m_lazyEmbeddedSymbolManager, New EmbeddedSymbolManager(embedded), Nothing)
                End If
                Return m_lazyEmbeddedSymbolManager
            End Get
        End Property

#Region "Constructors and Factories"
        ''' <summary>
        ''' Create a new compilation from scratch.
        ''' </summary>
        ''' <param name="assemblyName">Simple assembly name.</param>
        ''' <param name="syntaxTrees">The syntax trees with the source code for the new compilation.</param>
        ''' <param name="references">The references for the new compilation.</param>
        ''' <param name="options">The compiler options to use.</param>
        ''' <returns>A new compilation.</returns>
        Public Shared Function Create(
            assemblyName As String,
            Optional syntaxTrees As IEnumerable(Of SyntaxTree) = Nothing,
            Optional references As IEnumerable(Of MetadataReference) = Nothing,
            Optional options As VisualBasicCompilationOptions = Nothing
        ) As VisualBasicCompilation
            Return Create(assemblyName,
                          options,
                          If(syntaxTrees IsNot Nothing, syntaxTrees.Cast(Of SyntaxTree), Nothing),
                          references,
                          previousSubmission:=Nothing,
                          returnType:=Nothing,
                          hostObjectType:=Nothing,
                          isSubmission:=False)
        End Function

        ''' <summary> 
        ''' Creates a new compilation that can be used in scripting. 
        ''' </summary>
        Public Shared Function CreateSubmission(
            assemblyName As String,
            Optional syntaxTree As SyntaxTree = Nothing,
            Optional references As IEnumerable(Of MetadataReference) = Nothing,
            Optional options As VisualBasicCompilationOptions = Nothing,
            Optional previousSubmission As Compilation = Nothing,
            Optional returnType As Type = Nothing,
            Optional hostObjectType As Type = Nothing) As VisualBasicCompilation

            CheckSubmissionOptions(options)

            Dim vbTree = DirectCast(syntaxTree, SyntaxTree)
            Dim vbPrevious = DirectCast(previousSubmission, VisualBasicCompilation)

            Return Create(
                assemblyName,
                If(options, New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
                If((syntaxTree IsNot Nothing), {vbTree}, SpecializedCollections.EmptyEnumerable(Of SyntaxTree)()),
                references,
                vbPrevious,
                returnType,
                hostObjectType,
                isSubmission:=True)
        End Function

        Private Shared Function Create(
            assemblyName As String,
            options As VisualBasicCompilationOptions,
            syntaxTrees As IEnumerable(Of SyntaxTree),
            references As IEnumerable(Of MetadataReference),
            previousSubmission As VisualBasicCompilation,
            returnType As Type,
            hostObjectType As Type,
            isSubmission As Boolean
        ) As VisualBasicCompilation
            If options Is Nothing Then
                options = New VisualBasicCompilationOptions(OutputKind.ConsoleApplication)
            End If

            CheckAssemblyName(assemblyName)

            Dim validatedReferences = ValidateReferences(Of VisualBasicCompilationReference)(references)
            ValidateSubmissionParameters(previousSubmission, returnType, hostObjectType)

            Dim c As VisualBasicCompilation = Nothing
            Dim embeddedTrees = CreateEmbeddedTrees(New Lazy(Of VisualBasicCompilation)(Function() c))
            Dim declMap = ImmutableDictionary.Create(Of SyntaxTree, DeclarationTableEntry)()
            Dim declTable = AddEmbeddedTrees(DeclarationTable.Empty, embeddedTrees)

            c = New VisualBasicCompilation(
                assemblyName,
                options,
                validatedReferences,
                ImmutableArray(Of SyntaxTree).Empty,
                ImmutableDictionary.Create(Of SyntaxTree, Integer)(),
                declMap,
                embeddedTrees,
                declTable,
                previousSubmission,
                returnType,
                hostObjectType,
                isSubmission,
                referenceManager:=Nothing,
                reuseReferenceManager:=False)

            If syntaxTrees IsNot Nothing Then
                c = c.AddSyntaxTrees(syntaxTrees)
            End If

            Debug.Assert(c.m_lazyAssemblySymbol Is Nothing)

            Return c
        End Function

        Private Sub New(
            assemblyName As String,
            options As VisualBasicCompilationOptions,
            references As ImmutableArray(Of MetadataReference),
            syntaxTrees As ImmutableArray(Of SyntaxTree),
            syntaxTreeOrdinalMap As ImmutableDictionary(Of SyntaxTree, Integer),
            rootNamespaces As ImmutableDictionary(Of SyntaxTree, DeclarationTableEntry),
            embeddedTrees As ImmutableArray(Of EmbeddedTreeAndDeclaration),
            declarationTable As DeclarationTable,
            previousSubmission As VisualBasicCompilation,
            submissionReturnType As Type,
            hostObjectType As Type,
            isSubmission As Boolean,
            referenceManager As ReferenceManager,
            reuseReferenceManager As Boolean,
            Optional eventQueue As AsyncQueue(Of CompilationEvent) = Nothing
        )
            MyBase.New(assemblyName, references, submissionReturnType, hostObjectType, isSubmission, syntaxTreeOrdinalMap, eventQueue)

            Using Logger.LogBlock(FunctionId.VisualBasic_Compilation_Create, message:=assemblyName)
                Debug.Assert(rootNamespaces IsNot Nothing)
                Debug.Assert(declarationTable IsNot Nothing)

                Debug.Assert(syntaxTrees.All(Function(tree) syntaxTrees(syntaxTreeOrdinalMap(tree)) Is tree))
                Debug.Assert(syntaxTrees.SetEquals(rootNamespaces.Keys.AsImmutable(), EqualityComparer(Of SyntaxTree).Default))

                m_Options = options
                m_syntaxTrees = syntaxTrees
                m_rootNamespaces = rootNamespaces
                m_embeddedTrees = embeddedTrees
                m_declarationTable = declarationTable
                m_anonymousTypeManager = New AnonymousTypeManager(Me)

                m_scriptClass = New Lazy(Of ImplicitNamedTypeSymbol)(AddressOf BindScriptClass)

                If isSubmission Then
                    Debug.Assert(previousSubmission Is Nothing OrElse previousSubmission.HostObjectType = hostObjectType)
                    m_previousSubmission = previousSubmission
                Else
                    Debug.Assert(previousSubmission Is Nothing AndAlso submissionReturnType Is Nothing AndAlso hostObjectType Is Nothing)
                End If

                If reuseReferenceManager Then
                    referenceManager.AssertCanReuseForCompilation(Me)
                    m_referenceManager = referenceManager
                Else
                    m_referenceManager = New ReferenceManager(MakeSourceAssemblySimpleName(),
                                                              options.AssemblyIdentityComparer,
                                                              If(referenceManager IsNot Nothing, referenceManager.ObservedMetadata, Nothing))
                End If

                Debug.Assert(m_lazyAssemblySymbol Is Nothing)
                If Me.EventQueue IsNot Nothing Then
                    Me.EventQueue.Enqueue(New CompilationEvent.CompilationStarted(Me))
                End If
            End Using
        End Sub

        ''' <summary>
        ''' Create a duplicate of this compilation with different symbol instances
        ''' </summary>
        Public Shadows Function Clone() As VisualBasicCompilation
            Return New VisualBasicCompilation(
                Me.AssemblyName,
                m_Options,
                Me.ExternalReferences,
                m_syntaxTrees,
                Me.syntaxTreeOrdinalMap,
                m_rootNamespaces,
                m_embeddedTrees,
                m_declarationTable,
                m_previousSubmission,
                Me.SubmissionReturnType,
                Me.HostObjectType,
                Me.IsSubmission,
                m_referenceManager,
                reuseReferenceManager:=True,
                eventQueue:=Nothing) ' no event queue when cloning
        End Function

        Private Function UpdateSyntaxTrees(
            syntaxTrees As ImmutableArray(Of SyntaxTree),
            syntaxTreeOrdinalMap As ImmutableDictionary(Of SyntaxTree, Integer),
            rootNamespaces As ImmutableDictionary(Of SyntaxTree, DeclarationTableEntry),
            declarationTable As DeclarationTable,
            referenceDirectivesChanged As Boolean) As VisualBasicCompilation

            Return New VisualBasicCompilation(
                Me.AssemblyName,
                m_Options,
                Me.ExternalReferences,
                syntaxTrees,
                syntaxTreeOrdinalMap,
                rootNamespaces,
                m_embeddedTrees,
                declarationTable,
                m_previousSubmission,
                Me.SubmissionReturnType,
                Me.HostObjectType,
                Me.IsSubmission,
                m_referenceManager,
                reuseReferenceManager:=Not referenceDirectivesChanged)
        End Function

        ''' <summary>
        ''' Creates a new compilation with the specified name.
        ''' </summary>
        Public Shadows Function WithAssemblyName(assemblyName As String) As VisualBasicCompilation
            CheckAssemblyName(assemblyName)

            ' Can't reuse references since the source assembly name changed and the referenced symbols might 
            ' have internals-visible-to relationship with this compilation or they might had a circular reference 
            ' to this compilation.

            Return New VisualBasicCompilation(
                assemblyName,
                Me.Options,
                Me.ExternalReferences,
                m_syntaxTrees,
                Me.syntaxTreeOrdinalMap,
                m_rootNamespaces,
                m_embeddedTrees,
                m_declarationTable,
                m_previousSubmission,
                Me.SubmissionReturnType,
                Me.HostObjectType,
                Me.IsSubmission,
                m_referenceManager,
                reuseReferenceManager:=String.Equals(assemblyName, Me.AssemblyName, StringComparison.Ordinal))
        End Function

        Public Shadows Function WithReferences(ParamArray newReferences As MetadataReference()) As VisualBasicCompilation
            Return WithReferences(DirectCast(newReferences, IEnumerable(Of MetadataReference)))
        End Function

        ''' <summary>
        ''' Creates a new compilation with the specified references.
        ''' </summary>
        ''' <remarks>
        ''' The new <see cref="VisualBasicCompilation"/> will query the given <see cref="MetadataReference"/> for the underlying 
        ''' metadata as soon as the are needed. 
        ''' 
        ''' The New compilation uses whatever metadata is currently being provided by the <see cref="MetadataReference"/>.
        ''' E.g. if the current compilation references a metadata file that has changed since the creation of the compilation
        ''' the New compilation is going to use the updated version, while the current compilation will be using the previous (it doesn't change).
        ''' </remarks>
        Public Shadows Function WithReferences(newReferences As IEnumerable(Of MetadataReference)) As VisualBasicCompilation
            Dim declTable = RemoveEmbeddedTrees(m_declarationTable, m_embeddedTrees)
            Dim c As VisualBasicCompilation = Nothing
            Dim embeddedTrees = CreateEmbeddedTrees(New Lazy(Of VisualBasicCompilation)(Function() c))
            declTable = AddEmbeddedTrees(declTable, embeddedTrees)

            ' References might have changed, don't reuse reference manager.
            ' Don't even reuse observed metadata - let the manager query for the metadata again.

            c = New VisualBasicCompilation(
                Me.AssemblyName,
                Me.Options,
                ValidateReferences(Of VisualBasicCompilationReference)(newReferences),
                m_syntaxTrees,
                Me.syntaxTreeOrdinalMap,
                m_rootNamespaces,
                embeddedTrees,
                declTable,
                m_previousSubmission,
                Me.SubmissionReturnType,
                Me.HostObjectType,
                Me.IsSubmission,
                referenceManager:=Nothing,
                reuseReferenceManager:=False)
            Return c
        End Function

        Public Shadows Function WithOptions(newOptions As VisualBasicCompilationOptions) As VisualBasicCompilation
            If newOptions Is Nothing Then
                Throw New ArgumentNullException("options")
            End If

            Dim c As VisualBasicCompilation = Nothing
            Dim embeddedTrees = m_embeddedTrees
            Dim declTable = m_declarationTable
            Dim declMap = Me.m_rootNamespaces

            If Not String.Equals(Me.Options.RootNamespace, newOptions.RootNamespace, StringComparison.Ordinal) Then
                ' If the root namespace was updated we have to update declaration table 
                ' entries for all the syntax trees of the compilation
                '
                ' NOTE: we use case-sensitive comparison so that the new compilation
                '       gets a root namespace with correct casing

                declMap = ImmutableDictionary.Create(Of SyntaxTree, DeclarationTableEntry)()
                declTable = DeclarationTable.Empty

                embeddedTrees = CreateEmbeddedTrees(New Lazy(Of VisualBasicCompilation)(Function() c))
                declTable = AddEmbeddedTrees(declTable, embeddedTrees)

                Dim discardedReferenceDirectivesChanged As Boolean = False

                For Each tree In m_syntaxTrees
                    AddSyntaxTreeToDeclarationMapAndTable(tree, newOptions, Me.IsSubmission, declMap, declTable, discardedReferenceDirectivesChanged) ' declMap and declTable passed ByRef
                Next

            ElseIf Me.Options.EmbedVbCoreRuntime <> newOptions.EmbedVbCoreRuntime OrElse Me.Options.ParseOptions <> newOptions.ParseOptions Then
                declTable = RemoveEmbeddedTrees(declTable, m_embeddedTrees)
                embeddedTrees = CreateEmbeddedTrees(New Lazy(Of VisualBasicCompilation)(Function() c))
                declTable = AddEmbeddedTrees(declTable, embeddedTrees)
            End If

            c = New VisualBasicCompilation(
                Me.AssemblyName,
                newOptions,
                Me.ExternalReferences,
                m_syntaxTrees,
                Me.syntaxTreeOrdinalMap,
                declMap,
                embeddedTrees,
                declTable,
                m_previousSubmission,
                Me.SubmissionReturnType,
                Me.HostObjectType,
                Me.IsSubmission,
                m_referenceManager,
                reuseReferenceManager:=m_Options.CanReuseCompilationReferenceManager(newOptions))
            Return c
        End Function

        ''' <summary>
        ''' Returns a new compilation with the given compilation set as the previous submission. 
        ''' </summary>
        Friend Shadows Function WithPreviousSubmission(newPreviousSubmission As VisualBasicCompilation) As VisualBasicCompilation
            If Not IsSubmission Then
                Throw New NotSupportedException("Can't have a previousSubmission when not a submission")
            End If

            ' Reference binding doesn't depend on previous submission so we can reuse it.

            Return New VisualBasicCompilation(
                Me.AssemblyName,
                Me.Options,
                Me.ExternalReferences,
                m_syntaxTrees,
                Me.syntaxTreeOrdinalMap,
                m_rootNamespaces,
                m_embeddedTrees,
                m_declarationTable,
                newPreviousSubmission,
                Me.SubmissionReturnType,
                Me.HostObjectType,
                Me.IsSubmission,
                m_referenceManager,
                reuseReferenceManager:=True)
        End Function

        ''' <summary>
        ''' Returns a new compilation with a given event queue. 
        ''' </summary>
        Friend Shadows Function WithEventQueue(eventQueue As AsyncQueue(Of CompilationEvent)) As VisualBasicCompilation
            Return New VisualBasicCompilation(
                Me.AssemblyName,
                Me.Options,
                Me.ExternalReferences,
                m_syntaxTrees,
                Me.syntaxTreeOrdinalMap,
                m_rootNamespaces,
                m_embeddedTrees,
                m_declarationTable,
                m_previousSubmission,
                Me.SubmissionReturnType,
                Me.HostObjectType,
                Me.IsSubmission,
                m_referenceManager,
                reuseReferenceManager:=True,
                eventQueue:=eventQueue)
        End Function

#End Region

#Region "Submission"

        Friend Shadows ReadOnly Property PreviousSubmission As VisualBasicCompilation
            Get
                Return m_previousSubmission
            End Get
        End Property

        ' TODO (tomat): consider moving this method to SemanticModel

        ''' <summary>
        ''' Returns the type of the submission return value.
        ''' </summary>
        ''' <param name="hasValue">
        ''' Whether the submission is considered to have a value. 
        ''' This information can be used for example in a REPL implementation to determine whether to print out the result of a submission execution.
        ''' </param>
        ''' <returns>
        ''' Returns a static type of the expression of the last expression or call statement if there is any,
        ''' a symbol for <see cref="System.Void"/> otherwise.
        ''' </returns>
        ''' <remarks>
        ''' Note that the return type is System.Void for both compilations "System.Console.WriteLine()" and "?System.Console.WriteLine()",
        ''' and <paramref name="hasValue"/> is <c>False</c> for the former and <c>True</c> for the latter.
        ''' </remarks>
        ''' <exception cref="InvalidOperationException">The compilation doesn't represent a submission (<see cref="IsSubmission"/> return false).</exception>
        Friend Shadows Function GetSubmissionResultType(<Out> ByRef hasValue As Boolean) As TypeSymbol
            If Not IsSubmission Then
                Throw New InvalidOperationException(VBResources.CompilationDoesNotRepresentInteractiveSubmission)
            End If

            hasValue = False
            Dim tree = SyntaxTrees.SingleOrDefault()

            ' submission can be empty or comprise of a script file
            If tree Is Nothing OrElse tree.Options.Kind <> SourceCodeKind.Interactive Then
                Return GetSpecialType(SpecialType.System_Void)
            End If

            Dim lastStatement = tree.GetCompilationUnitRoot().Members.LastOrDefault()
            If lastStatement Is Nothing Then
                Return GetSpecialType(SpecialType.System_Void)
            End If

            Dim model = GetSemanticModel(tree)
            Select Case lastStatement.Kind
                Case SyntaxKind.PrintStatement
                    Dim expression = DirectCast(lastStatement, PrintStatementSyntax).Expression
                    Dim info = model.GetTypeInfo(expression)
                    hasValue = True ' always true, even for info.Type = Void
                    Return DirectCast(info.ConvertedType, TypeSymbol)

                Case SyntaxKind.ExpressionStatement
                    Dim expression = DirectCast(lastStatement, ExpressionStatementSyntax).Expression
                    Dim info = model.GetTypeInfo(expression)
                    hasValue = info.Type.SpecialType <> SpecialType.System_Void
                    Return DirectCast(info.ConvertedType, TypeSymbol)

                Case SyntaxKind.CallStatement
                    Dim expression = DirectCast(lastStatement, CallStatementSyntax).Invocation
                    Dim info = model.GetTypeInfo(expression)
                    hasValue = info.Type.SpecialType <> SpecialType.System_Void
                    Return DirectCast(info.ConvertedType, TypeSymbol)

                Case Else
                    Return GetSpecialType(SpecialType.System_Void)
            End Select
        End Function

#End Region

#Region "Syntax Trees"

        ''' <summary>
        ''' Get a read-only list of the syntax trees that this compilation was created with.
        ''' The ordering of the trees is arbitrary and may be different than the order the
        ''' trees were supplied to the compilation.
        ''' </summary>
        Public Shadows ReadOnly Property SyntaxTrees As ImmutableArray(Of SyntaxTree)
            Get
                Return m_syntaxTrees
            End Get
        End Property

        ''' <summary>
        ''' Get a read-only list of the syntax trees that this compilation was created with PLUS
        ''' the trees that were automatically added to it, i.e. Vb Core Runtime tree.
        ''' </summary>
        Friend Shadows ReadOnly Property AllSyntaxTrees As ImmutableArray(Of SyntaxTree)
            Get
                If m_lazyAllSyntaxTrees.IsDefault Then
                    Dim builder = ArrayBuilder(Of SyntaxTree).GetInstance()
                    builder.AddRange(m_rootNamespaces.Keys)
                    For Each embeddedTree In m_embeddedTrees
                        Dim tree = embeddedTree.Tree.Value
                        If tree IsNot Nothing Then
                            builder.Add(tree)
                        End If
                    Next
                    ImmutableInterlocked.InterlockedInitialize(m_lazyAllSyntaxTrees, builder.ToImmutableAndFree())
                End If

                Return m_lazyAllSyntaxTrees
            End Get
        End Property

        ''' <summary>
        ''' Is the passed in syntax tree in this compilation?
        ''' </summary>
        Public Shadows Function ContainsSyntaxTree(syntaxTree As SyntaxTree) As Boolean
            If syntaxTree Is Nothing Then
                Throw New ArgumentNullException("syntaxTree")
            End If

            Dim vbtree = TryCast(syntaxTree, SyntaxTree)
            Return vbtree IsNot Nothing AndAlso m_rootNamespaces.ContainsKey(vbtree)
        End Function

        Public Shadows Function AddSyntaxTrees(ParamArray trees As SyntaxTree()) As VisualBasicCompilation
            Return AddSyntaxTrees(DirectCast(trees, IEnumerable(Of SyntaxTree)))
        End Function

        Public Shadows Function AddSyntaxTrees(trees As IEnumerable(Of SyntaxTree)) As VisualBasicCompilation
            Using Logger.LogBlock(FunctionId.VisualBasic_Compilation_AddSyntaxTrees, message:=Me.AssemblyName)
                If trees Is Nothing Then
                    Throw New ArgumentNullException("trees")
                End If

                If Not trees.Any() Then
                    Return Me
                End If

                ' We're using a try-finally for this builder because there's a test that 
                ' specifically checks for one or more of the argument exceptions below
                ' and we don't want to see console spew (even though we don't generally
                ' care about pool "leaks" in exceptional cases).  Alternatively, we
                ' could create a new ArrayBuilder.
                Dim builder = ArrayBuilder(Of SyntaxTree).GetInstance()
                Try
                    builder.AddRange(m_syntaxTrees)

                    Dim referenceDirectivesChanged = False
                    Dim oldTreeCount = m_syntaxTrees.Length

                    Dim ordinalMap = Me.syntaxTreeOrdinalMap
                    Dim declMap = m_rootNamespaces
                    Dim declTable = m_declarationTable
                    Dim i = 0

                    For Each tree As SyntaxTree In trees
                        If tree Is Nothing Then
                            Throw New ArgumentNullException(String.Format(VBResources.Trees0, i))
                        End If

                        If Not tree.HasCompilationUnitRoot Then
                            Throw New ArgumentException(String.Format(VBResources.TreesMustHaveRootNode, i))
                        End If

                        If tree.IsEmbeddedOrMyTemplateTree() Then
                            Throw New ArgumentException(VBResources.CannotAddCompilerSpecialTree)
                        End If

                        If declMap.ContainsKey(tree) Then
                            Throw New ArgumentException(VBResources.SyntaxTreeAlreadyPresent, String.Format(VBResources.Trees0, i))
                        End If

                        AddSyntaxTreeToDeclarationMapAndTable(tree, m_Options, Me.IsSubmission, declMap, declTable, referenceDirectivesChanged) ' declMap and declTable passed ByRef
                        builder.Add(tree)
                        ordinalMap = ordinalMap.Add(tree, oldTreeCount + i)
                        i += 1
                    Next

                    If IsSubmission AndAlso declMap.Count > 1 Then
                        Throw New ArgumentException(VBResources.SubmissionCanHaveAtMostOneSyntaxTree, "trees")
                    End If

                    Return UpdateSyntaxTrees(builder.ToImmutable(), ordinalMap, declMap, declTable, referenceDirectivesChanged)
                Finally
                    builder.Free()
                End Try
            End Using
        End Function

        Private Shared Sub AddSyntaxTreeToDeclarationMapAndTable(
                tree As SyntaxTree,
                compilationOptions As VisualBasicCompilationOptions,
                isSubmission As Boolean,
                ByRef declMap As ImmutableDictionary(Of SyntaxTree, DeclarationTableEntry),
                ByRef declTable As DeclarationTable,
                ByRef referenceDirectivesChanged As Boolean
            )

            Dim entry = New DeclarationTableEntry(New Lazy(Of RootSingleNamespaceDeclaration)(Function() ForTree(tree, compilationOptions, isSubmission)), isEmbedded:=False)
            declMap = declMap.Add(tree, entry)
            declTable = declTable.AddRootDeclaration(entry)
            referenceDirectivesChanged = referenceDirectivesChanged OrElse tree.HasReferenceDirectives
        End Sub

        Private Shared Function ForTree(tree As SyntaxTree, options As VisualBasicCompilationOptions, isSubmission As Boolean) As RootSingleNamespaceDeclaration
            Return DeclarationTreeBuilder.ForTree(tree, options.GetRootNamespaceParts(), If(options.ScriptClassName, ""), isSubmission)
        End Function

        Public Shadows Function RemoveSyntaxTrees(ParamArray trees As SyntaxTree()) As VisualBasicCompilation
            Return RemoveSyntaxTrees(DirectCast(trees, IEnumerable(Of SyntaxTree)))
        End Function

        Public Shadows Function RemoveSyntaxTrees(trees As IEnumerable(Of SyntaxTree)) As VisualBasicCompilation
            Using Logger.LogBlock(FunctionId.VisualBasic_Compilation_RemoveSyntaxTrees, message:=Me.AssemblyName)
                If trees Is Nothing Then
                    Throw New ArgumentNullException("trees")
                End If

                If Not trees.Any() Then
                    Return Me
                End If

                Dim referenceDirectivesChanged = False
                Dim removeSet As New HashSet(Of SyntaxTree)()
                Dim declMap = m_rootNamespaces
                Dim declTable = m_declarationTable

                For Each tree As SyntaxTree In trees
                    If tree.IsEmbeddedOrMyTemplateTree() Then
                        Throw New ArgumentException(VBResources.CannotRemoveCompilerSpecialTree)
                    End If

                    RemoveSyntaxTreeFromDeclarationMapAndTable(tree, declMap, declTable, referenceDirectivesChanged)
                    removeSet.Add(tree)
                Next

                Debug.Assert(Not removeSet.IsEmpty())

                ' We're going to have to revise the ordinals of all
                ' trees after the first one removed, so just build
                ' a new map.

                ' CONSIDER: an alternative approach would be to set the map to empty and
                ' re-calculate it the next time we need it.  This might save us time in the
                ' case where remove calls are made sequentially (rare?).

                Dim ordinalMap = ImmutableDictionary.Create(Of SyntaxTree, Integer)()
                Dim builder = ArrayBuilder(Of SyntaxTree).GetInstance()
                Dim i = 0

                For Each tree In m_syntaxTrees
                    If Not removeSet.Contains(tree) Then
                        builder.Add(tree)
                        ordinalMap = ordinalMap.Add(tree, i)
                        i += 1
                    End If
                Next

                Return UpdateSyntaxTrees(builder.ToImmutableAndFree(), ordinalMap, declMap, declTable, referenceDirectivesChanged)
            End Using
        End Function

        Private Shared Sub RemoveSyntaxTreeFromDeclarationMapAndTable(
                tree As SyntaxTree,
                ByRef declMap As ImmutableDictionary(Of SyntaxTree, DeclarationTableEntry),
            ByRef declTable As DeclarationTable,
            ByRef referenceDirectivesChanged As Boolean
            )
            Dim root As DeclarationTableEntry = Nothing
            If Not declMap.TryGetValue(tree, root) Then
                Throw New ArgumentException(String.Format(VBResources.SyntaxTreeNotFoundToRemove, tree))
            End If

            declTable = declTable.RemoveRootDeclaration(root)
            declMap = declMap.Remove(tree)
            referenceDirectivesChanged = referenceDirectivesChanged OrElse tree.HasReferenceDirectives
        End Sub

        Public Shadows Function RemoveAllSyntaxTrees() As VisualBasicCompilation
            Return UpdateSyntaxTrees(ImmutableArray(Of SyntaxTree).Empty,
                                     ImmutableDictionary.Create(Of SyntaxTree, Integer)(),
                                     ImmutableDictionary.Create(Of SyntaxTree, DeclarationTableEntry)(),
                                     DeclarationTable.Empty,
                                     referenceDirectivesChanged:=m_declarationTable.ReferenceDirectives.Any())
        End Function

        Public Shadows Function ReplaceSyntaxTree(oldTree As SyntaxTree, newTree As SyntaxTree) As VisualBasicCompilation
            Using Logger.LogBlock(FunctionId.VisualBasic_Compilation_ReplaceSyntaxTree, message:=Me.AssemblyName)
                If oldTree Is Nothing Then
                    Throw New ArgumentNullException("oldSyntaxTree")
                End If

                If newTree Is Nothing Then
                    Return Me.RemoveSyntaxTrees(oldTree)
                ElseIf newTree Is oldTree Then
                    Return Me
                End If

                If Not newTree.HasCompilationUnitRoot Then
                    Throw New ArgumentException(VBResources.TreeMustHaveARootNodeWithCompilationUnit, "newTree")
                End If

                Dim vbOldTree = DirectCast(oldTree, SyntaxTree)
                Dim vbNewTree = DirectCast(newTree, SyntaxTree)

                If vbOldTree.IsEmbeddedOrMyTemplateTree() Then
                    Throw New ArgumentException(VBResources.CannotRemoveCompilerSpecialTree)
                End If

                If vbNewTree.IsEmbeddedOrMyTemplateTree() Then
                    Throw New ArgumentException(VBResources.CannotAddCompilerSpecialTree)
                End If

                Dim declMap = m_rootNamespaces
                Dim declTable = m_declarationTable
                Dim referenceDirectivesChanged = False

                ' TODO(tomat): Consider comparing #r's of the old and the new tree. If they are exactly the same we could still reuse.
                ' This could be a perf win when editing a script file in the IDE. The services create a new compilation every keystroke 
                ' that replaces the tree with a new one.

                RemoveSyntaxTreeFromDeclarationMapAndTable(vbOldTree, declMap, declTable, referenceDirectivesChanged)
                AddSyntaxTreeToDeclarationMapAndTable(vbNewTree, m_Options, Me.IsSubmission, declMap, declTable, referenceDirectivesChanged)

                Dim ordinalMap = Me.syntaxTreeOrdinalMap

                Debug.Assert(ordinalMap.ContainsKey(oldTree)) ' Checked by RemoveSyntaxTreeFromDeclarationMapAndTable
                Dim oldOrdinal = ordinalMap(oldTree)

                Dim newArray = m_syntaxTrees.ToArray()
                newArray(oldOrdinal) = vbNewTree

                ' CONSIDER: should this be an operation on ImmutableDictionary?
                ordinalMap = ordinalMap.Remove(oldTree)
                ordinalMap = ordinalMap.Add(newTree, oldOrdinal)

                Return UpdateSyntaxTrees(newArray.AsImmutableOrNull(), ordinalMap, declMap, declTable, referenceDirectivesChanged)
            End Using
        End Function

        Private Shared Function CreateEmbeddedTrees(compReference As Lazy(Of VisualBasicCompilation)) As ImmutableArray(Of EmbeddedTreeAndDeclaration)
            Return ImmutableArray.Create(Of EmbeddedTreeAndDeclaration)(
                New EmbeddedTreeAndDeclaration(
                    Function()
                        Dim compilation = compReference.Value
                        Return If(compilation.Options.EmbedVbCoreRuntime Or compilation.IncludeInternalXmlHelper,
                                  EmbeddedSymbolManager.EmbeddedSyntax,
                                  Nothing)
                    End Function,
                    Function()
                        Dim compilation = compReference.Value
                        Return If(compilation.Options.EmbedVbCoreRuntime Or compilation.IncludeInternalXmlHelper,
                                  ForTree(VisualBasic.Symbols.EmbeddedSymbolManager.EmbeddedSyntax, compilation.Options, compilation.IsSubmission),
                                  Nothing)
                    End Function),
                New EmbeddedTreeAndDeclaration(
                    Function()
                        Dim compilation = compReference.Value
                        Return If(compilation.Options.EmbedVbCoreRuntime,
                                  EmbeddedSymbolManager.VbCoreSyntaxTree,
                                  Nothing)
                    End Function,
                    Function()
                        Dim compilation = compReference.Value
                        Return If(compilation.Options.EmbedVbCoreRuntime,
                                  ForTree(VisualBasic.Symbols.EmbeddedSymbolManager.VbCoreSyntaxTree, compilation.Options, compilation.IsSubmission),
                                  Nothing)
                    End Function),
                New EmbeddedTreeAndDeclaration(
                    Function()
                        Dim compilation = compReference.Value
                        Return If(compilation.IncludeInternalXmlHelper(),
                                  EmbeddedSymbolManager.InternalXmlHelperSyntax,
                                  Nothing)
                    End Function,
                    Function()
                        Dim compilation = compReference.Value
                        Return If(compilation.IncludeInternalXmlHelper(),
                                  ForTree(VisualBasic.Symbols.EmbeddedSymbolManager.InternalXmlHelperSyntax, compilation.Options, compilation.IsSubmission),
                                  Nothing)
                    End Function),
                New EmbeddedTreeAndDeclaration(
                    Function()
                        Dim compilation = compReference.Value
                        Return compilation.MyTemplate
                    End Function,
                    Function()
                        Dim compilation = compReference.Value
                        Return If(compilation.MyTemplate IsNot Nothing,
                                  ForTree(compilation.MyTemplate, compilation.Options, compilation.IsSubmission),
                                  Nothing)
                    End Function))
        End Function

        Private Shared Function AddEmbeddedTrees(
            declTable As DeclarationTable,
            embeddedTrees As ImmutableArray(Of EmbeddedTreeAndDeclaration)
        ) As DeclarationTable

            For Each embeddedTree In embeddedTrees
                declTable = declTable.AddRootDeclaration(embeddedTree.DeclarationEntry)
            Next
            Return declTable
        End Function

        Private Shared Function RemoveEmbeddedTrees(
            declTable As DeclarationTable,
            embeddedTrees As ImmutableArray(Of EmbeddedTreeAndDeclaration)
        ) As DeclarationTable

            For Each embeddedTree In embeddedTrees
                declTable = declTable.RemoveRootDeclaration(embeddedTree.DeclarationEntry)
            Next
            Return declTable
        End Function

        ''' <summary>
        ''' Returns True if the set of references contains those assemblies needed for XML
        ''' literals (specifically System.Core.dll, System.Xml.dll, and System.Xml.Linq.dll).
        ''' If those assemblies are included, we should include the InternalXmlHelper
        ''' SyntaxTree in the Compilation so the helper methods are available for binding XML.
        ''' </summary>
        Private Function IncludeInternalXmlHelper() As Boolean
            Dim includesSystemCore = False
            Dim includesSystemXml = False
            Dim includesSystemXmlLinq = False

            For Each referencedAssembly In GetBoundReferenceManager().ReferencedAssembliesMap.Values
                ' Use AssemblySymbol.Name rather than AssemblyIdentity.Name
                ' since the latter involves binding of the assembly attributes (in case of source assembly).
                ' The name comparison is case-sensitive which should be sufficient
                ' since we're comparing against the name in the assembly.
                Select Case referencedAssembly.Symbol.Name
                    Case "System.Core"
                        includesSystemCore = True
                    Case "System.Xml"
                        includesSystemXml = True
                    Case "System.Xml.Linq"
                        includesSystemXmlLinq = True
                End Select
            Next

            Return includesSystemCore AndAlso includesSystemXml AndAlso includesSystemXmlLinq
        End Function

        ' TODO: This comparison probably will change to compiler command line order, or at least needs 
        ' TODO: to be resolved. See bug 8520.

        ''' <summary>
        ''' Compare two source locations, using their containing trees, and then by Span.First within a tree. 
        ''' Can be used to get a total ordering on declarations, for example.
        ''' </summary>
        Friend Overrides Function CompareSourceLocations(first As Location, second As Location) As Integer
            Return LexicalSortKey.Compare(first, second, Me)
        End Function

#End Region

#Region "References"
        Friend Overrides Function CommonGetBoundReferenceManager() As CommonReferenceManager
            Return GetBoundReferenceManager()
        End Function

        Friend Shadows Function GetBoundReferenceManager() As ReferenceManager
            If m_lazyAssemblySymbol Is Nothing Then
                m_referenceManager.CreateSourceAssemblyForCompilation(Me)
                Debug.Assert(m_lazyAssemblySymbol IsNot Nothing)
            End If

            ' referenceManager can only be accessed after we initialized the lazyAssemblySymbol.
            ' In fact, initialization of the assembly symbol might change the reference manager.
            Return m_referenceManager
        End Function

        ' for testing only:
        Friend Function ReferenceManagerEquals(other As VisualBasicCompilation) As Boolean
            Return m_referenceManager Is other.m_referenceManager
        End Function

        Public Overrides ReadOnly Property DirectiveReferences As ImmutableArray(Of MetadataReference)
            Get
                Return GetBoundReferenceManager().DirectiveReferences
            End Get
        End Property

        Friend Overrides ReadOnly Property ReferenceDirectiveMap As IDictionary(Of String, MetadataReference)
            Get
                Return GetBoundReferenceManager().ReferenceDirectiveMap
            End Get
        End Property

        ''' <summary>
        ''' Gets the <see cref="AssemblySymbol"/> or <see cref="ModuleSymbol"/> for a metadata reference used to create this compilation.
        ''' </summary>
        ''' <returns><see cref="AssemblySymbol"/> or <see cref="ModuleSymbol"/> corresponding to the given reference or Nothing if there is none.</returns>
        ''' <remarks>
        ''' Uses object identity when comparing two references. 
        ''' </remarks>
        Friend Shadows Function GetAssemblyOrModuleSymbol(reference As MetadataReference) As Symbol
            If (reference Is Nothing) Then
                Throw New ArgumentNullException("reference")
            End If

            If reference.Properties.Kind = MetadataImageKind.Assembly Then
                Return GetBoundReferenceManager().GetReferencedAssemblySymbol(reference)
            Else
                Debug.Assert(reference.Properties.Kind = MetadataImageKind.Module)
                Dim index As Integer = GetBoundReferenceManager().GetReferencedModuleIndex(reference)
                Return If(index < 0, Nothing, Me.Assembly.Modules(index))
            End If
        End Function

        ''' <summary>
        ''' Gets the <see cref="MetadataReference"/> that corresponds to the assembly symbol.
        ''' </summary>
        Friend Shadows Function GetMetadataReference(assemblySymbol As AssemblySymbol) As MetadataReference
            Return Me.GetBoundReferenceManager().ReferencedAssembliesMap.Where(Function(kvp) kvp.Value.Symbol Is assemblySymbol).Select(Function(kvp) kvp.Key).FirstOrDefault()
        End Function

        Public Overrides ReadOnly Property ReferencedAssemblyNames As IEnumerable(Of AssemblyIdentity)
            Get
                Return [Assembly].Modules.SelectMany(Function(m) m.GetReferencedAssemblies())
            End Get
        End Property

        Friend Overrides ReadOnly Property ReferenceDirectives As IEnumerable(Of ReferenceDirective)
            Get
                Return m_declarationTable.ReferenceDirectives
            End Get
        End Property

        Public Overrides Function ToMetadataReference(Optional aliases As ImmutableArray(Of String) = Nothing, Optional embedInteropTypes As Boolean = False) As CompilationReference
            Return New VisualBasicCompilationReference(Me, aliases, embedInteropTypes)
        End Function

        Public Shadows Function AddReferences(ParamArray references As MetadataReference()) As VisualBasicCompilation
            Return DirectCast(MyBase.AddReferences(references), VisualBasicCompilation)
        End Function

        Public Shadows Function AddReferences(references As IEnumerable(Of MetadataReference)) As VisualBasicCompilation
            Return DirectCast(MyBase.AddReferences(references), VisualBasicCompilation)
        End Function

        Public Shadows Function RemoveReferences(ParamArray references As MetadataReference()) As VisualBasicCompilation
            Return DirectCast(MyBase.RemoveReferences(references), VisualBasicCompilation)
        End Function

        Public Shadows Function RemoveReferences(references As IEnumerable(Of MetadataReference)) As VisualBasicCompilation
            Return DirectCast(MyBase.RemoveReferences(references), VisualBasicCompilation)
        End Function

        Public Shadows Function RemoveAllReferences() As VisualBasicCompilation
            Return DirectCast(MyBase.RemoveAllReferences(), VisualBasicCompilation)
        End Function

        Public Shadows Function ReplaceReference(oldReference As MetadataReference, newReference As MetadataReference) As VisualBasicCompilation
            Return DirectCast(MyBase.ReplaceReference(oldReference, newReference), VisualBasicCompilation)
        End Function

#End Region

#Region "Symbols"

        Friend ReadOnly Property SourceAssembly As SourceAssemblySymbol
            Get
                GetBoundReferenceManager()
                Return m_lazyAssemblySymbol
            End Get
        End Property

        ''' <summary>
        ''' Gets the AssemblySymbol that represents the assembly being created.
        ''' </summary>
        Friend Shadows ReadOnly Property Assembly As AssemblySymbol
            Get
                Return Me.SourceAssembly
            End Get
        End Property

        ''' <summary>
        ''' Get a ModuleSymbol that refers to the module being created by compiling all of the code. By
        ''' getting the GlobalNamespace property of that module, all of the namespace and types defined in source code 
        ''' can be obtained.
        ''' </summary>
        Friend Shadows ReadOnly Property SourceModule As ModuleSymbol
            Get
                Return Me.Assembly.Modules(0)
            End Get
        End Property

        ''' <summary>
        ''' Gets the merged root namespace that contains all namespaces and types defined in source code or in 
        ''' referenced metadata, merged into a single namespace hierarchy. This namespace hierarchy is how the compiler
        ''' binds types that are referenced in code.
        ''' </summary>
        Friend Shadows ReadOnly Property GlobalNamespace As NamespaceSymbol
            Get
                If m_lazyGlobalNamespace Is Nothing Then
                    Using Logger.LogBlock(FunctionId.VisualBasic_Compilation_GetGlobalNamespace, message:=Me.AssemblyName)
                        Interlocked.CompareExchange(m_lazyGlobalNamespace, MergedNamespaceSymbol.CreateGlobalNamespace(Me), Nothing)
                    End Using
                End If

                Return m_lazyGlobalNamespace
            End Get
        End Property

        ''' <summary>
        ''' Get the "root" or default namespace that all source types are declared inside. This may be the 
        ''' global namespace or may be another namespace. 
        ''' </summary>
        Friend ReadOnly Property RootNamespace As NamespaceSymbol
            Get
                Return DirectCast(Me.SourceModule, SourceModuleSymbol).RootNamespace
            End Get
        End Property

        ''' <summary>
        ''' Given a namespace symbol, returns the corresponding namespace symbol with Compilation extent
        ''' that refers to that namespace in this compilation. Returns Nothing if there is no corresponding 
        ''' namespace. This should not occur if the namespace symbol came from an assembly referenced by this
        ''' compilation. 
        ''' </summary>
        Friend Shadows Function GetCompilationNamespace(namespaceSymbol As INamespaceSymbol) As NamespaceSymbol
            If namespaceSymbol Is Nothing Then
                Throw New ArgumentNullException("namespaceSymbol")
            End If

            Dim vbNs = TryCast(namespaceSymbol, NamespaceSymbol)
            If vbNs IsNot Nothing AndAlso vbNs.Extent.Kind = NamespaceKind.Compilation AndAlso vbNs.Extent.Compilation Is Me Then
                ' If we already have a namespace with the right extent, use that.
                Return vbNs
            ElseIf namespaceSymbol.ContainingNamespace Is Nothing Then
                ' If is the root namespace, return the merged root namespace
                Debug.Assert(namespaceSymbol.Name = "", "Namespace with Nothing container should be root namespace with empty name")
                Return GlobalNamespace
            Else
                Dim containingNs = GetCompilationNamespace(namespaceSymbol.ContainingNamespace)
                If containingNs Is Nothing Then
                    Return Nothing
                End If

                ' Get the child namespace of the given name, if any.
                Return containingNs.GetMembers(namespaceSymbol.Name).OfType(Of NamespaceSymbol)().FirstOrDefault()
            End If
        End Function

        Friend Shadows Function GetEntryPoint(cancellationToken As CancellationToken) As MethodSymbol
            Dim entryPoint As EntryPoint = GetEntryPointAndDiagnostics(cancellationToken)
            Return If(entryPoint Is Nothing, Nothing, entryPoint.MethodSymbol)
        End Function

        Friend Function GetEntryPointAndDiagnostics(cancellationToken As CancellationToken) As EntryPoint
            If Not Me.Options.OutputKind.IsApplication() Then
                Return Nothing
            End If

            Debug.Assert(Not Me.IsSubmission)

            If Me.Options.MainTypeName IsNot Nothing AndAlso Not Me.Options.MainTypeName.IsValidClrTypeName() Then
                Debug.Assert(Not Me.Options.Errors.IsDefaultOrEmpty)
                Return New EntryPoint(Nothing, ImmutableArray(Of Diagnostic).Empty)
            End If

            If m_lazyEntryPoint Is Nothing Then
                Dim entryPoint As MethodSymbol = Nothing
                Dim diagnostics As ImmutableArray(Of Diagnostic) = Nothing
                FindEntryPoint(cancellationToken, entryPoint, diagnostics)

                Interlocked.CompareExchange(m_lazyEntryPoint, New EntryPoint(entryPoint, diagnostics), Nothing)
            End If

            Return m_lazyEntryPoint
        End Function

        Private Sub FindEntryPoint(cancellationToken As CancellationToken, ByRef entryPoint As MethodSymbol, ByRef sealedDiagnostics As ImmutableArray(Of Diagnostic))
            Using Logger.LogBlock(FunctionId.VisualBasic_Compilation_FindEntryPoint, message:=Me.AssemblyName, cancellationToken:=cancellationToken)
                Dim diagnostics As DiagnosticBag = DiagnosticBag.GetInstance()

                Try
                    entryPoint = Nothing

                    Dim entryPointCandidates As ArrayBuilder(Of MethodSymbol)
                    Dim mainType As SourceMemberContainerTypeSymbol

                    Dim mainTypeName As String = Me.Options.MainTypeName
                    Dim globalNamespace As NamespaceSymbol = Me.SourceModule.GlobalNamespace

                    Dim errorTarget As Object

                    If mainTypeName IsNot Nothing Then
                        ' Global code is the entry point, ignore all other Mains.
                        ' TODO: don't special case scripts (DevDiv #13119).
                        If Me.ScriptClass IsNot Nothing Then
                            ' CONSIDER: we could use the symbol instead of just the name.
                            diagnostics.Add(ERRID.WRN_MainIgnored, NoLocation.Singleton, mainTypeName)
                            Return
                        End If

                        Dim mainTypeOrNamespace = globalNamespace.GetNamespaceOrTypeByQualifiedName(mainTypeName.Split("."c)).OfType(Of NamedTypeSymbol)().OfMinimalArity()
                        If mainTypeOrNamespace Is Nothing Then
                            diagnostics.Add(ERRID.ERR_StartupCodeNotFound1, NoLocation.Singleton, mainTypeName)
                            Return
                        End If

                        mainType = TryCast(mainTypeOrNamespace, SourceMemberContainerTypeSymbol)
                        If mainType Is Nothing OrElse (mainType.TypeKind <> TypeKind.Class AndAlso mainType.TypeKind <> TypeKind.Structure AndAlso mainType.TypeKind <> TypeKind.Module) Then
                            diagnostics.Add(ERRID.ERR_StartupCodeNotFound1, NoLocation.Singleton, mainType)
                            Return
                        End If

                        ' Dev10 reports ERR_StartupCodeNotFound1 but that doesn't make much sense
                        If mainType.IsGenericType Then
                            diagnostics.Add(ERRID.ERR_GenericSubMainsFound1, NoLocation.Singleton, mainType)
                            Return
                        End If

                        errorTarget = mainType

                        ' NOTE: unlike in C#, we're not going search the member list of mainType directly.
                        ' Instead, we're going to mimic dev10's behavior by doing a lookup for "Main",
                        ' starting in mainType.  Among other things, this implies that the entrypoint
                        ' could be in a base class and that it could be hidden by a non-method member
                        ' named "Main".

                        Dim binder As Binder = BinderBuilder.CreateBinderForType(mainType.ContainingSourceModule, mainType.SyntaxReferences(0).SyntaxTree, mainType)
                        Dim lookupResult As LookupResult = LookupResult.GetInstance()
                        Dim entryPointLookupOptions As LookupOptions = LookupOptions.AllMethodsOfAnyArity Or LookupOptions.IgnoreExtensionMethods
                        binder.LookupMember(lookupResult, mainType, WellKnownMemberNames.EntryPointMethodName, arity:=0, options:=entryPointLookupOptions, useSiteDiagnostics:=Nothing)

                        If (Not lookupResult.IsGoodOrAmbiguous) OrElse lookupResult.Symbols(0).Kind <> SymbolKind.Method Then
                            diagnostics.Add(ERRID.ERR_StartupCodeNotFound1, NoLocation.Singleton, mainType)
                            lookupResult.Free()
                            Return
                        End If

                        entryPointCandidates = ArrayBuilder(Of MethodSymbol).GetInstance()
                        For Each candidate In lookupResult.Symbols
                            ' The entrypoint cannot be in another assembly.
                            ' NOTE: filter these out here, rather than below, so that we
                            ' report "not found", rather than "invalid", as in dev10.
                            If candidate.ContainingAssembly = Me.Assembly Then
                                entryPointCandidates.Add(DirectCast(candidate, MethodSymbol))
                            End If
                        Next

                        lookupResult.Free()

                        ' NOTE: Any return after this point must free entryPointCandidates.
                    Else
                        mainType = Nothing

                        errorTarget = Me.AssemblyName

                        entryPointCandidates = ArrayBuilder(Of MethodSymbol).GetInstance()
                        EntryPointCandidateFinder.FindCandidatesInNamespace(globalNamespace, entryPointCandidates, cancellationToken)

                        ' NOTE: Any return after this point must free entryPointCandidates.

                        ' Global code is the entry point, ignore all other Mains.
                        If Me.ScriptClass IsNot Nothing Then
                            For Each main In entryPointCandidates
                                diagnostics.Add(ERRID.WRN_MainIgnored, main.Locations.First(), main)
                            Next

                            entryPointCandidates.Free()
                            Return
                        End If
                    End If

                    If entryPointCandidates.Count = 0 Then
                        diagnostics.Add(ERRID.ERR_StartupCodeNotFound1, NoLocation.Singleton, errorTarget)
                        entryPointCandidates.Free()
                        Return
                    End If

                    Dim hasViableGenericEntryPoints As Boolean = False
                    Dim viableEntryPoints = ArrayBuilder(Of MethodSymbol).GetInstance()

                    ' NOTE: Any return after this point must free viableEntryPoints (and entryPointCandidates).

                    For Each candidate In entryPointCandidates
                        If Not candidate.IsViableMainMethod Then
                            Continue For
                        End If

                        If candidate.IsGenericMethod OrElse candidate.ContainingType.IsGenericType Then
                            hasViableGenericEntryPoints = True
                        Else
                            viableEntryPoints.Add(candidate)
                        End If
                    Next

                    If viableEntryPoints.Count = 0 Then
                        If hasViableGenericEntryPoints Then
                            diagnostics.Add(ERRID.ERR_GenericSubMainsFound1, NoLocation.Singleton, errorTarget)
                        Else
                            diagnostics.Add(ERRID.ERR_InValidSubMainsFound1, NoLocation.Singleton, errorTarget)
                        End If
                    ElseIf viableEntryPoints.Count > 1 Then
                        viableEntryPoints.Sort(LexicalOrderSymbolComparer.Instance)
                        diagnostics.Add(ERRID.ERR_MoreThanOneValidMainWasFound2,
                                        NoLocation.Singleton,
                                        Me.AssemblyName,
                                        New FormattedSymbolList(viableEntryPoints.ToArray(), CustomSymbolDisplayFormatter.ErrorMessageFormatNoModifiersNoReturnType))
                    Else
                        entryPoint = viableEntryPoints(0)

                        If entryPoint.IsAsync Then
                            ' The rule we follow:
                            ' First determine the Sub Main using pre-async rules, and give the pre-async errors if there were 0 or >1 results
                            ' If there was exactly one result, but it was async, then give an error. Otherwise proceed.
                            ' This doesn't follow the same pattern as "error due to being generic". That's because
                            ' maybe one day we'll want to allow Async Sub Main but without breaking back-compat.                    
                            Dim sourceMethod = TryCast(entryPoint, SourceMemberMethodSymbol)
                            Debug.Assert(sourceMethod IsNot Nothing)

                            If sourceMethod IsNot Nothing Then
                                Dim location As Location = sourceMethod.NonMergedLocation
                                Debug.Assert(location IsNot Nothing)

                                If location IsNot Nothing Then
                                    Binder.ReportDiagnostic(diagnostics, location, ERRID.ERR_AsyncSubMain)
                                End If
                            End If
                        End If
                    End If

                    entryPointCandidates.Free()
                    viableEntryPoints.Free()

                Finally
                    sealedDiagnostics = diagnostics.ToReadOnlyAndFree()
                End Try
            End Using
        End Sub

        Friend Class EntryPoint
            Public ReadOnly MethodSymbol As MethodSymbol
            Public ReadOnly Diagnostics As ImmutableArray(Of Diagnostic)

            Public Sub New(methodSymbol As MethodSymbol, diagnostics As ImmutableArray(Of Diagnostic))
                Me.MethodSymbol = methodSymbol
                Me.Diagnostics = diagnostics
            End Sub
        End Class

        ''' <summary>
        ''' Returns the list of member imports that apply to all syntax trees in this compilation.
        ''' </summary>
        Friend ReadOnly Property MemberImports As ImmutableArray(Of NamespaceOrTypeSymbol)
            Get
                Return DirectCast(Me.SourceModule, SourceModuleSymbol).MemberImports.SelectAsArray(Function(m) m.NamespaceOrType)
            End Get
        End Property

        ''' <summary>
        ''' Returns the list of alias imports that apply to all syntax trees in this compilation.
        ''' </summary>
        Friend ReadOnly Property AliasImports As ImmutableArray(Of AliasSymbol)
            Get
                Return DirectCast(Me.SourceModule, SourceModuleSymbol).AliasImports.SelectAsArray(Function(a) a.Alias)
            End Get
        End Property

        Friend Sub ReportUnusedImports(filterTree As SyntaxTree, diagnostics As DiagnosticBag, cancellationToken As CancellationToken)
            If m_lazyImportInfos Is Nothing Then
                Return
            End If

            Dim unusedBuilder As ArrayBuilder(Of TextSpan) = Nothing

            For Each info As ImportInfo In m_lazyImportInfos
                cancellationToken.ThrowIfCancellationRequested()

                Dim infoTree As SyntaxTree = info.Tree
                If filterTree Is Nothing OrElse filterTree Is infoTree Then
                    Dim clauseSpans = info.ClauseSpans
                    Dim numClauseSpans = clauseSpans.Length

                    If numClauseSpans = 1 Then
                        ' Do less work in common case (one clause per statement).
                        If Not Me.IsImportDirectiveUsed(infoTree, clauseSpans(0).Start) Then
                            diagnostics.Add(ERRID.INF_UnusedImportStatement, infoTree.GetLocation(info.StatementSpan))
                        End If
                    Else
                        If unusedBuilder IsNot Nothing Then
                            unusedBuilder.Clear()
                        End If

                        For Each clauseSpan In info.ClauseSpans
                            If Not Me.IsImportDirectiveUsed(infoTree, clauseSpan.Start) Then
                                If unusedBuilder Is Nothing Then
                                    unusedBuilder = ArrayBuilder(Of TextSpan).GetInstance()
                                End If
                                unusedBuilder.Add(clauseSpan)
                            End If
                        Next

                        If unusedBuilder IsNot Nothing AndAlso unusedBuilder.Count > 0 Then
                            If unusedBuilder.Count = numClauseSpans Then
                                diagnostics.Add(ERRID.INF_UnusedImportStatement, infoTree.GetLocation(info.StatementSpan))
                            Else
                                For Each clauseSpan In unusedBuilder
                                    diagnostics.Add(ERRID.INF_UnusedImportClause, infoTree.GetLocation(clauseSpan))
                                Next
                            End If
                        End If
                    End If
                End If
            Next

            If unusedBuilder IsNot Nothing Then
                unusedBuilder.Free()
            End If
        End Sub

        Friend Sub RecordImports(syntax As ImportsStatementSyntax)
            LazyInitializer.EnsureInitialized(m_lazyImportInfos).Enqueue(New ImportInfo(syntax))
        End Sub

        Private Structure ImportInfo
            Public ReadOnly Tree As SyntaxTree
            Public ReadOnly StatementSpan As TextSpan
            Public ReadOnly ClauseSpans As ImmutableArray(Of TextSpan)

            ' CONSIDER: ClauseSpans will usually be a singleton.  If we're
            ' creating too much garbage, it might be worthwhile to store
            ' a single clause span in a separate field.

            Public Sub New(syntax As ImportsStatementSyntax)
                Me.Tree = syntax.SyntaxTree
                Me.StatementSpan = syntax.Span

                Dim builder = ArrayBuilder(Of TextSpan).GetInstance()

                For Each clause In syntax.ImportsClauses
                    builder.Add(clause.Span)
                Next

                Me.ClauseSpans = builder.ToImmutableAndFree()
            End Sub

        End Structure

        Friend ReadOnly Property DeclaresTheObjectClass As Boolean
            Get
                Return SourceAssembly.DeclaresTheObjectClass
            End Get
        End Property

        Friend Function MightContainNoPiaLocalTypes() As Boolean
            Return SourceAssembly.MightContainNoPiaLocalTypes()
        End Function

        ' NOTE(cyrusn): There is a bit of a discoverability problem with this method and the same
        ' named method in SyntaxTreeSemanticModel.  Technically, i believe these are the appropriate
        ' locations for these methods.  This method has no dependencies on anything but the
        ' compilation, while the other method needs a bindings object to determine what bound node
        ' an expression syntax binds to.  Perhaps when we document these methods we should explain
        ' where a user can find the other.

        ''' <summary>
        ''' Determine what kind of conversion, if any, there is between the types 
        ''' "source" and "destination".
        ''' </summary>
        Public Shadows Function ClassifyConversion(source As ITypeSymbol, destination As ITypeSymbol) As Conversion
            Using Logger.LogBlock(FunctionId.VisualBasic_Compilation_ClassifyConversion, message:=Me.AssemblyName)
                If source Is Nothing Then
                    Throw New ArgumentNullException("source")
                End If

                If destination Is Nothing Then
                    Throw New ArgumentNullException("destination")
                End If

                Dim vbsource = source.EnsureVbSymbolOrNothing(Of TypeSymbol)("source")
                Dim vbdest = destination.EnsureVbSymbolOrNothing(Of TypeSymbol)("destination")

                If vbsource.IsErrorType() OrElse vbdest.IsErrorType() Then
                    Return New Conversion(Nothing) ' No conversion
                End If

                Return New Conversion(Conversions.ClassifyConversion(vbsource, vbdest, Nothing))
            End Using
        End Function

        Friend Function GetSubmissionReturnType() As TypeSymbol
            If IsSubmission AndAlso ScriptClass IsNot Nothing Then
                Return (DirectCast(ScriptClass.GetMembers(WellKnownMemberNames.InstanceConstructorName)(0), MethodSymbol)).Parameters(1).Type
            Else
                Return Nothing
            End If
        End Function

        ''' <summary>
        ''' A symbol representing the implicit Script class. This is null if the class is not
        ''' defined in the compilation.
        ''' </summary>
        Friend Shadows ReadOnly Property ScriptClass As NamedTypeSymbol
            Get
                Return SourceScriptClass
            End Get
        End Property

        Friend ReadOnly Property SourceScriptClass As ImplicitNamedTypeSymbol
            Get
                Return m_scriptClass.Value
            End Get
        End Property

        ''' <summary>
        ''' Resolves a symbol that represents script container (Script class). 
        ''' Uses the full name of the container class stored in <see cref="P:CompilationOptions.ScriptClassName"/>  to find the symbol.
        ''' </summary> 
        ''' <returns>
        ''' The Script class symbol or null if it is not defined.
        ''' </returns>
        Private Function BindScriptClass() As ImplicitNamedTypeSymbol
            If Options.ScriptClassName Is Nothing OrElse Not Options.ScriptClassName.IsValidClrTypeName() Then
                Return Nothing
            End If

            Dim namespaceOrType = Me.Assembly.GlobalNamespace.GetNamespaceOrTypeByQualifiedName(Options.ScriptClassName.Split("."c)).AsSingleton()
            Return TryCast(namespaceOrType, ImplicitNamedTypeSymbol)
        End Function

        ''' <summary>
        ''' Get symbol for predefined type from Cor Library referenced by this compilation.
        ''' </summary>
        ''' <param name="typeId"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shadows Function GetSpecialType(typeId As SpecialType) As NamedTypeSymbol
            Dim result = Assembly.GetSpecialType(typeId)
            Debug.Assert(result.SpecialType = typeId)
            Return result
        End Function

        ''' <summary>
        ''' Get symbol for predefined type member from Cor Library referenced by this compilation.
        ''' </summary>
        Friend Shadows Function GetSpecialTypeMember(memberId As SpecialMember) As Symbol
            Return Assembly.GetSpecialTypeMember(memberId)
        End Function

        ''' <summary>
        ''' Lookup a type within the compilation's assembly and all referenced assemblies
        ''' using its canonical CLR metadata name (names are compared case-sensitively).
        ''' </summary>
        ''' <param name="fullyQualifiedMetadataName">
        ''' </param>
        ''' <returns>
        ''' Symbol for the type or null if type cannot be found or is ambiguous. 
        ''' </returns>
        Friend Shadows Function GetTypeByMetadataName(fullyQualifiedMetadataName As String) As NamedTypeSymbol
            Return Me.Assembly.GetTypeByMetadataName(fullyQualifiedMetadataName, includeReferences:=True, isWellKnownType:=False)
        End Function

        Friend Shadows ReadOnly Property ObjectType As NamedTypeSymbol
            Get
                Return Assembly.ObjectType
            End Get
        End Property

        Friend Shadows Function CreateArrayTypeSymbol(elementType As TypeSymbol, Optional rank As Integer = 1) As ArrayTypeSymbol
            If elementType Is Nothing Then
                Throw New ArgumentNullException("elementType")
            End If

            Return New ArrayTypeSymbol(elementType, Nothing, rank, Me)
        End Function

#End Region

#Region "Binding"

        '''<summary> 
        ''' Get a fresh SemanticModel.  Note that each invocation gets a fresh SemanticModel, each of
        ''' which has a cache.  Therefore, one effectively clears the cache by discarding the
        ''' SemanticModel.
        '''</summary> 
        Public Shadows Function GetSemanticModel(syntaxTree As SyntaxTree) As SemanticModel
            Return New SyntaxTreeSemanticModel(Me, DirectCast(Me.SourceModule, SourceModuleSymbol), DirectCast(syntaxTree, SyntaxTree))
        End Function

#End Region

#Region "Diagnostics"

        Friend Overrides ReadOnly Property MessageProvider As CommonMessageProvider
            Get
                Return VisualBasic.MessageProvider.Instance
            End Get
        End Property

        ''' <summary>
        ''' Get all diagnostics for the entire compilation. This includes diagnostics from parsing, declarations, and
        ''' the bodies of methods. Getting all the diagnostics is potentially a length operations, as it requires parsing and
        ''' compiling all the code. The set of diagnostics is not caches, so each call to this method will recompile all
        ''' methods.
        ''' </summary>
        ''' <param name="cancellationToken">Cancellation token to allow cancelling the operation.</param>
        Public Overrides Function GetDiagnostics(Optional cancellationToken As CancellationToken = Nothing) As ImmutableArray(Of Diagnostic)
            Return GetDiagnostics(DefaultDiagnosticsStage, True, cancellationToken)
        End Function

        ''' <summary>
        ''' Get parse diagnostics for the entire compilation. This includes diagnostics from parsing BUT NOT from declarations and
        ''' the bodies of methods or initializers. The set of parse diagnostics is cached, so calling this method a second time
        ''' should be fast.
        ''' </summary>
        Public Overrides Function GetParseDiagnostics(Optional cancellationToken As CancellationToken = Nothing) As ImmutableArray(Of Diagnostic)
            Return GetDiagnostics(CompilationStage.Parse, False, cancellationToken)
        End Function

        ''' <summary>
        ''' Get declarations diagnostics for the entire compilation. This includes diagnostics from declarations, BUT NOT
        ''' the bodies of methods or initializers. The set of declaration diagnostics is cached, so calling this method a second time
        ''' should be fast.
        ''' </summary>
        ''' <param name="cancellationToken">Cancellation token to allow cancelling the operation.</param>
        Public Overrides Function GetDeclarationDiagnostics(Optional cancellationToken As CancellationToken = Nothing) As ImmutableArray(Of Diagnostic)
            Return GetDiagnostics(CompilationStage.Declare, False, cancellationToken)
        End Function

        ''' <summary>
        ''' Get method body diagnostics for the entire compilation. This includes diagnostics only from 
        ''' the bodies of methods and initializers. These diagnostics are NOT cached, so calling this method a second time
        ''' repeats significant work.
        ''' </summary>
        ''' <param name="cancellationToken">Cancellation token to allow cancelling the operation.</param>
        Public Overrides Function GetMethodBodyDiagnostics(Optional cancellationToken As CancellationToken = Nothing) As ImmutableArray(Of Diagnostic)
            Return GetDiagnostics(CompilationStage.Compile, False, cancellationToken)
        End Function

        ''' <summary>
        ''' Get all errors in the compilation, up through the given compilation stage. Note that this may
        ''' require significant work by the compiler, as all source code must be compiled to the given
        ''' level in order to get the errors. Errors on Options should be inspected by the user prior to constructing the compilation.
        ''' </summary>
        ''' <returns>
        ''' Returns all errors. The errors are not sorted in any particular order, and the client
        ''' should sort the errors as desired.
        ''' </returns>
        Friend Overloads Function GetDiagnostics(stage As CompilationStage, Optional includeEarlierStages As Boolean = True, Optional cancellationToken As CancellationToken = Nothing) As ImmutableArray(Of Diagnostic)
            Using Logger.LogBlock(FunctionId.VisualBasic_Compilation_GetDiagnostics, message:=Me.AssemblyName, cancellationToken:=cancellationToken)

                Dim builder = DiagnosticBag.GetInstance()

                ' Add all parsing errors.
                If (stage = CompilationStage.Parse OrElse stage > CompilationStage.Parse AndAlso includeEarlierStages) Then
                    If Options.ConcurrentBuild Then
                        Dim options = New ParallelOptions() With {.CancellationToken = cancellationToken}
                        Parallel.For(0, AllSyntaxTrees.Length, options,
                            Sub(i As Integer) builder.AddRange(AllSyntaxTrees(i).GetDiagnostics(cancellationToken)))
                    Else
                        For Each tree In AllSyntaxTrees
                            cancellationToken.ThrowIfCancellationRequested()
                            builder.AddRange(tree.GetDiagnostics(cancellationToken))
                        Next
                    End If
                End If

                ' Add declaration errors
                If (stage = CompilationStage.Declare OrElse stage > CompilationStage.Declare AndAlso includeEarlierStages) Then
                    builder.AddRange(Options.Errors)
                    builder.AddRange(GetBoundReferenceManager().Diagnostics)
                    builder.AddRange(SourceAssembly.GetAllDeclarationErrors(cancellationToken))
                    builder.AddRange(GetClsComplianceDiagnostics(cancellationToken))
                End If

                ' Add method body compilation errors.
                If (stage = CompilationStage.Compile OrElse stage > CompilationStage.Compile AndAlso includeEarlierStages) Then
                    ' Note: this phase does not need to be parallelized because 
                    '       it is already implemented in method compiler
                    Dim methodBodyDiagnostics = DiagnosticBag.GetInstance()
                    GetDiagnosticForAllMethodBodies(builder.HasAnyErrors(), methodBodyDiagnostics, stage, cancellationToken)
                    builder.AddRangeAndFree(methodBodyDiagnostics)
                End If

                ' Before returning diagnostics, we filter some of them
                ' to honor the compiler options (e.g., /nowarn and /warnaserror)
                Dim result = DiagnosticBag.GetInstance()
                FilterAndAppendAndFreeDiagnostics(result, builder)
                Return result.ToReadOnlyAndFree(Of Diagnostic)()
            End Using
        End Function

        Private Function GetClsComplianceDiagnostics(cancellationToken As CancellationToken, Optional filterTree As SyntaxTree = Nothing, Optional filterSpanWithinTree As TextSpan? = Nothing) As ImmutableArray(Of Diagnostic)
            If filterTree IsNot Nothing Then
                Dim builder = DiagnosticBag.GetInstance()
                ClsComplianceChecker.CheckCompliance(Me, builder, cancellationToken, filterTree, filterSpanWithinTree)
                Return builder.ToReadOnlyAndFree()
            End If

            Debug.Assert(filterSpanWithinTree Is Nothing)
            If m_lazyClsComplianceDiagnostics.IsDefault Then
                Dim builder = DiagnosticBag.GetInstance()
                ClsComplianceChecker.CheckCompliance(Me, builder, cancellationToken)
                ImmutableInterlocked.InterlockedInitialize(m_lazyClsComplianceDiagnostics, builder.ToReadOnlyAndFree())
            End If

            Debug.Assert(Not m_lazyClsComplianceDiagnostics.IsDefault)
            Return m_lazyClsComplianceDiagnostics
        End Function

        Private Shared Iterator Function FilterDiagnosticsByLocation(diagnostics As IEnumerable(Of Diagnostic), tree As SyntaxTree, filterSpanWithinTree As TextSpan?) As IEnumerable(Of Diagnostic)
            For Each diagnostic In diagnostics
                If diagnostic.ContainsLocation(tree, filterSpanWithinTree) Then
                    Yield diagnostic
                End If
            Next
        End Function

        Friend Function GetDiagnosticsForTree(stage As CompilationStage,
                                              tree As SyntaxTree,
                                              filterSpanWithinTree As TextSpan?,
                                              includeEarlierStages As Boolean,
                                              Optional cancellationToken As CancellationToken = Nothing) As ImmutableArray(Of Diagnostic)
            If Not SyntaxTrees.Contains(tree) Then
                Throw New ArgumentException("Cannot GetDiagnosticsForSyntax for a tree that is not part of the compilation", "tree")
            End If

            Dim builder = DiagnosticBag.GetInstance()

            If (stage = CompilationStage.Parse OrElse stage > CompilationStage.Parse AndAlso includeEarlierStages) Then
                ' Add all parsing errors.
                cancellationToken.ThrowIfCancellationRequested()
                Dim syntaxDiagnostics = tree.GetDiagnostics(cancellationToken)
                syntaxDiagnostics = FilterDiagnosticsByLocation(syntaxDiagnostics, tree, filterSpanWithinTree)
                builder.AddRange(syntaxDiagnostics)
            End If

            ' Add declaring errors errors
            If (stage = CompilationStage.Declare OrElse stage > CompilationStage.Declare AndAlso includeEarlierStages) Then
                Dim declarationDiags = DirectCast(SourceModule, SourceModuleSymbol).GetDeclarationErrorsInTree(tree, filterSpanWithinTree, AddressOf FilterDiagnosticsByLocation, cancellationToken)
                Dim filteredDiags = FilterDiagnosticsByLocation(declarationDiags, tree, filterSpanWithinTree)
                builder.AddRange(filteredDiags)
                builder.AddRange(GetClsComplianceDiagnostics(cancellationToken, tree, filterSpanWithinTree))
            End If

            ' Add method body declaring errors.
            If (stage = CompilationStage.Compile OrElse stage > CompilationStage.Compile AndAlso includeEarlierStages) Then
                Dim methodBodyDiagnostics = DiagnosticBag.GetInstance()
                GetDiagnosticForMethodBodiesInTree(tree, filterSpanWithinTree, builder.HasAnyErrors(), methodBodyDiagnostics, stage, cancellationToken)

                ' This diagnostics can include diagnostics for initializers that do not belong to the tree.
                ' Let's filter them out.
                If Not methodBodyDiagnostics.IsEmptyWithoutResolution Then
                    Dim allDiags = methodBodyDiagnostics.AsEnumerableWithoutResolution()
                    Dim filteredDiags = FilterDiagnosticsByLocation(allDiags, tree, filterSpanWithinTree)
                    For Each diag In filteredDiags
                        builder.Add(diag)
                    Next
                End If
            End If

            Dim result = DiagnosticBag.GetInstance()
            FilterAndAppendAndFreeDiagnostics(result, builder)
            Return result.ToReadOnlyAndFree(Of Diagnostic)()
        End Function

        ' Get diagnostics by compiling all method bodies.
        Private Sub GetDiagnosticForAllMethodBodies(hasDeclarationErrors As Boolean, diagnostics As DiagnosticBag, stage As CompilationStage, cancellationToken As CancellationToken)
            MethodCompiler.GetCompileDiagnostics(Me, SourceModule.GlobalNamespace, Nothing, Nothing, hasDeclarationErrors, diagnostics, stage >= CompilationStage.Emit, cancellationToken)
            DocumentationCommentCompiler.WriteDocumentationCommentXml(Me, Nothing, Nothing, diagnostics, cancellationToken)
            Me.ReportUnusedImports(Nothing, diagnostics, cancellationToken)
        End Sub

        ' Get diagnostics by compiling all method bodies in the given tree.
        Private Sub GetDiagnosticForMethodBodiesInTree(tree As SyntaxTree, filterSpanWithinTree As TextSpan?, hasDeclarationErrors As Boolean, diagnostics As DiagnosticBag, stage As CompilationStage, cancellationToken As CancellationToken)
            Dim sourceMod = DirectCast(SourceModule, SourceModuleSymbol)

            MethodCompiler.GetCompileDiagnostics(Me,
                                                 SourceModule.GlobalNamespace,
                                                 tree,
                                                 filterSpanWithinTree,
                                                 hasDeclarationErrors,
                                                 diagnostics,
                                                 stage >= CompilationStage.Emit,
                                                 cancellationToken)

            DocumentationCommentCompiler.WriteDocumentationCommentXml(Me, Nothing, Nothing, diagnostics, cancellationToken, tree, filterSpanWithinTree)

            ' Report unused import diagnostics only if computing diagnostics for the entire tree.
            ' Otherwise we cannot determine if a particular directive is used outside of the given sub-span within the tree.
            If Not filterSpanWithinTree.HasValue OrElse filterSpanWithinTree.Value = tree.GetRoot(cancellationToken).FullSpan Then
                Me.ReportUnusedImports(tree, diagnostics, cancellationToken)
            End If
        End Sub

        Friend Overrides Function FilterAndAppendAndFreeDiagnostics(accumulator As DiagnosticBag, ByRef incoming As DiagnosticBag) As Boolean
            Dim result As Boolean = FilterAndAppendDiagnostics(accumulator, incoming.AsEnumerableWithoutResolution(), Me.Options)
            incoming.Free()
            incoming = Nothing
            Return result
        End Function

        ' Filter out some warnings based on the compiler options (/nowarn and /warnaserror).
        Friend Overloads Shared Function FilterAndAppendDiagnostics(accumulator As DiagnosticBag, ByRef incoming As IEnumerable(Of Diagnostic), options As CompilationOptions) As Boolean

            Dim hasError As Boolean = False
            Dim hasWarnAsError As Boolean = False

            For Each diagnostic As Diagnostic In incoming
                ' Filter void diagnostics so that our callers don't have to perform resolution
                ' (which might copy the list of diagnostics).
                If (diagnostic.Severity = InternalDiagnosticSeverity.Void) Then
                    Continue For
                End If

                ' If it is an error, keep it as it is.
                If (diagnostic.Severity = DiagnosticSeverity.Error) Then
                    hasError = True
                    accumulator.Add(diagnostic)
                    Continue For
                End If

                '//In the native compiler, all warnings originating from alink.dll were issued
                '//under the id WRN_ALinkWarn - 1607. If nowarn:1607 is used they would get
                '//none of those warnings. In Roslyn, we've given each of these warnings their
                '//own number, so that they may be configured independently. To preserve compatibility
                '//if a user has specifically configured 1607 And we are reporting one of the alink warnings, use
                '//the configuration specified for 1607. As implemented, this could result in 
                '//specifying warnaserror:1607 And getting a message saying "warning as error CS8012..."
                '//We don't permit configuring 1607 and independently configuring the new warnings.

                Dim report As ReportDiagnostic

                If (AlinkWarnings.Contains(CType(diagnostic.Code, ERRID)) AndAlso
                    options.SpecificDiagnosticOptions.Keys.Contains(VisualBasic.MessageProvider.Instance.GetIdForErrorCode(ERRID.WRN_AssemblyGeneration1))) Then
                    report = GetDiagnosticReport(VisualBasic.MessageProvider.Instance.GetSeverity(ERRID.WRN_AssemblyGeneration1),
                                                        VisualBasic.MessageProvider.Instance.GetIdForErrorCode(ERRID.WRN_AssemblyGeneration1),
                                                        options)
                Else
                    report = GetDiagnosticReport(diagnostic.Severity, diagnostic.Id, options)
                End If

                Select Case report
                    Case ReportDiagnostic.Suppress
                        ' Skip it
                    Case ReportDiagnostic.Error
                        Debug.Assert(diagnostic.Severity = DiagnosticSeverity.Warning)
                        accumulator.Add(diagnostic.WithWarningAsError(True))
                        ' For a warning treated as an error, report ERR_WarningTreatedAsError for the first one
                        If hasWarnAsError = False Then
                            accumulator.Add(New VBDiagnostic(New DiagnosticInfo(VisualBasic.MessageProvider.Instance, CInt(ERRID.ERR_WarningTreatedAsError), diagnostic.GetMessage()), CType(diagnostic.Location, Location)))
                            hasWarnAsError = True
                        End If
                    Case ReportDiagnostic.Default, ReportDiagnostic.Warn
                        accumulator.Add(diagnostic)
                    Case Else
                        Throw ExceptionUtilities.UnexpectedValue(report)
                End Select
            Next

            Return Not (hasError OrElse hasWarnAsError)
        End Function


        Private Shared Function GetDiagnosticReport(severity As DiagnosticSeverity, id As String, options As CompilationOptions) As ReportDiagnostic

            Select Case (severity)
                Case InternalDiagnosticSeverity.Void
                    Return ReportDiagnostic.Suppress
                Case DiagnosticSeverity.Info
                    Return ReportDiagnostic.Default
                Case DiagnosticSeverity.Warning
                    ' Leave Select
                Case Else
                    Throw ExceptionUtilities.UnexpectedValue(severity)
            End Select

            ' Read options (e.g., /nowarn or /warnaserror)
            Dim report As ReportDiagnostic = ReportDiagnostic.Default
            options.SpecificDiagnosticOptions.TryGetValue(id, report)

            ' Compute if the reporting should be suppressed.
            If report = ReportDiagnostic.Suppress OrElse options.GeneralDiagnosticOption = ReportDiagnostic.Suppress Then
                ' check options (/nowarn)
                Return ReportDiagnostic.Suppress
            End If

            ' check the AllWarningsAsErrors flag and the specific lists from /warnaserror[+|-] option.
            If (options.GeneralDiagnosticOption = ReportDiagnostic.Error) Then
                ' In the case for both /warnaserror and /warnaserror-:<n> at the same time,
                ' do not report it as an error.
                If (report <> ReportDiagnostic.Warn) Then
                    Return ReportDiagnostic.Error
                End If
            Else
                ' In the case for /warnaserror:<n>, report it as an error.
                If (report = ReportDiagnostic.Error) Then
                    Return ReportDiagnostic.Error
                End If
            End If

            Return report

        End Function

#End Region

#Region "Resources"
        Protected Overrides Sub AppendDefaultVersionResource(resourceStream As Stream)
            Dim fileVersion As String = If(SourceAssembly.FileVersion, SourceAssembly.Identity.Version.ToString())

            'for some parameters, alink used to supply whitespace instead of null.
            Win32ResourceConversions.AppendVersionToResourceStream(resourceStream,
                Not Me.Options.OutputKind.IsApplication(),
                fileVersion:=fileVersion,
                originalFileName:=Me.SourceModule.Name,
                internalName:=Me.SourceModule.Name,
                productVersion:=If(SourceAssembly.InformationalVersion, fileVersion),
                assemblyVersion:=SourceAssembly.Identity.Version,
                fileDescription:=If(SourceAssembly.Title, " "),
                legalCopyright:=If(SourceAssembly.Copyright, " "),
                legalTrademarks:=SourceAssembly.Trademark,
                productName:=SourceAssembly.Product,
                comments:=SourceAssembly.Description,
                companyName:=SourceAssembly.Company)
        End Sub
#End Region

#Region "Emit"

        Friend Overrides ReadOnly Property IsDelaySign As Boolean
            Get
                Return SourceAssembly.IsDelaySign
            End Get
        End Property

        Friend Overrides ReadOnly Property StrongNameKeys As StrongNameKeys
            Get
                Return SourceAssembly.StrongNameKeys
            End Get
        End Property

        Friend Overrides ReadOnly Property EmitFunctionId As FunctionId
            Get
                Return FunctionId.VisualBasic_Compilation_Emit
            End Get
        End Property

        Friend Overrides Function MakeEmitResult(success As Boolean, diagnostics As ImmutableArray(Of Diagnostic)) As EmitResult
            Return New EmitResult(success, diagnostics)
        End Function

        ''' <summary>
        ''' Attempts to emit the assembly to the given stream. If there are 
        ''' compilation errors, false is returned. In this case, some bytes 
        ''' might have been written to the stream. The compilation errors can be
        ''' obtained by called GetDiagnostics(CompilationStage.Emit). If true is 
        ''' returned, the compilation proceeded without error and a valid 
        ''' assembly was written to the stream. 
        ''' </summary>
        ''' <param name="outputStream">Stream to which the compilation will be written.</param>
        ''' <param name="outputName">Name of the compilation: file name and extension.  Null to use the existing output name.
        ''' CAUTION: If this is set to a (non-null) value other than the existing compilation output name, then internals-visible-to
        ''' and assembly references may not work as expected.  In particular, things that were visible at bind time, based on the 
        ''' name of the compilation, may not be visible at runtime and vice-versa.
        ''' </param>
        ''' <param name="pdbFileName">The name of the PDB file - embedded in the output.  Null to infer from the stream or the compilation.
        ''' Ignored unless pdbStream is non-null.
        ''' </param>
        ''' <param name="pdbStream">Stream to which the compilation's debug info will be written.  Null to forego PDB generation.</param>
        ''' <param name="xmlDocStream">Stream to which the compilation's XML documentation will be written.  Null to forego XML generation.</param>
        ''' <param name="cancellationToken">To cancel the emit process.</param>
        ''' <param name="win32Resources">Stream from which the compilation's Win32 resources will be read (in RES format).  
        ''' Null to indicate that there are none.</param>
        ''' <param name="manifestResources">List of the compilation's managed resources.  Null to indicate that there are none.</param>
        Public Shadows Function Emit(
            outputStream As Stream,
            Optional outputName As String = Nothing,
            Optional pdbFileName As String = Nothing,
            Optional pdbStream As Stream = Nothing,
            Optional xmlDocStream As Stream = Nothing,
            Optional cancellationToken As CancellationToken = Nothing,
            Optional win32Resources As Stream = Nothing,
            Optional manifestResources As IEnumerable(Of ResourceDescription) = Nothing
        ) As EmitResult
            Return DirectCast(MyBase.Emit(
                    outputStream,
                        outputName,
                        pdbFileName,
                        pdbStream,
                        xmlDocStream,
                        cancellationToken,
                        win32Resources,
                    manifestResources), EmitResult)
        End Function

        ''' <summary>
        ''' Attempts to emit the assembly to the given stream. If there are 
        ''' compilation errors, false is returned. In this case, some bytes 
        ''' might have been written to the stream. The compilation errors can be
        ''' obtained by called GetDiagnostics(CompilationStage.Emit). If true is 
        ''' returned, the compilation proceeded without error and a valid 
        ''' assembly was written to the stream. 
        ''' </summary>
        ''' <param name="outputPath">Path of the file to which the compilation will be written.</param>
        ''' <param name="pdbPath">Path of the file to which the compilation's debug info will be written.
        ''' Also embedded in the output file.  Null to forego PDB generation.
        ''' </param>
        ''' <param name="xmlDocPath">Path of the file to which the compilation's XML documentation will be written.  Null to forego XML generation.</param>
        ''' <param name="cancellationToken">To cancel the emit process.</param>
        ''' <param name="win32ResourcesPath">Path of the file from which the compilation's Win32 resources will be read (in RES format).  
        ''' Null to indicate that there are none.</param>
        ''' <param name="manifestResources">List of the compilation's managed resources.  Null to indicate that there are none.</param>
        Public Shadows Function Emit(
            outputPath As String,
            Optional pdbPath As String = Nothing,
            Optional xmlDocPath As String = Nothing,
            Optional cancellationToken As CancellationToken = Nothing,
            Optional win32ResourcesPath As String = Nothing,
            Optional manifestResources As IEnumerable(Of ResourceDescription) = Nothing
        ) As EmitResult

            Return DirectCast(MyBase.Emit(
                    outputPath,
                    pdbPath,
                    xmlDocPath,
                    cancellationToken,
                    win32ResourcesPath,
                    manifestResources), EmitResult)
        End Function

        Friend Sub EnsureAnonymousTypeTemplates(cancellationToken As CancellationToken)
            If Me.GetSubmissionSlotIndex() >= 0 AndAlso HasCodeToEmit() Then
                If Not Me.AnonymousTypeManager.AreTemplatesSealed Then

                    Dim discardedDiagnostics = DiagnosticBag.GetInstance()
                    Compile(outputName:=Nothing,
                            moduleVersionId:=Guid.NewGuid,
                            manifestResources:=Nothing,
                            win32Resources:=Nothing,
                            xmlDocStream:=Nothing,
                            assemblySymbolMapper:=Nothing,
                            cancellationToken:=cancellationToken,
                            testData:=Nothing,
                            metadataOnly:=False,
                            generateDebugInfo:=False,
                            diagnostics:=discardedDiagnostics)
                    discardedDiagnostics.Free()
                End If

                Debug.Assert(Me.AnonymousTypeManager.AreTemplatesSealed)
            ElseIf Me.PreviousSubmission IsNot Nothing Then
                Me.PreviousSubmission.EnsureAnonymousTypeTemplates(cancellationToken)
            End If
        End Sub

        ''' <summary>
        ''' Attempts to emit just the metadata parts of the compilation, without compiling any executable code 
        ''' (method bodies). No debug info can be produced.
        ''' </summary>
        ''' <param name="metadataStream">Stream to which the compilation's metadata will be written.</param>
        ''' <param name="outputName">Name of the compilation: file name and extension.  Null to use the existing output name.
        ''' CAUTION: If this is set to a (non-null) value other than the existing compilation output name, then internals-visible-to
        ''' and assembly references may not work as expected.  In particular, things that were visible at bind time, based on the 
        ''' name of the compilation, may not be visible at runtime and vice-versa.
        ''' </param>
        ''' <param name="xmlDocStream">Stream to which the compilation's XML documentation will be written.  Null to forego XML generation.</param>
        ''' <param name="cancellationToken">To cancel the emit process.</param>
        Public Shadows Function EmitMetadataOnly(
            metadataStream As Stream,
            Optional outputName As String = Nothing,
            Optional xmlDocStream As Stream = Nothing,
            Optional cancellationToken As CancellationToken = Nothing
        ) As EmitResult
            Return DirectCast(MyBase.EmitMetadataOnly(
                    metadataStream,
                    outputName,
                    xmlDocStream,
                    cancellationToken), EmitResult)
        End Function

        Friend Overrides Function CreateModuleBuilder(
            outputName As String,
            moduleVersionId As Guid,
            manifestResources As IEnumerable(Of ResourceDescription),
            assemblySymbolMapper As Func(Of IAssemblySymbol, AssemblyIdentity),
            cancellationToken As CancellationToken,
            testData As CompilationTestData,
            diagnosticBag As DiagnosticBag,
            ByRef hasDeclarationErrors As Boolean) As CommonPEModuleBuilder

            Debug.Assert(diagnosticBag.IsEmptyWithoutResolution) ' True, but not required.

            ' The diagnostics should include syntax and declaration errors also. We insert these before calling Emitter.Emit, so that we don't emit
            ' metadata if there are declaration errors or method body errors (but we do insert all errors from method body binding...)
            If Not FilterAndAppendDiagnostics(
                diagnosticBag,
                GetDiagnostics(CompilationStage.Declare, True, cancellationToken),
                Me.Options) Then
                hasDeclarationErrors = True
            End If

            ' Do not waste a slot in the submission chain for submissions that contain no executable code
            ' (they may only contain #r directives, usings, etc.)
            If IsSubmission AndAlso Not HasCodeToEmit() Then
                Return Nothing
            End If

            ' Get the runtime metadata version from the cor library. If this fails we have no reasonable value to give.
            Dim runtimeMetadataVersion = GetRuntimeMetadataVersion()

            Dim moduleSerializationProperties = ConstructModuleSerializationProperties(runtimeMetadataVersion, moduleVersionId)
            If manifestResources Is Nothing Then
                manifestResources = SpecializedCollections.EmptyEnumerable(Of ResourceDescription)()
            End If

            ' if there is no stream to write to, then there is no need for a module
            Dim moduleBeingBuilt As PEModuleBuilder
            If Options.OutputKind.IsNetModule() Then
                moduleBeingBuilt = New PENetModuleBuilder(
                    DirectCast(Me.SourceModule, SourceModuleSymbol),
                    outputName,
                    moduleSerializationProperties,
                    manifestResources)
            Else
                Dim kind = If(Options.OutputKind.IsValid(), Options.OutputKind, OutputKind.DynamicallyLinkedLibrary)
                moduleBeingBuilt = New PEAssemblyBuilder(
                        SourceAssembly,
                        outputName,
                        kind,
                        moduleSerializationProperties,
                        manifestResources,
                        assemblySymbolMapper)
            End If

            If testData IsNot Nothing Then
                moduleBeingBuilt.SetMethodTestData(testData.Methods)
                testData.Module = moduleBeingBuilt
            End If

            Return moduleBeingBuilt
        End Function

        Friend Overrides Function Compile(
            moduleBuilder As CommonPEModuleBuilder,
            outputName As String,
            manifestResources As IEnumerable(Of ResourceDescription),
            win32Resources As Stream,
            xmlDocStream As Stream,
            cancellationToken As CancellationToken,
            metadataOnly As Boolean,
            generateDebugInfo As Boolean,
            diagnosticBag As DiagnosticBag,
            filter As Predicate(Of ISymbol),
            hasDeclarationErrors As Boolean) As Boolean

            Dim moduleBeingBuilt = DirectCast(moduleBuilder, PEModuleBuilder)

            Me.EmbeddedSymbolManager.MarkAllDeferredSymbolsAsReferenced(Me)

            If metadataOnly Then
                If hasDeclarationErrors Then
                    Return False
                End If

                SynthesizedMetadataCompiler.ProcessSynthesizedMembers(Me, moduleBeingBuilt, cancellationToken)
            Else

                ' start generating PDB checksums if we need to emit PDBs
                If generateDebugInfo AndAlso moduleBeingBuilt IsNot Nothing Then
                    ' Add debug documents for all trees with distinct paths.
                    For Each tree In Me.SyntaxTrees
                        Dim path As String = tree.FilePath
                        If Not String.IsNullOrEmpty(path) Then
                            ' compilation does not guarantee that all trees will have distinct paths.
                            ' Do not attempt adding a document for a particular path if we already added one.
                            Dim normalizedPath = moduleBeingBuilt.NormalizeDebugDocumentPath(path, basePath:=Nothing)
                            Dim existingDoc = moduleBeingBuilt.TryGetDebugDocumentForNormalizedPath(normalizedPath)
                            If existingDoc Is Nothing Then
                                moduleBeingBuilt.AddDebugDocument(MakeDebugSourceDocumentForTree(normalizedPath, tree))
                            End If
                        End If
                    Next

                    ' Add debug documents for all directives. 
                    ' If there are clashes with already processed directives, report warnings.
                    ' If there are clashes with debug documents that came from actual trees, ignore the directive.
                    For Each tree In Me.SyntaxTrees
                        AddDebugSourceDocumentsForChecksumDirectives(moduleBeingBuilt, tree, diagnosticBag)
                    Next
                End If

                ' EDMAURER perform initial bind of method bodies in spite of earlier errors. This is the same
                ' behavior as when calling GetDiagnostics()

                ' Use a temporary bag so we don't have to refilter pre-existing diagnostics.
                Dim methodBodyDiagnosticBag = DiagnosticBag.GetInstance()

                MethodCompiler.CompileMethodBodies(
                    Me,
                    moduleBeingBuilt,
                    generateDebugInfo,
                    hasDeclarationErrors,
                    filter,
                    methodBodyDiagnosticBag,
                    cancellationToken)
                DocumentationCommentCompiler.WriteDocumentationCommentXml(Me, outputName, xmlDocStream, methodBodyDiagnosticBag, cancellationToken)
                Me.ReportUnusedImports(Nothing, methodBodyDiagnosticBag, cancellationToken)

                SetupWin32Resources(moduleBeingBuilt, win32Resources, methodBodyDiagnosticBag)

                ' give the name of any added modules, but not the name of the primary module.
                ReportManifestResourceDuplicates(
                    manifestResources,
                    SourceAssembly.Modules.Skip(1).Select(Function(x) x.Name),
                    AddedModulesResourceNames(methodBodyDiagnosticBag),
                    methodBodyDiagnosticBag)

                Dim hasMethodBodyErrors As Boolean = Not FilterAndAppendAndFreeDiagnostics(diagnosticBag, methodBodyDiagnosticBag)

                If hasDeclarationErrors OrElse hasMethodBodyErrors Then
                    Return False
                End If
            End If

            cancellationToken.ThrowIfCancellationRequested()

            ' TODO (tomat): XML doc comments diagnostics

            Return True
        End Function

        Private Iterator Function AddedModulesResourceNames(diagnostics As DiagnosticBag) As IEnumerable(Of String)
            Dim modules As ImmutableArray(Of ModuleSymbol) = SourceAssembly.Modules

            For i As Integer = 1 To modules.Length - 1
                Dim m = DirectCast(modules(i), Symbols.Metadata.PE.PEModuleSymbol)

                Try
                    For Each resource In m.Module.GetEmbeddedResourcesOrThrow()
                        Yield resource.Name
                    Next
                Catch mrEx As BadImageFormatException
                    diagnostics.Add(ERRID.ERR_UnsupportedModule1, NoLocation.Singleton, m)
                End Try
            Next
        End Function

        Friend Overrides Function EmitDifference(
            baseline As EmitBaseline,
            edits As IEnumerable(Of SemanticEdit),
            metadataStream As Stream,
            ilStream As Stream,
            pdbStream As Stream,
            updatedMethodTokens As ICollection(Of UInteger),
            testData As CompilationTestData,
            cancellationToken As CancellationToken) As EmitDifferenceResult

            Return EmitHelpers.EmitDifference(
                Me,
                baseline,
                edits,
                metadataStream,
                ilStream,
                pdbStream,
                updatedMethodTokens,
                testData,
                cancellationToken)
        End Function

        Friend Function GetRuntimeMetadataVersion() As String
            Dim corLibrary = TryCast(Assembly.CorLibrary, Symbols.Metadata.PE.PEAssemblySymbol)
            Return If(corLibrary Is Nothing, String.Empty, corLibrary.Assembly.ManifestModule.MetadataVersion)
        End Function

        Private Shared Sub AddDebugSourceDocumentsForChecksumDirectives(
            moduleBeingBuilt As PEModuleBuilder,
            tree As SyntaxTree,
            diagnosticBag As DiagnosticBag)

            Dim checksumDirectives = tree.GetRoot().GetDirectives(Function(d) d.Kind = SyntaxKind.ExternalChecksumDirectiveTrivia AndAlso
                                                                              Not d.ContainsDiagnostics)

            For Each directive In checksumDirectives
                Dim checkSumDirective As ExternalChecksumDirectiveTriviaSyntax = DirectCast(directive, ExternalChecksumDirectiveTriviaSyntax)
                Dim path = checkSumDirective.ExternalSource.ValueText

                Dim checkSumText = checkSumDirective.Checksum.ValueText
                Dim normalizedPath = moduleBeingBuilt.NormalizeDebugDocumentPath(path, basePath:=tree.FilePath)
                Dim existingDoc = moduleBeingBuilt.TryGetDebugDocumentForNormalizedPath(normalizedPath)

                If existingDoc IsNot Nothing Then
                    ' directive matches a file path on an actual tree.
                    ' Dev12 compiler just ignores the directive in this case which means that
                    ' checksum of the actual tree always wins and no warning is given.
                    ' We will continue doing the same.
                    If (existingDoc.IsComputedChecksum) Then
                        Continue For
                    End If

                    If CheckSumMatches(checkSumText, existingDoc.SourceHash) Then
                        Dim guid As Guid = Guid.Parse(checkSumDirective.Guid.ValueText)
                        If guid = existingDoc.SourceHashKind Then
                            ' all parts match, nothing to do
                            Continue For
                        End If
                    End If

                    ' did not match to an existing document
                    ' produce a warning and ignore the directive
                    diagnosticBag.Add(ERRID.WRN_MultipleDeclFileExtChecksum, New SourceLocation(checkSumDirective), path)

                Else
                    Dim newDocument = New Cci.DebugSourceDocument(
                        normalizedPath,
                        Cci.DebugSourceDocument.CorSymLanguageTypeBasic,
                        MakeCheckSumBytes(checkSumDirective.Checksum.ValueText),
                        Guid.Parse(checkSumDirective.Guid.ValueText))

                    moduleBeingBuilt.AddDebugDocument(newDocument)
                End If
            Next
        End Sub

        Private Shared Function CheckSumMatches(bytesText As String, bytes As ImmutableArray(Of Byte)) As Boolean
            If bytesText.Length <> bytes.Length * 2 Then
                Return False
            End If

            For i As Integer = 0 To bytesText.Length \ 2 - 1
                ' 1A  in text becomes   0x1A
                Dim b As Integer = SyntaxFacts.IntegralLiteralCharacterValue(bytesText(i * 2)) * 16 +
                                   SyntaxFacts.IntegralLiteralCharacterValue(bytesText(i * 2 + 1))

                If b <> bytes(i) Then
                    Return False
                End If
            Next

            Return True
        End Function

        Private Shared Function MakeCheckSumBytes(bytesText As String) As ImmutableArray(Of Byte)
            Dim builder As ArrayBuilder(Of Byte) = ArrayBuilder(Of Byte).GetInstance()

            For i As Integer = 0 To bytesText.Length \ 2 - 1
                ' 1A  in text becomes   0x1A
                Dim b As Byte = CByte(SyntaxFacts.IntegralLiteralCharacterValue(bytesText(i * 2)) * 16 +
                                      SyntaxFacts.IntegralLiteralCharacterValue(bytesText(i * 2 + 1)))

                builder.Add(b)
            Next

            Return builder.ToImmutableAndFree()
        End Function

        Private Shared Function MakeDebugSourceDocumentForTree(normalizedPath As String, tree As SyntaxTree) As Cci.DebugSourceDocument
            Dim checkSumSha1 As Func(Of ImmutableArray(Of Byte)) = Function() tree.GetSha1Checksum()
            Return New Cci.DebugSourceDocument(normalizedPath, Microsoft.Cci.DebugSourceDocument.CorSymLanguageTypeBasic, checkSumSha1)
        End Function

        Private Sub SetupWin32Resources(moduleBeingBuilt As PEModuleBuilder, win32Resources As Stream, diagnostics As DiagnosticBag)
            If (win32Resources Is Nothing) Then Return

            Select Case DetectWin32ResourceForm(win32Resources)
                Case Win32ResourceForm.COFF
                    moduleBeingBuilt.Win32ResourceSection = MakeWin32ResourcesFromCOFF(win32Resources, diagnostics)
                Case Win32ResourceForm.RES
                    moduleBeingBuilt.Win32Resources = MakeWin32ResourceList(win32Resources, diagnostics)
                Case Else
                    diagnostics.Add(ERRID.ERR_ErrorCreatingWin32ResourceFile, NoLocation.Singleton, New LocalizableErrorArgument(ERRID.IDS_UnrecognizedFileFormat))
            End Select
        End Sub
        Protected Overrides Function HasCodeToEmit() As Boolean
            ' TODO (tomat):
            For Each syntaxTree In SyntaxTrees
                Dim unit = syntaxTree.GetCompilationUnitRoot()
                If unit.Members.Count > 0 Then
                    Return True
                End If
            Next

            Return False
        End Function

#End Region

#Region "Common Members"

        Protected Overrides Function CommonWithReferences(newReferences As IEnumerable(Of MetadataReference)) As Compilation
            Return WithReferences(newReferences)
        End Function

        Protected Overrides Function CommonWithAssemblyName(assemblyName As String) As Compilation
            Return WithAssemblyName(assemblyName)
        End Function

        Protected Overrides Function CommonGetSubmissionResultType(ByRef hasValue As Boolean) As ITypeSymbol
            Return GetSubmissionResultType(hasValue)
        End Function

        Protected Overrides ReadOnly Property CommonAssembly As IAssemblySymbol
            Get
                Return Me.Assembly
            End Get
        End Property

        Protected Overrides ReadOnly Property CommonGlobalNamespace As INamespaceSymbol
            Get
                Return Me.GlobalNamespace
            End Get
        End Property

        Protected Overrides ReadOnly Property CommonOptions As CompilationOptions
            Get
                Return Options
            End Get
        End Property

        Protected Overrides ReadOnly Property CommonPreviousSubmission As Compilation
            Get
                Return PreviousSubmission
            End Get
        End Property

        Protected Overrides Function CommonGetSemanticModel(syntaxTree As SyntaxTree) As SemanticModel
            Return Me.GetSemanticModel(DirectCast(syntaxTree, SyntaxTree))
        End Function

        Protected Overrides ReadOnly Property CommonSyntaxTrees As IEnumerable(Of SyntaxTree)
            Get
                Return Me.SyntaxTrees
            End Get
        End Property

        Protected Overrides Function CommonAddSyntaxTrees(trees As IEnumerable(Of SyntaxTree)) As Compilation
            Dim array = TryCast(trees, SyntaxTree())
            If array IsNot Nothing Then
                Return Me.AddSyntaxTrees(array)
            End If

            If trees Is Nothing Then
                Throw New ArgumentNullException("trees")
            End If

            Return Me.AddSyntaxTrees(trees.Cast(Of SyntaxTree)())
        End Function

        Protected Overrides Function CommonRemoveSyntaxTrees(trees As IEnumerable(Of SyntaxTree)) As Compilation
            Dim array = TryCast(trees, SyntaxTree())
            If array IsNot Nothing Then
                Return Me.RemoveSyntaxTrees(array)
            End If

            If trees Is Nothing Then
                Throw New ArgumentNullException("trees")
            End If

            Return Me.RemoveSyntaxTrees(trees.Cast(Of SyntaxTree)())
        End Function

        Protected Overrides Function CommonRemoveAllSyntaxTrees() As Compilation
            Return Me.RemoveAllSyntaxTrees()
        End Function

        Protected Overrides Function CommonReplaceSyntaxTree(oldTree As SyntaxTree, newTree As SyntaxTree) As Compilation
            Return Me.ReplaceSyntaxTree(DirectCast(oldTree, SyntaxTree), DirectCast(newTree, SyntaxTree))
        End Function

        Protected Overrides Function CommonWithOptions(options As CompilationOptions) As Compilation
            Return Me.WithOptions(DirectCast(options, VisualBasicCompilationOptions))
        End Function

        Protected Overrides Function CommonWithPreviousSubmission(newPreviousSubmission As Compilation) As Compilation
            Return Me.WithPreviousSubmission(DirectCast(newPreviousSubmission, VisualBasicCompilation))
        End Function

        Protected Overrides Function CommonContainsSyntaxTree(syntaxTree As SyntaxTree) As Boolean
            Return Me.ContainsSyntaxTree(DirectCast(syntaxTree, SyntaxTree))
        End Function

        Protected Overrides Function CommonGetAssemblyOrModuleSymbol(reference As MetadataReference) As ISymbol
            Return Me.GetAssemblyOrModuleSymbol(reference)
        End Function

        Protected Overrides Function CommonClone() As Compilation
            Return Me.Clone()
        End Function

        Protected Overrides ReadOnly Property CommonSourceModule As IModuleSymbol
            Get
                Return Me.SourceModule
            End Get
        End Property

        Protected Overrides Function CommonGetSpecialType(specialType As SpecialType) As INamedTypeSymbol
            Return Me.GetSpecialType(specialType)
        End Function

        Protected Overrides Function CommonGetCompilationNamespace(namespaceSymbol As INamespaceSymbol) As INamespaceSymbol
            Return Me.GetCompilationNamespace(namespaceSymbol)
        End Function

        Protected Overrides Function CommonGetTypeByMetadataName(metadataName As String) As INamedTypeSymbol
            Return Me.GetTypeByMetadataName(metadataName)
        End Function

        Protected Overrides ReadOnly Property CommonScriptClass As INamedTypeSymbol
            Get
                Return Me.ScriptClass
            End Get
        End Property

        Public Overrides Function CreateErrorTypeSymbol(container As INamespaceOrTypeSymbol, name As String, arity As Integer) As INamedTypeSymbol
            Return New ExtendedErrorTypeSymbol(DirectCast(container, NamespaceOrTypeSymbol), name, arity)
        End Function

        Protected Overrides Function CommonCreateArrayTypeSymbol(elementType As ITypeSymbol, rank As Integer) As IArrayTypeSymbol
            Return CreateArrayTypeSymbol(elementType.EnsureVbSymbolOrNothing(Of TypeSymbol)("elementType"), rank)
        End Function

        Protected Overrides Function CommonCreatePointerTypeSymbol(elementType As ITypeSymbol) As IPointerTypeSymbol
            Throw New NotSupportedException(VBResources.ThereAreNoPointerTypesInVB)
        End Function

        Protected Overrides ReadOnly Property CommonDynamicType As ITypeSymbol
            Get
                Throw New NotSupportedException(VBResources.ThereIsNoDynamicTypeInVB)
            End Get
        End Property

        Protected Overrides ReadOnly Property CommonObjectType As INamedTypeSymbol
            Get
                Return Me.ObjectType
            End Get
        End Property

        Protected Overrides Function CommonGetMetadataReference(_assemblySymbol As IAssemblySymbol) As MetadataReference
            Dim symbol = TryCast(_assemblySymbol, AssemblySymbol)
            If symbol IsNot Nothing Then
                Return Me.GetMetadataReference(symbol)
            Else
                Return Nothing
            End If
        End Function

        Protected Overrides Function CommonGetEntryPoint(cancellationToken As CancellationToken) As IMethodSymbol
            Return Me.GetEntryPoint(cancellationToken)
        End Function

#End Region

    End Class
End Namespace