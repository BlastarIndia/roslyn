﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal sealed class GeneratedLabelSymbol : LabelSymbol
    {
        public GeneratedLabelSymbol(string name)
            : base(LabelName(name))
        {
        }

#if DEBUG
        static int sequence = 1;
#endif
        private static string LabelName(string name)
        {
#if DEBUG
            int seq = System.Threading.Interlocked.Add(ref sequence, 1);
            return "<" + name + "-" + (seq & 0xffff) + ">";
#else
            return name;
#endif
        }

        public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
        {
            get
            {
                return ImmutableArray<SyntaxReference>.Empty;
            }
        }

        public override bool IsImplicitlyDeclared
        {
            get { return true; }
        }
    }
}