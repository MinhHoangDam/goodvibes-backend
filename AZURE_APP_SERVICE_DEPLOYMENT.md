# Azure App Service Deployment Guide

This guide will help you deploy your Good Vibes backend to Azure App Service with GitHub Actions CI/CD.

## Prerequisites

- Azure subscription (corporate account)
- GitHub repository with your code
- Azure CLI installed (optional, for command-line setup)

---

## Part 1: Create Azure App Service

### Option A: Using Azure Portal (Recommended for beginners)

1. **Login to Azure Portal**
   - Go to https://portal.azure.com
   - Sign in with your corporate account

2. **Create a Resource Group** (if you don't have one)
   - Click "Resource groups" in the left menu
   - Click "+ Create"
   - Fill in:
     - **Subscription**: Your corporate subscription
     - **Resource group**: `goodvibes-rg` (or your preferred name)
     - **Region**: Choose closest to your users (e.g., `East US`, `West Europe`)
   - Click "Review + create" → "Create"

3. **Create App Service**
   - Click "+ Create a resource"
   - Search for "Web App" and click "Create"
   - Fill in the basics:
     - **Subscription**: Your corporate subscription
     - **Resource Group**: Select `goodvibes-rg`
     - **Name**: `goodvibes-backend` (must be globally unique, try variations if taken)
     - **Publish**: `Code`
     - **Runtime stack**: `.NET 9 (STS)`
     - **Operating System**: `Linux` (cheaper) or `Windows`
     - **Region**: Same as your resource group

   - **App Service Plan** (Pricing):
     - Click "Create new"
     - **Name**: `goodvibes-plan`
     - **Pricing plan**:
       - **Development**: `B1 Basic` (~$13/month) - Good for testing
       - **Production**: `S1 Standard` (~$70/month) - Auto-scaling, staging slots
       - **Note**: Free tier (F1) is very limited, similar to Render free
     - Click "OK"

   - Click "Review + create" → "Create"
   - Wait for deployment to complete (~2 minutes)

4. **Get your App Service URL**
   - Go to your new App Service: `goodvibes-backend`
   - The URL will be: `https://goodvibes-backend.azurewebsites.net`
   - Save this URL - you'll need it for your frontend!

### Option B: Using Azure CLI (Faster)

```bash
# Login to Azure
az login

# Set your subscription (if you have multiple)
az account list --output table
az account set --subscription "YOUR_SUBSCRIPTION_NAME"

# Create resource group
az group create --name goodvibes-rg --location eastus

# Create App Service Plan
az appservice plan create \
  --name goodvibes-plan \
  --resource-group goodvibes-rg \
  --sku B1 \
  --is-linux

# Create Web App
az webapp create \
  --name goodvibes-backend \
  --resource-group goodvibes-rg \
  --plan goodvibes-plan \
  --runtime "DOTNET:9.0"

# Configure for .NET 9
az webapp config set \
  --name goodvibes-backend \
  --resource-group goodvibes-rg \
  --linux-fx-version "DOTNET|9.0"
```

---

## Part 2: Configure User-Assigned Managed Identity

This allows your app to securely access Key Vault without storing credentials.

### Using Azure Portal:

1. **Create Managed Identity** (if you haven't already from Key Vault setup)
   - Go to "Managed Identities" in Azure Portal
   - Click "+ Create"
   - Fill in:
     - **Subscription**: Your subscription
     - **Resource group**: `goodvibes-rg`
     - **Region**: Same as your app
     - **Name**: `goodvibes-identity`
   - Click "Review + create" → "Create"
   - Once created, click on it and **copy the Client ID** (you'll need this later)

2. **Assign Managed Identity to App Service**
   - Go to your App Service: `goodvibes-backend`
   - Click "Identity" in the left menu
   - Click "User assigned" tab
   - Click "+ Add"
   - Select `goodvibes-identity`
   - Click "Add"

### Using Azure CLI:

```bash
# Create managed identity (if not already created)
az identity create \
  --name goodvibes-identity \
  --resource-group goodvibes-rg

# Get the identity resource ID
IDENTITY_ID=$(az identity show \
  --name goodvibes-identity \
  --resource-group goodvibes-rg \
  --query id -o tsv)

# Get the Client ID (you'll need this for environment variables)
CLIENT_ID=$(az identity show \
  --name goodvibes-identity \
  --resource-group goodvibes-rg \
  --query clientId -o tsv)

echo "Client ID: $CLIENT_ID"

# Assign identity to App Service
az webapp identity assign \
  --name goodvibes-backend \
  --resource-group goodvibes-rg \
  --identities $IDENTITY_ID
```

---

## Part 3: Configure Environment Variables

Your app needs these environment variables to work correctly.

### Using Azure Portal:

1. Go to your App Service: `goodvibes-backend`
2. Click "Configuration" in the left menu (under Settings)
3. Click "Application settings" tab
4. Add these settings by clicking "+ New application setting":

| Name | Value | Notes |
|------|-------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Tells app it's in production |
| `KeyVaultUrl` | `https://your-keyvault-name.vault.azure.net/` | From IT department |
| `ManagedIdentityClientId` | `<client-id-from-step-2>` | From managed identity |
| `PORT` | `8080` | App Service uses 8080 by default |

5. Click "Save" at the top
6. Click "Continue" to confirm restart

### Using Azure CLI:

```bash
# Set environment variables
az webapp config appsettings set \
  --name goodvibes-backend \
  --resource-group goodvibes-rg \
  --settings \
    ASPNETCORE_ENVIRONMENT="Production" \
    KeyVaultUrl="https://your-keyvault-name.vault.azure.net/" \
    ManagedIdentityClientId="$CLIENT_ID" \
    PORT="8080"
```

---

## Part 4: Set Up GitHub Actions Deployment

### Step 1: Get Publish Profile from Azure

**Using Azure Portal:**
1. Go to your App Service: `goodvibes-backend`
2. Click "Overview" in the left menu
3. Click "Get publish profile" at the top (download button)
4. This downloads a `.publishsettings` XML file
5. Open the file and **copy all the contents**

**Using Azure CLI:**
```bash
az webapp deployment list-publishing-profiles \
  --name goodvibes-backend \
  --resource-group goodvibes-rg \
  --xml
```

### Step 2: Add Secret to GitHub

1. Go to your GitHub repository
2. Click "Settings" tab
3. Click "Secrets and variables" → "Actions" in the left menu
4. Click "New repository secret"
5. Fill in:
   - **Name**: `AZURE_WEBAPP_PUBLISH_PROFILE`
   - **Secret**: Paste the entire publish profile XML
6. Click "Add secret"

### Step 3: Update Workflow File

The GitHub Actions workflow has already been created at `.github/workflows/azure-deploy.yml`.

**Important**: Update the `AZURE_WEBAPP_NAME` in the workflow file to match your App Service name:

```yaml
env:
  AZURE_WEBAPP_NAME: 'goodvibes-backend'  # Change if you used a different name
```

### Step 4: Commit and Push

```bash
git add .github/workflows/azure-deploy.yml
git add AZURE_APP_SERVICE_DEPLOYMENT.md
git commit -m "Add Azure App Service deployment workflow"
git push origin main
```

### Step 5: Monitor Deployment

1. Go to your GitHub repository
2. Click "Actions" tab
3. You should see a workflow running
4. Click on it to see the deployment progress
5. Wait for it to complete (~2-3 minutes)

---

## Part 5: Verify Deployment

### Test Your Backend

1. **Health Check**
   ```bash
   curl https://goodvibes-backend.azurewebsites.net/health
   ```
   Should return: `{"status":"ok","message":"Server is running"}`

2. **Check Debug Endpoint**
   ```bash
   curl https://goodvibes-backend.azurewebsites.net/debug
   ```
   Should show environment info

3. **Test API**
   ```bash
   curl https://goodvibes-backend.azurewebsites.net/api/good-vibes/cached
   ```
   Should return good vibes data (once Key Vault is configured)

### View Logs

**Using Azure Portal:**
1. Go to your App Service
2. Click "Log stream" in the left menu (under Monitoring)
3. Watch logs in real-time

**Using Azure CLI:**
```bash
az webapp log tail \
  --name goodvibes-backend \
  --resource-group goodvibes-rg
```

---

## Part 6: Update Your Frontend

Once your backend is deployed, update your frontend to use the new Azure URL:

**Edit `.env.production` in your frontend:**
```
REACT_APP_API_BASE_URL=https://goodvibes-backend.azurewebsites.net
```

**For local development** (keep `.env` or `.env.development` as is):
```
REACT_APP_API_BASE_URL=http://localhost:5000
```

---

## Part 7: Enable CORS (Important!)

Your frontend needs permission to call your backend from a different domain.

### Using Azure Portal:

1. Go to your App Service: `goodvibes-backend`
2. Click "CORS" in the left menu (under API)
3. Add your frontend URLs:
   - For local development: `http://localhost:3000`
   - For production frontend: `https://your-frontend-url.com`
   - Or use `*` for all origins (less secure, but okay for testing)
4. Click "Save"

**Note**: Your backend code already has CORS configured with `AllowAll` policy, so this should work automatically. But you can restrict it here for extra security.

---

## Part 8: Custom Domain (Optional)

If you want to use a custom domain like `api.goodvibes.com`:

1. Go to your App Service
2. Click "Custom domains" in the left menu
3. Click "+ Add custom domain"
4. Follow the wizard to verify your domain ownership
5. Azure will provide you with DNS records to add to your domain registrar

---

## Troubleshooting

### App won't start / HTTP 500 errors

**Check logs:**
```bash
az webapp log tail --name goodvibes-backend --resource-group goodvibes-rg
```

**Common issues:**
- Missing environment variables (check Configuration)
- Key Vault access denied (check managed identity has permissions)
- Wrong .NET version (should be 9.0)

### "Address already in use" error

Your code listens on port 5000 locally but Azure uses port 8080. Make sure:
- Environment variable `PORT=8080` is set in Azure
- Your code reads `Environment.GetEnvironmentVariable("PORT")`

### Key Vault access denied

Make sure:
1. Managed identity is assigned to App Service (check Identity tab)
2. Managed identity has Key Vault access policy (check Key Vault → Access policies)
3. Environment variables are set correctly (KeyVaultUrl and ManagedIdentityClientId)

### Slow cold starts

App Service can go to sleep after 20 minutes of inactivity. Options:
- Upgrade to Standard tier (S1) and enable "Always On" in Configuration → General settings
- Use Application Insights to keep it warm
- Accept the cold start (usually <5 seconds)

---

## Monitoring & Optimization

### Enable Application Insights (Recommended)

1. Go to your App Service
2. Click "Application Insights" in the left menu
3. Click "Turn on Application Insights"
4. Click "Apply" → "Yes"

This gives you:
- Performance monitoring
- Error tracking
- Request analytics
- Dependency tracking

### Enable Always On (Prevents Sleep)

1. Go to your App Service
2. Click "Configuration" in the left menu
3. Click "General settings" tab
4. Set "Always On" to `On`
5. Click "Save"

**Note**: Always On requires Basic tier (B1) or higher

### Auto-scaling (Standard Tier+)

1. Go to your App Service Plan: `goodvibes-plan`
2. Click "Scale out (App Service plan)" in the left menu
3. Configure rules based on CPU, memory, or custom metrics

---

## Cost Estimation

Based on Azure pricing (may vary by region):

| Tier | Price/Month | Features | Best For |
|------|-------------|----------|----------|
| **F1 Free** | $0 | 60 min/day, 1GB RAM, no Always On | Testing only |
| **B1 Basic** | ~$13 | 100% uptime, 1.75GB RAM, Always On | Small production apps |
| **S1 Standard** | ~$70 | Auto-scaling, staging slots, backups | Production apps |
| **P1V2 Premium** | ~$100 | Better performance, VNet integration | High-traffic apps |

**Additional costs:**
- Key Vault: ~$0.03/10,000 operations (basically free)
- Managed Identity: Free
- Bandwidth: First 100GB free, then ~$0.087/GB

**Your corporate plan may include:**
- Free credits
- Discounted pricing
- Reserved instances

---

## Next Steps

1. ✅ Deploy to Azure App Service
2. ✅ Set up managed identity
3. ⏳ Wait for IT to grant Key Vault access (from your earlier request)
4. ✅ Test the deployment
5. ✅ Update frontend URL
6. 🎉 Go live!

---

## Useful Azure CLI Commands

```bash
# View app logs
az webapp log tail --name goodvibes-backend --resource-group goodvibes-rg

# Restart app
az webapp restart --name goodvibes-backend --resource-group goodvibes-rg

# View app settings
az webapp config appsettings list --name goodvibes-backend --resource-group goodvibes-rg

# Update app setting
az webapp config appsettings set --name goodvibes-backend --resource-group goodvibes-rg --settings KEY=VALUE

# View managed identities
az webapp identity show --name goodvibes-backend --resource-group goodvibes-rg

# View deployment logs
az webapp log deployment show --name goodvibes-backend --resource-group goodvibes-rg

# SSH into container (Linux only)
az webapp ssh --name goodvibes-backend --resource-group goodvibes-rg
```

---

## Support & Resources

- **Azure Documentation**: https://docs.microsoft.com/azure/app-service/
- **.NET on Azure**: https://docs.microsoft.com/aspnet/core/host-and-deploy/azure-apps/
- **GitHub Actions for Azure**: https://github.com/Azure/actions
- **Azure Pricing Calculator**: https://azure.microsoft.com/pricing/calculator/

---

## Questions?

If you run into any issues:
1. Check the troubleshooting section above
2. View your App Service logs in Azure Portal
3. Check GitHub Actions logs for deployment errors
4. Verify all environment variables are set correctly
