﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.FxCopAnalyzers.Design;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FxCopAnalyzers;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.VisualBasic.FxCopAnalyzers.Design;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public partial class CA1018Tests : DiagnosticAnalyzerTestBase
    {
        protected override IDiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicCA1018DiagnosticAnalyzer();
        }

        protected override IDiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpCA1018DiagnosticAnalyzer();
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void TestCSSimpleAttributeClass()
        {
            VerifyCSharp(@"
using System;

class C : Attribute
{
}
", GetCA1018CSharpResultAt(4, 7, "C"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void TestCSInheritedAttributeClass()
        {
            VerifyCSharp(@"
using System;

[AttributeUsage(AttributeTargets.Method)]
class C : Attribute
{
}
class D : C
{
}
", GetCA1018CSharpResultAt(8, 7, "D"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void TestCSInheritedAttributeClassWithScope()
        {
            VerifyCSharp(@"
using System;

[|[AttributeUsage(AttributeTargets.Method)]
class C : Attribute
{
}|]
class D : C
{
}
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void TestVBSimpleAttributeClass()
        {
            VerifyBasic(@"
Imports System

Class C 
    Inherits Attribute
End Class
", GetCA1018BasicResultAt(4, 7, "C"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void TestVBInheritedAttributeClass()
        {
            VerifyBasic(@"
Imports System

<AttributeUsage(AttributeTargets.Method)>
Class C 
    Inherits Attribute
End Class
Class D
    Inherits C
End Class
", GetCA1018BasicResultAt(8, 7, "D"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void TestVBInheritedAttributeClassWithScope()
        {
            VerifyBasic(@"
Imports System

[|<AttributeUsage(AttributeTargets.Method)>
Class C 
    Inherits Attribute
End Class|]
Class D
    Inherits C
End Class
");
        }

        internal static string CA1018Name = "CA1018";
        internal static string CA1018Message = FxCopRulesResources.MarkAttributesWithAttributeUsage;

        private static DiagnosticResult GetCA1018CSharpResultAt(int line, int column, string objectName)
        {
            return GetCSharpResultAt(line, column, CA1018Name, string.Format(CA1018Message, objectName));
        }

        private static DiagnosticResult GetCA1018BasicResultAt(int line, int column, string objectName)
        {
            return GetBasicResultAt(line, column, CA1018Name, string.Format(CA1018Message, objectName));
        }
    }
}
