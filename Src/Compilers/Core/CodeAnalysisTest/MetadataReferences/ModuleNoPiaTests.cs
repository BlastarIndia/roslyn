﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public class NoPia : TestBase
    {
        [Fact]
        public void ContainsNoPiaLocalTypes()
        {
            using (AssemblyMetadata piaMetadata = AssemblyMetadata.CreateFromImage(TestResources.SymbolsTests.NoPia.Pia1),
                                    metadata1 = AssemblyMetadata.CreateFromImage(TestResources.SymbolsTests.NoPia.LocalTypes1),
                                    metadata2 = AssemblyMetadata.CreateFromImage(TestResources.SymbolsTests.NoPia.LocalTypes2))
            {
                var pia1 = piaMetadata.Assembly.Modules[0];
                var localTypes1 = metadata1.Assembly.Modules[0];
                var localTypes2 = metadata2.Assembly.Modules[0];

                Assert.False(pia1.ContainsNoPiaLocalTypes());
                Assert.False(pia1.ContainsNoPiaLocalTypes());

                Assert.True(localTypes1.ContainsNoPiaLocalTypes());
                Assert.True(localTypes1.ContainsNoPiaLocalTypes());

                Assert.True(localTypes2.ContainsNoPiaLocalTypes());
                Assert.True(localTypes2.ContainsNoPiaLocalTypes());
            }
        }
    }
}
