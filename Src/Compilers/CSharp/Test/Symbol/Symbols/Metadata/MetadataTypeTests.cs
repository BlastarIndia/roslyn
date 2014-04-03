﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class MetadataTypeTests : CSharpTestBase
    {
        [Fact]
        public void MetadataNamespaceSymbol01()
        {
            var text = "public class A {}";
            var compilation = CreateCompilationWithMscorlib(text);

            var mscorlib = compilation.ExternalReferences[0];
            var mscorNS = compilation.GetReferencedAssemblySymbol(mscorlib);
            Assert.Equal("mscorlib", mscorNS.Name);
            Assert.Equal(SymbolKind.Assembly, mscorNS.Kind);
            var ns1 = mscorNS.GlobalNamespace.GetMembers("System").Single() as NamespaceSymbol;
            var ns2 = ns1.GetMembers("Runtime").Single() as NamespaceSymbol;
            var ns = ns2.GetMembers("Serialization").Single() as NamespaceSymbol;

            Assert.Equal(mscorNS, ns.ContainingAssembly);
            Assert.Equal(ns2, ns.ContainingSymbol);
            Assert.Equal(ns2, ns.ContainingNamespace);
            Assert.True(ns.IsDefinition); // ?
            Assert.True(ns.IsNamespace);
            Assert.False(ns.IsType);

            Assert.Equal(SymbolKind.Namespace, ns.Kind);
            // bug 1995
            Assert.Equal(Accessibility.Public, ns.DeclaredAccessibility);
            Assert.True(ns.IsStatic);
            Assert.False(ns.IsAbstract);
            Assert.False(ns.IsSealed);
            Assert.False(ns.IsVirtual);
            Assert.False(ns.IsOverride);

            // 47 types, 1 namespace (Formatters);
            Assert.Equal(48, ns.GetMembers().Length);
            Assert.Equal(47, ns.GetTypeMembers().Length);

            var fullName = "System.Runtime.Serialization";
            Assert.Equal(fullName, ns.ToTestDisplayString());

            Assert.Empty(compilation.GetDeclarationDiagnostics());
        }

        [Fact]
        public void MetadataTypeSymbolClass01()
        {
            var text = "public class A {}";
            var compilation = CreateCompilationWithMscorlib(text);

            var mscorlib = compilation.ExternalReferences[0];
            var mscorNS = compilation.GetReferencedAssemblySymbol(mscorlib);
            Assert.Equal("mscorlib", mscorNS.Name);
            var ns1 = mscorNS.GlobalNamespace.GetMembers("Microsoft").Single() as NamespaceSymbol;
            var ns2 = ns1.GetMembers("Runtime").Single() as NamespaceSymbol;
            var ns3 = ns2.GetMembers("Hosting").Single() as NamespaceSymbol;

            var class1 = ns3.GetTypeMembers("StrongNameHelpers").First() as NamedTypeSymbol;
            // internal static class
            Assert.Equal(0, class1.Arity);
            Assert.Equal(mscorNS, class1.ContainingAssembly);
            Assert.Equal(ns3, class1.ContainingSymbol);
            Assert.Equal(ns3, class1.ContainingNamespace);
            Assert.True(class1.IsDefinition);
            Assert.False(class1.IsNamespace);
            Assert.True(class1.IsType);
            Assert.True(class1.IsReferenceType);
            Assert.False(class1.IsValueType);

            Assert.Equal(SymbolKind.NamedType, class1.Kind);
            Assert.Equal(TypeKind.Class, class1.TypeKind);
            Assert.Equal(Accessibility.Internal, class1.DeclaredAccessibility);
            Assert.True(class1.IsStatic);
            Assert.False(class1.IsAbstract);
            Assert.False(class1.IsAbstract);
            Assert.False(class1.IsExtern);
            Assert.False(class1.IsSealed);
            Assert.False(class1.IsVirtual);
            Assert.False(class1.IsOverride);

            // 18 members
            Assert.Equal(18, class1.GetMembers().Length);
            Assert.Equal(0, class1.GetTypeMembers().Length);
            Assert.Equal(0, class1.Interfaces.Length);

            var fullName = "Microsoft.Runtime.Hosting.StrongNameHelpers";
            // Internal: Assert.Equal(fullName, class1.GetFullNameWithoutGenericArgs());
            Assert.Equal(fullName, class1.ToTestDisplayString());
            Assert.Equal(0, class1.TypeArguments.Length);
            Assert.Equal(0, class1.TypeParameters.Length);

            Assert.Empty(compilation.GetDeclarationDiagnostics());
        }

        [Fact]
        public void MetadataTypeSymbolGenClass02()
        {
            var text = "public class A {}";
            var compilation = CreateCompilationWithMscorlib(text, compOptions: TestOptions.DllAlwaysImportInternals);

            var mscorlib = compilation.ExternalReferences[0];
            var mscorNS = compilation.GetReferencedAssemblySymbol(mscorlib);
            Assert.Equal("mscorlib", mscorNS.Name);
            Assert.Equal(SymbolKind.Assembly, mscorNS.Kind);
            var ns1 = (mscorNS.GlobalNamespace.GetMembers("System").Single() as NamespaceSymbol).GetMembers("Collections").Single() as NamespaceSymbol;
            var ns2 = ns1.GetMembers("Generic").Single() as NamespaceSymbol;

            var type1 = ns2.GetTypeMembers("Dictionary").First() as NamedTypeSymbol;
            // public generic class
            Assert.Equal(2, type1.Arity);
            Assert.Equal(mscorNS, type1.ContainingAssembly);
            Assert.Equal(ns2, type1.ContainingSymbol);
            Assert.Equal(ns2, type1.ContainingNamespace);
            Assert.True(type1.IsDefinition);
            Assert.False(type1.IsNamespace);
            Assert.True(type1.IsType);
            Assert.True(type1.IsReferenceType);
            Assert.False(type1.IsValueType);

            Assert.Equal(SymbolKind.NamedType, type1.Kind);
            Assert.Equal(TypeKind.Class, type1.TypeKind);
            Assert.Equal(Accessibility.Public, type1.DeclaredAccessibility);
            Assert.False(type1.IsStatic);
            Assert.False(type1.IsAbstract);
            Assert.False(type1.IsSealed);
            Assert.False(type1.IsVirtual);
            Assert.False(type1.IsOverride);

            // 4 nested types, 64 members overall
            Assert.Equal(64, type1.GetMembers().Length);
            Assert.Equal(4, type1.GetTypeMembers().Length);
            // IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>>, 
            // IDictionary, ICollection, IEnumerable, ISerializable, IDeserializationCallback
            Assert.Equal(8, type1.Interfaces.Length);

            var fullName = "System.Collections.Generic.Dictionary<TKey, TValue>";
            // Internal Assert.Equal(fullName, class1.GetFullNameWithoutGenericArgs());
            Assert.Equal(fullName, type1.ToTestDisplayString());
            Assert.Equal(2, type1.TypeArguments.Length);
            Assert.Equal(2, type1.TypeParameters.Length);

            Assert.Empty(compilation.GetDeclarationDiagnostics());
        }

        [Fact]
        public void MetadataTypeSymbolGenInterface01()
        {
            var text = "public class A {}";
            var compilation = CreateCompilationWithMscorlib(text);

            var mscorlib = compilation.ExternalReferences[0];
            var mscorNS = compilation.GetReferencedAssemblySymbol(mscorlib);
            var ns1 = (mscorNS.GlobalNamespace.GetMembers("System").Single() as NamespaceSymbol).GetMembers("Collections").Single() as NamespaceSymbol;
            var ns2 = ns1.GetMembers("Generic").Single() as NamespaceSymbol;

            var type1 = ns2.GetTypeMembers("IList").First() as NamedTypeSymbol;
            // public generic interface
            Assert.Equal(1, type1.Arity);
            Assert.Equal(mscorNS, type1.ContainingAssembly);
            Assert.Equal(ns2, type1.ContainingSymbol);
            Assert.Equal(ns2, type1.ContainingNamespace);
            Assert.True(type1.IsDefinition);
            Assert.False(type1.IsNamespace);
            Assert.True(type1.IsType);
            Assert.True(type1.IsReferenceType);
            Assert.False(type1.IsValueType);

            Assert.Equal(SymbolKind.NamedType, type1.Kind);
            Assert.Equal(TypeKind.Interface, type1.TypeKind);
            Assert.Equal(Accessibility.Public, type1.DeclaredAccessibility);
            Assert.False(type1.IsStatic);
            Assert.True(type1.IsAbstract);
            Assert.False(type1.IsSealed);
            Assert.False(type1.IsVirtual);
            Assert.False(type1.IsOverride);
            Assert.False(type1.IsExtern);

            // 3 method, 2 get|set_<Prop> method, 1 Properties
            Assert.Equal(6, type1.GetMembers().Length);
            Assert.Equal(0, type1.GetTypeMembers().Length);
            // ICollection<T>, IEnumerable<T>, IEnumerable
            Assert.Equal(3, type1.Interfaces.Length);

            var fullName = "System.Collections.Generic.IList<T>";
            // Internal Assert.Equal(fullName, class1.GetFullNameWithoutGenericArgs());
            Assert.Equal(fullName, type1.ToTestDisplayString());
            Assert.Equal(1, type1.TypeArguments.Length);
            Assert.Equal(1, type1.TypeParameters.Length);

            Assert.Empty(compilation.GetDeclarationDiagnostics());
        }

        [Fact]
        public void MetadataTypeSymbolStruct01()
        {
            var text = "public class A {}";
            var compilation = CreateCompilationWithMscorlib(text, compOptions: TestOptions.DllAlwaysImportInternals);

            var mscorlib = compilation.ExternalReferences[0];
            var mscorNS = compilation.GetReferencedAssemblySymbol(mscorlib);
            Assert.Equal("mscorlib", mscorNS.Name);
            var ns1 = mscorNS.GlobalNamespace.GetMembers("System").Single() as NamespaceSymbol;
            var ns2 = ns1.GetMembers("Runtime").Single() as NamespaceSymbol;
            var ns3 = ns2.GetMembers("Serialization").Single() as NamespaceSymbol;
            var type1 = ns3.GetTypeMembers("StreamingContext").First() as NamedTypeSymbol;

            Assert.Equal(mscorNS, type1.ContainingAssembly);
            Assert.Equal(ns3, type1.ContainingSymbol);
            Assert.Equal(ns3, type1.ContainingNamespace);
            Assert.True(type1.IsDefinition);
            Assert.False(type1.IsNamespace);
            Assert.True(type1.IsType);
            Assert.False(type1.IsReferenceType);
            Assert.True(type1.IsValueType);

            Assert.Equal(SymbolKind.NamedType, type1.Kind);
            Assert.Equal(TypeKind.Struct, type1.TypeKind);
            Assert.Equal(Accessibility.Public, type1.DeclaredAccessibility);
            Assert.False(type1.IsStatic);
            Assert.False(type1.IsAbstract);
            Assert.True(type1.IsSealed);
            Assert.False(type1.IsVirtual);
            Assert.False(type1.IsOverride);

            // 4 method + 1 synthesized ctor, 2 get_<Prop> method, 2 Properties, 2 fields
            Assert.Equal(11, type1.GetMembers().Length);
            Assert.Equal(0, type1.GetTypeMembers().Length);
            Assert.Equal(0, type1.Interfaces.Length);

            var fullName = "System.Runtime.Serialization.StreamingContext";
            // Internal Assert.Equal(fullName, class1.GetFullNameWithoutGenericArgs());
            Assert.Equal(fullName, type1.ToTestDisplayString());
            Assert.Equal(0, type1.TypeArguments.Length);
            Assert.Equal(0, type1.TypeParameters.Length);

            Assert.Empty(compilation.GetDeclarationDiagnostics());
        }

        [Fact]
        public void MetadataArrayTypeSymbol01()
        {
            var text = "public class A {}";
            var compilation = CreateCompilationWithMscorlib(text, compOptions: TestOptions.DllAlwaysImportInternals);

            var mscorlib = compilation.ExternalReferences[0];
            var mscorNS = compilation.GetReferencedAssemblySymbol(mscorlib);
            Assert.Equal("mscorlib", mscorNS.Name);
            var ns1 = mscorNS.GlobalNamespace.GetMembers("System").Single() as NamespaceSymbol;
            var ns2 = ns1.GetMembers("Diagnostics").Single() as NamespaceSymbol;
            var ns3 = ns2.GetMembers("Eventing").Single() as NamespaceSymbol;

            var type1 = ns3.GetTypeMembers("EventProviderBase").Single() as NamedTypeSymbol;
            // EventData[]
            var type2 = (type1.GetMembers("m_eventData").Single() as FieldSymbol).Type as ArrayTypeSymbol;
            var member2 = type1.GetMembers("WriteTransferEventHelper").Single() as MethodSymbol;
            Assert.Equal(3, member2.Parameters.Length);
            // params object[]
            var type3 = (member2.Parameters[2] as ParameterSymbol).Type as ArrayTypeSymbol;

            Assert.Equal(SymbolKind.ArrayType, type2.Kind);
            Assert.Equal(SymbolKind.ArrayType, type3.Kind);
            Assert.Equal(Accessibility.NotApplicable, type2.DeclaredAccessibility);
            Assert.Equal(Accessibility.NotApplicable, type3.DeclaredAccessibility);

            Assert.Equal(1, type2.Rank);
            Assert.Equal(1, type3.Rank);
            Assert.Equal(TypeKind.ArrayType, type2.TypeKind);
            Assert.Equal(TypeKind.ArrayType, type3.TypeKind);

            Assert.Equal("EventData", type2.ElementType.Name);
            Assert.Equal("Array", type2.BaseType.Name);
            Assert.Equal("Object", type3.ElementType.Name);
            Assert.Equal("System.Diagnostics.Eventing.EventProviderBase.EventData[]", type2.ToTestDisplayString());
            Assert.Equal("System.Object[]", type3.ToTestDisplayString());

            Assert.Equal(1, type2.Interfaces.Length);
            Assert.Equal(1, type3.Interfaces.Length);
            // bug
            // Assert.False(type2.IsDefinition);
            Assert.False(type2.IsNamespace);
            Assert.True(type3.IsType);

            Assert.True(type2.IsReferenceType);
            Assert.True(type2.ElementType.IsValueType);
            Assert.True(type3.IsReferenceType);
            Assert.False(type3.IsValueType);

            Assert.False(type2.IsStatic);
            Assert.False(type2.IsAbstract);
            Assert.False(type2.IsSealed);
            Assert.False(type3.IsVirtual);
            Assert.False(type3.IsOverride);

            Assert.Equal(0, type2.GetMembers().Length);
            Assert.Equal(0, type3.GetMembers(String.Empty).Length);

            Assert.Equal(0, type3.GetTypeMembers().Length);
            Assert.Equal(0, type2.GetTypeMembers(String.Empty).Length);
            Assert.Equal(0, type3.GetTypeMembers(String.Empty, 0).Length);

            Assert.Empty(compilation.GetDeclarationDiagnostics());
        }

        [Fact, WorkItem(531619), WorkItem(531619)]
        public void InheritFromNetModuleMetadata01()
        {
            var modRef = TestReferences.MetadataTests.NetModule01.ModuleCS00;

            var text1 = @"
class Test : StaticModClass
{";
            var text2 = @"
    public static int Main()
    {
        r";

            var tree = SyntaxFactory.ParseSyntaxTree(String.Empty);
            var comp = CreateCompilationWithMscorlib(syntaxTree: tree, references: new[] { modRef });

            var currComp = comp;

            var oldTree = comp.SyntaxTrees.First();
            var oldIText = oldTree.GetText();
            var span = new TextSpan(oldIText.Length, 0);
            var change = new TextChange(span, text1);

            var newIText = oldIText.WithChanges(change);
            var newTree = oldTree.WithChangedText(newIText);
            currComp = currComp.ReplaceSyntaxTree(oldTree, newTree);

            var model = currComp.GetSemanticModel(newTree);
            var id = newTree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Where(s => s.ToString() == "StaticModClass").First();
            // NRE is thrown later but this one has to be called first
            var symInfo = model.GetSymbolInfo(id);
            Assert.NotNull(symInfo.Symbol);

            oldTree = newTree;
            oldIText = oldTree.GetText();
            span = new TextSpan(oldIText.Length, 0);
            change = new TextChange(span, text2);

            newIText = oldIText.WithChanges(change);
            newTree = oldTree.WithChangedText(newIText);
            currComp = currComp.ReplaceSyntaxTree(oldTree, newTree);

            model = currComp.GetSemanticModel(newTree);
            id = newTree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Where(s => s.ToString() == "StaticModClass").First();
            symInfo = model.GetSymbolInfo(id);
            Assert.NotNull(symInfo.Symbol);
        }
    }
}