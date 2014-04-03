﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public class VersionHelperTests
    {
        [Fact]
        public void ParseGood()
        {
            Version version;
            Assert.True(VersionHelper.TryParseWithWildcards("1.234.56.7", out version));
            Assert.True(VersionHelper.TryParseWithWildcards("3.2.*", out version));
            Assert.Equal(3, version.Major);
            Assert.Equal(2, version.Minor);
            //number of days since jan 1, 2000
            Assert.Equal((int)(DateTime.Now - new DateTime(2000, 1, 1)).TotalDays, version.Build);
            //number of seconds since midnight divided by two
            int s = (int)DateTime.Now.TimeOfDay.TotalSeconds / 2;   
            Assert.InRange(version.Revision, s - 2, s + 2);
            Assert.True(VersionHelper.TryParseWithWildcards("1.2.3.*", out version));
            s = (int)DateTime.Now.TimeOfDay.TotalSeconds / 2;
            Assert.InRange(version.Revision, s - 2, s + 2);
        }

        [Fact]
        public void ParseGood2()
        {
            Version version;
            Assert.True(VersionHelper.TryParse("1.234.56.7", out version));
            Assert.True(VersionHelper.TryParse("3.2", out version));
            Assert.Equal(3, version.Major);
            Assert.Equal(2, version.Minor);
            Assert.True(VersionHelper.TryParse("3", out version));
            Assert.Equal(3, version.Major);
        }

        [Fact]
        public void ParseBad()
        {
            Version version;
            Assert.False(VersionHelper.TryParseWithWildcards("1.234.56.7.*", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards("*", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards("1.2. *", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards("1.2.* ", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards("1.*", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards("1.1.*.*", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards("", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards(null, out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards("a", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards("********", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards("...", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards(".a.b.", out version));
            Assert.Null(version);
            Assert.False(VersionHelper.TryParseWithWildcards(".0.1.", out version));
            Assert.Null(version);

            // U+FF11 FULLWIDTH DIGIT ONE 
            Assert.False(VersionHelper.TryParseWithWildcards("\uFF11.\uFF10.\uFF10.\uFF10", out version));
            Assert.Null(version);
        }

        [Fact]
        public void ParseBad2()
        {
            Version nil = new Version(0, 0, 0, 0);

            Version version;
            Assert.False(VersionHelper.TryParse("", out version));
            Assert.Equal(nil, version);
            Assert.False(VersionHelper.TryParse(null, out version));
            Assert.Equal(nil, version);
            Assert.False(VersionHelper.TryParse("a", out version));
            Assert.Equal(nil, version);
            Assert.False(VersionHelper.TryParse("********", out version));
            Assert.Equal(nil, version);
            Assert.False(VersionHelper.TryParse("...", out version));
            Assert.Equal(nil, version);
            Assert.False(VersionHelper.TryParse(".a.b.", out version));
            Assert.Equal(nil, version);
            Assert.False(VersionHelper.TryParse(".1.2.", out version));
            Assert.Equal(new Version(0, 0, 0, 0), version);
            Assert.False(VersionHelper.TryParse("1.234.56.7.8", out version));
            Assert.Equal(new Version(1, 234, 56, 7), version);
            Assert.False(VersionHelper.TryParse("*", out version));
            Assert.Equal(nil, version);
            Assert.False(VersionHelper.TryParse("1.2. 3", out version));
            Assert.Equal(new Version(1, 2, 0, 0), version);
            Assert.False(VersionHelper.TryParse("1.2.3 ", out version));
            Assert.Equal(new Version(1, 2, 0, 0), version);
            Assert.False(VersionHelper.TryParse("1.a", out version));
            Assert.Equal(new Version(1, 0, 0, 0), version);
            Assert.False(VersionHelper.TryParse("1.2.a.b", out version));
            Assert.Equal(new Version(1, 2, 0, 0), version);

            // U+FF11 FULLWIDTH DIGIT ONE 
            Assert.False(VersionHelper.TryParse("\uFF11.\uFF10.\uFF10.\uFF10", out version));
            Assert.Equal(new Version(0, 0, 0, 0), version);
        }
    }
}
