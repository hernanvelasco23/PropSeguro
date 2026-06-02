# PropSeguro Initial MVP Tasks

## 1. Solution Setup

- [ ] Create .NET 9 solution.
- [ ] Add projects for Domain, Application, Infrastructure, API, and tests.
- [ ] Configure project references according to Clean Architecture dependency direction.
- [ ] Add baseline NuGet packages:
  - [ ] ASP.NET Core Web API.
  - [ ] MediatR.
  - [ ] FluentValidation.
  - [ ] Entity Framework Core.
  - [ ] SQL Server provider.
  - [ ] Swashbuckle or built-in OpenAPI support.
  - [ ] Test framework and assertion library.
- [ ] Add shared build settings and nullable reference types.

## 2. Domain Layer

- [ ] Create `PropertyReview` aggregate.
- [ ] Create `ReviewDocument` entity.
- [ ] Create `ReviewFinding` entity.
- [ ] Create `ChecklistItem` entity.
- [ ] Create `PropertyDetails`, `Money`, and `RiskAssessment` value objects.
- [ ] Add enums for review status, property type, operation type, document type, finding type, severity, finding source, checklist category, checklist priority, currency, and risk level.
- [ ] Add domain methods for:
  - [ ] Adding documents.
  - [ ] Replacing generated findings.
  - [ ] Replacing generated checklist items.
  - [ ] Completing analysis with a risk assessment.
  - [ ] Marking analysis failure.
- [ ] Add domain tests for entity invariants and risk level mapping.

## 3. Application Layer

- [ ] Add MediatR pipeline setup.
- [ ] Add validation pipeline behavior.
- [ ] Define application interfaces:
  - [ ] `IApplicationDbContext`.
  - [ ] `IDocumentStorage`.
  - [ ] `IMissingDocumentationAnalyzer`.
  - [ ] `IRiskSignalAnalyzer`.
  - [ ] `IChecklistGenerator`.
  - [ ] `IRiskScoringService`.
  - [ ] Future-facing `IDocumentAnalysisService`.
  - [ ] Future-facing `IListingExtractionService`.
- [ ] Implement `CreatePropertyReviewCommand`.
- [ ] Implement `UploadReviewDocumentCommand`.
- [ ] Implement `AnalyzePropertyReviewCommand`.
- [ ] Implement `GetPropertyReviewResultQuery`.
- [ ] Implement `ListPropertyReviewsQuery`.
- [ ] Add DTOs for create review, document metadata, findings, checklist items, and review result.
- [ ] Add application tests for command and query handlers.

## 4. MVP Rule Services

- [ ] Implement missing documentation analyzer.
- [ ] Implement risk signal analyzer.
- [ ] Implement checklist generator.
- [ ] Implement risk scoring service.
- [ ] Add rules for missing expenses document on apartment reviews.
- [ ] Add rules for missing reservation agreement on reservation-stage reviews.
- [ ] Add rules for missing or ambiguous location.
- [ ] Add rules for missing asking price.
- [ ] Add checklist items for seller identity, broker registration, tax/debt checks, expenses, and reservation terms.
- [ ] Add tests for each rule and score calculation.

## 5. Infrastructure Layer

- [ ] Create EF Core `DbContext`.
- [ ] Configure SQL Server provider.
- [ ] Add EF Core mappings for aggregate and owned value objects.
- [ ] Add migrations.
- [ ] Implement `IApplicationDbContext`.
- [ ] Implement local development `IDocumentStorage`.
- [ ] Add no-op or deterministic placeholder implementations for future AI-facing interfaces.
- [ ] Add database seed data only if useful for local testing.
- [ ] Add infrastructure tests for EF mappings.

## 6. API Layer

- [ ] Create Web API project.
- [ ] Register Domain, Application, and Infrastructure services.
- [ ] Configure OpenAPI.
- [ ] Configure problem details responses.
- [ ] Add property review endpoints:
  - [ ] `POST /api/property-reviews`.
  - [ ] `GET /api/property-reviews`.
  - [ ] `GET /api/property-reviews/{reviewId}`.
  - [ ] `POST /api/property-reviews/{reviewId}/documents`.
  - [ ] `POST /api/property-reviews/{reviewId}/analyze`.
  - [ ] `GET /api/property-reviews/{reviewId}/result`.
- [ ] Add request validation and error mapping.
- [ ] Add API tests for main happy paths and validation failures.

## 7. Document Upload Handling

- [ ] Define allowed file types for MVP.
- [ ] Define max file size.
- [ ] Add document type selection to upload request.
- [ ] Store original file name, content type, file size, storage key, and upload timestamp.
- [ ] Ensure uploaded file content is not returned by result endpoints.
- [ ] Add tests for invalid file type and oversized upload behavior.

## 8. Data and Configuration

- [ ] Add SQL Server connection string configuration.
- [ ] Add document storage root configuration for local development.
- [ ] Add environment-specific settings.
- [ ] Add database migration instructions.
- [ ] Add local development startup instructions.

## 9. Product and Legal Copy

- [ ] Add standard disclaimer that PropSeguro provides guidance, not legal advice.
- [ ] Add short explanations for risk levels.
- [ ] Add user-friendly finding titles and recommendations.
- [ ] Add checklist language tailored to Argentine property purchases.

## 10. Future AI Integration Preparation

- [ ] Keep document analysis behind `IDocumentAnalysisService`.
- [ ] Keep listing parsing behind `IListingExtractionService`.
- [ ] Keep risk detection extension points separate from deterministic MVP rules.
- [ ] Store enough document metadata to support later re-analysis.
- [ ] Consider storing extracted facts and agent trace data in a later migration.

## 11. Acceptance Criteria

- [ ] A user can create a review with a listing URL, manual property data, or both.
- [ ] A user can upload one or more documents for a review.
- [ ] The system can analyze a review using deterministic MVP rules.
- [ ] The system returns missing documentation findings.
- [ ] The system returns risk signals.
- [ ] The system returns a buyer checklist.
- [ ] The system returns a persisted risk score and risk level.
- [ ] The solution builds and tests pass.
- [ ] The design remains ready for future AI-backed analysis without changing the public API shape unnecessarily.
