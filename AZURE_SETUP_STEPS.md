# Azure App Service Setup - Step-by-Step Guide

Your IT department has created the App Service and configured everything! This guide will help you deploy.

---

## ✅ What IT Already Set Up

- **App Service**: `goodvibes-backend`
- **URL**: `goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net`
- **Resource Group**: `temp-minh-feng-1401`
- **Region**: Canada Central
- **Managed Identity**: System-assigned identity (enabled)
- **Key Vault**: `productopsprodvault` with access granted to the system identity
- **Secret Name**: `WorkleapCOToken`

---

## 🎉 Great News!

Since IT set up the **system-assigned identity** and granted Key Vault access, the setup is much simpler!
You only need to add ONE environment variable and deploy.

---

## 🔧 Step 1: Verify System-Assigned Identity (Optional Check)

Your IT department should have already enabled this, but you can verify:

### In Azure Portal:

1. Go to https://portal.azure.com
2. Search for "goodvibes-backend"
3. Click on your App Service
4. In the left sidebar: **Settings** → **Identity**
5. Click on the **System assigned** tab
6. **Status** should show: **On**

✅ If it shows "On", you're good to go!

---

## ⚙️ Step 2: Add Environment Variable

With system-assigned identity, you only need ONE environment variable!

### In Azure Portal:

1. In your App Service (goodvibes-backend)
2. Left sidebar: **Settings** → **Configuration**
3. Click on **Application settings** tab
4. Click **+ New application setting**

### Add This ONE Setting:

| Name | Value |
|------|-------|
| `KeyVaultUrl` | `https://productopsprodvault.vault.azure.net/` |

### Steps:
1. Name: `KeyVaultUrl`
2. Value: `https://productopsprodvault.vault.azure.net/`
3. Click **OK**
4. **IMPORTANT**: Click **Save** at the top
5. Click **Continue** when prompted about restart

That's it! System-assigned identity handles authentication automatically.

---

## 📦 Step 3: Get Publish Profile for GitHub

GitHub Actions needs credentials to deploy to your App Service.

### In Azure Portal:

1. Still in your App Service (goodvibes-backend)
2. At the top of the page, click **Get publish profile**
3. A file will download: `goodvibes-backend.PublishSettings`
4. Open this file in a text editor (Notepad, VS Code, etc.)
5. **Copy the entire contents** of this file

---

## 🔐 Step 4: Add Publish Profile to GitHub

Now we need to add this as a secret in your GitHub repository.

### In GitHub:

1. Go to https://github.com/MinhHoangDam/goodvibes-backend
2. Click on **Settings** (top right tabs)
3. In the left sidebar, click **Secrets and variables** → **Actions**
4. Click **New repository secret**
5. For **Name**, enter: `AZURE_WEBAPP_PUBLISH_PROFILE`
6. For **Secret**, paste the entire contents of the publish profile file
7. Click **Add secret**

---

## 🚀 Step 5: Commit and Deploy

Now let's commit the code changes and trigger the deployment.

### Option A: I'll do it for you
Just say "Ready to deploy" and I'll commit the changes and push to GitHub.

### Option B: Manual steps
```bash
git add .
git commit -m "Configure Azure Key Vault with WorkleapCOToken secret"
git push origin main
```

This will trigger the GitHub Actions workflow and deploy to Azure!

---

## 🔍 Step 6: Monitor the Deployment

### Watch GitHub Actions:

1. Go to https://github.com/MinhHoangDam/goodvibes-backend/actions
2. You'll see a workflow running: "Deploy to Azure App Service"
3. Click on it to see the progress
4. Wait for it to complete (usually 2-3 minutes)

### Check Azure Logs:

1. In Azure Portal, go to your App Service
2. In the left sidebar, go to **Monitoring** → **Log stream**
3. You'll see real-time logs as your app starts up
4. Look for messages like:
   - "🔐 Configuring Key Vault: https://productopsprodvault.vault.azure.net/"
   - "✓ Using System-Assigned Managed Identity in Azure"
   - "✓ Key Vault configuration completed"
   - "Server is running on http://localhost:8080"

---

## ✅ Step 7: Test Your Deployment

Once deployment completes, test these URLs in your browser:

1. **🔐 KEY VAULT VALIDATION (TEST THIS FIRST!)**:
   ```
   https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net/api/validate-keyvault
   ```
   **Expected response:**
   ```json
   {
     "success": true,
     "keyVaultConfigured": true,
     "keyVaultUrl": "https://productopsprodvault.vault.azure.net/",
     "apiKeySource": "WorkleapCOToken (Key Vault)",
     "apiKeyFound": true,
     "apiKeyPreview": "1dfdd5f5...",
     "environment": "Production",
     "timestamp": "2025-10-31T..."
   }
   ```

   **If success = false**, check the error message and:
   - Verify KeyVaultUrl environment variable is set
   - Ask IT to confirm the system identity has Key Vault access
   - Check Azure Log Stream for detailed errors

2. **Health Check**:
   ```
   https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net/health
   ```
   Should return: `{"status":"ok","message":"Server is running"}`

3. **Cached Good Vibes** (fast endpoint):
   ```
   https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net/api/good-vibes/cached
   ```
   Should return JSON with good vibes data

4. **Stats**:
   ```
   https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net/api/stats
   ```
   Should return statistics about good vibes

---

## 🐛 Troubleshooting

### If deployment fails:

1. **Check GitHub Actions logs** for build errors
2. **Check Azure Log Stream** for runtime errors
3. **Verify environment variables** are set correctly
4. **Verify managed identity** is assigned

### Common Issues:

**Issue**: "Failed to load configuration from Key Vault"
- **Fix**: Check that system-assigned identity is enabled (Status = On)
- **Fix**: Verify KeyVaultUrl environment variable is set correctly

**Issue**: "Unauthorized to access Key Vault" or "403 Forbidden"
- **Fix**: Ask IT to verify the system-assigned identity has "Get" and "List" permissions on the Key Vault
- **Fix**: Provide IT with the Object ID from the Identity page

**Issue**: "Application fails to start"
- **Fix**: Check Log Stream for error messages
- **Fix**: Verify KeyVaultUrl environment variable is set

**Issue**: `/api/validate-keyvault` returns `success: false`
- **Fix**: Check the error message in the response
- **Fix**: Most likely Key Vault permissions issue - contact IT with the error details

---

## 📊 Current Status Checklist

Use this to track your progress:

- [ ] Step 1: Verified system-assigned identity is enabled
- [ ] Step 2: Added KeyVaultUrl environment variable
- [ ] Step 3: Downloaded publish profile
- [ ] Step 4: Added publish profile to GitHub secrets
- [ ] Step 5: Committed and pushed code
- [ ] Step 6: GitHub Actions workflow completed successfully
- [ ] Step 7: Tested `/api/validate-keyvault` endpoint - success = true ✅
- [ ] Step 7: Tested health endpoint - working
- [ ] Step 7: Tested cached endpoint - working

---

## 🎯 Next Steps After Azure is Working

1. Update frontend to use Azure URL
2. Test full application end-to-end
3. Decide whether to keep Render or shut it down
4. Set up monitoring and alerts in Azure

---

## Need Help?

If you get stuck at any step, let me know:
- Which step you're on
- What error message you see (if any)
- Screenshot if helpful

I'm here to help troubleshoot!
