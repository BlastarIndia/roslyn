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
    // Some of the CA1052Tests tests hold true here as CA1052 and CA1053 are mutually exclusive
    public class CA1053Tests : DiagnosticAnalyzerTestBase
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

        internal static string RuleCA1053Name = "CA1053";
        internal static string RuleCA1053Text = "Type '{0}' is a static holder type and should not contain Instance Constructors";

        private static DiagnosticResult CSharpResult(int line, int column, string objectName)
        {
            return GetCSharpResultAt(line, column, StaticTypeRulesDiagnosticAnalyzer.CA1053RuleId, string.Format(FxCopRulesResources.StaticHolderTypesShouldNotHaveConstructorsMessage, objectName));
        }

        private static DiagnosticResult BasicResult(int line, int column, string objectName)
        {
            return GetBasicResultAt(line, column, StaticTypeRulesDiagnosticAnalyzer.CA1053RuleId, string.Format(FxCopRulesResources.StaticHolderTypesShouldNotHaveConstructorsMessage, objectName));
        }

        #endregion

        #region CSharp 
        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1053EmptyNestedClassCSharp()
        {
            VerifyCSharp(@"
public class C
{
    protected class D
    {
    }
}
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1053NonDefaultInstanceConstructorCSharp()
        {
            VerifyCSharp(@"
public class Program
{
    static void Main(string[] args)
    {
    }
    
    static Program()
    {
    }
    
    public Program(int x)
    {
    }

    private Program(int x , int y)
    {
    }
}
",
    CSharpResult(2, 14, "Program"));
        }

        #endregion

        #region VisualBasic
        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1053EmptyNestedClassBasic()
        {
            VerifyBasic(@"
Public Class C
    Protected Class A
    End Class
End Class
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1053NonDefaultInstanceConstructorBasic()
        {
            VerifyBasic(@"
Public Class C
    Shared Sub Main(args As String())
    End Sub

    Shared Sub New()
    End Sub

    Public Sub New(x As Integer)
    End Sub

    Private Sub New(x As Integer, y As Integer)
    End Sub
End Class
",
    BasicResult(2, 14, "C"));
        }
        #endregion 
    }
}
