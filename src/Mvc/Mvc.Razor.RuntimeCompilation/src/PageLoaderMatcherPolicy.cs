// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;

internal class PageLoaderMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    private readonly PageLoader _loader;

    public PageLoaderMatcherPolicy(PageLoader loader)
    {
        if (loader == null)
        {
            throw new ArgumentNullException(nameof(loader));
        }

        _loader = loader;
    }

    public override int Order => int.MinValue + 100;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        // We don't mark Pages as dynamic endpoints because that causes all matcher policies
        // to run in *slow mode*. Instead we produce the same metadata for things that would affect matcher
        // policies on both endpoints (uncompiled and compiled).
        //
        // This means that something like putting [Consumes] on a page wouldn't work. We've never said that it would.
        for (var i = 0; i < endpoints.Count; i++)
        {
            var page = endpoints[i].Metadata.GetMetadata<PageActionDescriptor>();
            if (page is not null and not CompiledPageActionDescriptor)
            {
                // Found an uncompiled page
                return true;
            }
        }

        return false;
    }

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }

        if (candidates == null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
            {
                continue;
            }

            ref var candidate = ref candidates[i];
            var endpoint = candidate.Endpoint;

            var page = endpoint.Metadata.GetMetadata<PageActionDescriptor>();
            if (page != null)
            {
                // We found an endpoint instance that has a PageActionDescriptor, but not a
                // CompiledPageActionDescriptor. Update the CandidateSet.
                var compiled = _loader.LoadAsync(page, endpoint.Metadata);

                if (compiled.IsCompletedSuccessfully)
                {
                    candidates.ReplaceEndpoint(i, compiled.Result.Endpoint, candidate.Values);
                }
                else
                {
                    // In the most common case, GetOrAddAsync will return a synchronous result.
                    // Avoid going async since this is a fairly hot path.
                    return ApplyAsyncAwaited(candidates, compiled, i);
                }
            }
        }

        return Task.CompletedTask;
    }

    private async Task ApplyAsyncAwaited(CandidateSet candidates, Task<CompiledPageActionDescriptor> actionDescriptorTask, int index)
    {
        var compiled = await actionDescriptorTask;

        candidates.ReplaceEndpoint(index, compiled.Endpoint, candidates[index].Values);

        for (var i = index + 1; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
            {
                continue;
            }

            var candidate = candidates[i];
            var endpoint = candidate.Endpoint;

            var page = endpoint.Metadata.GetMetadata<PageActionDescriptor>();
            if (page != null)
            {
                compiled = await _loader.LoadAsync(page, endpoint.Metadata);

                candidates.ReplaceEndpoint(i, compiled.Endpoint, candidates[i].Values);
            }
        }
    }
}
