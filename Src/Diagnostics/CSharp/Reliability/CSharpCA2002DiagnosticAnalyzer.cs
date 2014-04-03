// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FxCopAnalyzers.Reliability;

namespace Microsoft.CodeAnalysis.CSharp.FxCopAnalyzers.Reliability
{
    /// <summary>
    /// CA2002: Do not lock on objects with weak identities
    /// 
    /// Cause:
    /// A thread that attempts to acquire a lock on an object that has a weak identity could cause hangs.
    /// 
    /// Description:
    /// An object is said to have a weak identity when it can be directly accessed across application domain boundaries. 
    /// A thread that tries to acquire a lock on an object that has a weak identity can be blocked by a second thread in 
    /// a different application domain that has a lock on the same object. 
    /// </summary>
    [DiagnosticAnalyzer]
    [ExportDiagnosticAnalyzer(RuleId, LanguageNames.CSharp)]
    public class CSharpCA2002DiagnosticAnalyzer : CA2002DiagnosticAnalyzer, ISyntaxNodeAnalyzer<SyntaxKind>
    {
        private static readonly ImmutableArray<SyntaxKind> kindsOfInterest = ImmutableArray.Create<SyntaxKind>(SyntaxKind.LockStatement);

        public ImmutableArray<SyntaxKind> SyntaxKindsOfInterest
        {
            get
            {
                return kindsOfInterest;
            }
        }

        public void AnalyzeNode(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken)
        {
            var lockStatement = (LockStatementSyntax)node;
            GetDiagnosticsForNode(lockStatement.Expression, semanticModel, addDiagnostic);
        }
    }
}
