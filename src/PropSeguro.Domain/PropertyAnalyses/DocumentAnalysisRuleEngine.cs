using System.Globalization;
using System.Text;

namespace PropSeguro.Domain.PropertyAnalyses;

public static class DocumentAnalysisRuleEngine
{
    public static IReadOnlyCollection<DocumentFinding> Analyze(PropertyDocument document, string? extractedText)
    {
        var normalizedText = Normalize(extractedText);
        var findings = new List<DocumentFinding>();

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            findings.Add(new DocumentFinding(
                document.Id,
                document.PropertyAnalysisId,
                FindingSeverity.Low,
                FindingCategory.Documentation,
                "No hay texto extraido para analizar",
                "El documento fue cargado, pero todavia no hay texto suficiente para aplicar reglas de contenido.",
                "El PDF es escaneado o tiene texto seleccionable?",
                "Pegar o corregir el texto extraido antes de analizar la reserva."));

            return findings;
        }

        switch (document.DocumentType)
        {
            case DocumentType.Expenses:
                AddIfContains(findings, document, normalizedText, "expensas extraordinarias", FindingSeverity.Medium, FindingCategory.Financial, "Expensas extraordinarias detectadas", "El texto menciona expensas extraordinarias que pueden afectar el costo real de la operacion.");
                AddIfContains(findings, document, normalizedText, "deuda", FindingSeverity.High, FindingCategory.Financial, "Deuda detectada", "El texto menciona deuda. Conviene pedir detalle actualizado antes de avanzar.");
                AddIfContainsAny(findings, document, normalizedText, ["juicio", "demanda"], FindingSeverity.High, FindingCategory.Legal, "Riesgo legal detectado", "El texto menciona juicio o demanda vinculada al inmueble, consorcio o documentacion.");
                break;

            case DocumentType.AssemblyMinutes:
                AddIfContainsAny(findings, document, normalizedText, ["derrame", "expensas extraordinarias"], FindingSeverity.High, FindingCategory.Financial, "Riesgo financiero en acta", "El acta menciona derrame o expensas extraordinarias.");
                AddIfContains(findings, document, normalizedText, "ascensor", FindingSeverity.Medium, FindingCategory.Operational, "Tema operativo de ascensor", "El acta menciona ascensor. Puede implicar mantenimiento, obra o gasto relevante.");
                AddIfContainsAny(findings, document, normalizedText, ["humedad", "filtracion"], FindingSeverity.Medium, FindingCategory.Operational, "Humedad o filtracion detectada", "El acta menciona humedad o filtracion. Conviene revisar el estado fisico y responsabilidades.");
                AddIfContains(findings, document, normalizedText, "obra", FindingSeverity.Medium, FindingCategory.Operational, "Obra mencionada", "El acta menciona obras que pueden afectar gastos, tiempos o uso del inmueble.");
                break;

            case DocumentType.ReservationAgreement:
                AddReservationAgreementFindings(findings, document, normalizedText);
                break;

            case DocumentType.DomainReport:
                AddIfContainsAny(findings, document, normalizedText, ["embargo", "inhibicion", "hipoteca"], FindingSeverity.Critical, FindingCategory.Legal, "Restriccion legal critica", "El informe menciona embargo, inhibicion o hipoteca.");
                break;

            case DocumentType.Other:
                findings.Add(new DocumentFinding(
                    document.Id,
                    document.PropertyAnalysisId,
                    FindingSeverity.Info,
                    FindingCategory.Transparency,
                    "Texto recibido",
                    "Se recibio texto para un documento clasificado como Other. No se aplicaron reglas especificas."));
                break;
        }

        return findings;
    }

    private static void AddReservationAgreementFindings(ICollection<DocumentFinding> findings, PropertyDocument document, string normalizedText)
    {
        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["no reintegrable", "sena no reembolsable", "sin derecho a devolucion"],
            FindingSeverity.High,
            FindingCategory.Legal,
            "Reserva no reintegrable",
            "El texto indica que la reserva podria no devolverse si la operacion no avanza. Esto puede ser riesgoso si todavia no revisaste documentacion clave.",
            "La reserva se devuelve si el banco rechaza el credito o si aparece un problema documental?",
            "Agregar una clausula que condicione la reserva a aprobacion crediticia y revision satisfactoria de documentacion.");

        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["pierde la reserva", "renuncia a reclamar", "la inmobiliaria retendra"],
            FindingSeverity.Critical,
            FindingCategory.Legal,
            "Perdida o retencion de reserva",
            "El texto podria permitir perder la reserva o limitar reclamos del comprador antes de revisar condiciones esenciales.",
            "En que casos exactos se pierde la reserva y quien retiene el dinero?",
            "Definir causas objetivas de devolucion y eliminar renuncias amplias a reclamar.");

        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["penalidad", "multa"],
            FindingSeverity.High,
            FindingCategory.Financial,
            "Penalidad o multa detectada",
            "El texto menciona penalidades o multas. Conviene revisar montos, plazos y si aplican tambien cuando aparece un problema documental.",
            "Cual es el monto de la penalidad y en que supuesto se aplica?",
            "Limitar penalidades a incumplimientos claros y excluir casos de credito rechazado o documentacion observada.");

        AddIfContains(
            findings,
            document,
            normalizedText,
            "comision aun si no se escritura",
            FindingSeverity.Critical,
            FindingCategory.Financial,
            "Comision aunque no se escriture",
            "El texto sugiere que podria cobrarse comision incluso si la escritura no se concreta.",
            "La comision se paga solo si la operacion llega a escritura?",
            "Aclarar que la comision solo corresponde si la compraventa se escritura, salvo incumplimiento comprobado del comprador.");

        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["72 horas", "plazo perentorio"],
            FindingSeverity.Medium,
            FindingCategory.Negotiation,
            "Plazo breve o perentorio",
            "El texto fija un plazo corto o rigido. Puede dejar poco margen para revisar documentacion, credito o condiciones de la operacion.",
            "El plazo se suspende hasta recibir documentacion del vendedor?",
            "Extender el plazo o hacerlo correr desde la entrega completa de documentacion.");

        AddIfContains(
            findings,
            document,
            normalizedText,
            "sujeto a aprobacion",
            FindingSeverity.Medium,
            FindingCategory.Negotiation,
            "Aprobacion pendiente",
            "La reserva queda sujeta a aprobacion, pero puede faltar precision sobre quien aprueba, cuando y con que consecuencias.",
            "Quien aprueba la operacion y que pasa si no se aprueba?",
            "Indicar plazo, responsable y devolucion de reserva si no hay aprobacion.");

        AddIfContains(
            findings,
            document,
            normalizedText,
            "la documentacion sera presentada posteriormente",
            FindingSeverity.Medium,
            FindingCategory.Documentation,
            "Documentacion posterior",
            "El texto indica que la documentacion se entregara despues. Esto puede dejar al comprador pagando antes de revisar informacion clave.",
            "Que documentacion se entrega y en que fecha maxima?",
            "Condicionar la reserva a recibir y revisar documentacion completa dentro de un plazo claro.");

        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["ad corpus", "en el estado en que se encuentra"],
            FindingSeverity.Medium,
            FindingCategory.Legal,
            "Aceptacion amplia del estado del inmueble",
            "El texto puede implicar aceptacion amplia del estado o superficie del inmueble. Conviene revisar si excluye reclamos relevantes.",
            "La aceptacion del estado excluye vicios ocultos, deudas o diferencias documentales?",
            "Aclarar que la aceptacion no cubre vicios ocultos, deudas, restricciones ni problemas documentales no informados.");

        AddIfMissingAny(
            findings,
            document,
            normalizedText,
            ["credito hipotecario", "aprobacion crediticia", "aprobacion del credito", "banco"],
            FindingSeverity.High,
            FindingCategory.Financial,
            "Falta condicion por aprobacion crediticia",
            "No se detecta una condicion clara que proteja al comprador si necesita credito hipotecario y el banco no aprueba la operacion.",
            "La reserva se devuelve si el banco rechaza el credito o no aprueba la propiedad?",
            "Agregar condicion suspensiva por aprobacion crediticia y tasacion bancaria satisfactoria.");

        AddIfMissingAny(
            findings,
            document,
            normalizedText,
            ["revision de documentacion", "documentacion satisfactoria", "informe de dominio", "certificado de dominio"],
            FindingSeverity.High,
            FindingCategory.Documentation,
            "Falta condicion por revision documental",
            "No se detecta una clausula clara de devolucion si aparece un problema documental antes de escriturar.",
            "Que pasa con la reserva si el dominio, reglamento, expensas o deudas tienen observaciones?",
            "Agregar devolucion de reserva ante observaciones documentales, deudas, inhibiciones o restricciones no informadas.");

        AddIfMissingAny(
            findings,
            document,
            normalizedText,
            ["plazo de entrega de documentacion", "documentacion completa", "entrega de documentacion"],
            FindingSeverity.Medium,
            FindingCategory.Documentation,
            "Falta plazo de entrega de documentacion",
            "No se detecta un plazo claro para que el vendedor entregue documentacion necesaria.",
            "Hasta que fecha debe entregar el vendedor toda la documentacion?",
            "Agregar fecha limite para entregar titulo, dominio, libre deuda, expensas y documentacion del consorcio.");

        AddIfMissingAny(
            findings,
            document,
            normalizedText,
            ["libre de deudas", "libre deuda", "sin deuda", "deudas"],
            FindingSeverity.Medium,
            FindingCategory.Financial,
            "Falta mencion clara de libre deuda",
            "No se detecta una declaracion clara sobre deudas, impuestos, servicios o expensas pendientes.",
            "La propiedad se entrega libre de deudas, impuestos, servicios y expensas?",
            "Agregar declaracion de libre deuda y obligacion del vendedor de cancelar saldos previos a la escritura.");

        AddIfMissingAny(
            findings,
            document,
            normalizedText,
            ["expensas", "abl", "impuestos", "servicios", "tasas"],
            FindingSeverity.Medium,
            FindingCategory.Financial,
            "Falta detalle sobre gastos y deudas",
            "No se detecta referencia suficiente a expensas, impuestos, tasas o servicios.",
            "Que gastos quedan a cargo del vendedor y cuales del comprador?",
            "Definir corte de gastos, expensas, impuestos, tasas y servicios a la fecha de posesion o escritura.");

        AddIfMissingAny(
            findings,
            document,
            normalizedText,
            ["comprador", "vendedor", "dni", "cuit", "cuil"],
            FindingSeverity.Medium,
            FindingCategory.Legal,
            "Falta identificacion clara de las partes",
            "No se detecta identificacion suficiente de comprador, vendedor o intervinientes.",
            "Estan completos nombre, DNI/CUIT/CUIL, domicilio y rol de cada parte?",
            "Completar identificacion de comprador, vendedor, inmobiliaria y autorizaciones para firmar.");

        AddIfMissingAny(
            findings,
            document,
            normalizedText,
            ["domicilio", "direccion", "calle", "unidad funcional", "uf"],
            FindingSeverity.Medium,
            FindingCategory.Documentation,
            "Falta direccion clara del inmueble",
            "No se detecta una direccion o identificacion clara de la propiedad reservada.",
            "La reserva identifica exactamente direccion, unidad funcional, cochera y baulera si corresponden?",
            "Agregar direccion completa, unidad funcional, datos registrales y anexos incluidos.");
    }

    private static void AddIfContains(
        ICollection<DocumentFinding> findings,
        PropertyDocument document,
        string normalizedText,
        string needle,
        FindingSeverity severity,
        FindingCategory category,
        string title,
        string description,
        string? suggestedQuestion = null,
        string? suggestedChange = null)
    {
        if (normalizedText.Contains(Normalize(needle), StringComparison.Ordinal))
        {
            findings.Add(new DocumentFinding(document.Id, document.PropertyAnalysisId, severity, category, title, description, suggestedQuestion, suggestedChange));
        }
    }

    private static void AddIfContainsAny(
        ICollection<DocumentFinding> findings,
        PropertyDocument document,
        string normalizedText,
        IEnumerable<string> needles,
        FindingSeverity severity,
        FindingCategory category,
        string title,
        string description,
        string? suggestedQuestion = null,
        string? suggestedChange = null)
    {
        if (needles.Any(needle => normalizedText.Contains(Normalize(needle), StringComparison.Ordinal)))
        {
            findings.Add(new DocumentFinding(document.Id, document.PropertyAnalysisId, severity, category, title, description, suggestedQuestion, suggestedChange));
        }
    }

    private static void AddIfMissingAny(
        ICollection<DocumentFinding> findings,
        PropertyDocument document,
        string normalizedText,
        IEnumerable<string> needles,
        FindingSeverity severity,
        FindingCategory category,
        string title,
        string description,
        string suggestedQuestion,
        string suggestedChange)
    {
        if (needles.All(needle => !normalizedText.Contains(Normalize(needle), StringComparison.Ordinal)))
        {
            findings.Add(new DocumentFinding(document.Id, document.PropertyAnalysisId, severity, category, title, description, suggestedQuestion, suggestedChange));
        }
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
}
