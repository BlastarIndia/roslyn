﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Text;
using Microsoft.CodeAnalysis.Collections;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public class MetadataHelpersTests
    {
        [Fact]
        public void IsValidMetadataIdentifier()
        {
            string lowSurrogate = "\uDC00";
            string highSurrogate = "\uD800";
            Assert.True(Char.IsLowSurrogate(lowSurrogate, 0));
            Assert.True(Char.IsHighSurrogate(highSurrogate, 0));
            Assert.True(Char.IsSurrogatePair(highSurrogate + lowSurrogate, 0));

            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(null));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(""));
            Assert.True(MetadataHelpers.IsValidMetadataIdentifier("x"));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier("\0"));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier("x\0"));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier("\0x"));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier("abc\0xyz\0uwq"));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(lowSurrogate));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(highSurrogate));
            Assert.True(MetadataHelpers.IsValidMetadataIdentifier(highSurrogate + lowSurrogate));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(lowSurrogate + highSurrogate));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(highSurrogate + "x" + lowSurrogate));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(lowSurrogate + "x" + highSurrogate));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(highSurrogate + "xxx"));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(lowSurrogate + "xxx"));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(lowSurrogate + "\0" + highSurrogate));
            Assert.False(MetadataHelpers.IsValidMetadataIdentifier(highSurrogate + "\0" + lowSurrogate));
        }

        private enum ArrayKind
        {
            None,
            SingleDimensional,
            MultiDimensional,
            Jagged
        };

        private struct TypeNameConfig
        {
            public int nestingLevel;
            public TypeNameConfig[] genericParamsConfig;
            public ArrayKind arrayKind;
            public bool assemblyQualified;

            public TypeNameConfig(int nestingLevel, TypeNameConfig[] genericParamsConfig, ArrayKind arrayKind, bool assemblyQualified)
            {
                this.nestingLevel = nestingLevel;
                this.genericParamsConfig = genericParamsConfig;
                this.arrayKind = arrayKind;
                this.assemblyQualified = assemblyQualified;
            }
        }

        private static TypeNameConfig[] GenerateTypeNameConfigs(int typeParamStackDepth)
        {
            var builder = ArrayBuilder<TypeNameConfig>.GetInstance();

            for (int nestingLevel = 0; nestingLevel <= 2; nestingLevel++)
            {
                foreach(ArrayKind arrayKind in Enum.GetValues(typeof(ArrayKind)))
                {
                    var genericParamsConfigBuilder = ArrayBuilder<TypeNameConfig[]>.GetInstance();
                    genericParamsConfigBuilder.Add(null);
                    if (typeParamStackDepth < 2)
                    {
                        genericParamsConfigBuilder.Add(GenerateTypeNameConfigs(typeParamStackDepth + 1));
                    }

                    foreach(var genericParamsConfig in genericParamsConfigBuilder.ToImmutableAndFree())
                    {
                        builder.Add(new TypeNameConfig(nestingLevel, genericParamsConfig, arrayKind, assemblyQualified: true));
                        builder.Add(new TypeNameConfig(nestingLevel, genericParamsConfig, arrayKind, assemblyQualified: false));
                    }
                }
            }
            
            return builder.ToArrayAndFree();
        }

        private static string[] GenerateTypeNamesToDecode(TypeNameConfig[] typeNameConfigs, out MetadataHelpers.AssemblyQualifiedTypeName[] expectedDecodeNames)
        {
            var pooledStrBuilder = PooledStringBuilder.GetInstance();
            StringBuilder typeNamebuilder = pooledStrBuilder.Builder;
            
            var typeNamesToDecode = new string[typeNameConfigs.Length];
            expectedDecodeNames = new MetadataHelpers.AssemblyQualifiedTypeName[typeNameConfigs.Length];
            
            for (int index = 0; index < typeNameConfigs.Length; index++)
            {
                TypeNameConfig typeNameConfig = typeNameConfigs[index];

                string expectedTopLevelTypeName = "X";
                typeNamebuilder.Append("X");
                
                string[] expectedNestedTypes = null;
                if (typeNameConfig.nestingLevel > 0)
                {
                    expectedNestedTypes = new string[typeNameConfig.nestingLevel];
                    for (int i = 0; i < typeNameConfig.nestingLevel; i++)
                    {
                        expectedNestedTypes[i] = "Y" + i;
                        typeNamebuilder.Append("+" + expectedNestedTypes[i]);
                    }
                }

                MetadataHelpers.AssemblyQualifiedTypeName[] expectedTypeArguments;
                if (typeNameConfig.genericParamsConfig == null)
                {
                    expectedTypeArguments = null;
                }
                else
                {
                    string[] genericParamsToDecode = GenerateTypeNamesToDecode(typeNameConfig.genericParamsConfig, out expectedTypeArguments);
                    
                    var genericArityStr = "`" + genericParamsToDecode.Length.ToString();
                    typeNamebuilder.Append(genericArityStr);
                    if (typeNameConfig.nestingLevel == 0)
                    {
                        expectedTopLevelTypeName += genericArityStr;
                    }
                    else
                    {
                        expectedNestedTypes[typeNameConfig.nestingLevel - 1] += genericArityStr;
                    }

                    typeNamebuilder.Append("[");
                    
                    for (int i = 0; i < genericParamsToDecode.Length; i++)
                    {
                        if (i > 0)
                        {
                            typeNamebuilder.Append(", ");
                        }
                        
                        if (typeNameConfig.genericParamsConfig[i].assemblyQualified)
                        {
                            typeNamebuilder.Append("[");
                            typeNamebuilder.Append(genericParamsToDecode[i]);
                            typeNamebuilder.Append("]");
                        }
                        else
                        {
                            typeNamebuilder.Append(genericParamsToDecode[i]);
                        }
                    }

                    typeNamebuilder.Append("]");
                }

                int[] expectedArrayRanks = null;
                switch (typeNameConfig.arrayKind)
                {
                    case ArrayKind.SingleDimensional:
                        typeNamebuilder.Append("[]");
                        expectedArrayRanks = new [] { 1 };
                        break;

                    case ArrayKind.MultiDimensional:
                        typeNamebuilder.Append("[,]");
                        expectedArrayRanks = new[] { 2 };
                        break;

                    case ArrayKind.Jagged:
                        typeNamebuilder.Append("[,][]");
                        expectedArrayRanks = new[] { 1, 2 };
                        break;
                }

                string expectedAssemblyName;
                if (typeNameConfig.assemblyQualified)
                {
                    expectedAssemblyName = "Assembly, Version=0.0.0.0, Culture=neutral, null";
                    typeNamebuilder.Append(", " + expectedAssemblyName);
                }
                else
                {
                    expectedAssemblyName = null;
                }

                typeNamesToDecode[index] = typeNamebuilder.ToString();
                expectedDecodeNames[index] = new MetadataHelpers.AssemblyQualifiedTypeName(expectedTopLevelTypeName, expectedNestedTypes, expectedTypeArguments, expectedArrayRanks, expectedAssemblyName);

                typeNamebuilder.Clear();
            }

            pooledStrBuilder.Free();
            return typeNamesToDecode;
        }

        private static void VerifyDecodedTypeName(
            MetadataHelpers.AssemblyQualifiedTypeName decodedName,
            string expectedTopLevelType,
            string expectedAssemblyName,
            string[] expectedNestedTypes,
            MetadataHelpers.AssemblyQualifiedTypeName[] expectedTypeArguments,
            int[] expectedArrayRanks)
        {
            Assert.Equal(expectedTopLevelType, decodedName.TopLevelType);
            Assert.Equal(expectedAssemblyName, decodedName.AssemblyName);
            Assert.Equal(expectedNestedTypes, decodedName.NestedTypes);
            
            if (decodedName.TypeArguments == null)
            {
                Assert.Null(expectedTypeArguments);
            }
            else
            {
                var decodedTypeArguments = decodedName.TypeArguments;
                for (int i = 0; i < decodedTypeArguments.Length; i++)
                {
                    var expectedTypeArgument = expectedTypeArguments[i];
                    VerifyDecodedTypeName(decodedTypeArguments[i], expectedTypeArgument.TopLevelType, expectedTypeArgument.AssemblyName,
                        expectedTypeArgument.NestedTypes, expectedTypeArgument.TypeArguments, expectedTypeArgument.ArrayRanks);
                }
            }
        }

        private static void DecodeTypeNameAndVerify(
            MetadataHelpers.SerializedTypeDecoder decoder,
            string nameToDecode,
            string expectedTopLevelType,
            string expectedAssemblyName = null,
            string[] expectedNestedTypes = null,
            MetadataHelpers.AssemblyQualifiedTypeName[] expectedTypeArguments = null,
            int[] expectedArrayRanks = null)
        {
            MetadataHelpers.AssemblyQualifiedTypeName decodedName = decoder.DecodeTypeName(nameToDecode);
            VerifyDecodedTypeName(decodedName, expectedTopLevelType, expectedAssemblyName, expectedNestedTypes, expectedTypeArguments, expectedArrayRanks);
        }

        private static void DecodeTypeNamesAndVerify(MetadataHelpers.SerializedTypeDecoder decoder, string[] namesToDecode, MetadataHelpers.AssemblyQualifiedTypeName[] expectedDecodedNames)
        {
            Assert.Equal(namesToDecode.Length, expectedDecodedNames.Length);

            for (int i = 0; i < namesToDecode.Length; i++)
            {
                var expectedDecodedName = expectedDecodedNames[i];
                DecodeTypeNameAndVerify(decoder, namesToDecode[i], expectedDecodedName.TopLevelType, expectedDecodedName.AssemblyName,
                    expectedDecodedName.NestedTypes, expectedDecodedName.TypeArguments, expectedDecodedName.ArrayRanks);
            }
        }

        [WorkItem(546277)]
        [Fact]
        public void TestDecodeTypeNameMatrix()
        {
            var decoder = new MetadataHelpers.SerializedTypeDecoder();
            TypeNameConfig[] configsToTest = GenerateTypeNameConfigs(0);
            MetadataHelpers.AssemblyQualifiedTypeName[] expectedDecodedNames;
            string[] namesToDecode = GenerateTypeNamesToDecode(configsToTest, out expectedDecodedNames);
            DecodeTypeNamesAndVerify(decoder, namesToDecode, expectedDecodedNames);
        }

        [WorkItem(546277)]
        [Fact]
        public void TestDecodeArrayTypeName_Bug15478()
        {
            var decoder = new MetadataHelpers.SerializedTypeDecoder();
            DecodeTypeNameAndVerify(decoder, "System.Int32[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                expectedTopLevelType: "System.Int32",
                expectedAssemblyName: "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                expectedArrayRanks: new[] { 1 });
        }

        [WorkItem(546277)]
        [Fact]
        public void TestDecodeArrayTypeName_Valid()
        {
            var decoder = new MetadataHelpers.SerializedTypeDecoder();

            // Single-D Array
            DecodeTypeNameAndVerify(decoder, "W[]",
                expectedTopLevelType: "W",
                expectedArrayRanks: new[] { 1 });

            // Multi-D Array
            DecodeTypeNameAndVerify(decoder, "W[,]",
                expectedTopLevelType: "W",
                expectedArrayRanks: new[] { 2 });

            // Jagged Array
            DecodeTypeNameAndVerify(decoder, "W[][,]",
                expectedTopLevelType: "W",
                expectedArrayRanks: new[] { 1, 2 });

            // Generic Type Jagged Array
            DecodeTypeNameAndVerify(decoder, "Y`1[W][][,]",
                expectedTopLevelType: "Y`1",
                expectedTypeArguments: new[] { new MetadataHelpers.AssemblyQualifiedTypeName("W", null, null, null, null) },
                expectedArrayRanks: new[] { 1, 2 });

            // Nested Generic Type Jagged Array with Array type argument
            DecodeTypeNameAndVerify(decoder, "Y`1+F[[System.Int32[], mscorlib]][,,][][,]",
                expectedTopLevelType: "Y`1",
                expectedNestedTypes: new[] { "F" },
                expectedTypeArguments: new[] { new MetadataHelpers.AssemblyQualifiedTypeName(
                                                    "System.Int32",
                                                    nestedTypes: null,
                                                    typeArguments: null,
                                                    arrayRanks: new[] { 1 },
                                                    assemblyName: "mscorlib") },
                expectedArrayRanks: new[] { 3, 1, 2 });

            // Nested Generic Type Jagged Array with type arguments from nested type and outer type
            DecodeTypeNameAndVerify(decoder, "Y`1+Z`1[[System.Int32[], mscorlib], W][][,]",
                expectedTopLevelType: "Y`1",
                expectedNestedTypes: new[] { "Z`1" },
                expectedTypeArguments: new[] { new MetadataHelpers.AssemblyQualifiedTypeName(
                                                    "System.Int32",
                                                    nestedTypes: null,
                                                    typeArguments: null,
                                                    arrayRanks: new[] { 1 },
                                                    assemblyName: "mscorlib"),
                                               new MetadataHelpers.AssemblyQualifiedTypeName("W", null, null, null, null) },
                expectedArrayRanks: new[] { 1, 2 });
        }

        [WorkItem(546277)]
        [Fact]
        public void TestDecodeArrayTypeName_Invalid()
        {
            var decoder = new MetadataHelpers.SerializedTypeDecoder();
            
            // Error case, array shape before nested type
            DecodeTypeNameAndVerify(decoder, "X[]+Y",
                expectedTopLevelType: "X+Y",
                expectedNestedTypes: null,
                expectedArrayRanks: new [] { 1 });

            // Error case, array shape before generic type arguments
            DecodeTypeNameAndVerify(decoder, "X[]`1[T]",
                expectedTopLevelType: "X`1[T]",
                expectedTypeArguments: null,
                expectedArrayRanks: new[] { 1 });

            // Error case, invalid array shape
            DecodeTypeNameAndVerify(decoder, "X[T]",
                expectedTopLevelType: "X[T]",
                expectedTypeArguments: null,
                expectedArrayRanks: null);

            DecodeTypeNameAndVerify(decoder, "X[,",
                expectedTopLevelType: "X[,",
                expectedTypeArguments: null,
                expectedArrayRanks: null);

            // Incomplete type argument assembly name
            DecodeTypeNameAndVerify(decoder, "X`1[[T, Assembly",
                expectedTopLevelType: "X`1",
                expectedAssemblyName: null,
                expectedTypeArguments: new [] { new MetadataHelpers.AssemblyQualifiedTypeName("T", null, null, null, "Assembly") },
                expectedArrayRanks: null);
        }
    }
}
