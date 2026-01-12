# Good Vibes Backend - Setup Guide for Developers

This guide will walk you through setting up the Good Vibes Backend API on your local machine or deploying it to production.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Getting Your Workleap API Key](#getting-your-workleap-api-key)
4. [Testing the API](#testing-the-api)
5. [Deploying to Azure](#deploying-to-azure)
6. [Customization](#customization)
7. [Troubleshooting](#troubleshooting)

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 9.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/9.0)
  - Verify installation: `dotnet --version`
- **Git** - For cloning the repository
- **A Workleap account** with API access

## Local Development Setup

### Step 1: Clone the Repository

```bash
git clone <your-repository-url>
cd GoodVibesBackend
```

### Step 2: Configure Your API Key

1. Copy the example configuration file:
   ```bash
   cp appsettings.example.json appsettings.json
   ```

2. Open `appsettings.json` in your text editor

3. Replace `YOUR_WORKLEAP_API_KEY_HERE` with your actual Workleap API key:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "AllowedHosts": "*",
     "OfficevibeApiKey": "your-actual-api-key-here"
   }
   ```

### Step 3: Restore Dependencies

```bash
dotnet restore
```

### Step 4: Run the Application

```bash
dotnet run
```

You should see output similar to:
```
Starting server on port: 5000
Using Officevibe API Key: 1dfdd5f5...
info: GoodVibesCacheService[0]
      GoodVibesCacheService is starting - will fetch all good vibes in the background...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### Step 5: Verify It's Working

Open your browser and navigate to:
- **Swagger UI**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/health

You should see:
```json
{"status":"ok","message":"Server is running"}
```

## Getting Your Workleap API Key

### Option 1: From Workleap Portal

1. Log in to your Workleap account
2. Navigate to Settings > API Keys
3. Click "Generate New API Key"
4. Copy the key immediately (you won't be able to see it again)
5. Give it a descriptive name like "Good Vibes Backend - Development"

### Option 2: Contact Your Workleap Administrator

If you don't have permission to generate API keys:
1. Contact your Workleap account administrator
2. Request an API key with access to:
   - Officevibe Good Vibes API
   - Workleap User API (for avatar enrichment)

## Testing the API

### Using Swagger UI

1. Open http://localhost:5000/swagger in your browser
2. Explore all available endpoints
3. Try the `/api/good-vibes` endpoint:
   - Click "Try it out"
   - Set `isPublic` to `true`
   - Set `limit` to `10`
   - Click "Execute"

### Using cURL

```bash
# Get health status
curl http://localhost:5000/health

# Get Good Vibes
curl "http://localhost:5000/api/good-vibes?isPublic=true&limit=10"

# Get cached Good Vibes (fast)
curl "http://localhost:5000/api/good-vibes/cached?daysBack=30"

# Get top senders
curl "http://localhost:5000/api/stats/top-senders?limit=5"

# Get monthly leaderboard
curl "http://localhost:5000/api/stats/monthly/top-senders?year=2025&month=1&limit=5"
```

### Using Postman

1. Import the Swagger definition from http://localhost:5000/swagger/v1/swagger.json
2. Create a new collection
3. Test each endpoint

## Deploying to Azure

### Quick Azure Deployment

This project includes automated GitHub Actions deployment to Azure App Service.

#### Prerequisites

- Azure subscription
- GitHub account
- Azure CLI installed (optional, for manual setup)

#### Step 1: Create Azure Resources

Using Azure Portal:
1. Create a new **App Service**:
   - Runtime: .NET 9
   - Operating System: Linux or Windows
   - Pricing Tier: B1 (Basic) or higher recommended
   - Name: `your-app-name` (e.g., `goodvibes-backend`)

2. **(Optional)** Create an **Azure Key Vault**:
   - For secure storage of your API key in production
   - Name: `your-keyvault-name`

#### Step 2: Configure App Service

1. In Azure Portal, go to your App Service
2. Navigate to **Configuration** > **Application Settings**
3. Add a new setting:
   - Name: `OfficevibeApiKey`
   - Value: Your Workleap API key
   - Click **Save**

#### Step 3: Set Up GitHub Actions Deployment

The repository includes a pre-configured workflow at `.github/workflows/main_goodvibes-backend.yml`.

1. In Azure Portal, go to your App Service
2. Navigate to **Deployment Center**
3. Select **GitHub** as the source
4. Authorize GitHub
5. Select your repository and branch (`main`)
6. Azure will automatically configure the GitHub secrets needed for deployment

#### Step 4: Deploy

```bash
git push origin main
```

GitHub Actions will automatically:
1. Build your application
2. Run tests (if configured)
3. Deploy to Azure App Service

Monitor deployment progress at: https://github.com/your-username/your-repo/actions

### Verify Production Deployment

```bash
# Test health endpoint
curl https://your-app-name.azurewebsites.net/health

# Test API
curl "https://your-app-name.azurewebsites.net/api/good-vibes?isPublic=true&limit=5"
```

## Customization

### Adjusting Rate Limiting

If you experience rate limiting issues, adjust the delays in `Program.cs`:

```csharp
// Line 985: Delay between reply fetches (default: 100ms)
var delayBetweenRequests = TimeSpan.FromMilliseconds(200); // Increase to 200ms

// Line 976: Delay between pagination requests (default: 100ms)
await Task.Delay(TimeSpan.FromMilliseconds(200)); // Increase to 200ms
```

### Changing Cache Refresh Interval

Default is 5 minutes. To change it, edit `Program.cs` line 970:

```csharp
// Refresh every 10 minutes instead of 5
await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
```

### Customizing CORS

For production, restrict CORS to your frontend domain. Edit `Program.cs` line 24:

```csharp
policy.WithOrigins("https://your-frontend-domain.com")
      .AllowAnyMethod()
      .AllowAnyHeader();
```

### Adding Custom Endpoints

Add new endpoints in `Program.cs` after line 840. Example:

```csharp
app.MapGet("/api/custom-endpoint", async () =>
{
    return Results.Ok(new { message = "Custom endpoint" });
});
```

## Troubleshooting

### Issue: "Failed to fetch good vibes: 401"

**Cause**: Invalid or missing API key

**Solution**:
1. Verify your API key in `appsettings.json`
2. Ensure the key has proper permissions
3. Check if the key has expired

### Issue: "Failed to fetch good vibes: 429"

**Cause**: Rate limiting from Workleap API

**Solution**:
- The application should automatically retry
- If persistent, increase delays (see Customization section)
- Check if another instance is making requests with the same key

### Issue: Cache not loading ("cacheReady: false")

**Cause**: Cache is still initializing

**Solution**:
- Wait 10-15 seconds after startup
- Check application logs for errors
- Verify API key has access to Good Vibes data

### Issue: Avatars not loading

**Cause**: Missing user avatar data or API access

**Solution**:
1. Test with: `curl http://localhost:5000/api/debug/user-avatar/{userId}`
2. Check if users have uploaded avatars
3. Verify API key has access to User API
4. Review logs for specific error messages

### Issue: Application won't start

**Cause**: Port already in use

**Solution**:
```bash
# Find process using port 5000
lsof -i:5000

# Kill the process
kill -9 <PID>

# Or run on a different port
PORT=5001 dotnet run
```

### Issue: Build errors

**Solution**:
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

## Getting Help

### Check Logs

Development:
```bash
# Logs appear in console output
dotnet run
```

Production (Azure):
1. Go to Azure Portal > App Service
2. Navigate to **Log stream**
3. View real-time logs

### Common Log Messages

```
✓ Good message: "Cached 222 good vibes from 8 pages (50 with replies) - cache is ready"
  → Cache loaded successfully

⚠ Warning: "Rate limited during pagination, waiting before retry..."
  → Automatic rate limit handling in progress

✗ Error: "Failed to fetch good vibes: 401"
  → Check your API key configuration
```

### Additional Resources

- [Workleap API Documentation](https://developers.workleap.com)
- [.NET Documentation](https://docs.microsoft.com/dotnet)
- [Azure App Service Documentation](https://docs.microsoft.com/azure/app-service)

## Next Steps

Once your API is running:

1. **Build a Frontend**: Connect your React/Vue/Angular app to the API
2. **Implement Authentication**: Add user authentication if needed
3. **Monitor Performance**: Set up Application Insights in Azure
4. **Scale**: Upgrade App Service plan if traffic increases

## Security Best Practices

1. **Never commit API keys** to version control
2. **Use Azure Key Vault** for production secrets
3. **Restrict CORS** to your frontend domain only
4. **Enable HTTPS** in production (automatic with Azure App Service)
5. **Rotate API keys** regularly
6. **Monitor API usage** to detect anomalies

---

**Need more help?** Contact your Workleap support team or open an issue in this repository.
