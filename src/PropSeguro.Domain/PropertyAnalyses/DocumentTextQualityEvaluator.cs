using System.Globalization;
using System.Text;

namespace PropSeguro.Domain.PropertyAnalyses;

public static class DocumentTextQualityEvaluator
{
    private static readonly string[] AuditMetadataTerms =
    [
        "signnow",
        "historial del documento",
        "document history",
        "audit trail",
        "auditoria",
        "auditoría",
        "firmante",
        "firmantes",
        "signature",
        "certificado de finalizacion",
        "certificado de finalización",
        "id del documento",
        "document id",
        "hash",
        "ip address",
        "correo electronico",
        "correo electrónico"
    ];

    private static readonly string[] ReservationTerms =
    [
        "reserva",
        "seña",
        "sena",
        "comprador",
        "vendedor",
        "inmueble",
        "precio",
        "escritura",
        "dominio",
        "aprobacion crediticia",
        "aprobación crediticia",
        "inmobiliaria"
    ];

    private static readonly string[] ExpensesTerms =
    [
        "expensas",
        "consorcio",
        "administracion",
        "administración",
        "unidad funcional",
        "uf",
        "periodo",
        "período",
        "deuda",
        "morosidad",
        "fondo de reserva",
        "extraordinaria"
    ];

    public static TextExtractionQuality Evaluate(DocumentType documentType, string? text)
    {
        var normalizedText = Normalize(text);

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return TextExtractionQuality.Empty;
        }

        if (normalizedText.Length < 80)
        {
            return TextExtractionQuality.Poor;
        }

        var expectedKeywordCount = CountExpectedKeywords(documentType, normalizedText);
        var auditKeywordCount = CountMatchingTerms(AuditMetadataTerms, normalizedText);

        if (IsAuditDominant(auditKeywordCount, expectedKeywordCount))
        {
            return TextExtractionQuality.Poor;
        }

        if (documentType is DocumentType.ReservationAgreement or DocumentType.Expenses && expectedKeywordCount == 0)
        {
            return normalizedText.Length < 600
                ? TextExtractionQuality.Poor
                : TextExtractionQuality.Partial;
        }

        if (normalizedText.Length >= 600 && expectedKeywordCount >= 2)
        {
            return TextExtractionQuality.Good;
        }

        return expectedKeywordCount > 0
            ? TextExtractionQuality.Partial
            : TextExtractionQuality.Poor;
    }

    public static bool ShouldAttemptOcr(DocumentType documentType, string? text)
    {
        var quality = Evaluate(documentType, text);
        return quality is TextExtractionQuality.Empty or TextExtractionQuality.Poor;
    }

    public static bool IsLikelyAuditPage(DocumentType documentType, string? text)
    {
        var normalizedText = Normalize(text);

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        var auditKeywordCount = CountMatchingTerms(AuditMetadataTerms, normalizedText);
        var expectedKeywordCount = CountExpectedKeywords(documentType, normalizedText);

        return IsAuditDominant(auditKeywordCount, expectedKeywordCount);
    }

    private static int CountExpectedKeywords(DocumentType documentType, string normalizedText)
    {
        var terms = documentType switch
        {
            DocumentType.ReservationAgreement => ReservationTerms,
            DocumentType.Expenses => ExpensesTerms,
            _ => []
        };

        return CountMatchingTerms(terms, normalizedText);
    }

    private static int CountMatchingTerms(IEnumerable<string> terms, string normalizedText)
    {
        var compactText = Compact(normalizedText);

        return terms.Count(term =>
        {
            var normalizedTerm = Normalize(term);
            return normalizedText.Contains(normalizedTerm, StringComparison.Ordinal)
                || compactText.Contains(Compact(normalizedTerm), StringComparison.Ordinal);
        });
    }

    private static bool IsAuditDominant(int auditKeywordCount, int expectedKeywordCount)
    {
        if (auditKeywordCount >= 3 && expectedKeywordCount <= 1)
        {
            return true;
        }

        return auditKeywordCount >= 5 && auditKeywordCount >= expectedKeywordCount + 3;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Compact(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
