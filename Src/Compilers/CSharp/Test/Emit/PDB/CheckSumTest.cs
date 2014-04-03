// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.PDB
{
    public class CheckSumTest : CSharpTestBase
    {
        private static CSharpCompilation CreateCompilationWithChecksums(string source, string filePath, string baseDirectory)
        {
            return CSharpCompilation.Create(
                GetUniqueName(),
                new[] { ParseWithChecksum(source, filePath) },
                new[] { MscorlibRef },
                TestOptions.Dll.WithSourceReferenceResolver(new SourceFileResolver(ImmutableArray.Create<string>(), baseDirectory)));
        }

        [Fact]
        public void CheckSumPragmaClashesSameTree()
        {
            var text =
@"
class C
{

#pragma checksum ""bogus.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""

// same
#pragma checksum ""bogus.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""

// different case in Hex numerics, but otherwise same
#pragma checksum ""bogus.cs"" ""{406ea660-64cf-4C82-B6F0-42D48172A799}"" ""AB007f1d23d9""

// different case in path, so not a clash
#pragma checksum ""bogUs.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A788}"" ""ab007f1d23d9""

// whitespace in path, so not a clash
#pragma checksum ""bogUs.cs "" ""{406EA660-64CF-4C82-B6F0-42D48172A788}"" ""ab007f1d23d9""

#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""
// and now a clash in Guid
#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A798}"" ""ab007f1d23d9""
// and now a clash in CheckSum
#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d8""

    static void Main(string[] args)
    {
    }
}
";
            
            CompileAndVerify(text, emitPdb: true).
                VerifyDiagnostics(
                // (20,1): warning CS1697: Different checksum values given for 'bogus1.cs'
                // #pragma checksum "bogus1.cs" "{406EA660-64CF-4C82-B6F0-42D48172A798}" "ab007f1d23d9"
                Diagnostic(ErrorCode.WRN_ConflictingChecksum, @"#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A798}"" ""ab007f1d23d9""").WithArguments("bogus1.cs"),
                // (22,1): warning CS1697: Different checksum values given for 'bogus1.cs'
                // #pragma checksum "bogus1.cs" "{406EA660-64CF-4C82-B6F0-42D48172A799}" "ab007f1d23d8"
                Diagnostic(ErrorCode.WRN_ConflictingChecksum, @"#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d8""").WithArguments("bogus1.cs"));
        }

        [Fact]
        public void CheckSumPragmaClashesDifferentLength()
        {
            var text =
@"
class C
{

#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""
#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23""
#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" """"
#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""

// odd length, parsing warning, ignored by emit.
#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d""

// bad Guid, parsing warning, ignored by emit.
#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A79}"" ""ab007f1d23d9""

    static void Main(string[] args)
    {
    }
}
";

            CompileAndVerify(text, emitPdb: true).
                VerifyDiagnostics(
                // (11,71): warning CS1695: Invalid #pragma checksum syntax; should be #pragma checksum "filename" "{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}" "XXXX..."
                // #pragma checksum "bogus1.cs" "{406EA660-64CF-4C82-B6F0-42D48172A799}" "ab007f1d23d"
                Diagnostic(ErrorCode.WRN_IllegalPPChecksum, @"""ab007f1d23d"""),
                // (14,30): warning CS1695: Invalid #pragma checksum syntax; should be #pragma checksum "filename" "{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}" "XXXX..."
                // #pragma checksum "bogus1.cs" "{406EA660-64CF-4C82-B6F0-42D48172A79}" "ab007f1d23d9"
                Diagnostic(ErrorCode.WRN_IllegalPPChecksum, @"""{406EA660-64CF-4C82-B6F0-42D48172A79}"""),
                // (6,1): warning CS1697: Different checksum values given for 'bogus1.cs'
                // #pragma checksum "bogus1.cs" "{406EA660-64CF-4C82-B6F0-42D48172A799}" "ab007f1d23"
                Diagnostic(ErrorCode.WRN_ConflictingChecksum, @"#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23""").WithArguments("bogus1.cs"),
                // (7,1): warning CS1697: Different checksum values given for 'bogus1.cs'
                // #pragma checksum "bogus1.cs" "{406EA660-64CF-4C82-B6F0-42D48172A799}" ""
                Diagnostic(ErrorCode.WRN_ConflictingChecksum, @"#pragma checksum ""bogus1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" """"").WithArguments("bogus1.cs"));
        }

        [Fact]
        public void CheckSumPragmaClashesDifferentTrees()
        {
            var text1 =
@"
class C
{

#pragma checksum ""bogus.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""

// same
#pragma checksum ""bogus.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""

    static void Main(string[] args)
    {
    }
}
";

            var text2 =
@"
class C1
{

#pragma checksum ""bogus.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""

// same
#pragma checksum ""bogus.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""

// different
#pragma checksum ""bogus.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23""

}
";

            CompileAndVerify(new string[] {text1, text2}, emitPdb: true).
                VerifyDiagnostics(
                // (11,1): warning CS1697: Different checksum values given for 'bogus.cs'
                // #pragma checksum "bogus.cs" "{406EA660-64CF-4C82-B6F0-42D48172A799}" "ab007f1d23"
                Diagnostic(ErrorCode.WRN_ConflictingChecksum, @"#pragma checksum ""bogus.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23""").WithArguments("bogus.cs"));
        }

        [Fact]
        public void TestPartialClassFieldInitializers()
        {
            var text1 = @"
public partial class C
{
    int x = 1;
}

#pragma checksum ""UNUSED.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""

#pragma checksum ""USED1.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""

";

            var text2 = @"
public partial class C
{
#pragma checksum ""USED2.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""

int y = 1;

    static void Main()
    {

#line 112 ""USED1.cs""
        C c = new C();

#line 112 ""USED2.cs""
        C c1 = new C();

#line default

    }
}
";
            //Having a unique name here may be important. The infrastructure of the pdb to xml conversion
            //loads the assembly into the ReflectionOnlyLoadFrom context.
            //So it's probably a good idea to have a new name for each assembly.
            var compilation = CreateCompilationWithMscorlib(new SyntaxTree[] { ParseWithChecksum(text1, "a.cs"), ParseWithChecksum(text2, "b.cs") });

            string actual = PDBTests.GetPdbXml(compilation, "C.Main");

            string expected = @"<symbols>
  <files>
    <file id=""1"" name=""USED1.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""406ea660-64cf-4c82-b6f0-42d48172a799"" checkSum=""AB,  0, 7F, 1D, 23, D9, "" />
    <file id=""2"" name=""USED2.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""406ea660-64cf-4c82-b6f0-42d48172a799"" checkSum=""AB,  0, 7F, 1D, 23, D9, "" />
    <file id=""3"" name=""b.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""ff1816ec-aa5e-4d10-87f7-6f4963833460"" checkSum=""5C, 91, 93, 78, 46, CD, B4, D0, C0, 83, 68, 3D,  E, 37, 28, C5,  5, 5D, 45, 3F, "" />
    <file id=""4"" name=""a.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""ff1816ec-aa5e-4d10-87f7-6f4963833460"" checkSum=""EF, A0, 2C, F4, E1, A8, 7F, 16, 9C, A7, 52, 1E, A4, FA, 19, D0, 8F, 23, 8F, 43, "" />
  </files>
  <methods>
    <method containingType=""C"" name=""Main"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""0"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""3"">
        <entry il_offset=""0x0"" start_row=""112"" start_column=""9"" end_row=""112"" end_column=""23"" file_ref=""1"" />
        <entry il_offset=""0x6"" start_row=""112"" start_column=""9"" end_row=""112"" end_column=""24"" file_ref=""2"" />
        <entry il_offset=""0xc"" start_row=""19"" start_column=""5"" end_row=""19"" end_column=""6"" file_ref=""3"" />
      </sequencepoints>
      <locals />
    </method>
  </methods>
</symbols>";
            PDBTests.AssertXmlEqual(expected, actual);
        }

        [WorkItem(729235)]
        [Fact]
        public void NormalizedPath_Tree()
        {
            var source = @"
class C
{
    void M()
    {
    }
}
";


            var comp = CreateCompilationWithChecksums(source, "b.cs", @"b:\base");
            string actual = PDBTests.GetPdbXml(comp, "C.M");

            // Verify the value of name attribute in file element.
            string expected = @"
<symbols>
  <files>
    <file id=""1"" name=""b:\base\b.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""ff1816ec-aa5e-4d10-87f7-6f4963833460"" checkSum="" B, 29, 41, 20, A5, D6,  3, 30, 3A, EA, 97, 34, 92, 87, 44, 13, CF, 1E, EF, 58, "" />
  </files>
  <methods>
    <method containingType=""C"" name=""M"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""0"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""2"">
        <entry il_offset=""0x0"" start_row=""5"" start_column=""5"" end_row=""5"" end_column=""6"" file_ref=""1"" />
        <entry il_offset=""0x1"" start_row=""6"" start_column=""5"" end_row=""6"" end_column=""6"" file_ref=""1"" />
      </sequencepoints>
      <locals />
    </method>
  </methods>
</symbols>";

            PDBTests.AssertXmlEqual(expected, actual);
        }

        [Fact]
        public void NoResolver()
        {
            var comp = CSharpCompilation.Create(
                GetUniqueName(),
                new[] { ParseWithChecksum(@"
#pragma checksum ""a\..\a.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d5""
#line 10 ""a\..\a.cs""
class C { void M() { } }

", @"C:\a\..\b.cs") },
                new[] { MscorlibRef },
                TestOptions.Dll.WithSourceReferenceResolver(null));

            string actual = PDBTests.GetPdbXml(comp, "C.M");

            // Verify the value of name attribute in file element.
            string expected = @"
<symbols>
  <files>
    <file id=""1"" name=""a\..\a.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""406ea660-64cf-4c82-b6f0-42d48172a799"" checkSum=""AB,  0, 7F, 1D, 23, D5, "" />
  </files>
  <methods>
    <method containingType=""C"" name=""M"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""0"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""2"">
        <entry il_offset=""0x0"" start_row=""10"" start_column=""20"" end_row=""10"" end_column=""21"" file_ref=""1"" />
        <entry il_offset=""0x1"" start_row=""10"" start_column=""22"" end_row=""10"" end_column=""23"" file_ref=""1"" />
      </sequencepoints>
      <locals />
    </method>
  </methods>
</symbols>";

            PDBTests.AssertXmlEqual(expected, actual);
        }

        [WorkItem(729235)]
        [Fact]
        public void NormalizedPath_LineDirective()
        {
            var source = @"
class C
{
    void M()
    {
        M();
#line 1 ""line.cs""
        M();
#line 2 ""./line.cs""
        M();
#line 3 "".\line.cs""
        M();
#line 4 ""q\..\line.cs""
        M();
#line 5 ""q:\absolute\file.cs""
        M();
    }
}
";

            var comp = CreateCompilationWithChecksums(source, "b.cs", @"b:\base");
            string actual = PDBTests.GetPdbXml(comp, "C.M");

            // Verify the fact that there's a single file element for "line.cs" and it has an absolute path.
            // Verify the fact that the path that was already absolute wasn't affected by the base directory.
            string expected = @"
<symbols>
  <files>
    <file id=""1"" name=""b:\base\b.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""ff1816ec-aa5e-4d10-87f7-6f4963833460"" checkSum=""56, 67, 64, B1, B1, 58, 3D, 5D,  9, C6, 27, 81, 19, EB, F9, 64, 4A, 5D, 26, 3E, "" />
    <file id=""2"" name=""b:\base\line.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" />
    <file id=""3"" name=""q:\absolute\file.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" />
  </files>
  <methods>
    <method containingType=""C"" name=""M"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""0"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""8"">
        <entry il_offset=""0x0"" start_row=""5"" start_column=""5"" end_row=""5"" end_column=""6"" file_ref=""1"" />
        <entry il_offset=""0x1"" start_row=""6"" start_column=""9"" end_row=""6"" end_column=""13"" file_ref=""1"" />
        <entry il_offset=""0x8"" start_row=""1"" start_column=""9"" end_row=""1"" end_column=""13"" file_ref=""2"" />
        <entry il_offset=""0xf"" start_row=""2"" start_column=""9"" end_row=""2"" end_column=""13"" file_ref=""2"" />
        <entry il_offset=""0x16"" start_row=""3"" start_column=""9"" end_row=""3"" end_column=""13"" file_ref=""2"" />
        <entry il_offset=""0x1d"" start_row=""4"" start_column=""9"" end_row=""4"" end_column=""13"" file_ref=""2"" />
        <entry il_offset=""0x24"" start_row=""5"" start_column=""9"" end_row=""5"" end_column=""13"" file_ref=""3"" />
        <entry il_offset=""0x2b"" start_row=""6"" start_column=""5"" end_row=""6"" end_column=""6"" file_ref=""3"" />
      </sequencepoints>
      <locals />
    </method>
  </methods>
</symbols>";

            PDBTests.AssertXmlEqual(expected, actual);
        }

        [WorkItem(729235)]
        [Fact]
        public void NormalizedPath_ChecksumDirective()
        {
            var source = @"
class C
{
#pragma checksum ""a.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d5""
#pragma checksum ""./b.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d6""
#pragma checksum "".\c.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d7""
#pragma checksum ""q\..\d.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d8""
#pragma checksum ""b:\base\e.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d9""
    void M()
    {
        M();
#line 1 ""a.cs""
        M();
#line 1 ""b.cs""
        M();
#line 1 ""c.cs""
        M();
#line 1 ""d.cs""
        M();
#line 1 ""e.cs""
        M();
    }
}
";

            var comp = CreateCompilationWithChecksums(source, "file.cs", @"b:\base");
            comp.VerifyDiagnostics();
            string actual = PDBTests.GetPdbXml(comp, "C.M");

            // Verify the fact that all pragmas are referenced, even though the paths differ before normalization.
            string expected = @"
<symbols>
  <files>
    <file id=""1"" name=""b:\base\file.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""ff1816ec-aa5e-4d10-87f7-6f4963833460"" checkSum=""90, 98, 68, 60, F2, E3, 3E, B4, 61, 4A, FA,  4, 86, 74, 74, 58,  7, E4, 54, 2E, "" />
    <file id=""2"" name=""b:\base\a.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""406ea660-64cf-4c82-b6f0-42d48172a799"" checkSum=""AB,  0, 7F, 1D, 23, D5, "" />
    <file id=""3"" name=""b:\base\b.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""406ea660-64cf-4c82-b6f0-42d48172a799"" checkSum=""AB,  0, 7F, 1D, 23, D6, "" />
    <file id=""4"" name=""b:\base\c.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""406ea660-64cf-4c82-b6f0-42d48172a799"" checkSum=""AB,  0, 7F, 1D, 23, D7, "" />
    <file id=""5"" name=""b:\base\d.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""406ea660-64cf-4c82-b6f0-42d48172a799"" checkSum=""AB,  0, 7F, 1D, 23, D8, "" />
    <file id=""6"" name=""b:\base\e.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""406ea660-64cf-4c82-b6f0-42d48172a799"" checkSum=""AB,  0, 7F, 1D, 23, D9, "" />
  </files>
  <methods>
    <method containingType=""C"" name=""M"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""0"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""8"">
        <entry il_offset=""0x0"" start_row=""10"" start_column=""5"" end_row=""10"" end_column=""6"" file_ref=""1"" />
        <entry il_offset=""0x1"" start_row=""11"" start_column=""9"" end_row=""11"" end_column=""13"" file_ref=""1"" />
        <entry il_offset=""0x8"" start_row=""1"" start_column=""9"" end_row=""1"" end_column=""13"" file_ref=""2"" />
        <entry il_offset=""0xf"" start_row=""1"" start_column=""9"" end_row=""1"" end_column=""13"" file_ref=""3"" />
        <entry il_offset=""0x16"" start_row=""1"" start_column=""9"" end_row=""1"" end_column=""13"" file_ref=""4"" />
        <entry il_offset=""0x1d"" start_row=""1"" start_column=""9"" end_row=""1"" end_column=""13"" file_ref=""5"" />
        <entry il_offset=""0x24"" start_row=""1"" start_column=""9"" end_row=""1"" end_column=""13"" file_ref=""6"" />
        <entry il_offset=""0x2b"" start_row=""2"" start_column=""5"" end_row=""2"" end_column=""6"" file_ref=""6"" />
      </sequencepoints>
      <locals />
    </method>
  </methods>
</symbols>";

            PDBTests.AssertXmlEqual(expected, actual);
        }

        [WorkItem(729235)]
        [Fact]
        public void NormalizedPath_NoBaseDirectory()
        {
            var source = @"
class C
{
#pragma checksum ""a.cs"" ""{406EA660-64CF-4C82-B6F0-42D48172A799}"" ""ab007f1d23d5""
    void M()
    {
        M();
#line 1 ""a.cs""
        M();
#line 1 ""./a.cs""
        M();
#line 1 ""b.cs""
        M();
    }
}
";

            var comp = CreateCompilationWithChecksums(source, "file.cs", null);
            comp.VerifyDiagnostics();
            string actual = PDBTests.GetPdbXml(comp, "C.M");

            // Verify nothing blew up.
            string expected = @"
<symbols>
  <files>
    <file id=""1"" name=""file.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""ff1816ec-aa5e-4d10-87f7-6f4963833460"" checkSum=""CB, C0, E4, C6, CF, 21, DD, D7, AB, A5, CF, 80, CA, 59, C2, 35, A6, 1D, 85,  0, "" />
    <file id=""2"" name=""a.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" checkSumAlgorithmId=""406ea660-64cf-4c82-b6f0-42d48172a799"" checkSum=""AB,  0, 7F, 1D, 23, D5, "" />
    <file id=""3"" name=""./a.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" />
    <file id=""4"" name=""b.cs"" language=""3f5162f8-07c6-11d3-9053-00c04fa302a1"" languageVendor=""994b45c4-e6e9-11d2-903f-00c04fa302a1"" documentType=""5a869d0b-6611-11d3-bd2a-0000f80849bd"" />
  </files>
  <methods>
    <method containingType=""C"" name=""M"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""0"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""6"">
        <entry il_offset=""0x0"" start_row=""6"" start_column=""5"" end_row=""6"" end_column=""6"" file_ref=""1"" />
        <entry il_offset=""0x1"" start_row=""7"" start_column=""9"" end_row=""7"" end_column=""13"" file_ref=""1"" />
        <entry il_offset=""0x8"" start_row=""1"" start_column=""9"" end_row=""1"" end_column=""13"" file_ref=""2"" />
        <entry il_offset=""0xf"" start_row=""1"" start_column=""9"" end_row=""1"" end_column=""13"" file_ref=""3"" />
        <entry il_offset=""0x16"" start_row=""1"" start_column=""9"" end_row=""1"" end_column=""13"" file_ref=""4"" />
        <entry il_offset=""0x1d"" start_row=""2"" start_column=""5"" end_row=""2"" end_column=""6"" file_ref=""4"" />
      </sequencepoints>
      <locals />
    </method>
  </methods>
</symbols>";

            PDBTests.AssertXmlEqual(expected, actual);
        }
    }
}
