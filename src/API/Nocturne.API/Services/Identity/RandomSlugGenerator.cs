namespace Nocturne.API.Services.Identity;

/// <summary>
/// Generates memorable random English adjective-noun slugs (e.g. "swift-fox", "brave-eagle") without numbers.
/// </summary>
public static class RandomSlugGenerator
{
    public static readonly string[] Adjectives =
    [
        "agile", "alert", "amber", "arctic", "astute", "autumn", "bold", "brave", "bright", "brisk",
        "calm", "clever", "cosmic", "cozy", "crystal", "daring", "dawn", "eager", "early", "ember",
        "fair", "fast", "fearless", "fiery", "fine", "flash", "fleet", "flying", "forest", "free",
        "friendly", "frosty", "gentle", "glad", "golden", "graceful", "grand", "green", "happy", "hardy",
        "hearty", "heroic", "honest", "hopeful", "humble", "infinite", "jolly", "joyful", "keen", "kind",
        "lively", "lucky", "lunar", "magic", "merry", "mighty", "morning", "mystic", "nimble", "noble",
        "ocean", "polar", "proud", "pure", "quick", "quiet", "radiant", "rapid", "river", "robust",
        "ruby", "serene", "shining", "silent", "silver", "solar", "spring", "stellar", "strong", "sunny",
        "swift", "tidy", "true", "trusty", "valiant", "vibrant", "warm", "wild", "wise", "worthy"
    ];

    public static readonly string[] Nouns =
    [
        "antelope", "badger", "bear", "beaver", "bison", "buffalo", "cheetah", "civet", "condor", "cougar",
        "coyote", "crane", "deer", "dingo", "dolphin", "dove", "eagle", "elk", "falcon", "ferret",
        "finch", "fox", "gazelle", "gecko", "giraffe", "hawk", "heron", "husky", "ibex", "iguana",
        "jaguar", "koala", "lark", "lemur", "leopard", "lion", "lynx", "magpie", "marten", "meerkat",
        "mink", "moose", "ocelot", "orca", "osprey", "otter", "owl", "panda", "panther", "pelican",
        "penguin", "phoenix", "plover", "puma", "quail", "rabbit", "raccoon", "raven", "robin", "sable",
        "salmon", "seal", "serval", "shark", "sparrow", "starling", "stork", "swan", "swift", "tiger",
        "toucan", "trout", "turtle", "viper", "walrus", "whale", "wolf", "wombat", "zebra"
    ];

    /// <summary>
    /// Generates a random slug combination (e.g. "swift-fox") and its display name ("Swift Fox").
    /// No numbers are appended.
    /// </summary>
    public static (string Slug, string DisplayName) Generate()
    {
        var adj = Adjectives[Random.Shared.Next(Adjectives.Length)];
        var noun = Nouns[Random.Shared.Next(Nouns.Length)];

        var slug = $"{adj}-{noun}";
        var displayName = $"{char.ToUpperInvariant(adj[0])}{adj[1..]} {char.ToUpperInvariant(noun[0])}{noun[1..]}";

        return (slug, displayName);
    }
}

