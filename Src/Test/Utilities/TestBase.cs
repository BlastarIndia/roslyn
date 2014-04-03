﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using ProprietaryTestResources = Microsoft.CodeAnalysis.Test.Resources.Proprietary;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.PdbUtilities;
using Xunit;

namespace Roslyn.Test.Utilities
{
    /// <summary>
    /// Base class for all unit test classes.
    /// </summary>
    public abstract class TestBase : IDisposable
    {
        private TempRoot temp;

        static MetadataReference[] _WinRtRefs;
        static MetadataReference[] _PortableRefsMinimal;

        /// <summary>
        /// The array of 7 metadataimagereferences that are required to compile
        /// against windows.winmd (including windows.winmd itself).
        /// </summary>
        public static MetadataReference[] WinRtRefs
        {
            get
            {
                if (_WinRtRefs == null)
                {
                    var winmd = new MetadataImageReference(TestResources.WinRt.Windows, display: "Windows");
                    
                    var windowsruntime =
                        new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319_17929.System_Runtime_WindowsRuntime, display: "System.Runtime.WindowsRuntime.dll");
                                                    
                    var runtime =
                        new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319_17929.System_Runtime, display: "System.Runtime.dll");

                    var objectModel =
                        new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319_17929.System_ObjectModel, display: "System.ObjectModel.dll");


                    var uixaml = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319_17929.System_Runtime_WindowsRuntime_UI_Xaml,
                                                            display: "System.Runtime.WindowsRuntime.UI.Xaml.dll");


                    var interop = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319_17929.System_Runtime_InteropServices_WindowsRuntime,
                                                             display: "System.Runtime.InteropServices.WindowsRuntime.dll");

                    //Not mentioned in the adapter doc but pointed to from System.Runtime, so we'll put it here.
                    var system = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.System,
                                                             display: "System.dll");

                    var mscor = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30316_17626.mscorlib,
                                                             display: "mscorlib");

                    _WinRtRefs = new MetadataReference[] { winmd, windowsruntime, runtime, objectModel, uixaml, interop, system, mscor };
                }

                return _WinRtRefs;

            }

        }

        /// <summary>
        /// The array of minimal references for portable library (mscorlib.dll and System.Runtime.dll)
        /// </summary>
        public static MetadataReference[] PortableRefsMinimal
        {
            get
            {
                if (_PortableRefsMinimal == null)
                {
                    var mscorlibPortable = new MetadataImageReference(ProprietaryTestResources.NetFX.ReferenceAssemblies_PortableProfile7.mscorlib, display: "mscorlib.dll");

                    var systemRuntimePortable =
                        new MetadataImageReference(ProprietaryTestResources.NetFX.ReferenceAssemblies_PortableProfile7.System_Runtime, display: "System.Runtime.dll");

                    _PortableRefsMinimal = new MetadataReference[] { mscorlibPortable, systemRuntimePortable };
                }

                return _PortableRefsMinimal;
            }
        }

        protected TestBase()
        {
        }

        public static string GetUniqueName()
        {
            return Guid.NewGuid().ToString("D");
        }

        #region Metadata References

        /// <summary>
        /// Reference to an assembly that defines Expression Trees.
        /// </summary>
        public static MetadataReference ExpressionAssemblyRef
        {
            get { return SystemCoreRef; }
        }

        /// <summary>
        /// Reference to an assembly that defines LINQ operators.
        /// </summary>
        public static MetadataReference LinqAssemblyRef
        {
            get { return SystemCoreRef; }
        }

        /// <summary>
        /// Reference to an assembly that defines ExtensionAttribute.
        /// </summary>
        public static MetadataReference ExtensionAssemblyRef
        {
            get { return SystemCoreRef; }
        }

        private static MetadataReference _systemCoreRef_v4_0_30319_17929;
        public static MetadataReference SystemCoreRef_v4_0_30319_17929
        {
            get
            {
                if (_systemCoreRef_v4_0_30319_17929 == null)
                {
                    _systemCoreRef_v4_0_30319_17929 = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319_17929.System_Core, display: "System.Core.v4_0_30319_17929.dll");
                }

                return _systemCoreRef_v4_0_30319_17929;
            }
        }

        private static MetadataReference _systemCoreRef;
        public static MetadataReference SystemCoreRef
        {
            get
            {
                if (_systemCoreRef == null)
                {
                    _systemCoreRef = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.System_Core, display: "System.Core.v4_0_30319.dll");
                }

                return _systemCoreRef;
            }
        }

        private static MetadataReference _systemWindowsFormsRef;
        public static MetadataReference SystemWindowsFormsRef
        {
            get
            {
                if (_systemWindowsFormsRef == null)
                {
                    _systemWindowsFormsRef = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.System_Windows_Forms, display: "System.Windows.Forms.v4_0_30319.dll");
                }

                return _systemWindowsFormsRef;
            }
        }

        private static MetadataReference _systemDrawingRef;
        public static MetadataReference SystemDrawingRef
        {
            get
            {
                if (_systemDrawingRef == null)
                {
                    _systemDrawingRef = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.System_Drawing, display: "System.Drawing.v4_0_30319.dll");
                }

                return _systemDrawingRef;
            }
        }

        private static MetadataReference _systemDataRef;
        public static MetadataReference SystemDataRef
        {
            get
            {
                if (_systemDataRef == null)
                {
                    _systemDataRef = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.System_Data, display: "System.Data.v4_0_30319.dll");
                }

                return _systemDataRef;
            }
        }

        private static MetadataReference _mscorlibRef;
        public static MetadataReference MscorlibRef
        {
            get
            {
                if (_mscorlibRef == null)
                {
                    _mscorlibRef = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.mscorlib, display: "mscorlib.v4_0_30319.dll");
                }

                return _mscorlibRef;
            }
        }

        private static MetadataReference _mscorlibRefPortable;
        public static MetadataReference MscorlibRefPortable
        {
            get
            {
                if (_mscorlibRefPortable == null)
                {
                    _mscorlibRefPortable = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.mscorlib_portable, display: "mscorlib.v4_0_30319.portable.dll");
                }

                return _mscorlibRefPortable;
            }
        }

        private static MetadataReference _aacorlibRef;
        public static MetadataReference AacorlibRef
        {
            get
            {
                if (_aacorlibRef == null)
                {
                    _aacorlibRef = new MetadataImageReference(TestResources.NetFX.aacorlib_v15_0_3928.aacorlib_v15_0_3928, display: "mscorlib.v4_0_30319.dll");
                }

                return _aacorlibRef;
            }
        }

        private static MetadataReference _mscorlibRef_v4_0_30316_17626;
        public static MetadataReference MscorlibRef_v4_0_30316_17626
        {
            get
            {
                if (_mscorlibRef_v4_0_30316_17626 == null)
                {
                    _mscorlibRef_v4_0_30316_17626 = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30316_17626.mscorlib, display: "mscorlib.v4_0_30319_17626.dll", fullPath: @"Z:\FxReferenceAssembliesUri");
                }

                return _mscorlibRef_v4_0_30316_17626;
            }
        }

        private static MetadataReference _mscorlibRef_v20;
        public static MetadataReference MscorlibRef_v20
        {
            get
            {
                if (_mscorlibRef_v20 == null)
                {
                    _mscorlibRef_v20 = new MetadataImageReference(ProprietaryTestResources.NetFX.v2_0_50727.mscorlib, display: "mscorlib.v2.0.50727.dll");
                }

                return _mscorlibRef_v20;
            }
        }

        /// <summary>
        /// Reference to an mscorlib silverlight assembly in which the System.Array does not contain the special member LongLength.
        /// </summary>
        private static MetadataReference _mscorlibRef_silverlight;
        public static MetadataReference MscorlibRefSilverlight
        {
            get
            {
                if (_mscorlibRef_silverlight == null)
                {
                    _mscorlibRef_silverlight = new MetadataImageReference(ProprietaryTestResources.NetFX.silverlight_v5_0_5_0.mscorlib_v5_0_5_0_silverlight, display: "mscorlib.v5.0.5.0_silverlight.dll");
                }

                return _mscorlibRef_silverlight;
            }
        }

        private static MetadataReference _minCorlibRef;
        public static MetadataReference MinCorlibRef
        {
            get
            {
                if (_minCorlibRef == null)
                {
                    _minCorlibRef = new MetadataImageReference(TestResources.NetFX.Minimal.mincorlib, display: "minCorLib.dll");
                }

                return _minCorlibRef;
            }
        }

        private static MetadataReference _MsvbRef;
        public static MetadataReference MsvbRef
        {
            get
            {
                if (_MsvbRef == null)
                {
                    _MsvbRef = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.Microsoft_VisualBasic, display: "Microsoft.VisualBasic.v4_0_30319.dll");
                }

                return _MsvbRef;
            }
        }

        private static MetadataReference _MsvbRef_v4_0_30319_17929;
        public static MetadataReference MsvbRef_v4_0_30319_17929
        {
            get
            {
                if (_MsvbRef_v4_0_30319_17929 == null)
                {
                    _MsvbRef_v4_0_30319_17929 = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319_17929.Microsoft_VisualBasic, display: "Microsoft.VisualBasic.v4_0_30319_17929.dll");
                }

                return _MsvbRef_v4_0_30319_17929;
            }
        }

        private static MetadataReference csharpRef;
        public static MetadataReference CSharpRef
        {
            get
            {
                if (csharpRef == null)
                {
                    csharpRef = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.Microsoft_CSharp, display: "Microsoft.CSharp.v4.0.30319.dll");
                }

                return csharpRef;
            }
        }


        private static MetadataReference _SystemRef;
        public static MetadataReference SystemRef
        {
            get
            {
                if (_SystemRef == null)
                {
                    _SystemRef = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.System, display: "System.v4_0_30319.dll");
                }

                return _SystemRef;
            }
        }

        private static MetadataReference _SystemRef_v4_0_30319_17929;
        public static MetadataReference SystemRef_v4_0_30319_17929
        {
            get
            {
                if (_SystemRef_v4_0_30319_17929 == null)
                {
                    _SystemRef_v4_0_30319_17929 = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319_17929.System, display: "System.v4_0_30319_17929.dll");
                }

                return _SystemRef_v4_0_30319_17929;
            }
        }

        private static MetadataReference _SystemRef_v20;
        public static MetadataReference SystemRef_v20
        {
            get
            {
                if (_SystemRef_v20 == null)
                {
                    _SystemRef_v20 = new MetadataImageReference(ProprietaryTestResources.NetFX.v2_0_50727.System, display: "System.v2_0_50727.dll");
                }

                return _SystemRef_v20;
            }
        }

        private static MetadataReference _SystemXmlRef;
        public static MetadataReference SystemXmlRef
        {
            get
            {
                if (_SystemXmlRef == null)
                {
                    _SystemXmlRef = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.System_Xml, display: "System.Xml.v4_0_30319.dll");
                }

                return _SystemXmlRef;
            }
        }

        private static MetadataReference _SystemXmlLinqRef;
        public static MetadataReference SystemXmlLinqRef
        {
            get
            {
                if (_SystemXmlLinqRef == null)
                {
                    _SystemXmlLinqRef = new MetadataImageReference(ProprietaryTestResources.NetFX.v4_0_30319.System_Xml_Linq, display: "System.Xml.Linq.v4_0_30319.dll");
                }

                return _SystemXmlLinqRef;
            }
        }

        private static MetadataReference _FacadeSystemRuntimeRef;
        public static MetadataReference FacadeSystemRuntimeRef
        {
            get
            {
                if (_FacadeSystemRuntimeRef == null)
                {
                    _FacadeSystemRuntimeRef = new MetadataImageReference(ProprietaryTestResources.NetFX.ReferenceAssemblies_V45_Facades.System_Runtime, display: "System.Runtime.dll");
                }

                return _FacadeSystemRuntimeRef;
            }
        }

        private static MetadataReference _FSharpTestLibraryRef;
        public static MetadataReference FSharpTestLibraryRef
        {
            get
            {
                if (_FSharpTestLibraryRef == null)
                {
                    _FSharpTestLibraryRef = new MetadataImageReference(TestResources.SymbolsTests.General.FSharpTestLibrary, display: "FSharpTestLibrary.dll");
                }

                return _FSharpTestLibraryRef;
            }
        }

        public static MetadataReference InvalidRef = new TestMetadataReference(fullPath: @"R:\Invalid.dll");

        #endregion

        #region File System

        public TempRoot Temp
        {
            get
            {
                if (temp == null)
                {
                    temp = new TempRoot();
                }

                return temp;
            }
        }

        public virtual void Dispose()
        {
            if (temp != null)
            {
                temp.Dispose();
            }
        }

        #endregion

        #region Execution

        public static int Execute(System.Reflection.Assembly assembly, string[] args)
        {
            var parameters = assembly.EntryPoint.GetParameters();
            object[] arguments;

            if (parameters.Length == 0)
            {
                arguments = new object[0];
            }
            else if (parameters.Length == 1)
            {
                arguments = new object[] { args };
            }
            else
            {
                throw new InvalidOperationException("Invalid entry point");
            }

            if (assembly.EntryPoint.ReturnType == typeof(int))
            {
                return (int)assembly.EntryPoint.Invoke(null, arguments);
            }
            else if (assembly.EntryPoint.ReturnType == typeof(void))
            {
                assembly.EntryPoint.Invoke(null, arguments);
                return 0;
            }
            else
            {
                throw new InvalidOperationException("Invalid entry point");
            }
        }

        #endregion

        #region Metadata Validation

        /// <summary>
        /// Returns the name of the attribute class 
        /// </summary>
        internal static string GetAttributeName(MetadataReader metadataReader, CustomAttributeHandle customAttribute)
        {
            var ctorHandle = metadataReader.GetCustomAttribute(customAttribute).Constructor;
            if (ctorHandle.HandleType == HandleType.MemberReference) // MemberRef
            {
                var container = metadataReader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent;
                var name = metadataReader.GetTypeReference((TypeReferenceHandle)container).Name;
                return metadataReader.GetString(name);
            }
            else
            {
                Assert.True(false, "not impl");
                return null;
            }
        }

        internal static CustomAttributeHandle FindCustomAttribute(MetadataReader metadataReader, string attributeClassName)
        {
            foreach (var caHandle in metadataReader.CustomAttributes)
            {
                if (string.Equals(GetAttributeName(metadataReader, caHandle), attributeClassName, StringComparison.Ordinal))
                {
                    return caHandle;
                }
            }

            return default(CustomAttributeHandle);
        }

        /// <summary>
        /// Used to validate metadata blobs emitted for MarshalAs.
        /// </summary>
        internal static void MarshalAsMetadataValidator(PEAssembly assembly, Func<string, PEAssembly, EmitOptions, byte[]> getExpectedBlob, EmitOptions emitOptions, bool isField = true)
        {
            var metadataReader = assembly.GetMetadataReader();

            // no custom attributes should be emitted on parameters, fields or methods:
            foreach (var ca in metadataReader.CustomAttributes)
            {
                Assert.NotEqual("MarshalAsAttribute", GetAttributeName(metadataReader, ca));
            }

            int expectedMarshalCount = 0;

            if (isField)
            {
                // fields
                foreach (var fieldDef in metadataReader.FieldDefinitions)
                {
                    var field = metadataReader.GetField(fieldDef);
                    string fieldName = metadataReader.GetString(field.Name);

                    byte[] expectedBlob = getExpectedBlob(fieldName, assembly, emitOptions);
                    if (expectedBlob != null)
                    {
                        BlobHandle descriptor = metadataReader.GetField(fieldDef).GetMarshallingDescriptor();
                        Assert.False(descriptor.IsNil, "Expecting record in FieldMarshal table");

                        Assert.NotEqual(0, (int)(field.Attributes & FieldAttributes.HasFieldMarshal));
                        expectedMarshalCount++;

                        byte[] actualBlob = metadataReader.GetBytes(descriptor);
                        AssertEx.Equal(expectedBlob, actualBlob);
                    }
                    else
                    {
                        Assert.Equal(0, (int)(field.Attributes & FieldAttributes.HasFieldMarshal));
                    }
                }
            }
            else
            {
                // parameters
                foreach (var methodHandle in metadataReader.MethodDefinitions)
                {
                    var methodDef = metadataReader.GetMethod(methodHandle);
                    string memberName = metadataReader.GetString(methodDef.Name);
                    foreach (var paramHandle in methodDef.GetParameters())
                    {
                        var paramRow = metadataReader.GetParameter(paramHandle);
                        string paramName = metadataReader.GetString(paramRow.Name);

                        byte[] expectedBlob = getExpectedBlob(memberName + ":" + paramName, assembly, emitOptions);
                        if (expectedBlob != null)
                        {
                            Assert.NotEqual(0, (int)(paramRow.Attributes & ParameterAttributes.HasFieldMarshal));
                            expectedMarshalCount++;

                            BlobHandle descriptor = metadataReader.GetParameter(paramHandle).GetMarshallingDescriptor();
                            Assert.False(descriptor.IsNil, "Expecting record in FieldMarshal table");

                            byte[] actualBlob = metadataReader.GetBytes(descriptor);

                            AssertEx.Equal(expectedBlob, actualBlob);
                        }
                        else
                        {
                            Assert.Equal(0, (int)(paramRow.Attributes & ParameterAttributes.HasFieldMarshal));
                        }
                    }
                }
            }

            Assert.Equal(expectedMarshalCount, metadataReader.GetTableRowCount(TableIndex.FieldMarshal));
        }

        /// <summary>
        /// Creates instance of SignatureDescription for a specified member
        /// </summary>
        /// <param name="fullyQualifiedTypeName">
        /// Fully qualified type name for member
        /// Names must be in format recognized by reflection
        /// e.g. MyType{T}.MyNestedType{T, U} => MyType`1+MyNestedType`2
        /// </param>
        /// <param name="memberName">
        /// Name of member on specified type whose signature needs to be verified
        /// Names must be in format recognized by reflection
        /// e.g. For explicitly implemented member - I1{string}.Method => I1{System.String}.Method
        /// </param>
        /// <param name="expectedSignature">
        /// Baseline string for signature of specified member
        /// Skip this argument to get an error message that shows all available signatures for specified member
        /// </param>
        /// <returns>Instance of SignatureDescription for specified member</returns>
        internal static SignatureDescription Signature(string fullyQualifiedTypeName, string memberName, string expectedSignature = "")
        {
            return new SignatureDescription()
            {
                FullyQualifiedTypeName = fullyQualifiedTypeName,
                MemberName = memberName,
                ExpectedSignature = expectedSignature
            };
        }

        internal static IEnumerable<string> GetFullTypeNames(MetadataReader metadataReader)
        {
            foreach (var typeDefHandle in metadataReader.TypeDefinitions)
            {
                var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                var ns = metadataReader.GetString(typeDef.Namespace);
                var name = metadataReader.GetString(typeDef.Name);

                yield return (ns.Length == 0) ? name : (ns + "." + name);
            }
        }

        #endregion

        #region PDB Validation

        public static string GetPdbXml(Compilation compilation, string methodName = "")
        {
            string actual = null;
            using (var exebits = new MemoryStream())
            {
                using (var pdbbits = new MemoryStream())
                {
                    compilation.Emit(exebits, null, "DontCare", pdbbits, null);

                    pdbbits.Position = 0;
                    exebits.Position = 0;
                    actual = PdbToXmlConverter.ToXml(pdbbits, exebits, PdbToXmlOptions.ResolveTokens | PdbToXmlOptions.ThrowOnError, methodName: methodName);
                }
            }

            return actual;
        }

        public static string GetTokenToLocationMap(Compilation compilation, bool maskToken = false)
        {
            using (var exebits = new MemoryStream())
            {
                using (var pdbbits = new MemoryStream())
                {
                    compilation.Emit(exebits, null, "DontCare", pdbbits, null);
                    return Token2SourceLineExporter.TokenToSourceMap2Xml(pdbbits, maskToken);
                }
            }
        }

        public static void AssertXmlEqual(string expected, string actual)
        {
            XmlElementDiff.AssertEqual(expected, actual, ShallowElementComparer.Instance);
        }

        public static void AssertXmlEqual(XElement expected, XElement actual)
        {
            XmlElementDiff.AssertEqual(expected, actual, ShallowElementComparer.Instance);
        }

        private class ShallowElementComparer : IEqualityComparer<XElement>
        {
            public static readonly IEqualityComparer<XElement> Instance = new ShallowElementComparer();

            private ShallowElementComparer() { }

            public bool Equals(XElement element1, XElement element2)
            {
                Assert.NotNull(element1);
                Assert.NotNull(element2);

                return element1.Name == "customDebugInfo"
                    ? element1.ToString() == element2.ToString()
                    : XmlElementDiff.NameAndAttributeComparer.Instance.Equals(element1, element2);
            }

            public int GetHashCode(XElement element)
            {
                return element.Name.GetHashCode();
            }
        }

        protected static string ConsolidateArguments(string[] args)
        {
            var consolidated = new StringBuilder();
            foreach (string argument in args)
            {
                bool surround = Regex.Match(argument, @"[\s+]").Success;
                if (surround)
                {
                    consolidated.AppendFormat("\"{0}\" ", argument);
                }
                else
                {
                    consolidated.AppendFormat("{0} ", argument);
                }
            }
            return consolidated.ToString();
        }

        protected static string RunAndGetOutput(string exeFileName, string arguments = null, int expectedRetCode = 0, string startFolder = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(exeFileName);
            if (arguments != null)
            {
                startInfo.Arguments = arguments;
            }
            string result = null;

            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;

            if (startFolder != null)
            {
                startInfo.WorkingDirectory = startFolder;
            }

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                // Do not wait for the child process to exit before reading to the end of its
                // redirected stream. Read the output stream first and then wait. Doing otherwise
                // might cause a deadlock.
                result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                Assert.Equal(expectedRetCode, process.ExitCode);
            }

            return result;
        }

        #endregion

    }
}
