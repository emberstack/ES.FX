using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using static Microsoft.AspNetCore.Http.TypedResults;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.SUT.Endpoints
{
    public class SimpleEndpoint
    {
        public const string RoutePattern = "/simpleEndpoint";
        public static void MapRoutes(IEndpointRouteBuilder builder)
        {
            builder.MapPost(RoutePattern, Handle);
        }

        public static async Task<Results<Ok<Response>, BadRequest<ProblemDetails>>> Handle(
        ILogger<SimpleEndpoint> logger,
        [FromBody] Request request,
        CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return Ok(new Response(request.Name));
        }

        public record Request(string Name);

        public record Response(string Name);

    }
}
