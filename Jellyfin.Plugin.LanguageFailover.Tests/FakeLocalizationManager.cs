using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;

namespace Jellyfin.Plugin.LanguageFailover.Tests;

/// <summary>
/// Minimal <see cref="ILocalizationManager"/> backed by a small ISO 639 table.
/// Only <see cref="FindLanguageInfo"/> is exercised by the selection logic; every
/// other member throws so an accidental new dependency shows up as a test failure.
/// </summary>
public sealed class FakeLocalizationManager : ILocalizationManager
{
    private static readonly CultureDto[] Cultures =
    {
        Culture("English", "en", "eng"),
        Culture("French", "fr", "fre", "fra"),
        Culture("German", "de", "ger", "deu"),
        Culture("Spanish", "es", "spa"),
        Culture("Japanese", "ja", "jpn"),
        Culture("Korean", "ko", "kor"),
        Culture("Chinese", "zh", "chi", "zho"),
    };

    public CultureDto? FindLanguageInfo(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return null;
        }

        return Cultures.FirstOrDefault(
            c => string.Equals(c.TwoLetterISOLanguageName, language, StringComparison.OrdinalIgnoreCase)
                 || c.ThreeLetterISOLanguageNames.Contains(language, StringComparer.OrdinalIgnoreCase));
    }

    public IEnumerable<CultureDto> GetCultures() => Cultures;

    public IReadOnlyList<CountryInfo> GetCountries() => throw new NotSupportedException();

    public IReadOnlyList<ParentalRating> GetParentalRatings() => throw new NotSupportedException();

    public ParentalRatingScore? GetRatingScore(string rating, string? countryCode = null) => throw new NotSupportedException();

    public string GetLocalizedString(string phrase) => throw new NotSupportedException();

    public string GetLocalizedString(string phrase, string culture) => throw new NotSupportedException();

    public IEnumerable<LocalizationOption> GetLocalizationOptions() => throw new NotSupportedException();

    public bool TryGetISO6392TFromB(string isoB, [NotNullWhen(true)] out string? isoT) => throw new NotSupportedException();

    private static CultureDto Culture(string name, string twoLetter, params string[] threeLetter)
        => new(name, name, twoLetter, threeLetter);
}
