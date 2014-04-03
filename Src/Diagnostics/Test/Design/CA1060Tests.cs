﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.FxCopAnalyzers.Design;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FxCopAnalyzers.Design;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.VisualBasic.FxCopAnalyzers.Design;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests.Design
{
    public class CA1060Tests : DiagnosticAnalyzerTestBase
    {
        #region Verifiers

        protected override IDiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicCA1060DiagnosticAnalyzer();
        }

        protected override IDiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpCA1060DiagnosticAnalyzer();
        }

        private static DiagnosticResult CSharpResult(int line, int column)
        {
            return GetCSharpResultAt(line, column, CA1060DiagnosticAnalyzer.Rule.Id, CA1060DiagnosticAnalyzer.Rule.MessageFormat);
        }

        private static DiagnosticResult BasicResult(int line, int column)
        {
            return GetBasicResultAt(line, column, CA1060DiagnosticAnalyzer.Rule.Id, CA1060DiagnosticAnalyzer.Rule.MessageFormat);
        }

        #endregion

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1060ProperlyNamedClassCSharp()
        {
            VerifyCSharp(@"
using System.Runtime.InteropServices;

class NativeMethods
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}

class SafeNativeMethods
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}

class UnsafeNativeMethods
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1060ProperlyNamedClassBasic()
        {
            VerifyBasic(@"
Imports System.Runtime.InteropServices

Class NativeMethods
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class

Class SafeNativeMethods
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class

Class UnsafeNativeMethods
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1060ImproperlyNamedClassCSharp()
        {
            VerifyCSharp(@"
using System.Runtime.InteropServices;

class FooClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}

class BarClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}

class BazClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}
",
            CSharpResult(4, 7),
            CSharpResult(10, 7),
            CSharpResult(16, 7));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1060ImproperlyNamedClassCSharpWithScope()
        {
            VerifyCSharp(@"
using System.Runtime.InteropServices;

class FooClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}

[|class BarClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}|]

class BazClass
{
    [DllImport(""user32.dll"")]
    private static extern void Foo();
}
",
            CSharpResult(10, 7));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1060ImproperlyNamedClassBasic()
        {
            VerifyBasic(@"
Imports System.Runtime.InteropServices

Class FooClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class

Class BarClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class

Class BazClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class
",
            BasicResult(4, 7),
            BasicResult(10, 7),
            BasicResult(16, 7));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1060ImproperlyNamedClassBasicWithScope()
        {
            VerifyBasic(@"
Imports System.Runtime.InteropServices

Class FooClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class

[|Class BarClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class|]

Class BazClass
    <DllImport(""user32.dll"")>
    Private Shared Sub Foo()
    End Sub
End Class
",
            BasicResult(10, 7));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1060ClassesInNamespaceCSharp()
        {
            VerifyCSharp(@"
using System.Runtime.InteropServices;

namespace MyNamespace
{
    class NativeMethods
    {
        [DllImport(""user32.dll"")]
        private static extern void Foo();
    }

    class BarClass
    {
        [DllImport(""user32.dll"")]
        private static extern void Foo();
    }
}
",
            CSharpResult(12, 11));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1060ClassesInNamespaceBasic()
        {
            VerifyBasic(@"
Imports System.Runtime.InteropServices

Namespace MyNamespace
    Class NativeMethods
        <DllImport(""user32.dll"")>
        Private Shared Sub Foo()
        End Sub
    End Class

    Class BarClass
        <DllImport(""user32.dll"")>
        Private Shared Sub Foo()
        End Sub
    End Class
End Namespace
",
            BasicResult(11, 11));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1060NestedClassesCSharp()
        {
            VerifyCSharp(@"
using System.Runtime.InteropServices;

class Outer
{
    class BarClass
    {
        [DllImport(""user32.dll"")]
        private static extern void Foo();
    }
}
",
            CSharpResult(6, 11));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Diagnostics)]
        public void CA1060NestedClassesBasic()
        {
            VerifyBasic(@"
Imports System.Runtime.InteropServices

Class Outer
    Class BarClass
        <DllImport(""user32.dll"")>
        Private Shared Sub Foo()
        End Sub
    End Class
End Class
",
            BasicResult(5, 11));
        }
    }
}
