export type DocumentType =
  | 'Expenses'
  | 'AssemblyMinutes'
  | 'CoOwnershipRegulation'
  | 'DomainReport'
  | 'FloorPlan'
  | 'ReservationAgreement'
  | 'Other';

export type CreateAnalysisRequest = {
  title: string;
  listingUrl?: string | null;
  address: string;
  neighborhood: string;
  priceUsd?: number | null;
  monthlyExpenses?: number | null;
  totalSquareMeters?: number | null;
  coveredSquareMeters?: number | null;
  rooms?: number | null;
  bedrooms?: number | null;
  bathrooms?: number | null;
  buildingAgeYears?: number | null;
  floor?: number | null;
  hasBalcony?: boolean | null;
  hasAmenities?: boolean | null;
  isCreditEligible?: boolean | null;
  propertyCondition?: PropertyCondition | null;
  notes?: string | null;
};

export type UpdateAnalysisRequest = CreateAnalysisRequest;

export type PropertyCondition = 'New' | 'Excellent' | 'Good' | 'NeedsRenovation' | 'Unknown';

export type AddDocumentRequest = {
  documentType: DocumentType;
  file: File;
};

export type AnalyzeDocumentRequest = {
  extractedText?: string | null;
};

export type SaveDocumentTextRequest = {
  editedText?: string | null;
};

export type ListingImportRequest = {
  listingUrl: string;
};

export type ListingImportResult = {
  title?: string | null;
  priceUsd?: number | null;
  monthlyExpenses?: number | null;
  address?: string | null;
  neighborhood?: string | null;
  totalSquareMeters?: number | null;
  coveredSquareMeters?: number | null;
  rooms?: number | null;
  bedrooms?: number | null;
  bathrooms?: number | null;
  description?: string | null;
  imageUrls: string[];
  sourceUrl: string;
  confidenceScore: number;
  warnings: string[];
};

export type RiskFinding = {
  id: string;
  type: string;
  severity: string;
  code: string;
  title: string;
  description: string;
};

export type ChecklistItem = {
  id: string;
  documentType: DocumentType;
  title: string;
  description: string;
  isRequired: boolean;
  isProvided: boolean;
  status: string;
};

export type PropertyDocument = {
  id: string;
  documentType: DocumentType;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  storagePath: string;
  hasExtractedText: boolean;
  extractedText?: string | null;
  editedText?: string | null;
  textExtractionStatus: string;
  textExtractionQuality: string;
  lastAnalyzedTextSource?: string | null;
  uploadedAtUtc: string;
  status: string;
  analysisStatus: string;
  analyzedAtUtc?: string | null;
  analysisSummary?: string | null;
};

export type DocumentFinding = {
  id: string;
  propertyDocumentId: string;
  severity: string;
  category: string;
  title: string;
  description: string;
  buyerImpact?: string | null;
  suggestedQuestion?: string | null;
  suggestedChange?: string | null;
  suggestedNegotiationPoint?: string | null;
  matchedText?: string | null;
  createdAtUtc: string;
};

export type NegotiationAnalysis = {
  publishedPriceUsd?: number | null;
  usdPerSquareMeter?: number | null;
  benchmarkUsdPerSquareMeter?: number | null;
  marketPosition: 'Cheap' | 'Fair' | 'Expensive' | 'Overpriced' | 'Unknown';
  estimatedFairValueMinUsd?: number | null;
  estimatedFairValueMaxUsd?: number | null;
  suggestedFirstOfferUsd?: number | null;
  suggestedTargetPriceUsd?: number | null;
  negotiationMarginPercent?: number | null;
  explanation: string;
};

export type PropertyAnalysis = {
  id: string;
  title: string;
  listingUrl?: string | null;
  address: string;
  neighborhood: string;
  priceUsd?: number | null;
  monthlyExpenses?: number | null;
  totalSquareMeters?: number | null;
  coveredSquareMeters?: number | null;
  rooms?: number | null;
  bedrooms?: number | null;
  bathrooms?: number | null;
  buildingAgeYears?: number | null;
  floor?: number | null;
  hasBalcony?: boolean | null;
  hasAmenities?: boolean | null;
  isCreditEligible?: boolean | null;
  propertyCondition: PropertyCondition;
  notes?: string | null;
  opportunityScore: number;
  protectionScore: number;
  initialRiskLevel: string;
  recommendedUse: string;
  usdPerSquareMeter?: number | null;
  benchmarkComparison: string;
  marketPosition: 'Cheap' | 'Fair' | 'Expensive' | 'Overpriced' | 'Unknown';
  estimatedFairValueMinUsd?: number | null;
  estimatedFairValueMaxUsd?: number | null;
  suggestedFirstOfferUsd?: number | null;
  suggestedTargetPriceUsd?: number | null;
  negotiationMarginPercent?: number | null;
  negotiationExplanation: string;
  negotiationAnalysis: NegotiationAnalysis;
  coverImageFileName?: string | null;
  coverImageContentType?: string | null;
  coverImageSizeBytes?: number | null;
  coverImageStoragePath?: string | null;
  coverImageUploadedAtUtc?: string | null;
  coverImageUrl?: string | null;
  status: string;
  riskFindings: RiskFinding[];
  documents: PropertyDocument[];
  checklistItems: ChecklistItem[];
  documentFindings: DocumentFinding[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type PropertyAnalysisSummary = {
  id: string;
  title: string;
  listingUrl?: string | null;
  address: string;
  neighborhood: string;
  priceUsd?: number | null;
  monthlyExpenses?: number | null;
  totalSquareMeters?: number | null;
  usdPerSquareMeter?: number | null;
  opportunityScore: number;
  protectionScore: number;
  recommendedUse: string;
  initialRiskLevel: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  missingRequiredDocumentsCount: number;
  findingsCount: number;
  criticalFindingsCount: number;
  coverImageUrl?: string | null;
  negotiationAnalysis: NegotiationAnalysis;
};

export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5288').replace(/\/$/, '');

export function resolveApiUrl(path?: string | null) {
  if (!path) {
    return null;
  }

  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  return `${API_BASE_URL}${path.startsWith('/') ? path : `/${path}`}`;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const isMultipart = init?.body instanceof FormData;
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: isMultipart
      ? init?.headers
      : {
          'Content-Type': 'application/json',
          ...(init?.headers ?? {})
        },
    ...init
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(body || `Request failed with status ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export function createAnalysis(payload: CreateAnalysisRequest) {
  return request<PropertyAnalysis>('/api/property-analyses', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
}

export function getAnalysis(id: string) {
  return request<PropertyAnalysis>(`/api/property-analyses/${id}`);
}

export function listAnalyses() {
  return request<PropertyAnalysisSummary[]>('/api/property-analyses');
}

export function updateAnalysis(id: string, payload: UpdateAnalysisRequest) {
  return request<PropertyAnalysis>(`/api/property-analyses/${id}`, {
    method: 'PUT',
    body: JSON.stringify(payload)
  });
}

export function deleteAnalysis(id: string) {
  return request<void>(`/api/property-analyses/${id}`, {
    method: 'DELETE'
  });
}

export function uploadCoverImage(analysisId: string, file: File) {
  const formData = new FormData();
  formData.append('file', file);

  return request<PropertyAnalysis>(`/api/property-analyses/${analysisId}/cover-image`, {
    method: 'POST',
    body: formData
  });
}

export function importListing(payload: ListingImportRequest) {
  return request<ListingImportResult>('/api/listing-imports', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
}

export function addDocument(analysisId: string, payload: AddDocumentRequest) {
  const formData = new FormData();
  formData.append('documentType', payload.documentType);
  formData.append('file', payload.file);

  return request<PropertyAnalysis>(`/api/property-analyses/${analysisId}/documents`, {
    method: 'POST',
    body: formData
  });
}

export function analyzeDocument(analysisId: string, documentId: string, payload: AnalyzeDocumentRequest) {
  return request(`/api/property-analyses/${analysisId}/documents/${documentId}/analyze`, {
    method: 'POST',
    body: JSON.stringify(payload)
  });
}

export function saveDocumentText(analysisId: string, documentId: string, payload: SaveDocumentTextRequest) {
  return request<PropertyDocument>(`/api/property-analyses/${analysisId}/documents/${documentId}/text`, {
    method: 'PUT',
    body: JSON.stringify(payload)
  });
}
