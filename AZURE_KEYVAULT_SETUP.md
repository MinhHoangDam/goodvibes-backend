# Azure Key Vault Setup with User-Assigned Managed Identity

This guide walks you through setting up Azure Key Vault with a user-assigned managed identity for secure API key management in production.

## Prerequisites

- Azure CLI installed (`az --version` to check)
- Azure subscription
- Permissions to create resources in Azure

## Step 1: Create Azure Resources

### 1.1 Login to Azure
```bash
az login
```

### 1.2 Set your subscription (if you have multiple)
```bash
az account list --output table
az account set --subscription "YOUR_SUBSCRIPTION_ID"
```

### 1.3 Create a Resource Group
```bash
az group create --name goodvibes-rg --location eastus
```

### 1.4 Create a User-Assigned Managed Identity
```bash
az identity create \
  --name goodvibes-identity \
  --resource-group goodvibes-rg \
  --location eastus
```

**Save the output!** You'll need:
- `clientId` - This is your `ManagedIdentityClientId`
- `principalId` - You'll use this to grant Key Vault access

### 1.5 Create Azure Key Vault
```bash
az keyvault create \
  --name goodvibes-keyvault \
  --resource-group goodvibes-rg \
  --location eastus
```

**Note:** Key Vault names must be globally unique. If `goodvibes-keyvault` is taken, try `goodvibes-keyvault-yourname` or similar.

### 1.6 Grant Managed Identity Access to Key Vault
```bash
# Get the principalId from step 1.4 output
PRINCIPAL_ID="YOUR_PRINCIPAL_ID_FROM_STEP_1.4"

az keyvault set-policy \
  --name goodvibes-keyvault \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

### 1.7 Add Your API Key to Key Vault
```bash
az keyvault secret set \
  --vault-name goodvibes-keyvault \
  --name "OfficevibeApiKey" \
  --value "YOUR_ACTUAL_API_KEY_HERE"
```

**Important:** The secret name `OfficevibeApiKey` must match exactly what's configured in the application code.

## Step 2: Configure Your Application

### 2.1 Update appsettings.Production.json
```json
{
  "KeyVaultUrl": "https://goodvibes-keyvault.vault.azure.net/",
  "ManagedIdentityClientId": "YOUR_CLIENT_ID_FROM_STEP_1.4"
}
```

### 2.2 Set Environment Variables (Azure App Service / Container)

When deploying to Azure App Service or Azure Container Apps, set these environment variables:

```bash
KeyVaultUrl=https://goodvibes-keyvault.vault.azure.net/
ManagedIdentityClientId=YOUR_CLIENT_ID_FROM_STEP_1.4
ASPNETCORE_ENVIRONMENT=Production
```

## Step 3: Assign Managed Identity to Your Azure Resource

### For Azure App Service:
```bash
az webapp identity assign \
  --name YOUR_APP_NAME \
  --resource-group goodvibes-rg \
  --identities /subscriptions/YOUR_SUBSCRIPTION_ID/resourcegroups/goodvibes-rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/goodvibes-identity
```

### For Azure Container Apps:
```bash
az containerapp identity assign \
  --name YOUR_CONTAINER_APP_NAME \
  --resource-group goodvibes-rg \
  --user-assigned /subscriptions/YOUR_SUBSCRIPTION_ID/resourcegroups/goodvibes-rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/goodvibes-identity
```

### For Railway (or other non-Azure platforms):
You'll need to use a different approach since managed identities only work on Azure. Consider:
- Using Railway's secret management
- Using Azure AD Service Principal with client credentials
- Environment variables (less secure)

## Step 4: Verify Configuration

### 4.1 Test Key Vault Access Locally (Optional)

For local development, you can use Azure CLI authentication:

```bash
az login
```

Then set environment variables:
```bash
export KeyVaultUrl="https://goodvibes-keyvault.vault.azure.net/"
export ASPNETCORE_ENVIRONMENT="Production"
```

Run your app:
```bash
dotnet run
```

You should see in the logs:
```
Configuring Key Vault: https://goodvibes-keyvault.vault.azure.net/
Using Officevibe API Key: 1dfdd5f5...
```

## Step 5: Deploy to Production

1. **Build and publish your app**
2. **Assign the managed identity** to your Azure resource (Step 3)
3. **Set environment variables** in your hosting platform
4. **Deploy and verify** the logs show Key Vault is being used

## Security Best Practices

1. **Never commit secrets to source control**
   - Add `appsettings.Production.json` to `.gitignore` if it contains real values
   - Use Key Vault for all production secrets

2. **Rotate API keys regularly**
   ```bash
   az keyvault secret set \
     --vault-name goodvibes-keyvault \
     --name "OfficevibeApiKey" \
     --value "NEW_API_KEY_HERE"
   ```

3. **Use least privilege access**
   - Only grant `get` and `list` permissions to the managed identity
   - Don't grant `set`, `delete`, or other administrative permissions

4. **Monitor Key Vault access**
   - Enable Azure Monitor diagnostics
   - Review access logs regularly

## Troubleshooting

### Error: "Access denied to Key Vault"
- Verify the managed identity is assigned to your Azure resource
- Check Key Vault access policies include your managed identity's principalId
- Ensure the `ManagedIdentityClientId` environment variable is correct

### Error: "Secret not found"
- Verify the secret name is exactly `OfficevibeApiKey` (case-sensitive)
- Check the secret exists: `az keyvault secret show --vault-name goodvibes-keyvault --name OfficevibeApiKey`

### App falls back to hardcoded API key
- Check `KeyVaultUrl` environment variable is set
- Verify `ASPNETCORE_ENVIRONMENT` is set to "Production"
- Review application logs for Key Vault configuration messages

## Local Development

For local development, the app will use the API key from `appsettings.json`. This allows you to develop without Azure access.

To test Key Vault integration locally:
1. Run `az login`
2. Set `KeyVaultUrl` environment variable
3. The app will use `DefaultAzureCredential` which tries Azure CLI credentials

## Environment Variables Summary

| Variable | Required | Description |
|----------|----------|-------------|
| `KeyVaultUrl` | Production only | Your Key Vault URL (e.g., `https://goodvibes-keyvault.vault.azure.net/`) |
| `ManagedIdentityClientId` | Production only | Client ID of your user-assigned managed identity |
| `ASPNETCORE_ENVIRONMENT` | Yes | Set to `Production` for production deployment |
| `OfficevibeApiKey` | Local dev only | API key for local development (not used in production) |

## Key Vault Secret Names

The following secrets should be stored in Key Vault:

| Secret Name | Description |
|-------------|-------------|
| `OfficevibeApiKey` | Officevibe/Workleap API subscription key |

**Note:** Key Vault automatically converts secret names with hyphens to double underscores in configuration. Use the exact names shown above.
