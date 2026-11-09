namespace Rbos.Api.Security;

/// <summary>Basic security headers, request-size limit, JSON content-type enforcement and no-store for API responses.</summary>
public sealed class SecurityMiddleware(RequestDelegate next)
{
    public const long MaxBodyBytes = 64 * 1024;
    public const long MaxInvoiceBodyBytes = 12 * 1024 * 1024;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()";
        h["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            h["Cache-Control"] = "no-store";
            h["Pragma"] = "no-cache";

            if (!HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method))
            {
                var limit = ctx.Request.Path.Equals("/api/purchases/extract") ? MaxInvoiceBodyBytes : MaxBodyBytes;
                if (ctx.Request.ContentLength > limit)
                {
                    ctx.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    await ctx.Response.WriteAsJsonAsync(new { error = "Request is too large." });
                    return;
                }
                var feature = ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
                if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = limit;

                var hasBody = ctx.Request.ContentLength is > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding");
                if (hasBody && !ctx.Request.HasJsonContentType())
                {
                    ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                    await ctx.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/json." });
                    return;
                }
            }
        }
        await next(ctx);
    }
}
