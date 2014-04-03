﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.WorkspaceServices;

namespace Microsoft.CodeAnalysis.UnitTests
{
    [ExportWorkspaceServiceFactory(typeof(ITextCacheService), "NotKeptAlive")]
    internal class NotKeptAliveTextCacheServiceFactory : IWorkspaceServiceFactory
    {
        public IWorkspaceService CreateService(IWorkspaceServiceProvider workspaceServices)
        {
            return new Cache();
        }

        private class Cache : ITextCacheService
        {
            public void AddOrAccess(TextAndVersion instance, IWeakAction<TextAndVersion> evictor)
            {
                evictor.Invoke(instance);
            }

            public void Clear()
            {
            }
        }
    }
}