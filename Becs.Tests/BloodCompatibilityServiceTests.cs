using Becs.Services;
using FluentAssertions;

public class BloodCompatibilityServiceTests
{
    private readonly IBloodCompatibilityService _svc = new BloodCompatibilityService();

    [Fact]
    public void APlus_Compatibility_Is_Correct()
    {
        var result = _svc.GetCompatibleTypes("A", "+");

        result.Should().BeEquivalentTo(new[] { "A+", "A-", "O+", "O-" }, 
            opts => opts.WithoutStrictOrdering());
    }

    [Fact]
    public void Alternatives_Order_Prefers_ONeg_First_When_Allowed()
    {
        var result = _svc.GetAlternativesOrderedByRarity("AB", "+");

        result.First().Should().Be("O-"); // given our toy “rarity” rule
        result.Should().Contain("AB+");   // still includes exact match among options
    }
}