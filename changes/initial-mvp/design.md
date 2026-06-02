# PropSeguro Initial MVP Design

## Architecture

The MVP should use Clean Architecture with clear dependency direction:

- Domain: business entities, value objects, enums, domain rules, and domain events.
- Application: CQRS commands and queries, MediatR handlers, DTOs, interfaces, validation, and orchestration.
- Infrastructure: EF Core, SQL Server persistence, document storage implementation, external service adapters, and future AI provider adapters.
- API: ASP.NET Core Web API controllers/endpoints, request/response contracts, authentication when introduced, and error mapping.

Dependencies should flow inward. Domain should not depend on Application, Infrastructure, or API. Application should depend on Domain and abstractions. Infrastructure should implement Application abstractions.

## Project Shape

Recommended initial solution layout:

- `src/PropSeguro.Domain`
- `src/PropSeguro.Application`
- `src/PropSeguro.Infrastructure`
- `src/PropSeguro.Api`
- `tests/PropSeguro.Application.Tests`
- `tests/PropSeguro.Domain.Tests`
- `tests/PropSeguro.Api.Tests`

## Domain Model

### PropertyReview

Represents a buyer's review request.

Key fields:

- `Id`
- `Status`
- `ListingUrl`
- `ListingSource`
- `PropertyDetails`
- `CreatedAtUtc`
- `UpdatedAtUtc`
- `CompletedAtUtc`
- `RiskAssessment`
- `Documents`
- `Findings`
- `ChecklistItems`

Statuses:

- `Draft`
- `Submitted`
- `Analyzing`
- `Completed`
- `Failed`

### PropertyDetails

Owned value object containing manually entered property information.

Fields:

- `PropertyType`
- `OperationType`
- `Address`
- `Neighborhood`
- `City`
- `Province`
- `AskingPrice`
- `Expenses`
- `TotalSurfaceSquareMeters`
- `CoveredSurfaceSquareMeters`
- `Rooms`
- `Bedrooms`
- `Bathrooms`
- `PropertyAgeYears`
- `Condition`
- `SellerName`
- `BrokerName`
- `BrokerRegistration`

### Money

Value object:

- `Amount`
- `Currency`

Currencies should initially support at least:

- `ARS`
- `USD`

### ReviewDocument

Represents metadata for an uploaded document associated with a review.

Fields:

- `Id`
- `PropertyReviewId`
- `DocumentType`
- `OriginalFileName`
- `ContentType`
- `SizeBytes`
- `StorageKey`
- `UploadedAtUtc`
- `AnalysisStatus`
- `ExtractedSummary`

Document types:

- `ExpensesStatement`
- `ReservationAgreement`
- `MeetingMinutes`
- `PropertyTitle`
- `CoOwnershipRegulations`
- `FloorPlan`
- `TaxBill`
- `UtilityBill`
- `Other`

### ReviewFinding

Represents missing documentation, risk signals, or informational notes.

Fields:

- `Id`
- `PropertyReviewId`
- `FindingType`
- `Severity`
- `Code`
- `Title`
- `Description`
- `Recommendation`
- `Source`
- `CreatedAtUtc`

Finding types:

- `MissingDocumentation`
- `RiskSignal`
- `Information`

Severities:

- `Low`
- `Medium`
- `High`
- `Critical`

Sources:

- `RuleEngine`
- `DocumentAnalysis`
- `Manual`

### ChecklistItem

Represents buyer actions generated for a review.

Fields:

- `Id`
- `PropertyReviewId`
- `Title`
- `Description`
- `Category`
- `Priority`
- `IsCompleted`
- `CreatedAtUtc`

Categories:

- `Documentation`
- `Legal`
- `Financial`
- `Broker`
- `Building`
- `Visit`

### RiskAssessment

Owned entity or value object persisted with the review.

Fields:

- `Score`
- `Level`
- `Summary`
- `CalculatedAtUtc`

Risk levels:

- `Low`
- `Medium`
- `High`
- `Critical`

## Core Use Cases

### Create Property Review

Command: `CreatePropertyReviewCommand`

Responsibilities:

- Accept listing URL and/or manual property details.
- Validate at least one useful input is provided.
- Create review in `Draft` or `Submitted` status.
- Persist review.
- Return review ID and current status.

### Upload Review Document

Command: `UploadReviewDocumentCommand`

Responsibilities:

- Validate review exists.
- Validate file metadata and document type.
- Store file through `IDocumentStorage`.
- Persist document metadata.
- Mark document analysis as pending or not required for MVP.

### Run Review Analysis

Command: `AnalyzePropertyReviewCommand`

Responsibilities:

- Load review with documents.
- Evaluate missing documentation rules.
- Evaluate risk signal rules.
- Generate checklist items.
- Calculate risk score.
- Persist findings, checklist, and risk assessment.
- Mark review as `Completed` or `Failed`.

### Get Review Result

Query: `GetPropertyReviewResultQuery`

Responsibilities:

- Return review details.
- Return uploaded document metadata.
- Return missing documentation findings.
- Return risk signals.
- Return checklist.
- Return risk score and explanation.

### List Reviews

Query: `ListPropertyReviewsQuery`

Responsibilities:

- Return a paginated list of reviews.
- Include status, property summary, risk level, and timestamps.
- Useful for internal review and early product testing.

## Rule Engine

The MVP should use deterministic rule services before AI integration.

Application abstractions:

- `IMissingDocumentationRule`
- `IRiskSignalRule`
- `IChecklistRule`
- `IRiskScoringService`

Suggested initial rules:

- Apartment reviews should expect expenses documentation.
- Reservation-stage reviews should expect a reservation agreement.
- Reviews with no address or neighborhood should receive a location ambiguity warning.
- Reviews with no price should receive a high-severity missing price finding.
- Reviews with documents but no declared document type should receive an unknown document warning.
- Missing broker registration should create a checklist item.
- Missing seller identity should create a checklist item.

## Risk Scoring

Start with an explainable weighted model.

Suggested scoring:

- Base score: 15.
- Low severity finding: +5.
- Medium severity finding: +12.
- High severity finding: +25.
- Critical severity finding: +40.
- Cap at 100.

Suggested levels:

- `Low`: 0-24.
- `Medium`: 25-49.
- `High`: 50-74.
- `Critical`: 75-100.

The score response should include a summary generated from the highest-severity findings.

## Persistence

Use SQL Server with EF Core.

Main tables:

- `PropertyReviews`
- `ReviewDocuments`
- `ReviewFindings`
- `ChecklistItems`

Owned or complex persisted data:

- `PropertyDetails`
- `Money`
- `RiskAssessment`

Indexes:

- `PropertyReviews.Status`
- `PropertyReviews.CreatedAtUtc`
- `ReviewDocuments.PropertyReviewId`
- `ReviewFindings.PropertyReviewId`
- `ChecklistItems.PropertyReviewId`

## API Surface

Initial endpoints:

- `POST /api/property-reviews`
- `GET /api/property-reviews`
- `GET /api/property-reviews/{reviewId}`
- `POST /api/property-reviews/{reviewId}/documents`
- `POST /api/property-reviews/{reviewId}/analyze`
- `GET /api/property-reviews/{reviewId}/result`

The API should return problem details for validation and domain errors.

## Future AI Integration

The MVP should define interfaces but use deterministic implementations.

Future abstractions:

- `IDocumentAnalysisService`
- `IListingExtractionService`
- `IRiskDetectionAgent`
- `IChecklistGenerationService`

Expected future capabilities:

- Extract structured facts from uploaded documents.
- Classify uploaded documents automatically.
- Parse listing URLs and normalize property data.
- Detect inconsistencies between listing data and documents.
- Produce natural-language explanations for findings.

The Application layer should depend only on interfaces. Infrastructure can later add AI-backed adapters.

## Security and Compliance

- Treat uploaded documents as sensitive.
- Validate file size and content type.
- Avoid storing unnecessary personal data.
- Add audit timestamps to review, document, and analysis entities.
- Clearly label outputs as guidance, not legal advice.
- Plan for authentication before public launch.

## Testing Strategy

Domain tests:

- Risk score calculation.
- Risk level mapping.
- Rule behavior for common scenarios.

Application tests:

- Create review command validation.
- Upload document command behavior.
- Analyze review command orchestration.
- Get review result query shape.

API tests:

- Validation errors.
- Successful review creation.
- Result retrieval.

Infrastructure tests:

- EF Core mappings.
- Repository behavior if repositories are introduced.
- Document storage adapter behavior using a test implementation.
