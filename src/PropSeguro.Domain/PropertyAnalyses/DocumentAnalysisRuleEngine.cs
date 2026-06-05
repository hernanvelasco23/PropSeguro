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
                "Pegar o corregir el texto extraido antes de analizar la reserva.",
                "No hay texto usable para detectar senales de alerta.",
                "Pegar o corregir el texto extraido antes de analizar.",
                null));

            return findings;
        }

        switch (document.DocumentType)
        {
            case DocumentType.Expenses:
                AddExpensesFindings(findings, document, normalizedText);
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
            ["no reintegrable", "sena no reembolsable", "no reembolsable", "sin derecho a devolucion"],
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
            ["pierde la reserva", "perdera la reserva", "renuncia a reclamar", "la inmobiliaria retendra", "retendra la suma"],
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

        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["comision aun si no se escritura", "comision aunque no se concrete"],
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

    private static void AddExpensesFindings(ICollection<DocumentFinding> findings, PropertyDocument document, string normalizedText)
    {
        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["expensas extraordinarias", "extraordinaria", "derrame"],
            FindingSeverity.Medium,
            FindingCategory.Financial,
            "Expensas extraordinarias o derrame",
            "El texto menciona expensas extraordinarias o derrames. Pueden indicar costos adicionales por fuera de la expensa ordinaria.",
            "Que monto extraordinario queda pendiente y hasta cuando se paga?",
            "Pedir detalle de saldos, cuotas pendientes y dejar aclarado quien asume esos importes antes de la posesion.");

        AddIfContains(
            findings,
            document,
            normalizedText,
            "fondo de reserva",
            FindingSeverity.Low,
            FindingCategory.Financial,
            "Fondo de reserva mencionado",
            "El texto menciona fondo de reserva. Puede ser una senal de obras previstas o de necesidad de cubrir gastos del consorcio.",
            "Para que se esta usando el fondo de reserva y si hay aumentos previstos?",
            "Solicitar liquidaciones recientes y actas que expliquen destino, saldo y proyeccion del fondo.");

        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["deuda", "moroso", "morosidad"],
            FindingSeverity.High,
            FindingCategory.Financial,
            "Deuda o morosidad detectada",
            "El texto menciona deuda, morosos o morosidad. Conviene pedir detalle actualizado para entender si afecta al consorcio o a la unidad.",
            "La unidad esta libre de deuda de expensas y existe morosidad relevante en el edificio?",
            "Exigir libre deuda de expensas y aclarar que toda deuda anterior queda a cargo del vendedor.");

        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["juicio", "demanda"],
            FindingSeverity.High,
            FindingCategory.Legal,
            "Riesgo legal detectado",
            "El texto menciona juicio o demanda vinculada al inmueble, consorcio o administracion.",
            "Cual es el estado del juicio o demanda y que costo potencial puede generar?",
            "Pedir documentacion respaldatoria y reservar derecho a revisar el impacto antes de avanzar.");

        AddIfContains(
            findings,
            document,
            normalizedText,
            "ascensor",
            FindingSeverity.Medium,
            FindingCategory.Operational,
            "Mantenimiento de ascensor",
            "El texto menciona ascensor. Puede implicar mantenimiento, reparaciones o gastos extraordinarios.",
            "Hay reparaciones, adecuaciones o presupuestos pendientes sobre ascensores?",
            "Pedir actas y presupuestos vinculados al ascensor antes de cerrar precio.");

        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["filtracion", "humedad"],
            FindingSeverity.Medium,
            FindingCategory.Operational,
            "Humedad o filtracion detectada",
            "El texto menciona humedad o filtracion. Puede anticipar reparaciones, reclamos o problemas estructurales.",
            "La humedad o filtracion afecta a la unidad, partes comunes o unidades vecinas?",
            "Condicionar avance a inspeccion y aclaracion de responsabilidad y costos de reparacion.");

        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["obra", "reparacion"],
            FindingSeverity.Medium,
            FindingCategory.Operational,
            "Obras o reparaciones mencionadas",
            "El texto menciona obras o reparaciones. Pueden impactar expensas futuras, tiempos y uso del edificio.",
            "Que obras estan aprobadas, cuanto cuestan y como se financian?",
            "Solicitar cronograma, presupuesto y cuotas pendientes antes de negociar precio.");

        AddIfContainsAny(
            findings,
            document,
            normalizedText,
            ["administracion", "seguro", "encargado"],
            FindingSeverity.Info,
            FindingCategory.Transparency,
            "Costos operativos del consorcio",
            "El texto menciona administracion, seguro o encargado. Sirve para revisar composicion y tendencia de expensas.",
            "Hubo aumentos recientes o previstos en administracion, seguro o encargado?",
            "Comparar liquidaciones de varios meses para entender si las expensas son estables.");
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
            findings.Add(new DocumentFinding(
                document.Id,
                document.PropertyAnalysisId,
                severity,
                category,
                title,
                description,
                suggestedQuestion,
                suggestedChange,
                BuildBuyerImpact(severity, category),
                suggestedChange,
                needle));
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
        var matchedNeedle = needles.FirstOrDefault(needle => normalizedText.Contains(Normalize(needle), StringComparison.Ordinal));

        if (matchedNeedle is not null)
        {
            findings.Add(new DocumentFinding(
                document.Id,
                document.PropertyAnalysisId,
                severity,
                category,
                title,
                description,
                suggestedQuestion,
                suggestedChange,
                BuildBuyerImpact(severity, category),
                suggestedChange,
                matchedNeedle));
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
            findings.Add(new DocumentFinding(
                document.Id,
                document.PropertyAnalysisId,
                severity,
                category,
                title,
                description,
                suggestedQuestion,
                suggestedChange,
                BuildBuyerImpact(severity, category),
                suggestedChange));
        }
    }

    private static string BuildBuyerImpact(FindingSeverity severity, FindingCategory category)
    {
        if (severity is FindingSeverity.Critical or FindingSeverity.High)
        {
            return category switch
            {
                FindingCategory.Financial => "Podrias asumir costos, deudas o perder dinero por condiciones poco claras.",
                FindingCategory.Legal => "Podrias quedar obligado por una condicion legal desfavorable o dificil de reclamar.",
                FindingCategory.Documentation => "Podrias avanzar sin documentacion suficiente para validar la operacion.",
                FindingCategory.Operational => "Podrias comprar con problemas de mantenimiento o reparaciones pendientes.",
                _ => "Podria afectar tu decision de avanzar, negociar o frenar la operacion."
            };
        }

        return "Es una senal para pedir aclaraciones antes de comprometer dinero.";
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
