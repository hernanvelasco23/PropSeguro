using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using PropSeguro.Application.Common;

namespace PropSeguro.Infrastructure.Listings;

public sealed class BasicHtmlListingImporter(HttpClient httpClient) : IListingImporter
{
    private static readonly Regex MetaTagRegex = new("<meta\\s+[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ScriptJsonLdRegex = new("<script\\s+[^>]*type=[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    public async Task<ListingImportResult> ImportAsync(string listingUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(listingUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A valid listingUrl is required.", nameof(listingUrl));
        }

        var warnings = new List<string>();
        string html;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", "PropSegura/1.0 (+https://propsegura.local)");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/ld+json;q=0.9,*/*;q=0.8");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                warnings.Add($"No se pudo leer la publicacion. El sitio respondio {(int)response.StatusCode}.");
                warnings.Add("Se intentaron recuperar datos basicos desde el link, pero precio, expensas y metros pueden requerir carga manual.");
                return ResultFromUrlFallback(uri.ToString(), warnings);
            }

            html = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            warnings.Add("No se pudo acceder a la publicacion. El sitio puede estar bloqueando la extraccion o no responder.");
            warnings.Add("Se intentaron recuperar datos basicos desde el link, pero precio, expensas y metros pueden requerir carga manual.");
            return ResultFromUrlFallback(uri.ToString(), warnings);
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            warnings.Add("La publicacion no devolvio HTML util para extraer datos.");
            warnings.Add("Se intentaron recuperar datos basicos desde el link, pero precio, expensas y metros pueden requerir carga manual.");
            return ResultFromUrlFallback(uri.ToString(), warnings);
        }

        var meta = ExtractMetaTags(html);
        var jsonLd = ExtractJsonLd(html, warnings);
        var visibleText = ExtractVisibleText(html);
        var combinedText = string.Join(" ", meta.Values.Concat(jsonLd.TextValues).Concat([visibleText]));

        var title = FirstNonEmpty(
            GetMeta(meta, "og:title"),
            GetMeta(meta, "twitter:title"),
            jsonLd.Name,
            ExtractTitleTag(html));
        var description = FirstNonEmpty(
            GetMeta(meta, "og:description"),
            GetMeta(meta, "description"),
            GetMeta(meta, "twitter:description"),
            jsonLd.Description,
            visibleText.Length > 600 ? visibleText[..600] : visibleText);

        var result = new ListingImportResult(
            title,
            FirstNonNull(jsonLd.PriceUsd, ExtractUsdPrice(combinedText)),
            ExtractExpenses(combinedText),
            FirstNonEmpty(jsonLd.Address, ExtractAddress(combinedText)),
            ExtractNeighborhood(combinedText, title, description),
            ExtractNumber(combinedText, @"(\d+(?:[.,]\d+)?)\s*(?:m2|m²|metros?\s+totales?)"),
            ExtractNumber(combinedText, @"(\d+(?:[.,]\d+)?)\s*(?:m2|m²|metros?)\s+cubiertos"),
            ExtractInt(combinedText, @"(\d+)\s*(?:ambientes?|amb\.?)"),
            ExtractInt(combinedText, @"(\d+)\s*(?:dormitorios?|habitaciones?)"),
            ExtractInt(combinedText, @"(\d+)\s*(?:baños?|banos?)"),
            description,
            ExtractImageUrls(meta, jsonLd.ImageUrls),
            uri.ToString(),
            0,
            warnings);

        var confidence = CalculateConfidence(result);
        if (confidence < 45)
        {
            warnings.Add("La extraccion encontro pocos datos. Revisalos y completalos manualmente.");
        }

        return result with
        {
            ConfidenceScore = confidence,
            Warnings = warnings.Distinct().ToArray()
        };
    }

    private static ListingImportResult EmptyResult(string sourceUrl, IReadOnlyCollection<string> warnings)
    {
        return new ListingImportResult(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            sourceUrl,
            0,
            warnings);
    }

    private static ListingImportResult ResultFromUrlFallback(string sourceUrl, IReadOnlyCollection<string> warnings)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            return EmptyResult(sourceUrl, warnings);
        }

        var slug = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return EmptyResult(sourceUrl, warnings);
        }

        var tokens = slug
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !Regex.IsMatch(token, @"^\d{6,}$"))
            .ToArray();

        if (tokens.Length == 0)
        {
            return EmptyResult(sourceUrl, warnings);
        }

        var titleStart = Array.FindIndex(tokens, token => token.Equals("venta", StringComparison.OrdinalIgnoreCase) || token.Equals("alquiler", StringComparison.OrdinalIgnoreCase));
        var addressStart = Array.FindIndex(tokens, token => token is "av" or "avenida" or "calle");
        if (addressStart < 0)
        {
            addressStart = 0;
        }

        var addressTokens = titleStart > addressStart
            ? tokens[addressStart..titleStart]
            : Array.Empty<string>();
        var titleTokens = titleStart >= 0
            ? tokens[titleStart..]
            : tokens;

        var address = addressTokens.Length > 0 ? ToTitleCase(string.Join(' ', addressTokens)) : null;
        var title = titleTokens.Length > 0 ? ToTitleCase(string.Join(' ', titleTokens)) : null;
        var rooms = ExtractRoomsFromSlug(tokens);

        var result = new ListingImportResult(
            title,
            null,
            null,
            address,
            ExtractNeighborhood(string.Join(' ', tokens), title, address),
            null,
            null,
            rooms,
            null,
            null,
            "Datos recuperados desde el link porque el sitio bloqueo la lectura automatica. Completa precio, expensas, metros y otros datos manualmente.",
            [],
            sourceUrl,
            0,
            warnings);

        return result with
        {
            ConfidenceScore = CalculateConfidence(result),
            Warnings = warnings.Distinct().ToArray()
        };
    }

    private static Dictionary<string, string> ExtractMetaTags(string html)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in MetaTagRegex.Matches(html))
        {
            var tag = match.Value;
            var key = FirstNonEmpty(GetAttribute(tag, "property"), GetAttribute(tag, "name"));
            var content = GetAttribute(tag, "content");

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(content))
            {
                values[key] = WebUtility.HtmlDecode(content).Trim();
            }
        }

        return values;
    }

    private static JsonLdData ExtractJsonLd(string html, ICollection<string> warnings)
    {
        var data = new JsonLdData();

        foreach (Match match in ScriptJsonLdRegex.Matches(html))
        {
            var json = WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                VisitJson(document.RootElement, data);
            }
            catch (JsonException)
            {
                warnings.Add("Algunos datos estructurados de la publicacion no pudieron leerse.");
            }
        }

        return data;
    }

    private static void VisitJson(JsonElement element, JsonLdData data)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var name = property.Name.ToLowerInvariant();
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = property.Value.GetString();
                        data.TextValues.Add(value ?? string.Empty);

                        if (name is "name" or "headline")
                        {
                            data.Name ??= value;
                        }
                        else if (name is "description")
                        {
                            data.Description ??= value;
                        }
                        else if (name is "price" or "priceusd")
                        {
                            data.PriceUsd ??= ParseDecimal(value);
                        }
                        else if (name is "image" or "contenturl")
                        {
                            AddImage(data, value);
                        }
                        else if (name is "streetaddress" or "address")
                        {
                            data.Address ??= value;
                        }
                    }
                    else if (name is "price" && property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDecimal(out var price))
                    {
                        data.PriceUsd ??= price;
                    }

                    VisitJson(property.Value, data);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        AddImage(data, item.GetString());
                    }
                    VisitJson(item, data);
                }
                break;
        }
    }

    private static string ExtractVisibleText(string html)
    {
        var withoutScripts = Regex.Replace(html, "<script[\\s\\S]*?</script>|<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
        var decoded = WebUtility.HtmlDecode(TagRegex.Replace(withoutScripts, " "));
        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }

    private static string? ExtractTitleTag(string html)
    {
        var match = Regex.Match(html, "<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
    }

    private static string? ExtractAddress(string text)
    {
        var match = Regex.Match(text, @"(?:ubicaci[oó]n|direcci[oó]n)\s*:?\s*([^|•\n\r]{8,120})", RegexOptions.IgnoreCase);
        return match.Success ? CleanValue(match.Groups[1].Value) : null;
    }

    private static string? ExtractNeighborhood(string text, params string?[] candidates)
    {
        var joined = string.Join(" ", candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)).Append(text));
        var known = new[] { "Recoleta", "Palermo", "Belgrano", "Caballito", "Villa Urquiza", "Almagro" };
        return known.FirstOrDefault(neighborhood => joined.Contains(neighborhood, StringComparison.OrdinalIgnoreCase));
    }

    private static decimal? ExtractUsdPrice(string text)
    {
        return ExtractNumber(text, @"(?:usd|u\$s|us\$|dolares?)\s*([0-9][0-9.,]*)") ??
               ExtractNumber(text, @"([0-9][0-9.,]*)\s*(?:usd|u\$s|us\$)");
    }

    private static decimal? ExtractExpenses(string text)
    {
        return ExtractNumber(text, @"expensas?\s*(?:\$|ars|ar\$)?\s*([0-9][0-9.,]*)");
    }

    private static decimal? ExtractNumber(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? ParseDecimal(match.Groups[1].Value) : null;
    }

    private static int? ExtractInt(string text, string pattern)
    {
        var number = ExtractNumber(text, pattern);
        return number is null ? null : Convert.ToInt32(Math.Round(number.Value, MidpointRounding.AwayFromZero));
    }

    private static int? ExtractRoomsFromSlug(IReadOnlyCollection<string> tokens)
    {
        var joined = string.Join(' ', tokens);
        var numeric = Regex.Match(joined, @"(\d+)\s*(?:ambientes?|amb)", RegexOptions.IgnoreCase);
        if (numeric.Success && int.TryParse(numeric.Groups[1].Value, out var rooms))
        {
            return rooms;
        }

        if (tokens.Contains("monoambiente", StringComparer.OrdinalIgnoreCase))
        {
            return 1;
        }

        var words = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["uno"] = 1,
            ["un"] = 1,
            ["dos"] = 2,
            ["tres"] = 3,
            ["cuatro"] = 4,
            ["cinco"] = 5
        };

        var tokenList = tokens.ToArray();
        for (var index = 0; index < tokenList.Length; index++)
        {
            if (words.TryGetValue(tokenList[index], out var value) &&
                index > 0 &&
                tokenList[index - 1].Equals("de", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = Regex.Replace(value, @"[^\d,.]", "");
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        if (cleaned.Contains('.') && cleaned.Contains(','))
        {
            cleaned = cleaned.LastIndexOf(',') > cleaned.LastIndexOf('.')
                ? cleaned.Replace(".", "").Replace(',', '.')
                : cleaned.Replace(",", "");
        }
        else if (cleaned.Count(character => character == '.') > 1)
        {
            cleaned = cleaned.Replace(".", "");
        }
        else if (cleaned.Count(character => character == ',') > 1)
        {
            cleaned = cleaned.Replace(",", "");
        }
        else
        {
            cleaned = cleaned.Replace(',', '.');
        }

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? GetAttribute(string tag, string attribute)
    {
        var match = Regex.Match(tag, $"{attribute}\\s*=\\s*([\"'])(.*?)\\1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[2].Value : null;
    }

    private static string? GetMeta(IReadOnlyDictionary<string, string> meta, string key)
    {
        return meta.TryGetValue(key, out var value) ? value : null;
    }

    private static IReadOnlyCollection<string> ExtractImageUrls(IReadOnlyDictionary<string, string> meta, IEnumerable<string> jsonLdImages)
    {
        return new[]
            {
                GetMeta(meta, "og:image"),
                GetMeta(meta, "twitter:image")
            }
            .Concat(jsonLdImages)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static void AddImage(JsonLdData data, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            data.ImageUrls.Add(value.Trim());
        }
    }

    private static int CalculateConfidence(ListingImportResult result)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(result.Title)) score += 12;
        if (result.PriceUsd is not null) score += 18;
        if (!string.IsNullOrWhiteSpace(result.Address)) score += 12;
        if (!string.IsNullOrWhiteSpace(result.Neighborhood)) score += 12;
        if (result.TotalSquareMeters is not null) score += 12;
        if (result.Rooms is not null) score += 8;
        if (result.Bedrooms is not null) score += 8;
        if (result.Bathrooms is not null) score += 8;
        if (!string.IsNullOrWhiteSpace(result.Description)) score += 6;
        if (result.ImageUrls.Count > 0) score += 4;
        return Math.Clamp(score, 0, 100);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static decimal? FirstNonNull(params decimal?[] values)
    {
        return values.FirstOrDefault(value => value is not null);
    }

    private static string CleanValue(string value)
    {
        return WhitespaceRegex.Replace(WebUtility.HtmlDecode(value), " ").Trim(' ', '-', '|', '•');
    }

    private static string ToTitleCase(string value)
    {
        var text = value.Replace(" av ", " avenida ", StringComparison.OrdinalIgnoreCase);
        return CultureInfo.GetCultureInfo("es-AR").TextInfo.ToTitleCase(text.ToLowerInvariant());
    }

    private sealed class JsonLdData
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? PriceUsd { get; set; }
        public string? Address { get; set; }
        public List<string> ImageUrls { get; } = [];
        public List<string> TextValues { get; } = [];
    }
}
