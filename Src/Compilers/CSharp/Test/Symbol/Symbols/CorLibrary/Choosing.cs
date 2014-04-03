﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.Symbols.CorLibrary
{
    public class Choosing : CSharpTestBase
    {
        [Fact]
        public void MultipleMscorlibReferencesInMetadata()
        {
            var assemblies = MetadataTestHelpers.GetSymbolsForReferences(new[]
            {
                TestReferences.SymbolsTests.CorLibrary.GuidTest2.exe,
                TestReferences.NetFx.v4_0_21006.mscorlib
            });

            Assert.Same(assemblies[1], assemblies[0].Modules[0].CorLibrary()); 
        }

        [Fact, WorkItem(760148)]
        public void Bug760148_1()
        {
            var corLib = CreateCompilation(@"
namespace System
{
    public class Object
    {
    }
}
",compOptions: TestOptions.Dll);

            var obj = corLib.GetSpecialType(SpecialType.System_Object);

            Assert.False(obj.IsErrorType());
            Assert.Same(corLib.Assembly, obj.ContainingAssembly);

            var consumer = CreateCompilation(@"
public class Test
{
}
", new[] { new CSharpCompilationReference(corLib)}, compOptions: TestOptions.Dll);

            Assert.Same(obj, consumer.GetSpecialType(SpecialType.System_Object));
        }

        [Fact, WorkItem(760148)]
        public void Bug760148_2()
        {
            var corLib = CreateCompilation(@"
namespace System
{
    class Object
    {
    }
}
", compOptions: TestOptions.Dll);

            var consumer = CreateCompilation(@"
public class Test
{
}
", new[] { new CSharpCompilationReference(corLib) }, compOptions: TestOptions.Dll);

            Assert.True(consumer.GetSpecialType(SpecialType.System_Object).IsErrorType());
        }
    }
}
