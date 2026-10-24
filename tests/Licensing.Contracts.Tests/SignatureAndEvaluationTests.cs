using Faranou.Licensing.Contracts;
using Xunit;

namespace Licensing.Contracts.Tests;

/// <summary>
/// Legacy smoke tests retained for graph coverage; primary suite is EnvelopeValidatorTests.
/// </summary>
public sealed class SignatureAndEvaluationTests
{
	[Fact]
	public void ActivationRequest_RejectsInvalidFields()
	{
		var bad = new ActivationRequest(0, "x", "", "", default, new string('a', 500));
		Assert.NotEmpty(StructuralValidation.ValidateActivationRequest(bad));
	}
}
