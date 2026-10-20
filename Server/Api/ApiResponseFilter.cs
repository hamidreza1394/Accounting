namespace Server.Api;

/// <summary>
/// Cross-cutting behaviour of every API endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Responses contain company data, so they are never stored by a browser or a proxy
/// (<c>Cache-Control: no-store</c>).</item>
/// <item>An unexpected exception is logged with its details on the server and answered with
/// a generic problem response: the client never receives a stack trace, a SQL message or a
/// connection string, and the error is never turned silently into an empty result.</item>
/// </list>
/// </remarks>
public sealed class ApiResponseFilter : IEndpointFilter
{
	public ApiResponseFilter(ILogger<ApiResponseFilter> logger)
	{
		Logger = logger;
	}

	private ILogger<ApiResponseFilter> Logger { get; }

	public async ValueTask<object?> InvokeAsync(
		EndpointFilterInvocationContext context,
		EndpointFilterDelegate next)
	{
		var httpContext = context.HttpContext;

		httpContext.Response.Headers.CacheControl = "no-store";

		try
		{
			return await next(context);
		}
		catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
		{
			// The client went away; there is nobody to answer.
			throw;
		}
		catch (Exception exception)
		{
			Logger.LogError(
				exception,
				"API request {Method} {Path} failed with an unexpected error.",
				httpContext.Request.Method,
				httpContext.Request.Path.Value);

			return TypedResults.Problem(
				title: "خطای غیرمنتظره‌ای در سرور رخ داد. لطفاً دوباره تلاش کنید.",
				statusCode: StatusCodes.Status500InternalServerError);
		}
	}
}
