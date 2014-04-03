﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.FxCopAnalyzers.Design;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FxCopAnalyzers;
using Microsoft.CodeAnalysis.FxCopAnalyzers.Design;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.VisualBasic.FxCopAnalyzers.Design;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public class CA1052Tests : DiagnosticAnalyzerTestBase
    {
        #region Verifiers
         
        protected override IDiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new BasicStaticTypeRulesDiagnosticAnalyzer();
        }

        protected override IDiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new CSharpStaticTypeRulesDiagnosticAnalyzer();
        }

        private static DiagnosticResult CSharpResult(int line, int column, string objectName)
        {
            return GetCSharpResultAt(line, column, StaticTypeRulesDiagnosticAnalyzer.CA1052RuleId, string.Format(FxCopRulesResources.StaticHolderTypeIsNotStatic, objectName));
        }

        private static DiagnosticResult BasicResult(int line, int column, string objectName)
        {
            return GetBasicResultAt(line, column, StaticTypeRulesDiagnosticAnalyzer.CA1052RuleId, string.Format(FxCopRulesResources.StaticHolderTypeIsNotStatic, objectName));
        }

        #endregion

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052EmptyClassCSharp()
        {
            VerifyCSharp(@"
public class C
{
}
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052EmptyClassBasic()
        {
            VerifyBasic(@"
Public Class C
End Class
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052ClassWithOperatorOverloadCSharp()
        {
            VerifyCSharp(@"
public class C
{
    public static int operator +(C a, C b)
    {
        return 0;
    }
}
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052ClassWithOperatorOverloadBasic()
        {
            VerifyBasic(@"
Public Class C
    Public Shared Operator +(a As C, b As C) As Integer
        Return 0
    End Operator
End Class
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052ClassWithStaticMethodCSharp()
        {
            VerifyCSharp(@"
public class C
{
    static void Foo()
    {
    }
}
",
                 CSharpResult(2, 14, "C"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052ClassWithStaticMethodCSharpWithScope()
        {
            VerifyCSharp(@"
[|public class B
{
}|]

public class C
{
    static void Foo()
    {
    }
}
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052ClassWithStaticMethodBasic()
        {
            VerifyBasic(@"
Public Class C
    Shared Sub Foo()
    End Sub
End Class
",
                BasicResult(2, 14, "C"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052ClassWithStaticMethodBasicwithScope()
        {
            VerifyBasic(@"
[|Public Class B
End Class|]

Public Class C
    Shared Sub Foo()
    End Sub
End Class
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052ClassWithStaticMethodAndOperatorOverloadCSharp()
        {
            VerifyCSharp(@"
public class C
{
    static void Foo()
    {
    }

    public static int operator +(C a, C b)
    {
        return 0;
    }
}
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052ClassWithStaticMethodAndOperatorOverloadBasic()
        {
            VerifyBasic(@"
Public Class C
    Shared Sub Foo()
    End Sub

    Public Shared Operator +(a As C, b As C) As Integer
        Return 0
    End Operator
End Class
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052PrivateClassWithStaticMethodCSharp()
        {
            VerifyCSharp(@"
class C
{
    static void Foo()
    {
    }
}
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1052PrivateClassWithStaticMethodBasic()
        {
            VerifyBasic(@"
Class C
    Shared Sub Foo()
    End Sub
End Class
");
        }
    }
}
