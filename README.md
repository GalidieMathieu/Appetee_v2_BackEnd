# Appetee — Backend Service

## Current Status and Next Steps

### Deployment footprint
- 2 App Services: one for the Angular frontend, one for the ASP.NET Core backend
- 1 Azure Database for MySQL Flexible Server
- 1 Azure Storage Account (Blobs)
- 1 Azure Key Vault for secrets and connection strings

### Deployed frontend (public link)
- https://appetee-awhsgkeqamfqh0dg.eastus-01.azurewebsites.net/

### Implemented features
- Account creation (sign-up)
- Login / sign-in flows
- Cookie-based authentication handling (secure cookies, appropriate SameSite and Secure policies)
- Backend project structure: controllers, services, queries, repositories, and infrastructure layers
- Centralized error handling that returns RFC 7807 `ProblemDetails` responses

### What remains (TODO)
- Wire Azure integration for: App Service -> MySQL, Blob Storage, and Key Vault
- Create recipe management features
- Implement core meal-prep algorithms (planning, batching, optimization)

## Local development

### Prerequisites
- .NET 10 SDK installed
- Local or remote MySQL instance with the expected schema

### Configuration
- Edit `src/Appetee.Api/appsettings.json` (or use environment variables) to set:
  - `ConnectionStrings:AppeteeDb` — connection string for MySQL
  - Azure-related settings (storage, key vault, etc.) when wiring cloud services
- Recommended: store production secrets in Azure Key Vault and configure App Service to reference them directly

### Run locally
1. From repository root:
   - `cd src/Appetee.Api`
   - `dotnet restore`
   - `dotnet run`
2. The API will print listening URLs to the console.

### Security notes
- Cookies are configured with `HttpOnly`, `Secure`, and an appropriate `SameSite` policy.
- `UseExceptionHandler("/error")` is enabled and returns structured `ProblemDetails`; avoid exposing stack traces in production.
- Use Azure Key Vault for production secrets — do not hardcode connection strings or credentials.

### Observability & diagnostics
- Logging is integrated via `ILogger<>` throughout controllers and services.
- Central error handling logs both known `ApiException` instances and unexpected exceptions with a trace id for correlation.


