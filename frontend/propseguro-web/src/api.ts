import { API_BASE_URL } from './config';

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
  apartmentUnit?: string | null;
  unitIdentifier?: string | null;
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

export type UserRole = 'Admin' | 'User';
export type UserStatus = 'Invited' | 'Active' | 'Disabled';

export type AuthUser = {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  status: UserStatus;
  createdAtUtc: string;
  lastLoginAtUtc?: string | null;
};

export type LoginResponse = {
  token: string;
  user: AuthUser;
};

export type BetaAccessRequest = {
  id: string;
  email: string;
  fullName: string;
  phone?: string | null;
  intendedUse?: string | null;
  message?: string | null;
  status: 'Pending' | 'Approved' | 'Rejected';
  createdAtUtc: string;
};

export type CreatedBetaAccessRequest = {
  id: string;
  status: 'Pending' | 'Approved' | 'Rejected';
};

export type ApproveBetaAccessResponse = {
  requestId: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  userId: string;
  email: string;
  emailSent: boolean;
  emailError?: string | null;
  message?: string | null;
  temporaryPassword?: string | null;
};

export type CreateBetaAccessRequest = {
  email: string;
  fullName: string;
  phone?: string | null;
  intendedUse?: string | null;
  message?: string | null;
};

export type CreateUserRequest = {
  email: string;
  fullName: string;
  password: string;
  role: UserRole;
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
  expenseValidation?: ExpenseDocumentValidation | null;
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
  maxRecommendedPriceUsd?: number | null;
  negotiationMarginPercent?: number | null;
  explanation: string;
};

export type ExpenseIntelligenceAnalysis = {
  buildingHealthScore: number;
  classification: string;
  summary: string;
  findings: DocumentFinding[];
  suggestedQuestions: string[];
  suggestedNegotiationPoints: string[];
};

export type ExpenseDocumentValidation = {
  detectedFloor?: number | null;
  detectedApartmentUnit?: string | null;
  detectedUnitIdentifier?: string | null;
  detectedExpenseAmount?: number | null;
  extractionConfidence: string;
  confidenceScore: number;
  classification: 'Validated' | 'PartiallyValidated' | 'NotValidated' | 'Mismatch';
  label: string;
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
  apartmentUnit?: string | null;
  unitIdentifier?: string | null;
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
  maxRecommendedPriceUsd?: number | null;
  negotiationMarginPercent?: number | null;
  negotiationExplanation: string;
  negotiationAnalysis: NegotiationAnalysis;
  coverImageFileName?: string | null;
  coverImageContentType?: string | null;
  coverImageSizeBytes?: number | null;
  coverImageUploadedAtUtc?: string | null;
  coverImageUrl?: string | null;
  status: string;
  riskFindings: RiskFinding[];
  documents: PropertyDocument[];
  checklistItems: ChecklistItem[];
  documentFindings: DocumentFinding[];
  expenseIntelligence?: ExpenseIntelligenceAnalysis | null;
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

export type PropertyReport = {
  propertySummary: PropertyReportSummary;
  reportStatus: string;
  reportStatusLabel: string;
  coverSection: PropertyReportCoverSection;
  executiveSummary: string;
  executiveConclusion: string;
  overallRecommendation: string;
  overallRecommendationLabel: string;
  overallRecommendationExplanation: string;
  opportunitySection: PropertyReportOpportunitySection;
  protectionSection: PropertyReportProtectionSection;
  negotiationSection: PropertyReportNegotiationSection;
  documentsSection: PropertyReportDocumentsSection;
  reservationSection: PropertyReportDocumentTypeSection;
  expensesSection: PropertyReportExpensesSection;
  findingsSection: PropertyReportFindingsSection;
  whatThisMeansForYou: string[];
  conditionsToAdvance: string[];
  moneyRisks: PropertyReportMoneyRisk[];
  negotiationArguments: string[];
  questionsForSellerOrAgent: string[];
  questionsForNotaryOrLawyer: string[];
  reviewedDocuments: PropertyReportDocumentStatus[];
  pendingDocuments: PropertyReportDocumentStatus[];
  suggestedQuestions: string[];
  suggestedNegotiationPoints: string[];
  nextSteps: string[];
  disclaimer: string;
  generatedAtUtc: string;
};

export type PropertyReportSummary = {
  title: string;
  address: string;
  neighborhood: string;
  priceUsd?: number | null;
  totalSquareMeters?: number | null;
  usdPerSquareMeter?: number | null;
  monthlyExpenses?: number | null;
  floor?: number | null;
  apartmentUnit?: string | null;
  unitIdentifier?: string | null;
};

export type PropertyReportCoverSection = {
  title: string;
  subtitle: string;
  propertyTitle: string;
  address: string;
  neighborhood: string;
  priceUsd?: number | null;
  totalSquareMeters?: number | null;
  usdPerSquareMeter?: number | null;
  monthlyExpenses?: number | null;
  coverImageUrl?: string | null;
  reportStatus: string;
  reportStatusLabel: string;
  generatedAtUtc: string;
};

export type PropertyReportOpportunitySection = {
  opportunityScore: number;
  recommendedUse: string;
  recommendedUseLabel: string;
  initialRiskLevel: string;
  initialRiskLevelLabel: string;
  benchmarkComparison: string;
  explanation: string;
};

export type PropertyReportProtectionSection = {
  protectionScore: number;
  documentConfidenceSummary: string;
  missingDocumentsCount: number;
  highOrCriticalFindingsCount: number;
  explanation: string;
};

export type PropertyReportNegotiationSection = {
  publishedPriceUsd?: number | null;
  estimatedFairValueMinUsd?: number | null;
  estimatedFairValueMaxUsd?: number | null;
  suggestedFirstOfferUsd?: number | null;
  suggestedTargetPriceUsd?: number | null;
  maxRecommendedPriceUsd?: number | null;
  negotiationMarginPercent?: number | null;
  marketPosition: string;
  marketPositionLabel: string;
  explanation: string;
};

export type PropertyReportDocumentsSection = {
  requiredDocumentsCount: number;
  providedDocumentsCount: number;
  missingDocumentsCount: number;
  checklistItems: { title: string; documentType: string; isRequired: boolean; isProvided: boolean; status: string }[];
};

export type PropertyReportDocumentTypeSection = {
  isAvailable: boolean;
  isAnalyzed: boolean;
  status: string;
  riskLevel?: string | null;
  keyFindings: DocumentFinding[];
  suggestedQuestions: string[];
  suggestedNegotiationPoints: string[];
};

export type PropertyReportExpensesSection = PropertyReportDocumentTypeSection & {
  buildingHealthScore?: number | null;
  buildingHealthClassification?: string | null;
  documentConfidence?: ExpenseDocumentValidation | null;
  detectedExpenseAmount?: number | null;
  publishedMonthlyExpenses?: number | null;
  totalSquareMeters?: number | null;
  monthlyExpensesPerSquareMeter?: number | null;
  benchmarkMonthlyExpensesPerSquareMeter?: number | null;
  expenseBenchmarkComparison: string;
  unitValidationStatus?: string | null;
};

export type PropertyReportFindingsSection = {
  groups: { severity: string; severityLabel: string; findings: DocumentFinding[] }[];
};

export type PropertyReportMoneyRisk = {
  title: string;
  whyItMatters: string;
  buyerImpact: string;
  suggestedAction: string;
  sourceDocumentType?: string | null;
  sourceDocumentLabel?: string | null;
};

export type PropertyReportDocumentStatus = {
  documentType: string;
  documentLabel: string;
  status: string;
  isAnalyzed: boolean;
  isFuture: boolean;
};

const AUTH_TOKEN_KEY = 'propsegura.authToken';
const AUTH_USER_KEY = 'propsegura.authUser';
export const AUTH_SESSION_EXPIRED_EVENT = 'propsegura:session-expired';

export function getStoredAuthToken() {
  return window.localStorage.getItem(AUTH_TOKEN_KEY);
}

type JwtPayload = {
  exp?: number;
};

export function getJwtExpiration(token: string | null) {
  if (!token) {
    return null;
  }

  try {
    const [, payload] = token.split('.');

    if (!payload) {
      return null;
    }

    const normalizedPayload = payload.replace(/-/g, '+').replace(/_/g, '/');
    const paddedPayload = normalizedPayload.padEnd(normalizedPayload.length + ((4 - normalizedPayload.length % 4) % 4), '=');
    const decoded = JSON.parse(window.atob(paddedPayload)) as JwtPayload;

    return typeof decoded.exp === 'number'
      ? decoded.exp * 1000
      : null;
  } catch {
    return null;
  }
}

export function isAuthTokenExpired(token = getStoredAuthToken()) {
  const expiration = getJwtExpiration(token);

  if (!expiration) {
    return true;
  }

  return expiration <= Date.now() + 30_000;
}

export function getValidStoredAuthToken() {
  const token = getStoredAuthToken();

  if (!token) {
    return null;
  }

  if (isAuthTokenExpired(token)) {
    notifySessionExpired();
    return null;
  }

  return token;
}

export function getStoredAuthUser(): AuthUser | null {
  const stored = window.localStorage.getItem(AUTH_USER_KEY);

  if (!stored) {
    return null;
  }

  try {
    return JSON.parse(stored) as AuthUser;
  } catch {
    clearStoredAuth();
    return null;
  }
}

export function storeAuthSession(session: LoginResponse) {
  window.localStorage.setItem(AUTH_TOKEN_KEY, session.token);
  window.localStorage.setItem(AUTH_USER_KEY, JSON.stringify(session.user));
}

export function clearStoredAuth() {
  window.localStorage.removeItem(AUTH_TOKEN_KEY);
  window.localStorage.removeItem(AUTH_USER_KEY);
}

function notifySessionExpired() {
  clearStoredAuth();
  window.dispatchEvent(new CustomEvent(AUTH_SESSION_EXPIRED_EVENT));
}

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
  const token = path === '/api/auth/login' ? null : getValidStoredAuthToken();
  const headers = new Headers(init?.headers);

  if (!isMultipart && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
  });

  if (response.status === 401) {
    if (path === '/api/auth/login') {
      throw new Error('Email o contraseña inválidos.');
    }

    notifySessionExpired();
    throw new Error('Tu sesión expiró. Volvé a ingresar para continuar.');
  }

  if (!response.ok) {
    const body = await response.text();
    throw new Error(body || `Request failed with status ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

async function requestBlob(path: string, init?: RequestInit): Promise<Blob> {
  const token = getValidStoredAuthToken();
  const headers = new Headers(init?.headers);

  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
  });

  if (response.status === 401) {
    notifySessionExpired();
    throw new Error('Tu sesión expiró. Volvé a ingresar para continuar.');
  }

  if (!response.ok) {
    const body = await response.text();
    throw new Error(body || `Request failed with status ${response.status}`);
  }

  return response.blob();
}

export function login(email: string, password: string) {
  return request<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password })
  });
}

export function createBetaAccessRequest(payload: CreateBetaAccessRequest) {
  return request<CreatedBetaAccessRequest>('/api/beta-access-requests', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
}

export function listBetaAccessRequests() {
  return request<BetaAccessRequest[]>('/api/admin/beta-access-requests');
}

export function approveBetaAccessRequest(id: string) {
  return request<ApproveBetaAccessResponse>(`/api/admin/beta-access-requests/${id}/approve`, {
    method: 'POST'
  });
}

export function rejectBetaAccessRequest(id: string) {
  return request<BetaAccessRequest>(`/api/admin/beta-access-requests/${id}/reject`, {
    method: 'POST'
  });
}

export function listUsers() {
  return request<AuthUser[]>('/api/admin/users');
}

export function createUser(payload: CreateUserRequest) {
  return request<AuthUser>('/api/admin/users', {
    method: 'POST',
    body: JSON.stringify(payload)
  });
}

export function enableUser(id: string) {
  return request<AuthUser>(`/api/admin/users/${id}/enable`, {
    method: 'PATCH'
  });
}

export function disableUser(id: string) {
  return request<AuthUser>(`/api/admin/users/${id}/disable`, {
    method: 'PATCH'
  });
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

export function getPropertyReport(id: string) {
  return request<PropertyReport>(`/api/property-analyses/${id}/report`);
}

export function downloadPropertyReportPdf(id: string) {
  return requestBlob(`/api/property-analyses/${id}/report/pdf`);
}

export async function createProtectedAssetUrl(path: string) {
  const blob = await requestBlob(path);
  return URL.createObjectURL(blob);
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
