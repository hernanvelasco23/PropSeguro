import { CSSProperties, FormEvent, ReactNode, useEffect, useState } from 'react';
import {
  AuthUser,
  AUTH_SESSION_EXPIRED_EVENT,
  BetaAccessRequest,
  ChecklistItem,
  CreateBetaAccessRequest,
  CreateUserRequest,
  DocumentFinding,
  DocumentType,
  ExpenseDocumentValidation,
  ExpenseIntelligenceAnalysis,
  PropertyAnalysis,
  PropertyReport,
  PropertyAnalysisSummary,
  PropertyCondition,
  PropertyDocument,
  UpdateAnalysisRequest,
  approveBetaAccessRequest,
  addDocument,
  analyzeDocument,
  clearStoredAuth,
  confirmDocumentMatch,
  createProtectedAssetUrl,
  createAnalysis,
  createBetaAccessRequest,
  createUser,
  deleteAnalysis,
  downloadPropertyReportPdf,
  disableUser,
  enableUser,
  getAnalysis,
  getPropertyReport,
  getStoredAuthUser,
  getValidStoredAuthToken,
  importListing,
  listBetaAccessRequests,
  listAnalyses,
  listUsers,
  login,
  rejectBetaAccessRequest,
  saveDocumentText,
  storeAuthSession,
  updateAnalysis,
  uploadCoverImage
} from './api';
import propertyPlaceholder from './assets/property-placeholder.svg';

const documentTypes: DocumentType[] = [
  'Expenses',
  'ReservationAgreement',
  'AssemblyMinutes',
  'FloorPlan',
  'DomainReport',
  'Other'
];

const availableDocumentTypes: DocumentType[] = ['Expenses', 'ReservationAgreement'];
const futureDocumentTypes: DocumentType[] = ['AssemblyMinutes', 'FloorPlan', 'DomainReport', 'Other'];

const documentTypeLabels: Record<DocumentType, string> = {
  Expenses: 'Últimas expensas',
  AssemblyMinutes: 'Actas de asamblea',
  CoOwnershipRegulation: 'Reglamento de copropiedad',
  DomainReport: 'Informe de dominio',
  FloorPlan: 'Plano',
  ReservationAgreement: 'Reserva o seña',
  Other: 'Otro'
};

const propertyConditionLabels: Record<PropertyCondition, string> = {
  New: 'A estrenar',
  Excellent: 'Excelente',
  Good: 'Bueno',
  NeedsRenovation: 'A refaccionar',
  Unknown: 'Sin dato'
};

type CreateFormState = {
  title: string;
  listingUrl: string;
  address: string;
  neighborhood: string;
  priceUsd: string;
  monthlyExpenses: string;
  totalSquareMeters: string;
  coveredSquareMeters: string;
  rooms: string;
  bedrooms: string;
  bathrooms: string;
  buildingAgeYears: string;
  floor: string;
  apartmentUnit: string;
  unitIdentifier: string;
  hasBalcony: string;
  hasAmenities: string;
  isCreditEligible: string;
  propertyCondition: PropertyCondition;
  notes: string;
};

type ChecklistStats = {
  required: number;
  provided: number;
  percent: number;
};

type DashboardSort =
  | 'newest'
  | 'title'
  | 'neighborhood'
  | 'highestOpportunity'
  | 'highestProtection'
  | 'bestInvestment'
  | 'bestLiving'
  | 'lowestPrice'
  | 'totalSquareMeters'
  | 'recommendedUse'
  | 'lowestUsdPerSquareMeter';

type SortDirection = 'asc' | 'desc';

type ApprovalNotice = {
  message: string;
  detail?: string | null;
  temporaryPassword?: string | null;
  copied?: boolean;
  tone: 'success' | 'warning' | 'info' | 'neutral';
};

type WorkspaceTab = 'general' | 'price' | 'documents' | 'observations' | 'findings' | 'report';

const visitObservationOptions = [
  'Humedad',
  'Grietas',
  'Olor a humedad',
  'Problemas eléctricos',
  'Problemas de agua',
  'Falta de ventilación',
  'Falta de luz natural',
  'Ruidos',
  'Ascensor deficiente',
  'Fachada deteriorada',
  'Otro'
];

type DocumentOperationStage = 'uploading' | 'extracting' | 'ready' | 'failed';

type DocumentOperation = {
  documentType: DocumentType;
  stage: DocumentOperationStage;
  message: string;
};

const emptyCreateForm: CreateFormState = {
  title: '',
  listingUrl: '',
  address: '',
  neighborhood: '',
  priceUsd: '',
  monthlyExpenses: '',
  totalSquareMeters: '',
  coveredSquareMeters: '',
  rooms: '',
  bedrooms: '',
  bathrooms: '',
  buildingAgeYears: '',
  floor: '',
  apartmentUnit: '',
  unitIdentifier: '',
  hasBalcony: '',
  hasAmenities: '',
  isCreditEligible: '',
  propertyCondition: 'Unknown',
  notes: ''
};

export function App() {
  const [path, setPath] = useState(() => window.location.pathname);
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(() => getStoredAuthUser());
  const [analysis, setAnalysis] = useState<PropertyAnalysis | null>(null);
  const [analyses, setAnalyses] = useState<PropertyAnalysisSummary[]>([]);
  const [initializing, setInitializing] = useState(true);
  const [pendingDetailTab, setPendingDetailTab] = useState<WorkspaceTab | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    function handlePopState() {
      setPath(window.location.pathname);
    }

    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  useEffect(() => {
    const token = getValidStoredAuthToken();

    if (!token) {
      setCurrentUser(null);

      if (isProtectedPath(window.location.pathname)) {
        window.sessionStorage.setItem('propsegura.returnTo', window.location.pathname);
        navigate('/login');
      }
    }

    setInitializing(false);
  }, []);

  useEffect(() => {
    function handleSessionExpired() {
      setCurrentUser(null);
      setAnalysis(null);
      setAnalyses([]);
      if (isProtectedPath(window.location.pathname)) {
        window.sessionStorage.setItem('propsegura.returnTo', window.location.pathname);
      }
      setError('Tu sesión expiró. Volvé a ingresar para continuar.');
      navigate('/login');
    }

    window.addEventListener(AUTH_SESSION_EXPIRED_EVENT, handleSessionExpired);
    return () => window.removeEventListener(AUTH_SESSION_EXPIRED_EVENT, handleSessionExpired);
  }, []);

  useEffect(() => {
    if (initializing) {
      return;
    }

    if (isProtectedPath(path) && !currentUser) {
      window.sessionStorage.setItem('propsegura.returnTo', path);
      navigate('/login');
      return;
    }

    if (path.startsWith('/admin') && currentUser?.role !== 'Admin') {
      navigate('/properties');
      return;
    }

    if (path === '/properties') {
      void refreshDashboard();
      return;
    }

    const id = getPropertyIdFromPath(path);
    if (id) {
      void loadAnalysis(id);
    }
  }, [path, currentUser, initializing]);

  function navigate(to: string) {
    window.history.pushState(null, '', to);
    setPath(to);
  }

  function navigateToLandingSection(sectionId?: string) {
    navigate('/');

    if (!sectionId) {
      window.scrollTo({ top: 0, behavior: 'smooth' });
      return;
    }

    window.setTimeout(() => {
      document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 0);
  }

  async function refreshDashboard() {
    setLoading(true);
    setError(null);
    try {
      setAnalyses(await listAnalyses());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar el panel.');
    } finally {
      setLoading(false);
    }
  }

  async function loadAnalysis(id: string) {
    setLoading(true);
    setError(null);
    try {
      setAnalysis(await getAnalysis(id));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo abrir el análisis.');
      setAnalysis(null);
    } finally {
      setLoading(false);
    }
  }

  async function refreshAnalysis(id = analysis?.id) {
    if (!id) {
      return;
    }

    setLoading(true);
    setError(null);
    try {
      setAnalysis(await getAnalysis(id));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar el análisis.');
    } finally {
      setLoading(false);
    }
  }

  async function handleCreated(created: PropertyAnalysis) {
    setAnalysis(created);
    setPendingDetailTab('documents');
    setNotice('Propiedad guardada correctamente. Te llevamos a Documentos para continuar.');
    navigate(`/properties/${created.id}`);
    await refreshDashboard();
  }

  async function openAnalysis(id: string) {
    setLoading(true);
    setError(null);
    try {
      setAnalysis(await getAnalysis(id));
      navigate(`/properties/${id}`);
      await refreshDashboard();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo abrir el análisis.');
    } finally {
      setLoading(false);
    }
  }

  async function handleDeleteAnalysis(id: string) {
    setLoading(true);
    setError(null);
    try {
      await deleteAnalysis(id);
      setAnalyses((current) => current.filter((item) => item.id !== id));

      if (analysis?.id === id) {
        setAnalysis(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo eliminar la propiedad.');
    } finally {
      setLoading(false);
    }
  }

  function handleLoginSuccess(user: AuthUser) {
    setCurrentUser(user);
    const returnTo = window.sessionStorage.getItem('propsegura.returnTo');
    window.sessionStorage.removeItem('propsegura.returnTo');
    navigate(returnTo && isProtectedPath(returnTo) ? returnTo : '/properties');
  }

  function handleLogout() {
    clearStoredAuth();
    setCurrentUser(null);
    setAnalysis(null);
    setAnalyses([]);
    navigate('/');
  }

  const detailId = getPropertyIdFromPath(path);

  if (initializing) {
    return (
      <main className="app-shell">
        <InitialLoadingScreen />
      </main>
    );
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <button className="brand-button" type="button" onClick={() => navigateToLandingSection()}>
          <span className="brand-mark">PS</span>
          <span>PropSegura</span>
        </button>
        <nav className="topnav" aria-label="Navegación principal">
          <button className={path === '/' ? 'topnav-link active' : 'topnav-link'} type="button" onClick={() => navigateToLandingSection()}>
            Inicio
          </button>
          <button className="topnav-link" type="button" onClick={() => navigateToLandingSection('process')}>
            Cómo funciona
          </button>
          <button className="topnav-link" type="button" onClick={() => navigateToLandingSection('guarantee')}>
            Garantía
          </button>
          <button className="topnav-link" type="button" onClick={() => navigateToLandingSection('faq')}>
            Preguntas frecuentes
          </button>
          <button className="topnav-link" type="button" onClick={() => navigateToLandingSection('contact')}>
            Contacto
          </button>
          {currentUser ? (
            <>
              <button className={path === '/properties' ? 'topnav-link active' : 'topnav-link'} type="button" onClick={() => navigate('/properties')}>
                Propiedades
              </button>
              <button className={path === '/properties/new' ? 'topnav-link active' : 'topnav-link'} type="button" onClick={() => navigate('/properties/new')}>
                Nueva propiedad
              </button>
              {currentUser.role === 'Admin' && (
                <button className={path === '/admin' ? 'topnav-link active' : 'topnav-link'} type="button" onClick={() => navigate('/admin')}>
                  Admin
                </button>
              )}
              <button className="topbar-cta" type="button" onClick={handleLogout}>
                Salir
              </button>
            </>
          ) : (
            <>
              <button className={path === '/solicitar-acceso' ? 'topnav-link active' : 'topnav-link'} type="button" onClick={() => navigate('/solicitar-acceso')}>
                Solicitar acceso
              </button>
              <button className={path === '/login' ? 'topbar-cta active' : 'topbar-cta'} type="button" onClick={() => navigate('/login')}>
                Ingresar
              </button>
            </>
          )}
        </nav>
      </header>

      {loading && (
        <div className="top-loading" role="status" aria-live="polite">
          <span className="loading-dot" aria-hidden="true" />
          Procesando...
        </div>
      )}

      {error && (
        <div className="banner-error" role="alert">
          <span>{error}</span>
          <button className="banner-close" type="button" onClick={() => setError(null)}>
            Cerrar
          </button>
        </div>
      )}

      {notice && (
        <div className="banner-success" role="status" aria-live="polite">
          <span>{notice}</span>
          <button className="banner-close" type="button" onClick={() => setNotice(null)}>
            Cerrar
          </button>
        </div>
      )}

      {path === '/' && (
        <LandingPage
          onCreate={() => navigate('/properties/new')}
          onDashboard={() => navigate('/properties')}
          onRequestAccess={() => navigate('/solicitar-acceso')}
        />
      )}
      {path === '/login' && (
        <LoginPage
          onLogin={handleLoginSuccess}
          onRequestAccess={() => navigate('/solicitar-acceso')}
          onError={setError}
          setLoading={setLoading}
        />
      )}
      {path === '/solicitar-acceso' && (
        <BetaAccessPage
          onLogin={() => navigate('/login')}
          onError={setError}
          setLoading={setLoading}
        />
      )}
      {path === '/admin' && currentUser?.role === 'Admin' && (
        <AdminPage
          onError={setError}
          setLoading={setLoading}
        />
      )}
      {path === '/properties' && (
        <DashboardPage
          analyses={analyses}
          onCreate={() => navigate('/properties/new')}
          onOpen={openAnalysis}
          onDelete={handleDeleteAnalysis}
          onRefresh={refreshDashboard}
        />
      )}
      {path === '/properties/new' && (
        <CreateAnalysisForm
          onCancel={() => navigate('/properties')}
          onCreated={handleCreated}
          onError={setError}
          setLoading={setLoading}
        />
      )}
      {detailId && analysis && analysis.id === detailId && (
        <AnalysisDetail
          analysis={analysis}
          loading={loading}
          onRefresh={() => refreshAnalysis()}
          onError={setError}
          setLoading={setLoading}
          initialTab={pendingDetailTab}
          onInitialTabConsumed={() => setPendingDetailTab(null)}
          onAnalysisUpdated={(updated) => {
            setAnalysis(updated);
            void refreshDashboard();
          }}
        />
      )}
      {detailId && (!analysis || analysis.id !== detailId) && !loading && (
        <section className="panel">
          <h2>No se pudo abrir la propiedad</h2>
          <p className="section-copy">Volvé al dashboard para elegir otra propiedad.</p>
          <button className="primary-button" type="button" onClick={() => navigate('/properties')}>
            Ver propiedades cargadas
          </button>
        </section>
      )}
      <footer className="app-footer">
        <div>
          <strong>PropSegura</strong>
          <span>Segunda opinión para compradores inmobiliarios.</span>
        </div>
        <nav aria-label="Navegación secundaria">
          <button type="button" onClick={() => navigateToLandingSection('process')}>Cómo funciona</button>
          <button type="button" onClick={() => navigateToLandingSection('faq')}>Preguntas frecuentes</button>
          <button type="button" onClick={() => navigateToLandingSection('contact')}>Contacto</button>
        </nav>
      </footer>
    </main>
  );
}

function InitialLoadingScreen() {
  return (
    <section className="initial-loading-screen" role="status" aria-live="polite">
      <span className="button-spinner" aria-hidden="true" />
      <strong>Cargando PropSegura...</strong>
      <p>Validando tu sesion y preparando tus propiedades.</p>
    </section>
  );
}

function LandingPage({
  onCreate,
  onDashboard,
  onRequestAccess
}: {
  onCreate: () => void;
  onDashboard: () => void;
  onRequestAccess: () => void;
}) {
  return (
    <div className="landing-page">
      <section className="hero landing-hero">
        <div className="hero-content">
          <p className="eyebrow">COMPRÁ CON MÁS INFORMACIÓN Y MENOS RIESGO</p>
          <h1>
            Tomá mejores decisiones
            <br />
            antes de <span>reservar</span>
            <br />
            o <span>comprar.</span>
          </h1>
          <p className="hero-copy">
            Analizamos la propiedad, la documentación y los riesgos ocultos para que sepas exactamente a qué te enfrentás.
          </p>
          <div className="hero-actions">
            <button className="primary-button large-button" type="button" onClick={onCreate}>
              Analizar una propiedad
            </button>
            <button className="secondary-button large-button" type="button" onClick={onDashboard}>
              Ver propiedades cargadas
            </button>
          </div>
          <button className="landing-beta-link" type="button" onClick={onRequestAccess}>
            Solicitar acceso a la beta privada
          </button>
          <p className="trust-note">
            No reemplaza la revisión de un escribano o abogado. Te damos una segunda opinión para ayudarte a tomar mejores decisiones.
          </p>
        </div>

        <div className="landing-photo-wrap" aria-label="Compradores visitando una propiedad">
          <div className="landing-photo" />
          <div className="analysis-preview-card">
            <div className="preview-score-row">
              <span>Score de oportunidad</span>
              <strong>82</strong>
            </div>
            <div className="preview-score-row">
              <span>Score de protección</span>
              <strong>68</strong>
            </div>
            <Badge tone="success">Uso recomendado: inversión</Badge>
          </div>
        </div>
      </section>

      <section className="benefits-strip" aria-label="Beneficios principales">
        <BenefitCard icon="✓" title="Evaluación completa" text="Analizamos precio, zona, documentación y riesgos." />
        <BenefitCard icon="▤" title="Validación documental" text="Detectamos documentos faltantes y posibles problemas." />
        <BenefitCard icon="!" title="Riesgos ocultos" text="Encontramos cláusulas peligrosas, deudas y problemas edilicios." />
        <BenefitCard icon="▥" title="Decisión más segura" text="Toda la información que necesitás para negociar mejor." />
      </section>

      <ProcessSection />

      <section className="security-note landing-support" id="guarantee">
        <span className="security-icon" aria-hidden="true">▣</span>
        <p>
          <strong>Tus datos están protegidos.</strong> La información que cargás es confidencial y solo la usás vos.
        </p>
      </section>
    </div>
  );
}

function LoginPage({
  onLogin,
  onRequestAccess,
  onError,
  setLoading
}: {
  onLogin: (user: AuthUser) => void;
  onRequestAccess: () => void;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
}) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setLoading(true);
    onError(null);

    try {
      const session = await login(email, password);
      storeAuthSession(session);
      onLogin(session.user);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo iniciar sesión.');
    } finally {
      setSubmitting(false);
      setLoading(false);
    }
  }

  return (
    <section className="auth-page">
      <div className="auth-card">
        <p className="eyebrow">BETA PRIVADA</p>
        <h1>Ingresá a PropSegura</h1>
        <p>Acceso disponible únicamente para usuarios invitados.</p>
        <form className="stack-form" onSubmit={handleSubmit}>
          <label className="field">
            <span>Email</span>
            <input type="email" value={email} autoComplete="email" required onChange={(event) => setEmail(event.target.value)} />
          </label>
          <label className="field">
            <span>Contraseña</span>
            <input type="password" value={password} autoComplete="current-password" required onChange={(event) => setPassword(event.target.value)} />
          </label>
          <button className={`primary-button ${submitting ? 'btn-loading' : ''}`} type="submit" disabled={submitting} aria-busy={submitting ? 'true' : undefined}>
            {submitting && <span className="button-spinner" aria-hidden="true" />}
            {submitting ? 'Ingresando...' : 'Ingresar'}
          </button>
        </form>
        <button className="secondary-link-button" type="button" onClick={onRequestAccess}>
          Solicitar acceso beta
        </button>
      </div>
    </section>
  );
}

function BetaAccessPage({
  onLogin,
  onError,
  setLoading
}: {
  onLogin: () => void;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
}) {
  const [form, setForm] = useState<CreateBetaAccessRequest>({
    email: '',
    fullName: '',
    phone: '',
    intendedUse: '',
    message: ''
  });
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setLoading(true);
    onError(null);

    try {
      await createBetaAccessRequest({
        ...form,
        phone: nullableText(form.phone ?? ''),
        intendedUse: nullableText(form.intendedUse ?? ''),
        message: nullableText(form.message ?? '')
      });
      setSubmitted(true);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo enviar la solicitud.');
    } finally {
      setSubmitting(false);
      setLoading(false);
    }
  }

  return (
    <section className="auth-page beta-page">
      <div className="auth-card beta-card">
        <p className="eyebrow">ACCESO ANTICIPADO</p>
        <h1>Solicitá acceso a PropSegura</h1>
        {submitted ? (
          <div className="auth-success">
            <strong>Solicitud recibida.</strong>
            <p>Te vamos a contactar cuando tu acceso esté aprobado.</p>
            <button className="secondary-button" type="button" onClick={onLogin}>Ir a ingresar</button>
          </div>
        ) : (
          <form className="stack-form" onSubmit={handleSubmit}>
            <TextField label="Nombre y apellido" value={form.fullName} required onChange={(fullName) => setForm({ ...form, fullName })} />
            <TextField label="Email" type="email" value={form.email} required onChange={(email) => setForm({ ...form, email })} />
            <TextField label="Teléfono" value={form.phone ?? ''} onChange={(phone) => setForm({ ...form, phone })} />
            <TextField label="Uso previsto" value={form.intendedUse ?? ''} placeholder="Ej: comprar mi primera propiedad" onChange={(intendedUse) => setForm({ ...form, intendedUse })} />
            <label className="field">
              <span>Mensaje</span>
              <textarea value={form.message ?? ''} onChange={(event) => setForm({ ...form, message: event.target.value })} />
            </label>
            <button className={`primary-button ${submitting ? 'btn-loading' : ''}`} type="submit" disabled={submitting} aria-busy={submitting ? 'true' : undefined}>
              {submitting && <span className="button-spinner" aria-hidden="true" />}
              {submitting ? 'Enviando...' : 'Solicitar acceso'}
            </button>
          </form>
        )}
      </div>
    </section>
  );
}

function AdminPage({
  onError,
  setLoading
}: {
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
}) {
  const [requests, setRequests] = useState<BetaAccessRequest[]>([]);
  const [users, setUsers] = useState<AuthUser[]>([]);
  const [approvalNotice, setApprovalNotice] = useState<ApprovalNotice | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [newUser, setNewUser] = useState<CreateUserRequest>({
    email: '',
    fullName: '',
    password: '',
    role: 'User'
  });

  useEffect(() => {
    void refreshAdmin();
  }, []);

  async function refreshAdmin() {
    setLoading(true);
    onError(null);

    try {
      const [nextRequests, nextUsers] = await Promise.all([listBetaAccessRequests(), listUsers()]);
      setRequests(nextRequests);
      setUsers(nextUsers);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo cargar administración.');
    } finally {
      setLoading(false);
    }
  }

  async function handleApprove(request: BetaAccessRequest) {
    setBusyId(request.id);
    setLoading(true);
    onError(null);

    try {
      const result = await approveBetaAccessRequest(request.id);
      if (result.emailSent) {
        setApprovalNotice({
          message: `Usuario aprobado e invitación enviada a ${result.email}.`,
          tone: 'success'
        });
      } else if (result.emailError) {
        setApprovalNotice({
          message: result.message ?? 'Usuario aprobado, pero no se pudo enviar el email.',
          detail: result.emailError,
          temporaryPassword: result.temporaryPassword,
          tone: 'warning'
        });
      } else {
        setApprovalNotice({
          message: 'Usuario aprobado. Email deshabilitado en este entorno.',
          temporaryPassword: result.temporaryPassword,
          tone: 'info'
        });
      }
      await refreshAdmin();
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo aprobar la solicitud.');
    } finally {
      setBusyId(null);
      setLoading(false);
    }
  }

  async function handleCopyTemporaryPassword() {
    if (!approvalNotice?.temporaryPassword) {
      return;
    }

    try {
      await navigator.clipboard.writeText(approvalNotice.temporaryPassword);
      setApprovalNotice({ ...approvalNotice, copied: true });
    } catch {
      onError('No se pudo copiar la contraseña temporal.');
    }
  }

  async function handleReject(request: BetaAccessRequest) {
    setBusyId(request.id);
    setLoading(true);
    onError(null);

    try {
      await rejectBetaAccessRequest(request.id);
      await refreshAdmin();
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo rechazar la solicitud.');
    } finally {
      setBusyId(null);
      setLoading(false);
    }
  }

  async function handleCreateUser(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    onError(null);

    try {
      await createUser(newUser);
      setNewUser({ email: '', fullName: '', password: '', role: 'User' });
      await refreshAdmin();
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo crear el usuario.');
    } finally {
      setLoading(false);
    }
  }

  async function handleToggleUser(user: AuthUser) {
    setBusyId(user.id);
    setLoading(true);
    onError(null);

    try {
      if (user.status === 'Disabled') {
        await enableUser(user.id);
      } else {
        await disableUser(user.id);
      }

      await refreshAdmin();
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo actualizar el usuario.');
    } finally {
      setBusyId(null);
      setLoading(false);
    }
  }

  return (
    <section className="admin-page">
      <div className="admin-heading">
        <div>
          <p className="eyebrow">ADMIN</p>
          <h1>Beta privada</h1>
          <p>Gestioná solicitudes de acceso y usuarios habilitados.</p>
        </div>
        <button className="secondary-button" type="button" onClick={() => void refreshAdmin()}>
          Actualizar
        </button>
      </div>

      {approvalNotice && (
        <div className={`admin-secret admin-secret-${approvalNotice.tone}`}>
          <strong>{approvalNotice.message}</strong>
          {approvalNotice.detail && <span>{approvalNotice.detail}</span>}
          {approvalNotice.temporaryPassword && (
            <>
              <span>Contraseña temporal:</span>
              <code>{approvalNotice.temporaryPassword}</code>
              <button className="secondary-button" type="button" onClick={() => void handleCopyTemporaryPassword()}>
                {approvalNotice.copied ? 'Copiada' : 'Copiar'}
              </button>
            </>
          )}
        </div>
      )}

      <div className="admin-grid">
        <section className="admin-card">
          <h2>Solicitudes beta</h2>
          <div className="admin-list">
            {requests.length === 0 ? (
              <p className="muted">No hay solicitudes por ahora.</p>
            ) : requests.map((request) => (
              <article className="admin-row" key={request.id}>
                <div>
                  <strong>{request.fullName}</strong>
                  <span>{request.email}</span>
                  {request.intendedUse && <p>{request.intendedUse}</p>}
                </div>
                <Badge tone={request.status === 'Pending' ? 'warning' : request.status === 'Approved' ? 'success' : 'neutral'}>
                  {betaStatusLabel(request.status)}
                </Badge>
                {request.status === 'Pending' && (
                  <div className="admin-row-actions">
                    <button className="primary-button" type="button" disabled={busyId === request.id} onClick={() => void handleApprove(request)}>
                      {busyId === request.id ? 'Aprobando...' : 'Aprobar acceso'}
                    </button>
                    <button className="danger-text-button" type="button" disabled={busyId === request.id} onClick={() => void handleReject(request)}>
                      Rechazar
                    </button>
                  </div>
                )}
              </article>
            ))}
          </div>
        </section>

        <section className="admin-card">
          <h2>Usuarios</h2>
          <form className="admin-create-user" onSubmit={handleCreateUser}>
            <TextField label="Nombre" value={newUser.fullName} required onChange={(fullName) => setNewUser({ ...newUser, fullName })} />
            <TextField label="Email" type="email" value={newUser.email} required onChange={(email) => setNewUser({ ...newUser, email })} />
            <TextField label="Contraseña inicial" type="password" value={newUser.password} required onChange={(password) => setNewUser({ ...newUser, password })} />
            <label className="field">
              <span>Rol</span>
              <select value={newUser.role} onChange={(event) => setNewUser({ ...newUser, role: event.target.value as CreateUserRequest['role'] })}>
                <option value="User">Usuario</option>
                <option value="Admin">Admin</option>
              </select>
            </label>
            <button className="primary-button" type="submit">Crear usuario</button>
          </form>
          <div className="admin-list">
            {users.map((user) => (
              <article className="admin-row" key={user.id}>
                <div>
                  <strong>{user.fullName}</strong>
                  <span>{user.email}</span>
                </div>
                <Badge tone={user.status === 'Disabled' ? 'critical' : user.role === 'Admin' ? 'info' : 'success'}>
                  {user.role === 'Admin' ? 'Admin' : userStatusLabel(user.status)}
                </Badge>
                <button className="secondary-button" type="button" disabled={busyId === user.id} onClick={() => void handleToggleUser(user)}>
                  {user.status === 'Disabled' ? 'Habilitar' : 'Deshabilitar'}
                </button>
              </article>
            ))}
          </div>
        </section>
      </div>
    </section>
  );
}

function DashboardPage({
  analyses,
  onCreate,
  onOpen,
  onDelete,
  onRefresh
}: {
  analyses: PropertyAnalysisSummary[];
  onCreate: () => void;
  onOpen: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
  onRefresh: () => Promise<void>;
}) {
  const [neighborhoodFilter, setNeighborhoodFilter] = useState('');
  const [recommendedUseFilter, setRecommendedUseFilter] = useState('');
  const [riskFilter, setRiskFilter] = useState('');
  const [sortBy, setSortBy] = useState<DashboardSort>('newest');
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc');

  const neighborhoods = Array.from(new Set(analyses.map((item) => item.neighborhood).filter(Boolean))).sort();
  const filteredAnalyses = analyses
    .filter((item) => !neighborhoodFilter || item.neighborhood === neighborhoodFilter)
    .filter((item) => !recommendedUseFilter || item.recommendedUse === recommendedUseFilter)
    .filter((item) => !riskFilter || item.initialRiskLevel === riskFilter)
    .sort((left, right) => {
      const direction = sortDirection === 'asc' ? 1 : -1;

      if (sortBy === 'title') {
        return direction * compareText(left.title, right.title);
      }
      if (sortBy === 'neighborhood') {
        return direction * compareText(left.neighborhood, right.neighborhood);
      }
      if (sortBy === 'highestOpportunity') {
        return direction * (left.opportunityScore - right.opportunityScore);
      }
      if (sortBy === 'highestProtection') {
        return direction * (left.protectionScore - right.protectionScore);
      }
      if (sortBy === 'bestInvestment') {
        return investmentRank(right) - investmentRank(left) || right.opportunityScore - left.opportunityScore;
      }
      if (sortBy === 'bestLiving') {
        return livingRank(right) - livingRank(left) || right.opportunityScore - left.opportunityScore;
      }
      if (sortBy === 'lowestPrice') {
        return direction * (valueOrMax(left.priceUsd) - valueOrMax(right.priceUsd));
      }
      if (sortBy === 'totalSquareMeters') {
        return direction * (valueOrMax(left.totalSquareMeters) - valueOrMax(right.totalSquareMeters));
      }
      if (sortBy === 'lowestUsdPerSquareMeter') {
        return direction * (valueOrMax(left.usdPerSquareMeter) - valueOrMax(right.usdPerSquareMeter));
      }
      if (sortBy === 'recommendedUse') {
        return direction * compareText(recommendedUseLabel(left.recommendedUse), recommendedUseLabel(right.recommendedUse));
      }
      return direction * (new Date(left.updatedAtUtc).getTime() - new Date(right.updatedAtUtc).getTime());
    });

  function handleSortChange(value: DashboardSort) {
    setSortBy(value);
    setSortDirection(defaultSortDirection(value));
  }

  function handleColumnSort(value: DashboardSort) {
    if (sortBy === value) {
      setSortDirection((current) => current === 'asc' ? 'desc' : 'asc');
      return;
    }

    setSortBy(value);
    setSortDirection(defaultSortDirection(value));
  }

  return (
    <PropertyDashboard
      analyses={filteredAnalyses}
      neighborhoods={neighborhoods}
      neighborhoodFilter={neighborhoodFilter}
      recommendedUseFilter={recommendedUseFilter}
      riskFilter={riskFilter}
      sortBy={sortBy}
      sortDirection={sortDirection}
      onNeighborhoodFilter={setNeighborhoodFilter}
      onRecommendedUseFilter={setRecommendedUseFilter}
      onRiskFilter={setRiskFilter}
      onSortBy={handleSortChange}
      onColumnSort={handleColumnSort}
      onCreate={onCreate}
      onOpen={onOpen}
      onDelete={onDelete}
      onRefresh={onRefresh}
    />
  );
}

function PropertyDashboard({
  analyses,
  neighborhoods,
  neighborhoodFilter,
  recommendedUseFilter,
  riskFilter,
  sortBy,
  sortDirection,
  onNeighborhoodFilter,
  onRecommendedUseFilter,
  onRiskFilter,
  onSortBy,
  onColumnSort,
  onCreate,
  onOpen,
  onDelete,
  onRefresh
}: {
  analyses: PropertyAnalysisSummary[];
  neighborhoods: string[];
  neighborhoodFilter: string;
  recommendedUseFilter: string;
  riskFilter: string;
  sortBy: DashboardSort;
  sortDirection: SortDirection;
  onNeighborhoodFilter: (value: string) => void;
  onRecommendedUseFilter: (value: string) => void;
  onRiskFilter: (value: string) => void;
  onSortBy: (value: DashboardSort) => void;
  onColumnSort: (value: DashboardSort) => void;
  onCreate: () => void;
  onOpen: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
  onRefresh: () => Promise<void>;
}) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [viewMode, setViewMode] = useState<'table' | 'cards'>('table');
  const searchedAnalyses = analyses.filter((analysis) => matchesDashboardSearch(analysis, searchTerm));
  const dashboardStats = getDashboardStats(analyses);
  const featured = getFeaturedAnalyses(analyses);
  const totalPages = Math.max(1, Math.ceil(searchedAnalyses.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleAnalyses = searchedAnalyses.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  useEffect(() => {
    setPage(1);
  }, [analyses.length, neighborhoodFilter, recommendedUseFilter, riskFilter, sortBy, pageSize, searchTerm]);

  async function handleDelete(analysis: PropertyAnalysisSummary) {
    const confirmed = window.confirm('¿Eliminar "' + analysis.title + '"? Esta acción no se puede deshacer.');

    if (!confirmed) {
      return;
    }

    setDeletingId(analysis.id);
    try {
      await onDelete(analysis.id);
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <section className="dashboard-v2-page">
      <div className="dashboard-v2-hero">
        <div className="dashboard-v2-title">
          <h2>Mis oportunidades inmobiliarias</h2>
          <p>Compará propiedades, identificá riesgos y decidí dónde avanzar.</p>
          <div className="dashboard-actions">
            <button className="primary-button dashboard-primary-action" type="button" onClick={onCreate}>
              <span aria-hidden="true">+</span>
              Nueva propiedad
            </button>
            <button className="secondary-button dashboard-secondary-action" type="button" onClick={() => void onRefresh()}>
              <span aria-hidden="true">↻</span>
              Actualizar análisis
            </button>
          </div>
        </div>

        <div className="dashboard-kpi-grid">
          <DashboardKpiCard icon="⌂" value={dashboardStats.total} title="Propiedades cargadas" helper="Total en tu cuenta" tone="teal" />
          <DashboardKpiCard icon="✓" value={dashboardStats.recommended} title="Recomendadas" helper="Buen potencial" tone="teal" />
          <DashboardKpiCard icon="!" value={dashboardStats.highRisk} title="Riesgo alto" helper="Requieren atención" tone="danger" />
          <DashboardKpiCard icon="▥" value={dashboardStats.averageOpportunity} title="Promedio oportunidad" helper="Score promedio" tone="blue" />
        </div>
      </div>

      {analyses.length > 0 && (
        <div className="featured-opportunities">
          <FeaturedOpportunityCard
            eyebrow="Mejor oportunidad"
            icon="🏆"
            analysis={featured.bestOpportunity}
            scoreLabel="Opportunity Score"
            onOpen={onOpen}
            tone="success"
          />
          <FeaturedOpportunityCard
            eyebrow="Mejor para inversión"
            icon="↗"
            analysis={featured.bestInvestment}
            scoreLabel="Opportunity Score"
            onOpen={onOpen}
            tone="success"
          />
          <FeaturedOpportunityCard
            eyebrow="Mejor para vivienda"
            icon="⌂"
            analysis={featured.bestLiving}
            scoreLabel="Opportunity Score"
            onOpen={onOpen}
            tone="success"
          />
          <AttentionOpportunityCard count={dashboardStats.needsAttention} analysis={featured.needsAttention} onOpen={onOpen} />
        </div>
      )}

      <div className="dashboard-table-shell">
        <div className="dashboard-toolbar">
          <div className="view-switch" aria-label="Seleccionar vista">
            <button className={viewMode === 'table' ? 'active' : ''} type="button" onClick={() => setViewMode('table')}>
              ☰ Vista tabla
            </button>
            <button className={viewMode === 'cards' ? 'active' : ''} type="button" onClick={() => setViewMode('cards')}>
              ▦ Vista tarjetas
            </button>
          </div>

          <div className="dashboard-search-and-filters">
            <label className="dashboard-search">
              <span className="sr-only">Buscar</span>
              <input
                type="search"
                value={searchTerm}
                placeholder="Buscar propiedad, barrio o uso..."
                onChange={(event) => setSearchTerm(event.target.value)}
              />
              <span aria-hidden="true">⌕</span>
            </label>
            <label className="dashboard-select">
              <span>Barrio</span>
              <select value={neighborhoodFilter} onChange={(event) => onNeighborhoodFilter(event.target.value)}>
                <option value="">Todos</option>
                {neighborhoods.map((neighborhood) => (
                  <option key={neighborhood} value={neighborhood}>{neighborhood}</option>
                ))}
              </select>
            </label>
            <label className="dashboard-select">
              <span>Uso</span>
              <select value={recommendedUseFilter} onChange={(event) => onRecommendedUseFilter(event.target.value)}>
                <option value="">Todos</option>
                {['Living', 'Investment', 'Both', 'NotRecommended', 'Unknown'].map((value) => (
                  <option key={value} value={value}>{recommendedUseLabel(value)}</option>
                ))}
              </select>
            </label>
            <label className="dashboard-select">
              <span>Riesgo</span>
              <select value={riskFilter} onChange={(event) => onRiskFilter(event.target.value)}>
                <option value="">Todos</option>
                {['Low', 'Medium', 'High'].map((value) => (
                  <option key={value} value={value}>{riskLevelLabel(value)}</option>
                ))}
              </select>
            </label>
            <label className="dashboard-select">
              <span>Orden</span>
              <select value={sortBy} onChange={(event) => onSortBy(event.target.value as DashboardSort)}>
                <option value="newest">Más recientes</option>
                <option value="highestOpportunity">Mayor oportunidad</option>
                <option value="highestProtection">Mayor protección</option>
                <option value="lowestPrice">Menor precio</option>
                <option value="lowestUsdPerSquareMeter">Menor USD/m²</option>
                <option value="recommendedUse">Uso recomendado</option>
                <option value="bestInvestment">Mejor para inversión</option>
                <option value="bestLiving">Mejor para vivienda</option>
                <option value="title">Propiedad</option>
                <option value="neighborhood">Barrio</option>
              </select>
            </label>
          </div>
        </div>

        {analyses.length === 0 ? (
          <div className="empty-state dashboard-empty">
            <strong>Todavía no cargaste propiedades.</strong>
            <button className="primary-button" type="button" onClick={onCreate}>
              Analizar primera propiedad
            </button>
          </div>
        ) : viewMode === 'cards' ? (
          <div className="dashboard-card-view-note">
            <strong>Vista tarjetas</strong>
            <p>Próximamente vas a poder comparar cada oportunidad en formato de tarjetas. Por ahora, usá la vista tabla para operar.</p>
          </div>
        ) : searchedAnalyses.length === 0 ? (
          <div className="empty-state dashboard-empty">
            <strong>No encontramos propiedades con esos filtros.</strong>
            <p>Probá ajustar la búsqueda, el barrio, el uso o el riesgo.</p>
          </div>
        ) : (
          <div className="comparison-table-wrap">
            <table className="comparison-table">
              <thead>
                <tr>
                  <SortableHeader label="Propiedad" sortKey="title" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                  <SortableHeader label="Barrio" sortKey="neighborhood" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                  <SortableHeader label="Precio" sortKey="lowestPrice" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                  <SortableHeader label="USD/m²" sortKey="lowestUsdPerSquareMeter" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                  <SortableHeader label="Oportunidad" sortKey="highestOpportunity" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                  <SortableHeader label="Protección" sortKey="highestProtection" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                  <SortableHeader label="Uso recomendado" sortKey="recommendedUse" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                  <th>Mi opinión</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {visibleAnalyses.map((analysis) => (
                  <PropertyDashboardRow
                    key={analysis.id}
                    analysis={analysis}
                    deleting={deletingId === analysis.id}
                    onOpen={onOpen}
                    onDelete={handleDelete}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}

        {searchedAnalyses.length > 0 && viewMode === 'table' && (
          <div className="pagination-bar" aria-label="Paginación de propiedades">
            <button className="ghost-button" type="button" onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={currentPage === 1}>
              Anterior
            </button>
            <span>Página {currentPage} de {totalPages}</span>
            <button className="ghost-button" type="button" onClick={() => setPage((value) => Math.min(totalPages, value + 1))} disabled={currentPage === totalPages}>
              Siguiente
            </button>
            <label className="page-size">
              <span>Filas</span>
              <select value={pageSize} onChange={(event) => setPageSize(Number(event.target.value))}>
                {[10, 25, 50].map((size) => (
                  <option key={size} value={size}>{size}</option>
                ))}
              </select>
            </label>
          </div>
        )}
      </div>

      <p className="dashboard-disclaimer">
        <span aria-hidden="true">▱</span>
        <strong>Recordá:</strong> PropSegura te da una segunda opinión experta. No reemplaza la revisión de un escribano o abogado.
      </p>
    </section>
  );
}

function DashboardKpiCard({
  icon,
  value,
  title,
  helper,
  tone
}: {
  icon: string;
  value: number;
  title: string;
  helper: string;
  tone: 'teal' | 'danger' | 'blue';
}) {
  return (
    <article className={'dashboard-kpi-card dashboard-kpi-' + tone}>
      <span className="dashboard-kpi-icon" aria-hidden="true">{icon}</span>
      <strong>{value}</strong>
      <h3>{title}</h3>
      <p>{helper}</p>
    </article>
  );
}

function FeaturedOpportunityCard({
  eyebrow,
  icon,
  analysis,
  scoreLabel,
  onOpen,
  tone
}: {
  eyebrow: string;
  icon: string;
  analysis?: PropertyAnalysisSummary;
  scoreLabel: string;
  onOpen: (id: string) => Promise<void>;
  tone: 'success' | 'warning';
}) {
  if (!analysis) {
    return null;
  }

  return (
    <article className={'featured-card featured-' + tone}>
      <span className="featured-icon" aria-hidden="true">{icon}</span>
      <div className="featured-main">
        <span>{eyebrow}</span>
        <strong>{analysis.title}</strong>
        <Badge tone={recommendedTone(analysis.recommendedUse)}>{recommendedUseLabel(analysis.recommendedUse)}</Badge>
      </div>
      <div className="featured-score">
        <strong>{analysis.opportunityScore}</strong>
        <span>{scoreLabel}</span>
      </div>
      <button className="featured-link" type="button" onClick={() => void onOpen(analysis.id)}>
        Ver análisis <span aria-hidden="true">→</span>
      </button>
    </article>
  );
}

function AttentionOpportunityCard({
  count,
  analysis,
  onOpen
}: {
  count: number;
  analysis?: PropertyAnalysisSummary;
  onOpen: (id: string) => Promise<void>;
}) {
  return (
    <article className="featured-card featured-warning">
      <span className="featured-icon" aria-hidden="true">!</span>
      <div className="featured-main">
        <span>Requiere atención</span>
        <strong>{count} {count === 1 ? 'propiedad' : 'propiedades'}</strong>
        <p>Documentación incompleta o riesgos detectados</p>
      </div>
      {analysis && (
        <button className="featured-link" type="button" onClick={() => void onOpen(analysis.id)}>
          Ver detalles <span aria-hidden="true">→</span>
        </button>
      )}
    </article>
  );
}

function SortableHeader({
  label,
  sortKey,
  activeSort,
  direction,
  onSort
}: {
  label: string;
  sortKey: DashboardSort;
  activeSort: DashboardSort;
  direction: SortDirection;
  onSort: (value: DashboardSort) => void;
}) {
  const isActive = activeSort === sortKey;

  return (
    <th aria-sort={isActive ? (direction === 'asc' ? 'ascending' : 'descending') : 'none'}>
      <button className={isActive ? 'sortable-heading active' : 'sortable-heading'} type="button" onClick={() => onSort(sortKey)}>
        <span>{label}</span>
        <span aria-hidden="true">{isActive ? (direction === 'asc' ? 'Asc' : 'Desc') : 'Ordenar'}</span>
      </button>
    </th>
  );
}

function PropertyCoverImage({
  coverImageUrl,
  alt,
  className
}: {
  coverImageUrl?: string | null;
  alt: string;
  className: string;
}) {
  const [assetUrl, setAssetUrl] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;
    let objectUrl: string | null = null;

    async function loadImage() {
      if (!coverImageUrl) {
        setAssetUrl(null);
        return;
      }

      try {
        objectUrl = await createProtectedAssetUrl(coverImageUrl);

        if (isMounted) {
          setAssetUrl(objectUrl);
        }
      } catch {
        if (isMounted) {
          setAssetUrl(null);
        }
      }
    }

    void loadImage();

    return () => {
      isMounted = false;

      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [coverImageUrl]);

  return (
    <img
      className={className}
      src={assetUrl ?? propertyPlaceholder}
      alt={alt}
      loading="lazy"
    />
  );
}

function PropertyDashboardCard({ analysis, onOpen }: { analysis: PropertyAnalysisSummary; onOpen: (id: string) => Promise<void> }) {
  return (
    <article className="property-dashboard-card">
      <PropertyCoverImage className="property-card-cover" coverImageUrl={analysis.coverImageUrl} alt={`Imagen de ${analysis.title}`} />
      <div className="property-card-heading">
        <div>
          <h3>{analysis.title}</h3>
          <p>{analysis.neighborhood} · {analysis.address}</p>
        </div>
        <Badge tone={riskTone(analysis.initialRiskLevel)}>{riskLevelLabel(analysis.initialRiskLevel)}</Badge>
      </div>
      <div className="dashboard-score-row">
        <MiniScore label="Oportunidad" score={analysis.opportunityScore} tone="opportunity" />
        <MiniScore label="Protección" score={analysis.protectionScore} tone="protection" />
      </div>
      <div className="summary-grid dashboard-card-metrics">
        <Info label="Precio USD" value={formatMoney(analysis.priceUsd)} />
        <Info label="USD/m²" value={formatMoney(analysis.usdPerSquareMeter)} />
        <Info label="Expensas" value={formatMoney(analysis.monthlyExpenses)} />
        <Info label="Metros" value={formatNumber(analysis.totalSquareMeters)} />
      </div>
      <div className="badge-row">
        <Badge tone={recommendedTone(analysis.recommendedUse)}>{recommendedUseLabel(analysis.recommendedUse)}</Badge>
        <Badge tone={analysis.missingRequiredDocumentsCount > 0 ? 'warning' : 'success'}>{analysis.missingRequiredDocumentsCount} documentos faltantes</Badge>
        <Badge tone={analysis.criticalFindingsCount > 0 ? 'critical' : analysis.findingsCount > 0 ? 'warning' : 'neutral'}>{analysis.findingsCount} hallazgos</Badge>
      </div>
      <div className="dashboard-card-actions">
        <button className="primary-button" type="button" onClick={() => void onOpen(analysis?.id)}>
          Continuar análisis
        </button>
        <button className="secondary-button" type="button" onClick={() => void onOpen(analysis?.id)}>
          Completar datos
        </button>
        <button className="ghost-button" type="button" onClick={() => void onOpen(analysis?.id)}>
          Validar documentación
        </button>
      </div>
    </article>
  );
}

function PropertyDashboardRow({
  analysis,
  deleting,
  onOpen,
  onDelete
}: {
  analysis: PropertyAnalysisSummary;
  deleting: boolean;
  onOpen: (id: string) => Promise<void>;
  onDelete: (analysis: PropertyAnalysisSummary) => Promise<void>;
}) {
  function open() {
    void onOpen(analysis.id);
  }

  return (
    <tr
      className="clickable-row"
      tabIndex={0}
      onClick={open}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          open();
        }
      }}
    >
      <td>
        <div className="dashboard-property-cell">
          <PropertyCoverImage className="property-thumb" coverImageUrl={analysis.coverImageUrl} alt="" />
          <div>
            <button className="property-title-button" type="button" onClick={(event) => { event.stopPropagation(); open(); }}>
              {analysis.title}
            </button>
            <span>{analysis.address || 'Sin dirección cargada'}</span>
          </div>
        </div>
      </td>
      <td>{analysis.neighborhood || 'Sin dato'}</td>
      <td>{formatMoney(analysis.priceUsd)}</td>
      <td>{formatMoney(analysis.usdPerSquareMeter)}</td>
      <td><ScoreBadge type="opportunity" score={analysis.opportunityScore} /></td>
      <td><ProtectionBadge analysis={analysis} /></td>
      <td><Badge tone={recommendedTone(analysis.recommendedUse)}>{recommendedUseLabel(analysis.recommendedUse)}</Badge></td>
      <td><OpinionBadge analysis={analysis} /></td>
      <td>
        <div className="row-actions">
          <button className="text-button" type="button" onClick={(event) => { event.stopPropagation(); open(); }}>
            Ver
          </button>
          <button className="text-button" type="button" onClick={(event) => { event.stopPropagation(); open(); }}>
            Editar
          </button>
          <button className="text-button" type="button" onClick={(event) => { event.stopPropagation(); open(); }}>
            Documentos
          </button>
          <button
            className="text-button danger-text-button"
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              void onDelete(analysis);
            }}
            disabled={deleting}
            aria-busy={deleting ? 'true' : undefined}
          >
            {deleting ? 'Eliminando...' : 'Eliminar'}
          </button>
        </div>
      </td>
    </tr>
  );
}

function ScoreBadge({ type, score }: { type: 'opportunity' | 'protection'; score: number }) {
  return (
    <span className={`decision-score decision-score-${type} ${scoreClassName(type, score)}`}>
      <strong>{score}</strong>
      <span>{scoreBadgeLabel(type, score)}</span>
    </span>
  );
}

function ProtectionBadge({ analysis }: { analysis: PropertyAnalysisSummary }) {
  if (hasNoDetectedDocumentRisk(analysis)) {
    return (
      <span className="decision-score decision-score-protection score-clear">
        <strong aria-hidden="true">◇</strong>
        <span>Sin riesgos detectados</span>
      </span>
    );
  }

  return <ScoreBadge type="protection" score={analysis.protectionScore} />;
}

function OpinionBadge({ analysis }: { analysis: PropertyAnalysisSummary }) {
  const opinion = getDashboardOpinion(analysis);

  return (
    <span className={`opinion-badge opinion-${opinion.tone}`}>
      <span aria-hidden="true">{opinion.icon}</span>
      {opinion.label}
    </span>
  );
}

function MiniScore({ label, score, tone }: { label: string; score: number; tone: 'opportunity' | 'protection' }) {
  return (
    <div className={`mini-score mini-score-${tone}`}>
      <strong>{score}</strong>
      <span>{label}</span>
    </div>
  );
}

function BenefitCard({ icon, title, text }: { icon: string; title: string; text: string }) {
  return (
    <article className="benefit-card">
      <span className="benefit-icon" aria-hidden="true">{icon}</span>
      <div>
        <strong>{title}</strong>
        <p>{text}</p>
      </div>
    </article>
  );
}

function ProcessSection() {
  return (
    <section className="process-section" id="process">
      <div className="section-heading process-heading">
        <div>
          <p className="eyebrow">Pasos simples</p>
          <h2>Cómo funciona PropSegura</h2>
        </div>
      </div>
      <div className="process-grid">
        <ProcessCard
          step="1"
          icon="↥"
          title="Cargá la propiedad"
          text="Completá los datos básicos o importalos desde ZonaProp."
        />
        <ProcessCard
          step="2"
          icon="▤"
          title="Subí la documentación"
          text="Reserva, expensas, actas, informes y todo lo que tengas."
        />
        <ProcessCard
          step="3"
          icon="○"
          title="Analizamos todo"
          text="Revisamos precios, documentos y detectamos riesgos ocultos."
        />
        <ProcessCard
          step="4"
          icon="✓"
          title="Recibís tu informe"
          text="Obtenés una evaluación clara, hallazgos y próximos pasos."
        />
      </div>
    </section>
  );
}

function ProcessCard({ step, icon, title, text }: { step: string; icon: string; title: string; text: string }) {
  return (
    <article className="process-card">
      <span className="process-step-number">{step}</span>
      <span className="process-icon" aria-hidden="true">{icon}</span>
      <h3>{title}</h3>
      <p>{text}</p>
    </article>
  );
}

function CreateAnalysisForm({
  onCancel,
  onCreated,
  onError,
  setLoading
}: {
  onCancel: () => void;
  onCreated: (analysis: PropertyAnalysis) => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
}) {
  const [form, setForm] = useState<CreateFormState>(emptyCreateForm);
  const [importWarnings, setImportWarnings] = useState<string[]>([]);
  const [importConfidence, setImportConfidence] = useState<number | null>(null);
  const [coverImageFile, setCoverImageFile] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [importing, setImporting] = useState(false);
  const expensesWarning = getExpensesPerSquareMeterWarning(form.monthlyExpenses, form.totalSquareMeters);

  async function handleImportListing() {
    const listingUrl = nullableText(form.listingUrl);

    if (!listingUrl) {
      onError('Pegá el link de la publicación para importar datos.');
      return;
    }

    setImporting(true);
    setLoading(true);
    setImportWarnings([]);
    setImportConfidence(null);
    onError(null);

    try {
      const imported = await importListing({ listingUrl });
      setForm((current) => ({
        ...current,
        listingUrl: imported.sourceUrl || current.listingUrl,
        title: imported.title ?? current.title,
        priceUsd: formatImportedValue(imported.priceUsd) ?? current.priceUsd,
        monthlyExpenses: formatImportedValue(imported.monthlyExpenses) ?? current.monthlyExpenses,
        address: imported.address ?? current.address,
        neighborhood: imported.neighborhood ?? current.neighborhood,
        totalSquareMeters: formatImportedValue(imported.totalSquareMeters) ?? current.totalSquareMeters,
        coveredSquareMeters: formatImportedValue(imported.coveredSquareMeters) ?? current.coveredSquareMeters,
        rooms: formatImportedValue(imported.rooms) ?? current.rooms,
        bedrooms: formatImportedValue(imported.bedrooms) ?? current.bedrooms,
        bathrooms: formatImportedValue(imported.bathrooms) ?? current.bathrooms,
        notes: imported.description ?? current.notes
      }));
      setImportWarnings(imported.warnings ?? []);
      setImportConfidence(imported.confidenceScore);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo importar la publicación.');
    } finally {
      setImporting(false);
      setLoading(false);
    }
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setLoading(true);
    onError(null);

    try {
      if (coverImageFile && !isValidCoverImageFile(coverImageFile)) {
        throw new Error('La imagen principal debe ser JPEG, PNG o WEBP y pesar hasta 5 MB.');
      }

      const created = await createAnalysis({
        title: form.title,
        listingUrl: nullableText(form.listingUrl),
        address: form.address,
        neighborhood: form.neighborhood,
        priceUsd: nullableNumber(form.priceUsd),
        monthlyExpenses: nullableNumber(form.monthlyExpenses),
        totalSquareMeters: nullableNumber(form.totalSquareMeters),
        coveredSquareMeters: nullableNumber(form.coveredSquareMeters),
        rooms: nullableNumber(form.rooms),
        bedrooms: nullableNumber(form.bedrooms),
        bathrooms: nullableNumber(form.bathrooms),
        buildingAgeYears: nullableNumber(form.buildingAgeYears),
        floor: nullableNumber(form.floor),
        apartmentUnit: nullableText(form.apartmentUnit),
        unitIdentifier: nullableText(form.unitIdentifier),
        hasBalcony: nullableBoolean(form.hasBalcony),
        hasAmenities: nullableBoolean(form.hasAmenities),
        isCreditEligible: nullableBoolean(form.isCreditEligible),
        propertyCondition: form.propertyCondition,
        notes: nullableText(form.notes)
      });

      const createdWithCover = coverImageFile
        ? await uploadCoverImage(created.id, coverImageFile)
        : created;

      await onCreated(createdWithCover);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo crear el análisis.');
    } finally {
      setSubmitting(false);
      setLoading(false);
    }
  }

  return (
    <section className="property-wizard-page">
      <aside className="wizard-sidebar" aria-label="Progreso de carga">
        <div>
          <h2>Nueva propiedad</h2>
          <p>Completá la información en pasos simples.</p>
        </div>
        <div className="wizard-steps">
          <WizardStep number="1" title="Información básica" text="Datos principales de la propiedad" state="active" />
          <WizardStep number="2" title="Precio y características" text="Detalles y especificaciones" state="current" />
          <WizardStep number="3" title="Documentación" text="Subí los documentos disponibles" />
          <WizardStep number="4" title="Análisis y revisión" text="Resumen y confirmación" />
        </div>
        <div className="wizard-help-card">
          <span className="wizard-help-icon" aria-hidden="true">◇</span>
          <strong>¿Necesitás ayuda?</strong>
          <p>Estamos para ayudarte en cada paso del análisis.</p>
          <button className="ghost-button" type="button">Contactar soporte</button>
        </div>
      </aside>

      <div className="wizard-main">
        <button className="wizard-back" type="button" onClick={onCancel}>
          ← Volver a mis oportunidades
        </button>

        <div className="wizard-content-grid">
          <form className="wizard-form-card" onSubmit={handleSubmit}>
            <div className="wizard-card-heading">
              <h1>Información básica</h1>
              <p>Datos principales de la propiedad para comenzar el análisis.</p>
            </div>

            <div className="wizard-import-block">
              <label className="cover-upload-field">
                <span>Imagen principal</span>
                <small>Agregá una foto para reconocer fácilmente la propiedad.</small>
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={(event) => setCoverImageFile(event.target.files?.[0] ?? null)}
                />
                <strong>{coverImageFile ? coverImageFile.name : 'Seleccionar imagen'}</strong>
              </label>
              <div className="import-row">
                <TextField label="Pegá el link de la publicación" value={form.listingUrl} placeholder="https://..." onChange={(listingUrl) => setForm({ ...form, listingUrl })} />
                <button
                  className={`secondary-button ${importing ? 'btn-loading' : ''}`}
                  type="button"
                  onClick={handleImportListing}
                  disabled={importing || !form.listingUrl.trim()}
                  aria-busy={importing ? 'true' : undefined}
                >
                  {importing && <span className="button-spinner" aria-hidden="true" />}
                  {importing ? 'Cargando...' : 'Importar datos'}
                </button>
              </div>
              {importConfidence !== null && (
                <div className="import-feedback">
                  <strong>Datos importados con confianza {importConfidence}%.</strong>
                  {importWarnings.length > 0 && (
                    <ul>
                      {importWarnings.map((warning) => (
                        <li key={warning}>{warning}</li>
                      ))}
                    </ul>
                  )}
                </div>
              )}
            </div>

            <div className="wizard-field-grid">
              <TextField label="Título de la propiedad" value={form.title} required placeholder="Ej: Departamento 2 ambientes en Belgrano" onChange={(title) => setForm({ ...form, title })} />
              <label className="field">
                <span>Uso recomendado</span>
                <select disabled value="Both" aria-label="Uso recomendado inicial">
                  <option>Vivienda e inversión</option>
                </select>
              </label>
              <TextField label="Barrio" value={form.neighborhood} required placeholder="Ej: Recoleta" onChange={(neighborhood) => setForm({ ...form, neighborhood })} />
              <p className="field-helper field-wide">Por ahora disponible para propiedades en CABA. Los valores de referencia son estimativos y sujetos a actualizacion.</p>
              <TextField label="Dirección" value={form.address} required placeholder="Ej: Av. Cabildo 1234" onChange={(address) => setForm({ ...form, address })} />
              <TextField label="Precio de publicación" type="number" value={form.priceUsd} placeholder="Ej: 85000" onChange={(priceUsd) => setForm({ ...form, priceUsd })} />
              <TextField label="Expensas mensuales" type="number" value={form.monthlyExpenses} placeholder="Ej: 100000" onChange={(monthlyExpenses) => setForm({ ...form, monthlyExpenses })} />
              <TextField label="M² totales" type="number" value={form.totalSquareMeters} placeholder="Ej: 55" onChange={(totalSquareMeters) => setForm({ ...form, totalSquareMeters })} />
              <TextField label="M² cubiertos" type="number" value={form.coveredSquareMeters} placeholder="Ej: 48" onChange={(coveredSquareMeters) => setForm({ ...form, coveredSquareMeters })} />
              <TextField label="Ambientes" type="number" value={form.rooms} onChange={(rooms) => setForm({ ...form, rooms })} />
              <TextField label="Dormitorios" type="number" value={form.bedrooms} onChange={(bedrooms) => setForm({ ...form, bedrooms })} />
              <TextField label="Baños" type="number" value={form.bathrooms} onChange={(bathrooms) => setForm({ ...form, bathrooms })} />
              <TextField label="Antigüedad" type="number" value={form.buildingAgeYears} onChange={(buildingAgeYears) => setForm({ ...form, buildingAgeYears })} />
              <TextField label="Piso" type="number" value={form.floor} onChange={(floor) => setForm({ ...form, floor })} />
              <TextField label="Departamento / Unidad" value={form.apartmentUnit} placeholder="Ej: B" onChange={(apartmentUnit) => setForm({ ...form, apartmentUnit })} />
              <TextField label="Identificador de unidad" value={form.unitIdentifier} placeholder="Ej: UF 12" onChange={(unitIdentifier) => setForm({ ...form, unitIdentifier })} />
              <p className="field-helper field-wide">Estos datos ayudan a validar que las expensas correspondan a la propiedad correcta.</p>
              {expensesWarning && (
                <div className="inline-warning field-wide" role="alert">
                  {expensesWarning}
                </div>
              )}
              <SelectField label="Balcón" value={form.hasBalcony} onChange={(hasBalcony) => setForm({ ...form, hasBalcony })} />
              <SelectField label="Amenities" value={form.hasAmenities} onChange={(hasAmenities) => setForm({ ...form, hasAmenities })} />
              <SelectField label="Apto crédito" value={form.isCreditEligible} onChange={(isCreditEligible) => setForm({ ...form, isCreditEligible })} />
              <label className="field">
                <span>Estado</span>
                <select value={form.propertyCondition} onChange={(event) => setForm({ ...form, propertyCondition: event.target.value as PropertyCondition })}>
                  {(Object.keys(propertyConditionLabels) as PropertyCondition[]).map((condition) => (
                    <option key={condition} value={condition}>
                      {propertyConditionLabels[condition]}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field field-wide">
                <span>Descripción de la propiedad</span>
                <textarea
                  placeholder="Agregá detalles relevantes sobre la propiedad, estado, amenities, ubicación, etc."
                  value={form.notes}
                  onChange={(event) => setForm({ ...form, notes: event.target.value })}
                />
              </label>
            </div>

            <div className="wizard-form-actions">
              <button
                className={`primary-button ${submitting ? 'btn-loading' : ''}`}
                type="submit"
                disabled={submitting}
                aria-busy={submitting ? 'true' : undefined}
              >
                {submitting && <span className="button-spinner" aria-hidden="true" />}
                {submitting ? 'Cargando...' : 'Guardar y continuar →'}
              </button>
            </div>
          </form>

          <aside className="wizard-insights" aria-label="Ayuda para completar el análisis">
            <WizardProgressCard percent={25} />
            <WizardInfoCard icon="◌" title="Consejo" text="Cuanta más información completes, más preciso será nuestro análisis de riesgos y oportunidades." tone="warning" />
            <WizardInfoCard icon="◇" title="¿Por qué analizamos?" text="Evaluamos oportunidades, riesgos legales, edilicios y financieros para que tomes mejores decisiones." />
          </aside>
        </div>

        <div className="wizard-disclaimer">
          <span aria-hidden="true">◇</span>
          <p><strong>Recordá:</strong> PropSegura te da una segunda opinión experta. No reemplaza la revisión de un escribano o abogado.</p>
        </div>
      </div>
    </section>
  );
}

function WizardStep({
  number,
  title,
  text,
  state = 'pending'
}: {
  number: string;
  title: string;
  text: string;
  state?: 'active' | 'current' | 'pending';
}) {
  return (
    <div className={`wizard-step wizard-step-${state}`}>
      <span>{number}</span>
      <div>
        <strong>{title}</strong>
        <p>{text}</p>
      </div>
    </div>
  );
}

function WizardProgressCard({ percent }: { percent: number }) {
  return (
    <article className="wizard-side-card wizard-progress-card">
      <h3>Progreso del análisis</h3>
      <div className="wizard-progress-body">
        <div className="wizard-progress-ring" style={{ '--progress': `${percent}%` } as CSSProperties}>
          <strong>{percent}%</strong>
        </div>
        <p>Completá los pasos para obtener el análisis completo de la propiedad.</p>
      </div>
      <div className="wizard-progress-list">
        <span className="done">Información básica</span>
        <span className="active">Precio y características</span>
        <span>Documentación</span>
        <span>Análisis y revisión</span>
      </div>
    </article>
  );
}

function WizardInfoCard({
  icon,
  title,
  text,
  tone = 'success'
}: {
  icon: string;
  title: string;
  text: string;
  tone?: 'success' | 'warning';
}) {
  return (
    <article className={`wizard-side-card wizard-info-card wizard-info-${tone}`}>
      <span aria-hidden="true">{icon}</span>
      <div>
        <h3>{title}</h3>
        <p>{text}</p>
      </div>
    </article>
  );
}

function FormGroup({ title, children }: { title: string; children: ReactNode }) {
  return (
    <fieldset className="form-group">
      <legend>{title}</legend>
      <div className="form-group-grid">{children}</div>
    </fieldset>
  );
}

function PropertyEditForm({
  analysis,
  onCancel,
  onSaved,
  onError,
  setLoading,
  variant = 'default',
  formId
}: {
  analysis: PropertyAnalysis;
  onCancel: () => void;
  onSaved: (analysis: PropertyAnalysis) => void;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
  variant?: 'default' | 'workspace';
  formId?: string;
}) {
  const [form, setForm] = useState<CreateFormState>(() => formStateFromAnalysis(analysis));
  const [saving, setSaving] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setLoading(true);
    onError(null);

    try {
      const updated = await updateAnalysis(analysis.id, {
        title: form.title,
        listingUrl: nullableText(form.listingUrl),
        address: form.address,
        neighborhood: form.neighborhood,
        priceUsd: nullableNumber(form.priceUsd),
        monthlyExpenses: nullableNumber(form.monthlyExpenses),
        totalSquareMeters: nullableNumber(form.totalSquareMeters),
        coveredSquareMeters: nullableNumber(form.coveredSquareMeters),
        rooms: nullableNumber(form.rooms),
        bedrooms: nullableNumber(form.bedrooms),
        bathrooms: nullableNumber(form.bathrooms),
        buildingAgeYears: nullableNumber(form.buildingAgeYears),
        floor: nullableNumber(form.floor),
        apartmentUnit: nullableText(form.apartmentUnit),
        unitIdentifier: nullableText(form.unitIdentifier),
        hasBalcony: nullableBoolean(form.hasBalcony),
        hasAmenities: nullableBoolean(form.hasAmenities),
        isCreditEligible: nullableBoolean(form.isCreditEligible),
        propertyCondition: form.propertyCondition,
        notes: nullableText(form.notes)
      });

      onSaved(updated);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo actualizar la propiedad.');
    } finally {
      setSaving(false);
      setLoading(false);
    }
  }

  if (variant === 'workspace') {
    return (
      <form id={formId} className="edit-workspace-form" onSubmit={handleSubmit}>
        <div className="edit-form-heading">
          <h3>Información básica</h3>
          <p>Datos principales de la propiedad.</p>
        </div>
        <div className="edit-field-grid">
          <TextField label="Título de la propiedad" value={form.title} required onChange={(title) => setForm({ ...form, title })} />
          <label className="field">
            <span>Uso recomendado</span>
            <select disabled value={analysis.recommendedUse} aria-label="Uso recomendado calculado">
              <option>{recommendedUseLabel(analysis.recommendedUse)}</option>
            </select>
          </label>
          <TextField label="Barrio" value={form.neighborhood} required onChange={(neighborhood) => setForm({ ...form, neighborhood })} />
          <TextField label="Dirección" value={form.address} required onChange={(address) => setForm({ ...form, address })} />
          <TextField label="Link de publicación" value={form.listingUrl} onChange={(listingUrl) => setForm({ ...form, listingUrl })} />
          <TextField label="Precio de publicación" type="number" value={form.priceUsd} onChange={(priceUsd) => setForm({ ...form, priceUsd })} />
          <TextField label="Expensas mensuales" type="number" value={form.monthlyExpenses} onChange={(monthlyExpenses) => setForm({ ...form, monthlyExpenses })} />
          <TextField label="M² totales" type="number" value={form.totalSquareMeters} onChange={(totalSquareMeters) => setForm({ ...form, totalSquareMeters })} />
          <TextField label="M² cubiertos" type="number" value={form.coveredSquareMeters} onChange={(coveredSquareMeters) => setForm({ ...form, coveredSquareMeters })} />
          <TextField label="Ambientes" type="number" value={form.rooms} onChange={(rooms) => setForm({ ...form, rooms })} />
          <TextField label="Dormitorios" type="number" value={form.bedrooms} onChange={(bedrooms) => setForm({ ...form, bedrooms })} />
          <TextField label="Baños" type="number" value={form.bathrooms} onChange={(bathrooms) => setForm({ ...form, bathrooms })} />
          <TextField label="Antigüedad" type="number" value={form.buildingAgeYears} onChange={(buildingAgeYears) => setForm({ ...form, buildingAgeYears })} />
          <TextField label="Piso" type="number" value={form.floor} onChange={(floor) => setForm({ ...form, floor })} />
          <TextField label="Departamento / Unidad" value={form.apartmentUnit} onChange={(apartmentUnit) => setForm({ ...form, apartmentUnit })} />
          <TextField label="Identificador de unidad" value={form.unitIdentifier} onChange={(unitIdentifier) => setForm({ ...form, unitIdentifier })} />
          <p className="field-helper field-wide">Estos datos ayudan a validar que las expensas correspondan a la propiedad correcta.</p>
          <SelectField label="Balcón" value={form.hasBalcony} onChange={(hasBalcony) => setForm({ ...form, hasBalcony })} />
          <SelectField label="Amenities" value={form.hasAmenities} onChange={(hasAmenities) => setForm({ ...form, hasAmenities })} />
          <SelectField label="Apto crédito" value={form.isCreditEligible} onChange={(isCreditEligible) => setForm({ ...form, isCreditEligible })} />
          <label className="field">
            <span>Estado</span>
            <select value={form.propertyCondition} onChange={(event) => setForm({ ...form, propertyCondition: event.target.value as PropertyCondition })}>
              {(Object.keys(propertyConditionLabels) as PropertyCondition[]).map((condition) => (
                <option key={condition} value={condition}>
                  {propertyConditionLabels[condition]}
                </option>
              ))}
            </select>
          </label>
          <label className="field field-wide">
            <span>Descripción</span>
            <textarea value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} />
          </label>
        </div>
        <div className="edit-inline-actions">
          <button className="ghost-button" type="button" onClick={onCancel}>
            Cancelar
          </button>
          <button className={`primary-button ${saving ? 'btn-loading' : ''}`} type="submit" disabled={saving} aria-busy={saving ? 'true' : undefined}>
            {saving && <span className="button-spinner" aria-hidden="true" />}
            {saving ? 'Cargando...' : 'Guardar cambios'}
          </button>
        </div>
      </form>
    );
  }

  return (
    <form className="form-grid edit-form" onSubmit={handleSubmit}>
      <FormGroup title="Datos principales">
        <TextField label="Título" value={form.title} required onChange={(title) => setForm({ ...form, title })} />
        <TextField label="Link de publicación" value={form.listingUrl} onChange={(listingUrl) => setForm({ ...form, listingUrl })} />
        <TextField label="Dirección" value={form.address} required onChange={(address) => setForm({ ...form, address })} />
        <TextField label="Barrio" value={form.neighborhood} required onChange={(neighborhood) => setForm({ ...form, neighborhood })} />
      </FormGroup>
      <FormGroup title="Precio y características">
        <TextField label="Precio USD" type="number" value={form.priceUsd} onChange={(priceUsd) => setForm({ ...form, priceUsd })} />
        <TextField label="Expensas mensuales" type="number" value={form.monthlyExpenses} onChange={(monthlyExpenses) => setForm({ ...form, monthlyExpenses })} />
        <TextField label="Metros totales" type="number" value={form.totalSquareMeters} onChange={(totalSquareMeters) => setForm({ ...form, totalSquareMeters })} />
        <TextField label="Metros cubiertos" type="number" value={form.coveredSquareMeters} onChange={(coveredSquareMeters) => setForm({ ...form, coveredSquareMeters })} />
        <TextField label="Ambientes" type="number" value={form.rooms} onChange={(rooms) => setForm({ ...form, rooms })} />
        <TextField label="Dormitorios" type="number" value={form.bedrooms} onChange={(bedrooms) => setForm({ ...form, bedrooms })} />
        <TextField label="Baños" type="number" value={form.bathrooms} onChange={(bathrooms) => setForm({ ...form, bathrooms })} />
        <TextField label="Antigüedad del edificio" type="number" value={form.buildingAgeYears} onChange={(buildingAgeYears) => setForm({ ...form, buildingAgeYears })} />
        <TextField label="Piso" type="number" value={form.floor} onChange={(floor) => setForm({ ...form, floor })} />
        <TextField label="Departamento / Unidad" value={form.apartmentUnit} onChange={(apartmentUnit) => setForm({ ...form, apartmentUnit })} />
        <TextField label="Identificador de unidad" value={form.unitIdentifier} onChange={(unitIdentifier) => setForm({ ...form, unitIdentifier })} />
        <p className="field-helper field-wide">Estos datos ayudan a validar que las expensas correspondan a la propiedad correcta.</p>
        <SelectField label="Balcón" value={form.hasBalcony} onChange={(hasBalcony) => setForm({ ...form, hasBalcony })} />
        <SelectField label="Amenities" value={form.hasAmenities} onChange={(hasAmenities) => setForm({ ...form, hasAmenities })} />
        <SelectField label="Apto crédito" value={form.isCreditEligible} onChange={(isCreditEligible) => setForm({ ...form, isCreditEligible })} />
        <label className="field">
          <span>Estado</span>
          <select value={form.propertyCondition} onChange={(event) => setForm({ ...form, propertyCondition: event.target.value as PropertyCondition })}>
            {(Object.keys(propertyConditionLabels) as PropertyCondition[]).map((condition) => (
              <option key={condition} value={condition}>
                {propertyConditionLabels[condition]}
              </option>
            ))}
          </select>
        </label>
      </FormGroup>
      <FormGroup title="Notas">
        <label className="field field-wide">
          <span>Objetivo, dudas o notas</span>
          <textarea value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} />
        </label>
      </FormGroup>
      <div className="form-actions">
        <button className="ghost-button" type="button" onClick={onCancel}>
          Cancelar
        </button>
        <button className={`primary-button ${saving ? 'btn-loading' : ''}`} type="submit" disabled={saving} aria-busy={saving ? 'true' : undefined}>
          {saving && <span className="button-spinner" aria-hidden="true" />}
          {saving ? 'Cargando...' : 'Guardar cambios'}
        </button>
      </div>
    </form>
  );
}

function AnalysisDetail({
  analysis,
  loading,
  onRefresh,
  onError,
  setLoading,
  initialTab,
  onInitialTabConsumed,
  onAnalysisUpdated
}: {
  analysis: PropertyAnalysis;
  loading: boolean;
  onRefresh: () => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
  initialTab?: WorkspaceTab | null;
  onInitialTabConsumed?: () => void;
  onAnalysisUpdated: (analysis: PropertyAnalysis) => void;
}) {
  const checklistStats = getChecklistStats(analysis.checklistItems);
  const detailOpinion = getAnalysisOpinion(analysis, checklistStats);
  const progressPercent = getAnalysisProgressPercent(analysis, checklistStats);
  const workspaceFormId = `property-edit-${analysis.id}`;
  const [uploadingCover, setUploadingCover] = useState(false);
  const [activeTab, setActiveTab] = useState<WorkspaceTab>('general');
  const [selectedDocumentType, setSelectedDocumentType] = useState<DocumentType | null>(null);
  const [report, setReport] = useState<PropertyReport | null>(null);
  const [reportLoading, setReportLoading] = useState(false);
  const analysisStatus = getWorkspaceAnalysisStatus(analysis, checklistStats, report);
  const protectionKpi = getProtectionKpi(analysis);
  const buildingHealthKpi = getBuildingHealthKpi(analysis.expenseIntelligence);

  useEffect(() => {
    setReport(null);
    setSelectedDocumentType(null);
  }, [analysis.id]);

  useEffect(() => {
    if (!initialTab) {
      return;
    }

    setActiveTab(initialTab);
    onInitialTabConsumed?.();
  }, [initialTab, onInitialTabConsumed]);

  useEffect(() => {
    if (activeTab === 'report' && !report && !reportLoading) {
      void loadReport();
    }
  }, [activeTab, report, reportLoading]);

  async function loadReport() {
    setReportLoading(true);
    setLoading(true);
    onError(null);

    try {
      setReport(await getPropertyReport(analysis.id));
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo cargar el informe.');
    } finally {
      setReportLoading(false);
      setLoading(false);
    }
  }

  async function handleCoverImageChange(file: File | null) {
    if (!file) {
      return;
    }

    if (!isValidCoverImageFile(file)) {
      onError('La imagen principal debe ser JPEG, PNG o WEBP y pesar hasta 5 MB.');
      return;
    }

    setUploadingCover(true);
    setLoading(true);
    onError(null);

    try {
      const updated = await uploadCoverImage(analysis.id, file);
      onAnalysisUpdated(updated);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo actualizar la imagen principal.');
    } finally {
      setUploadingCover(false);
      setLoading(false);
    }
  }

  return (
    <div className="property-edit-page">
      <aside className="property-edit-sidebar">
        <button className="sidebar-back" type="button" onClick={() => window.history.back()}>
          ← Volver a mis oportunidades
        </button>
        <div className="sidebar-property-photo-wrap">
          <PropertyCoverImage className="sidebar-property-photo" coverImageUrl={analysis.coverImageUrl} alt={`Imagen de ${analysis.title}`} />
          <label className={`change-cover-button ${uploadingCover ? 'btn-loading' : ''}`}>
            {uploadingCover && <span className="button-spinner" aria-hidden="true" />}
            {uploadingCover ? 'Cargando...' : 'Cambiar imagen'}
            <input
              type="file"
              accept="image/jpeg,image/png,image/webp"
              disabled={uploadingCover}
              onChange={(event) => void handleCoverImageChange(event.target.files?.[0] ?? null)}
            />
          </label>
        </div>
        <Badge tone={analysisStatus.tone}>{analysisStatus.badge}</Badge>
        <div className="sidebar-property-summary">
          <h2>{analysis.title}</h2>
          <p>{analysis.neighborhood || 'Sin barrio'}, CABA</p>
          <strong>{formatMoney(analysis.priceUsd)} · {formatNumber(analysis.totalSquareMeters)} m²</strong>
        </div>
        <div className="sidebar-progress-list">
          <WorkspaceStep title="Información básica" text="Completado" state={activeTab === 'general' ? 'active' : 'done'} number="1" onClick={() => setActiveTab('general')} />
          <WorkspaceStep title="Precio y características" text="Completado" state={activeTab === 'price' ? 'active' : 'done'} number="2" onClick={() => setActiveTab('price')} />
          <WorkspaceStep title="Documentación" text={checklistStats.percent >= 100 ? 'Completado' : 'En progreso'} state={activeTab === 'documents' ? 'active' : checklistStats.percent >= 100 ? 'done' : 'pending'} number="3" onClick={() => setActiveTab('documents')} />
          <WorkspaceStep title="Observaciones" text={(analysis.visitObservations ?? []).length > 0 ? `${(analysis.visitObservations ?? []).length} registradas` : 'Opcional'} state={activeTab === 'observations' ? 'active' : (analysis.visitObservations ?? []).length > 0 ? 'done' : 'pending'} number="4" onClick={() => setActiveTab('observations')} />
          <WorkspaceStep title="Análisis y hallazgos" text={analysisStatus.findingsText} state={activeTab === 'findings' ? 'active' : analysisStatus.findingsDone ? 'done' : 'pending'} number="5" onClick={() => setActiveTab('findings')} />
          <WorkspaceStep title="Informe final" text={report ? 'Informe generado' : 'Informe pendiente'} state={activeTab === 'report' ? 'active' : report ? 'done' : 'pending'} number="6" onClick={() => setActiveTab('report')} />
        </div>
        <div className="wizard-help-card edit-sidebar-help">
          <span className="wizard-help-icon" aria-hidden="true">◇</span>
          <strong>¿Necesitás ayuda?</strong>
          <p>Nuestro equipo está para ayudarte en cada paso.</p>
          <button className="ghost-button" type="button">Contactar soporte</button>
        </div>
      </aside>

      <main className="property-edit-main">
        <div className="property-edit-titlebar">
          <div>
            <button className="wizard-back" type="button" onClick={() => window.history.back()}>
              ← Volver
            </button>
            <h1>{analysis.title}</h1>
            <div className="property-meta-row">
              <span>{analysis.neighborhood || 'Sin barrio'}, CABA</span>
              <span>{propertyConditionLabels[analysis.propertyCondition]}</span>
              <span>{formatNumber(analysis.totalSquareMeters)} m²</span>
              <span>Creada el {formatDate(analysis.createdAtUtc)}</span>
            </div>
          </div>
          <div className="property-edit-actions">
            <button className="secondary-button" type="button" onClick={() => setActiveTab('documents')}>
              Más acciones
            </button>
            <button className="secondary-button" type="submit" form={workspaceFormId}>
              Guardar cambios
            </button>
            <button className={`primary-button ${loading ? 'btn-loading' : ''}`} type="button" onClick={onRefresh} disabled={loading} aria-busy={loading ? 'true' : undefined}>
              {loading && <span className="button-spinner" aria-hidden="true" />}
              {loading ? 'Cargando...' : 'Analizar propiedad'}
            </button>
          </div>
        </div>

        <section className="edit-kpi-grid">
          <EditKpiCard title="Oportunidad" value={analysis.opportunityScore} label={scoreBadgeLabel('opportunity', analysis.opportunityScore)} text={analysis.benchmarkComparison || 'Comparación pendiente'} tone="success" icon="↗" />
          <EditKpiCard title="Protección" value={protectionKpi.value} label={protectionKpi.label} text={protectionKpi.text} tone={protectionKpi.tone} icon="◇" />
          <EditKpiCard title="Salud del edificio" value={buildingHealthKpi.value} label={buildingHealthKpi.label} text={buildingHealthKpi.text} tone={buildingHealthKpi.tone} icon="▥" />
          <EditKpiCard title="Precio objetivo" value={formatMoney(analysis.negotiationAnalysis?.suggestedTargetPriceUsd ?? analysis.suggestedTargetPriceUsd)} label="Negociación" text="Basado en oportunidad y riesgos identificados" tone="info" />
          <EditOpinionCard opinion={detailOpinion} updatedAt={analysis.updatedAtUtc} />
        </section>

        <nav className="edit-tabs" aria-label="Secciones de la propiedad">
          <button className={activeTab === 'general' ? 'active' : ''} type="button" onClick={() => setActiveTab('general')}>⌂ General</button>
          <button className={activeTab === 'price' ? 'active' : ''} type="button" onClick={() => setActiveTab('price')}>◇ Precio y características</button>
          <button className={activeTab === 'documents' ? 'active' : ''} type="button" onClick={() => setActiveTab('documents')}>▤ Documentos</button>
          <button className={activeTab === 'observations' ? 'active' : ''} type="button" onClick={() => setActiveTab('observations')}>□ Observaciones</button>
          <button className={activeTab === 'findings' ? 'active' : ''} type="button" onClick={() => setActiveTab('findings')}>◌ Hallazgos</button>
          <button className={activeTab === 'report' ? 'active' : ''} type="button" onClick={() => setActiveTab('report')}>▧ Informe</button>
        </nav>

        {activeTab === 'general' && (
        <div className="property-edit-content-grid">
          <div>
            <section className="edit-form-card">
              <PropertyEditForm
                analysis={analysis}
                variant="workspace"
                formId={workspaceFormId}
                onCancel={() => undefined}
                onSaved={onAnalysisUpdated}
                onError={onError}
                setLoading={setLoading}
              />
            </section>

          </div>

          <aside className="edit-right-rail">
            <WorkspaceProgressCard percent={progressPercent} checklistStats={checklistStats} />
            <WizardInfoCard icon="◌" title="Consejo" text="Cuanta más información y documentos subas, más preciso será nuestro análisis y recomendaciones." tone="warning" />
            <WizardInfoCard icon="◇" title="¿Por qué analizamos?" text="Evaluamos oportunidades, riesgos legales, edilicios y financieros para que compres más seguro." />
          </aside>
        </div>
        )}

        {activeTab === 'price' && (
        <>
        <section className="panel negotiation-card edit-continuation-card">
          <div className="section-heading compact-heading">
            <div>
              <p className="eyebrow">Negociación</p>
              <h3>Rango de negociación</h3>
            </div>
            <Badge tone={marketPositionTone(analysis.marketPosition)}>{marketPositionLabel(analysis.marketPosition)}</Badge>
          </div>
          <div className="negotiation-grid">
            <Info label="Precio publicado" value={formatMoney(analysis.negotiationAnalysis?.publishedPriceUsd ?? analysis.priceUsd)} />
            <Info label="USD/m²" value={formatMoney(analysis.negotiationAnalysis?.usdPerSquareMeter ?? analysis.usdPerSquareMeter)} />
            <Info label="Promedio zona" value={formatMoney(analysis.negotiationAnalysis?.benchmarkUsdPerSquareMeter)} />
            <Info label="Valor estimado" value={formatRange(analysis.negotiationAnalysis?.estimatedFairValueMinUsd, analysis.negotiationAnalysis?.estimatedFairValueMaxUsd)} />
            <Info label="Oferta inicial sugerida" value={formatMoney(analysis.negotiationAnalysis?.suggestedFirstOfferUsd ?? analysis.suggestedFirstOfferUsd)} />
            <Info label="Precio objetivo" value={formatMoney(analysis.negotiationAnalysis?.suggestedTargetPriceUsd ?? analysis.suggestedTargetPriceUsd)} />
            <Info label="Maximo recomendado" value={formatMoney(analysis.negotiationAnalysis?.maxRecommendedPriceUsd ?? analysis.maxRecommendedPriceUsd)} />
            <Info label="Margen de negociación" value={formatPercent(analysis.negotiationAnalysis?.negotiationMarginPercent ?? analysis.negotiationMarginPercent)} />
          </div>
          <p className="score-summary">{analysis.negotiationAnalysis?.explanation ?? analysis.negotiationExplanation}</p>
          <p className="reference-note">Valores de referencia estimativos para CABA, basados en tabla interna por barrio y sujetos a actualizacion.</p>
        </section>

        <BuildingHealthSection expenseIntelligence={analysis.expenseIntelligence} />
        </>
        )}

        {activeTab === 'documents' && (
        <DocumentWorkspace
          analysis={analysis}
          selectedDocumentType={selectedDocumentType}
          onSelectDocumentType={setSelectedDocumentType}
          onRefresh={onRefresh}
          onError={onError}
          setLoading={setLoading}
        />
        )}

        {activeTab === 'observations' && (
        <VisitObservationsPanel
          analysis={analysis}
          onSaved={onAnalysisUpdated}
          onError={onError}
          setLoading={setLoading}
        />
        )}

        {activeTab === 'findings' && (
        <section className="panel edit-continuation-card" id="findings-workspace">
          <div className="section-heading compact-heading">
            <div>
              <p className="eyebrow">Hallazgos</p>
              <h3>Señales agrupadas por severidad</h3>
            </div>
          </div>
          <GroupedFindings riskFindings={analysis.riskFindings} documentFindings={analysis.documentFindings} documents={analysis.documents} />
        </section>
        )}

        {activeTab === 'report' && (
        <PropertyReportPanel report={report} analysisId={analysis.id} onError={onError} loading={reportLoading} onRefresh={loadReport} />
        )}
      </main>
    </div>
  );
}

function PropertyReportPanel({
  report,
  analysisId,
  onError,
  loading,
  onRefresh
}: {
  report: PropertyReport | null;
  analysisId: string;
  onError: (message: string | null) => void;
  loading: boolean;
  onRefresh: () => Promise<void>;
}) {
  const [downloadingPdf, setDownloadingPdf] = useState(false);

  async function handleDownloadPdf() {
    if (!report) {
      return;
    }

    setDownloadingPdf(true);
    onError(null);

    try {
      const blob = await downloadPropertyReportPdf(analysisId);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = buildReportPdfFileName(report);
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo descargar el PDF.');
    } finally {
      setDownloadingPdf(false);
    }
  }

  if (!report && loading) {
    return (
      <section className="panel report-page">
        <div className="empty-state">
          <strong>Generando informe...</strong>
          <p>Estamos consolidando oportunidad, proteccion, documentacion y proximos pasos.</p>
        </div>
      </section>
    );
  }

  if (!report) {
    return (
      <section className="panel report-page">
        <div className="empty-state">
          <strong>Informe no disponible</strong>
          <p>Actualiza el informe para generar el resumen de esta propiedad.</p>
          <button className="primary-button" type="button" onClick={() => void onRefresh()}>
            Actualizar informe
          </button>
        </div>
      </section>
    );
  }

  const recommendationTone = reportRecommendationTone(report.overallRecommendation);
  const statusTone = reportStatusTone(report.reportStatus);
  const consistencyNote = getReportConsistencyNote(report);

  return (
    <section className="report-page" id="report-workspace">
      <div className="buyer-report-cover">
        <div>
          <p className="eyebrow">Informe PropSegura</p>
          <h2>{report.coverSection.propertyTitle}</h2>
          <p>{report.coverSection.subtitle}</p>
          <div className="buyer-report-meta">
            <Info label="Barrio" value={report.coverSection.neighborhood || 'Sin dato'} />
            <Info label="Dirección" value={report.coverSection.address || 'Sin dato'} />
            <Info label="Precio" value={formatMoney(report.coverSection.priceUsd)} />
            <Info label="m²" value={formatNumber(report.coverSection.totalSquareMeters)} />
            <Info label="USD/m²" value={formatMoney(report.coverSection.usdPerSquareMeter)} />
            <Info label="Expensas" value={formatMoney(report.coverSection.monthlyExpenses)} />
          </div>
          <div className="badge-row">
            <Badge tone={statusTone}>{report.reportStatusLabel}</Badge>
            <span>Generado el {formatDate(report.generatedAtUtc)}</span>
          </div>
        </div>
        <div className="buyer-report-cover-media">
          <PropertyCoverImage
            className="buyer-report-cover-placeholder"
            coverImageUrl={report.coverSection.coverImageUrl}
            alt={report.coverSection.coverImageUrl ? `Imagen de ${report.coverSection.propertyTitle}` : 'Sin imagen'}
          />
        </div>
      </div>

      <div className="report-header">
        <div>
          <p className="eyebrow">Reporte para comprador</p>
          <h2>Segunda opinión para decidir antes de reservar o comprar.</h2>
        </div>
        <div className="report-actions">
          <button className={`secondary-button ${loading ? 'btn-loading' : ''}`} type="button" onClick={() => void onRefresh()} disabled={loading} aria-busy={loading ? 'true' : undefined}>
            {loading && <span className="button-spinner" aria-hidden="true" />}
            {loading ? 'Cargando...' : 'Actualizar informe'}
          </button>
          <button
            className={`ghost-button ${downloadingPdf ? 'btn-loading' : ''}`}
            type="button"
            onClick={() => void handleDownloadPdf()}
            disabled={downloadingPdf}
            aria-busy={downloadingPdf ? 'true' : undefined}
          >
            {downloadingPdf && <span className="button-spinner" aria-hidden="true" />}
            {downloadingPdf ? 'Descargando...' : 'Descargar PDF'}
          </button>
          <button className="ghost-button" type="button" disabled>Enviar por mail - Próximamente</button>
        </div>
      </div>

      <div className="report-grid report-list-grid report-advisory-grid">
        <ReportList title="Riesgos identificados" items={report.identifiedRisks} emptyText="No se detectaron riesgos específicos con la información cargada." />
        {report.detectedStrengths.length > 0 && (
          <ReportList title="Fortalezas detectadas" items={report.detectedStrengths} emptyText="" />
        )}
        {report.zoneStrengths.length > 0 && (
          <ReportList title="Fortalezas de la zona" items={report.zoneStrengths} emptyText="" />
        )}
        <ReportList title="Palancas de negociación" items={report.negotiationLevers} emptyText="No se detectaron palancas específicas de negociación con la información cargada." />
      </div>

      <article className={`report-recommendation report-recommendation-${recommendationTone}`}>
        <Badge tone={recommendationTone}>{report.overallRecommendationLabel}</Badge>
        <h3>Conclusión ejecutiva</h3>
        <p>{report.executiveConclusion}</p>
        <strong>{report.executiveSummary}</strong>
        <p>{report.overallRecommendationExplanation}</p>
        {consistencyNote && <p className="report-consistency-note">{consistencyNote}</p>}
      </article>

      <div className="report-summary-cards">
        <Info label="OpportunityScore" value={formatNumber(report.opportunitySection.opportunityScore)} />
        <Info label="ProtectionScore" value={formatNumber(report.protectionSection.protectionScore)} />
        <Info label="Precio objetivo" value={formatMoney(report.negotiationSection.suggestedTargetPriceUsd)} />
        <Info label="BuildingHealthScore" value={formatNumber(report.expensesSection.buildingHealthScore)} />
      </div>

      <ReportMoneyRisks risks={report.moneyRisks} />
      <ReportNegotiationOpportunities opportunities={report.negotiationOpportunities} />

      <div className="report-grid">
        <ReportCard title="Datos de la propiedad">
          <div className="summary-grid compact-report-grid">
            <Info label="Propiedad" value={report.propertySummary.title} />
            <Info label="Dirección" value={report.propertySummary.address} />
            <Info label="Barrio" value={report.propertySummary.neighborhood} />
            <Info label="Precio" value={formatMoney(report.propertySummary.priceUsd)} />
            <Info label="Metros" value={formatNumber(report.propertySummary.totalSquareMeters)} />
            <Info label="USD/m2" value={formatMoney(report.propertySummary.usdPerSquareMeter)} />
            <Info label="Expensas" value={formatMoney(report.propertySummary.monthlyExpenses)} />
            <Info label="Unidad" value={formatReportUnit(report.propertySummary)} />
          </div>
        </ReportCard>

        <ReportCard title="Evaluación de oportunidad">
          <p>{report.opportunitySection.explanation}</p>
          <div className="badge-row">
            <Badge tone="success">{report.opportunitySection.recommendedUseLabel}</Badge>
            <Badge tone={riskTone(report.opportunitySection.initialRiskLevel)}>{report.opportunitySection.initialRiskLevelLabel}</Badge>
            <Badge tone="info">{report.opportunitySection.benchmarkComparison}</Badge>
          </div>
        </ReportCard>

        <ReportCard title="Estrategia sugerida">
          <p>{report.negotiationSection.suggestedStrategy}</p>
          <p>{report.negotiationSection.explanation}</p>
          <div className="summary-grid compact-report-grid">
            <Info label="Precio publicado" value={formatMoney(report.negotiationSection.publishedPriceUsd)} />
            <Info label="Valor estimado" value={formatRange(report.negotiationSection.estimatedFairValueMinUsd, report.negotiationSection.estimatedFairValueMaxUsd)} />
            <Info label="Oferta inicial" value={formatMoney(report.negotiationSection.suggestedFirstOfferUsd)} />
            <Info label="Maximo recomendado" value={formatMoney(report.negotiationSection.maxRecommendedPriceUsd)} />
            <Info label="Margen" value={formatPercent(report.negotiationSection.negotiationMarginPercent)} />
          </div>
        </ReportCard>

        <ReportCard title="Validación documental">
          <p>{report.protectionSection.explanation}</p>
          <p>{report.protectionSection.documentConfidenceSummary}</p>
          <div className="summary-grid compact-report-grid">
            <Info label="Requeridos" value={formatNumber(report.documentsSection.requiredDocumentsCount)} />
            <Info label="Cargados" value={formatNumber(report.documentsSection.providedDocumentsCount)} />
            <Info label="Pendientes" value={formatNumber(report.documentsSection.missingDocumentsCount)} />
            <Info label="Altos/Críticos" value={formatNumber(report.protectionSection.highOrCriticalFindingsCount)} />
          </div>
        </ReportCard>

        <ReportDocumentSection title="Reserva" section={report.reservationSection} emptyText="Reserva no analizada todavia." />
        <ReportExpensesSection section={report.expensesSection} />
      </div>

      <ReportDocumentsChecklist reviewed={report.reviewedDocuments} pending={report.pendingDocuments} />
      <ReportFindingsSection section={report.findingsSection} />

      <div className="report-grid report-list-grid">
        <ReportList title="Qué significa esto para vos" items={report.whatThisMeansForYou} emptyText="Todavía faltan datos para generar consecuencias concretas." />
        <ReportList title="Condiciones mínimas para avanzar" items={report.conditionsToAdvance} emptyText="No hay condiciones específicas detectadas." />
        <ReportList title="Puntos principales para negociar" items={report.negotiationArguments.slice(0, 3)} emptyText="No hay argumentos fuertes por ahora." />
        <ReportList title="Preguntas para la inmobiliaria o el vendedor" items={report.questionsForSellerOrAgent} emptyText="No hay preguntas sugeridas por ahora." />
        <ReportList title="Preguntas para escribano o abogado" items={report.questionsForNotaryOrLawyer} emptyText="No hay preguntas legales sugeridas por ahora." />
        <ReportList title="Próximos pasos" items={report.nextSteps} emptyText="No hay próximos pasos sugeridos." />
      </div>

      <div className="wizard-disclaimer edit-report-note">
        <span aria-hidden="true">◇</span>
        <p>{report.disclaimer}</p>
      </div>
    </section>
  );
}

function ReportCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <article className="panel report-card">
      <h3>{title}</h3>
      {children}
    </article>
  );
}

function ReportDocumentSection({
  title,
  section,
  emptyText
}: {
  title: string;
  section: PropertyReport['reservationSection'];
  emptyText: string;
}) {
  return (
    <ReportCard title={title}>
      {!section.isAvailable || !section.isAnalyzed ? (
        <div className="empty-state compact-empty">
          <strong>{emptyText}</strong>
          <p>{section.status}</p>
        </div>
      ) : (
        <>
          <Badge tone={findingTone(section.riskLevel ?? 'Info')}>{findingSeverityLabel(section.riskLevel ?? 'Info')}</Badge>
          <ReportFindingPreview findings={section.keyFindings} />
        </>
      )}
    </ReportCard>
  );
}

function ReportExpensesSection({ section }: { section: PropertyReport['expensesSection'] }) {
  const expenseBenchmarkTone = section.expenseBenchmarkComparison?.startsWith('Por debajo')
    ? 'success'
    : section.expenseBenchmarkComparison?.startsWith('Muy por encima')
      ? 'critical'
      : section.expenseBenchmarkComparison?.startsWith('Por encima')
        ? 'warning'
        : 'info';

  return (
    <ReportCard title="Expensas">
      <div className="summary-grid compact-report-grid expense-benchmark-grid">
        <Info label="Fuente del analisis" value={section.analysisSource} />
        <Info label="Expensas publicadas" value={formatMoney(section.publishedMonthlyExpenses)} />
        <Info label="Expensas en documento" value={formatMoney(section.detectedExpenseAmount)} />
        <Info label="Publicacion vs documento" value={section.publishedVsDocumentStatus} />
        <Info label="Metros usados" value={formatNumber(section.totalSquareMeters)} />
        <Info label="Expensas ARS/m2" value={formatMoney(section.monthlyExpensesPerSquareMeter)} />
        <Info label="Benchmark zona ARS/m2" value={formatMoney(section.benchmarkMonthlyExpensesPerSquareMeter)} />
        <Info label="Unidad" value={section.unitValidationStatus ?? 'Pendiente'} />
      </div>
      <div className="expense-benchmark-note">
        <Badge tone={expenseBenchmarkTone}>{section.expenseBenchmarkComparison}</Badge>
        <p>Formula: expensas mensuales / m2 totales. No se compara ARS contra USD.</p>
      </div>
      {!section.isAvailable ? (
        <div className="empty-state compact-empty">
          <strong>Sin documento de expensas cargado.</strong>
          <p>{section.status}</p>
        </div>
      ) : !section.isAnalyzed ? (
        <div className="empty-state compact-empty">
          <strong>Documento de expensas pendiente de analisis.</strong>
          <p>{section.status}</p>
        </div>
      ) : (
        <>
          <div className="summary-grid compact-report-grid">
            <Info label="Salud edificio" value={formatNumber(section.buildingHealthScore)} />
            <Info label="Clasificacion" value={section.buildingHealthClassification ?? 'Sin dato'} />
            <Info label="Confianza" value={section.documentConfidence ? `${section.documentConfidence.confidenceScore}%` : 'Sin dato'} />
            <Info label="Confirmado por usuario" value={section.documentConfirmedByUser ? 'Si' : 'No'} />
          </div>
          <ReportFindingPreview findings={section.keyFindings} />
        </>
      )}
    </ReportCard>
  );
}

function ReportMoneyRisks({ risks }: { risks: PropertyReport['moneyRisks'] }) {
  if (risks.length === 0) {
    return (
      <ReportCard title="Riesgos que podrían costarte dinero">
        <div className="empty-state compact-empty">
          <strong>No hay riesgos económicos fuertes detectados</strong>
          <p>Con la información actual, no encontramos señales económicas críticas. Seguí completando documentos para mejorar la revisión.</p>
        </div>
      </ReportCard>
    );
  }

  return (
    <ReportCard title="Riesgos que podrían costarte dinero">
      <div className="money-risk-list">
        {risks.map((risk) => (
          <article key={`${risk.title}-${risk.sourceDocumentLabel ?? 'property'}`} className="money-risk-card">
            <div>
              <Badge tone="warning">{risk.sourceDocumentLabel ?? 'Propiedad'}</Badge>
              <h4>{risk.title}</h4>
              <p><strong>Por qué importa:</strong> {risk.whyItMatters}</p>
              <p><strong>Impacto posible:</strong> {risk.buyerImpact}</p>
              <p><strong>Acción sugerida:</strong> {risk.suggestedAction}</p>
            </div>
          </article>
        ))}
      </div>
    </ReportCard>
  );
}

function ReportNegotiationOpportunities({ opportunities }: { opportunities: PropertyReport['negotiationOpportunities'] }) {
  if (opportunities.length === 0) {
    return (
      <ReportCard title="Oportunidades de negociación detectadas">
        <div className="empty-state compact-empty">
          <strong>No se detectaron oportunidades específicas de negociación con la información cargada.</strong>
          <p>Sumá observaciones de la visita o documentos para que PropSegura transforme esas señales en argumentos concretos.</p>
        </div>
      </ReportCard>
    );
  }

  return (
    <ReportCard title="Oportunidades de negociación detectadas">
      <div className="negotiation-opportunity-list">
        {opportunities.map((opportunity) => (
          <article key={opportunity.title} className="negotiation-opportunity-card">
            <div className="negotiation-opportunity-header">
              <h4>{opportunity.title}</h4>
              <Badge tone={opportunityImpactTone(opportunity.impact)}>{opportunity.impact}</Badge>
            </div>
            <p>{opportunity.description}</p>
            <p><strong>Acción sugerida:</strong> {opportunity.suggestedAction}</p>
          </article>
        ))}
      </div>
    </ReportCard>
  );
}

function opportunityImpactTone(impact: string) {
  if (impact === 'Alto') {
    return 'critical';
  }

  if (impact === 'Medio') {
    return 'warning';
  }

  return 'info';
}

function ReportDocumentsChecklist({
  reviewed,
  pending
}: {
  reviewed: PropertyReport['reviewedDocuments'];
  pending: PropertyReport['pendingDocuments'];
}) {
  return (
    <ReportCard title="Checklist documental">
      <div className="report-document-checklist">
        <div>
          <h4>Documentos revisados</h4>
          {reviewed.length === 0 ? (
            <p className="section-copy">Todavía no hay documentos analizados.</p>
          ) : (
            reviewed.map((document) => (
              <div className="report-document-row" key={`${document.documentType}-${document.status}`}>
                <strong>{document.documentLabel}</strong>
                <Badge tone={document.isAnalyzed ? 'success' : 'warning'}>{document.status}</Badge>
              </div>
            ))
          )}
        </div>
        <div>
          <h4>Documentos pendientes</h4>
          {pending.map((document) => (
            <div className="report-document-row" key={`${document.documentType}-${document.status}`}>
              <strong>{document.documentLabel}</strong>
              <Badge tone={document.isFuture ? 'neutral' : 'warning'}>{document.status}</Badge>
            </div>
          ))}
        </div>
      </div>
    </ReportCard>
  );
}

function ReportFindingPreview({ findings }: { findings: DocumentFinding[] }) {
  if (findings.length === 0) {
    return <p className="section-copy">No hay hallazgos relevantes en esta seccion.</p>;
  }

  return (
    <div className="report-finding-preview">
      {findings.slice(0, 4).map((finding) => (
        <article key={finding.id}>
          <Badge tone={findingTone(finding.severity)}>{findingSeverityLabel(finding.severity)}</Badge>
          <strong>{finding.title}</strong>
          <p>{finding.buyerImpact ?? finding.description}</p>
        </article>
      ))}
    </div>
  );
}

function ReportFindingsSection({ section }: { section: PropertyReport['findingsSection'] }) {
  if (section.groups.length === 0) {
    return (
      <ReportCard title="Hallazgos principales">
        <div className="empty-state compact-empty">
          <strong>No hay hallazgos detectados todavia</strong>
          <p>Cuando analices documentos, las senales relevantes van a aparecer en este informe.</p>
        </div>
      </ReportCard>
    );
  }

  return (
    <ReportCard title="Hallazgos principales">
      <div className="finding-groups report-finding-groups">
        {section.groups.map((group) => (
          <section className="finding-group" key={group.severity}>
            <div className="finding-group-heading">
              <Badge tone={findingTone(group.severity)}>{group.severityLabel}</Badge>
              <span>{group.findings.length} hallazgos</span>
            </div>
            <ReportFindingPreview findings={group.findings} />
          </section>
        ))}
      </div>
    </ReportCard>
  );
}

function ReportList({ title, items, emptyText }: { title: string; items: string[]; emptyText: string }) {
  return (
    <ReportCard title={title}>
      {items.length === 0 ? (
        <p className="section-copy">{emptyText}</p>
      ) : (
        <ul className="report-list">
          {items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      )}
    </ReportCard>
  );
}

function WorkspaceStep({
  title,
  text,
  state = 'pending',
  number,
  onClick
}: {
  title: string;
  text: string;
  state?: 'done' | 'active' | 'pending';
  number?: string;
  onClick?: () => void;
}) {
  const content = (
    <>
      <span>{state === 'done' ? '✓' : number}</span>
      <div>
        <strong>{title}</strong>
        <p>{text}</p>
      </div>
    </>
  );

  if (onClick) {
    return (
      <button className={`workspace-step workspace-step-button workspace-step-${state}`} type="button" onClick={onClick}>
        {content}
      </button>
    );
  }

  return (
    <div className={`workspace-step workspace-step-${state}`}>
      {content}
    </div>
  );
}

function EditKpiCard({
  title,
  value,
  label,
  text,
  tone,
  icon
}: {
  title: string;
  value: number | string;
  label: string;
  text: string;
  tone: 'success' | 'warning' | 'info' | 'neutral';
  icon?: string;
}) {
  return (
    <article className={`edit-kpi-card edit-kpi-${tone}`}>
      <div>
        <h3>{title}</h3>
        <div className="edit-kpi-value">
          <strong>{value}</strong>
          <span>{label}</span>
        </div>
        <p>{text}</p>
      </div>
      {icon && <span aria-hidden="true">{icon}</span>}
    </article>
  );
}

function EditOpinionCard({
  opinion,
  updatedAt
}: {
  opinion: { label: string; tone: string };
  updatedAt: string;
}) {
  return (
    <article className="edit-kpi-card edit-opinion-card">
      <div className="edit-opinion-copy">
        <h3>Mi opinión</h3>
        <strong className={`edit-opinion-label opinion-${opinion.tone}`}>{opinion.label}</strong>
        <p>Última actualización: {formatDate(updatedAt)}</p>
      </div>
      <button className="ghost-button" type="button" aria-label="Editar opinión">✎</button>
    </article>
  );
}

function WorkspaceProgressCard({ percent, checklistStats }: { percent: number; checklistStats: ChecklistStats }) {
  return (
    <article className="wizard-side-card wizard-progress-card">
      <h3>Progreso del análisis</h3>
      <div className="wizard-progress-body">
        <div className="wizard-progress-ring" style={{ '--progress': `${percent}%` } as CSSProperties}>
          <strong>{percent}%</strong>
        </div>
        <p>Completá los pasos para obtener el análisis completo de la propiedad.</p>
      </div>
      <div className="wizard-progress-list">
        <span className="done">Información básica</span>
        <span className="done">Precio y características</span>
        <span className={checklistStats.percent >= 100 ? 'done' : 'active'}>Documentación</span>
        <span>Análisis y hallazgos</span>
        <span>Informe final</span>
      </div>
    </article>
  );
}

function ScoreMeter({
  score,
  variant,
  label
}: {
  score: number;
  variant: 'opportunity' | 'protection';
  label: string;
}) {
  return (
    <div className={`score-meter score-meter-${variant}`} style={{ '--score': `${score}%` } as CSSProperties}>
      <div className="score-ring">
        <strong>{score}</strong>
        <span>{label}</span>
      </div>
    </div>
  );
}

function Badge({ tone, children }: { tone: string; children: ReactNode }) {
  return <span className={`badge badge-${tone}`}>{children}</span>;
}

function ChecklistProgress({ stats }: { stats: ChecklistStats }) {
  return (
    <div className="checklist-progress">
      <div className="progress-copy">
        <strong>{stats.provided} de {stats.required}</strong>
        <span>documentos requeridos cargados</span>
      </div>
      <div className="progress-track" aria-hidden="true">
        <span style={{ width: `${stats.percent}%` }} />
      </div>
    </div>
  );
}

function BuildingHealthSection({ expenseIntelligence }: { expenseIntelligence?: ExpenseIntelligenceAnalysis | null }) {
  if (!expenseIntelligence) {
    return (
      <section className="panel building-health-card edit-continuation-card">
        <div className="section-heading compact-heading">
          <div>
            <p className="eyebrow">Expensas</p>
            <h3>Salud del edificio</h3>
            <p className="section-copy">Subí y analizá una liquidación de expensas para detectar costos ocultos, obras, morosidad o riesgos del consorcio.</p>
          </div>
          <Badge tone="neutral">Pendiente</Badge>
        </div>
      </section>
    );
  }

  return (
    <section className="panel building-health-card edit-continuation-card">
      <div className="section-heading compact-heading">
        <div>
          <p className="eyebrow">Expensas</p>
          <h3>Salud del edificio</h3>
          <p className="section-copy">{expenseIntelligence.summary}</p>
        </div>
        <Badge tone={buildingHealthTone(expenseIntelligence.buildingHealthScore)}>{expenseIntelligence.classification}</Badge>
      </div>

      <div className="building-health-layout">
        <div className={`building-health-score ${buildingHealthTone(expenseIntelligence.buildingHealthScore)}`}>
          <strong>{expenseIntelligence.buildingHealthScore}</strong>
          <span>BuildingHealthScore</span>
        </div>
        <div className="building-health-lists">
          <div>
            <h4>Preguntas sugeridas</h4>
            {expenseIntelligence.suggestedQuestions.length === 0 ? (
              <p className="section-copy">No hay preguntas nuevas por ahora.</p>
            ) : (
              <ul>
                {expenseIntelligence.suggestedQuestions.map((question) => (
                  <li key={question}>{question}</li>
                ))}
              </ul>
            )}
          </div>
          <div>
            <h4>Puntos para negociar</h4>
            {expenseIntelligence.suggestedNegotiationPoints.length === 0 ? (
              <p className="section-copy">No hay puntos nuevos por ahora.</p>
            ) : (
              <ul>
                {expenseIntelligence.suggestedNegotiationPoints.map((point) => (
                  <li key={point}>{point}</li>
                ))}
              </ul>
            )}
          </div>
        </div>
      </div>

      {expenseIntelligence.findings.length > 0 && (
        <div className="building-finding-strip">
          {expenseIntelligence.findings.map((finding) => (
            <article className="building-finding" key={finding.id}>
              <Badge tone={findingTone(finding.severity)}>{findingSeverityLabel(finding.severity)}</Badge>
              <strong>{finding.title}</strong>
              <p>{finding.buyerImpact ?? finding.description}</p>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

function MissingDocuments({ items }: { items: ChecklistItem[] }) {
  const missing = items.filter((item) => item.isRequired && !item.isProvided);

  if (missing.length === 0) {
    return (
      <div className="empty-state compact-empty">
        <strong>Checklist documental completo</strong>
        <p>Los documentos requeridos ya figuran como cargados.</p>
      </div>
    );
  }

  return (
    <div className="missing-list">
      <span className="mini-title">Documentos pendientes</span>
      {missing.slice(0, 4).map((item) => (
        <span className="missing-chip" key={item.id}>{item.title}</span>
      ))}
    </div>
  );
}

function PropertySummary({ analysis }: { analysis: PropertyAnalysis }) {
  return (
    <div className="summary-grid">
      <Info label="Dirección" value={analysis.address} />
      <Info label="Barrio" value={analysis.neighborhood} />
      <Info label="Precio USD" value={formatMoney(analysis.priceUsd)} />
      <Info label="Expensas" value={formatMoney(analysis.monthlyExpenses)} />
      <Info label="Metros totales" value={formatNumber(analysis.totalSquareMeters)} />
      <Info label="Metros cubiertos" value={formatNumber(analysis.coveredSquareMeters)} />
      <Info label="Ambientes" value={formatNumber(analysis.rooms)} />
      <Info label="Dormitorios" value={formatNumber(analysis.bedrooms)} />
      <Info label="Baños" value={formatNumber(analysis.bathrooms)} />
      <Info label="Antigüedad" value={formatNumber(analysis.buildingAgeYears)} />
      <Info label="Piso" value={formatNumber(analysis.floor)} />
      <Info label="Departamento / Unidad" value={analysis.apartmentUnit || 'Sin dato'} />
      <Info label="Identificador de unidad" value={analysis.unitIdentifier || 'Sin dato'} />
      <Info label="Balcón" value={formatBoolean(analysis.hasBalcony)} />
      <Info label="Amenities" value={formatBoolean(analysis.hasAmenities)} />
      <Info label="Apto crédito" value={formatBoolean(analysis.isCreditEligible)} />
      <Info label="Estado" value={propertyConditionLabels[analysis.propertyCondition]} />
      <Info label="Link" value={analysis.listingUrl || 'Sin link'} />
      <Info label="Notas" value={analysis.notes || 'Sin notas'} />
    </div>
  );
}

function Checklist({ items }: { items: ChecklistItem[] }) {
  return (
    <div className="checklist-list">
      {items.map((item) => (
        <div className="checklist-row" key={item.id}>
          <div>
            <strong>{item.title}</strong>
            <p>{item.description}</p>
          </div>
          <Badge tone={checklistTone(item.status)}>{checklistStatusLabel(item.status)}</Badge>
        </div>
      ))}
    </div>
  );
}

function VisitObservationsPanel({
  analysis,
  onSaved,
  onError,
  setLoading
}: {
  analysis: PropertyAnalysis;
  onSaved: (analysis: PropertyAnalysis) => void;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
}) {
  const [selectedObservations, setSelectedObservations] = useState<string[]>(() => analysis.visitObservations ?? []);
  const [visitNotes, setVisitNotes] = useState(() => analysis.visitNotes ?? '');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setSelectedObservations(analysis.visitObservations ?? []);
    setVisitNotes(analysis.visitNotes ?? '');
  }, [analysis.id, analysis.visitObservations, analysis.visitNotes]);

  const observationCount = selectedObservations.length;

  function toggleObservation(observation: string) {
    setSelectedObservations((current) =>
      current.includes(observation)
        ? current.filter((item) => item !== observation)
        : [...current, observation]
    );
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setLoading(true);
    onError(null);

    try {
      const updated = await updateAnalysis(analysis.id, {
        ...buildUpdateRequestFromAnalysis(analysis),
        visitObservations: selectedObservations,
        visitNotes: nullableText(visitNotes)
      });

      onSaved(updated);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudieron guardar las observaciones de la visita.');
    } finally {
      setSaving(false);
      setLoading(false);
    }
  }

  return (
    <section className="panel visit-observations-panel">
      <div className="section-heading compact-heading">
        <div>
          <p className="eyebrow">Visita</p>
          <h3>Observaciones de la visita</h3>
        </div>
        <Badge tone={observationCount > 0 ? 'info' : 'neutral'}>
          {observationCount > 0 ? `${observationCount} observaciones registradas` : 'Sin observaciones registradas'}
        </Badge>
      </div>

      <form className="visit-observations-form" onSubmit={handleSubmit}>
        <div className="visit-observation-grid">
          {visitObservationOptions.map((observation) => (
            <label className="visit-observation-option" key={observation}>
              <input
                type="checkbox"
                checked={selectedObservations.includes(observation)}
                onChange={() => toggleObservation(observation)}
              />
              <span>{observation}</span>
            </label>
          ))}
        </div>

        <label className="field visit-notes-field">
          <span>Notas de la visita</span>
          <textarea
            value={visitNotes}
            placeholder="Describí cualquier situación observada durante la visita que consideres relevante para la negociación o evaluación."
            onChange={(event) => setVisitNotes(event.target.value)}
          />
        </label>

        <div className="edit-inline-actions">
          <button className={`primary-button ${saving ? 'btn-loading' : ''}`} type="submit" disabled={saving} aria-busy={saving ? 'true' : undefined}>
            {saving && <span className="button-spinner" aria-hidden="true" />}
            {saving ? 'Guardando...' : 'Guardar observaciones'}
          </button>
        </div>
      </form>
    </section>
  );
}

function DocumentWorkspace({
  analysis,
  selectedDocumentType,
  onSelectDocumentType,
  onRefresh,
  onError,
  setLoading
}: {
  analysis: PropertyAnalysis;
  selectedDocumentType: DocumentType | null;
  onSelectDocumentType: (documentType: DocumentType | null) => void;
  onRefresh: () => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
}) {
  const stats = getDocumentWorkspaceStats(analysis);
  const [showFutureDocuments, setShowFutureDocuments] = useState(false);
  const [documentOperation, setDocumentOperation] = useState<DocumentOperation | null>(null);
  const selectedDocuments = selectedDocumentType
    ? getDocumentsByType(analysis.documents, selectedDocumentType)
    : [];
  const activeDocument = selectedDocuments[0] ?? null;
  const activeFindings = activeDocument
    ? analysis.documentFindings.filter((finding) => finding.propertyDocumentId === activeDocument.id)
    : [];

  return (
    <section className="document-workspace-v2" id="documents-workspace">
      <div className="section-heading document-workspace-heading">
        <div>
          <p className="eyebrow">Documentos</p>
          <h3>Validación documental</h3>
          <p className="section-copy">Trabajá un documento por vez: cargá, revisá texto, analizá y leé solo sus resultados.</p>
        </div>
      </div>

      <div className="document-summary-grid">
        <DocumentSummaryCard title="Cargados" value={stats.uploaded} text="tipos con archivo" />
        <DocumentSummaryCard title="Analizados" value={stats.analyzed} text="con resultado disponible" />
        <DocumentSummaryCard title="Pendientes" value={stats.pending} text="sin cargar todavía" />
        <DocumentSummaryCard title="Críticos" value={stats.criticalFindings} text="hallazgos documentales" tone={stats.criticalFindings > 0 ? 'critical' : 'success'} />
      </div>

      {documentOperation && (
        <DocumentOperationBanner operation={documentOperation} onDismiss={() => setDocumentOperation(null)} />
      )}

      <div className="document-workspace-grid">
        <aside className="document-type-panel" aria-label="Tipos de documento">
          <div className="document-type-panel-header">
            <strong>Documentos</strong>
            <span>PropSegura V1</span>
          </div>
          <DocumentTypeGroup
            title="Disponibles ahora"
            icon="✦"
            documentTypes={availableDocumentTypes}
            analysis={analysis}
            selectedDocumentType={selectedDocumentType}
            onSelectDocumentType={onSelectDocumentType}
          />
          <button className="future-documents-toggle" type="button" onClick={() => setShowFutureDocuments((value) => !value)}>
            <span>Futuras versiones</span>
            <Badge tone="neutral">{futureDocumentTypes.length} documentos</Badge>
          </button>
          {showFutureDocuments && (
            <DocumentTypeGroup
              title="Disponible en futuras versiones"
              icon="▧"
              documentTypes={futureDocumentTypes}
              analysis={analysis}
              selectedDocumentType={selectedDocumentType}
              onSelectDocumentType={onSelectDocumentType}
              future
            />
          )}
          <DocumentRoadmapFooter />
        </aside>

        <div className="selected-document-panel">
          {!selectedDocumentType ? (
            <DocumentStartState analysisId={analysis.id} onRefresh={onRefresh} onError={onError} setLoading={setLoading} onOperation={setDocumentOperation} />
          ) : isFutureDocumentType(selectedDocumentType) ? (
            <FutureDocumentPlaceholder documentType={selectedDocumentType} />
          ) : (
            <SelectedDocumentWorkspace
              analysisId={analysis.id}
              analysis={analysis}
              documentType={selectedDocumentType}
              document={activeDocument}
              olderDocumentsCount={Math.max(0, selectedDocuments.length - 1)}
              findings={activeFindings}
              expenseIntelligence={selectedDocumentType === 'Expenses' ? analysis.expenseIntelligence : null}
              onRefresh={onRefresh}
              onError={onError}
              setLoading={setLoading}
              onOperation={setDocumentOperation}
            />
          )}
          {/*
          {!selectedDocumentType ? (
            <div className="document-select-empty">
              <span aria-hidden="true">▤</span>
              <strong>Seleccione un documento</strong>
              <p>Elegí el tipo de documento que querés cargar, revisar o analizar.</p>
            </div>
          ) : (
            <SelectedDocumentWorkspace
              analysisId={analysis.id}
              analysis={analysis}
              documentType={selectedDocumentType}
              document={activeDocument}
              olderDocumentsCount={Math.max(0, selectedDocuments.length - 1)}
              findings={activeFindings}
              expenseIntelligence={selectedDocumentType === 'Expenses' ? analysis.expenseIntelligence : null}
              onRefresh={onRefresh}
              onError={onError}
              setLoading={setLoading}
            />
          */}
        </div>
      </div>
    </section>
  );
}

function DocumentOperationBanner({
  operation,
  onDismiss
}: {
  operation: DocumentOperation;
  onDismiss: () => void;
}) {
  const busy = operation.stage === 'uploading' || operation.stage === 'extracting';

  return (
    <div className={`document-operation-banner ${operation.stage}`} role={busy ? 'status' : 'alert'} aria-live="polite">
      {busy && <span className="button-spinner" aria-hidden="true" />}
      <div>
        <strong>{cleanDocumentTypeLabel(operation.documentType)}</strong>
        <p>{operation.message}</p>
      </div>
      {!busy && (
        <button className="banner-close" type="button" onClick={onDismiss}>
          Cerrar
        </button>
      )}
    </div>
  );
}

function DocumentSummaryCard({
  title,
  value,
  text,
  tone = 'neutral'
}: {
  title: string;
  value: number;
  text: string;
  tone?: string;
}) {
  return (
    <article className={`document-summary-card document-summary-${tone}`}>
      <strong>{value}</strong>
      <div>
        <span>{title}</span>
        <p>{text}</p>
      </div>
    </article>
  );
}

function DocumentTypeGroup({
  title,
  icon,
  documentTypes,
  analysis,
  selectedDocumentType,
  onSelectDocumentType,
  future = false
}: {
  title: string;
  icon: string;
  documentTypes: DocumentType[];
  analysis: PropertyAnalysis;
  selectedDocumentType: DocumentType | null;
  onSelectDocumentType: (documentType: DocumentType) => void;
  future?: boolean;
}) {
  return (
    <div className={`document-type-group ${future ? 'document-type-group-future' : ''}`}>
      <div className="document-type-group-heading">
        <span aria-hidden="true">{icon}</span>
        <strong>{title}</strong>
      </div>
      <div className="document-type-list">
        {documentTypes.map((documentType) => (
          <DocumentTypeItem
            key={documentType}
            documentType={documentType}
            documents={getDocumentsByType(analysis.documents, documentType)}
            findings={getFindingsForDocumentType(analysis.documentFindings, analysis.documents, documentType)}
            selected={selectedDocumentType === documentType}
            future={future}
            onClick={() => onSelectDocumentType(documentType)}
          />
        ))}
      </div>
    </div>
  );
}

function DocumentRoadmapFooter() {
  return (
    <div className="document-roadmap-footer">
      <strong>PropSegura V1</strong>
      <span>✓ Expensas</span>
      <span>✓ Reserva</span>
      <strong>Próximamente</strong>
      <span>Actas de consorcio</span>
      <span>Plano del departamento</span>
      <span>Informe de dominio e inhibiciones</span>
      <span>Escritura</span>
    </div>
  );
}

function DocumentStartState({
  analysisId,
  onRefresh,
  onError,
  setLoading,
  onOperation
}: {
  analysisId: string;
  onRefresh: () => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
  onOperation: (operation: DocumentOperation | null) => void;
}) {
  return (
    <div className="document-start-state">
      <div>
        <p className="eyebrow">Seleccione un documento</p>
        <h3>Elegí por dónde empezar</h3>
        <p>PropSegura V1 analiza expensas y reserva. Te recomendamos empezar por expensas para entender costos, riesgos del consorcio y señales edilicias antes de reservar.</p>
      </div>

      <div className="document-start-grid">
        <article className="document-start-card document-start-card-featured">
          <span aria-hidden="true">▤</span>
          <div>
            <h4>Expensas</h4>
            <Badge tone="success">Recomendado para comenzar</Badge>
            <p>Las expensas suelen revelar obras pendientes, extraordinarias, humedad, problemas edilicios, juicios y gastos futuros.</p>
          </div>
          <label className="primary-button file-action-button">
            Subir expensas
            <AddDocumentInlineInput analysisId={analysisId} documentType="Expenses" onRefresh={onRefresh} onError={onError} setLoading={setLoading} onOperation={onOperation} />
          </label>
        </article>

        <article className="document-start-card">
          <span aria-hidden="true">▧</span>
          <div>
            <h4>Reserva</h4>
            <p>Analizá cláusulas, penalidades, condiciones de devolución y obligaciones antes de comprometer dinero.</p>
          </div>
          <label className="secondary-button file-action-button">
            Subir reserva
            <AddDocumentInlineInput analysisId={analysisId} documentType="ReservationAgreement" onRefresh={onRefresh} onError={onError} setLoading={setLoading} onOperation={onOperation} />
          </label>
        </article>
      </div>
    </div>
  );
}

function FutureDocumentPlaceholder({ documentType }: { documentType: DocumentType }) {
  const content = futureDocumentContent(documentType);

  return (
    <div className="future-document-placeholder">
      <span aria-hidden="true">{content.icon}</span>
      <Badge tone="neutral">Disponible próximamente</Badge>
      <h3>{content.title}</h3>
      <p>{content.description}</p>
      <ul>
        {content.items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}

function DocumentTypeItem({
  documentType,
  documents,
  findings,
  selected,
  future = false,
  onClick
}: {
  documentType: DocumentType;
  documents: PropertyDocument[];
  findings: DocumentFinding[];
  selected: boolean;
  future?: boolean;
  onClick: () => void;
}) {
  const latestDocument = documents[0] ?? null;
  const risk = documentRiskLevel(findings);
  const hasDocument = Boolean(latestDocument);
  const uiState = getDocumentUiState(latestDocument);

  return (
    <button className={`document-type-item ${selected ? 'selected' : ''} ${future ? 'future' : ''}`} type="button" onClick={onClick}>
      <span className={`document-type-state ${future ? 'future' : uiState.state}`} aria-hidden="true">
        {future ? '...' : uiState.icon}
      </span>
      <span className="document-type-main">
        <strong>{cleanDocumentTypeLabel(documentType)}</strong>
        <small>{future ? 'Disponible en futuras versiones' : uiState.label}</small>
      </span>
      {hasDocument && findings.length > 0 && (
        <Badge tone={findingTone(risk)}>{findingSeverityLabel(risk)}</Badge>
      )}
    </button>
  );
}

function SelectedDocumentWorkspace({
  analysisId,
  analysis,
  documentType,
  document,
  olderDocumentsCount,
  findings,
  expenseIntelligence,
  onRefresh,
  onError,
  setLoading,
  onOperation
}: {
  analysisId: string;
  analysis: PropertyAnalysis;
  documentType: DocumentType;
  document: PropertyDocument | null;
  olderDocumentsCount: number;
  findings: DocumentFinding[];
  expenseIntelligence?: ExpenseIntelligenceAnalysis | null;
  onRefresh: () => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
  onOperation: (operation: DocumentOperation | null) => void;
}) {
  const risk = documentRiskLevel(findings);
  const suggestedQuestions = uniqueStrings(findings.map((finding) => finding.suggestedQuestion));
  const suggestedNegotiationPoints = uniqueStrings(findings.map((finding) => finding.suggestedNegotiationPoint ?? finding.suggestedChange));

  return (
    <div className="selected-document-workspace">
      <header className="selected-document-header">
        <div>
          <p className="eyebrow">Documento activo</p>
          <h3>{cleanDocumentTypeLabel(documentType)}</h3>
          <div className="selected-document-status">
            <Badge tone={document ? 'success' : 'neutral'}>{document ? 'Cargado' : 'No cargado'}</Badge>
            {document && <Badge tone="neutral">{documentAnalysisStatusLabel(document.analysisStatus)}</Badge>}
            {document && <Badge tone={textExtractionQualityTone(document.textExtractionQuality)}>{textExtractionQualityLabel(document.textExtractionQuality)}</Badge>}
            {document && findings.length > 0 && <Badge tone={findingTone(risk)}>{findingSeverityLabel(risk)}</Badge>}
          </div>
        </div>
        <div className="selected-document-actions">
          <label className="secondary-button file-action-button">
            Subir nuevo archivo
            <AddDocumentInlineInput
              analysisId={analysisId}
              documentType={documentType}
              onRefresh={onRefresh}
              onError={onError}
              setLoading={setLoading}
              onOperation={onOperation}
            />
          </label>
          <button className="ghost-button" type="button" disabled>Eliminar documento</button>
        </div>
      </header>

      {!document ? (
        <div className="document-empty-workspace">
          <strong>No hay {documentTypeEmptyLabel(documentType)} cargada todavía.</strong>
          <p>Subí este documento para revisar el texto extraído y analizar sus señales de riesgo.</p>
          <AddDocumentForm
            analysisId={analysisId}
            initialDocumentType={documentType}
            lockDocumentType
            onRefresh={onRefresh}
            onError={onError}
            setLoading={setLoading}
            onOperation={onOperation}
          />
        </div>
      ) : (
        <>
          {olderDocumentsCount > 0 && (
            <div className="summary-note document-version-note">
              Se muestra el archivo más reciente. Hay {olderDocumentsCount} carga(s) anterior(es) de este tipo.
            </div>
          )}

          <DocumentSpecificOverview
            documentType={documentType}
            document={document}
            findings={findings}
            expenseIntelligence={expenseIntelligence}
            suggestedQuestions={suggestedQuestions}
            suggestedNegotiationPoints={suggestedNegotiationPoints}
          />

          <DocumentCard
            analysisId={analysisId}
            analysis={analysis}
            document={document}
            findings={findings}
            onRefresh={onRefresh}
            onError={onError}
            setLoading={setLoading}
          />
        </>
      )}
    </div>
  );
}

function AddDocumentInlineInput({
  analysisId,
  documentType,
  onRefresh,
  onError,
  setLoading,
  onOperation
}: {
  analysisId: string;
  documentType: DocumentType;
  onRefresh: () => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
  onOperation: (operation: DocumentOperation | null) => void;
}) {
  const [uploading, setUploading] = useState(false);

  async function handleFile(file: File | null) {
    if (!file) {
      return;
    }

    setUploading(true);
    setLoading(true);
    onError(null);
    onOperation({
      documentType,
      stage: 'extracting',
      message: 'Documento cargandose. Extrayendo texto/OCR, esto puede demorar unos segundos...'
    });

    try {
      await addDocument(analysisId, { documentType, file });
      await onRefresh();
      onOperation({
        documentType,
        stage: 'ready',
        message: 'Documento cargado correctamente. Texto extraido o listo para revisar.'
      });
    } catch (err) {
      onOperation({
        documentType,
        stage: 'failed',
        message: err instanceof Error ? err.message : 'No se pudo subir el documento.'
      });
      onError(err instanceof Error ? err.message : 'No se pudo subir el documento.');
    } finally {
      setUploading(false);
      setLoading(false);
    }
  }

  return (
    <input
      type="file"
      accept="application/pdf,image/jpeg,image/png,.pdf,.jpg,.jpeg,.png"
      disabled={uploading}
      aria-busy={uploading ? 'true' : undefined}
      onChange={(event) => void handleFile(event.target.files?.[0] ?? null)}
    />
  );
}

function DocumentSpecificOverview({
  documentType,
  document,
  findings,
  expenseIntelligence,
  suggestedQuestions,
  suggestedNegotiationPoints
}: {
  documentType: DocumentType;
  document: PropertyDocument;
  findings: DocumentFinding[];
  expenseIntelligence?: ExpenseIntelligenceAnalysis | null;
  suggestedQuestions: string[];
  suggestedNegotiationPoints: string[];
}) {
  if (documentType === 'Expenses') {
    return (
      <div className="document-specific-grid">
        <Info label="Salud del edificio" value={expenseIntelligence ? formatNumber(expenseIntelligence.buildingHealthScore) : 'Pendiente'} />
        <Info label="Confianza" value={document.expenseValidation ? `${document.expenseValidation.confidenceScore}%` : textExtractionQualityLabel(document.textExtractionQuality)} />
        <Info label="Expensa detectada" value={formatMoney(document.expenseValidation?.detectedExpenseAmount)} />
        <Info label="Unidad detectada" value={document.expenseValidation ? formatDetectedExpenseUnit(document.expenseValidation) : 'Sin dato'} />
        <Info label="Validación" value={document.expenseValidation?.label ?? 'Pendiente'} />
        <Info label="Hallazgos" value={formatNumber(findings.length)} />
      </div>
    );
  }

  if (documentType === 'ReservationAgreement') {
    return (
      <div className="document-specific-grid">
        <Info label="Riesgo" value={findingSeverityLabel(documentRiskLevel(findings))} />
        <Info label="Hallazgos" value={formatNumber(findings.length)} />
        <Info label="Preguntas sugeridas" value={formatNumber(suggestedQuestions.length)} />
        <Info label="Puntos de negociación" value={formatNumber(suggestedNegotiationPoints.length)} />
      </div>
    );
  }

  return (
    <div className="document-future-card">
      <strong>{cleanDocumentTypeLabel(documentType)}</strong>
      <p>Este tipo de documento queda preparado para el flujo de carga y análisis. Las tarjetas específicas se agregarán cuando haya reglas dedicadas.</p>
    </div>
  );
}

function AddDocumentForm({
  analysisId,
  initialDocumentType,
  lockDocumentType = false,
  onRefresh,
  onError,
  setLoading,
  onOperation
}: {
  analysisId: string;
  initialDocumentType?: DocumentType;
  lockDocumentType?: boolean;
  onRefresh: () => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
  onOperation: (operation: DocumentOperation | null) => void;
}) {
  const [form, setForm] = useState<{ documentType: DocumentType; file: File | null }>({
    documentType: initialDocumentType ?? 'Expenses',
    file: null
  });
  const [fileInputKey, setFileInputKey] = useState(0);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (initialDocumentType) {
      setForm((current) => ({ ...current, documentType: initialDocumentType }));
    }
  }, [initialDocumentType]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setLoading(true);
    onError(null);

    try {
      if (!form.file) {
        throw new Error('Seleccioná un PDF para subir.');
      }

      onOperation({
        documentType: form.documentType,
        stage: 'extracting',
        message: 'Documento cargandose. Extrayendo texto/OCR, esto puede demorar unos segundos...'
      });

      await addDocument(analysisId, {
        documentType: form.documentType,
        file: form.file
      });
      onOperation({
        documentType: form.documentType,
        stage: 'ready',
        message: 'Documento cargado correctamente. Texto extraido o listo para revisar.'
      });
      setForm({ documentType: initialDocumentType ?? 'Expenses', file: null });
      setFileInputKey((key) => key + 1);
      await onRefresh();
    } catch (err) {
      onOperation({
        documentType: form.documentType,
        stage: 'failed',
        message: err instanceof Error ? err.message : 'No se pudo agregar el documento.'
      });
      onError(err instanceof Error ? err.message : 'No se pudo agregar el documento.');
    } finally {
      setSubmitting(false);
      setLoading(false);
    }
  }

  return (
    <form className="stack-form" onSubmit={handleSubmit}>
      <label className="field">
        <span>Tipo</span>
        <select value={form.documentType} disabled={lockDocumentType} onChange={(event) => setForm({ ...form, documentType: event.target.value as DocumentType })}>
          {documentTypes.map((type) => (
            <option key={type} value={type}>
              {cleanDocumentTypeLabel(type)}
            </option>
          ))}
        </select>
      </label>
      <label className="field">
        <span>PDF, JPG o PNG</span>
        <input
          key={fileInputKey}
          type="file"
          accept="application/pdf,image/jpeg,image/png,.pdf,.jpg,.jpeg,.png"
          required
          onChange={(event) => {
            const file = event.target.files?.[0] ?? null;
            setForm({ ...form, file: file as File });
          }}
        />
      </label>
      <button
        className={`primary-button ${submitting ? 'btn-loading' : ''}`}
        type="submit"
        disabled={submitting || !form.file}
        aria-busy={submitting ? 'true' : undefined}
      >
        {submitting && <span className="button-spinner" aria-hidden="true" />}
        {submitting ? 'Cargando...' : 'Subir documento'}
      </button>
    </form>
  );
}

function DocumentsList({
  analysisId,
  documents,
  findings,
  onRefresh,
  onError,
  setLoading
}: {
  analysisId: string;
  documents: PropertyDocument[];
  findings: DocumentFinding[];
  onRefresh: () => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
}) {
  if (documents.length === 0) {
    return (
      <div className="empty-state">
        <strong>Todavía no hay documentos cargados</strong>
        <p>Subí documentos cuando estés listo para validar la propiedad antes de reservar o comprar.</p>
      </div>
    );
  }

  return (
    <div className="document-list">
      {documents.map((document) => (
        <DocumentCard
          key={document.id}
          analysisId={analysisId}
          document={document}
          findings={findings.filter((finding) => finding.propertyDocumentId === document.id)}
          onRefresh={onRefresh}
          onError={onError}
          setLoading={setLoading}
        />
      ))}
    </div>
  );
}

function GroupedFindings({
  riskFindings,
  documentFindings,
  documents
}: {
  riskFindings: PropertyAnalysis['riskFindings'];
  documentFindings: DocumentFinding[];
  documents: PropertyDocument[];
}) {
  const documentTypeById = new Map(documents.map((document) => [document.id, document.documentType]));
  const combined = [
    ...riskFindings.map((finding) => ({
      id: finding.id,
      severity: finding.severity,
      category: finding.type,
      title: finding.title,
      description: finding.description,
      buyerImpact: null,
      suggestedQuestion: null,
      suggestedChange: null,
      suggestedNegotiationPoint: null,
      matchedText: null,
      source: 'Propiedad'
    })),
    ...documentFindings.map((finding) => ({
      id: finding.id,
      severity: finding.severity,
      category: finding.category,
      title: finding.title,
      description: finding.description,
      buyerImpact: finding.buyerImpact,
      suggestedQuestion: finding.suggestedQuestion,
      suggestedChange: finding.suggestedChange,
      suggestedNegotiationPoint: finding.suggestedNegotiationPoint,
      matchedText: finding.matchedText,
      source: cleanDocumentTypeLabel(documentTypeById.get(finding.propertyDocumentId) ?? 'Other')
    }))
  ];

  if (combined.length === 0) {
    return (
      <div className="empty-state">
        <strong>No hay hallazgos detectados todavía</strong>
        <p>El próximo paso es subir documentación y analizar los documentos disponibles.</p>
      </div>
    );
  }

  const severities = ['Critical', 'High', 'Medium', 'Low', 'Info'];

  return (
    <div className="finding-groups">
      {severities.map((severity) => {
        const items = combined.filter((finding) => finding.severity === severity);

        if (items.length === 0) {
          return null;
        }

        return (
          <section className="finding-group" key={severity}>
            <div className="finding-group-heading">
              <Badge tone={findingTone(severity)}>{findingSeverityLabel(severity)}</Badge>
              <span>{items.length} hallazgos</span>
            </div>
            <div className="finding-list">
              {items.map((finding) => (
                <div className={`finding-card finding-${findingTone(finding.severity)}`} key={finding.id}>
                  <div>
                    <strong>{finding.title}</strong>
                    <p><strong>Origen:</strong> {finding.source}</p>
                    <p>{finding.description}</p>
                    {finding.buyerImpact && <p><strong>Impacto para comprador:</strong> {finding.buyerImpact}</p>}
                    {finding.matchedText && <p><strong>Texto detectado:</strong> {finding.matchedText}</p>}
                    {finding.suggestedQuestion && <p><strong>Pregunta sugerida:</strong> {finding.suggestedQuestion}</p>}
                    {(finding.suggestedNegotiationPoint ?? finding.suggestedChange) && (
                      <p><strong>Punto para negociar:</strong> {finding.suggestedNegotiationPoint ?? finding.suggestedChange}</p>
                    )}
                  </div>
                  <Badge tone={findingTone(finding.severity)}>{findingCategoryLabel(finding.category)}</Badge>
                </div>
              ))}
            </div>
          </section>
        );
      })}
    </div>
  );
}

function DocumentCard({
  analysisId,
  analysis,
  document,
  findings,
  onRefresh,
  onError,
  setLoading
}: {
  analysisId: string;
  analysis?: Pick<PropertyAnalysis, 'floor' | 'apartmentUnit' | 'unitIdentifier' | 'monthlyExpenses'>;
  document: PropertyDocument;
  findings: DocumentFinding[];
  onRefresh: () => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
}) {
  const [text, setText] = useState(document.editedText ?? document.extractedText ?? '');
  const [isEditingText, setIsEditingText] = useState(false);
  const [savingText, setSavingText] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [confirmingMatch, setConfirmingMatch] = useState(false);
  const suggestedQuestions = uniqueStrings(findings.map((finding) => finding.suggestedQuestion));
  const suggestedNegotiationPoints = uniqueStrings(findings.map((finding) => finding.suggestedNegotiationPoint ?? finding.suggestedChange));
  const documentRisk = documentRiskLevel(findings);

  useEffect(() => {
    setText(document.editedText ?? document.extractedText ?? '');
  }, [document.editedText, document.extractedText]);

  async function handleSaveText() {
    setSavingText(true);
    setLoading(true);
    onError(null);

    try {
      await saveDocumentText(analysisId, document.id, { editedText: nullableText(text) });
      setIsEditingText(false);
      await onRefresh();
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo guardar el texto.');
    } finally {
      setSavingText(false);
      setLoading(false);
    }
  }

  async function handleAnalyze() {
    setSubmitting(true);
    setLoading(true);
    onError(null);

    try {
      await analyzeDocument(analysisId, document.id, { extractedText: nullableText(text) });
      await onRefresh();
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo analizar el documento.');
    } finally {
      setSubmitting(false);
      setLoading(false);
    }
  }

  async function handleConfirmMatch() {
    setConfirmingMatch(true);
    setLoading(true);
    onError(null);

    try {
      await confirmDocumentMatch(analysisId, document.id);
      await onRefresh();
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo confirmar el documento.');
    } finally {
      setConfirmingMatch(false);
      setLoading(false);
    }
  }

  return (
    <article className="document-card">
      <div className="document-header">
        <div>
          <strong>{documentIntelligenceTitle(document.documentType)}</strong>
          <p>{documentTypeLabels[document.documentType]} - {document.fileName} - {document.sizeBytes} bytes</p>
        </div>
        <div className="document-status">
          <Badge tone={document.hasExtractedText ? 'success' : 'warning'}>
            {document.hasExtractedText ? 'Texto extraído' : 'Sin texto extraído'}
          </Badge>
          <Badge tone={textExtractionQualityTone(document.textExtractionQuality)}>{textExtractionQualityLabel(document.textExtractionQuality)}</Badge>
          <Badge tone={findingTone(documentRisk)}>{findingSeverityLabel(documentRisk)}</Badge>
          <Badge tone="neutral">{documentAnalysisStatusLabel(document.analysisStatus)}</Badge>
        </div>
      </div>
      {document.analysisSummary && <p className="summary-note">{document.analysisSummary}</p>}
      {shouldShowOcrWarning(document) && (
        <div className="ocr-warning">
          El PDF parece escaneado. PropSegura intentó leerlo con OCR. Revisá el texto antes de analizar.
        </div>
      )}
      {document.documentType === 'Expenses' && document.expenseValidation && (
        <div className={`expense-validation-box expense-validation-${document.expenseValidation.classification.toLowerCase()}`}>
          <div>
            <strong>{document.expenseValidation.label}</strong>
            <p>{document.expenseValidation.explanation}</p>
            {document.userConfirmedMatchesProperty && (
              <p className="summary-note">Confirmaste que este documento corresponde a la propiedad.</p>
            )}
          </div>
          <div className="expense-validation-grid">
            <Info label="Confianza" value={`${document.expenseValidation.confidenceScore}%`} />
            <Info label="Unidad cargada" value={analysis ? formatLoadedExpenseUnit(analysis) : 'Sin dato'} />
            <Info label="Unidad detectada" value={formatDetectedExpenseUnit(document.expenseValidation)} />
            <Info label="Expensa cargada" value={formatMoney(analysis?.monthlyExpenses)} />
            <Info label="Expensa detectada" value={formatMoney(document.expenseValidation.detectedExpenseAmount)} />
          </div>
          {document.expenseValidation.classification === 'UnitVerificationPending' && !document.userConfirmedMatchesProperty && (
            <div className="document-match-actions">
              <button
                className={`secondary-button ${confirmingMatch ? 'btn-loading' : ''}`}
                type="button"
                onClick={handleConfirmMatch}
                disabled={confirmingMatch}
                aria-busy={confirmingMatch ? 'true' : undefined}
              >
                {confirmingMatch && <span className="button-spinner" aria-hidden="true" />}
                Confirmar que corresponde a la propiedad
              </button>
              <span>Si no corresponde, reemplazalo con “Subir nuevo archivo”.</span>
            </div>
          )}
        </div>
      )}
      <div className="text-workflow">
        <div className="text-workflow-header">
          <strong>Texto del documento</strong>
          <Badge tone={textExtractionTone(document.textExtractionStatus)}>{textExtractionLabel(document.textExtractionStatus)}</Badge>
        </div>
        {document.editedText || document.extractedText ? (
          <pre className="extracted-preview">{document.editedText ?? document.extractedText}</pre>
        ) : (
          <div className="empty-state compact-empty">
            <strong>No hay texto extraído</strong>
            <p>Si el PDF es escaneado o la extracción salió vacía, pegá el texto manualmente antes de analizar.</p>
          </div>
        )}
        <div className="document-actions">
          <button className="ghost-button" type="button" onClick={() => setIsEditingText((current) => !current)}>
            {isEditingText ? 'Ocultar texto' : 'Revisar texto'}
          </button>
        </div>
        {isEditingText && (
          <div className="text-editor">
            <textarea
              className="analysis-text"
              placeholder="Corregí o pegá el texto del documento antes de analizar"
              value={text}
              onChange={(event) => setText(event.target.value)}
            />
            <button
              className={`primary-button ${savingText ? 'btn-loading' : ''}`}
              type="button"
              onClick={handleSaveText}
              disabled={savingText}
              aria-busy={savingText ? 'true' : undefined}
            >
              {savingText && <span className="button-spinner" aria-hidden="true" />}
              {savingText ? 'Cargando...' : 'Guardar texto'}
            </button>
          </div>
        )}
      </div>
      <button
        className={`secondary-button ${submitting ? 'btn-loading' : ''}`}
        type="button"
        onClick={handleAnalyze}
        disabled={submitting}
        aria-busy={submitting ? 'true' : undefined}
      >
        {submitting && <span className="button-spinner" aria-hidden="true" />}
        {submitting ? 'Analizando...' : document.documentType === 'ReservationAgreement' ? 'Analizar reserva' : 'Analizar documento'}
      </button>
      {findings.length > 0 && (
        <div className="document-intelligence-results">
          <DocumentFindingsBySeverity findings={findings} />
          {suggestedQuestions.length > 0 && (
            <InsightList title="Preguntas sugeridas" items={suggestedQuestions} />
          )}
          {suggestedNegotiationPoints.length > 0 && (
            <InsightList title="Puntos para negociar" items={suggestedNegotiationPoints} />
          )}
        </div>
      )}
      <p className="document-disclaimer">Esto no reemplaza la revisión de un escribano o abogado. PropSegura te ayuda a detectar señales de alerta y preparar mejores preguntas.</p>
    </article>
  );
}

function DocumentFindingsBySeverity({ findings }: { findings: DocumentFinding[] }) {
  const severities = ['Critical', 'High', 'Medium', 'Low', 'Info'];

  return (
    <div className="document-finding-groups">
      {severities.map((severity) => {
        const items = findings.filter((finding) => finding.severity === severity);

        if (items.length === 0) {
          return null;
        }

        return (
          <section className="document-finding-group" key={severity}>
            <div className="finding-group-heading">
              <Badge tone={findingTone(severity)}>{findingSeverityLabel(severity)}</Badge>
              <span>{items.length} señal(es)</span>
            </div>
            <div className="finding-list">
              {items.map((finding) => (
                <div className={`finding-card finding-${findingTone(finding.severity)}`} key={finding.id}>
                  <div>
                    <strong>{finding.title}</strong>
                    <p>{finding.description}</p>
                    {finding.buyerImpact && <p><strong>Impacto para comprador:</strong> {finding.buyerImpact}</p>}
                    {finding.matchedText && <p><strong>Texto detectado:</strong> {finding.matchedText}</p>}
                    {finding.suggestedQuestion && <p><strong>Pregunta sugerida:</strong> {finding.suggestedQuestion}</p>}
                    {(finding.suggestedNegotiationPoint ?? finding.suggestedChange) && (
                      <p><strong>Punto para negociar:</strong> {finding.suggestedNegotiationPoint ?? finding.suggestedChange}</p>
                    )}
                  </div>
                  <Badge tone={findingTone(finding.severity)}>{findingCategoryLabel(finding.category)}</Badge>
                </div>
              ))}
            </div>
          </section>
        );
      })}
    </div>
  );
}

function InsightList({ title, items }: { title: string; items: string[] }) {
  return (
    <div className="insight-list">
      <strong>{title}</strong>
      <ul>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}

function FindingsList({
  riskFindings,
  documentFindings
}: {
  riskFindings: PropertyAnalysis['riskFindings'];
  documentFindings: DocumentFinding[];
}) {
  const hasFindings = riskFindings.length > 0 || documentFindings.length > 0;

  if (!hasFindings) {
    return (
      <div className="empty-state">
        <strong>No hay señales detectadas todavía</strong>
        <p>Cuando cargues y analices documentos, las alertas importantes van a aparecer acá.</p>
      </div>
    );
  }

  return (
    <div className="finding-list">
      {riskFindings.map((finding) => (
        <div className={`finding-card finding-${findingTone(finding.severity)}`} key={finding.id}>
          <div>
            <strong>{finding.title}</strong>
            <p>{finding.description}</p>
          </div>
          <Badge tone={findingTone(finding.severity)}>{findingSeverityLabel(finding.severity)}</Badge>
        </div>
      ))}
      {documentFindings.map((finding) => (
        <div className={`finding-card finding-${findingTone(finding.severity)}`} key={finding.id}>
          <div>
            <strong>{finding.title}</strong>
            <p>{finding.description}</p>
            {finding.buyerImpact && <p><strong>Impacto para comprador:</strong> {finding.buyerImpact}</p>}
            {finding.suggestedQuestion && <p><strong>Pregunta sugerida:</strong> {finding.suggestedQuestion}</p>}
            {(finding.suggestedNegotiationPoint ?? finding.suggestedChange) && (
              <p><strong>Punto para negociar:</strong> {finding.suggestedNegotiationPoint ?? finding.suggestedChange}</p>
            )}
          </div>
          <Badge tone={findingTone(finding.severity)}>{findingSeverityLabel(finding.severity)} - {findingCategoryLabel(finding.category)}</Badge>
        </div>
      ))}
    </div>
  );
}

function getChecklistStats(items: ChecklistItem[]): ChecklistStats {
  const required = items.filter((item) => item.isRequired).length;
  const provided = items.filter((item) => item.isRequired && item.isProvided).length;

  return {
    required,
    provided,
    percent: required === 0 ? 100 : Math.round((provided / required) * 100)
  };
}

function opportunitySummary(analysis: PropertyAnalysis) {
  if (analysis.recommendedUse === 'NotRecommended') {
    return 'La oportunidad necesita más cuidado antes de avanzar. Revisá precio, expensas, estado y datos faltantes.';
  }

  if (analysis.opportunityScore >= 75) {
    return 'Los datos iniciales muestran una oportunidad sólida para seguir evaluando con documentación.';
  }

  if (analysis.opportunityScore >= 45) {
    return 'La propiedad puede tener potencial, pero conviene completar datos y revisar los puntos sensibles.';
  }

  return 'Faltan datos o aparecen señales iniciales que sugieren avanzar con cautela.';
}

function checklistTone(status: string) {
  if (status === 'Provided') {
    return 'success';
  }
  if (status === 'NotApplicable') {
    return 'neutral';
  }
  return 'warning';
}

function checklistStatusLabel(status: string) {
  const labels: Record<string, string> = {
    Missing: 'Pendiente',
    Provided: 'Cargado',
    NotApplicable: 'No aplica'
  };

  return labels[status] ?? status;
}

function findingTone(severity: string) {
  const tones: Record<string, string> = {
    Critical: 'critical',
    High: 'danger',
    Medium: 'warning',
    Low: 'info',
    Info: 'neutral'
  };

  return tones[severity] ?? 'neutral';
}

function findingSeverityLabel(severity: string) {
  const labels: Record<string, string> = {
    Critical: 'Crítico',
    High: 'Alto',
    Medium: 'Medio',
    Low: 'Bajo',
    Info: 'Info'
  };

  return labels[severity] ?? severity;
}

function findingCategoryLabel(category: string) {
  const labels: Record<string, string> = {
    Legal: 'Legal',
    Financial: 'Financiero',
    Documentation: 'Documentación',
    Negotiation: 'Negociación'
  };

  return labels[category] ?? category;
}

function buildingHealthTone(score: number) {
  if (score >= 90) {
    return 'success';
  }
  if (score >= 70) {
    return 'info';
  }
  if (score >= 50) {
    return 'warning';
  }
  return 'danger';
}

function textExtractionTone(status: string) {
  if (status === 'Extracted' || status === 'ManuallyEdited') {
    return 'success';
  }
  if (status === 'Empty' || status === 'Failed') {
    return 'warning';
  }
  return 'neutral';
}

function textExtractionLabel(status: string) {
  const labels: Record<string, string> = {
    NotStarted: 'Sin extraer',
    Extracted: 'Extraído',
    Empty: 'Vacío',
    Failed: 'Falló',
    ManuallyEdited: 'Editado'
  };

  return labels[status] ?? status;
}

function textExtractionQualityTone(quality: string) {
  if (quality === 'Good') {
    return 'success';
  }
  if (quality === 'Partial' || quality === 'Poor') {
    return 'warning';
  }
  if (quality === 'Empty') {
    return 'danger';
  }
  return 'neutral';
}

function textExtractionQualityLabel(quality: string) {
  const labels: Record<string, string> = {
    Unknown: 'Sin dato',
    Good: 'Buena',
    Partial: 'Parcial',
    Poor: 'Pobre',
    Empty: 'Vacía'
  };

  return labels[quality] ?? quality;
}

function shouldShowOcrWarning(document: PropertyDocument) {
  return document.analysisSummary?.toLowerCase().includes('ocr') === true ||
    document.textExtractionQuality === 'Poor' ||
    document.textExtractionQuality === 'Empty';
}

function documentAnalysisStatusLabel(status: string) {
  const labels: Record<string, string> = {
    NotStarted: 'Sin analizar',
    Pending: 'Pendiente',
    Analyzed: 'Analizado',
    Completed: 'Analizado',
    Failed: 'Falló'
  };

  return labels[status] ?? status;
}

function getDocumentUiState(document: PropertyDocument | null) {
  if (!document) {
    return {
      state: 'pending',
      label: 'Pendiente',
      icon: '...'
    };
  }

  if (document.textExtractionStatus === 'Failed' || document.analysisStatus === 'Failed') {
    return {
      state: 'failed',
      label: 'Fallo',
      icon: '!'
    };
  }

  if (document.analysisStatus === 'Completed' || document.analysisStatus === 'Analyzed') {
    return {
      state: 'analyzed',
      label: 'Analizado',
      icon: 'OK'
    };
  }

  if (document.hasExtractedText || document.textExtractionStatus === 'Completed') {
    return {
      state: 'ready',
      label: 'Listo para analizar',
      icon: 'OK'
    };
  }

  return {
    state: 'uploaded',
    label: 'Archivo cargado',
    icon: 'OK'
  };
}

function documentIntelligenceTitle(documentType: DocumentType) {
  if (documentType === 'ReservationAgreement') {
    return 'Análisis de reserva';
  }

  if (documentType === 'Expenses') {
    return 'Análisis de expensas';
  }

  return 'Análisis documental';
}

function documentRiskLevel(findings: DocumentFinding[]) {
  if (findings.some((finding) => finding.severity === 'Critical')) {
    return 'Critical';
  }
  if (findings.some((finding) => finding.severity === 'High')) {
    return 'High';
  }
  if (findings.some((finding) => finding.severity === 'Medium')) {
    return 'Medium';
  }
  if (findings.some((finding) => finding.severity === 'Low')) {
    return 'Low';
  }

  return 'Info';
}

function cleanDocumentTypeLabel(documentType: DocumentType) {
  const labels: Record<DocumentType, string> = {
    Expenses: 'Últimas expensas',
    AssemblyMinutes: 'Actas de asamblea',
    CoOwnershipRegulation: 'Reglamento de copropiedad',
    DomainReport: 'Informe de dominio e inhibiciones',
    FloorPlan: 'Plano del departamento',
    ReservationAgreement: 'Reserva',
    Other: 'Escritura'
  };

  return labels[documentType];
}

function isFutureDocumentType(documentType: DocumentType) {
  return futureDocumentTypes.includes(documentType);
}

function futureDocumentContent(documentType: DocumentType) {
  const content: Record<DocumentType, { icon: string; title: string; description: string; items: string[] }> = {
    Expenses: {
      icon: '▤',
      title: 'Expensas',
      description: 'Disponible en PropSegura V1.',
      items: []
    },
    ReservationAgreement: {
      icon: '▧',
      title: 'Reserva',
      description: 'Disponible en PropSegura V1.',
      items: []
    },
    AssemblyMinutes: {
      icon: '▦',
      title: 'Actas de consorcio',
      description: 'Las actas permitirán detectar:',
      items: ['conflictos recurrentes', 'problemas edilicios', 'discusiones de consorcio', 'obras pendientes']
    },
    FloorPlan: {
      icon: '⌗',
      title: 'Plano del departamento',
      description: 'Los planos permitirán validar:',
      items: ['distribución real', 'superficies', 'inconsistencias', 'aprovechamiento del espacio']
    },
    DomainReport: {
      icon: '⚖',
      title: 'Informe de dominio e inhibiciones',
      description: 'Permitirá validar:',
      items: ['titularidad', 'inhibiciones', 'embargos', 'restricciones registrales']
    },
    Other: {
      icon: '▩',
      title: 'Escritura',
      description: 'Permitirá revisar:',
      items: ['antecedentes', 'observaciones', 'consistencia documental']
    },
    CoOwnershipRegulation: {
      icon: '▥',
      title: 'Reglamento de copropiedad',
      description: 'Permitirá revisar reglas del edificio y restricciones de uso.',
      items: ['uso permitido', 'restricciones', 'responsabilidades', 'reglas del consorcio']
    }
  };

  return content[documentType];
}

function documentTypeEmptyLabel(documentType: DocumentType) {
  const labels: Record<DocumentType, string> = {
    Expenses: 'liquidación de expensas',
    AssemblyMinutes: 'acta',
    CoOwnershipRegulation: 'reglamento',
    DomainReport: 'informe de dominio',
    FloorPlan: 'plano',
    ReservationAgreement: 'reserva',
    Other: 'documentación'
  };

  return labels[documentType];
}

function getDocumentsByType(documents: PropertyDocument[], documentType: DocumentType) {
  return documents
    .filter((document) => document.documentType === documentType)
    .sort((left, right) => Date.parse(right.uploadedAtUtc) - Date.parse(left.uploadedAtUtc));
}

function getFindingsForDocumentType(findings: DocumentFinding[], documents: PropertyDocument[], documentType: DocumentType) {
  const documentIds = new Set(documents.filter((document) => document.documentType === documentType).map((document) => document.id));
  return findings.filter((finding) => documentIds.has(finding.propertyDocumentId));
}

function getDocumentWorkspaceStats(analysis: PropertyAnalysis) {
  const uploadedTypes = new Set(analysis.documents.map((document) => document.documentType));
  const analyzedTypes = new Set(
    analysis.documents
      .filter((document) => document.analysisStatus === 'Completed')
      .map((document) => document.documentType)
  );
  const criticalFindings = analysis.documentFindings.filter((finding) => finding.severity === 'Critical').length;

  return {
    uploaded: uploadedTypes.size,
    analyzed: analyzedTypes.size,
    pending: Math.max(0, documentTypes.length - uploadedTypes.size),
    criticalFindings
  };
}

function uniqueStrings(values: Array<string | null | undefined>) {
  return Array.from(new Set(values.filter((value): value is string => Boolean(value?.trim()))));
}

function riskTone(level: string) {
  return level === 'Low' ? 'success' : level === 'Medium' ? 'warning' : 'danger';
}

function protectionTone(score: number) {
  if (score >= 80) {
    return 'success';
  }
  if (score >= 55) {
    return 'warning';
  }
  return 'danger';
}

function recommendedTone(use: string) {
  if (use === 'Both' || use === 'Living' || use === 'Investment') {
    return 'success';
  }
  if (use === 'NotRecommended') {
    return 'danger';
  }
  return 'neutral';
}

function marketPositionTone(position: string) {
  if (position === 'Cheap') {
    return 'success';
  }
  if (position === 'Fair') {
    return 'info';
  }
  if (position === 'Expensive') {
    return 'warning';
  }
  if (position === 'Overpriced') {
    return 'danger';
  }
  return 'neutral';
}

function marketPositionLabel(position: string) {
  const labels: Record<string, string> = {
    Cheap: 'Barato',
    Fair: 'En precio',
    Expensive: 'Caro',
    Overpriced: 'Sobreprecio',
    Unknown: 'Sin datos'
  };

  return labels[position] ?? position;
}

function reportRecommendationTone(value: string) {
  const tones: Record<string, string> = {
    Advance: 'success',
    AdvanceWithCaution: 'info',
    WaitForDocuments: 'warning',
    Renegotiate: 'warning',
    ProfessionalReview: 'danger',
    InsufficientData: 'neutral'
  };

  return tones[value] ?? 'neutral';
}

function reportStatusTone(value: string) {
  const tones: Record<string, string> = {
    Ready: 'success',
    Incomplete: 'warning',
    NeedsReview: 'danger'
  };

  return tones[value] ?? 'neutral';
}

function buildReportPdfFileName(report: PropertyReport) {
  const safeTitle = report.propertySummary.title
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-zA-Z0-9-_ ]/g, '')
    .trim()
    .replace(/\s+/g, '-');
  const date = new Date(report.generatedAtUtc);
  const yyyy = date.getFullYear();
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const dd = String(date.getDate()).padStart(2, '0');

  return `PropSegura-${safeTitle || 'Propiedad'}-${yyyy}${mm}${dd}.pdf`;
}

function formatReportUnit(summary: PropertyReport['propertySummary']) {
  const parts = [];

  if (summary.floor != null) {
    parts.push(`Piso ${summary.floor}`);
  }

  if (summary.apartmentUnit) {
    parts.push(`Depto ${summary.apartmentUnit}`);
  }

  if (summary.unitIdentifier) {
    parts.push(`UF ${summary.unitIdentifier}`);
  }

  return parts.length === 0 ? 'Sin dato' : parts.join(' · ');
}

function TextField({
  label,
  value,
  onChange,
  type = 'text',
  required = false,
  placeholder
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  required?: boolean;
  placeholder?: string;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <input type={type} value={value} required={required} placeholder={placeholder} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function SelectField({
  label,
  value,
  onChange
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">Sin dato</option>
        <option value="true">Sí</option>
        <option value="false">No</option>
      </select>
    </label>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="info">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function nullableText(value: string) {
  return value.trim().length === 0 ? null : value.trim();
}

function nullableNumber(value: string) {
  return value.trim().length === 0 ? null : Number(value);
}

function nullableBoolean(value: string) {
  if (value === 'true') {
    return true;
  }
  if (value === 'false') {
    return false;
  }
  return null;
}

function formatImportedValue(value?: number | null) {
  return value == null ? null : String(value);
}

function formatDetectedExpenseUnit(validation: ExpenseDocumentValidation) {
  const parts = [];

  if (validation.detectedFloor != null) {
    parts.push(`Piso ${validation.detectedFloor}`);
  }

  if (validation.detectedApartmentUnit) {
    parts.push(`Depto ${validation.detectedApartmentUnit}`);
  }

  if (validation.detectedUnitIdentifier) {
    parts.push(`UF ${validation.detectedUnitIdentifier}`);
  }

  return parts.length === 0 ? 'Sin dato' : parts.join(' · ');
}

function formatLoadedExpenseUnit(analysis: Pick<PropertyAnalysis, 'floor' | 'apartmentUnit' | 'unitIdentifier'>) {
  const parts = [];

  if (analysis.floor != null) {
    parts.push(`Piso ${analysis.floor}`);
  }

  if (analysis.apartmentUnit) {
    parts.push(`Depto ${analysis.apartmentUnit}`);
  }

  if (analysis.unitIdentifier) {
    parts.push(`UF ${analysis.unitIdentifier}`);
  }

  return parts.length === 0 ? 'Sin dato' : parts.join(' - ');
}

function formStateFromAnalysis(analysis: PropertyAnalysis): CreateFormState {
  return {
    title: analysis.title,
    listingUrl: analysis.listingUrl ?? '',
    address: analysis.address,
    neighborhood: analysis.neighborhood,
    priceUsd: formatImportedValue(analysis.priceUsd) ?? '',
    monthlyExpenses: formatImportedValue(analysis.monthlyExpenses) ?? '',
    totalSquareMeters: formatImportedValue(analysis.totalSquareMeters) ?? '',
    coveredSquareMeters: formatImportedValue(analysis.coveredSquareMeters) ?? '',
    rooms: formatImportedValue(analysis.rooms) ?? '',
    bedrooms: formatImportedValue(analysis.bedrooms) ?? '',
    bathrooms: formatImportedValue(analysis.bathrooms) ?? '',
    buildingAgeYears: formatImportedValue(analysis.buildingAgeYears) ?? '',
    floor: formatImportedValue(analysis.floor) ?? '',
    apartmentUnit: analysis.apartmentUnit ?? '',
    unitIdentifier: analysis.unitIdentifier ?? '',
    hasBalcony: booleanToFormValue(analysis.hasBalcony),
    hasAmenities: booleanToFormValue(analysis.hasAmenities),
    isCreditEligible: booleanToFormValue(analysis.isCreditEligible),
    propertyCondition: analysis.propertyCondition,
    notes: analysis.notes ?? ''
  };
}

function buildUpdateRequestFromAnalysis(analysis: PropertyAnalysis): UpdateAnalysisRequest {
  return {
    title: analysis.title,
    listingUrl: analysis.listingUrl ?? null,
    address: analysis.address,
    neighborhood: analysis.neighborhood,
    priceUsd: analysis.priceUsd ?? null,
    monthlyExpenses: analysis.monthlyExpenses ?? null,
    totalSquareMeters: analysis.totalSquareMeters ?? null,
    coveredSquareMeters: analysis.coveredSquareMeters ?? null,
    rooms: analysis.rooms ?? null,
    bedrooms: analysis.bedrooms ?? null,
    bathrooms: analysis.bathrooms ?? null,
    buildingAgeYears: analysis.buildingAgeYears ?? null,
    floor: analysis.floor ?? null,
    apartmentUnit: analysis.apartmentUnit ?? null,
    unitIdentifier: analysis.unitIdentifier ?? null,
    hasBalcony: analysis.hasBalcony ?? null,
    hasAmenities: analysis.hasAmenities ?? null,
    isCreditEligible: analysis.isCreditEligible ?? null,
    propertyCondition: analysis.propertyCondition,
    notes: analysis.notes ?? null,
    visitObservations: analysis.visitObservations ?? [],
    visitNotes: analysis.visitNotes ?? null
  };
}

function booleanToFormValue(value?: boolean | null) {
  if (value == null) {
    return '';
  }

  return value ? 'true' : 'false';
}

function valueOrMax(value?: number | null) {
  return value == null ? Number.MAX_SAFE_INTEGER : value;
}

function matchesDashboardSearch(analysis: PropertyAnalysisSummary, searchTerm: string) {
  const normalizedSearch = searchTerm.trim().toLocaleLowerCase('es-AR');

  if (!normalizedSearch) {
    return true;
  }

  return [
    analysis.title,
    analysis.address,
    analysis.neighborhood,
    recommendedUseLabel(analysis.recommendedUse),
    riskLevelLabel(analysis.initialRiskLevel)
  ]
    .filter(Boolean)
    .some((value) => value.toLocaleLowerCase('es-AR').includes(normalizedSearch));
}

function getDashboardStats(analyses: PropertyAnalysisSummary[]) {
  const total = analyses.length;
  const recommended = analyses.filter((analysis) => ['Living', 'Investment', 'Both'].includes(analysis.recommendedUse) && analysis.opportunityScore >= 70).length;
  const highRisk = analyses.filter(isHighRiskDashboardAnalysis).length;
  const needsAttention = analyses.filter(needsDashboardAttention).length;
  const averageOpportunity = total === 0
    ? 0
    : Math.round(analyses.reduce((sum, analysis) => sum + analysis.opportunityScore, 0) / total);

  return {
    averageOpportunity,
    highRisk,
    needsAttention,
    recommended,
    total
  };
}

function getFeaturedAnalyses(analyses: PropertyAnalysisSummary[]) {
  const byOpportunity = [...analyses].sort((left, right) => right.opportunityScore - left.opportunityScore);
  const bestOpportunity = byOpportunity[0];
  const investmentCandidates = analyses.filter((analysis) => analysis.recommendedUse === 'Investment' || analysis.recommendedUse === 'Both');
  const livingCandidates = analyses.filter((analysis) => analysis.recommendedUse === 'Living' || analysis.recommendedUse === 'Both');
  const attentionCandidates = analyses.filter(needsDashboardAttention);
  const bestInvestment = pickFeaturedCandidate(investmentCandidates, [bestOpportunity?.id]) ?? pickFeaturedCandidate(analyses, [bestOpportunity?.id]);
  const bestLiving = pickFeaturedCandidate(livingCandidates, [bestOpportunity?.id, bestInvestment?.id]) ?? pickFeaturedCandidate(analyses, [bestOpportunity?.id, bestInvestment?.id]);

  return {
    bestInvestment,
    bestLiving,
    bestOpportunity,
    needsAttention: [...(attentionCandidates.length > 0 ? attentionCandidates : analyses)].sort((left, right) =>
      right.criticalFindingsCount - left.criticalFindingsCount ||
      left.protectionScore - right.protectionScore ||
      right.missingRequiredDocumentsCount - left.missingRequiredDocumentsCount
    )[0]
  };
}

function pickFeaturedCandidate(candidates: PropertyAnalysisSummary[], excludedIds: Array<string | undefined>) {
  const excluded = new Set(excludedIds.filter(Boolean));
  const available = candidates.filter((analysis) => !excluded.has(analysis.id));

  return [...(available.length > 0 ? available : candidates)].sort((left, right) =>
    right.opportunityScore - left.opportunityScore ||
    right.protectionScore - left.protectionScore ||
    new Date(right.updatedAtUtc).getTime() - new Date(left.updatedAtUtc).getTime()
  )[0];
}

function isHighRiskDashboardAnalysis(analysis: PropertyAnalysisSummary) {
  return analysis.initialRiskLevel === 'High' ||
    analysis.criticalFindingsCount > 0 ||
    analysis.opportunityScore < 50;
}

function needsDashboardAttention(analysis: PropertyAnalysisSummary) {
  return analysis.missingRequiredDocumentsCount > 0 ||
    analysis.criticalFindingsCount > 0 ||
    analysis.findingsCount > 0 ||
    analysis.initialRiskLevel === 'High';
}

function hasNoDetectedDocumentRisk(analysis: PropertyAnalysisSummary) {
  return analysis.protectionScore === 0 &&
    analysis.findingsCount === 0 &&
    analysis.criticalFindingsCount === 0;
}

function scoreClassName(type: 'opportunity' | 'protection', score: number) {
  if (type === 'protection') {
    if (score >= 80) {
      return 'score-strong';
    }
    if (score >= 50) {
      return 'score-medium';
    }
    return 'score-low';
  }

  if (score >= 90) {
    return 'score-excellent';
  }
  if (score >= 70) {
    return 'score-strong';
  }
  if (score >= 50) {
    return 'score-medium';
  }
  return 'score-low';
}

function scoreBadgeLabel(type: 'opportunity' | 'protection', score: number) {
  if (type === 'protection') {
    if (score >= 80) {
      return 'Alta';
    }
    if (score >= 50) {
      return 'Media';
    }
    return 'Baja';
  }

  if (score >= 90) {
    return 'Excelente';
  }
  if (score >= 70) {
    return 'Muy buena';
  }
  if (score >= 50) {
    return 'Buena';
  }
  return 'Baja';
}

function getDashboardOpinion(analysis: PropertyAnalysisSummary) {
  if (analysis.recommendedUse === 'NotRecommended' || analysis.opportunityScore < 45 || analysis.criticalFindingsCount > 0) {
    return {
      icon: '●',
      label: 'Revisar con profesional',
      tone: 'danger'
    };
  }

  if (analysis.protectionScore < 55 || analysis.missingRequiredDocumentsCount > 0 || analysis.findingsCount > 0) {
    return {
      icon: '●',
      label: 'Resolver pendientes',
      tone: 'warning'
    };
  }

  return {
    icon: '●',
    label: 'Avanzar',
    tone: 'success'
  };
}

function compareText(left?: string | null, right?: string | null) {
  return (left || '').localeCompare(right || '', 'es-AR', { sensitivity: 'base' });
}

function defaultSortDirection(sortBy: DashboardSort): SortDirection {
  if (sortBy === 'title' || sortBy === 'neighborhood' || sortBy === 'lowestPrice' || sortBy === 'lowestUsdPerSquareMeter' || sortBy === 'recommendedUse') {
    return 'asc';
  }

  return 'desc';
}

function investmentRank(analysis: PropertyAnalysisSummary) {
  if (analysis.recommendedUse === 'Investment') {
    return 3;
  }
  if (analysis.recommendedUse === 'Both') {
    return 2;
  }
  return 0;
}

function livingRank(analysis: PropertyAnalysisSummary) {
  if (analysis.recommendedUse === 'Living') {
    return 3;
  }
  if (analysis.recommendedUse === 'Both') {
    return 2;
  }
  return 0;
}

function isProtectedPath(path: string) {
  return path.startsWith('/properties') || path.startsWith('/admin');
}

function betaStatusLabel(status: BetaAccessRequest['status']) {
  const labels: Record<BetaAccessRequest['status'], string> = {
    Pending: 'Pendiente',
    Approved: 'Aprobada',
    Rejected: 'Rechazada'
  };

  return labels[status];
}

function userStatusLabel(status: AuthUser['status']) {
  const labels: Record<AuthUser['status'], string> = {
    Invited: 'Invitado',
    Active: 'Activo',
    Disabled: 'Deshabilitado'
  };

  return labels[status];
}

function getPropertyIdFromPath(path: string) {
  const match = path.match(/^\/properties\/([^/]+)$/);
  if (!match || match[1] === 'new') {
    return null;
  }

  return match[1];
}

function isValidCoverImageFile(file: File) {
  return ['image/jpeg', 'image/png', 'image/webp'].includes(file.type) && file.size <= 5 * 1024 * 1024;
}

function getAnalysisProgressPercent(analysis: PropertyAnalysis, checklistStats: ChecklistStats) {
  let completed = 2;

  if (checklistStats.percent >= 100) {
    completed += 1;
  }

  if (analysis.documentFindings.length > 0 || analysis.riskFindings.length > 0) {
    completed += 1;
  }

  return Math.round((completed / 5) * 100);
}

function getAnalysisOpinion(analysis: PropertyAnalysis, checklistStats: ChecklistStats) {
  if (analysis.recommendedUse === 'NotRecommended' || analysis.opportunityScore < 45 || analysis.riskFindings.some((finding) => finding.severity === 'Critical')) {
    return {
      label: 'Revisar con profesional',
      tone: 'danger'
    };
  }

  if (analysis.protectionScore < 55 || checklistStats.percent < 100 || analysis.documentFindings.length > 0) {
    return {
      label: 'Resolver pendientes',
      tone: 'warning'
    };
  }

  return {
    label: 'Avanzar',
    tone: 'success'
  };
}

function getWorkspaceAnalysisStatus(analysis: PropertyAnalysis, checklistStats: ChecklistStats, report: PropertyReport | null) {
  const findingsDone = analysis.documentFindings.length > 0 ||
    analysis.riskFindings.length > 0 ||
    analysis.documents.some((document) => document.analysisStatus === 'Analyzed');
  const completed = checklistStats.percent >= 100 && findingsDone && Boolean(report);

  if (completed) {
    return {
      badge: 'Análisis completado',
      findingsDone: true,
      findingsText: 'Completado',
      tone: 'success' as const
    };
  }

  if (findingsDone) {
    return {
      badge: 'Analizando',
      findingsDone: true,
      findingsText: 'En progreso',
      tone: 'warning' as const
    };
  }

  return {
    badge: 'Pendiente',
    findingsDone: false,
    findingsText: 'Pendiente',
    tone: 'neutral' as const
  };
}

function getProtectionKpi(analysis: PropertyAnalysis): { value: string | number; label: string; text: string; tone: 'success' | 'warning' | 'info' | 'neutral' } {
  const hasFindings = analysis.documentFindings.length > 0 || analysis.riskFindings.length > 0;
  const hasAnalyzedDocuments = analysis.documents.some((document) => document.analysisStatus === 'Analyzed');

  if (!hasFindings && analysis.protectionScore === 0) {
    return {
      value: '◇',
      label: 'Sin riesgos detectados',
      text: hasAnalyzedDocuments
        ? 'Los documentos analizados no muestran señales críticas.'
        : 'Cargá expensas o reserva para validar la protección documental.',
      tone: hasAnalyzedDocuments ? 'success' : 'neutral'
    };
  }

  return {
    value: analysis.protectionScore,
    label: scoreBadgeLabel('protection', analysis.protectionScore),
    text: hasFindings ? 'Riesgos documentales detectados' : 'Protección documental en evaluación',
    tone: analysis.protectionScore >= 80 ? 'success' : analysis.protectionScore >= 50 ? 'info' : 'warning'
  };
}

function getBuildingHealthKpi(expenseIntelligence?: ExpenseIntelligenceAnalysis | null): { value: string | number; label: string; text: string; tone: 'success' | 'warning' | 'info' | 'neutral' } {
  if (!expenseIntelligence) {
    return {
      value: 'Pendiente',
      label: 'Sin expensas',
      text: 'Subí expensas para evaluar señales del edificio.',
      tone: 'neutral'
    };
  }

  const score = expenseIntelligence.buildingHealthScore;
  return {
    value: score,
    label: expenseIntelligence.classification,
    text: expenseIntelligence.summary,
    tone: score >= 75 ? 'success' : score >= 55 ? 'info' : 'warning'
  };
}

function getReportConsistencyNote(report: PropertyReport) {
  const opportunityScore = report.opportunitySection.opportunityScore;
  const recommendation = report.overallRecommendation;
  const hasCriticalFindings = report.protectionSection.highOrCriticalFindingsCount > 0 ||
    report.findingsSection.groups.some((group) => group.severity === 'Critical' || group.severity === 'High');
  const missingDocuments = report.documentsSection.missingDocumentsCount;

  if (opportunityScore >= 70 && (recommendation === 'ProfessionalReview' || recommendation === 'WaitForDocuments' || hasCriticalFindings || missingDocuments > 0)) {
    if (hasCriticalFindings) {
      return 'La oportunidad de mercado es buena, pero los riesgos documentales detectados impiden recomendar avanzar sin revisión profesional.';
    }

    return 'La oportunidad inicial puede ser buena, pero todavía falta documentación clave para confirmar si conviene avanzar.';
  }

  return null;
}

function formatMoney(value?: number | null) {
  return value == null ? 'Sin dato' : value.toLocaleString('es-AR');
}

function getExpensesPerSquareMeterWarning(monthlyExpensesValue?: string | number | null, totalSquareMetersValue?: string | number | null) {
  const monthlyExpenses = typeof monthlyExpensesValue === 'number'
    ? monthlyExpensesValue
    : nullableNumber(monthlyExpensesValue ?? '');
  const totalSquareMeters = typeof totalSquareMetersValue === 'number'
    ? totalSquareMetersValue
    : nullableNumber(totalSquareMetersValue ?? '');

  if (!monthlyExpenses || !totalSquareMeters || totalSquareMeters <= 0) {
    return null;
  }

  const expensesPerSquareMeter = monthlyExpenses / totalSquareMeters;

  if (expensesPerSquareMeter >= 15000) {
    return `Expensas muy altas para la superficie: ${expensesPerSquareMeter.toLocaleString('es-AR', { maximumFractionDigits: 0 })} ARS/m2. Revisar extraordinarias, deuda o gastos del consorcio.`;
  }

  if (expensesPerSquareMeter >= 8000) {
    return `Expensas elevadas para la superficie: ${expensesPerSquareMeter.toLocaleString('es-AR', { maximumFractionDigits: 0 })} ARS/m2. Conviene validar el detalle de la liquidacion.`;
  }

  return null;
}

function formatRange(min?: number | null, max?: number | null) {
  if (min == null || max == null) {
    return 'Sin dato';
  }

  return `${formatMoney(min)} - ${formatMoney(max)}`;
}

function formatPercent(value?: number | null) {
  return value == null ? 'Sin dato' : `${value.toLocaleString('es-AR')}%`;
}

function formatNumber(value?: number | null) {
  return value == null ? 'Sin dato' : value.toLocaleString('es-AR');
}

function formatBoolean(value?: boolean | null) {
  if (value == null) {
    return 'Sin dato';
  }
  return value ? 'Sí' : 'No';
}

function formatDate(value?: string | null) {
  if (!value) {
    return 'Sin dato';
  }

  return new Date(value).toLocaleDateString('es-AR');
}

function riskLevelLabel(level: string) {
  return level === 'Low' ? 'bajo' : level === 'Medium' ? 'medio' : 'alto';
}

function recommendedUseLabel(use: string) {
  const labels: Record<string, string> = {
    Living: 'Vivienda',
    Investment: 'Inversión',
    Both: 'Vivienda e inversión',
    NotRecommended: 'Requiere mas analisis',
    Unknown: 'Sin recomendación'
  };

  return labels[use] ?? use;
}

function protectionLabel(score: number) {
  if (score >= 80) {
    return 'Protección alta';
  }
  if (score >= 55) {
    return 'Protección media';
  }
  return 'Protección baja';
}

