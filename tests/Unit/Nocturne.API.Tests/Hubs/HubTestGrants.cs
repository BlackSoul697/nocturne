using Microsoft.AspNetCore.SignalR;
using Nocturne.API.Hubs;

namespace Nocturne.API.Tests.Hubs;

/// <summary>Records a credential on a test connection whose re-check always proves it unchanged.</summary>
internal static class HubTestGrants
{
    public static HubCredentialCheck Unchanged(HubAuthorization authorization) =>
        (_, _) => Task.FromResult<HubAuthorization?>(authorization);

    public static HubAuthorization Grant(HubCallerContext context, HubAuthorization authorization) =>
        HubAuthorizationState.Grant(context, authorization, Unchanged(authorization));
}
