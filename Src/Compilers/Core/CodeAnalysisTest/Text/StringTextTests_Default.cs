﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public class StringTextTest_Default
    {
        private Encoding currentEncoding;

        protected byte[] GetBytes(Encoding encoding, string source)
        {
            currentEncoding = encoding;

            var preamble = encoding.GetPreamble();
            var content = encoding.GetBytes(source);

            byte[] bytes = new byte[preamble.Length + content.Length];
            preamble.CopyTo(bytes, 0);
            content.CopyTo(bytes, preamble.Length);

            return bytes;
        }

        protected virtual SourceText Create(string source)
        {
            return Create(source, null);
        }

        protected virtual SourceText Create(string source, Encoding encoding)
        {
            byte[] buffer = GetBytes(encoding ?? Encoding.Default, source);
            using (var stream = new MemoryStream(buffer, 0, buffer.Length, writable: false, publiclyVisible: true))
            {
                return new EncodedStringText(stream, encodingOpt: null);
            }
        }

        [Fact]
        public void Ctor1()
        {
            var data = Create("foo");
            Assert.Equal(1, data.Lines.Count);
            Assert.Equal(3, data.Lines[0].Span.Length);
        }

        /// <summary>
        /// Empty string case
        /// </summary>
        [Fact]
        public void Ctor2()
        {
            var data = Create(string.Empty);
            Assert.Equal(1, data.Lines.Count);
            Assert.Equal(0, data.Lines[0].Span.Length);
        }

        [Fact]
        public void Indexer1()
        {
            var data = Create(String.Empty);
            Assert.Throws(
                typeof(IndexOutOfRangeException),
                () => { var value = data[-1]; });
        }

        [Fact]
        public void NewLines1()
        {
            var data = Create("foo" + Environment.NewLine + " bar");
            Assert.Equal(2, data.Lines.Count);
            Assert.Equal(3, data.Lines[0].Span.Length);
            Assert.Equal(5, data.Lines[1].Span.Start);
        }

        [Fact]
        public void NewLines2()
        {
            var text =
@"foo
bar
baz";
            var data = Create(text);
            Assert.Equal(3, data.Lines.Count);
            Assert.Equal("foo", data.ToString(data.Lines[0].Span));
            Assert.Equal("bar", data.ToString(data.Lines[1].Span));
            Assert.Equal("baz", data.ToString(data.Lines[2].Span));
        }

        [Fact]
        public void NewLines3()
        {
            var data = Create("foo\r\nbar");
            Assert.Equal(2, data.Lines.Count);
            Assert.Equal("foo", data.ToString(data.Lines[0].Span));
            Assert.Equal("bar", data.ToString(data.Lines[1].Span));
        }

        [Fact]
        public void NewLines4()
        {
            var data = Create("foo\n\rbar");
            Assert.Equal(3, data.Lines.Count);
        }

        [Fact]
        public void LinesGetText1()
        {
            var data = Create(
@"foo
bar baz");
            Assert.Equal(2, data.Lines.Count);
            Assert.Equal("foo", data.Lines[0].ToString());
            Assert.Equal("bar baz", data.Lines[1].ToString());
        }

        [Fact]
        public void LinesGetText2()
        {
            var data = Create("foo");
            Assert.Equal("foo", data.Lines[0].ToString());
        }

#if false
        [Fact]
        public void TextLine1()
        {
            var text = Create("foo" + Environment.NewLine);
            var span = new TextSpan(0, 3);
            var line = new TextLine(text, 0, 0, text.Length);
            Assert.Equal(span, line.Extent);
            Assert.Equal(5, line.EndIncludingLineBreak);
            Assert.Equal(0, line.LineNumber);
        }

        [Fact]
        public void GetText1()
        {
            var text = Create("foo");
            var line = new TextLine(text, 0, 0, 2);
            Assert.Equal("fo", line.ToString());
            Assert.Equal(0, line.LineNumber);
        }

        [Fact]
        public void GetText2()
        {
            var text = Create("abcdef");
            var line = new TextLine(text, 0, 1, 2);
            Assert.Equal("bc", line.ToString());
            Assert.Equal(0, line.LineNumber);
        }
#endif

        [Fact]
        public void GetTextDiacritic()
        {
            var text = Create("Å", Encoding.GetEncoding(1252));
            Assert.Equal("Å", text.ToString());
        }
    }
}
