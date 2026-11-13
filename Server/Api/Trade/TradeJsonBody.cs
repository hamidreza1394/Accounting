using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Server.Api.Trade;

/// <summary>
/// Shared JSON body reader of the Trade write endpoints (Party, Product): 16 KB in-handler cap, 413/400 problem details.
/// </summary>
internal static class TradeJsonBody
{
	/// <summary>Largest accepted POST/PUT body. Enforced by <see cref="ReadAsync{T}" /> on every host.</summary>
	public const int MaxBodyBytes = 16 * 1024;

	/// <summary>
	/// Reads the JSON body with a hard size cap before anything is parsed or any service is called. Done by hand
	/// because endpoint size-limit metadata is only honoured by Kestrel: this works for a declared Content-Length
	/// and for chunked/unknown-length bodies on every host. It runs after the gate (identity, Content-Type check,
	/// anti-forgery, permission). The gate has already insisted on application/json, so anti-forgery never sees a
	/// form body; an unauthorised caller's JSON body is never read by this method.
	/// </summary>
	public static async Task<(T? Value, IResult? Error)> ReadAsync<T>(HttpContext httpContext)
		where T : class
	{
		var request = httpContext.Request;
		if (request.ContentLength > MaxBodyBytes)
		{
			return (null, TooLarge());
		}

		using var buffer = new MemoryStream();
		var chunk = new byte[4096];
		int read;
		while ((read = await request.Body.ReadAsync(chunk, httpContext.RequestAborted)) > 0)
		{
			if (buffer.Length + read > MaxBodyBytes)
			{
				return (null, TooLarge());
			}

			buffer.Write(chunk, 0, read);
		}

		try
		{
			var options = httpContext.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
			var value = JsonSerializer.Deserialize<T>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), options);
			return value is null ? (null, InvalidBody()) : (value, null);
		}
		catch (JsonException)
		{
			return (null, InvalidBody());
		}
	}

	private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult TooLarge() =>
		TypedResults.Problem(
			title: "حجم درخواست بیش از حد مجاز است.",
			detail: "trade_request_too_large",
			statusCode: StatusCodes.Status413PayloadTooLarge);

	private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult InvalidBody() =>
		TypedResults.Problem(
			title: "بدنهٔ درخواست معتبر نیست.",
			detail: "trade_invalid_request_body",
			statusCode: StatusCodes.Status400BadRequest);

}
