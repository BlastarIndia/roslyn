﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.PDB
{
    public class PDBConstantTests : CSharpTestBase
    {
        [Fact]
        public void TestSimpleLocalConstants()
        {
            var text = @"
class C
{
    void M()
    {
        const int x = 1;
        {
            const int y = 2;
        }
    }
}
";
            string actual = GetPdbXml(text, TestOptions.Dll, "C.M");
            string expected = @"
<symbols>
  <methods>
    <method containingType=""C"" name=""M"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""0"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""4"">
        <entry il_offset=""0x0"" start_row=""5"" start_column=""5"" end_row=""5"" end_column=""6"" file_ref=""0"" />
        <entry il_offset=""0x1"" start_row=""7"" start_column=""9"" end_row=""7"" end_column=""10"" file_ref=""0"" />
        <entry il_offset=""0x2"" start_row=""9"" start_column=""9"" end_row=""9"" end_column=""10"" file_ref=""0"" />
        <entry il_offset=""0x3"" start_row=""10"" start_column=""5"" end_row=""10"" end_column=""6"" file_ref=""0"" />
      </sequencepoints>
      <locals>
        <constant name=""x"" value=""1"" type=""Int32"" />
        <constant name=""y"" value=""2"" type=""Int32"" />
      </locals>
      <scope startOffset=""0x0"" endOffset=""0x4"">
        <constant name=""x"" value=""1"" type=""Int32"" />
        <scope startOffset=""0x1"" endOffset=""0x3"">
          <constant name=""y"" value=""2"" type=""Int32"" />
        </scope>
      </scope>
    </method>
  </methods>
</symbols>";
            AssertXmlEqual(expected, actual);
        }

        [Fact]
        public void TestLambdaLocalConstants()
        {
            var text = @"
using System;

class C
{
    void M(Action a)
    {
        const int x = 1;
        M(() =>
        {
            const int y = 2;
            {
                const int z = 3;
            }
        });
    }
}
";
            string actual = GetPdbXml(text, TestOptions.Dll);
            string expected = @"
<symbols>
  <methods>
    <method containingType=""C"" name=""M"" parameterNames=""a"">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""1"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""3"">
        <entry il_offset=""0x0"" start_row=""7"" start_column=""5"" end_row=""7"" end_column=""6"" file_ref=""0"" />
        <entry il_offset=""0x1"" start_row=""9"" start_column=""9"" end_row=""15"" end_column=""12"" file_ref=""0"" />
        <entry il_offset=""0x23"" start_row=""16"" start_column=""5"" end_row=""16"" end_column=""6"" file_ref=""0"" />
      </sequencepoints>
      <locals>
        <constant name=""x"" value=""1"" type=""Int32"" />
      </locals>
      <scope startOffset=""0x0"" endOffset=""0x24"">
        <namespace name=""System"" />
        <constant name=""x"" value=""1"" type=""Int32"" />
      </scope>
    </method>
    <method containingType=""C"" name=""&lt;M&gt;b__0"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <forward version=""4"" kind=""ForwardInfo"" size=""12"" declaringType=""C"" methodName=""M"" parameterNames=""a"" />
      </customDebugInfo>
      <sequencepoints total=""4"">
        <entry il_offset=""0x0"" start_row=""10"" start_column=""9"" end_row=""10"" end_column=""10"" file_ref=""0"" />
        <entry il_offset=""0x1"" start_row=""12"" start_column=""13"" end_row=""12"" end_column=""14"" file_ref=""0"" />
        <entry il_offset=""0x2"" start_row=""14"" start_column=""13"" end_row=""14"" end_column=""14"" file_ref=""0"" />
        <entry il_offset=""0x5"" start_row=""15"" start_column=""9"" end_row=""15"" end_column=""10"" file_ref=""0"" />
      </sequencepoints>
      <locals>
        <constant name=""y"" value=""2"" type=""Int32"" />
        <constant name=""z"" value=""3"" type=""Int32"" />
      </locals>
      <scope startOffset=""0x0"" endOffset=""0x6"">
        <constant name=""y"" value=""2"" type=""Int32"" />
        <scope startOffset=""0x1"" endOffset=""0x3"">
          <constant name=""z"" value=""3"" type=""Int32"" />
        </scope>
      </scope>
    </method>
  </methods>
</symbols>";
            AssertXmlEqual(expected, actual);
        }

        [WorkItem(543342)]
        [Fact]
        public void TestIteratorLocalConstants()
        {
            var text = @"
using System.Collections.Generic;

class C
{
    IEnumerable<int> M()
    {
        const int x = 1;
        for (int i = 0; i < 10; i++)
        {
            const int y = 2;
            yield return x + y + i;
        }
    }
}
";
            // NOTE: Roslyn's output is somewhat different than Dev10's in this case, but
            // all of the changes look reasonable.  The main thing for this test is that 
            // Dev10 creates fields for the locals in the iterator class.  Roslyn doesn't
            // do that - the <constant> in the <scope> is sufficient.
            string actual = GetPdbXml(text, TestOptions.Dll);
            string expected = @"
<symbols>
  <methods>
    <method containingType=""C+&lt;M&gt;d__0"" name=""MoveNext"" parameterNames="""">
      <customDebugInfo version=""4"" count=""2"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""1"" />
        </using>
        <iteratorLocals version=""4"" kind=""IteratorLocals"" size=""20"" bucketCount=""1"">
          <bucket startOffset=""0x22"" endOffset=""0x6B"" />
        </iteratorLocals>
      </customDebugInfo>
      <sequencepoints total=""11"">
        <entry il_offset=""0x0"" hidden=""true"" start_row=""16707566"" start_column=""0"" end_row=""16707566"" end_column=""0"" file_ref=""0"" />
        <entry il_offset=""0x21"" start_row=""7"" start_column=""5"" end_row=""7"" end_column=""6"" file_ref=""0"" />
        <entry il_offset=""0x22"" start_row=""9"" start_column=""14"" end_row=""9"" end_column=""23"" file_ref=""0"" />
        <entry il_offset=""0x29"" hidden=""true"" start_row=""16707566"" start_column=""0"" end_row=""16707566"" end_column=""0"" file_ref=""0"" />
        <entry il_offset=""0x2b"" start_row=""10"" start_column=""9"" end_row=""10"" end_column=""10"" file_ref=""0"" />
        <entry il_offset=""0x2c"" start_row=""12"" start_column=""13"" end_row=""12"" end_column=""36"" file_ref=""0"" />
        <entry il_offset=""0x45"" hidden=""true"" start_row=""16707566"" start_column=""0"" end_row=""16707566"" end_column=""0"" file_ref=""0"" />
        <entry il_offset=""0x4c"" start_row=""13"" start_column=""9"" end_row=""13"" end_column=""10"" file_ref=""0"" />
        <entry il_offset=""0x4d"" start_row=""9"" start_column=""33"" end_row=""9"" end_column=""36"" file_ref=""0"" />
        <entry il_offset=""0x5d"" start_row=""9"" start_column=""25"" end_row=""9"" end_column=""31"" file_ref=""0"" />
        <entry il_offset=""0x6b"" start_row=""14"" start_column=""5"" end_row=""14"" end_column=""6"" file_ref=""0"" />
      </sequencepoints>
      <locals>
        <local name=""cachedState"" il_index=""0"" il_start=""0x0"" il_end=""0x7"" attributes=""0"" />
        <constant name=""x"" value=""1"" type=""Int32"" />
        <constant name=""y"" value=""2"" type=""Int32"" />
      </locals>
      <scope startOffset=""0x0"" endOffset=""0x6f"">
        <namespace name=""System.Collections.Generic"" />
        <scope startOffset=""0x0"" endOffset=""0x7"">
          <local name=""cachedState"" il_index=""0"" il_start=""0x0"" il_end=""0x7"" attributes=""0"" />
        </scope>
        <scope startOffset=""0x21"" endOffset=""0x6f"">
          <constant name=""x"" value=""1"" type=""Int32"" />
          <scope startOffset=""0x2b"" endOffset=""0x4d"">
            <constant name=""y"" value=""2"" type=""Int32"" />
          </scope>
        </scope>
      </scope>
    </method>
  </methods>
</symbols>";
            AssertXmlEqual(expected, actual);
        }

        [Fact]
        public void TestLocalConstantsTypes()
        {
            var text = @"
class C
{
    void M()
    {
        const object o = null;
        const string s = ""hello"";
        const float f = float.MinValue;
        const double d = double.MaxValue;
    }
}
";
            string actual = GetPdbXml(text, TestOptions.Dll, "C.M");
            string expected = @"
<symbols>
  <methods>
    <method containingType=""C"" name=""M"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""0"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""2"">
        <entry il_offset=""0x0"" start_row=""5"" start_column=""5"" end_row=""5"" end_column=""6"" file_ref=""0"" />
        <entry il_offset=""0x1"" start_row=""10"" start_column=""5"" end_row=""10"" end_column=""6"" file_ref=""0"" />
      </sequencepoints>
      <locals>
        <constant name=""o"" value=""0"" type=""Int32"" />
        <constant name=""s"" value=""hello"" type=""String"" />
        <constant name=""f"" value=""-3.402823E+38"" type=""Single"" />
        <constant name=""d"" value=""1.79769313486232E+308"" type=""Double"" />
      </locals>
      <scope startOffset=""0x0"" endOffset=""0x2"">
        <constant name=""o"" value=""0"" type=""Int32"" />
        <constant name=""s"" value=""hello"" type=""String"" />
        <constant name=""f"" value=""-3.402823E+38"" type=""Single"" />
        <constant name=""d"" value=""1.79769313486232E+308"" type=""Double"" />
      </scope>
    </method>
  </methods>
</symbols>";
            AssertXmlEqual(expected, actual);
        }

        [Fact]
        public void StringConstantTooLong()
        {
            var text = @"
class C
{
    void M()
    {
        const string text = @""
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB
this is a string constant that is too long to fit into the PDB"";
    }
}
";
            string actual = GetPdbXml(text, TestOptions.Dll, "C.M");
            string expected = @"
<symbols>
  <methods>
    <method containingType=""C"" name=""M"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""0"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""2"">
        <entry il_offset=""0x0"" start_row=""5"" start_column=""5"" end_row=""5"" end_column=""6"" file_ref=""0"" />
        <entry il_offset=""0x1"" start_row=""43"" start_column=""5"" end_row=""43"" end_column=""6"" file_ref=""0"" />
      </sequencepoints>
      <locals />
    </method>
  </methods>
</symbols>";
            AssertXmlEqual(expected, actual);
        }

        [Fact]
        public void TestDecimalLocalConstants()
        {
            var text = @"
class C
{
    void M()
    {
        const decimal d = (decimal)1.5;
    }
}
";
            string actual = GetPdbXml(text, TestOptions.Dll, "C.M");
            string expected = @"
<symbols>
  <methods>
    <method containingType=""C"" name=""M"" parameterNames="""">
      <customDebugInfo version=""4"" count=""1"">
        <using version=""4"" kind=""UsingInfo"" size=""12"" namespaceCount=""1"">
          <namespace usingCount=""0"" />
        </using>
      </customDebugInfo>
      <sequencepoints total=""2"">
        <entry il_offset=""0x0"" start_row=""5"" start_column=""5"" end_row=""5"" end_column=""6"" file_ref=""0"" />
        <entry il_offset=""0x1"" start_row=""7"" start_column=""5"" end_row=""7"" end_column=""6"" file_ref=""0"" />
      </sequencepoints>
      <locals>
        <constant name=""d"" value=""1.5"" type=""Decimal"" />
      </locals>
      <scope startOffset=""0x0"" endOffset=""0x2"">
        <constant name=""d"" value=""1.5"" type=""Decimal"" />
      </scope>
    </method>
  </methods>
</symbols>";
            AssertXmlEqual(expected, actual);
        }
    }
}