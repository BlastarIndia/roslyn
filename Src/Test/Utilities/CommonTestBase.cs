﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeGen;
using Roslyn.Test.Utilities;
using Xunit;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Emit;

namespace Microsoft.CodeAnalysis.Test.Utilities
{
    /// <summary>
    /// Base class for all language specific tests.
    /// </summary>
    public abstract partial class CommonTestBase : TestBase
    {
        static CommonTestBase()
        {
            var configFileName = Path.GetFileName(Assembly.GetExecutingAssembly().Location) + ".config";
            var configFilePath = Path.Combine(Environment.CurrentDirectory, configFileName);
            
            if (File.Exists(configFilePath))
            {
                var assemblyConfig = XDocument.Load(configFilePath);

                var roslynUnitTestsSection = assemblyConfig.Root.Element("roslyn.unittests");

                if (roslynUnitTestsSection != null)
                {
                    var emitSection = roslynUnitTestsSection.Element("emit");

                    if (emitSection != null)
                    {
                        var methodElements = emitSection.Elements("method");
                        emitters = new Emitter[methodElements.Count()];

                        int i = 0;
                        foreach (var method in methodElements)
                        {
                            var asm = Assembly.Load(method.Attribute("assembly").Value);
                            emitters[i] = (Emitter)Delegate.CreateDelegate(typeof(Emitter),
                                asm.GetType(method.Attribute("type").Value),
                                method.Attribute("name").Value);

                            i++;
                        }
                    }
                }
            }
        }

        internal abstract IEnumerable<IModuleSymbol> ReferencesToModuleSymbols(IEnumerable<MetadataReference> references, MetadataImportOptions importOptions = MetadataImportOptions.Public);

        #region Emit
        
        protected abstract Compilation GetCompilationForEmit(
            IEnumerable<string> source,
            MetadataReference[] additionalRefs,
            CompilationOptions options);

        protected abstract CompilationOptions DefaultCompilationOptions { get; }
        protected abstract CompilationOptions OptionsDll { get; }

        internal delegate CompilationVerifier Emitter(
            CommonTestBase test,
            Compilation compilation,
            IEnumerable<ModuleData> dependencies,
            EmitOptions emitOptions,
            IEnumerable<ResourceDescription> manifestResources,
            SignatureDescription[] expectedSignatures,
            string expectedOutput,
            Action<PEAssembly, EmitOptions> assemblyValidator,
            Action<IModuleSymbol, EmitOptions> symbolValidator,
            bool collectEmittedAssembly,
            bool emitPdb,
            bool verify);

        private static Emitter[] emitters;

        internal CompilationVerifier CompileAndVerify(
            string source,
            MetadataReference[] additionalRefs = null,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<IModuleSymbol, EmitOptions> sourceSymbolValidator = null,
            Action<PEAssembly, EmitOptions> assemblyValidator = null,
            Action<IModuleSymbol, EmitOptions> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            CompilationOptions options = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false,
            bool verify = true)
        {
            return CompileAndVerify(
                sources: new string[] { source },
                additionalRefs: additionalRefs,
                dependencies: dependencies,
                emitOptions: emitOptions,
                sourceSymbolValidator: sourceSymbolValidator,
                assemblyValidator: assemblyValidator,
                symbolValidator: symbolValidator,
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
            Action<IModuleSymbol, EmitOptions> sourceSymbolValidator = null,
            Action<PEAssembly, EmitOptions> assemblyValidator = null,
            Action<IModuleSymbol, EmitOptions> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            CompilationOptions options = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false,
            bool verify = true)
        {
            if (options == null)
            {
                options = DefaultCompilationOptions.WithOutputKind((expectedOutput != null) ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary);
            }

            if (emitPdb)
            {
                options = options.WithOptimizations(false);
            }

            var compilation = GetCompilationForEmit(sources, additionalRefs, options);

            return this.CompileAndVerify(
                compilation,
                null,
                dependencies,
                emitOptions,
                sourceSymbolValidator,
                assemblyValidator,
                symbolValidator,
                expectedSignatures,
                expectedOutput,
                collectEmittedAssembly,
                emitPdb,
                verify);
        }

        internal CompilationVerifier CompileAndVerify(
            Compilation compilation,
            IEnumerable<ResourceDescription> manifestResources = null,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<IModuleSymbol, EmitOptions> sourceSymbolValidator = null,
            Action<PEAssembly, EmitOptions> assemblyValidator = null,
            Action<IModuleSymbol, EmitOptions> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false,
            bool verify = true)
        {
            Assert.NotNull(compilation);

            Assert.True(expectedOutput == null || 
                (compilation.Options.OutputKind == OutputKind.ConsoleApplication || compilation.Options.OutputKind == OutputKind.WindowsApplication),
                "Compilation must be executable if output is expected.");

            if (verify)
            {
                // Unsafe code might not verify, so don't try.
                var csharpOptions = compilation.Options as Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions;
                verify = (csharpOptions == null || !csharpOptions.AllowUnsafe);
            }

            if (sourceSymbolValidator != null)
            {
                var module = compilation.Assembly.Modules.First();
                sourceSymbolValidator(module, emitOptions);
            }

            if (emitters == null || emitters.Length == 0)
            {
                throw new InvalidOperationException(
                    @"You must specify at least one Emitter.

Example app.config:

<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <roslyn.unittests>
    <emit>
      <method assembly=""SomeAssembly"" type=""SomeClass"" name= ""SomeEmitMethod"" />
    </emit>
  </roslyn.unittests>
</configuration>");
            }

            CompilationVerifier result = null;

            foreach (var emit in emitters)
            {
                var verifier = emit(this,
                                    compilation,
                                    dependencies,
                                    emitOptions,
                                    manifestResources,
                                    expectedSignatures,
                                    expectedOutput,
                                    assemblyValidator,
                                    symbolValidator,
                                    collectEmittedAssembly,
                                    emitPdb,
                                    verify);

                if (result == null)
                {
                    result = verifier;
                }
                else
                {
                    // only one emitter should return a verifier
                    Assert.Null(verifier);
                }
            }

            // If this fails, it means that more that all emmiters failed to return a validator
            // (i.e. none thought that they were applicable for the given input parameters).
            Assert.NotNull(result);

            return result;
        }

        /// <summary>
        /// Compiles, but only verifies if run on a Windows 8 machine.
        /// </summary>
        internal CompilationVerifier CompileAndVerifyOnWin8Only(
            string source,
            MetadataReference[] additionalRefs = null,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<IModuleSymbol> sourceSymbolValidator = null,
            Action<PEAssembly> validator = null,
            Action<IModuleSymbol> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            CompilationOptions options = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false)
        {
            return CompileAndVerify(
                source,
                additionalRefs,
                dependencies,
                emitOptions,
                Translate(sourceSymbolValidator),
                Translate(validator),
                Translate(symbolValidator),
                expectedSignatures,
                OSVersion.IsWin8 ? expectedOutput : null,
                options,
                collectEmittedAssembly,
                emitPdb,
                verify: OSVersion.IsWin8);
        }

        /// <summary>
        /// Compiles, but only verifies on a Windows 8 machine.
        /// </summary>
        /// <param name="compilation"></param>
        /// <param name="dependencies"></param>
        /// <param name="emitOptions"></param>
        /// <param name="sourceSymbolValidator"></param>
        /// <param name="validator"></param>
        /// <param name="symbolValidator"></param>
        /// <param name="expectedSignatures"></param>
        /// <param name="expectedOutput"></param>
        /// <param name="collectEmittedAssembly"></param>
        /// <param name="emitPdb"></param>
        /// <param name="verify"></param>
        /// <returns></returns>
        internal CompilationVerifier CompileAndVerifyOnWin8Only(
            Compilation compilation,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<IModuleSymbol> sourceSymbolValidator = null,
            Action<PEAssembly> validator = null,
            Action<IModuleSymbol> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false)
        {
            return CompileAndVerify(
                compilation,
                null,
                dependencies,
                emitOptions,
                Translate(sourceSymbolValidator),
                Translate(validator),
                Translate(symbolValidator),
            	expectedSignatures,
            	OSVersion.IsWin8 ? expectedOutput : null,
            	collectEmittedAssembly,
            	emitPdb,
            	verify: OSVersion.IsWin8);
        }

        /// <summary>
        /// Compiles, but only verifies on a win8 machine.
        /// </summary>
        internal CompilationVerifier CompileAndVerifyOnWin8Only(
            string[] sources,
            MetadataReference[] additionalRefs = null,
            IEnumerable<ModuleData> dependencies = null,
            EmitOptions emitOptions = EmitOptions.All,
            Action<IModuleSymbol> sourceSymbolValidator = null,
            Action<PEAssembly> validator = null,
            Action<IModuleSymbol> symbolValidator = null,
            SignatureDescription[] expectedSignatures = null,
            string expectedOutput = null,
            CompilationOptions options = null,
            bool collectEmittedAssembly = true,
            bool emitPdb = false,
            bool verify = true)
        {
            return CompileAndVerify(
                sources,
                additionalRefs,
                dependencies,
                emitOptions,
                Translate(sourceSymbolValidator),
                Translate(validator),
                Translate(symbolValidator),
                expectedSignatures,
                OSVersion.IsWin8 ? expectedOutput : null,
                options,
                collectEmittedAssembly,
                emitPdb,
                verify: verify && OSVersion.IsWin8);
        }

        private static Action<T, EmitOptions> Translate<T>(Action<T> action)
        {
            if (action != null)
            {
                return (module, _) => action(module);
            }
            else
            {
                return null;
            }
        }
        
        internal CompilationVerifier CompileAndVerifyFieldMarshal(string source, Dictionary<string, byte[]> expectedBlobs, bool isField = true, EmitOptions emitOptions = EmitOptions.All)
        {
            return CompileAndVerifyFieldMarshal(
                source, 
                (s, _omitted1, _omitted2) => 
                { 
                    Assert.True(expectedBlobs.ContainsKey(s), "Expecting marshalling blob for " + (isField ? "field " : "parameter ") + s);
                    return expectedBlobs[s]; 
                }, 
                isField,
                emitOptions);
        }

        internal CompilationVerifier CompileAndVerifyFieldMarshal(string source, Func<string, PEAssembly, EmitOptions, byte[]> getExpectedBlob, bool isField = true, EmitOptions emitOptions = EmitOptions.All)
        {
            return CompileAndVerify(source, emitOptions: emitOptions, options: OptionsDll, assemblyValidator: (assembly, options) => MarshalAsMetadataValidator(assembly, getExpectedBlob, options, isField));
        }

        static internal void RunValidators(CompilationVerifier verifier, EmitOptions emitOptions, Action<PEAssembly, EmitOptions> assemblyValidator, Action<IModuleSymbol, EmitOptions> symbolValidator)
        {
            if (assemblyValidator != null)
            {
                using (var emittedMetadata = AssemblyMetadata.Create(verifier.GetAllModuleMetadata()))
                {
                    assemblyValidator(emittedMetadata.Assembly, emitOptions);
                }
            }

            if (symbolValidator != null)
            {
                var peModuleSymbol = verifier.GetModuleSymbolForEmittedImage();
                Debug.Assert(peModuleSymbol != null);
                symbolValidator(peModuleSymbol, emitOptions);
            }
        }

        // The purpose of this method is simply to check that the signature of the 'Emit' method 
        // matches the 'Emitter' delegate type that it will be dynamically assigned to...
        // That is, we will catch mismatches due to changes in the signatures at compile time instead
        // of getting an opaque ArgumentException from the call to CreateDelegate.
        private static void TestEmitSignature()
        {
            Emitter emitter = Emit;
        }

        static internal CompilationVerifier Emit(
            CommonTestBase test,
            Compilation compilation,
            IEnumerable<ModuleData> dependencies,
            EmitOptions emitOptions,
            IEnumerable<ResourceDescription> manifestResources,
            SignatureDescription[] expectedSignatures,
            string expectedOutput,
            Action<PEAssembly, EmitOptions> assemblyValidator,
            Action<IModuleSymbol, EmitOptions> symbolValidator,
            bool collectEmittedAssembly,
            bool emitPdb,
            bool verify)
        {
            CompilationVerifier verifier = null;

            // We only handle CCI emit here for now...
            if (emitOptions != EmitOptions.RefEmit)
            {
                verifier = new CompilationVerifier(test, compilation, dependencies);

                verifier.Emit(expectedOutput, manifestResources, emitPdb, verify, expectedSignatures);

                // We're dual-purposing EmitOptions here.  In this context, it
                // tells the validator the version of Emit that is calling it. 
                RunValidators(verifier, EmitOptions.CCI, assemblyValidator, symbolValidator);
            }

            return verifier;
        }

        /// <summary>
        /// Reads content of the specified file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>Read-only binary data read from the file.</returns>
        public static ImmutableArray<byte> ReadFromFile(string path)
        {
            return ImmutableArray.Create<byte>(File.ReadAllBytes(path));
        }

        internal static void EmitILToArray(
            string ilSource,
            bool appendDefaultHeader,
            bool includePdb,
            out ImmutableArray<byte> assemblyBytes,
            out ImmutableArray<byte> pdbBytes)
        {
            string assemblyPath;
            string pdbPath;
            SharedCompilationUtils.IlasmTempAssembly(ilSource, appendDefaultHeader, includePdb, out assemblyPath, out pdbPath);

            Assert.NotNull(assemblyPath);
            Assert.Equal(pdbPath != null, includePdb);

            using (new DisposableFile(assemblyPath))
            {
                assemblyBytes = ReadFromFile(assemblyPath);
            }

            if (pdbPath != null)
            {
                using (new DisposableFile(pdbPath))
                {
                    pdbBytes = ReadFromFile(pdbPath);
                }
            }
            else
            {
                pdbBytes = default(ImmutableArray<byte>);
            }
        }

        internal static MetadataReference CompileIL(string ilSource, bool appendDefaultHeader = true, bool embedInteropTypes = false)
        {
            ImmutableArray<byte> assemblyBytes;
            ImmutableArray<byte> pdbBytes;
            EmitILToArray(ilSource, appendDefaultHeader, includePdb: false, assemblyBytes: out assemblyBytes, pdbBytes: out pdbBytes);
            return new MetadataImageReference(assemblyBytes, embedInteropTypes: embedInteropTypes);
        }

        internal static MetadataReference CreateReflectionEmitAssembly(Action<ModuleBuilder> create)
        {
            using (var file = new DisposableFile(extension: ".dll"))
            {
                var name = Path.GetFileName(file.Path);
                var appDomain = AppDomain.CurrentDomain;
                var assembly = appDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Save, Path.GetDirectoryName(file.Path));
                var module = assembly.DefineDynamicModule(CommonTestBase.GetUniqueName(), name);
                create(module);
                assembly.Save(name);

                var image = CommonTestBase.ReadFromFile(file.Path);
                return new MetadataImageReference(image);
            }
        }

        #endregion

        #region Compilation Creation Helpers

        protected CSharp.CSharpCompilation CreateCSharpCompilation(
            XCData code,
            CSharp.CSharpParseOptions parseOptions = null,
            CSharp.CSharpCompilationOptions compilationOptions = null,
            string assemblyName = null,
            IEnumerable<MetadataReference> referencedAssemblies = null)
        {
            return CreateCSharpCompilation(assemblyName, code, parseOptions, compilationOptions, referencedAssemblies, referencedCompilations: null);
        }

        protected CSharp.CSharpCompilation CreateCSharpCompilation(
            string assemblyName,
            XCData code,
            CSharp.CSharpParseOptions parseOptions = null,
            CSharp.CSharpCompilationOptions compilationOptions = null,
            IEnumerable<MetadataReference> referencedAssemblies = null,
            IEnumerable<Compilation> referencedCompilations = null)
        {
            return CreateCSharpCompilation(
                assemblyName,
                code.Value,
                parseOptions,
                compilationOptions,
                referencedAssemblies,
                referencedCompilations);
        }

        protected VisualBasic.VisualBasicCompilation CreateVisualBasicCompilation(
            XCData code,
            VisualBasic.VisualBasicParseOptions parseOptions = null,
            VisualBasic.VisualBasicCompilationOptions compilationOptions = null,
            string assemblyName = null,
            IEnumerable<MetadataReference> referencedAssemblies = null)
        {
            return CreateVisualBasicCompilation(assemblyName, code, parseOptions, compilationOptions, referencedAssemblies, referencedCompilations: null);
        }

        protected VisualBasic.VisualBasicCompilation CreateVisualBasicCompilation(
            string assemblyName,
            XCData code,
            VisualBasic.VisualBasicParseOptions parseOptions = null,
            VisualBasic.VisualBasicCompilationOptions compilationOptions = null,
            IEnumerable<MetadataReference> referencedAssemblies = null,
            IEnumerable<Compilation> referencedCompilations = null)
        {
            return CreateVisualBasicCompilation(
                assemblyName,
                code.Value,
                parseOptions,
                compilationOptions,
                referencedAssemblies,
                referencedCompilations);
        }

        protected CSharp.CSharpCompilation CreateCSharpCompilation(
            string code,
            CSharp.CSharpParseOptions parseOptions = null,
            CSharp.CSharpCompilationOptions compilationOptions = null,
            string assemblyName = null,
            IEnumerable<MetadataReference> referencedAssemblies = null)
        {
            return CreateCSharpCompilation(assemblyName, code, parseOptions, compilationOptions, referencedAssemblies, referencedCompilations: null);
        }

        protected CSharp.CSharpCompilation CreateCSharpCompilation(
            string assemblyName,
            string code,
            CSharp.CSharpParseOptions parseOptions = null,
            CSharp.CSharpCompilationOptions compilationOptions = null,
            IEnumerable<MetadataReference> referencedAssemblies = null,
            IEnumerable<Compilation> referencedCompilations = null)
        {
            if (assemblyName == null)
            {
                assemblyName = GetUniqueName();
            }
            
            if (parseOptions == null)
            {
                parseOptions = CSharp.CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.None);
            }

            if (compilationOptions == null)
            {
                compilationOptions = new CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            }

            var references = new List<MetadataReference>();
            if (referencedAssemblies == null)
            {
                references.Add(MscorlibRef);
                references.Add(SystemRef);
                references.Add(SystemCoreRef);
                //TODO: references.Add(MsCSRef);
                references.Add(SystemXmlRef);
                references.Add(SystemXmlLinqRef);
            }
            else
            {
                references.AddRange(referencedAssemblies);
            }

            AddReferencedCompilations(referencedCompilations, references);

            var tree = CSharp.SyntaxFactory.ParseSyntaxTree(code, options: parseOptions);

            return CSharp.CSharpCompilation.Create(assemblyName, new[] { tree }, references, compilationOptions);
        }

        protected VisualBasic.VisualBasicCompilation CreateVisualBasicCompilation(
            string code,
            VisualBasic.VisualBasicParseOptions parseOptions = null,
            VisualBasic.VisualBasicCompilationOptions compilationOptions = null,
            string assemblyName = null,
            IEnumerable<MetadataReference> referencedAssemblies = null)
        {
            return CreateVisualBasicCompilation(assemblyName, code, parseOptions, compilationOptions, referencedAssemblies, referencedCompilations: null);
        }

        protected VisualBasic.VisualBasicCompilation CreateVisualBasicCompilation(
            string assemblyName,
            string code,
            VisualBasic.VisualBasicParseOptions parseOptions = null,
            VisualBasic.VisualBasicCompilationOptions compilationOptions = null,
            IEnumerable<MetadataReference> referencedAssemblies = null,
            IEnumerable<Compilation> referencedCompilations = null)
        {
            if (assemblyName == null)
            {
                assemblyName = GetUniqueName();
            }
                        
            if (parseOptions == null)
            {
                parseOptions = VisualBasic.VisualBasicParseOptions.Default;
            }

            if (compilationOptions == null)
            {
                compilationOptions = new VisualBasic.VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            }

            var references = new List<MetadataReference>();
            if (referencedAssemblies == null)
            {
                references.Add(MscorlibRef);
                references.Add(SystemRef);
                references.Add(SystemCoreRef);
                references.Add(MsvbRef);
                references.Add(SystemXmlRef);
                references.Add(SystemXmlLinqRef);
            }
            else
            {
                references.AddRange(referencedAssemblies);
            }

            AddReferencedCompilations(referencedCompilations, references);

            var tree = VisualBasic.VisualBasicSyntaxTree.ParseText(code, options: parseOptions);

            return VisualBasic.VisualBasicCompilation.Create(assemblyName, new[] { tree }, references, compilationOptions);
        }

        private void AddReferencedCompilations(IEnumerable<Compilation> referencedCompilations, List<MetadataReference> references)
        {
            if (referencedCompilations != null)
            {
                foreach (var referencedCompilation in referencedCompilations)
                {
                    references.Add(referencedCompilation.EmitToImageReference());
                }
            }
        }

        #endregion

        #region IL Verification

        internal abstract string VisualizeRealIL(IModuleSymbol peModule, CompilationTestData.MethodData methodData);

        #endregion

        #region Other Helpers
        internal static ModulePropertiesForSerialization GetDefaultModulePropertiesForSerialization()
        {
            return new ModulePropertiesForSerialization(
                persistentIdentifier: default(Guid),
                fileAlignment: ModulePropertiesForSerialization.DefaultFileAlignment32Bit,
                targetRuntimeVersion: "v4.0.30319",
                platform: Platform.AnyCpu,
                trackDebugData: false,
                baseAddress: ModulePropertiesForSerialization.DefaultExeBaseAddress32Bit,
                sizeOfHeapReserve: ModulePropertiesForSerialization.DefaultSizeOfHeapReserve32Bit,
                sizeOfHeapCommit: ModulePropertiesForSerialization.DefaultSizeOfHeapCommit32Bit,
                sizeOfStackReserve: ModulePropertiesForSerialization.DefaultSizeOfStackReserve32Bit,
                sizeOfStackCommit: ModulePropertiesForSerialization.DefaultSizeOfStackCommit32Bit,
                enableHighEntropyVA: true,
                strongNameSigned: false,
                configureToExecuteInAppContainer: false,
                subsystemVersion: default(SubsystemVersion));
        }

        /// <summary>
        /// Given a list of compilers to look for, determines which is the first to actually appear on the Filesystem.
        /// </summary>
        /// <param name="compilerPriority">A list of assembly names that may exist in the system.</param>
        /// <returns>The compiler which had the highest priority and was found.</returns>
        protected static string FindCompiler(IEnumerable<string> compilerPriority)
        {
            string highestPriorityCompiler = string.Empty;
            var versionCheck = true;
            var testVersion = Assembly.GetAssembly(typeof(CommonTestBase)).GetName().Version;

            foreach (string path in compilerPriority)
            {
                try
                {
                    var compilerAssembly = Assembly.LoadFile(path);

                    if (!versionCheck || compilerAssembly.GetName().Version.Equals(testVersion))
                    {
                        highestPriorityCompiler = path;
                    }
                    break;
                }
                catch
                {
                    continue;
                }
            }

            return highestPriorityCompiler;
        }

        #endregion

        private static MetadataReference scriptingRef = null;      
        protected static MetadataReference MockScriptingRef
        {
            get
            {
                if (scriptingRef == null)
                {
                    scriptingRef = CSharp.CSharpCompilation.Create("Roslyn.Scripting",                                       
                                       new[] { CSharp.CSharpSyntaxTree.ParseText(@"
                                           using System;

                                           namespace Roslyn.Scripting
                                           { 
                                               public class Session
                                               {
                                               }
                                           }

                                           namespace Microsoft.CSharp.RuntimeHelpers
                                           {
                                               using Roslyn.Scripting;

                                               public static class SessionHelpers
                                               {
                                                   public static object GetSubmission(Session session, int id)
                                                   {
                                                       throw new NotImplementedException();
                                                   }

                                                   public static object SetSubmission(Session session, int slotIndex, object submission)
                                                   {
                                                       throw new NotImplementedException();
                                                   }
                                               }
                                           }")
                                       } ,
                                       new[] { MscorlibRef },
                                       new CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)).EmitToImageReference();
                }

                return scriptingRef;
            }
        }
    }
}
