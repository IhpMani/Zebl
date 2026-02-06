using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Linq;
using System.Reflection;

namespace Zebl.Api.Configuration;

/// <summary>
/// Swagger operation filter to handle file uploads for HL7 import endpoint
/// Explicitly defines multipart/form-data with binary file field for IFormFile parameters with [FromForm]
/// </summary>
public class SwaggerFileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        try
        {
            // Check if the method parameter is IFormFile with [FromForm] attribute
            var parameters = context.MethodInfo?.GetParameters();
            if (parameters == null || parameters.Length == 0)
            {
                return;
            }

            var fileParameter = parameters.FirstOrDefault(p => 
                p != null &&
                p.ParameterType == typeof(IFormFile) && 
                p.GetCustomAttribute<FromFormAttribute>() != null);

            if (fileParameter != null)
            {
                var parameterName = fileParameter.Name ?? "file";

                // Clear any existing request body and create new one
                operation.RequestBody = new OpenApiRequestBody
                {
                    Required = true,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["multipart/form-data"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, OpenApiSchema>
                                {
                                    [parameterName] = new OpenApiSchema
                                    {
                                        Type = "string",
                                        Format = "binary",
                                        Description = "The HL7 file to import (.hl7 extension)"
                                    }
                                },
                                Required = new HashSet<string> { parameterName }
                            }
                        }
                    }
                };

                // Remove the file parameter from parameters list since it's now in the request body
                if (operation.Parameters != null)
                {
                    operation.Parameters = operation.Parameters
                        .Where(p => p != null && p.Name != parameterName)
                        .ToList();
                }
            }
        }
        catch
        {
            // Silently fail - don't break Swagger generation if filter has issues
            // This allows Swagger to continue generating for other endpoints
        }
    }
}
