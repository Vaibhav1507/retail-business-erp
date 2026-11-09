using Rbos.Api.Application;

namespace Rbos.Api;

/// <summary>D1: single place that turns a ServiceResult into an HTTP response (ProblemDetails + legacy "error" field).</summary>
public static class ApiResults
{
    public static IResult Reply<T>(ServiceResult<T> r)
    {
        if (r.Error is null) return Results.Ok(r.Value);
        return Fail(r.Status is 404 or 409 ? r.Status : 400, r.Error);
    }

    public static IResult Fail(int status, string? message)
    {
        message ??= "Request failed.";
        var title = status switch { 400 => "Bad request", 401 => "Unauthorized", 403 => "Forbidden", 404 => "Not found", 409 => "Conflict", _ => "Error" };
        return Results.Json(new Dictionary<string, object?>
        {
            ["type"] = "about:blank",
            ["title"] = title,
            ["status"] = status,
            ["detail"] = message,
            ["error"] = message,
        }, statusCode: status, contentType: "application/problem+json");
    }
}
