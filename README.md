# PropSeguro

PropSeguro es el repositorio y backend de la marca visible **PropSegura**: una segunda opinion antes de comprar una propiedad en Argentina.

La aplicacion permite cargar propiedades, comparar oportunidades, completar datos faltantes y validar documentacion cuando el comprador esta listo para avanzar.

## Roadmap

Prioridad actual:

1. Property Dashboard: ver y comparar propiedades cargadas.
2. OCR fallback: mejorar extraccion cuando el PDF no trae texto legible.
3. Buyer Report export: generar un reporte para revisar o compartir.
4. Shareable analysis link: compartir un analisis con otra persona.

Mas adelante:

- Vision analysis.
- Autenticacion.
- Pagos.

## Arquitectura

- `src/PropSeguro.Domain`: entidades de dominio y reglas deterministicas.
- `src/PropSeguro.Application`: comandos, queries, DTOs y abstracciones.
- `src/PropSeguro.Infrastructure`: EF Core, SQL Server, migraciones, storage local y servicios externos simples.
- `src/PropSeguro.Api`: endpoints Web API, Swagger y CORS de desarrollo.
- `frontend/propseguro-web`: SPA en React + Vite + TypeScript.

## Funcionalidades principales

- Crear analisis de una propiedad desde datos basicos.
- Importar datos iniciales desde un link de publicacion cuando sea posible.
- Ver `OpportunityScore`, `ProtectionScore`, uso recomendado y riesgo inicial.
- Ver rango de negociacion estimado.
- Cargar una imagen principal para reconocer cada propiedad.
- Subir documentos PDF y guardar texto extraido o editado.
- Analizar reservas con reglas deterministicas para detectar clausulas riesgosas.
- Usar el dashboard para retomar propiedades y completar datos o documentacion mas tarde.

## Property Dashboard

En la home, la seccion **Propiedades analizadas** muestra todas las propiedades cargadas.

Desde el dashboard podes:

- Crear una nueva propiedad con **Nueva propiedad**.
- Abrir una propiedad existente con **Continuar analisis**.
- Completar datos faltantes con **Completar datos**.
- Ir a la validacion documental con **Validar documentacion**.
- Filtrar por barrio, uso recomendado y riesgo inicial.
- Ordenar por mayor `OpportunityScore`, menor precio, menor USD/m2 o mas recientes.

Cada tarjeta muestra precio, USD/m2, expensas, metros, `OpportunityScore`, `ProtectionScore`, documentos faltantes y hallazgos detectados.

## Backend

Endpoints principales:

- `POST /api/property-analyses`: crea un analisis.
- `GET /api/property-analyses`: lista resumenes para el dashboard.
- `GET /api/property-analyses/{id}`: obtiene el detalle completo.
- `PUT /api/property-analyses/{id}`: actualiza datos incompletos y recalcula oportunidad.
- `POST /api/property-analyses/{id}/cover-image`: sube una imagen principal JPEG, PNG o WEBP de hasta 5 MB.
- `GET /api/property-analyses/{id}/cover-image`: sirve la imagen principal cargada.
- `POST /api/listing-imports`: intenta importar datos desde una publicacion.
- `POST /api/property-analyses/{id}/documents`: sube un documento PDF.
- `PUT /api/property-analyses/{analysisId}/documents/{documentId}/text`: guarda texto extraido/editado.
- `POST /api/property-analyses/{analysisId}/documents/{documentId}/analyze`: analiza el documento.

## Ejecutar localmente

Backend:

```powershell
dotnet run --project src/PropSeguro.Api
```

Frontend:

```powershell
cd frontend/propseguro-web
npm install
npm run dev
```

El frontend usa `VITE_API_BASE_URL`; ver `frontend/propseguro-web/.env.example`.

## Base de datos

La cadena de conexion de desarrollo esta en:

```text
src/PropSeguro.Api/appsettings.Development.json
```

Aplicar migraciones:

```powershell
dotnet ef database update --project src/PropSeguro.Infrastructure --startup-project src/PropSeguro.Api
```

## Validacion

Build backend:

```powershell
dotnet build PropSeguro.sln
```

Tests backend:

```powershell
dotnet test PropSeguro.sln
```

Build frontend:

```powershell
cd frontend/propseguro-web
npm run build
```

## Notas de producto

- No hay autenticacion ni pagos.
- Hay una imagen principal por propiedad solo para identificacion visual.
- No hay galeria, analisis de imagenes ni vision artificial.
- La logica actual es deterministica.
- El analisis documental no reemplaza la revision de un escribano o abogado.
