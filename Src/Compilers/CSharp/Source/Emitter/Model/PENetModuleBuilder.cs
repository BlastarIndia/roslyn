﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.Emit;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Emit
{
    internal sealed class PENetModuleBuilder : PEModuleBuilder
    {
        internal PENetModuleBuilder(SourceModuleSymbol sourceModule, string outputName, ModulePropertiesForSerialization serializationProperties, IEnumerable<ResourceDescription> manifestResources)
            : base(sourceModule, outputName, OutputKind.NetModule, serializationProperties, manifestResources, assemblySymbolMapper: null)
        {
        }

        protected override void AddEmbeddedResourcesFromAddedModules(ArrayBuilder<Cci.ManagedResource> builder, DiagnosticBag diagnostics)
        {
            throw ExceptionUtilities.Unreachable;
        }
    }
}
