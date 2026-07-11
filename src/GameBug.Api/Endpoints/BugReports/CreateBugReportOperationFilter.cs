using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameBug.Api.Endpoints.BugReports;

public sealed class CreateBugReportOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.ApiDescription.HttpMethod, HttpMethods.Post, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string relativePath = context.ApiDescription.RelativePath ?? string.Empty;
        bool isCreateReport = string.Equals(relativePath, "api/v1/bug-reports", StringComparison.OrdinalIgnoreCase);
        bool isStartAnalysis = relativePath.StartsWith("api/v1/bug-reports/", StringComparison.OrdinalIgnoreCase) &&
            relativePath.EndsWith("/analyses", StringComparison.OrdinalIgnoreCase);
        bool isHistoricalTicketImport = string.Equals(
            relativePath,
            "api/v1/admin/historical-tickets/import",
            StringComparison.OrdinalIgnoreCase);
        if (!isCreateReport && !isStartAnalysis && !isHistoricalTicketImport)
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Unique key for this request (16-128 characters).",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 16,
                MaxLength = 128
            }
        });

        if (!isCreateReport)
        {
            return;
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new()
                {
                    Schema = context.SchemaGenerator.GenerateSchema(
                        typeof(CreateBugReportOpenApiRequest), context.SchemaRepository)
                }
            }
        };
    }
}

public sealed class CreateBugReportOpenApiRequest
{
    public required string Description { get; init; }
    public string? BuildVersion { get; init; }
    public string? Platform { get; init; }
    public string? Device { get; init; }
    public string? Locale { get; init; }
    public string? SessionReference { get; init; }
    public List<IFormFile>? Attachments { get; init; }
}
