using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API Gateway", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Gateway v1");
        c.SwaggerEndpoint("/swagger/main", "Main API v1");
        c.SwaggerEndpoint("/swagger/identity", "Identity API v1");

        app.MapGet("/swagger/{service}/", async (HttpContext context, string service) =>
        {
            var httpClient = new HttpClient();
            string backendUrl = service switch
            {
                "main" => $"{context.Request.Scheme}://{context.Request.Host}/main/swagger/v1/swagger.json",
                "identity" => $"{context.Request.Scheme}://{context.Request.Host}/identity/swagger/v1/swagger.json",
                _ => null!
            };

            if (backendUrl == null)
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Service not found");
                return;
            }

            try
            {
                string stringResponse = await httpClient.GetStringAsync(backendUrl);
                JObject jobject = JsonConvert.DeserializeObject<JObject>(stringResponse)!;

                foreach (JToken jToken in jobject["paths"]?.ToArray()!)
                {
                    string path = jToken.Path.Replace("paths['", "").Replace("']", "");
                    string lastSegment = path.Replace("/api/", "");
                    string newJson = jToken.ToArray()[0].ToString();
                    
                    jobject["paths"]![$"/{service}/{lastSegment}"] = JToken.Parse(newJson);
                    jobject["paths"]![path] = null;
                }

                byte[] result = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jobject, Formatting.Indented));

                context.Response.ContentType = "application/json";
                context.Response.Headers["Content-Length"] = result.Length.ToString();

                await context.Response.Body.WriteAsync(result, 0, result.Length);
                await context.Response.Body.FlushAsync();
            }
            catch (HttpRequestException)
            {
                context.Response.StatusCode = 502;
                await context.Response.WriteAsync("Unable to fetch service Swagger JSON");
            }
        });
    });
}

app.MapReverseProxy();

app.Run();
