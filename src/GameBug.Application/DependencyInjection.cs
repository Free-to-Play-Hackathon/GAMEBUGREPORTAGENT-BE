using FluentValidation;
using GameBug.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GameBug.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddTransient<Evidence.EvidenceResolver>();
        services.AddTransient<Evidence.EventTimelineBuilder>();
        services.AddTransient<ReproCases.SeverityPolicy>();
        services.AddSingleton<Evaluation.EvaluationIdentityBuilder>();
        services.AddSingleton<Evaluation.DuplicateMetricCalculator>();
        services.AddSingleton<Evaluation.LatencyMetricCalculator>();

        services.AddScoped<Abstractions.Trust.IProvenanceValidator, Trust.MvpProvenanceValidator>();
        services.AddScoped<Abstractions.Trust.IQualityGate, Trust.MvpQualityGate>();

        return services;
    }
}
