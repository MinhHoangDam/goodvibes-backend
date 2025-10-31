# Azure Production Configuration

## Information from IT Department

### App Service
- **Name**: goodvibes-backend
- **URL**: https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net
- **Resource Group**: temp-minh-feng-1401
- **Region**: Canada Central

### User-Assigned Managed Identity
- **Name**: goodvibes-carousell-app-identity
- **Client ID**: 852d3fc6-436b-439e-a419-b61d0ddf8aca
- **Object (Principal) ID**: 7a60f4f6-6e79-4e5f-ac76-d9b2160fc220

### Key Vault
- **URL**: https://productopsprodvault.vault.azure.net/
- **Secret Name**: WorkleapCOToken

---

## Required Environment Variables for Azure App Service

Once your App Service is created, these environment variables must be configured:

```bash
ASPNETCORE_ENVIRONMENT=Production
KeyVaultUrl=https://productopsprodvault.vault.azure.net/
ManagedIdentityClientId=852d3fc6-436b-439e-a419-b61d0ddf8aca
PORT=8080
```

---

## App Service Configuration Needed

### Basic Settings
- **Name**: goodvibes-backend (or available alternative)
- **Runtime Stack**: .NET 9 (STS)
- **Operating System**: Linux
- **Region**: Your choice (e.g., East US, West US 2, etc.)
- **Pricing Tier**: B1 (Basic) minimum, S1 (Standard) recommended for production

### Identity Configuration
1. Go to App Service → Settings → Identity
2. Click "User assigned" tab
3. Click "Add"
4. Select: goodvibes-carousell-app-identity
5. Save

### Required Permissions for You
- **Contributor** access to the App Service (to deploy code and manage settings)

---

## Pending Questions for IT

1. **Secret Name**: What is the name of the secret in the Key Vault that contains the Officevibe API key?
   - Our code will look for: `Configuration["OfficevibeApiKey"]`
   - This maps to Key Vault secret name (usually the same or similar)

2. **App Service Creation**: Can IT create the App Service with the above settings, or can they grant me permissions to create it in a specific Resource Group?

3. **Key Vault Access**: Has the managed identity `goodvibes-carousell-app-identity` been granted "Get" and "List" permissions on the Key Vault secrets?

---

## Testing Locally (Optional)

To test Key Vault access from your local machine:

### Prerequisites
- Azure CLI installed
- Logged in with: `az login`

### Test Access
```bash
# Login to Azure
az login

# Test Key Vault access
az keyvault secret show --vault-name productopsprodvault --name [SECRET_NAME]
```

If this works, you have access to test locally!

### Local Development Configuration

Update `appsettings.Development.json` to use Key Vault for local testing:

```json
{
  "KeyVaultUrl": "https://productopsprodvault.vault.azure.net/",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

When running locally, the app will use `DefaultAzureCredential` which will use your Azure CLI login.

---

## Deployment Flow

Once App Service is created:

1. **GitHub Actions** will automatically deploy when you push to `main` branch
2. The workflow file is already set up: `.github/workflows/azure-deploy.yml`
3. You'll need to update the workflow with:
   - Your actual App Service name
   - Download and add the Publish Profile as a GitHub secret

See `AZURE_APP_SERVICE_DEPLOYMENT.md` for detailed deployment instructions.

---

## Current Status

- ✅ Managed Identity created by IT
- ✅ Key Vault set up by IT
- ✅ Backend code configured for Key Vault
- ✅ GitHub Actions workflow ready
- ⏳ Pending: Secret name from IT
- ⏳ Pending: App Service creation
- ⏳ Pending: Verify Key Vault permissions

---

## Next Steps

1. Ask IT for the secret name
2. Ask IT to create App Service OR grant you permissions
3. Verify Key Vault permissions are set
4. Test deployment
5. Update frontend to use new Azure backend URL
