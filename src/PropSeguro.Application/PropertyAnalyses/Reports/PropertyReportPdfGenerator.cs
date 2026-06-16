using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PropSeguro.Application.PropertyAnalyses.Reports;

public sealed class PropertyReportPdfGenerator : IPropertyReportPdfGenerator
{
    private const string Navy = "#082545";
    private const string Teal = "#0B8A72";
    private const string SoftTeal = "#E9F7F3";
    private const string LightBlue = "#EEF6FB";
    private const string Paper = "#FBFCFE";
    private const string Border = "#D9E4EE";
    private const string Text = "#263C55";
    private const string Muted = "#66788D";
    private const string Amber = "#D98A16";
    private const string SoftAmber = "#FFF7E8";
    private const string Danger = "#C83D36";
    private const string SoftDanger = "#FFF0EF";

    public byte[] Generate(PropertyReportDto report)
    {
        ArgumentNullException.ThrowIfNull(report);
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(34);
                page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(10).FontColor(Text));
                page.Content().Element(container => CoverPage(container, report));
            });

            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(34);
                page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(10).FontColor(Text));
                page.Header().Element(Header);
                page.Content().Element(container => ReportContent(container, report));
                page.Footer().Element(Footer);
            });
        }).GeneratePdf();
    }

    private static void CoverPage(IContainer container, PropertyReportDto report)
    {
        container.Background(Colors.White).Padding(8).Column(column =>
        {
            column.Spacing(26);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(brand =>
                {
                    brand.Item().Text("PROPSEGURA").FontSize(34).Bold().FontColor(Navy);
                    brand.Item().PaddingTop(5).Width(110).Height(4).Background(Teal);
                });

                row.ConstantItem(150).AlignRight().Text(report.GeneratedAtUtc.ToString("dd/MM/yyyy"))
                    .FontSize(10).Bold().FontColor(Muted);
            });

            column.Item().PaddingTop(26).Text("Informe de Evaluación de Propiedad")
                .FontSize(30).SemiBold().FontColor(Navy);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(info =>
                {
                    info.Spacing(14);
                    info.Item().Text(report.CoverSection.PropertyTitle).FontSize(22).Bold().FontColor(Navy);
                    info.Item().Text(report.CoverSection.Address).FontSize(13).FontColor(Text);
                    info.Item().Text(report.CoverSection.Neighborhood).FontSize(12).Bold().FontColor(Teal);

                    info.Item().PaddingTop(12).Row(cards =>
                    {
                        cards.RelativeItem().Element(item => CoverFact(item, "Precio", Usd(report.CoverSection.PriceUsd)));
                        cards.RelativeItem().Element(item => CoverFact(item, "Superficie", $"{Number(report.CoverSection.TotalSquareMeters)} m²"));
                    });

                    info.Item().Row(cards =>
                    {
                        cards.RelativeItem().Element(item => CoverFact(item, "USD/m²", UsdPerSquareMeter(report.CoverSection.UsdPerSquareMeter)));
                        cards.RelativeItem().Element(item => CoverFact(item, "Expensas", Ars(report.CoverSection.MonthlyExpenses)));
                    });
                });

            row.ConstantItem(250).Element(item => CoverImage(item, report));
            });

            column.Item().PaddingTop(28).BorderLeft(4).BorderColor(Teal).Background(SoftTeal).Padding(16).Text(
                "Segunda opinión para compradores inmobiliarios. Este informe ordena oportunidad, documentación, riesgos y próximos pasos antes de reservar o comprar.")
                .FontSize(13).LineHeight(1.35f).FontColor(Navy);

            column.Item().Row(row =>
            {
                row.Spacing(8);
                row.RelativeItem(30).Element(item => KpiCard(item, "Recomendación", PdfRecommendationLabel(report.OverallRecommendation), "Conclusión actual", RecommendationColor(report.OverallRecommendation)));
                row.RelativeItem(20).Element(item => KpiCard(item, "Oportunidad", report.OpportunitySection.OpportunityScore.ToString(), report.OpportunitySection.RecommendedUseLabel, ScoreColor(report.OpportunitySection.OpportunityScore)));
                row.RelativeItem(25).Element(item => KpiCard(item, "Protección", ProtectionStatusTitle(report), ProtectionStatusLabel(report), ProtectionStatusColor(report)));
                row.RelativeItem(25).Element(item => KpiCard(item, "Precio", Usd(report.CoverSection.PriceUsd), "Publicado", Navy));
            });

            column.Item().ExtendVertical();

            column.Item().BorderTop(1).BorderColor(Border).PaddingTop(12).Text(
                "Este informe constituye una herramienta de apoyo para la toma de decisiones y no reemplaza la revisión de un escribano, abogado o profesional matriculado.")
                .FontSize(9).FontColor(Muted);
        });
    }

    private static void ReportContent(IContainer container, PropertyReportDto report)
    {
        container.Column(column =>
        {
            column.Spacing(18);

            column.Item().Element(item => ExecutiveSummary(item, report));
            column.Item().PageBreak();

            column.Item().Element(item => PropertySnapshot(item, report));
            column.Item().Element(item => OpportunityAnalysis(item, report));
            column.Item().PageBreak();

            column.Item().Element(item => DocumentRiskAnalysis(item, report));
            column.Item().Element(item => CriticalFindings(item, report));
            column.Item().PageBreak();

            column.Item().Element(item => NegotiationStrategy(item, report));
            column.Item().Element(item => ConditionsToAdvance(item, report));
            column.Item().Element(item => NextSteps(item, report));
            column.Item().Element(item => FinalRecommendation(item, report));
        });
    }

    private static void Header(IContainer container)
    {
        container.PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Text("PROPSEGURA").FontSize(12).Bold().FontColor(Navy);
            row.RelativeItem().AlignRight().Text("Informe Profesional de Evaluación Inmobiliaria")
                .FontSize(8).FontColor(Muted);
        });
    }

    private static void Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Border).PaddingTop(5).Column(column =>
        {
            column.Spacing(3);
            column.Item().Text(
                "Este informe constituye una herramienta de apoyo para la toma de decisiones y no reemplaza la revisión de un escribano, abogado o profesional matriculado.")
                .FontSize(7).LineHeight(1.15f).FontColor(Muted);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("PropSegura - Segunda opinión para comprar con menos riesgo")
                    .FontSize(8).FontColor(Muted);
                row.ConstantItem(80).AlignRight().Text(text =>
                {
                    text.Span("Página ").FontSize(8).FontColor(Muted);
                    text.CurrentPageNumber().FontSize(8).FontColor(Muted);
                });
            });
        });
    }

    private static void ExecutiveSummary(IContainer container, PropertyReportDto report)
    {
        container.Column(column =>
        {
            column.Spacing(16);
            SectionHeader(column, "Resumen Ejecutivo", "La decisión resumida en una página.");

            column.Item().Element(item => RecommendationHero(item, report));
            column.Item().Element(item => DecisionExplanation(item, report));

            column.Item().Text(report.ExecutiveConclusion)
                .FontSize(13).LineHeight(1.35f).FontColor(Text);

            column.Item().Element(item => VisualLegend(item));

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(item => KpiCard(item, "Score PropSegura", $"{OverallScore(report)} / 100", "Evaluación integral", ScoreColor(OverallScore(report))));
                row.RelativeItem().Element(item => KpiCard(item, "Oportunidad", report.OpportunitySection.OpportunityScore.ToString(), report.OpportunitySection.RecommendedUseLabel, ScoreColor(report.OpportunitySection.OpportunityScore)));
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(item => ProtectionStatusCard(item, report));
                row.RelativeItem().Element(item => KpiCard(item, "Salud del edificio", report.ExpensesSection.BuildingHealthScore?.ToString() ?? "S/D", report.ExpensesSection.BuildingHealthClassification ?? "Pendiente", ScoreColor(report.ExpensesSection.BuildingHealthScore)));
                row.RelativeItem().Element(item => KpiCard(item, "Negociación", NegotiationScore(report).ToString(), report.NegotiationSection.MarketPositionLabel, ScoreColor(NegotiationScore(report))));
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(item => ExecutiveCallout(item, "Principal oportunidad", MainOpportunity(report), Teal, SoftTeal));
                row.RelativeItem().Element(item => ExecutiveCallout(item, "Principal riesgo", MainRisk(report), Amber, SoftAmber));
            });
        });
    }

    private static void PropertySnapshot(IContainer container, PropertyReportDto report)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            SectionHeader(column, "Ficha de la Propiedad", "Datos base usados para la evaluación.");

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                SnapshotRow(table, "Dirección", report.PropertySummary.Address);
                SnapshotRow(table, "Barrio", report.PropertySummary.Neighborhood);
                SnapshotRow(table, "Precio publicado", Usd(report.PropertySummary.PriceUsd));
                SnapshotRow(table, "Superficie", $"{Number(report.PropertySummary.TotalSquareMeters)} m²");
                SnapshotRow(table, "USD/m²", UsdPerSquareMeter(report.PropertySummary.UsdPerSquareMeter));
                SnapshotRow(table, "Piso", report.PropertySummary.Floor?.ToString() ?? "Sin dato");
                SnapshotRow(table, "Unidad", FirstNonEmpty(report.PropertySummary.UnitIdentifier, report.PropertySummary.ApartmentUnit, "Sin dato"));
                SnapshotRow(table, "Expensas mensuales", Ars(report.PropertySummary.MonthlyExpenses));
            });
        });
    }

    private static void OpportunityAnalysis(IContainer container, PropertyReportDto report)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            SectionHeader(column, "Análisis de Oportunidad", "Precio, zona y conveniencia inicial.");

            column.Item().Row(row =>
            {
                row.ConstantItem(110).Element(item => ScorePanel(item, report.OpportunitySection.OpportunityScore, "Oportunidad"));
                row.RelativeItem().PaddingLeft(14).Column(text =>
                {
                    text.Spacing(8);
                    text.Item().Text(report.OpportunitySection.Explanation).FontSize(12).LineHeight(1.35f);
                    text.Item().Text($"Uso recomendado: {report.OpportunitySection.RecommendedUseLabel}.")
                        .FontSize(10).Bold().FontColor(Teal);
                    text.Item().Text($"Posición de mercado: {report.NegotiationSection.MarketPositionLabel}. {report.OpportunitySection.BenchmarkComparison}.")
                        .FontSize(10).FontColor(Text);
                });
            });

            column.Item().Element(item => BulletBox(item, "Fortalezas", BuildStrengths(report), Teal));
            column.Item().Element(item => ExecutiveCallout(item, "Salud del edificio", BuildingHealthExplanation(report), Teal, SoftTeal));
        });
    }

    private static void DocumentRiskAnalysis(IContainer container, PropertyReportDto report)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            SectionHeader(column, "Análisis Documental", "Expensas y reserva revisadas con reglas determinísticas.");

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(item => DocumentCard(item, "Expensas", report.ExpensesSection.Status, report.ExpensesSection.IsAnalyzed, report.ExpensesSection.KeyFindings, report.ExpensesSection.SuggestedQuestions, report.ExpensesSection.SuggestedNegotiationPoints));
                row.RelativeItem().Element(item => DocumentCard(item, "Reserva", report.ReservationSection.Status, report.ReservationSection.IsAnalyzed, report.ReservationSection.KeyFindings, report.ReservationSection.SuggestedQuestions, report.ReservationSection.SuggestedNegotiationPoints));
            });
        });
    }

    private static void CriticalFindings(IContainer container, PropertyReportDto report)
    {
        var findings = report.FindingsSection.Groups
            .Where(group => group.Severity is "Critical" or "High")
            .SelectMany(group => group.Findings.Select(finding => (group.SeverityLabel, Finding: finding)))
            .Take(8)
            .ToArray();

        container.Column(column =>
        {
            column.Spacing(10);
            SectionHeader(column, "Hallazgos Prioritarios", "Puntos que pueden cambiar la decisión.");

            if (findings.Length == 0)
            {
                column.Item().Element(item => EmptyState(item, "No se detectaron hallazgos altos o críticos con la información analizada."));
                return;
            }

            foreach (var (severityLabel, finding) in findings)
            {
                column.Item().Element(item => FindingCard(item, severityLabel, finding));
            }
        });
    }

    private static void NegotiationStrategy(IContainer container, PropertyReportDto report)
    {
        container.Column(column =>
        {
            column.Spacing(12);
            SectionHeader(column, "Estrategia de Negociación", "Rango de precio y argumentos accionables.");

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(item => MoneyCard(item, "Precio publicado", Usd(report.NegotiationSection.PublishedPriceUsd)));
                row.RelativeItem().Element(item => MoneyCard(item, "Valor justo estimado", FairRange(report)));
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(item => MoneyCard(item, "Primera oferta sugerida", Usd(report.NegotiationSection.SuggestedFirstOfferUsd)));
                row.RelativeItem().Element(item => MoneyCard(item, "Precio objetivo", Usd(report.NegotiationSection.SuggestedTargetPriceUsd)));
            });

            column.Item().Text(report.NegotiationSection.Explanation).FontSize(11).LineHeight(1.35f).FontColor(Text);
            column.Item().Element(item => BulletBox(item, "Puntos para negociar", report.NegotiationArguments, Amber));
        });
    }

    private static void ConditionsToAdvance(IContainer container, PropertyReportDto report)
    {
        container.Column(column =>
        {
            column.Spacing(10);
            SectionHeader(column, "Condiciones para avanzar", "Qué debería resolverse antes de firmar o transferir dinero.");
            column.Item().Element(item => ChecklistBox(item, report.ConditionsToAdvance));
        });
    }

    private static void NextSteps(IContainer container, PropertyReportDto report)
    {
        var steps = BuildBuyerNextSteps(report);

        container.Column(column =>
        {
            column.Spacing(10);
            SectionHeader(column, "Próximos pasos recomendados", "Checklist operativo para continuar.");
            column.Item().Element(item => ChecklistBox(item, steps));
        });
    }

    private static void FinalRecommendation(IContainer container, PropertyReportDto report)
    {
        container.Border(1).BorderColor(RecommendationColor(report.OverallRecommendation))
            .Background(RecommendationBackground(report.OverallRecommendation)).Padding(14).Column(column =>
            {
                column.Spacing(7);
                column.Item().Text($"Recomendación Final: {PdfRecommendationLabel(report.OverallRecommendation)}")
                    .FontSize(19).Bold().FontColor(Navy);
                column.Item().Text(report.OverallRecommendationExplanation).FontSize(10).LineHeight(1.25f).FontColor(Text);
                column.Item().Text(report.ExecutiveConclusion).FontSize(10).LineHeight(1.25f).FontColor(Text);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(item => BulletBox(item, "Qué sostiene esta recomendación", RecommendationSupports(report), Teal));
                    row.RelativeItem().Element(item => BulletBox(item, "Qué la bloquea", RecommendationBlocks(report), Danger));
                });
                column.Item().Element(item => BulletBox(item, $"Para pasar a: {NextRecommendationLevel(report)}", NextLevelActions(report), Amber));
            });
    }

    private static void CoverImage(IContainer container, PropertyReportDto report)
    {
        var path = report.CoverSection.CoverImageStoragePath;

        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            container.Height(220).Border(1).BorderColor(Border).Image(path).FitArea();
            return;
        }

        container.Height(220).Border(1).BorderColor(Border).Background(Paper).AlignCenter().AlignMiddle().Column(column =>
        {
            column.Item().AlignCenter().Text("PS").FontSize(38).Bold().FontColor(Teal);
            column.Item().PaddingTop(8).AlignCenter().Text("Sin imagen").FontSize(12).FontColor(Muted);
        });
    }

    private static void RecommendationHero(IContainer container, PropertyReportDto report)
    {
        container.BorderLeft(6).BorderColor(RecommendationColor(report.OverallRecommendation))
            .Background(RecommendationBackground(report.OverallRecommendation)).Padding(18).Column(column =>
            {
                column.Spacing(8);
                column.Item().Text(PdfRecommendationBadge(report.OverallRecommendation))
                    .FontSize(24).Bold().FontColor(Navy);
                column.Item().Text(report.ExecutiveSummary).FontSize(12).LineHeight(1.35f).FontColor(Text);
            });
    }

    private static void DecisionExplanation(IContainer container, PropertyReportDto report)
    {
        container.Border(1).BorderColor(RecommendationColor(report.OverallRecommendation))
            .Background(Paper).Padding(14).Column(column =>
            {
                column.Spacing(5);
                column.Item().Text("Por qué recomendamos esto").FontSize(12).Bold().FontColor(Navy);
                column.Item().Text(BuildDecisionExplanation(report)).FontSize(11).LineHeight(1.35f).FontColor(Text);
            });
    }

    private static void VisualLegend(IContainer container)
    {
        container.Border(1).BorderColor(Border).Background(Colors.White).Padding(10).Row(row =>
        {
            row.RelativeItem().Element(item => LegendItem(item, Teal, "Verde", "Situación favorable"));
            row.RelativeItem().Element(item => LegendItem(item, Amber, "Amarillo", "Requiere revisión"));
            row.RelativeItem().Element(item => LegendItem(item, Danger, "Rojo", "Riesgo relevante"));
        });
    }

    private static void LegendItem(IContainer container, string color, string title, string text)
    {
        container.Row(row =>
        {
            row.ConstantItem(12).Height(12).Background(color);
            row.RelativeItem().PaddingLeft(8).Text($"{title}: {text}").FontSize(9).FontColor(Text);
        });
    }

    private static void ProtectionStatusCard(IContainer container, PropertyReportDto report)
    {
        container.Padding(4).Border(1).BorderColor(Border).Background(ProtectionStatusBackground(report)).Padding(14).Column(column =>
        {
            column.Spacing(5);
            column.Item().Text("Protección").FontSize(9).Bold().FontColor(Muted);
            column.Item().MinHeight(32).Text(ProtectionStatusTitle(report)).FontSize(13).Bold().FontColor(ProtectionStatusColor(report)).LineHeight(1.1f);
            column.Item().Text(ProtectionStatusLabel(report)).FontSize(9).LineHeight(1.25f).FontColor(Text);
        });
    }

    private static void DocumentCard(
        IContainer container,
        string title,
        string status,
        bool isAnalyzed,
        IReadOnlyCollection<DocumentFindingDto> findings,
        IReadOnlyCollection<string> questions,
        IReadOnlyCollection<string> actions)
    {
        var hasHighRisk = findings.Any(item => item.Severity is "Critical" or "High");
        var color = hasHighRisk ? Danger : isAnalyzed ? Teal : Amber;
        var badge = hasHighRisk ? "Riesgo detectado" : isAnalyzed ? "Analizado" : "Requiere revisión";

        container.PaddingRight(6).Border(1).BorderColor(Border).Background(Colors.White).Padding(14).Column(column =>
        {
            column.Spacing(8);
            column.Item().Text(title).FontSize(15).Bold().FontColor(Navy);
            column.Item().Text(badge).FontSize(10).Bold().FontColor(color);
            column.Item().Text(status).FontSize(9).FontColor(Muted);

            AddShortList(column, "Hallazgos clave", findings.Select(item => item.Title).Take(3).ToArray());
            AddShortList(column, "Preguntas sugeridas", questions.Take(2).ToArray());
            AddShortList(column, "Acciones recomendadas", actions.Take(2).ToArray());
        });
    }

    private static void FindingCard(IContainer container, string severityLabel, DocumentFindingDto finding)
    {
        var isCritical = finding.Severity is "Critical" or "High";
        var color = isCritical ? Danger : Amber;
        var background = isCritical ? SoftDanger : SoftAmber;

        container.BorderLeft(5).BorderColor(color).Background(background).Padding(12).Column(column =>
        {
            column.Spacing(5);
            column.Item().Text(finding.Title).FontSize(12).Bold().FontColor(Navy);
            column.Item().Text($"Problema: {finding.Description}").FontSize(10).LineHeight(1.25f).FontColor(Text);
            column.Item().Text($"Impacto: {finding.BuyerImpact ?? "Puede afectar tu decisión de avanzar, negociar o frenar."}")
                .FontSize(9).LineHeight(1.25f).FontColor(Text);
            column.Item().Text($"Acción: {finding.SuggestedChange ?? finding.SuggestedNegotiationPoint ?? "Pedir aclaración por escrito antes de avanzar."}")
                .FontSize(9).LineHeight(1.25f).FontColor(Text);
        });
    }

    private static void SectionHeader(ColumnDescriptor column, string title, string subtitle)
    {
        column.Item().Column(header =>
        {
            header.Item().Text(title).FontSize(22).Bold().FontColor(Navy);
            header.Item().Text(subtitle).FontSize(10).FontColor(Muted);
        });
    }

    private static void KpiCard(IContainer container, string title, string value, string label, string accent)
    {
        container.Padding(2).Border(1).BorderColor(Border).Background(Colors.White).Padding(12).Column(column =>
        {
            column.Spacing(5);
            column.Item().Text(title).FontSize(9).Bold().FontColor(Muted);
            column.Item().MinHeight(34).Text(value).FontSize(KpiValueFontSize(value)).Bold().FontColor(accent).LineHeight(1.05f);
            column.Item().Text(label).FontSize(8).FontColor(Text).LineHeight(1.15f);
        });
    }

    private static void ScorePanel(IContainer container, int score, string label)
    {
        container.Height(100).Background(ScoreBackground(score)).Border(1).BorderColor(Border).AlignCenter().AlignMiddle().Column(column =>
        {
            column.Item().AlignCenter().Text(score.ToString()).FontSize(30).Bold().FontColor(ScoreColor(score));
            column.Item().AlignCenter().Text(label).FontSize(9).Bold().FontColor(Muted);
        });
    }

    private static void CoverFact(IContainer container, string label, string value)
    {
        container.PaddingRight(8).Border(1).BorderColor(Border).Background(Paper).Padding(10).Column(column =>
        {
            column.Item().Text(label).FontSize(8).Bold().FontColor(Muted);
            column.Item().Text(value).FontSize(12).Bold().FontColor(Navy);
        });
    }

    private static void MoneyCard(IContainer container, string label, string value)
    {
        container.PaddingRight(8).Border(1).BorderColor(Border).Background(Colors.White).Padding(14).Column(column =>
        {
            column.Item().Text(label).FontSize(9).Bold().FontColor(Muted);
            column.Item().Text(value).FontSize(17).Bold().FontColor(Navy);
        });
    }

    private static void ExecutiveCallout(IContainer container, string title, string text, string color, string background)
    {
        container.PaddingRight(6).BorderLeft(4).BorderColor(color).Background(background).Padding(12).Column(column =>
        {
            column.Item().Text(title).FontSize(10).Bold().FontColor(color);
            column.Item().Text(text).FontSize(10).LineHeight(1.25f).FontColor(Text);
        });
    }

    private static void BulletBox(IContainer container, string title, IReadOnlyCollection<string> items, string color)
    {
        container.Border(1).BorderColor(Border).Background(Colors.White).Padding(12).Column(column =>
        {
            column.Spacing(5);
            column.Item().Text(title).FontSize(12).Bold().FontColor(color);

            foreach (var item in items.DefaultIfEmpty("Sin información disponible."))
            {
                column.Item().Text($"• {item}").FontSize(10).LineHeight(1.3f).FontColor(Text);
            }
        });
    }

    private static void ChecklistBox(IContainer container, IReadOnlyCollection<string> items)
    {
        container.Border(1).BorderColor(Border).Background(Colors.White).Padding(12).Column(column =>
        {
            column.Spacing(7);

            foreach (var item in items.DefaultIfEmpty("Completar información pendiente."))
            {
                column.Item().Text($"□ {item}").FontSize(11).LineHeight(1.3f).FontColor(Text);
            }
        });
    }

    private static void EmptyState(IContainer container, string text)
    {
        container.Background(Paper).Border(1).BorderColor(Border).Padding(14).Text(text).FontSize(10).FontColor(Muted);
    }

    private static void AddShortList(ColumnDescriptor column, string title, IReadOnlyCollection<string> items)
    {
        column.Item().Text(title).FontSize(9).Bold().FontColor(Navy);

        if (items.Count == 0)
        {
            column.Item().Text("Sin señales relevantes por ahora.").FontSize(9).FontColor(Muted);
            return;
        }

        foreach (var item in items)
        {
            column.Item().Text($"• {item}").FontSize(9).LineHeight(1.25f).FontColor(Text);
        }
    }

    private static void SnapshotRow(TableDescriptor table, string label, string value)
    {
        table.Cell().BorderBottom(1).BorderColor(Border).Padding(8).Text(label).FontSize(9).Bold().FontColor(Muted);
        table.Cell().BorderBottom(1).BorderColor(Border).Padding(8).Text(value).FontSize(10).FontColor(Navy);
    }

    private static IReadOnlyCollection<string> BuildStrengths(PropertyReportDto report)
    {
        var strengths = new List<string>();

        if (report.OpportunitySection.OpportunityScore >= 70)
        {
            strengths.Add("La evaluación inicial muestra una oportunidad atractiva para seguir analizando.");
        }

        if (report.NegotiationSection.MarketPosition is "Cheap" or "Fair")
        {
            strengths.Add("El precio publicado parece competitivo o razonable frente al benchmark de la zona.");
        }

        if (report.ExpensesSection.IsAnalyzed)
        {
            strengths.Add("Las expensas ya fueron revisadas para detectar señales de costo o consorcio.");
        }

        if (report.ReservationSection.IsAnalyzed)
        {
            strengths.Add("La reserva ya fue analizada para identificar cláusulas riesgosas.");
        }

        return strengths.Count == 0
            ? ["La propiedad ya cuenta con una base ordenada para continuar la revisión."]
            : strengths;
    }

    private static IReadOnlyCollection<string> BuildBuyerNextSteps(PropertyReportDto report)
    {
        var steps = new List<string>
        {
            "Contactar inmobiliaria o vendedor con las preguntas del informe."
        };

        steps.AddRange(report.NextSteps.Take(4));

        if (report.FindingsSection.Groups.Any(group => group.Severity is "Critical" or "High"))
        {
            steps.Add("Revisar los hallazgos prioritarios con escribano o abogado.");
        }

        steps.Add("Confirmar documentación pendiente antes de firmar.");

        return steps.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string BuildDecisionExplanation(PropertyReportDto report)
    {
        var hasHighRisk = report.FindingsSection.Groups.Any(group => group.Severity is "Critical" or "High");

        if (report.OpportunitySection.OpportunityScore >= 70 && hasHighRisk)
        {
            return "La oportunidad económica es atractiva, pero los riesgos documentales detectados impiden recomendar el avance en este momento.";
        }

        if (report.OpportunitySection.OpportunityScore >= 70 && report.ProtectionSection.MissingDocumentsCount > 0)
        {
            return "El precio y la oportunidad inicial son competitivos, pero todavía faltan validaciones documentales antes de comprometer dinero.";
        }

        if (report.NegotiationSection.MarketPosition is "Expensive" or "Overpriced")
        {
            return "El análisis sugiere que el precio publicado exige negociación antes de avanzar con una reserva.";
        }

        if (report.OverallRecommendation is "Advance" or "AdvanceWithCaution")
        {
            return "La información disponible sostiene continuar el análisis, manteniendo revisión profesional antes de firmar.";
        }

        return "La recomendación combina oportunidad, protección documental, salud del edificio y margen de negociación.";
    }

    private static string BuildingHealthExplanation(PropertyReportDto report)
    {
        if (!report.ExpensesSection.BuildingHealthScore.HasValue)
        {
            return "No se encontraron suficientes referencias para una evaluación completa del estado del edificio.";
        }

        return "Basado en referencias detectadas en expensas, mantenimiento, obras, ascensores, morosidad y observaciones del consorcio.";
    }

    private static IReadOnlyCollection<string> RecommendationSupports(PropertyReportDto report)
    {
        var items = new List<string>();

        if (report.OpportunitySection.OpportunityScore >= 70)
        {
            items.Add("Buen puntaje de oportunidad inicial.");
        }

        if (report.NegotiationSection.MarketPosition is "Cheap" or "Fair")
        {
            items.Add("Precio competitivo o razonable frente al benchmark.");
        }

        if (report.ExpensesSection.IsAnalyzed)
        {
            items.Add("Expensas revisadas dentro del flujo PropSegura.");
        }

        if (report.ReservationSection.IsAnalyzed)
        {
            items.Add("Reserva analizada para detectar cláusulas riesgosas.");
        }

        return items.Count == 0 ? ["La información cargada permite ordenar próximos pasos."] : items;
    }

    private static IReadOnlyCollection<string> RecommendationBlocks(PropertyReportDto report)
    {
        var items = new List<string>();

        if (report.FindingsSection.Groups.Any(group => group.Severity == "Critical"))
        {
            items.Add("Existen hallazgos críticos que requieren revisión profesional.");
        }

        if (report.FindingsSection.Groups.Any(group => group.Severity == "High"))
        {
            items.Add("Hay hallazgos altos que pueden afectar precio, reserva o documentación.");
        }

        if (report.ProtectionSection.MissingDocumentsCount > 0)
        {
            items.Add("Falta documentación clave para cerrar la validación.");
        }

        if (report.NegotiationSection.MarketPosition is "Expensive" or "Overpriced")
        {
            items.Add("El precio publicado necesita renegociación.");
        }

        return items.Count == 0 ? ["No hay bloqueos fuertes con la información analizada."] : items;
    }

    private static string NextRecommendationLevel(PropertyReportDto report)
    {
        return report.OverallRecommendation switch
        {
            "Avoid" => "Avanzar con precaución",
            "Renegotiate" => "Avanzar con precaución",
            "WaitForDocuments" => "Avanzar con precaución",
            "AdvanceWithCaution" => "Avanzar",
            _ => "mantener una decisión informada"
        };
    }

    private static IReadOnlyCollection<string> NextLevelActions(PropertyReportDto report)
    {
        var actions = new List<string>();
        actions.AddRange(report.ConditionsToAdvance.Take(4));

        if (actions.Count == 0)
        {
            actions.Add("Validar documentación completa con escribano o abogado.");
            actions.Add("Confirmar que precio y expensas coincidan con la publicación.");
        }

        return actions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string MainOpportunity(PropertyReportDto report)
    {
        if (report.NegotiationSection.MarketPosition is "Cheap")
        {
            return "El precio por m² se ubica por debajo del benchmark de la zona, lo que puede indicar una oportunidad.";
        }

        if (report.OpportunitySection.OpportunityScore >= 75)
        {
            return "La propiedad combina buen puntaje inicial, zona y datos suficientes para seguir evaluando.";
        }

        return "La propiedad tiene información inicial organizada para decidir qué validar antes de avanzar.";
    }

    private static string MainRisk(PropertyReportDto report)
    {
        var critical = report.FindingsSection.Groups
            .Where(group => group.Severity is "Critical" or "High")
            .SelectMany(group => group.Findings)
            .FirstOrDefault();

        if (critical is not null)
        {
            return critical.Title;
        }

        if (report.ProtectionSection.MissingDocumentsCount > 0)
        {
            return "Falta documentación clave para confirmar si el comprador está protegido.";
        }

        return "No se detectaron riesgos altos con la información analizada.";
    }

    private static int NegotiationScore(PropertyReportDto report)
    {
        return report.NegotiationSection.MarketPosition switch
        {
            "Cheap" => 90,
            "Fair" => 75,
            "Expensive" => 55,
            "Overpriced" => 35,
            _ => report.NegotiationSection.SuggestedTargetPriceUsd.HasValue ? 60 : 0
        };
    }

    private static int OverallScore(PropertyReportDto report)
    {
        var opportunity = report.OpportunitySection.OpportunityScore;
        var protection = report.ProtectionSection.ProtectionScore;
        var building = report.ExpensesSection.BuildingHealthScore ?? 50;
        var negotiation = NegotiationScore(report);

        return Math.Clamp((int)Math.Round(opportunity * 0.35m + protection * 0.30m + building * 0.20m + negotiation * 0.15m), 0, 100);
    }

    private static string ProtectionStatusTitle(PropertyReportDto report)
    {
        if (report.ProtectionSection.HighOrCriticalFindingsCount > 0 || report.ProtectionSection.ProtectionScore < 30)
        {
            return "Riesgo documental elevado";
        }

        if (report.ProtectionSection.MissingDocumentsCount > 0 || report.ProtectionSection.ProtectionScore < 60)
        {
            return "Validación documental incompleta";
        }

        return "Protección documental razonable";
    }

    private static string ProtectionStatusLabel(PropertyReportDto report)
    {
        if (report.ProtectionSection.HighOrCriticalFindingsCount > 0)
        {
            return $"{report.ProtectionSection.HighOrCriticalFindingsCount} hallazgo(s) alto(s) o crítico(s).";
        }

        if (report.ProtectionSection.MissingDocumentsCount > 0)
        {
            return $"{report.ProtectionSection.MissingDocumentsCount} documento(s) pendiente(s).";
        }

        return "Sin alertas severas con la información analizada.";
    }

    private static string ProtectionStatusColor(PropertyReportDto report)
    {
        if (report.ProtectionSection.HighOrCriticalFindingsCount > 0 || report.ProtectionSection.ProtectionScore < 30)
        {
            return Danger;
        }

        return report.ProtectionSection.MissingDocumentsCount > 0 || report.ProtectionSection.ProtectionScore < 60 ? Amber : Teal;
    }

    private static string ProtectionStatusBackground(PropertyReportDto report)
    {
        if (report.ProtectionSection.HighOrCriticalFindingsCount > 0 || report.ProtectionSection.ProtectionScore < 30)
        {
            return SoftDanger;
        }

        return report.ProtectionSection.MissingDocumentsCount > 0 || report.ProtectionSection.ProtectionScore < 60 ? SoftAmber : SoftTeal;
    }

    private static string FairRange(PropertyReportDto report)
    {
        if (!report.NegotiationSection.EstimatedFairValueMinUsd.HasValue ||
            !report.NegotiationSection.EstimatedFairValueMaxUsd.HasValue)
        {
            return "Sin dato";
        }

        return $"{Usd(report.NegotiationSection.EstimatedFairValueMinUsd)} - {Usd(report.NegotiationSection.EstimatedFairValueMaxUsd)}";
    }

    private static int KpiValueFontSize(string value)
    {
        if (value.Length >= 30)
        {
            return 11;
        }

        if (value.Length >= 22)
        {
            return 12;
        }

        if (value.Length >= 16)
        {
            return 14;
        }

        if (value.Length >= 10)
        {
            return 17;
        }

        return 22;
    }

    private static string Usd(decimal? value)
    {
        return value.HasValue ? $"USD {value.Value:N0}" : "Sin dato";
    }

    private static string UsdPerSquareMeter(decimal? value)
    {
        return value.HasValue ? $"USD {value.Value:N0}" : "Sin dato";
    }

    private static string Ars(decimal? value)
    {
        return value.HasValue ? $"ARS {value.Value:N0}" : "Sin dato";
    }

    private static string Number(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("N0") : "Sin dato";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "Sin dato";
    }

    private static string ProtectionLabel(int score)
    {
        if (score >= 80)
        {
            return "Alta";
        }

        return score >= 50 ? "Media" : "Baja";
    }

    private static string PdfRecommendationBadge(string recommendation)
    {
        return recommendation switch
        {
            "Advance" => "✓ Avanzar",
            "AdvanceWithCaution" => "⚠ Avanzar con precaución",
            "Renegotiate" => "$ Renegociar",
            "Avoid" => "No avanzar",
            "WaitForDocuments" => "⚠ Esperar documentación",
            _ => "Faltan datos"
        };
    }

    private static string PdfRecommendationLabel(string recommendation)
    {
        return recommendation switch
        {
            "Advance" => "Avanzar",
            "AdvanceWithCaution" => "Avanzar con Precaución",
            "Renegotiate" => "Renegociar",
            "Avoid" => "No Avanzar",
            "WaitForDocuments" => "Esperar Documentación",
            _ => "Completar Datos"
        };
    }

    private static string ScoreColor(int? score)
    {
        return score switch
        {
            null => Muted,
            >= 80 => Teal,
            >= 60 => "#4FA878",
            >= 40 => Amber,
            _ => Danger
        };
    }

    private static string ScoreBackground(int score)
    {
        return score switch
        {
            >= 80 => SoftTeal,
            >= 60 => LightBlue,
            >= 40 => SoftAmber,
            _ => SoftDanger
        };
    }

    private static string RecommendationColor(string recommendation)
    {
        return recommendation switch
        {
            "Advance" => Teal,
            "AdvanceWithCaution" => "#2473A6",
            "Renegotiate" => Amber,
            "Avoid" => Danger,
            "WaitForDocuments" => Amber,
            _ => Muted
        };
    }

    private static string RecommendationBackground(string recommendation)
    {
        return recommendation switch
        {
            "Advance" => SoftTeal,
            "AdvanceWithCaution" => LightBlue,
            "Renegotiate" => SoftAmber,
            "Avoid" => SoftDanger,
            "WaitForDocuments" => SoftAmber,
            _ => Paper
        };
    }
}
