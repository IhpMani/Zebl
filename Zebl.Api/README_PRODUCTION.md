# Production Deployment Guide

## Security Configuration

### 1. JWT Secret Key
**CRITICAL**: Before deploying to production, you MUST change the JWT secret key in `appsettings.json`:

```json
"JwtSettings": {
  "SecretKey": "GENERATE_A_STRONG_SECRET_KEY_MIN_32_CHARACTERS",
  "Issuer": "ZeblApi",
  "Audience": "ZeblApiUsers",
  "ExpirationMinutes": 60
}
```

**Recommended**: Use Azure Key Vault, AWS Secrets Manager, or environment variables for production.

### 2. Connection String
**DO NOT** commit production connection strings to source control. Use:
- Azure Key Vault
- AWS Secrets Manager
- Environment Variables
- User Secrets (for development)

### 3. CORS Configuration
Update `appsettings.json` with your production frontend URLs:

```json
"CorsSettings": {
  "AllowedOrigins": [
    "https://your-production-domain.com"
  ]
}
```

## Environment Variables

Set these in your production environment:

```bash
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection="Your Production Connection String"
JwtSettings__SecretKey="Your Production Secret Key"
```

## Health Checks

- Health Check Endpoint: `/health`
- Health Check UI: `/health-ui` (Development only)

## Rate Limiting

Rate limiting is enabled by default. Configure in `appsettings.json`:

```json
"RateLimiting": {
  "EnableRateLimiting": true,
  "PermitLimit": 100,
  "Window": "00:01:00"
}
```

## Authentication

### Development Mode
By default, authentication is **disabled** in Development (`appsettings.Development.json`):
```json
"RequireAuthentication": false
```

### Production Mode
In Production, authentication is **required** (`appsettings.json`):
```json
"RequireAuthentication": true
```

### Using JWT Authentication
When authentication is enabled:

1. Obtain a JWT token from your authentication provider
2. Include in requests: `Authorization: Bearer <token>`
3. In Swagger UI (development), click "Authorize" and enter: `Bearer <token>`

**Note**: The JWT secret key must be at least 32 characters for HS256 algorithm.

## Logging

Logs are written to:
- Console (stdout)
- File: `logs/log-YYYYMMDD.txt` (retained for 7 days)

For production, consider:
- Application Insights
- ELK Stack
- CloudWatch (AWS)
- Azure Monitor

## Required NuGet Packages

All packages are already added to `Zebl.Api.csproj`:
- Microsoft.AspNetCore.Authentication.JwtBearer
- AspNetCoreRateLimit
- Serilog.AspNetCore
- AspNetCore.HealthChecks.SqlServer

## Deployment Checklist

- [ ] Change JWT secret key
- [ ] Configure production connection string (use secrets)
- [ ] Update CORS allowed origins
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Configure logging destination (Application Insights, etc.)
- [ ] Review rate limiting settings
- [ ] Enable HTTPS only
- [ ] Configure firewall rules
- [ ] Set up monitoring/alerting
- [ ] Review security headers
- [ ] Test health check endpoints
- [ ] Verify authentication is working

## Security Features Implemented

✅ JWT Authentication
✅ Authorization on all endpoints
✅ CORS configuration
✅ Rate limiting
✅ Security headers (X-Frame-Options, X-Content-Type-Options, etc.)
✅ Input validation
✅ Error handling (no stack traces in production)
✅ Structured logging
✅ Health checks
✅ HTTPS enforcement

## Notes

- Swagger is only enabled in Development environment
- Health Check UI is available at `/health-ui` (consider restricting in production)
- All endpoints require authentication by default

