using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.Common;

public sealed class DeterministicExpenseIntelligenceAnalyzer : IExpenseIntelligenceAnalyzer
{
    public ExpenseIntelligenceAnalysis Analyze(PropertyDocument document, string? text)
    {
        var findings = DocumentAnalysisRuleEngine.Analyze(document, text).ToArray();
        var buildingFindings = findings
            .Where(finding => finding.Category != FindingCategory.Documentation)
            .ToArray();
        var score = CalculateBuildingHealthScore(buildingFindings);

        return new ExpenseIntelligenceAnalysis(
            score,
            Classify(score),
            BuildSummary(buildingFindings),
            findings,
            findings
                .Select(finding => finding.SuggestedQuestion)
                .OfType<string>()
                .Where(question => !string.IsNullOrWhiteSpace(question))
                .Distinct()
                .ToArray(),
            findings
                .Select(finding => finding.SuggestedNegotiationPoint ?? finding.SuggestedChange)
                .OfType<string>()
                .Where(point => !string.IsNullOrWhiteSpace(point))
                .Distinct()
                .ToArray());
    }

    public static int CalculateBuildingHealthScore(IEnumerable<DocumentFinding> findings)
    {
        var score = 100;
        var findingList = findings.ToArray();

        if (findingList.Any(finding => finding.Title == "Expensas extraordinarias detectadas"))
        {
            score -= 20;
        }

        if (findingList.Any(finding => finding.Title == "Humedad o filtracion detectada"))
        {
            score -= 20;
        }

        if (findingList.Any(finding => finding.Title == "Juicio o reclamo judicial detectado"))
        {
            score -= 25;
        }

        if (findingList.Any(finding => finding.Title == "Obras o reparaciones mencionadas"))
        {
            score -= 10;
        }

        if (findingList.Any(finding => finding.Title == "Morosidad o deuda detectada"))
        {
            score -= 10;
        }

        if (findingList.Any(finding => finding.Title == "Referencia a ascensor"))
        {
            score -= 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    public static string Classify(int score)
    {
        if (score >= 90)
        {
            return "Excelente";
        }

        if (score >= 70)
        {
            return "Buena";
        }

        if (score >= 50)
        {
            return "Atencion";
        }

        return "Riesgo elevado";
    }

    private static string BuildSummary(IReadOnlyCollection<DocumentFinding> findings)
    {
        if (findings.Count == 0)
        {
            return "No se detectaron senales relevantes en las expensas con las reglas actuales.";
        }

        var pieces = new List<string>();

        if (findings.Any(finding => finding.Title == "Expensas extraordinarias detectadas"))
        {
            pieces.Add("expensas extraordinarias");
        }

        if (findings.Any(finding => finding.Title == "Humedad o filtracion detectada"))
        {
            pieces.Add("humedad o filtraciones");
        }

        if (findings.Any(finding => finding.Title == "Juicio o reclamo judicial detectado"))
        {
            pieces.Add("reclamos judiciales");
        }

        if (findings.Any(finding => finding.Title == "Obras o reparaciones mencionadas"))
        {
            pieces.Add("reparaciones edilicias");
        }

        if (findings.Any(finding => finding.Title == "Morosidad o deuda detectada"))
        {
            pieces.Add("morosidad o deuda");
        }

        if (pieces.Count == 0)
        {
            return "Se detectaron senales menores en las expensas. Recomendamos pedir liquidaciones recientes y consultar si hay aumentos previstos.";
        }

        return $"Se detectaron {JoinSpanishList(pieces)}. Recomendamos consultar si existen pagos pendientes, obras abiertas o costos futuros antes de avanzar.";
    }

    private static string JoinSpanishList(IReadOnlyList<string> values)
    {
        return values.Count switch
        {
            0 => string.Empty,
            1 => values[0],
            2 => $"{values[0]} y {values[1]}",
            _ => $"{string.Join(", ", values.Take(values.Count - 1))} y {values[^1]}"
        };
    }
}
