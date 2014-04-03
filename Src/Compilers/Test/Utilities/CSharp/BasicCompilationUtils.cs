﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Symbols;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Roslyn.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public static class BasicCompilationUtils
    {
        public static MetadataReference CompileToMetadata(string source, string assemblyName = null, IEnumerable<MetadataReference> references = null, bool verify = true)
        {
            if (references == null)
            {
                references = new[] { TestBase.MscorlibRef };
            }
            var compilation = CreateCompilationWithMscorlib(source, assemblyName, references);
            var verifier = Instance.CompileAndVerify(compilation, emitOptions: EmitOptions.CCI, verify: verify);
            return new MetadataImageReference(verifier.EmittedAssemblyData);
        }

        private static VisualBasicCompilation CreateCompilationWithMscorlib(string source, string assemblyName, IEnumerable<MetadataReference> references)
        {
            if (assemblyName == null)
            {
                assemblyName = TestBase.GetUniqueName();
            }
            var tree = VisualBasicSyntaxTree.ParseText(source);
            var options = new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimize: true);
            return VisualBasicCompilation.Create(assemblyName, new[] { tree }, references, options);
        }

        private static BasicTestBase Instance = new BasicTestBase();

        private sealed class BasicTestBase : CommonTestBase
        {
            protected override CompilationOptions DefaultCompilationOptions
            {
                get { throw new NotImplementedException(); }
            }

            protected override CompilationOptions OptionsDll
            {
                get { throw new NotImplementedException(); }
            }

            protected override Compilation GetCompilationForEmit(IEnumerable<string> source, MetadataReference[] additionalRefs, CompilationOptions options)
            {
                throw new NotImplementedException();
            }

            internal override IEnumerable<IModuleSymbol> ReferencesToModuleSymbols(IEnumerable<MetadataReference> references, MetadataImportOptions importOptions = MetadataImportOptions.Public)
            {
                throw new NotImplementedException();
            }

            internal override string VisualizeRealIL(IModuleSymbol peModule, CodeAnalysis.CodeGen.CompilationTestData.MethodData methodData)
            {
                throw new NotImplementedException();
            }
        }
    }
}
