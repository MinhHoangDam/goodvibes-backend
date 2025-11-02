# Strategies to Keep Azure App Service Warm

Your backend is deployed on Azure App Service at: `https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net`

## Problem
Azure App Services can "sleep" after periods of inactivity (typically 20 minutes), causing:
- Slow first requests (cold start: 10-30 seconds)
- Cache being rebuilt on every wake-up
- Poor user experience for first visitors

## Solutions

### 1. Enable "Always On" (Recommended - Easiest)

**Requirements:** Basic (B1) tier or higher

**Steps:**
1. Go to Azure Portal → goodvibes-backend
2. Settings → Configuration → General settings
3. Toggle "Always On" to **On**
4. Click "Save"

**Benefits:**
- ✅ Simplest solution
- ✅ No code changes needed
- ✅ Keeps your app loaded in memory
- ✅ Prevents automatic unloading

**Cost:** Small additional cost (typically included in Basic tier and above)

---

### 2. Azure Health Check Monitoring (Built-in Feature)

Your backend already has a health endpoint at `/health`. Configure Azure to monitor it:

**Steps:**
1. Go to Azure Portal → goodvibes-backend
2. Monitoring → Health check
3. Enable health check
4. Set path: `/health`
5. Set interval: 5 minutes (or less)
6. Save

**What it does:**
- Azure pings your `/health` endpoint every 5 minutes
- Automatically routes traffic away if unhealthy
- Keeps the app warm as a side effect

**Benefits:**
- ✅ Built into Azure App Service
- ✅ Also provides monitoring/alerting
- ✅ Free feature (no additional cost)

---

### 3. External Monitoring Service (Most Reliable)

Use an external service to ping your backend regularly.

#### Option A: UptimeRobot (Free tier available)
1. Sign up at https://uptimerobot.com
2. Add Monitor:
   - Type: HTTP(s)
   - URL: `https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net/health`
   - Interval: 5 minutes
3. Set up email alerts (optional)

#### Option B: Azure Application Insights (Already included)
1. Go to Azure Portal → goodvibes-backend
2. Monitoring → Application Insights
3. Availability → Add test
4. Test type: URL ping test
5. URL: `https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net/health`
6. Test frequency: 5 minutes

#### Option C: Simple Cron Job / GitHub Actions
Create a GitHub Action that pings your backend every 5 minutes:

```yaml
# .github/workflows/keep-warm.yml
name: Keep Backend Warm

on:
  schedule:
    - cron: '*/5 * * * *'  # Every 5 minutes
  workflow_dispatch:  # Allow manual trigger

jobs:
  ping:
    runs-on: ubuntu-latest
    steps:
      - name: Ping health endpoint
        run: curl -f https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net/health
```

**Benefits:**
- ✅ Free (GitHub Actions has generous free tier)
- ✅ Reliable
- ✅ Easy to set up
- ✅ Can combine with other checks

**Limitations:**
- GitHub Actions cron can have delays (up to 15 minutes)
- May not run exactly every 5 minutes

---

### 4. Warm Up the Cache Proactively

Your backend has a caching system. You can add a warmup endpoint that pre-loads the cache:

**Create a `/warmup` endpoint:**
```csharp
app.MapGet("/warmup", async (GoodVibesCacheService cacheService) =>
{
    Console.WriteLine("🔥 Warming up cache...");

    // Trigger cache refresh
    await cacheService.RefreshCacheAsync();

    return Results.Ok(new { status = "warmed", message = "Cache has been refreshed" });
});
```

Then configure Azure Health Check to call `/warmup` instead of `/health`.

---

## Recommended Approach

### For Production (Best Performance):
1. **Enable "Always On"** in Azure Portal (5 minutes to set up)
2. **Enable Azure Health Check** pointing to `/health` (backup)
3. **Optional:** Set up Application Insights availability test

### For Free/Basic Tier:
1. **Use UptimeRobot** or similar free monitoring service
2. **Or:** Set up GitHub Actions workflow (if you need it to be free)

---

## Testing

Test if your backend is responding quickly:

```bash
# Test health endpoint
time curl https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net/health

# Test cached endpoint (should be fast if warm)
time curl https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net/api/good-vibes/cached?daysBack=30
```

If the first request is slow (>5 seconds), your app is cold starting.

---

## Current Status

✅ Health endpoint available at: `/health`
✅ "Always On" enabled in Azure Portal
✅ Azure Health Check enabled and monitoring `/health`
✅ UptimeRobot configured to ping every 5 minutes
✅ Backend confirmed warm with <1 second response times!
✅ HEAD request support added for UptimeRobot compatibility

**Performance Results:**
- Health endpoint: 0.34 seconds (was 71 seconds)
- API endpoint: 2.6 seconds (was 18+ seconds)

**UptimeRobot Compatibility:**
- Supports GET, POST, and HEAD HTTP methods
- UptimeRobot free tier uses HEAD requests - now fully supported!

---

## Quick Start: Enable "Always On" Now

This is the fastest way to solve the problem:

1. Open [Azure Portal](https://portal.azure.com)
2. Navigate to your App Service: `goodvibes-backend`
3. Settings → Configuration → General settings
4. Find "Always On" toggle
5. Switch to **On**
6. Click **Save** at the top
7. Wait ~1 minute for changes to apply

Done! Your backend will now stay warm 24/7.
