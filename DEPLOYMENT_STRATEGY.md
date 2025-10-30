# Dual Deployment Strategy: Render + Azure

## Overview

We're running both Render and Azure deployments in parallel:
- **Render**: Current production, simple deployment, immediate availability
- **Azure**: Future production with Key Vault integration, waiting for IT setup

---

## Current Status

### ✅ Render Deployment (Active)

**Status**: Live and working
**URL**: [Your Render URL - e.g., https://goodvibes-backend.onrender.com]
**Deployment**: Auto-deploys from GitHub `main` branch

**Environment Variables Set in Render**:
```
ASPNETCORE_ENVIRONMENT=Production
OfficevibeApiKey=1dfdd5f52b3c4829adc15e794485eea6
PORT=5000 (or auto-set by Render)
```

**Pros**:
- ✅ Working now
- ✅ Simple setup
- ✅ Free tier available
- ✅ Auto-deploys from GitHub
- ✅ HTTPS included

**Cons**:
- ⚠️ Cold starts on free tier
- ⚠️ API key stored in Render (not company Key Vault)
- ⚠️ Limited resources on free tier

---

### ⏳ Azure Deployment (In Progress)

**Status**: Waiting for IT to create App Service
**URL**: TBD (will be https://[app-name].azurewebsites.net)
**Deployment**: GitHub Actions ready, needs App Service setup

**IT-Provided Details**:
- Managed Identity: goodvibes-carousell-app-identity
- Client ID: 852d3fc6-436b-439e-a419-b61d0ddf8aca
- Key Vault: https://productopsprodvault.vault.azure.net/

**Pending from IT**:
1. Secret name in Key Vault
2. App Service creation OR resource group permissions
3. Confirmation of Key Vault access permissions

**Pros**:
- ✅ Corporate Key Vault integration
- ✅ Better performance (no cold starts on paid tiers)
- ✅ Corporate compliance
- ✅ Managed Identity (no stored credentials)
- ✅ More resources available

**Cons**:
- ⚠️ Requires IT coordination
- ⚠️ More complex setup
- ⚠️ Cost ($13-69/month minimum)

---

## Migration Plan

### Phase 1: Development (Current)
- **Backend**: Localhost on port 5000
- **Frontend**: Localhost on port 3000
- **API Key**: From appsettings.json

### Phase 2: Render Production (Current)
- **Backend**: Render deployment
- **Frontend**: Points to Render URL
- **API Key**: Environment variable in Render

### Phase 3: Azure Setup (In Progress)
1. ✅ Code is ready for Azure Key Vault
2. ⏳ Waiting for IT to create App Service
3. ⏳ Configure App Service environment variables
4. ⏳ Set up GitHub Actions deployment
5. ⏳ Test Azure deployment

### Phase 4: Parallel Running
- **Render**: Keep running as backup
- **Azure**: Deploy and test
- **Frontend**: Still points to Render
- **Purpose**: Verify Azure works before switching

### Phase 5: Full Azure Migration
- **Frontend**: Update to point to Azure URL
- **Render**: Demote to backup or shut down
- **Azure**: Primary production

---

## How to Switch Frontend Between Deployments

### For Render Backend:
```bash
# In frontend .env.production file:
REACT_APP_API_BASE_URL=https://your-app.onrender.com
```

### For Azure Backend:
```bash
# In frontend .env.production file:
REACT_APP_API_BASE_URL=https://goodvibes-backend.azurewebsites.net
```

---

## GitHub Actions Workflow

Already configured in `.github/workflows/azure-deploy.yml`

**To Enable**:
1. Update workflow with actual App Service name
2. Get publish profile from Azure
3. Add as GitHub secret: `AZURE_WEBAPP_PUBLISH_PROFILE`
4. Push to `main` branch triggers deployment

**To Disable** (temporarily):
- Comment out the workflow file
- Or add `if: false` to the job

---

## Cost Comparison

| Platform | Tier | Monthly Cost | Cold Starts | Resources |
|----------|------|--------------|-------------|-----------|
| Render Free | Free | $0 | Yes (15 min) | 512MB RAM |
| Render Starter | Paid | $7 | No | 512MB RAM |
| Azure B1 | Basic | ~$13 | No | 1.75GB RAM |
| Azure S1 | Standard | ~$69 | No | 1.75GB RAM, better SLA |

---

## Monitoring Both Deployments

### Render Monitoring:
- Dashboard: https://dashboard.render.com
- Logs: Available in dashboard
- Metrics: Basic metrics in dashboard

### Azure Monitoring:
- Portal: https://portal.azure.com
- Logs: Log Stream in App Service
- Metrics: Application Insights (optional)
- Alerts: Can set up email/SMS alerts

---

## When to Shut Down Render

Only after confirming:
- ✅ Azure deployment is stable
- ✅ Frontend points to Azure and works
- ✅ Key Vault access works
- ✅ Performance is acceptable
- ✅ At least 1-2 weeks of Azure uptime with no issues

---

## Rollback Plan

If Azure has problems:
1. Update frontend `.env.production` back to Render URL
2. Redeploy frontend
3. Render should still be running
4. Troubleshoot Azure without downtime

---

## Questions to Ask IT (Send Email)

```
Subject: Azure App Service Setup - Follow-up Questions

Hi [IT Team],

Thank you for the managed identity setup! To complete our dual deployment strategy, I need a few more details:

1. **Key Vault Secret Name**: What is the name of the secret in productopsprodvault that contains the Officevibe API key?

2. **App Service**: Can you either:
   - Create an Azure App Service with the specs in my previous email, OR
   - Grant me permissions to create one in a specific resource group?

3. **Key Vault Permissions**: Has goodvibes-carousell-app-identity been granted "Get" and "List" permissions on the vault?

Our plan is to run Render and Azure in parallel during migration to ensure zero downtime.

Thank you!
```

---

## Current Action Items

**Your Tasks**:
- ✅ Code is ready for both deployments
- ✅ Documentation created
- ⏳ Send follow-up email to IT
- ⏳ Keep Render deployment healthy

**IT Tasks**:
- ⏳ Provide secret name
- ⏳ Create App Service or grant permissions
- ⏳ Confirm Key Vault access

**Next Steps After IT Response**:
1. Configure Azure App Service environment variables
2. Update GitHub Actions workflow with App Service name
3. Test Azure deployment
4. Run both in parallel
5. Gradually migrate traffic to Azure
