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
- Analizar expensas para detectar riesgos del edificio, costos futuros, morosidad, obras y calcular `BuildingHealthScore`.
- Usar el dashboard para retomar propiedades y completar datos o documentacion mas tarde.
- Acceder por beta privada con usuarios habilitados por un administrador.

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

- `POST /api/auth/login`: inicia sesion y devuelve JWT.
- `POST /api/beta-access-requests`: registra una solicitud publica de acceso beta.
- `GET /api/admin/beta-access-requests`: lista solicitudes beta (admin).
- `POST /api/admin/beta-access-requests/{id}/approve`: aprueba una solicitud y crea/invita usuario (admin).
- `POST /api/admin/beta-access-requests/{id}/reject`: rechaza una solicitud (admin).
- `GET /api/admin/users`: lista usuarios (admin).
- `POST /api/admin/users`: crea un usuario (admin).
- `PATCH /api/admin/users/{id}/enable`: habilita usuario (admin).
- `PATCH /api/admin/users/{id}/disable`: deshabilita usuario (admin).
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

## Beta privada y login

En Development se puede seedear un administrador desde:

```json
"SeedAdmin": {
  "Enabled": true,
  "Email": "admin@propsegura.local",
  "FullName": "Admin PropSegura",
  "Password": ""
}
```

Para usar seed admin en Development, configurar la contraseña por user-secrets:

```powershell
dotnet user-secrets set "SeedAdmin:Password" "TU_PASSWORD_ADMIN_DEV" --project src/PropSeguro.Api
```

En `appsettings.json` los valores sensibles quedan como placeholders. Para ambientes fuera de Development configurar por variables de entorno, user secrets o el proveedor del deploy:

```json
"Jwt": {
  "Issuer": "PropSegura",
  "Audience": "PropSegura",
  "SigningKey": "__SET_IN_USER_SECRETS_OR_ENVIRONMENT__",
  "ExpirationMinutes": 720
},
"Cors": {
  "AllowedOrigins": ["https://tu-frontend.example"]
}
```

Notas de seguridad:

- En produccion `SeedAdmin:Enabled` debe quedar en `false` salvo inicializacion controlada.
- Si se habilita seed en produccion, usar una password segura; la API falla al iniciar si no cumple minimo 8 caracteres, una letra y un numero.
- CORS no usa `AllowAnyOrigin`; en produccion solo responde a `Cors:AllowedOrigins`.

El frontend expone:

- `/login`: ingreso de usuarios habilitados.
- `/solicitar-acceso`: solicitud publica de acceso beta.
- `/admin`: gestion de solicitudes y usuarios para administradores.
- `/properties`, `/properties/new` y `/properties/{id}`: rutas protegidas.

El token JWT se guarda en `localStorage` del navegador para desarrollo local.

## Email de invitaciones beta

La aprobacion de solicitudes beta puede enviar un email de invitacion con URL de login, email y contraseña temporal.

En Development el proveedor queda configurado para DonWeb, pero la contraseña SMTP no se versiona. Configurarla por variable de entorno o user-secrets antes de iniciar la API:

```powershell
dotnet user-secrets set "Email:Smtp:Password" "TU_PASSWORD_SMTP" --project src/PropSeguro.Api
```

Alternativamente, en la misma terminal desde la que vas a ejecutar la API:

```powershell
$env:Email__Smtp__Password="TU_PASSWORD_SMTP"
```

Notas:

- User Secrets funciona en Development porque `PropSeguro.Api.csproj` define `UserSecretsId`.
- Variables de entorno usan doble guion bajo para secciones anidadas: `Email__Smtp__Password`.
- Si seteas la variable despues de iniciar la API, reinicia el proceso; la configuracion no cambia en un proceso ya levantado.
- No guardar la contraseña SMTP en `appsettings*.json`.

Configuracion DonWeb:

```json
"Email": {
  "Provider": "Smtp",
  "FromEmail": "hola@propsegura.com.ar",
  "FromName": "PropSegura",
  "Smtp": {
    "Host": "a0140791.ferozo.com",
    "Port": 465,
    "Username": "hola@propsegura.com.ar",
    "Password": "__SET_EMAIL_SMTP_PASSWORD_IN_ENVIRONMENT__",
    "EnableSsl": true
  }
}
```

Con puerto `465` y `EnableSsl: true`, la API usa SSL directo (`SslOnConnect`).

Endpoint de prueba en Development:

Antes de probar el envio, podes auditar la configuracion efectiva sin exponer secretos:

```http
GET /api/dev/config/email
Authorization: Bearer <token-admin>
```

El campo `passwordSource` indica si la clave viene de user-secrets, environment variables u otro proveedor de configuracion.

```http
POST /api/dev/email-test
Authorization: Bearer <token-admin>
Content-Type: application/json

{
  "toEmail": "tu-email@dominio.com",
  "toName": "Prueba"
}
```

Respuesta esperada:

```json
{
  "emailSent": true,
  "message": "Correo de prueba enviado a tu-email@dominio.com."
}
```

Si el envio falla, la API responde un error controlado y registra host, puerto, proveedor y destinatario. No registra la contraseña SMTP.

Para volver a modo local sin email real, usar:

```powershell
$env:Email__Provider="Null"
```

Con `Provider: "Null"` no se envia ningun email real. La API devuelve la contraseña temporal al admin para poder probar el flujo localmente.

Para produccion configurar SMTP por variables de entorno, user secrets o el proveedor del deploy:

```json
"Email": {
  "Provider": "Smtp",
  "FromEmail": "hola@propsegura.com.ar",
  "FromName": "PropSegura",
  "Smtp": {
    "Host": "a0140791.ferozo.com",
    "Port": 465,
    "Username": "hola@propsegura.com.ar",
    "Password": "__SET_IN_ENVIRONMENT__",
    "EnableSsl": true
  }
}
```

No commitear credenciales reales. Para una beta privada en Azure, SMTP con un proveedor transaccional como SendGrid o el SMTP del dominio alcanza para esta primera version.

## Base de datos

La cadena de conexion de desarrollo esta en:

```text
src/PropSeguro.Api/appsettings.Development.json
```

Aplicar migraciones:

```powershell
dotnet ef database update --project src/PropSeguro.Infrastructure --startup-project src/PropSeguro.Api
```

## Preparacion para Azure

Arquitectura objetivo para beta privada:

- Frontend: Azure Static Web Apps.
- Backend: Azure App Service con .NET 8.
- Base de datos: Azure SQL Database.
- Archivos: Azure Blob Storage.
- Dominio: `propsegura.com.ar`.
- Email: SMTP DonWeb para invitaciones beta. SendGrid o Azure Communication Services quedan como alternativas futuras.

Recursos Azure necesarios:

- Resource Group, por ejemplo `rg-propsegura-beta`.
- Azure SQL Server y Azure SQL Database.
- Azure App Service Plan y Web App para `PropSeguro.Api`.
- Storage Account con container privado `propsegura-files`.
- Azure Static Web App para `frontend/propseguro-web`.
- DNS en el proveedor del dominio para `propsegura.com.ar` y, opcionalmente, `api.propsegura.com.ar`.

Configuracion del backend en Azure App Service:

Configurar como App Settings o Connection Strings, no en archivos versionados.

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<cadena Azure SQL>
Jwt__Issuer=PropSegura
Jwt__Audience=PropSegura
Jwt__Secret=<secreto largo de 32+ bytes>
Cors__AllowedOrigins__0=https://propsegura.com.ar
Frontend__BaseUrl=https://propsegura.com.ar
Frontend__LoginUrl=https://propsegura.com.ar/login
SeedAdmin__Enabled=false
Storage__Provider=AzureBlob
Storage__LocalBasePath=storage
Storage__AzureBlob__ConnectionString=<connection string del storage account>
Storage__AzureBlob__ContainerName=propsegura-files
Storage__MaxPdfSizeBytes=15728640
Storage__MaxImageSizeBytes=5242880
Email__Provider=Smtp
Email__FromEmail=hola@propsegura.com.ar
Email__FromName=PropSegura
Email__Smtp__Host=a0140791.ferozo.com
Email__Smtp__Port=465
Email__Smtp__Username=hola@propsegura.com.ar
Email__Smtp__Password=<password SMTP>
Email__Smtp__EnableSsl=true
```

Notas:

- `appsettings.json` y `appsettings.Production.json` contienen placeholders seguros; los secretos reales deben entrar por variables de entorno/App Settings.
- En Production la API falla al iniciar si `Jwt__Secret` esta vacio, usa placeholder o tiene menos de 32 bytes. `Jwt__SigningKey` queda soportado por compatibilidad, pero para Azure usar `Jwt__Secret`.
- `SeedAdmin__Enabled` debe quedar `false` en Production. Crear usuarios admin de beta con un proceso controlado o habilitar seed solo de forma temporal y con password fuerte.
- Para la beta con invitaciones reales, usar `Email__Provider=Smtp` con la configuracion DonWeb anterior. `Email__Provider=Null` queda solo para smoke tests temporales donde se copie la contraseña manualmente.
- Swagger queda habilitado solo en Development. En Production no se expone `/swagger`.
- Login y solicitud de acceso beta tienen rate limiting basico por IP: 5 intentos por minuto para login y 5 solicitudes por hora para acceso beta.

Configuracion del frontend en Azure Static Web Apps:

```text
VITE_API_BASE_URL=https://api.propsegura.com.ar
```

Si todavia no existe subdominio de API, usar temporalmente la URL del App Service:

```text
VITE_API_BASE_URL=https://<tu-app-service>.azurewebsites.net
```

Migraciones Azure SQL:

```powershell
$env:ConnectionStrings__DefaultConnection="<cadena Azure SQL>"
dotnet ef database update --project src/PropSeguro.Infrastructure --startup-project src/PropSeguro.Api --configuration Release
```

Blob Storage:

- Crear el container `propsegura-files`.
- Mantener acceso publico deshabilitado.
- La API sirve las imagenes, documentos y reportes mediante endpoints autenticados; el frontend no accede directo al blob.
- Los nombres de archivo almacenados se generan del lado servidor. No se usa el nombre original como ruta de storage.
- Las respuestas publicas de la API no exponen rutas fisicas ni nombres internos de Blob Storage.
- Uploads permitidos: PDF `application/pdf`; imagenes `image/jpeg`, `image/png`, `image/webp`.
- Limites configurables: `Storage__MaxPdfSizeBytes` y `Storage__MaxImageSizeBytes`.
- En Development se mantiene `Storage__Provider=Local` y los archivos se guardan bajo `Storage__LocalBasePath`, por defecto `storage/`.
- Los informes PDF se generan bajo demanda. No se persisten actualmente; si en el futuro se guardan, deben pasar por `IFileStorageService`.

Configuracion rapida de storage:

```text
# Local / Development
Storage__Provider=Local
Storage__LocalBasePath=storage

# Azure / Production
Storage__Provider=AzureBlob
Storage__AzureBlob__ConnectionString=<connection string de propsegurastorage>
Storage__AzureBlob__ContainerName=propsegura-files
```

Las claves internas se generan con la abstraccion de storage y usan nombres seguros del servidor, por ejemplo `property-analyses/{propertyId}/documents/{documentId}/original.pdf` y `property-analyses/{propertyId}/cover/{guid}.jpg`. Si una fila existente conserva una ruta local anterior, la API intenta leerla desde el proveedor local configurado y no requiere migracion destructiva.

DNS y dominio:

- Static Web Apps: configurar dominio custom `propsegura.com.ar` y seguir el TXT/CNAME indicado por Azure.
- App Service: configurar `api.propsegura.com.ar` si se quiere separar API y frontend.
- Actualizar `Cors__AllowedOrigins__0` con el dominio final del frontend.
- Actualizar `Frontend__BaseUrl` y `Frontend__LoginUrl` con el dominio final.

Checklist antes de beta:

- Ejecutar migraciones contra Azure SQL.
- Confirmar que App Service arranca con `ASPNETCORE_ENVIRONMENT=Production`.
- Confirmar que login/admin funciona con usuarios reales.
- Confirmar carga de portada y PDF contra Blob Storage.
- Confirmar que `/swagger` responde 404 en Production.
- Confirmar CORS desde `https://propsegura.com.ar`.
- Confirmar que `Jwt__Secret`, `Email__Smtp__Password`, `ConnectionStrings__DefaultConnection` y `Storage__AzureBlob__ConnectionString` estan definidos en App Service y no en archivos versionados.
- Confirmar que no hay secretos reales commiteados.

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

- Hay autenticacion basica para beta privada; no hay pagos.
- Hay una imagen principal por propiedad solo para identificacion visual.
- No hay galeria, analisis de imagenes ni vision artificial.
- La logica actual es deterministica.
- El analisis documental no reemplaza la revision de un escribano o abogado.
