using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var id = ctx.Request.Headers[HeaderName];
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("n");

        ctx.Items[HeaderName] = id.ToString();
        ctx.Response.Headers[HeaderName] = id.ToString();

        await _next(ctx);
    }
}

public static class HttpContextExt
{
    public static string? GetCorrelationId(this HttpContext ctx)
        => ctx?.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
}