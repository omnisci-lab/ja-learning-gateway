using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Japanese.Gateway;

public class SwaggerAggregator : IDocumentFilter
{
    private readonly List<SwaggerUrl> _swaggerUrls;

    public SwaggerAggregator(List<SwaggerUrl> swaggerUrls)
    {
        _swaggerUrls = swaggerUrls;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        using HttpClient httpClient = new HttpClient();
        foreach (SwaggerUrl swaggerUrl in _swaggerUrls)
        {
            HttpResponseMessage response = httpClient.GetAsync(swaggerUrl.Url).Result;
            if (!response.IsSuccessStatusCode) 
                continue;

            using Stream stream = response.Content.ReadAsStreamAsync().Result;
            OpenApiDocument openApiDoc = new OpenApiStreamReader().Read(stream, out var diagnostic);

            if (openApiDoc?.Paths == null) 
                continue;

            foreach (var path in openApiDoc.Paths)
            {
                if (!swaggerDoc.Paths.ContainsKey(path.Key))
                {
                    string lastSegment = path.Key.Replace("/api/", "");
                    string newPathKey = $"{swaggerUrl.PrefixPath}/{lastSegment}";
                    swaggerDoc.Paths.Add(newPathKey, path.Value);
                }
            }

            foreach (var schema in openApiDoc.Components.Schemas)
            {
                if (!swaggerDoc.Components.Schemas.ContainsKey(schema.Key))
                {
                    swaggerDoc.Components.Schemas.Add(schema.Key, schema.Value);
                }
            }
        }
    }
}