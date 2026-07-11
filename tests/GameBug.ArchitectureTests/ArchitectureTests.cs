using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace GameBug.ArchitectureTests;

public class ArchitectureTests
{
    private const string DomainNamespace = "GameBug.Domain";
    private const string ApplicationNamespace = "GameBug.Application";
    private const string InfrastructureNamespace = "GameBug.Infrastructure";
    private const string ApiNamespace = "GameBug.Api";
    private const string ContractsNamespace = "GameBug.Contracts";

    [Fact]
    public void Domain_ShouldNotHaveDependencyOnOtherProjects()
    {
        // Arrange
        var assembly = typeof(Domain.SharedKernel.Result).Assembly;

        var otherProjects = new[]
        {
            ApplicationNamespace,
            InfrastructureNamespace,
            ApiNamespace
        };

        // Act
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherProjects)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Contracts_ShouldNotDependOnDomainOrInfrastructure()
    {
        var assembly = typeof(Contracts.BugReports.BugReportResponse).Assembly;

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(DomainNamespace, ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_ShouldNotHaveDependencyOnInfrastructureOrApi()
    {
        // Arrange
        var assembly = typeof(Application.DependencyInjection).Assembly;

        var otherProjects = new[]
        {
            InfrastructureNamespace,
            ApiNamespace
        };

        // Act
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherProjects)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Infrastructure_ShouldNotHaveDependencyOnApi()
    {
        // Arrange
        var assembly = typeof(Infrastructure.DependencyInjection).Assembly;

        var otherProjects = new[]
        {
            ApiNamespace
        };

        // Act
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherProjects)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }
}
