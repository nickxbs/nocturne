using FluentAssertions;
using Nocturne.API.Services.Identity;
using Xunit;

namespace Nocturne.API.Tests.Services.Identity;

[Trait("Category", "Unit")]
public class RandomSlugGeneratorTests
{
    [Fact]
    public void Generate_ProducesValidSlugWithoutNumbers()
    {
        for (var i = 0; i < 50; i++)
        {
            var (slug, displayName) = RandomSlugGenerator.Generate();

            slug.Should().NotBeNullOrWhiteSpace();
            slug.Should().MatchRegex(@"^[a-z]+-[a-z]+$", "slug must consist of two lowercase English words separated by a single hyphen without numbers");

            displayName.Should().NotBeNullOrWhiteSpace();
            displayName.Should().MatchRegex(@"^[A-Z][a-z]+ [A-Z][a-z]+$", "display name must consist of two capitalized words separated by a space");

            var parts = slug.Split('-');
            parts.Should().HaveCount(2);
            RandomSlugGenerator.Adjectives.Should().Contain(parts[0]);
            RandomSlugGenerator.Nouns.Should().Contain(parts[1]);
        }
    }
}

