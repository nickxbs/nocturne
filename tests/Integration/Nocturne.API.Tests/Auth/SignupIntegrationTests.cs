using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nocturne.API.Configuration;
using Nocturne.API.Controllers.V4;
using Nocturne.API.Multitenancy;
using Nocturne.API.Tests.Integration.Infrastructure;
using Nocturne.Connectors.Core.Utilities;
using Nocturne.Core.Contracts.Multitenancy;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.API.Tests.Integration.Auth;

/// <summary>
/// Integration tests for self-service passkey registration and tenant provisioning endpoints.
/// </summary>
[Collection(ApiIntegrationShards.One)]
[Trait("Category", "Integration")]
public class SignupIntegrationTests : ApiIntegrationTestBase
{
    public SignupIntegrationTests(
        ApiIntegrationTestFixture fixture,
        ITestOutputHelper output)
        : base(fixture, output) { }

    [Fact]
    public async Task GetMultitenancyInfo_ApexHost_ReturnsSignupEnabled()
    {
        using var apex = Fixture.CreateApexClient();
        var response = await apex.GetAsync("/api/metadata/multitenancy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        root.GetProperty("signupEnabled").GetBoolean().Should().BeTrue();
        root.GetProperty("subdomainResolution").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ValidateUsername_ApexHost_ValidUsername_ReturnsTrue()
    {
        using var apex = Fixture.CreateApexClient();
        var response = await apex.GetAsync("/api/v4/signup/validate-username?username=validuser");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("isValid").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ValidateUsername_ApexHost_ReservedUsername_ReturnsFalse()
    {
        using var apex = Fixture.CreateApexClient();
        var response = await apex.GetAsync("/api/v4/signup/validate-username?username=admin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("isValid").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task SignupOptions_TenantHost_RefusedWithNotFound()
    {
        // Requests directed at a tenant subdomain rather than the apex are refused
        using var response = await ApiClient.PostAsJsonAsync("/api/v4/signup/options", new SignupOptionsRequest
        {
            Username = "tenantuser",
            DisplayName = "Tenant User",
            TenantSlug = "sometenant",
            TenantDisplayName = "Some Tenant",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SignupOptions_ApexHost_ValidRequest_ReturnsOptionsAndChallenge()
    {
        using var apex = Fixture.CreateApexClient();
        using var response = await apex.PostAsJsonAsync("/api/v4/signup/options", new SignupOptionsRequest
        {
            Username = "apexuser",
            DisplayName = "Apex User",
            TenantSlug = "apextenant",
            TenantDisplayName = "Apex Tenant",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SignupOptionsResponse>();
        result.Should().NotBeNull();
        result!.ChallengeToken.Should().NotBeNullOrWhiteSpace();
        result.Options.Should().NotBeNullOrWhiteSpace();

        using var optionsDoc = JsonDocument.Parse(result.Options);
        optionsDoc.RootElement.TryGetProperty("challenge", out var challenge).Should().BeTrue();
        challenge.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InactiveTenant_WhenActivatedByAdmin_BecomesAccessible()
    {
        using var scope = Fixture.Services.CreateScope();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
        var slug = $"inactive-{Guid.NewGuid():N}"[..18];
        var creatorSubjectId = Fixture.OwnerSubjectId;
        var created = await tenantService.CreateAsync(slug, "Inactive Clinic", creatorSubjectId, isActive: false);

        var baseDomain = Fixture.Services.GetRequiredService<IOptions<BaseDomainOptions>>().Value.BaseDomain;
        using (var tenantClient = AuthTestHelpers.CreateTenantClient(Fixture, slug, baseDomain))
        {
            var inactiveRes = await tenantClient.GetAsync("/api/v1/status");
            inactiveRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        using var adminClient = Fixture.CreateApexClient();
        adminClient.DefaultRequestHeaders.Add("X-Instance-Key", HashUtils.Sha256Hex(ApiIntegrationTestFixture.InstanceKey));
        adminClient.DefaultRequestHeaders.Add("X-Instance-Service", "nocturne-web");

        var activateRes = await adminClient.PostAsync($"/api/v4/admin/tenants/{created.Id}/activate", null);
        activateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var activatedDto = await activateRes.Content.ReadFromJsonAsync<TenantDto>();
        activatedDto.Should().NotBeNull();
        activatedDto!.IsActive.Should().BeTrue();

        using (var tenantClient = AuthTestHelpers.CreateTenantClient(Fixture, slug, baseDomain))
        {
            var activeRes = await tenantClient.GetAsync("/api/v1/status");
            activeRes.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
