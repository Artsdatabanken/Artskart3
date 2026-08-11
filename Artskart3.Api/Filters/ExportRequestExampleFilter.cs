using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Artskart3.Api.Filters;

/// <summary>
/// Setter eksempelverdier for eksport-endepunktene (summary og start) slik at
/// Swagger "Try it out" gir et brukbart request-body uten manuell redigering.
/// </summary>
public class ExportRequestExampleFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var actionName = context.MethodInfo.Name;
        if (actionName is not ("GetSummary" or "StartExport"))
            return;

        var content = operation.RequestBody?.Content;
        if (content is not null && content.TryGetValue("application/json", out var mediaType))
        {
            mediaType.Examples = new Dictionary<string, IOpenApiExample>
            {
                ["Tom filter - overstiger maks antall rader"] = new OpenApiExample
                {
                    Summary = "Tom filter - overstiger maks antall rader",
                    Value = new JsonObject
                    {
                        ["name"] = null,
                        ["filter"] = new JsonObject(),
                        ["selectedColumns"] = new JsonArray()
                    }
                },
                ["Kong Karls Land (Svalbard)"] = new OpenApiExample
                {
                    Summary = "Kong Karls Land (Svalbard)",
                    Value = new JsonObject
                    {
                        ["name"] = "Kong Karls Land",
                        ["filter"] = new JsonObject
                        {
                            ["municipalityIds"] = new JsonArray("2104")
                        },
                        ["selectedColumns"] = new JsonArray()
                    }
                }
            };
        }
    }
}
