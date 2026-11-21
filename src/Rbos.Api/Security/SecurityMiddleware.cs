namespace Rbos.Api.Security;

/// <summary>Basic security headers, request-size limit, JSON content-type enforcement and no-store for API responses.</summary>
public sealed class SecurityMiddleware(RequestDelegate next)
{
    public const long MaxBodyBytes = 64 * 1024;
    // Preserve the existing 12 MiB JSON request cap. Multipart needs a slightly larger
    // envelope for boundaries/headers while keeping the actual file bytes capped at 12 MiB.
    public const long MaxInvoiceBodyBytes = 12L * 1024 * 1024;
    public const long MaxInvoiceMultipartBodyBytes = 13L * 1024 * 1024;
    /// <summary>Catalogue import carries a 12 MB file as base64 inside JSON (4/3 overhead) plus a small envelope.</summary>
    public const long MaxCatalogueImportBodyBytes = 12L * 1024 * 1024 / 3 * 4 + 16 * 1024;
    public const long MaxItemTagPhotoBodyBytes = 8L * 1024 * 1024;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Permissions-Policy"] = "camera=(self), microphone=(self), geolocation=()";
        h["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            h["Cache-Control"] = "no-store";
            h["Pragma"] = "no-cache";

            if (!HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method))
            {
                var isItemTagPhotoUpload = HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Path.Equals("/api/sales/item-tag-scan");
                var isInvoiceExtraction = HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Path.Equals("/api/purchases/extract");
                var contentType = ctx.Request.ContentType?.Split(';')[0].Trim();
                var isMultipartInvoiceUpload = isInvoiceExtraction
                    && string.Equals(contentType, "multipart/form-data", StringComparison.OrdinalIgnoreCase);
                var limit = isItemTagPhotoUpload ? MaxItemTagPhotoBodyBytes
                    : isMultipartInvoiceUpload ? MaxInvoiceMultipartBodyBytes
                    : isInvoiceExtraction ? MaxInvoiceBodyBytes
                    : ctx.Request.Path.Equals("/api/products/import/read") ? MaxCatalogueImportBodyBytes
                    : MaxBodyBytes;
                if (ctx.Request.ContentLength > limit)
                {
                    ctx.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    await ctx.Response.WriteAsJsonAsync(new { error = ctx.Request.Path.Equals("/api/products/import/read") ? "This file is larger than the 12 MB limit. Split the PDF into smaller parts or compress it, then upload again." : "Request is too large." });
                    return;
                }
                var feature = ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
                if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = limit;

                var hasBody = ctx.Request.ContentLength is > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding");
                if (hasBody && isItemTagPhotoUpload)
                {
                    var imageContentType = ctx.Request.ContentType?.Split(';')[0].Trim();
                    if (!string.Equals(imageContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(imageContentType, "image/png", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(imageContentType, "image/webp", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                        await ctx.Response.WriteAsJsonAsync(new { error = "Choose a JPEG, PNG or WebP photo." });
                        return;
                    }
                }
                else if (hasBody && !ctx.Request.HasJsonContentType() && !isMultipartInvoiceUpload)
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
