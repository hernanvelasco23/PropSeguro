namespace PropSeguro.Domain.PropertyAnalyses;

public enum AnalysisStatus
{
    Created = 1,
    Completed = 2
}

public enum RecommendedUse
{
    Living = 1,
    Investment = 2,
    Both = 3,
    NotRecommended = 4,
    Unknown = 5
}

public enum InitialRiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum PropertyCondition
{
    New = 1,
    Excellent = 2,
    Good = 3,
    NeedsRenovation = 4,
    Unknown = 5
}

public enum DemandLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum MarketPosition
{
    Cheap = 1,
    Fair = 2,
    Expensive = 3,
    Overpriced = 4,
    Unknown = 5
}

public enum DocumentType
{
    Expenses = 1,
    AssemblyMinutes = 2,
    CoOwnershipRegulation = 3,
    DomainReport = 4,
    FloorPlan = 5,
    ReservationAgreement = 6,
    Other = 7
}

public enum DocumentStatus
{
    Uploaded = 1,
    PendingAnalysis = 2,
    Analyzed = 3,
    Rejected = 4
}

public enum DocumentAnalysisStatus
{
    NotStarted = 1,
    Pending = 2,
    Completed = 3,
    Failed = 4
}

public enum TextExtractionStatus
{
    NotStarted = 1,
    Extracted = 2,
    Empty = 3,
    Failed = 4,
    ManuallyEdited = 5
}

public enum TextExtractionQuality
{
    Unknown = 1,
    Good = 2,
    Partial = 3,
    Poor = 4,
    Empty = 5
}

public enum LastAnalyzedTextSource
{
    ExtractedText = 1,
    EditedText = 2,
    ManualText = 3
}

public enum FindingSeverity
{
    Info = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    Critical = 5
}

public enum FindingCategory
{
    Legal = 1,
    Financial = 2,
    Operational = 3,
    Documentation = 4,
    Transparency = 5,
    Negotiation = 6
}

public enum ChecklistStatus
{
    Missing = 1,
    Provided = 2,
    NotApplicable = 3
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
