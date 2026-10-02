using System.Net;
using System.Net.Http.Json;
using DevForge.Application.Applications;
using DevForge.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DevForge.IntegrationTests.Api;

public class ApplicationsApiTests(DatabaseFixture database) : ApiTestBase(database)
{
    [Fact]
    public async Task Create_returns_201_with_the_application_and_its_location()
    {
        var response = await Client.PostAsJsonAsync("/api/applications", NewApplication("Create Test"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ApplicationDto>();
        Assert.Equal("Create Test", created.Name);
        Assert.Equal("https://github.com/vijay/todo-api", created.RepositoryUrl);
        Assert.Equal("github.com/vijay/todo-api", created.RepositoryDisplayName);
        Assert.Equal(".NET 10", created.RuntimeDisplayName);
        Assert.Equal(ApplicationStatus.NeverDeployed, created.Status);
        Assert.Null(created.LastDeployment);
        Assert.EndsWith($"/api/applications/{created.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Get_returns_the_application()
    {
        var created = await CreateApplicationAsync("Get Test");

        var fetched = await Client.GetFromJsonAsync<ApplicationDto>($"/api/applications/{created.Id}", ApiJson.Options);

        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("Get Test", fetched.Name);
    }

    [Fact]
    public async Task Get_returns_404_problem_details_for_an_unknown_id()
    {
        var response = await Client.GetAsync($"/api/applications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.ReadAsync<ProblemDetails>();
        Assert.Equal(404, problem.Status);
        Assert.Contains("was not found", problem.Detail);
    }

    [Fact]
    public async Task List_includes_created_applications()
    {
        var created = await CreateApplicationAsync("List Test");

        var applications = await Client.GetFromJsonAsync<List<ApplicationDto>>("/api/applications", ApiJson.Options);

        Assert.Contains(applications!, application => application.Id == created.Id);
    }

    [Fact]
    public async Task Create_returns_400_with_field_errors_for_invalid_input()
    {
        var response = await Client.PostAsJsonAsync("/api/applications", new
        {
            name = "",
            repositoryUrl = "github.com/vijay/todo-api",
            branch = "main",
            runtime = "dotnet-10",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.Contains("name", problem.Errors.Keys);
    }

    [Theory]
    [InlineData("ftp://example.com/repo", "main", "dotnet-10", "repositoryUrl")]
    [InlineData("github.com/a/b", "bad branch", "dotnet-10", "branch")]
    [InlineData("github.com/a/b", "main", "cobol", "runtime")]
    public async Task Create_returns_400_when_a_domain_rule_is_violated(string repositoryUrl, string branch, string runtime, string field)
    {
        var response = await Client.PostAsJsonAsync("/api/applications", new { name = $"Invalid {field}", repositoryUrl, branch, runtime });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.Contains(field, problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_returns_409_when_the_name_is_taken()
    {
        await CreateApplicationAsync("Duplicate Test");

        var response = await Client.PostAsJsonAsync("/api/applications", NewApplication("Duplicate Test"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_changes_the_editable_fields()
    {
        var created = await CreateApplicationAsync("Update Test");

        var response = await Client.PutAsJsonAsync($"/api/applications/{created.Id}", new
        {
            name = "Update Test Renamed",
            repositoryUrl = "https://gitlab.com/vijay/renamed",
            branch = "develop",
            description = "Updated",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<ApplicationDto>();
        Assert.Equal("Update Test Renamed", updated.Name);
        Assert.Equal("https://gitlab.com/vijay/renamed", updated.RepositoryUrl);
        Assert.Equal("develop", updated.Branch);
        Assert.Equal("Updated", updated.Description);
        Assert.Equal(created.Runtime, updated.Runtime);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);
    }

    [Fact]
    public async Task Update_returns_404_for_an_unknown_id()
    {
        var response = await Client.PutAsJsonAsync($"/api/applications/{Guid.NewGuid()}", new
        {
            name = "Nobody",
            repositoryUrl = "github.com/a/b",
            branch = "main",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_application()
    {
        var created = await CreateApplicationAsync("Delete Test");

        var response = await Client.DeleteAsync($"/api/applications/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/applications/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_is_refused_while_a_deployment_is_in_progress()
    {
        var created = await CreateApplicationAsync("Delete Busy Test");
        await CreateDeploymentAsync(created.Id);

        var response = await Client.DeleteAsync($"/api/applications/{created.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/applications/{created.Id}")).StatusCode);
    }
}
