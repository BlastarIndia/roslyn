﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Collections;

namespace Roslyn.Utilities.Pdb
{
    /// <summary>
    /// A collection of utility method for consuming custom debug info from a PDB.
    /// </summary>
    /// <remarks>
    /// This is not a public API, so we're just going to let bad offsets fail on their own.
    /// </remarks>
    internal static class CustomDebugInfoReader
    {
        // The version number of the custom debug info binary format.
        internal const int CdiVersion = 4; //CDIVERSION in Dev10

        // The name of the attribute containing the byte array of custom debug info.
        private const string CdiAttributeName = "MD2"; //MSCUSTOMDEBUGINFO in Dev10

        // The number of bytes at the beginning of the byte array that contain global header information.
        private const int CdiGlobalHeaderSize = 4; //start after header (version byte + size byte + dword padding)

        // The number of bytes at the beginning of each custom debug info record that contain header information
        // common to all record types (i.e. byte, kind, size).
        private const int CdiRecordHeaderSize = 8; // version byte + kind byte + two bytes padding + size dword

        /// <summary>
        /// Get the blob of binary custom debug info for a given method.
        /// </summary>
        public static byte[] GetCustomDebugInfo(this ISymUnmanagedReader symReader, int methodToken)
        {
            int bytesAvailable;
            symReader.GetSymAttribute(methodToken, CdiAttributeName, 0, out bytesAvailable, buffer: null);

            if (bytesAvailable <= 0)
            {
                return null;
            }

            var buffer = new byte[bytesAvailable];
            int bytesRead;
            symReader.GetSymAttribute(methodToken, CdiAttributeName, bytesAvailable, out bytesRead, buffer);

            if (bytesAvailable != bytesRead)
            {
                return null;
            }

            return buffer;
        }

        /// <summary>
        /// This is the first thing in the blob returned by <see cref="GetCustomDebugInfo"/>.
        /// </summary>
        public static void ReadGlobalHeader(byte[] bytes, ref int offset, out byte version, out byte count)
        {
            version = bytes[offset + 0];
            count = bytes[offset + 1];
            offset += CdiGlobalHeaderSize;
        }

        /// <summary>
        /// After the global header (see <see cref="ReadGlobalHeader"/> comes  list of custom debug info record.
        /// Each record begins with a standard header.
        /// </summary>
        public static void ReadRecordHeader(byte[] bytes, ref int offset, out byte version, out CustomDebugInfoKind kind, out int size)
        {
            version = bytes[offset + 0];
            kind = (CustomDebugInfoKind)bytes[offset + 1];
            size = BitConverter.ToInt32(bytes, offset + 4); //note: two bytes of padding after kind
            offset += CdiRecordHeaderSize;
        }

        /// <summary>
        /// For each namespace declaration enclosing a method (innermost-to-outermost), there is a count
        /// of the number of imports in that declaration.
        /// </summary>
        /// <remarks>
        /// There's always at least one entry (for the global namespace).
        /// </remarks>
        public static void ReadUsingRecord(byte[] bytes, ref int offset, int size, out ImmutableArray<short> counts)
        {
            int tempOffset = offset;

            var numCounts = BitConverter.ToInt16(bytes, tempOffset);
            tempOffset += 2;

            var builder = ArrayBuilder<short>.GetInstance(numCounts);
            for (int i = 0; i < numCounts; i++)
            {
                short count = BitConverter.ToInt16(bytes, tempOffset);
                tempOffset += 2;

                builder.Add(count);
            }
            counts = builder.ToImmutableAndFree();

            offset += size - CdiRecordHeaderSize;
            Debug.Assert(offset >= tempOffset);
        }

        /// <summary>
        /// This indicates that further information can be obtained by looking at the custom debug
        /// info of another method (specified by token).
        /// </summary>
        /// <remarks>
        /// Appears when multiple method would otherwise have identical using records (see <see cref="ReadUsingRecord"/>).
        /// </remarks>
        public static void ReadForwardRecord(byte[] bytes, ref int offset, int size, out int token)
        {
            int tempOffset = offset;

            token = BitConverter.ToInt32(bytes, tempOffset);

            offset += size - CdiRecordHeaderSize;
            Debug.Assert(offset >= tempOffset);
        }

        /// <summary>
        /// This indicates that further information can be obtained by looking at the custom debug
        /// info of another method (specified by token).
        /// </summary>
        /// <remarks>
        /// Appears when there are extern aliases and edit-and-continue is disabled.
        /// </remarks>
        public static void ReadForwardToModuleRecord(byte[] bytes, ref int offset, int size, out int token)
        {
            int tempOffset = offset;

            token = BitConverter.ToInt32(bytes, tempOffset);

            offset += size - CdiRecordHeaderSize;
            Debug.Assert(offset >= tempOffset);
        }

        /// <summary>
        /// Appears when iterator locals have to lifted into fields.  Contains a list of buckets with
        /// start and end offsets (presumably, into IL).
        /// </summary>
        /// <remarks>
        /// Appears when there are locals in iterator methods.
        /// </remarks>
        public static void ReadIteratorLocalsRecord(byte[] bytes, ref int offset, int size, out ImmutableArray<IteratorLocalBucket> buckets)
        {
            int tempOffset = offset;

            var bucketCount = BitConverter.ToInt32(bytes, tempOffset);
            tempOffset += 4;

            var builder = ArrayBuilder<IteratorLocalBucket>.GetInstance(bucketCount);
            for (int i = 0; i < bucketCount; i++)
            {
                int startOffset = BitConverter.ToInt32(bytes, tempOffset);
                tempOffset += 4;
                int endOffset = BitConverter.ToInt32(bytes, tempOffset);
                tempOffset += 4;

                builder.Add(new IteratorLocalBucket(startOffset, endOffset));
            }
            buckets = builder.ToImmutableAndFree();

            offset += size - CdiRecordHeaderSize;
            Debug.Assert(offset >= tempOffset);
        }

        /// <summary>
        /// Indicates that this method is the iterator state machine for the method named in the record.
        /// </summary>
        /// <remarks>
        /// Appears when are iterator methods.
        /// </remarks>
        public static void ReadForwardIteratorRecord(byte[] bytes, ref int offset, int size, out string name)
        {
            int tempOffset = offset;

            var pooled = PooledStringBuilder.GetInstance();
            var builder = pooled.Builder;
            while (true)
            {
                char ch = BitConverter.ToChar(bytes, tempOffset);
                tempOffset += 2;

                if (ch == 0)
                {
                    break;
                }

                builder.Append(ch);
            }
            name = pooled.ToStringAndFree();

            offset += size - CdiRecordHeaderSize;
            Debug.Assert(offset >= tempOffset);
        }

        /// <summary>
        /// Does for locals what <see cref="System.Runtime.CompilerServices.DynamicAttribute"/> does for parameters, return types, and fields.
        /// In particular, indicates which occurences of <see cref="object"/> in the signature are really dynamic.
        /// </summary>
        /// <remarks>
        /// Appears when there are dynamic locals.
        /// </remarks>
        public static void ReadDynamicLocalsRecord(byte[] bytes, ref int offset, int size, out ImmutableArray<DynamicLocalBucket> buckets)
        {
            int tempOffset = offset;

            int bucketCount = BitConverter.ToInt32(bytes, tempOffset);
            tempOffset += 4;

            var builder = ArrayBuilder<DynamicLocalBucket>.GetInstance(bucketCount);
            for (int i = 0; i < bucketCount; i++)
            {
                const int numFlagBytes = 64;
                var flags = 0UL;
                for (int j = 0; j < numFlagBytes; j++)
                {
                    var flag = bytes[tempOffset + j] != 0;
                    if (flag)
                    {
                        flags |= 1UL << j;
                    }
                }
                tempOffset += numFlagBytes;

                int flagCount = BitConverter.ToInt32(bytes, tempOffset);
                tempOffset += 4;

                int slotId = BitConverter.ToInt32(bytes, tempOffset);
                tempOffset += 4;

                const int numNameBytes = 128;
                var pooled = PooledStringBuilder.GetInstance();
                var nameBuilder = pooled.Builder;
                for (int j = 0; j < numNameBytes; j += 2)
                {
                    char ch = BitConverter.ToChar(bytes, tempOffset + j);
                    if (ch == 0) break;
                    nameBuilder.Append(ch);
                }
                tempOffset += numNameBytes; //The Identifier name takes 64 WCHAR no matter how big its actual length is.
                var name = pooled.ToStringAndFree();

                var bucket = new DynamicLocalBucket(flagCount, flags, slotId, name);
                builder.Add(bucket);
            }
            buckets = builder.ToImmutableAndFree();

            offset += size - CdiRecordHeaderSize;
            Debug.Assert(offset >= tempOffset);
        }

        /// <summary>
        /// Returns the raw bytes of a record.
        /// </summary>
        public static void ReadUnknownRecord(byte[] bytes, ref int offset, int size, out ImmutableArray<byte> body)
        {
            var bodySize = size - CdiRecordHeaderSize;
            var builder = ArrayBuilder<byte>.GetInstance(bodySize);
            var end = offset + bodySize;
            for (; offset < end; offset++)
            {
                builder.Add(bytes[offset]);
            }
            body = builder.ToImmutableAndFree();
        }

        /// <summary>
        /// Skips past a record.
        /// </summary>
        public static void SkipRecord(byte[] bytes, ref int offset, int size)
        {
            offset += size - CdiRecordHeaderSize;
        }

        /// <summary>
        /// Get the import strings for a given method, following forward pointers as necessary.
        /// </summary>
        /// <returns>
        /// For each namespace enclosing the method, a list of import strings, innermost to outermost.
        /// There should always be at least one entry, for the global namespace.
        /// </returns>
        public static ImmutableArray<ImmutableArray<string>> GetCSharpGroupedImportStrings(this ISymUnmanagedReader reader, int methodToken, out ImmutableArray<string> externAliasStrings)
        {
            externAliasStrings = default(ImmutableArray<string>);

            ImmutableArray<short> groupSizes = default(ImmutableArray<short>);
            bool seenForward = false;

            RETRY:
            var bytes = reader.GetCustomDebugInfo(methodToken);
            if (bytes == null)
            {
                return default(ImmutableArray<ImmutableArray<string>>);
            }

            int offset = 0;

            byte globalVersion;
            byte unusedGlobalCount;
            ReadGlobalHeader(bytes, ref offset, out globalVersion, out unusedGlobalCount);
            CheckVersion(globalVersion, methodToken);

            while (offset < bytes.Length)
            {
                byte version;
                CustomDebugInfoKind kind;
                int size;
                ReadRecordHeader(bytes, ref offset, out version, out kind, out size);
                CheckVersion(version, methodToken);

                if (kind == CustomDebugInfoKind.UsingInfo)
                {
                    if (!groupSizes.IsDefault)
                    {
                        throw new InvalidOperationException(string.Format("Expected at most one Using record for method {0}", FormatMethodToken(methodToken)));
                    }

                    ReadUsingRecord(bytes, ref offset, size, out groupSizes);
                }
                else if (kind == CustomDebugInfoKind.ForwardInfo)
                {
                    if (!externAliasStrings.IsDefault)
                    {
                        throw new InvalidOperationException(string.Format("Did not expect both Forward and ForwardToModule records for method {0}", FormatMethodToken(methodToken)));
                    }

                    ReadForwardRecord(bytes, ref offset, size, out methodToken);
                    if (!seenForward) // Follow at most one forward link.
                    {
                        seenForward = true;
                        goto RETRY;
                    }
                }
                else if (kind == CustomDebugInfoKind.ForwardToModuleInfo)
                {
                    if (!externAliasStrings.IsDefault)
                    {
                        throw new InvalidOperationException(string.Format("Expected at most one ForwardToModule record for method {0}", FormatMethodToken(methodToken)));
                    }

                    int moduleInfoMethodToken;
                    ReadForwardToModuleRecord(bytes, ref offset, size, out moduleInfoMethodToken);
                    ImmutableArray<string> allModuleInfoImportStrings = GetImportStrings(reader.GetBaselineMethod(moduleInfoMethodToken));
                    ArrayBuilder<string> externAliasBuilder = ArrayBuilder<string>.GetInstance();
                    foreach(string importString in allModuleInfoImportStrings)
                    {
                        if (IsCSharpExternAliasInfo(importString))
                        {
                            externAliasBuilder.Add(importString);
                        }
                    }
                    externAliasStrings = externAliasBuilder.ToImmutableAndFree();
                }
                else
                {
                    SkipRecord(bytes, ref offset, size);
                }
            }

            if (groupSizes.IsDefault)
            {
                throw new InvalidOperationException(string.Format("Didn't find usings info for method {0}", FormatMethodToken(methodToken)));
            }

            ImmutableArray<string> importStrings = GetImportStrings(reader.GetBaselineMethod(methodToken));
            int numImportStrings = importStrings.Length;

            ArrayBuilder<ImmutableArray<string>> resultBuilder = ArrayBuilder<ImmutableArray<string>>.GetInstance(groupSizes.Length);
            ArrayBuilder<string> groupBuilder = ArrayBuilder<string>.GetInstance();

            int pos = 0;

            foreach (short groupSize in groupSizes)
            {
                for (int i = 0; i < groupSize; i++, pos++)
                {
                    if (pos >= numImportStrings)
                    {
                        throw new InvalidOperationException(string.Format("Group size indicates more imports than there are import strings (method {0}).", FormatMethodToken(methodToken)));
                    }

                    string importString = importStrings[pos];
                    if (IsCSharpExternAliasInfo(importString))
                    {
                        throw new InvalidOperationException(string.Format("Encountered extern alias info before all import strings were consumed (method {0}).", FormatMethodToken(methodToken)));
                    }

                    groupBuilder.Add(importString);
                }

                resultBuilder.Add(groupBuilder.ToImmutable());
                groupBuilder.Clear();
            }


            if (externAliasStrings.IsDefault)
            {
                Debug.Assert(groupBuilder.Count == 0);

                // Extern alias detail strings (prefix "Z") are not included in the group counts.
                for (; pos < numImportStrings; pos++)
                {
                    string importString = importStrings[pos];
                    if (!IsCSharpExternAliasInfo(importString))
                    {
                        throw new InvalidOperationException(string.Format("Expected only extern alias info strings after consuming the indicated number of imports (method {0}).", FormatMethodToken(methodToken)));
                    }

                    groupBuilder.Add(importString);
                }

                externAliasStrings = groupBuilder.ToImmutableAndFree();
            }
            else
            {
                groupBuilder.Free();

                if (pos < numImportStrings)
                {
                    throw new InvalidOperationException(string.Format("Group size indicates fewer imports than there are import strings (method {0}).", FormatMethodToken(methodToken)));
                }
            }

            return resultBuilder.ToImmutableAndFree();
        }

        private static void CheckVersion(byte globalVersion, int methodToken)
        {
            if (globalVersion != CdiVersion)
            {
                throw new InvalidOperationException(string.Format("Method {0}: Expected version {1}, but found version {2}.", FormatMethodToken(methodToken), CdiVersion, globalVersion));
            }
        }

        /// <summary>
        /// Get the (unprocessed) import strings for a given method.
        /// </summary>
        /// <remarks>
        /// Doesn't consider forwarding.
        /// </remarks>
        private static ImmutableArray<string> GetImportStrings(this ISymUnmanagedMethod method)
        {

            ISymUnmanagedScope rootScope = method.GetRootScope();
            if (rootScope == null)
            {
                Debug.Assert(false, "Expected a root scope.");
                return ImmutableArray<string>.Empty;
            }

            ImmutableArray<ISymUnmanagedScope> childScopes = rootScope.GetScopes();
            if (childScopes.Length == 0)
            {
                //Debug.Assert(false, "Expected at least one child scope."); // TODO (acasey): Why can't we assume this?
                return ImmutableArray<string>.Empty;
            }

            // As in NamespaceListWrapper::Init, we only consider namespaces in the first
            // child of the root scope.
            ISymUnmanagedScope firstChildScope = childScopes[0];

            ImmutableArray<ISymUnmanagedNamespace> namespaces = firstChildScope.GetNamespaces();
            if (namespaces.Length == 0)
            {
                //Debug.Assert(false, "Expected at least one namespace (i.e. the global namespace)."); // TODO (acasey): Why can't we assume this?
                return ImmutableArray<string>.Empty;
            }

            ArrayBuilder<string> importsBuilder = ArrayBuilder<string>.GetInstance(namespaces.Length);
            foreach (ISymUnmanagedNamespace @namespace in namespaces)
            {
                importsBuilder.Add(@namespace.GetName());
            }
            return importsBuilder.ToImmutableAndFree();
        }

        public static bool IsCSharpExternAliasInfo(string import)
        {
            return import.Length > 0 && import[0] == 'Z';
        }

        /// <summary>
        /// Parse a string representing a C# using (or extern alias) directive.
        /// </summary>
        /// <remarks>
        /// <![CDATA[
        /// For C#:
        ///  "USystem" -> <namespace name="System" />
        ///  "AS USystem" -> <alias name="S" target="System" kind="namespace" />
        ///  "AC TSystem.Console" -> <alias name="C" target="System.Console" kind="type" />
        ///  "AS ESystem alias" -> <alias name="S" qualifier="alias" target="System" kind="type" />
        ///  "XOldLib" -> <extern alias="OldLib" />
        ///  "ZOldLib assembly" -> <externinfo name="OldLib" assembly="assembly" />
        ///  "ESystem alias" -> <namespace qualifier="alias" name="System" />
        /// ]]>
        /// </remarks>
        public static void ParseCSharpImportString(string import, out string alias, out string externAlias, out string target, out ImportTargetKind kind)
        {
            if (import == null)
            {
                throw new ArgumentNullException("import");
            }

            if (import.Length == 0)
            {
                throw new ArgumentException(import, "import");
            }

            switch (import[0])
            {
                case 'U': // C# using
                    alias = null;
                    externAlias = null;
                    target = import.Substring(1);
                    kind = ImportTargetKind.Namespace;
                    return;
                case 'E': // C# using
                    // NOTE: Dev12 has related cases "I" and "O" in EMITTER::ComputeDebugNamespace,
                    // but they were probably implementation details that do not affect roslyn.
                    if (!TrySplit(import, 1, ' ', out target, out externAlias))
                    {
                        throw new ArgumentException(import, "import");
                    }

                    alias = null;
                    kind = ImportTargetKind.Namespace;
                    return;
                case 'A': // C# type or namespace alias
                    if (!TrySplit(import, 1, ' ', out alias, out target))
                    {
                        throw new ArgumentException(import, "import");
                    }

                    switch (target[0])
                    {
                        case 'U':
                            kind = ImportTargetKind.Namespace;
                            target = target.Substring(1);
                            externAlias = null;
                            return;
                        case 'T':
                            kind = ImportTargetKind.Type;
                            target = target.Substring(1);
                            externAlias = null;
                            return;
                        case 'E':
                            kind = ImportTargetKind.Namespace; // Never happens for types.
                            if (!TrySplit(target, 1, ' ', out target, out externAlias))
                            {
                                throw new ArgumentException(import, "import");
                            }
                            return;
                        default:
                            throw new ArgumentException(import, "import");
                    }
                case 'X': // C# extern alias (in file)
                    externAlias = import.Substring(1);
                    alias = null;
                    target = null;
                    kind = ImportTargetKind.Assembly;
                    return;
                case 'Z': // C# extern alias (module-level)
                    if (!TrySplit(import, 1, ' ', out externAlias, out target))
                    {
                        throw new ArgumentException(import, "import");
                    }

                    alias = null;
                    kind = ImportTargetKind.Assembly;
                    return;
                default:
                    throw new ArgumentException(import, "import");
            }
        }

        /// <summary>
        /// Parse a string representing a VB import statement.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="import"/> is null.</exception>
        /// <exception cref="ArgumentException">Format of <paramref name="import"/> is not valid.</exception>
        public static void ParseVisualBasicImportString(string import, out string alias, out string target, out ImportTargetKind kind, out ImportScope scope)
        {
            if (import == null)
            {
                throw new ArgumentNullException("import");
            }

            if (import.Length == 0) // VB current namespace
            {
                alias = null;
                target = import;
                kind = ImportTargetKind.CurrentNamespace;
                scope = ImportScope.Unspecified;
                return;
            }

            int pos = 0;
            switch (import[pos])
            {
                case '*': // VB default namespace
                    // see PEBuilder.cpp in vb\language\CodeGen
                    pos++;
                    alias = null;
                    target = import.Substring(pos);
                    kind = ImportTargetKind.DefaultNamespace;
                    scope = ImportScope.Unspecified;
                    return;
                case '@': // VB cases other than default and current namespace
                    // see PEBuilder.cpp in vb\language\CodeGen
                    pos++;
                    if (pos >= import.Length)
                    {
                        throw new ArgumentException(import, "import");
                    }

                    scope = ImportScope.Unspecified;
                    switch (import[pos])
                    {
                        case 'F':
                            scope = ImportScope.File;
                            pos++;
                            break;
                        case 'P':
                            scope = ImportScope.Project;
                            pos++;
                            break;
                    }

                    if (pos >= import.Length)
                    {
                        throw new ArgumentException(import, "import");
                    }

                    switch (import[pos])
                    {
                        case 'A':
                            pos++;
                            if (import[pos] != ':')
                            {
                                throw new ArgumentException(import, "import");
                            }
                            pos++;

                            if (!TrySplit(import, pos, '=', out alias, out target))
                            {
                                throw new ArgumentException(import, "import");
                            }

                            kind = ImportTargetKind.NamespaceOrType;
                            return;
                        case 'X':
                            pos++;
                            if (import[pos] != ':')
                            {
                                throw new ArgumentException(import, "import");
                            }
                            pos++;

                            if (!TrySplit(import, pos, '=', out alias, out target))
                            {
                                throw new ArgumentException(import, "import");
                            }

                            kind = ImportTargetKind.XmlNamespace;
                            return;
                        case 'T':
                            pos++;
                            if (import[pos] != ':')
                            {
                                throw new ArgumentException(import, "import");
                            }
                            pos++;

                            alias = null;
                            target = import.Substring(pos);
                            kind = ImportTargetKind.Type;
                            return;
                        case ':':
                            pos++;
                            alias = null;
                            target = import.Substring(pos);
                            kind = ImportTargetKind.Namespace;
                            return;
                        default:
                            alias = null;
                            target = import.Substring(pos);
                            kind = ImportTargetKind.MethodToken;
                            return;
                    }
                default: // VB current namespace
                    alias = null;
                    target = import;
                    kind = ImportTargetKind.CurrentNamespace;
                    scope = ImportScope.Unspecified;
                    return;
            }
        }

        private static bool TrySplit(string input, int offset, char separator, out string before, out string after)
        {
            int separatorPos = input.IndexOf(separator, offset);
            // Allow zero-length before for the global namespace (empty string).
            // Allow zero-length after for an XML alias in VB ("@PX:=").  Not sure what it means.
            if (offset <= separatorPos && separatorPos < input.Length)
            {
                before = input.Substring(offset, separatorPos - offset);
                after = separatorPos + 1 == input.Length
                    ? ""
                    : input.Substring(separatorPos + 1);
                return true;
            }

            before = null;
            after = null;
            return false;
        }

        private static string FormatMethodToken(int methodToken)
        {
            return string.Format("0x{0:x8}", methodToken);
        }
    }

    internal enum ImportTargetKind
    {
        /// <summary>C# or VB namespace import.</summary>
        Namespace,
        /// <summary>C# or VB type import.</summary>
        Type,
        /// <summary>VB namespace or type alias target (not specified).</summary>
        NamespaceOrType,
        /// <summary>C# extern alias.</summary>
        Assembly,
        /// <summary>VB XML import.</summary>
        XmlNamespace,
        /// <summary>VB forwarding information (i.e. another method has the imports for this one).</summary>
        MethodToken,
        /// <summary>VB containing namespace (not an import).</summary>
        CurrentNamespace,
        /// <summary>VB root namespace (not an import).</summary>
        DefaultNamespace,
    }

    internal enum ImportScope
    {
        Unspecified,
        File,
        Project,
    }

    internal struct IteratorLocalBucket
    {
        public readonly int StartOffset;
        public readonly int EndOffset;

        public IteratorLocalBucket(int startoffset, int endOffset)
        {
            this.StartOffset = startoffset;
            this.EndOffset = endOffset;
        }
    }

    internal struct DynamicLocalBucket
    {
        public readonly int FlagCount;
        public readonly ulong Flags;
        public readonly int SlotId;
        public readonly string Name;

        public DynamicLocalBucket(int flagCount, ulong flags, int slotId, string name)
        {
            this.FlagCount = flagCount;
            this.Flags = flags;
            this.SlotId = slotId;
            this.Name = name;
        }
    }

    /// <summary>
    /// The kinds of custom debug info that we know how to interpret.
    /// The values correspond to possible values of the "kind" byte
    /// in the record header.
    /// </summary>
    internal enum CustomDebugInfoKind : byte
    {
        UsingInfo = 0,
        ForwardInfo = 1,
        ForwardToModuleInfo = 2,
        IteratorLocals = 3,
        ForwardIterator = 4,
        DynamicLocals = 5,
    }
}