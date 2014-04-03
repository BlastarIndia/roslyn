﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Schema;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public class CommonCommandLineParserTests : TestBase
    {
        private const int EN_US = 1033;
        
        private void VerifyCommandLineSplitter(string commandLine, string[] expected)
        {
            string[] actual = CommandLineSplitter.SplitCommandLine(commandLine);

            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        private RuleSet ParseRuleSet(string source, params string[] otherSources)
        {
            var dir = Temp.CreateDirectory();
            var file = dir.CreateFile("a.ruleset");
            file.WriteAllText(source);

            for (int i = 1; i <= otherSources.Length; i++)
            {
                var newFile = dir.CreateFile("file" + i + ".ruleset");
                newFile.WriteAllText(otherSources[i - 1]);
            }

            if (otherSources.Length != 0)
            {
                return RuleSet.LoadEffectiveRuleSetFromFile(file.Path);
            }

            return RuleSetProcessor.LoadFromFile(file.Path);
        }

        private void VerifyRuleSetError(string source, string message, bool locSpecific = true, string locMessage = "", params string[] otherSources)
        {
            try
            {
                ParseRuleSet(source, otherSources);
            }
            catch (Exception e)
            {
                if (CultureInfo.CurrentCulture.LCID == EN_US || CultureInfo.CurrentUICulture.LCID == EN_US || CultureInfo.CurrentCulture == CultureInfo.InvariantCulture || CultureInfo.CurrentUICulture == CultureInfo.InvariantCulture)
                {
                    Assert.Equal(message, e.Message);
                }
                else if (locSpecific)
                {
                    if (locMessage != "")
                        Assert.Contains(locMessage, e.Message);
                    else
                        Assert.Equal(message, e.Message);
                }

                return;
            }

            Assert.True(false, "Didn't return an error");
        }

        [Fact]
        public void TestCommandLineSplitter()
        {
            VerifyCommandLineSplitter("", new string[0]);
            VerifyCommandLineSplitter("   \t   ", new string[0]);
            VerifyCommandLineSplitter("   abc\tdef baz    quuz   ", new string[] {"abc", "def", "baz", "quuz"});
            VerifyCommandLineSplitter(@"  ""abc def""  fi""ddle dee de""e  ""hi there ""dude  he""llo there""  ",
                                        new string[] { @"abc def", @"fi""ddle dee de""e", @"""hi there ""dude", @"he""llo there""" });
            VerifyCommandLineSplitter(@"  ""abc def \"" baz quuz"" ""\""straw berry"" fi\""zz \""buzz fizzbuzz",
                                        new string[] { @"abc def "" baz quuz", @"""straw berry", @"fi""zz", @"""buzz", @"fizzbuzz"});
            VerifyCommandLineSplitter(@"  \\""abc def""  \\\""abc def"" ",
                                        new string[] { @"\""abc def""", @"\""abc", @"def""" });
            VerifyCommandLineSplitter(@"  \\\\""abc def""  \\\\\""abc def"" ",
                                        new string[] { @"\\""abc def""", @"\\""abc", @"def""" });
        }

        [Fact]
        public void TestRuleSetParsingDuplicateRule()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test""  ToolsVersion=""12.0"">
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
    <Rule Id=""CA1012"" Action=""Warning"" />
    <Rule Id=""CA1013"" Action=""Warning"" />
    <Rule Id=""CA1014"" Action=""None"" />
  </Rules>
</RuleSet>";

            string locMessage = string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "");
            VerifyRuleSetError(source, string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "There is a duplicate key sequence 'CA1012' for the 'UniqueRuleName' key or unique identity constraint."), locMessage:  locMessage);
        }

        [Fact]
        public void TestRuleSetParsingDuplicateRuleSet()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"">
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
<RuleSet Name=""Ruleset2"" Description=""Test"">
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            VerifyRuleSetError(source, "There are multiple root elements. Line 8, position 2.", false);
        }

        [Fact]
        public void TestRuleSetParsingIncludeAll1()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test""  ToolsVersion=""12.0"">
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.GeneralDiagnosticOption);
        }

        [Fact]
        public void TestRuleSetParsingIncludeAll2()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test""  ToolsVersion=""12.0"">
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source);
            Assert.Equal(ReportDiagnostic.Default, ruleSet.GeneralDiagnosticOption);
        }

        [Fact (Skip = "899271")]
        public void TestRuleSetParsingWithIncludeOfSameFile()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test""  ToolsVersion=""12.0"">
  <Include Path=""a.ruleset"" Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, new string[] { "" });
            Assert.Equal(ReportDiagnostic.Default, ruleSet.GeneralDiagnosticOption);
        }

        [Fact]
        public void TestRuleSetParsingIncludeAll3()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test""  ToolsVersion=""12.0"">
  <IncludeAll Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            string locMessage = string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "");
            VerifyRuleSetError(source, string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "The 'Action' attribute is invalid - The value 'Default' is invalid according to its datatype 'TIncludeAllAction' - The Enumeration constraint failed."), locMessage: locMessage);
        }

        [Fact]
        public void TestRuleSetParsingRulesMissingAttribute1()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test""  ToolsVersion=""12.0"">
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Action=""Error"" />
  </Rules>
</RuleSet>
";
            string locMessage = string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "");
            VerifyRuleSetError(source, string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "The required attribute 'Id' is missing."), locMessage: locMessage);
        }

        [Fact]
        public void TestRuleSetParsingRulesMissingAttribute2()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test""  ToolsVersion=""12.0"">
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" />
  </Rules>
</RuleSet>
";
            string locMessage = string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "");
            VerifyRuleSetError(source, string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "The required attribute 'Action' is missing."), locMessage: locMessage);
        }

        [Fact]
        public void TestRuleSetParsingRulesMissingAttribute3()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test""  ToolsVersion=""12.0"">
  <IncludeAll Action=""Warning"" />
  <Rules RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            string locMessage = string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "");
            VerifyRuleSetError(source, string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "The required attribute 'AnalyzerId' is missing."), locMessage: locMessage);
        }

        [Fact]
        public void TestRuleSetParsingRulesMissingAttribute4()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test""  ToolsVersion=""12.0"">
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            string locMessage = string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "");
            VerifyRuleSetError(source, string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "The required attribute 'RuleNamespace' is missing."), locMessage: locMessage);
        }

        [Fact]
        public void TestRuleSetParsingRulesMissingAttribute5()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" >
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            string locMessage = string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "");
            VerifyRuleSetError(source, string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "The required attribute 'ToolsVersion' is missing."), locMessage: locMessage);
        }

        [Fact]
        public void TestRuleSetParsingRulesMissingAttribute6()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            string locMessage = string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "");
            VerifyRuleSetError(source, string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "The required attribute 'Name' is missing."), locMessage: locMessage);
        }

        [Fact]
        public void TestRuleSetParsingRules()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
    <Rule Id=""CA1013"" Action=""Warning"" />
    <Rule Id=""CA1014"" Action=""None"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ruleSet.SpecificDiagnosticOptions["CA1012"], ReportDiagnostic.Error);
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ruleSet.SpecificDiagnosticOptions["CA1013"], ReportDiagnostic.Warn);
            Assert.Contains("CA1014", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ruleSet.SpecificDiagnosticOptions["CA1014"], ReportDiagnostic.Suppress);
        }

        [Fact]
        public void TestRuleSetParsingRules2()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Default"" />
    <Rule Id=""CA1013"" Action=""Warning"" />
    <Rule Id=""CA1014"" Action=""None"" />
  </Rules>
</RuleSet>
";
            string locMessage = string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "");
            VerifyRuleSetError(source, string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "The 'Action' attribute is invalid - The value 'Default' is invalid according to its datatype 'TRuleAction' - The Enumeration constraint failed."), locMessage: locMessage);
        }

        [Fact]
        public void TestRuleSetInclude()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""foo.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source);
            Assert.True(ruleSet.Includes.Count() == 1);
            Assert.Equal(ruleSet.Includes.First().Action, ReportDiagnostic.Default);
            Assert.Equal(ruleSet.Includes.First().IncludePath, "foo.ruleset");
        }

        [Fact]
        public void TestRuleSetInclude1()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""foo.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            VerifyRuleSetError(source, string.Format(CodeAnalysisResources.InvalidRuleSetInclude, "foo.ruleset", string.Format(CodeAnalysisResources.FailedToResolveRuleSetName, "foo.ruleset")), otherSources: new string[] {""});
        }

        [Fact]
        public void TestRuleSetInclude2()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";

            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            Assert.Equal(ReportDiagnostic.Default, ruleSet.GeneralDiagnosticOption);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1013"]);
        }

        [Fact]
        public void TestRuleSetIncludeGlobalStrict()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";

            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.GeneralDiagnosticOption);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1013"]);
        }

        [Fact]
        public void TestRuleSetIncludeGlobalStrict1()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Include Path=""file1.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";

            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.GeneralDiagnosticOption);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1013"]);
        }

        [Fact]
        public void TestRuleSetIncludeGlobalStrict2()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Include Path=""file1.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";

            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            Assert.Equal(ReportDiagnostic.Error, ruleSet.GeneralDiagnosticOption);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1013"]);
        }

        [Fact]
        public void TestRuleSetIncludeGlobalStrict3()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Include Path=""file1.ruleset"" Action=""Error"" />
  <Include Path=""file2.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";

            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1, source2);
            Assert.Equal(ReportDiagnostic.Error, ruleSet.GeneralDiagnosticOption);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Error, ruleSet.SpecificDiagnosticOptions["CA1013"]);
        }

        [Fact]
        public void TestRuleSetIncludeRecursiveIncludes()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Include Path=""file1.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";

            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Error"" />
  <Include Path=""file2.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1014"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1, source2);
            Assert.Equal(ReportDiagnostic.Error, ruleSet.GeneralDiagnosticOption);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1013"]);
            Assert.Contains("CA1014", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1014"]);
        }

        [Fact]
        public void TestRuleSetIncludeSpecificStrict1()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";

            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            // CA1012's value in source wins.
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
        }

        [Fact]
        public void TestRuleSetIncludeSpecificStrict2()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Default"" />
  <Include Path=""file2.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";

            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Error"" />
  <Include Path=""file2.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            string source2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1, source2);
            // CA1012's value in source still wins.
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
        }

        [Fact]
        public void TestRuleSetIncludeSpecificStrict3()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Default"" />
  <Include Path=""file2.ruleset"" Action=""Default"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";

            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Error"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1, source2);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            // CA1013's value in source2 wins.
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Error, ruleSet.SpecificDiagnosticOptions["CA1013"]);
        }

        [Fact]
        public void TestRuleSetIncludeEffectiveAction()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""None"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            Assert.DoesNotContain("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
        }

        [Fact]
        public void TestRuleSetIncludeEffectiveAction1()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Error, ruleSet.SpecificDiagnosticOptions["CA1013"]);
            Assert.Equal(ReportDiagnostic.Default, ruleSet.GeneralDiagnosticOption);
        }

        [Fact]
        public void TestRuleSetIncludeEffectiveActionGlobal1()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            Assert.Equal(ReportDiagnostic.Error, ruleSet.GeneralDiagnosticOption);
        }

        [Fact]
        public void TestRuleSetIncludeEffectiveActionGlobal2()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <IncludeAll Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.GeneralDiagnosticOption);
        }

        [Fact]
        public void TestRuleSetIncludeEffectiveActionSpecific1()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""None"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Suppress, ruleSet.SpecificDiagnosticOptions["CA1013"]);
        }

        [Fact]
        public void TestRuleSetIncludeEffectiveActionSpecific2()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            var ruleSet = ParseRuleSet(source, source1);
            Assert.Contains("CA1012", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1012"]);
            Assert.Contains("CA1013", ruleSet.SpecificDiagnosticOptions.Keys);
            Assert.Equal(ReportDiagnostic.Error, ruleSet.SpecificDiagnosticOptions["CA1013"]);
        }

        [Fact]
        public void TestAllCombinations()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""New Rule Set1"" Description=""Test"" ToolsVersion=""12.0"">
  <Include Path=""file1.ruleset"" Action=""Error"" />
  <Include Path=""file2.ruleset"" Action=""Warning"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1000"" Action=""Warning"" />
    <Rule Id=""CA1001"" Action=""Warning"" />
    <Rule Id=""CA2111"" Action=""None"" />
  </Rules>
</RuleSet>
";

            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""New Rule Set2"" Description=""Test"" ToolsVersion=""12.0"">
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA2100"" Action=""Warning"" />
    <Rule Id=""CA2111"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""New Rule Set3"" Description=""Test"" ToolsVersion=""12.0"">
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA2100"" Action=""Warning"" />
    <Rule Id=""CA2111"" Action=""Warning"" />
    <Rule Id=""CA2119"" Action=""None"" />
    <Rule Id=""CA2104"" Action=""Error"" />
    <Rule Id=""CA2105"" Action=""Warning"" />
  </Rules>
</RuleSet>";

            var ruleSet = ParseRuleSet(source, source1, source2);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1000"]);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA1001"]);
            Assert.Equal(ReportDiagnostic.Error, ruleSet.SpecificDiagnosticOptions["CA2100"]);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA2104"]);
            Assert.Equal(ReportDiagnostic.Warn, ruleSet.SpecificDiagnosticOptions["CA2105"]);
            Assert.Equal(ReportDiagnostic.Suppress, ruleSet.SpecificDiagnosticOptions["CA2111"]);
            Assert.Equal(ReportDiagnostic.Suppress, ruleSet.SpecificDiagnosticOptions["CA2119"]);
        }

        [Fact]
        public void TestRuleSetIncludeError()
        {
            string source = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset1"" Description=""Test"" ToolsVersion=""12.0"" >
  <Include Path=""file1.ruleset"" Action=""Error"" />
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1012"" Action=""Warning"" />
  </Rules>
</RuleSet>
";
            string source1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<RuleSet Name=""Ruleset2"" Description=""Test"" ToolsVersion=""12.0"" >
  <Rules AnalyzerId=""Microsoft.Analyzers.ManagedCodeAnalysis"" RuleNamespace=""Microsoft.Rules.Managed"">
    <Rule Id=""CA1013"" Action=""Default"" />
  </Rules>
</RuleSet>
";
            var dir = Temp.CreateDirectory();
            var file = dir.CreateFile("a.ruleset");
            file.WriteAllText(source);
            var newFile = dir.CreateFile("file1.ruleset");
            newFile.WriteAllText(source1);

            try
            {
                RuleSet.LoadEffectiveRuleSetFromFile(file.Path);
                Assert.True(false, "Didn't throw an exception");
            }
            catch (InvalidRuleSetException e)
            {
                Assert.Contains(string.Format(CodeAnalysisResources.InvalidRuleSetInclude, newFile.Path, string.Format(CodeAnalysisResources.RuleSetSchemaViolation, "")), e.Message);
            }
        }
    }
}
