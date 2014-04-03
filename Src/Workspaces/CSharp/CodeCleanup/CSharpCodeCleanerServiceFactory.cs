﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.LanguageServices;

namespace Microsoft.CodeAnalysis.CSharp.CodeCleanup
{
#if MEF
    using Microsoft.CodeAnalysis.CodeCleanup;

    [ExportLanguageServiceFactory(typeof(ICodeCleanerService), LanguageNames.CSharp)]
#endif
    internal class CSharpCodeCleanerServiceFactory : ILanguageServiceFactory
    {
        public ILanguageService CreateLanguageService(ILanguageServiceProvider provider)
        {
            return new CSharpCodeCleanerService();
        }
    }
}