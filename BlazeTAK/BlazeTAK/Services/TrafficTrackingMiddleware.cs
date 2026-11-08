namespace BlazeTAK.Services;

public class TrafficTrackingMiddleware
{
    private readonly RequestDelegate _next;

    public TrafficTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITrafficTracker trafficTracker)
    {
        var path = context.Request.Path.Value ?? "";

        var isStatic = path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
                       path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                       path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                       path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                       path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
                       path.EndsWith(".map", StringComparison.OrdinalIgnoreCase);

        var user = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : null;
        if (!string.IsNullOrEmpty(user))
        {
            trafficTracker.RecordUserActivity(user);
        }

        await _next(context);

        if (!isStatic)
        {
            trafficTracker.RecordRequest(path, context.Request.Method, context.Response.StatusCode, user);
        }
    }
}
