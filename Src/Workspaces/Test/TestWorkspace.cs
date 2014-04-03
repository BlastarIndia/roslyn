﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.UnitTests;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.WorkspaceServices;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.UnitTests
{
    internal class TestWorkspace : Workspace
    {
        // Forces serialization of mutation calls. Must take this lock before taking stateLock.
        private readonly NonReentrantLock serializationLock = new NonReentrantLock();

        public TestWorkspace(IWorkspaceServiceProvider workspaceServiceProvider = null)
            : base(workspaceServiceProvider ?? WorkspaceService.GetProvider(new CustomWorkspace()))
        {
        }

        public void AddProject(ProjectId projectId, string projectName, string language = LanguageNames.CSharp)
        {
            using (this.serializationLock.DisposableWait())
            {
                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.AddProject(projectId, projectName, projectName, language));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectAdded, oldSolution, newSolution, projectId);
            }
        }

        public ProjectId AddProject(string projectName, string languageName = LanguageNames.CSharp)
        {
            ProjectId id = ProjectId.CreateNewId(debugName: projectName);
            this.AddProject(id, projectName, languageName);
            return id;
        }

        public T GetService<T>()
            where T : class, IWorkspaceService
        {
            return WorkspaceService.GetService<T>(this);
        }
    }
}
