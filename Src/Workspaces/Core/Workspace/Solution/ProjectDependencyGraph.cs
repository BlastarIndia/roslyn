﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// A <see cref="ProjectDependencyGraph"/> models the dependencies between projects in a solution.
    /// </summary>
    public class ProjectDependencyGraph
    {
        private readonly ImmutableList<ProjectId> projectIds;
        private readonly ImmutableDictionary<ProjectId, ImmutableHashSet<ProjectId>> referencesMap;

        // guards lazy computed data
        private readonly NonReentrantLock dataLock = new NonReentrantLock();

        // these are computed fully on demand
        private ImmutableDictionary<ProjectId, ImmutableHashSet<ProjectId>> lazyReverseReferencesMap;
        private ImmutableList<ProjectId> lazyTopologicallySortedProjects;
        private ImmutableList<IEnumerable<ProjectId>> lazyDependencySets;

        // these accumulate results on demand
        private ImmutableDictionary<ProjectId, ImmutableHashSet<ProjectId>> transitiveReferencesMap = ImmutableDictionary<ProjectId, ImmutableHashSet<ProjectId>>.Empty;
        private ImmutableDictionary<ProjectId, ImmutableHashSet<ProjectId>> reverseTransitiveReferencesMap = ImmutableDictionary<ProjectId, ImmutableHashSet<ProjectId>>.Empty;

        internal static readonly ProjectDependencyGraph Empty = new ProjectDependencyGraph(
            ImmutableList<ProjectId>.Empty,
            ImmutableDictionary<ProjectId, ImmutableHashSet<ProjectId>>.Empty);

        internal ProjectDependencyGraph(
            ImmutableList<ProjectId> projectIds,
            ImmutableDictionary<ProjectId, ImmutableHashSet<ProjectId>> referencesMap)
        {
            this.projectIds = projectIds;
            this.referencesMap = referencesMap;
        }

        /// <summary>
        /// Gets the list of projects (topologically sorted) that this project directly depends on.
        /// </summary>
        public IImmutableSet<ProjectId> GetProjectsThatThisProjectDirectlyDependsOn(ProjectId projectId)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            ImmutableHashSet<ProjectId> projectIds;
            if (this.referencesMap.TryGetValue(projectId, out projectIds))
            {
                return projectIds;
            }
            else
            {
                return ImmutableHashSet<ProjectId>.Empty;
            }
        }

        /// <summary>
        /// Gets the list of projects (topologically sorted) that directly depend on this project.
        /// </summary> 
        public IImmutableSet<ProjectId> GetProjectsThatDirectlyDependOnThisProject(ProjectId projectId)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (this.lazyReverseReferencesMap == null)
            {
                using (this.dataLock.DisposableWait())
                {
                    return this.GetProjectsThatDirectlyDependOnThisProject_NoLock(projectId);
                }
            }
            else
            {
                // okay, because its only ever going to be computed and assigned once
                return this.GetProjectsThatThisProjectTransitivelyDependsOn_NoLock(projectId);
            }
        }

        private ImmutableHashSet<ProjectId> GetProjectsThatDirectlyDependOnThisProject_NoLock(ProjectId projectId)
        {
            if (this.lazyReverseReferencesMap == null)
            {
                this.lazyReverseReferencesMap = this.ComputeReverseReferencesMap();
            }

            ImmutableHashSet<ProjectId> reverseReferences;
            if (this.lazyReverseReferencesMap.TryGetValue(projectId, out reverseReferences))
            {
                return reverseReferences;
            }
            else
            {
                return ImmutableHashSet<ProjectId>.Empty;
            }
        }

        private ImmutableDictionary<ProjectId, ImmutableHashSet<ProjectId>> ComputeReverseReferencesMap()
        {
            var reverseReferencesMap = new Dictionary<ProjectId, HashSet<ProjectId>>();

            foreach (var kvp in this.referencesMap)
            {
                var references = kvp.Value;
                foreach (var referencedId in references)
                {
                    HashSet<ProjectId> reverseReferences;
                    if (!reverseReferencesMap.TryGetValue(referencedId, out reverseReferences))
                    {
                        reverseReferences = new HashSet<ProjectId>();
                        reverseReferencesMap.Add(referencedId, reverseReferences);
                    }

                    reverseReferences.Add(kvp.Key);
                }
            }

            return reverseReferencesMap
                .Select(kvp => new KeyValuePair<ProjectId, ImmutableHashSet<ProjectId>>(kvp.Key, kvp.Value.ToImmutableHashSet()))
                .ToImmutableDictionary();
        }

        /// <summary>
        /// Gets the list of projects that directly or transitively this project depends on
        /// </summary>
        public IImmutableSet<ProjectId> GetProjectsThatThisProjectTransitivelyDependsOn(ProjectId projectId)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            // first try without lock for speed
            var currentMap = this.transitiveReferencesMap;
            ImmutableHashSet<ProjectId> transitiveReferences;
            if (currentMap.TryGetValue(projectId, out transitiveReferences))
            {
                return transitiveReferences;
            }
            else
            {
                using (this.dataLock.DisposableWait())
                {
                    return GetProjectsThatThisProjectTransitivelyDependsOn_NoLock(projectId);
                }
            }
        }

        private ImmutableHashSet<ProjectId> GetProjectsThatThisProjectTransitivelyDependsOn_NoLock(ProjectId projectId)
        {
            ImmutableHashSet<ProjectId> transitiveReferences;
            if (!this.transitiveReferencesMap.TryGetValue(projectId, out transitiveReferences))
            {
                var results = SharedPools.Default<HashSet<ProjectId>>().AllocateAndClear();
                this.ComputeTransitiveReferences(projectId, results);
                transitiveReferences = results.ToImmutableHashSet();
                this.transitiveReferencesMap = this.transitiveReferencesMap.Add(projectId, transitiveReferences);
                SharedPools.Default<HashSet<ProjectId>>().ClearAndFree(results);
            }

            return transitiveReferences;
        }

        private void ComputeTransitiveReferences(ProjectId project, HashSet<ProjectId> result)
        {
            var otherProjects = this.GetProjectsThatThisProjectDirectlyDependsOn(project);

            foreach (var other in otherProjects)
            {
                if (result.Add(other))
                {
                    ComputeTransitiveReferences(other, result);
                }
            }
        }

        /// <summary>
        /// Gets the list of projects that directly or transitively depend on this project.
        /// </summary>
        public IEnumerable<ProjectId> GetProjectsThatTransitivelyDependOnThisProject(ProjectId projectId)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            // first try without lock for speed
            var currentMap = this.reverseTransitiveReferencesMap;
            ImmutableHashSet<ProjectId> reverseTransitiveReferences;
            if (currentMap.TryGetValue(projectId, out reverseTransitiveReferences))
            {
                return reverseTransitiveReferences;
            }
            else
            {
                using (this.dataLock.DisposableWait())
                {
                    return this.GetProjectsThatTransitivelyDependOnThisProject_NoLock(projectId);
                }
            }
        }

        private ImmutableHashSet<ProjectId> GetProjectsThatTransitivelyDependOnThisProject_NoLock(ProjectId projectId)
        {
            ImmutableHashSet<ProjectId> reverseTransitiveReferences;

            if (!this.reverseTransitiveReferencesMap.TryGetValue(projectId, out reverseTransitiveReferences))
            {
                var results = SharedPools.Default<HashSet<ProjectId>>().AllocateAndClear();
                ComputeReverseTransitiveReferences(projectId, results);

                reverseTransitiveReferences = results.ToImmutableHashSet();
                this.reverseTransitiveReferencesMap.Add(projectId, reverseTransitiveReferences);

                SharedPools.Default<HashSet<ProjectId>>().ClearAndFree(results);
            }

            return reverseTransitiveReferences;
        }

        private void ComputeReverseTransitiveReferences(ProjectId project, HashSet<ProjectId> results)
        {
            var otherProjects = this.GetProjectsThatDirectlyDependOnThisProject_NoLock(project);
            foreach (var other in otherProjects)
            {
                if (results.Add(other))
                {
                    ComputeReverseTransitiveReferences(other, results);
                }
            }
        }

        /// <summary>
        /// Returns all the projects for the solution in a topologically sorted order with respect
        /// to their dependencies. Projects that depend on other projects will always show up later in this sequence
        /// than the projects they depend on.
        /// </summary>
        public IEnumerable<ProjectId> GetTopologicallySortedProjects(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this.lazyTopologicallySortedProjects == null)
            {
                using (this.dataLock.DisposableWait(cancellationToken))
                {
                    this.GetTopologicallySortedProjects_NoLock(cancellationToken);
                }
            }

            return this.lazyTopologicallySortedProjects;
        }

        private IEnumerable<ProjectId> GetTopologicallySortedProjects_NoLock(CancellationToken cancellationToken)
        {
            if (this.lazyTopologicallySortedProjects == null)
            {
                var seenProjects = SharedPools.Default<HashSet<ProjectId>>().AllocateAndClear();
                var resultList = SharedPools.Default<List<ProjectId>>().AllocateAndClear();

                this.TopologicalSort(this.projectIds, cancellationToken, seenProjects, resultList);

                this.lazyTopologicallySortedProjects = resultList.ToImmutableList();

                SharedPools.Default<HashSet<ProjectId>>().ClearAndFree(seenProjects);
                SharedPools.Default<List<ProjectId>>().ClearAndFree(resultList);
            }

            return this.lazyTopologicallySortedProjects;
        }

        private void TopologicalSort(
            IEnumerable<ProjectId> projectIds,
            CancellationToken cancellationToken,
            HashSet<ProjectId> seenProjects,
            List<ProjectId> resultList)
        {
            foreach (var projectId in projectIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Make sure we only ever process a project once.
                if (seenProjects.Add(projectId))
                {
                    // Recurse and add anything this project depends on before adding the project itself.
                    ImmutableHashSet<ProjectId> projectReferenceIds;
                    if (this.referencesMap.TryGetValue(projectId, out projectReferenceIds))
                    {
                        TopologicalSort(projectReferenceIds, cancellationToken, seenProjects, resultList);
                    }

                    resultList.Add(projectId);
                }
            }
        }

        /// <summary>
        /// Returns a sequence of sets, where each set contains items with shared interdependency,
        /// and there is no dependency between sets.
        /// </summary>
        public IEnumerable<IEnumerable<ProjectId>> GetDependencySets(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this.lazyDependencySets == null)
            {
                using (this.dataLock.DisposableWait(cancellationToken))
                {
                    this.GetDependencySets_NoLock(cancellationToken);
                }
            }

            return this.lazyDependencySets;
        }

        private IEnumerable<IEnumerable<ProjectId>> GetDependencySets_NoLock(CancellationToken cancellationToken)
        {
            if (this.lazyDependencySets == null)
            {
                var seenProjects = SharedPools.Default<HashSet<ProjectId>>().AllocateAndClear();
                var results = SharedPools.Default<List<IEnumerable<ProjectId>>>().AllocateAndClear();

                this.ComputeDependencySets(seenProjects, results, cancellationToken);

                this.lazyDependencySets = results.ToImmutableList();

                SharedPools.Default<HashSet<ProjectId>>().ClearAndFree(seenProjects);
                SharedPools.Default<List<IEnumerable<ProjectId>>>().ClearAndFree(results);
            }

            return this.lazyDependencySets;
        }

        private void ComputeDependencySets(HashSet<ProjectId> seenProjects, List<IEnumerable<ProjectId>> results, CancellationToken cancellationToken)
        {
            foreach (var project in this.projectIds)
            {
                if (seenProjects.Add(project))
                {
                    // We've never seen this project before, so we have not yet dealt with any projects
                    // in its dependency set.
                    var dependencySet = SharedPools.Default<HashSet<ProjectId>>().AllocateAndClear();
                    ComputedDependencySet(project, dependencySet);

                    // add all items in the dependency set to seen projects so we don't revisit any of them
                    seenProjects.UnionWith(dependencySet);

                    // now make sure the items within the sets are topologically sorted.
                    var topologicallySeenProjects = SharedPools.Default<HashSet<ProjectId>>().AllocateAndClear();
                    var sortedProjects = SharedPools.Default<List<ProjectId>>().AllocateAndClear();
                    this.TopologicalSort(dependencySet, cancellationToken, topologicallySeenProjects, sortedProjects);

                    results.Add(sortedProjects.ToImmutableList());

                    SharedPools.Default<HashSet<ProjectId>>().ClearAndFree(dependencySet);
                    SharedPools.Default<HashSet<ProjectId>>().ClearAndFree(topologicallySeenProjects);
                    SharedPools.Default<List<ProjectId>>().ClearAndFree(sortedProjects);
                }
            }
        }

        private void ComputedDependencySet(ProjectId project, HashSet<ProjectId> result)
        {
            if (result.Add(project))
            {
                var otherProjects = this.GetProjectsThatDirectlyDependOnThisProject_NoLock(project).Concat(
                                    this.GetProjectsThatThisProjectDirectlyDependsOn(project));

                foreach (var other in otherProjects)
                {
                    ComputedDependencySet(other, result);
                }
            }
        }
    }
}