namespace PropSeguro.Domain.PropertyAnalyses;

public enum AnalysisStatus
{
    Created = 1,
    Completed = 2
}

public enum DocumentType
{
    Unknown = 0,
    UltimasExpensas = 1,
    ActasDeAsamblea = 2,
    ReglamentoDeCopropiedad = 3,
    InformeDeDominio = 4,
    Plano = 5
}

public enum RiskFindingType
{
    MissingDocumentation = 1,
    Risk = 2,
    Warning = 3
}

public enum RiskSeverity
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum ChecklistCategory
{
    Documentation = 1
}
