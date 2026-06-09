using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.Common;

public sealed record ExpenseUnitExtraction(
    int? DetectedFloor,
    string? DetectedApartmentUnit,
    string? DetectedUnitIdentifier,
    decimal? DetectedExpenseAmount,
    string Confidence);

public sealed record ExpenseDocumentValidation(
    ExpenseUnitExtraction Extraction,
    int ConfidenceScore,
    string Classification,
    string Label,
    string Explanation,
    IReadOnlyCollection<DocumentFinding> Findings);

public static partial class ExpenseDocumentValidator
{
    public static ExpenseDocumentValidation Validate(PropertyAnalysis analysis, PropertyDocument document, string? text)
    {
        var extraction = Extract(text);
        var findings = BuildFindings(analysis, document, extraction);
        var classification = Classify(analysis, extraction, findings);
        var score = classification switch
        {
            "Validated" => 95,
            "PartiallyValidated" => 68,
            "Mismatch" => 25,
            _ => 40
        };

        return new ExpenseDocumentValidation(
            extraction,
            score,
            classification,
            LabelFor(classification),
            ExplanationFor(classification, extraction),
            findings);
    }

    public static ExpenseUnitExtraction Extract(string? text)
    {
        var normalized = Normalize(text);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new ExpenseUnitExtraction(null, null, null, null, "Unknown");
        }

        var floor = TryParseInt(FirstGroup(FloorRegex(), normalized));
        var apartmentUnit = FirstGroup(ApartmentRegex(), normalized);
        var unitIdentifier = FirstGroup(UnitIdentifierRegex(), normalized);
        var amount = ParseMoney(FirstGroup(ExpenseAmountRegex(), normalized));
        var detectedParts = 0;

        detectedParts += floor.HasValue ? 1 : 0;
        detectedParts += !string.IsNullOrWhiteSpace(apartmentUnit) ? 1 : 0;
        detectedParts += !string.IsNullOrWhiteSpace(unitIdentifier) ? 1 : 0;
        detectedParts += amount.HasValue ? 1 : 0;

        var confidence = detectedParts switch
        {
            >= 3 => "High",
            2 => "Medium",
            1 => "Low",
            _ => "Unknown"
        };

        return new ExpenseUnitExtraction(
            floor,
            NormalizeDetectedValue(apartmentUnit),
            NormalizeDetectedValue(unitIdentifier),
            amount,
            confidence);
    }

    private static IReadOnlyCollection<DocumentFinding> BuildFindings(
        PropertyAnalysis analysis,
        PropertyDocument document,
        ExpenseUnitExtraction extraction)
    {
        var findings = new List<DocumentFinding>();
        var hasPropertyUnit = analysis.Floor.HasValue || HasValue(analysis.ApartmentUnit) || HasValue(analysis.UnitIdentifier);
        var hasDocumentUnit = extraction.DetectedFloor.HasValue || HasValue(extraction.DetectedApartmentUnit) || HasValue(extraction.DetectedUnitIdentifier);
        var unitMismatch = IsUnitMismatch(analysis, extraction);
        var amountMismatch = IsExpenseAmountMismatch(analysis.MonthlyExpenses, extraction.DetectedExpenseAmount);

        if (unitMismatch)
        {
            findings.Add(new DocumentFinding(
                document.Id,
                analysis.Id,
                FindingSeverity.High,
                FindingCategory.Documentation,
                "Las expensas podrian corresponder a otra unidad",
                "El documento parece indicar una unidad distinta a la cargada para la propiedad.",
                "Podes confirmar que estas expensas corresponden exactamente al piso y departamento publicado?",
                "No avanzar con la reserva hasta validar que la liquidacion corresponde a la unidad correcta.",
                "El analisis de expensas podria no corresponder a este departamento.",
                "No avanzar con la reserva hasta validar que la liquidacion corresponde a la unidad correcta.",
                BuildMatchedUnitText(extraction)));
        }
        else if (!hasPropertyUnit && hasDocumentUnit)
        {
            findings.Add(new DocumentFinding(
                document.Id,
                analysis.Id,
                FindingSeverity.Medium,
                FindingCategory.Documentation,
                "No se pudo validar la unidad de las expensas",
                "El documento contiene datos de unidad, pero la propiedad no tiene piso/departamento cargado para comparar.",
                "Cual es el piso y departamento exacto de la unidad publicada?",
                "Solicitar liquidacion de expensas identificada por unidad funcional o departamento.",
                "No podemos confirmar automaticamente que estas expensas corresponden a la propiedad analizada.",
                "Solicitar liquidacion de expensas identificada por unidad funcional o departamento.",
                BuildMatchedUnitText(extraction)));
        }
        else if (!hasDocumentUnit)
        {
            findings.Add(new DocumentFinding(
                document.Id,
                analysis.Id,
                FindingSeverity.Medium,
                FindingCategory.Documentation,
                "No se identifico la unidad en las expensas",
                "No fue posible detectar piso, departamento o unidad funcional en el documento.",
                "Estas expensas corresponden exactamente a la unidad publicada?",
                "Pedir una liquidacion donde figure piso, departamento o unidad funcional.",
                "El analisis puede ser util, pero no valida que el documento corresponda a la propiedad.",
                "Pedir una liquidacion donde figure piso, departamento o unidad funcional."));
        }

        if (amountMismatch)
        {
            findings.Add(new DocumentFinding(
                document.Id,
                analysis.Id,
                FindingSeverity.High,
                FindingCategory.Financial,
                "Las expensas del documento no coinciden con la publicacion",
                "El valor detectado en el documento difiere significativamente del valor cargado o publicado.",
                "Cual es el valor real y actualizado de las expensas de esta unidad?",
                "Usar la diferencia de expensas como argumento para renegociar precio o condiciones.",
                "Podrias estar evaluando la propiedad con un costo mensual incorrecto.",
                "Usar la diferencia de expensas como argumento para renegociar precio o condiciones.",
                extraction.DetectedExpenseAmount?.ToString("N0", CultureInfo.InvariantCulture)));
        }
        else if (!analysis.MonthlyExpenses.HasValue || !extraction.DetectedExpenseAmount.HasValue)
        {
            findings.Add(new DocumentFinding(
                document.Id,
                analysis.Id,
                FindingSeverity.Info,
                FindingCategory.Financial,
                "No se pudo verificar el valor real de expensas",
                "Faltan datos suficientes para comparar el valor publicado contra el documento.",
                "Cual es el valor real y actualizado de las expensas de esta unidad?",
                "Pedir la liquidacion mas reciente y comparar contra el valor publicado.",
                "La validacion de costos mensuales queda incompleta.",
                "Pedir la liquidacion mas reciente y comparar contra el valor publicado."));
        }

        return findings;
    }

    private static string Classify(
        PropertyAnalysis analysis,
        ExpenseUnitExtraction extraction,
        IReadOnlyCollection<DocumentFinding> findings)
    {
        if (findings.Any(finding => finding.Severity == FindingSeverity.High))
        {
            return "Mismatch";
        }

        var hasPropertyUnit = analysis.Floor.HasValue || HasValue(analysis.ApartmentUnit) || HasValue(analysis.UnitIdentifier);
        var hasDocumentUnit = extraction.DetectedFloor.HasValue || HasValue(extraction.DetectedApartmentUnit) || HasValue(extraction.DetectedUnitIdentifier);
        var amountClose = analysis.MonthlyExpenses.HasValue &&
            extraction.DetectedExpenseAmount.HasValue &&
            !IsExpenseAmountMismatch(analysis.MonthlyExpenses, extraction.DetectedExpenseAmount);

        if (!hasDocumentUnit)
        {
            return "NotValidated";
        }

        if (hasPropertyUnit && hasDocumentUnit && amountClose)
        {
            return "Validated";
        }

        if (hasDocumentUnit || extraction.DetectedExpenseAmount.HasValue)
        {
            return "PartiallyValidated";
        }

        return "NotValidated";
    }

    private static string LabelFor(string classification)
    {
        return classification switch
        {
            "Validated" => "Documento validado",
            "PartiallyValidated" => "Validacion parcial",
            "Mismatch" => "Posible documento incorrecto",
            _ => "No validado"
        };
    }

    private static string ExplanationFor(string classification, ExpenseUnitExtraction extraction)
    {
        var unitText = BuildMatchedUnitText(extraction);
        var amountText = extraction.DetectedExpenseAmount.HasValue
            ? $" Expensas detectadas: USD/ARS {extraction.DetectedExpenseAmount:N0}."
            : string.Empty;

        return classification switch
        {
            "Validated" => $"La unidad detectada coincide con los datos cargados.{amountText}",
            "PartiallyValidated" => $"Se detectaron datos parciales para validar las expensas. {unitText}{amountText}".Trim(),
            "Mismatch" => $"Hay diferencias relevantes entre la publicacion y el documento. {unitText}{amountText}".Trim(),
            _ => "No se detectaron datos suficientes de unidad o importe para validar automaticamente este documento."
        };
    }

    private static bool IsUnitMismatch(PropertyAnalysis analysis, ExpenseUnitExtraction extraction)
    {
        if (analysis.Floor.HasValue && extraction.DetectedFloor.HasValue && analysis.Floor.Value != extraction.DetectedFloor.Value)
        {
            return true;
        }

        if (HasValue(analysis.ApartmentUnit) &&
            HasValue(extraction.DetectedApartmentUnit) &&
            !ValuesMatch(analysis.ApartmentUnit, extraction.DetectedApartmentUnit))
        {
            return true;
        }

        if (HasValue(analysis.UnitIdentifier) &&
            HasValue(extraction.DetectedUnitIdentifier) &&
            !ValuesMatch(analysis.UnitIdentifier, extraction.DetectedUnitIdentifier))
        {
            return true;
        }

        return false;
    }

    private static bool IsExpenseAmountMismatch(decimal? expected, decimal? detected)
    {
        if (!expected.HasValue || !detected.HasValue || expected.Value <= 0)
        {
            return false;
        }

        var difference = Math.Abs(expected.Value - detected.Value) / expected.Value;
        return difference > 0.15m;
    }

    private static bool ValuesMatch(string? left, string? right)
    {
        return string.Equals(NormalizeToken(left), NormalizeToken(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeToken(string? value)
    {
        return Normalize(value).Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? FirstGroup(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int? TryParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static decimal? ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = Regex.Replace(value, @"[^\d,\.]", string.Empty);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return null;
        }

        if (cleaned.Contains(','))
        {
            cleaned = cleaned.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }
        else
        {
            var lastDot = cleaned.LastIndexOf('.');
            if (lastDot >= 0 && cleaned.Length - lastDot - 1 != 2)
            {
                cleaned = cleaned.Replace(".", string.Empty, StringComparison.Ordinal);
            }
        }

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? NormalizeDetectedValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private static bool HasValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string BuildMatchedUnitText(ExpenseUnitExtraction extraction)
    {
        var parts = new List<string>();

        if (extraction.DetectedFloor.HasValue)
        {
            parts.Add($"Piso {extraction.DetectedFloor.Value}");
        }

        if (HasValue(extraction.DetectedApartmentUnit))
        {
            parts.Add($"Depto {extraction.DetectedApartmentUnit}");
        }

        if (HasValue(extraction.DetectedUnitIdentifier))
        {
            parts.Add($"UF {extraction.DetectedUnitIdentifier}");
        }

        return parts.Count == 0 ? "Sin unidad detectada" : string.Join(" - ", parts);
    }

    [GeneratedRegex(@"\b(?:piso|p)\s*[:\-]?\s*(\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FloorRegex();

    [GeneratedRegex(@"\b(?:depto|departamento|dpto|unidad)\s*[:\-]?\s*([a-z0-9]{1,8})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ApartmentRegex();

    [GeneratedRegex(@"\b(?:unidad\s+funcional|u\.?\s*f\.?|uf)\s*[:\-]?\s*([a-z0-9\-]{1,12})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnitIdentifierRegex();

    [GeneratedRegex(@"\b(?:importe|total\s+a\s+pagar|total\s+expensas|expensas\s+del\s+mes|saldo\s+a\s+pagar)\s*[:\-]?\s*(?:ars|usd|\$)?\s*([0-9][0-9\.,]{2,})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExpenseAmountRegex();
}
