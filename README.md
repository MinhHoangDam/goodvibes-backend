# Good Vibes Backend API

A production-ready .NET backend API for displaying and managing Workleap Officevibe Good Vibes. This project demonstrates best practices for integrating with Workleap APIs, including caching, rate limiting, and avatar enrichment.

## Features

- **Complete Good Vibes API Coverage**: List, view, and filter Good Vibes with replies
- **User Avatar Enrichment**: Automatic avatar fetching and caching from Workleap User API
- **Advanced Statistics**: Top senders, top recipients, monthly leaderboards, and collection analytics
- **Smart Caching**: Background cache service with automatic refresh every 5 minutes
- **Rate Limiting Protection**: Built-in delays and retry logic to prevent API throttling
- **Azure Ready**: Optional Azure Key Vault integration for secure API key management
- **CORS Enabled**: Ready for frontend integration
- **Swagger UI**: Interactive API documentation at `/swagger`

## Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A Workleap API key (get one from your Workleap account)

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone <your-repo-url>
   cd GoodVibesBackend
   ```

2. **Configure your API key**

   Open `appsettings.json` and replace the placeholder API key with your Workleap API key:
   ```json
   {
     "OfficevibeApiKey": "YOUR_WORKLEAP_API_KEY_HERE"
   }
   ```

3. **Run the application**
   ```bash
   dotnet run
   ```

4. **Access the API**
   - API Base URL: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger
   - Health Check: http://localhost:5000/health

## API Endpoints

### Health & Debug
- `GET /health` - Health check endpoint
- `GET /debug` - Environment and configuration info
- `GET /api/validate-keyvault` - Azure Key Vault validation (production only)

### Good Vibes
- `GET /api/good-vibes` - List all Good Vibes with optional filters
  - Query params: `isPublic`, `limit`, `continuationToken`
- `GET /api/good-vibes/{id}` - Get a single Good Vibe with replies
- `GET /api/good-vibes/collections` - List available Good Vibes collections
- `GET /api/good-vibes/cached` - Fast cached endpoint with date filtering
  - Query params: `monthsBack`, `daysBack`, `avatarSize`, `skipAvatars`

### User Information
- `GET /api/users/{userId}` - Get user information including avatar
- `GET /api/debug/user-avatar/{userId}` - Debug avatar fetching for a specific user

### Statistics & Leaderboards
- `GET /api/stats/top-senders?limit=10` - Top Good Vibes senders (all-time)
- `GET /api/stats/top-recipients?limit=10` - Top Good Vibes recipients (all-time)
- `GET /api/stats/available-months` - Get all months with Good Vibes data
- `GET /api/stats/monthly/top-senders?year=2025&month=1&limit=5` - Monthly top senders
- `GET /api/stats/monthly/top-recipients?year=2025&month=1&limit=5` - Monthly top recipients
- `GET /api/stats/monthly/top-collections?year=2025&month=1&limit=10` - Monthly top collections

## Architecture

### Caching Strategy

The application uses a smart caching system to minimize API calls and improve performance:

- **GoodVibesCacheService**: Background service that caches all Good Vibes
  - Fetches all public Good Vibes on startup
  - Automatically enriches vibes with replies
  - Refreshes cache every 5 minutes
  - ~222 vibes cached in ~10-15 seconds

- **UserCacheService**: On-demand user avatar caching
  - 1-hour cache for successful avatar fetches
  - 5-minute cache for failed fetches (prevents API hammering)
  - Supports multiple avatar sizes (24x24, 32x32, 48x48, 64x64, 128x128, 256x256)
  - Concurrent request limiting (max 10 simultaneous requests)

### Rate Limiting Protection

Built-in protection against API rate limiting:
- 100ms delays between pagination requests
- 100ms delays between reply fetch requests
- Automatic 429 (Too Many Requests) detection with retry logic
- Exponential backoff for avatar requests (1s, 2s, 4s)

### Performance Optimizations

- **Progressive Loading**: Frontend can request data in chunks using `daysBack` parameter
- **Avatar Size Selection**: Request specific avatar sizes to reduce bandwidth
- **Skip Avatars Option**: Fast mode that skips avatar enrichment for instant response
- **Parallel Processing**: Avatar enrichment uses concurrent requests with semaphore limiting

## Configuration

### Environment Variables

- `PORT` - Server port (default: 5000)
- `KeyVaultUrl` - Azure Key Vault URL (optional, for production)

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "OfficevibeApiKey": "YOUR_API_KEY_HERE"
}
```

## Deployment

### Azure App Service Deployment

This project includes GitHub Actions workflow for automatic deployment to Azure App Service.

See [QUICKSTART_AZURE.md](QUICKSTART_AZURE.md) for detailed Azure deployment instructions.

#### Quick Azure Setup:

1. **Create Azure Resources**
   - App Service (B1 or higher recommended)
   - Key Vault (optional, for secure API key storage)

2. **Configure GitHub Secrets**
   - Set up Azure credentials in GitHub repository secrets
   - The workflow file is already configured at `.github/workflows/main_goodvibes-backend.yml`

3. **Deploy**
   ```bash
   git push origin main
   ```

### Azure Key Vault (Optional)

For production deployments, store your API key securely in Azure Key Vault:

1. Create a Key Vault in Azure
2. Add a secret named `WorkleapCOToken` with your API key
3. Enable System-Assigned Managed Identity on your App Service
4. Grant the App Service access to Key Vault secrets
5. Set `KeyVaultUrl` environment variable in App Service configuration

The application will automatically use Key Vault in production and fall back to `appsettings.json` in development.

## CORS Configuration

The application is configured with permissive CORS for easy frontend integration:

```csharp
policy.AllowAnyOrigin()
      .AllowAnyMethod()
      .AllowAnyHeader();
```

**For production**, consider restricting CORS to specific origins:

```csharp
policy.WithOrigins("https://your-frontend-domain.com")
      .AllowAnyMethod()
      .AllowAnyHeader();
```

## Example Frontend Integration

```javascript
// Fetch all Good Vibes
const response = await fetch('http://localhost:5000/api/good-vibes?isPublic=true&limit=50');
const data = await response.json();

// Get cached Good Vibes (fast, for carousel)
const cachedResponse = await fetch('http://localhost:5000/api/good-vibes/cached?daysBack=30');
const cachedData = await cachedResponse.json();

// Get monthly top senders
const topSenders = await fetch('http://localhost:5000/api/stats/monthly/top-senders?year=2025&month=1&limit=5');
const leaderboard = await topSenders.json();
```

## Troubleshooting

### Rate Limiting Errors

If you see 429 errors in logs:
- The application automatically handles retries
- Increase delays in `Program.cs` if needed (currently 100ms between requests)
- Consider increasing cache refresh interval from 5 to 10 minutes

### Cache Not Ready

- Cache initialization takes 10-15 seconds on startup
- Check `/api/good-vibes/cached` response for `cacheReady: false`
- Monitor logs for "cache is ready" message

### Avatar Loading Issues

- Use `/api/debug/user-avatar/{userId}` to diagnose avatar fetch issues
- Check if users have `imageUrls` in their profile
- Failed avatar fetches are cached for 5 minutes to prevent API hammering

## Dependencies

- .NET 9.0
- Azure.Extensions.AspNetCore.Configuration.Secrets (1.4.0) - Key Vault integration
- Azure.Identity (1.17.0) - Azure authentication
- Swashbuckle.AspNetCore (6.5.0) - Swagger/OpenAPI documentation

## License

This is an example project for Workleap clients. Modify and use as needed for your integration.

## Support

For questions about Workleap APIs, contact Workleap support.

For questions about this example project, please open an issue in the repository.

## Credits

Built for Workleap clients to demonstrate best practices for Good Vibes API integration.
