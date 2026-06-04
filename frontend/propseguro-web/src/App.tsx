import { CSSProperties, FormEvent, ReactNode, useEffect, useState } from 'react';
import {
  ChecklistItem,
  DocumentFinding,
  DocumentType,
  PropertyAnalysis,
  PropertyAnalysisSummary,
  PropertyCondition,
  PropertyDocument,
  addDocument,
  analyzeDocument,
  createAnalysis,
  deleteAnalysis,
  getAnalysis,
  importListing,
  listAnalyses,
  saveDocumentText,
  updateAnalysis
} from './api';

const documentTypes: DocumentType[] = [
  'Expenses',
  'AssemblyMinutes',
  'CoOwnershipRegulation',
  'DomainReport',
  'FloorPlan',
  'ReservationAgreement',
  'Other'
];

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
  hasBalcony: '',
  hasAmenities: '',
  isCreditEligible: '',
  propertyCondition: 'Unknown',
  notes: ''
};

export function App() {
  const [path, setPath] = useState(() => window.location.pathname);
  const [analysis, setAnalysis] = useState<PropertyAnalysis | null>(null);
  const [analyses, setAnalyses] = useState<PropertyAnalysisSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    function handlePopState() {
      setPath(window.location.pathname);
    }

    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  useEffect(() => {
    if (path === '/properties') {
      void refreshDashboard();
      return;
    }

    const id = getPropertyIdFromPath(path);
    if (id) {
      void loadAnalysis(id);
    }
  }, [path]);

  function navigate(to: string) {
    window.history.pushState(null, '', to);
    setPath(to);
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

  const detailId = getPropertyIdFromPath(path);

  return (
    <main className="app-shell">
      <header className="topbar">
        <button className="brand-button" type="button" onClick={() => navigate('/')}>
          <span className="brand-mark">PS</span>
          <span>PropSegura</span>
        </button>
        <nav className="topnav" aria-label="Navegación principal">
          <button className={path === '/' ? 'topnav-link active' : 'topnav-link'} type="button" onClick={() => navigate('/')}>
            Inicio
          </button>
          <button className={path === '/properties' ? 'topnav-link active' : 'topnav-link'} type="button" onClick={() => navigate('/properties')}>
            Propiedades
          </button>
          <button className={path === '/properties/new' ? 'topbar-cta active' : 'topbar-cta'} type="button" onClick={() => navigate('/properties/new')}>
            Nueva propiedad
          </button>
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

      {path === '/' && (
        <LandingPage
          onCreate={() => navigate('/properties/new')}
          onDashboard={() => navigate('/properties')}
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
        PropSegura — Segunda opinión para comprar una propiedad con menos riesgo.
      </footer>
    </main>
  );
}

function LandingPage({
  onCreate,
  onDashboard
}: {
  onCreate: () => void;
  onDashboard: () => void;
}) {
  return (
    <>
      <section className="hero">
        <div className="hero-content">
          <p className="eyebrow">La segunda opinión antes de comprar una propiedad</p>
          <h1>Comprá una propiedad con más información y menos riesgo</h1>
          <p className="hero-copy">
            No necesitás tener todos los papeles para empezar.
            <br />
            Primero evaluá si vale la pena avanzar.
            <br />
            Después validá la documentación antes de reservar.
          </p>
          <div className="hero-actions">
            <button className="primary-button large-button" type="button" onClick={onCreate}>
              Analizar una propiedad
            </button>
            <button className="secondary-button large-button" type="button" onClick={onDashboard}>
              Ver propiedades cargadas
            </button>
            <a className="secondary-link" href="#process">
              Ver cómo funciona
            </a>
          </div>
        </div>
        <div className="hero-visual" aria-hidden="true">
          <div className="property-card">
            <span className="property-chip">Palermo</span>
            <div className="property-window-grid">
              <span />
              <span />
              <span />
              <span />
            </div>
            <div className="property-score">
              <strong>82</strong>
              <span>Oportunidad</span>
            </div>
          </div>
        </div>
      </section>

      <section className="panel product-explainer">
        <div>
          <p className="eyebrow">Producto</p>
          <h2>Una vista simple para decidir mejor antes de reservar</h2>
        </div>
        <div className="benefit-grid">
          <article>
            <strong>Evaluación inicial</strong>
            <p>Compará precio, metros, expensas, estado y barrio para entender si vale la pena avanzar.</p>
          </article>
          <article>
            <strong>Validación documental</strong>
            <p>Cuando tengas papeles, revisá faltantes y señales de riesgo antes de comprometer dinero.</p>
          </article>
          <article>
            <strong>Decisión más segura</strong>
            <p>Usá el análisis para preguntar, negociar o frenar a tiempo.</p>
          </article>
        </div>
      </section>

      <ProcessSection />
    </>
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
  const totalPages = Math.max(1, Math.ceil(analyses.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleAnalyses = analyses.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  useEffect(() => {
    setPage(1);
  }, [analyses.length, neighborhoodFilter, recommendedUseFilter, riskFilter, sortBy, pageSize]);

  async function handleDelete(analysis: PropertyAnalysisSummary) {
    const confirmed = window.confirm(`¿Eliminar "${analysis.title}"? Esta acción no se puede deshacer.`);

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
    <section className="panel dashboard-panel dashboard-page">
      <div className="section-heading dashboard-heading">
        <div>
          <p className="eyebrow">Panel</p>
          <h2>Propiedades cargadas</h2>
          <p className="section-copy">Compará oportunidades, completá datos faltantes y continuá el análisis cuando quieras.</p>
        </div>
        <div className="dashboard-actions">
          <button className="ghost-button" type="button" onClick={() => void onRefresh()}>
            Actualizar
          </button>
          <button className="primary-button" type="button" onClick={onCreate}>
            Nueva propiedad
          </button>
        </div>
      </div>

      <div className="dashboard-filters compact-filters">
        <label className="field">
          <span>Barrio</span>
          <select value={neighborhoodFilter} onChange={(event) => onNeighborhoodFilter(event.target.value)}>
            <option value="">Todos</option>
            {neighborhoods.map((neighborhood) => (
              <option key={neighborhood} value={neighborhood}>{neighborhood}</option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>Uso</span>
          <select value={recommendedUseFilter} onChange={(event) => onRecommendedUseFilter(event.target.value)}>
            <option value="">Todos</option>
            {['Living', 'Investment', 'Both', 'NotRecommended', 'Unknown'].map((value) => (
              <option key={value} value={value}>{recommendedUseLabel(value)}</option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>Riesgo</span>
          <select value={riskFilter} onChange={(event) => onRiskFilter(event.target.value)}>
            <option value="">Todos</option>
            {['Low', 'Medium', 'High'].map((value) => (
              <option key={value} value={value}>{riskLevelLabel(value)}</option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>Orden</span>
          <select value={sortBy} onChange={(event) => onSortBy(event.target.value as DashboardSort)}>
            <option value="newest">Más recientes</option>
            <option value="title">Propiedad</option>
            <option value="neighborhood">Barrio</option>
            <option value="highestOpportunity">Mayor oportunidad</option>
            <option value="highestProtection">Mayor protección</option>
            <option value="lowestPrice">Menor precio</option>
            <option value="totalSquareMeters">Metros totales</option>
            <option value="lowestUsdPerSquareMeter">Menor USD/m²</option>
            <option value="recommendedUse">Uso recomendado</option>
            <option value="bestInvestment">Mejor para inversión</option>
            <option value="bestLiving">Mejor para vivienda</option>
          </select>
        </label>
      </div>

      {analyses.length === 0 ? (
        <div className="empty-state dashboard-empty">
          <strong>Todavía no cargaste propiedades.</strong>
          <button className="primary-button" type="button" onClick={onCreate}>
            Analizar primera propiedad
          </button>
        </div>
      ) : (
        <div className="comparison-table-wrap">
          <table className="comparison-table">
            <thead>
              <tr>
                <SortableHeader label="Propiedad" sortKey="title" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                <SortableHeader label="Barrio" sortKey="neighborhood" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                <SortableHeader label="Precio" sortKey="lowestPrice" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                <SortableHeader label="m²" sortKey="totalSquareMeters" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                <SortableHeader label="USD/m²" sortKey="lowestUsdPerSquareMeter" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                <SortableHeader label="Oportunidad" sortKey="highestOpportunity" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                <SortableHeader label="Protección" sortKey="highestProtection" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
                <SortableHeader label="Uso" sortKey="recommendedUse" activeSort={sortBy} direction={sortDirection} onSort={onColumnSort} />
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
      {analyses.length > 0 && (
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
    </section>
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
        <span aria-hidden="true">{isActive ? (direction === 'asc' ? '▲' : '▼') : '↕'}</span>
      </button>
    </th>
  );
}

function PropertyDashboardCard({ analysis, onOpen }: { analysis: PropertyAnalysisSummary; onOpen: (id: string) => Promise<void> }) {
  return (
    <article className="property-dashboard-card">
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
        <button className="property-title-button" type="button" onClick={(event) => { event.stopPropagation(); open(); }}>
          {analysis.title}
        </button>
      </td>
      <td>{analysis.neighborhood || 'Sin dato'}</td>
      <td>{formatMoney(analysis.priceUsd)}</td>
      <td>{formatNumber(analysis.totalSquareMeters)}</td>
      <td>{formatMoney(analysis.usdPerSquareMeter)}</td>
      <td><span className="score-pill score-pill-opportunity">{analysis.opportunityScore}</span></td>
      <td><span className="score-pill score-pill-protection">{analysis.protectionScore}</span></td>
      <td><Badge tone={recommendedTone(analysis.recommendedUse)}>{recommendedUseLabel(analysis.recommendedUse)}</Badge></td>
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

function MiniScore({ label, score, tone }: { label: string; score: number; tone: 'opportunity' | 'protection' }) {
  return (
    <div className={`mini-score mini-score-${tone}`}>
      <strong>{score}</strong>
      <span>{label}</span>
    </div>
  );
}

function ProcessSection() {
  return (
    <section className="process-section" id="process">
      <div className="section-heading process-heading">
        <div>
          <p className="eyebrow">Tres pasos claros</p>
          <h2>Cómo funciona PropSegura</h2>
        </div>
      </div>
      <div className="process-grid">
        <ProcessCard
          step="1"
          title="Evaluación inicial"
          text="Empezá con los datos de la publicación: precio, zona, expensas, metros y características."
          items={['Publicación', 'Precio', 'Zona', 'Expensas']}
        />
        <ProcessCard
          step="2"
          title="Validación documental"
          text="Cuando la propiedad te interesa, cargá documentación para detectar riesgos antes de reservar."
          items={['Actas', 'Reglamento', 'Reserva', 'Informe de dominio']}
        />
        <ProcessCard
          step="3"
          title="Compra más segura"
          text="Usá los hallazgos para decidir, negociar o frenar antes de comprometer dinero."
          items={['Riesgos', 'Preguntas clave', 'Señales de alerta', 'Próximos pasos']}
        />
      </div>
    </section>
  );
}

function ProcessCard({ step, title, text, items }: { step: string; title: string; text: string; items: string[] }) {
  return (
    <article className="process-card">
      <span className="process-icon">{step}</span>
      <h3>{title}</h3>
      <p>{text}</p>
      <div className="process-checklist">
        {items.map((item) => (
          <span key={item}>{item}</span>
        ))}
      </div>
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
  const [submitting, setSubmitting] = useState(false);
  const [importing, setImporting] = useState(false);

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
        hasBalcony: nullableBoolean(form.hasBalcony),
        hasAmenities: nullableBoolean(form.hasAmenities),
        isCreditEligible: nullableBoolean(form.isCreditEligible),
        propertyCondition: form.propertyCondition,
        notes: nullableText(form.notes)
      });

      await onCreated(created);
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo crear el análisis.');
    } finally {
      setSubmitting(false);
      setLoading(false);
    }
  }

  return (
    <section className="panel form-panel">
      <div className="section-heading">
        <div>
          <p className="eyebrow">Nuevo análisis</p>
          <h2>Datos iniciales de la propiedad</h2>
          <p className="section-copy">
            No necesitás actas, reglamentos ni planos todavía.
            <br />
            Completá la información que tengas de la publicación y obtené una evaluación inicial.
          </p>
        </div>
        <button className="ghost-button" type="button" onClick={onCancel}>
          Volver
        </button>
      </div>

      <form className="form-grid" onSubmit={handleSubmit}>
        <FormGroup title="Publicación">
          <div className="import-row">
            <TextField label="Pegá el link de la publicación" value={form.listingUrl} onChange={(listingUrl) => setForm({ ...form, listingUrl })} />
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
          <TextField label="Título" value={form.title} required onChange={(title) => setForm({ ...form, title })} />
        </FormGroup>
        <FormGroup title="Ubicación">
          <TextField label="Dirección" value={form.address} required onChange={(address) => setForm({ ...form, address })} />
          <TextField label="Barrio" value={form.neighborhood} required onChange={(neighborhood) => setForm({ ...form, neighborhood })} />
        </FormGroup>
        <FormGroup title="Precio y expensas">
          <TextField label="Precio USD" type="number" value={form.priceUsd} onChange={(priceUsd) => setForm({ ...form, priceUsd })} />
          <TextField label="Expensas mensuales" type="number" value={form.monthlyExpenses} onChange={(monthlyExpenses) => setForm({ ...form, monthlyExpenses })} />
          <TextField label="Metros totales" type="number" value={form.totalSquareMeters} onChange={(totalSquareMeters) => setForm({ ...form, totalSquareMeters })} />
          <TextField label="Metros cubiertos" type="number" value={form.coveredSquareMeters} onChange={(coveredSquareMeters) => setForm({ ...form, coveredSquareMeters })} />
        </FormGroup>
        <FormGroup title="Características">
          <TextField label="Ambientes" type="number" value={form.rooms} onChange={(rooms) => setForm({ ...form, rooms })} />
          <TextField label="Dormitorios" type="number" value={form.bedrooms} onChange={(bedrooms) => setForm({ ...form, bedrooms })} />
          <TextField label="Baños" type="number" value={form.bathrooms} onChange={(bathrooms) => setForm({ ...form, bathrooms })} />
          <TextField label="Antigüedad del edificio" type="number" value={form.buildingAgeYears} onChange={(buildingAgeYears) => setForm({ ...form, buildingAgeYears })} />
          <TextField label="Piso" type="number" value={form.floor} onChange={(floor) => setForm({ ...form, floor })} />
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
        <FormGroup title="Objetivo de compra">
          <label className="field field-wide">
            <span>Objetivo, dudas o notas</span>
            <textarea value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} />
          </label>
        </FormGroup>
        <div className="form-actions">
          <button
            className={`primary-button ${submitting ? 'btn-loading' : ''}`}
            type="submit"
            disabled={submitting}
            aria-busy={submitting ? 'true' : undefined}
          >
            {submitting && <span className="button-spinner" aria-hidden="true" />}
            {submitting ? 'Cargando...' : 'Analizar una propiedad'}
          </button>
        </div>
      </form>
    </section>
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
  setLoading
}: {
  analysis: PropertyAnalysis;
  onCancel: () => void;
  onSaved: (analysis: PropertyAnalysis) => void;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
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
  onAnalysisUpdated
}: {
  analysis: PropertyAnalysis;
  loading: boolean;
  onRefresh: () => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
  onAnalysisUpdated: (analysis: PropertyAnalysis) => void;
}) {
  const checklistStats = getChecklistStats(analysis.checklistItems);
  const [isEditingProperty, setIsEditingProperty] = useState(false);

  function scrollToDocuments() {
    document.getElementById('documents-workspace')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  return (
    <div className="analysis-page property-workspace">
      <section className="panel workspace-hero">
        <div className="workspace-heading">
          <div>
            <p className="eyebrow">Propiedad</p>
            <h2>{analysis.title}</h2>
            <p className="section-copy">{analysis.address} - {analysis.neighborhood || 'Sin barrio'}</p>
          </div>
          <div className="workspace-actions">
            <button className="secondary-button" type="button" onClick={() => setIsEditingProperty((current) => !current)}>
              Editar propiedad
            </button>
            <button className="secondary-button" type="button" onClick={scrollToDocuments}>
              Subir documento
            </button>
            <button className="ghost-button" type="button" onClick={scrollToDocuments}>
              Analizar documento
            </button>
            <button className="ghost-button" type="button" disabled title="Próximamente">
              Generar informe
            </button>
            <button
              className={`ghost-button ${loading ? 'btn-loading' : ''}`}
              type="button"
              onClick={onRefresh}
              disabled={loading}
              aria-busy={loading ? 'true' : undefined}
            >
              {loading && <span className="button-spinner" aria-hidden="true" />}
              {loading ? 'Cargando...' : 'Actualizar'}
            </button>
          </div>
        </div>

        <div className="workspace-summary-grid">
          <Info label="Precio" value={formatMoney(analysis.priceUsd)} />
          <Info label="m²" value={formatNumber(analysis.totalSquareMeters)} />
          <Info label="USD/m²" value={formatMoney(analysis.usdPerSquareMeter)} />
          <Info label="Barrio" value={analysis.neighborhood || 'Sin dato'} />
        </div>

        {isEditingProperty && (
          <PropertyEditForm
            analysis={analysis}
            onCancel={() => setIsEditingProperty(false)}
            onSaved={(updated) => {
              setIsEditingProperty(false);
              onAnalysisUpdated(updated);
            }}
            onError={onError}
            setLoading={setLoading}
          />
        )}
      </section>

      <section className="workspace-grid">
        <article className="panel score-workspace-card opportunity-section">
          <div className="section-heading compact-heading">
            <div>
              <p className="eyebrow">Evaluación inicial</p>
              <h3>¿Vale la pena avanzar?</h3>
            </div>
            <Badge tone={riskTone(analysis.initialRiskLevel)}>{riskLevelLabel(analysis.initialRiskLevel)}</Badge>
          </div>
          <ScoreMeter score={analysis.opportunityScore} variant="opportunity" label="Oportunidad" />
          <div className="badge-row">
            <Badge tone={recommendedTone(analysis.recommendedUse)}>{recommendedUseLabel(analysis.recommendedUse)}</Badge>
            <Badge tone="neutral">{analysis.benchmarkComparison}</Badge>
          </div>
          <p className="score-summary">{opportunitySummary(analysis)}</p>
        </article>

        <article className="panel score-workspace-card protection-section">
          <div className="section-heading compact-heading">
            <div>
              <p className="eyebrow">Validación documental</p>
              <h3>¿Estoy protegido?</h3>
            </div>
            <Badge tone={protectionTone(analysis.protectionScore)}>{protectionLabel(analysis.protectionScore)}</Badge>
          </div>
          <ScoreMeter score={analysis.protectionScore} variant="protection" label="Protección" />
          <ChecklistProgress stats={checklistStats} />
          <MissingDocuments items={analysis.checklistItems} />
        </article>
      </section>

      <section className="panel negotiation-card">
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
          <Info label="Margen de negociación" value={formatPercent(analysis.negotiationAnalysis?.negotiationMarginPercent ?? analysis.negotiationMarginPercent)} />
        </div>
        <p className="score-summary">{analysis.negotiationAnalysis?.explanation ?? analysis.negotiationExplanation}</p>
      </section>

      <section className="panel document-workspace" id="documents-workspace">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Documentos</p>
            <h3>Validación documental</h3>
            <p className="section-copy">Completá el checklist, subí documentos y analizalos cuando estés listo para validar la propiedad.</p>
          </div>
        </div>
        <div className="document-layout">
          <div>
            <h4>Checklist</h4>
            <Checklist items={analysis.checklistItems} />
          </div>
          <div>
            <h4>Subir documento</h4>
            <AddDocumentForm analysisId={analysis.id} onRefresh={onRefresh} onError={onError} setLoading={setLoading} />
          </div>
        </div>
        <div className="workspace-subsection">
          <h4>Documentos cargados</h4>
          <DocumentsList analysisId={analysis.id} documents={analysis.documents} findings={analysis.documentFindings} onRefresh={onRefresh} onError={onError} setLoading={setLoading} />
        </div>
      </section>

      <section className="panel">
        <div className="section-heading compact-heading">
          <div>
            <p className="eyebrow">Hallazgos</p>
            <h3>Señales agrupadas por severidad</h3>
          </div>
        </div>
        <GroupedFindings riskFindings={analysis.riskFindings} documentFindings={analysis.documentFindings} />
      </section>
    </div>
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

function AddDocumentForm({
  analysisId,
  onRefresh,
  onError,
  setLoading
}: {
  analysisId: string;
  onRefresh: () => Promise<void>;
  onError: (message: string | null) => void;
  setLoading: (loading: boolean) => void;
}) {
  const [form, setForm] = useState<{ documentType: DocumentType; file: File | null }>({
    documentType: 'Expenses',
    file: null
  });
  const [fileInputKey, setFileInputKey] = useState(0);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setLoading(true);
    onError(null);

    try {
      if (!form.file) {
        throw new Error('Seleccioná un PDF para subir.');
      }

      await addDocument(analysisId, {
        documentType: form.documentType,
        file: form.file
      });
      setForm({ documentType: 'Expenses', file: null });
      setFileInputKey((key) => key + 1);
      await onRefresh();
    } catch (err) {
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
        <select value={form.documentType} onChange={(event) => setForm({ ...form, documentType: event.target.value as DocumentType })}>
          {documentTypes.map((type) => (
            <option key={type} value={type}>
              {documentTypeLabels[type]}
            </option>
          ))}
        </select>
      </label>
      <label className="field">
        <span>PDF</span>
        <input
          key={fileInputKey}
          type="file"
          accept="application/pdf,.pdf"
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
        {submitting ? 'Cargando...' : 'Subir PDF'}
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
  documentFindings
}: {
  riskFindings: PropertyAnalysis['riskFindings'];
  documentFindings: DocumentFinding[];
}) {
  const combined = [
    ...riskFindings.map((finding) => ({
      id: finding.id,
      severity: finding.severity,
      category: finding.type,
      title: finding.title,
      description: finding.description,
      suggestedQuestion: null,
      suggestedChange: null
    })),
    ...documentFindings.map((finding) => ({
      id: finding.id,
      severity: finding.severity,
      category: finding.category,
      title: finding.title,
      description: finding.description,
      suggestedQuestion: finding.suggestedQuestion,
      suggestedChange: finding.suggestedChange
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
                    <p>{finding.description}</p>
                    {finding.suggestedQuestion && <p><strong>Pregunta sugerida:</strong> {finding.suggestedQuestion}</p>}
                    {finding.suggestedChange && <p><strong>Cambio sugerido:</strong> {finding.suggestedChange}</p>}
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
  document,
  findings,
  onRefresh,
  onError,
  setLoading
}: {
  analysisId: string;
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
      setText('');
      await onRefresh();
    } catch (err) {
      onError(err instanceof Error ? err.message : 'No se pudo analizar el documento.');
    } finally {
      setSubmitting(false);
      setLoading(false);
    }
  }

  return (
    <article className="document-card">
      <div className="document-header">
        <div>
          <strong>{document.fileName}</strong>
          <p>{documentTypeLabels[document.documentType]} - {document.contentType} - {document.sizeBytes} bytes</p>
        </div>
        <div className="document-status">
          <Badge tone={document.hasExtractedText ? 'success' : 'warning'}>
            {document.hasExtractedText ? 'Texto extraído' : 'Sin texto extraído'}
          </Badge>
          <Badge tone="neutral">{document.analysisStatus}</Badge>
        </div>
      </div>
      {document.analysisSummary && <p className="summary-note">{document.analysisSummary}</p>}
      <div className="text-workflow">
        <div className="text-workflow-header">
          <strong>Texto extraído</strong>
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
            {isEditingText ? 'Ocultar edición' : 'Editar texto extraído'}
          </button>
        </div>
        {isEditingText && (
          <div className="text-editor">
            <textarea
              className="analysis-text"
              placeholder="Corregí o pegá el texto de la reserva antes de analizar"
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
        <div className="mini-findings">
          {findings.map((finding) => (
            <Badge tone={findingTone(finding.severity)} key={finding.id}>
              {findingSeverityLabel(finding.severity)} - {findingCategoryLabel(finding.category)}
            </Badge>
          ))}
        </div>
      )}
    </article>
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
            {finding.suggestedQuestion && <p><strong>Pregunta sugerida:</strong> {finding.suggestedQuestion}</p>}
            {finding.suggestedChange && <p><strong>Cambio sugerido:</strong> {finding.suggestedChange}</p>}
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

function TextField({
  label,
  value,
  onChange,
  type = 'text',
  required = false
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  required?: boolean;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <input type={type} value={value} required={required} onChange={(event) => onChange(event.target.value)} />
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
    hasBalcony: booleanToFormValue(analysis.hasBalcony),
    hasAmenities: booleanToFormValue(analysis.hasAmenities),
    isCreditEligible: booleanToFormValue(analysis.isCreditEligible),
    propertyCondition: analysis.propertyCondition,
    notes: analysis.notes ?? ''
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

function getPropertyIdFromPath(path: string) {
  const match = path.match(/^\/properties\/([^/]+)$/);
  if (!match || match[1] === 'new') {
    return null;
  }

  return match[1];
}

function formatMoney(value?: number | null) {
  return value == null ? 'Sin dato' : value.toLocaleString('es-AR');
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
    NotRecommended: 'No recomendado',
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
