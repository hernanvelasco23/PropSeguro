# PropSeguro Initial MVP Proposal

## Summary

PropSeguro is a buyer-side second-opinion platform for residential property purchases in Argentina. The MVP will let a prospective buyer submit a property listing or manual property details, optionally upload supporting documents, and receive a simple risk assessment with missing documentation, risk signals, and a practical buyer checklist.

The MVP should establish the core workflow, domain model, persistence model, API boundaries, and future-ready integration points for AI-assisted document analysis and risk detection.

## Problem

Home buyers in Argentina often reserve or buy properties with incomplete information. Important documents may be missing, risks may be overlooked, and buyers may not know which questions to ask before paying a reservation or signing an agreement.

The MVP addresses this by giving buyers a structured, plain-language review before they commit.

## Goals

- Allow users to create a property review from a listing URL or manually entered property data.
- Allow optional upload of documents such as expenses, reservation agreement, title-related notes, meeting minutes, and other supporting files.
- Detect missing documentation based on the review context.
- Identify common buyer risk signals.
- Generate a buyer checklist tailored to the property and submitted information.
- Return a simple risk score and explanation.
- Store review submissions, documents, detected findings, checklist items, and scoring results.
- Keep the architecture ready for future AI agents without coupling the MVP to a specific AI provider.

## Non-Goals

- No legal advice or binding legal opinion.
- No automated title search or official registry integration.
- No payment processing.
- No user-facing frontend implementation in this change.
- No real AI document analysis implementation in the first code slice.
- No scraping implementation unless explicitly added later; listing URLs are stored as inputs and may be parsed in the future.
- No multi-tenant enterprise administration.

## MVP Users

- Primary user: a home buyer evaluating an apartment or house before reservation or purchase.
- Secondary user: an internal operator or future advisor reviewing submitted cases.

## MVP Workflow

1. User creates a property review.
2. User provides either a property listing URL, manual property data, or both.
3. User optionally uploads documents.
4. System validates required review inputs.
5. System creates an initial review record.
6. System evaluates missing documentation from structured inputs and uploaded document types.
7. System detects rule-based risk signals.
8. System generates a buyer checklist.
9. System calculates a simple risk score.
10. User retrieves the review result.

## Inputs

### Listing Input

- Listing URL.
- Optional source name, such as Zonaprop, Argenprop, Mercado Libre, or other.

### Manual Property Data

- Property type.
- Address or neighborhood.
- City and province.
- Asking price and currency.
- Expenses amount and currency, when available.
- Surface area, rooms, bedrooms, bathrooms, age, and condition, when available.
- Operation type, such as sale or reservation review.
- Seller or broker information, when available.

### Uploaded Documents

- Expenses or expensas statement.
- Reservation agreement.
- Meeting minutes or acta.
- Property deed or title-related copy, if provided.
- Regulations or co-ownership rules, if provided.
- Floor plan, tax bill, utility bill, or other supporting document.

## Outputs

### Missing Documentation

The system should report expected documents that were not provided, grouped by importance.

Examples:

- Reservation agreement missing.
- Expenses statement missing.
- Co-ownership regulations missing for apartment building.
- Seller/broker details missing.

### Risk Signals

The system should report detected risk signals with severity and explanation.

Examples:

- High expenses relative to asking price or property type.
- Reservation agreement uploaded but not yet reviewed by a professional.
- Missing property address or ambiguous location.
- Currency or price missing.
- Document set is incomplete for an apartment purchase.

### Buyer Checklist

The system should generate actionable checklist items.

Examples:

- Confirm seller identity and authority to sell.
- Request latest expenses statement.
- Confirm outstanding debts, taxes, utilities, and building assessments.
- Ask broker for reservation cancellation terms.
- Review reservation agreement before paying.

### Risk Score

The MVP should return a simple score:

- Numeric score from 0 to 100.
- Risk level: Low, Medium, High, or Critical.
- Short explanation.

## Success Criteria

- A review can be created through the API using listing URL and/or manual data.
- Documents can be associated with a review.
- Missing documentation findings are generated from deterministic MVP rules.
- Risk signals are generated from deterministic MVP rules.
- A checklist is generated from deterministic MVP rules.
- A risk score is calculated and persisted.
- The application follows Clean Architecture and CQRS with MediatR.
- SQL Server persistence is implemented through EF Core.
- AI analysis can be added later through an application abstraction without changing the core workflow.

## Key Risks

- Legal and compliance framing must be clear: PropSeguro provides buyer guidance, not legal advice.
- Risk scoring must remain explainable and conservative.
- Document classification may be unreliable until AI or manual tagging is introduced.
- Listing URL parsing can become complex and should not block the MVP.

## Open Questions

- Should the MVP require user accounts, or allow anonymous review sessions?
- Which document types are mandatory for the first release?
- Should uploaded documents be stored in SQL Server, filesystem/blob storage, or a future cloud storage provider?
- What Argentine provinces or cities are in scope for first risk rules?
- Should internal operators be able to override findings and risk scores in the MVP?
