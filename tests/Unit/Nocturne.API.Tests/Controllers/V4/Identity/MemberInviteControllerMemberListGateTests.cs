using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Nocturne.API.Controllers.V4.Identity;
using Nocturne.Core.Models.Authorization;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V4.Identity;

[Trait("Category", "Unit")]
public class MemberInviteControllerMemberListGateTests
{
    [Fact]
    public void GetMembers_RefusesAnAlertsReadOnlyCaller()
    {
        Authorize(new HashSet<string> { Scope.AlertsRead }).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void GetMembers_RefusesAGuestLinkCaller()
    {
        var guestScopes = Scope.Normalize(
            Scope.ValidateGrantScopes(Scope.AllowedGuestScopes, OAuthGrantTypes.Guest));

        guestScopes.Should().Contain(Scope.AlertsRead);
        Authorize(guestScopes).Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData(Scope.MembersInvite)]
    [InlineData(Scope.MembersManage)]
    [InlineData(Scope.SharingManage)]
    [InlineData(Scope.FullAccess)]
    public void GetMembers_AdmitsACallerWhoCanAdministerMembersOrSharing(string scope)
    {
        Authorize(new HashSet<string> { scope }).Should().BeNull();
    }

    private static IActionResult? Authorize(IReadOnlySet<string> grantedScopes)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["AuthContext"] = new AuthContext { IsAuthenticated = true };
        httpContext.Items["GrantedScopes"] = grantedScopes;

        var context = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), []);

        typeof(MemberInviteController).GetMethod(nameof(MemberInviteController.GetMembers))!
            .GetCustomAttributes(inherit: true)
            .OfType<IAuthorizationFilter>()
            .ToList()
            .ForEach(filter =>
            {
                if (context.Result is null)
                    filter.OnAuthorization(context);
            });

        return context.Result;
    }
}
