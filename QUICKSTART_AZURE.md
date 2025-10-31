# Azure Deployment - Quick Start

## 🎯 What You Need to Do (4 Steps)

### Step 1: Add Environment Variable (2 minutes)
1. Go to https://portal.azure.com
2. Find your App Service: `goodvibes-backend`
3. Settings → Configuration → Application settings
4. Click **+ New application setting**
5. Name: `KeyVaultUrl`
6. Value: `https://productopsprodvault.vault.azure.net/`
7. Click **OK**, then **Save**

### Step 2: Get Publish Profile (1 minute)
1. In your App Service, click **Get publish profile** at the top
2. Download the file
3. Open it and copy ALL the contents

### Step 3: Add to GitHub (1 minute)
1. Go to https://github.com/MinhHoangDam/goodvibes-backend
2. Settings → Secrets and variables → Actions
3. Click **New repository secret**
4. Name: `AZURE_WEBAPP_PUBLISH_PROFILE`
5. Paste the publish profile contents
6. Click **Add secret**

### Step 4: Ready to Deploy!
Just tell me **"Deploy to Azure"** and I'll push the code!

---

## 🧪 Testing After Deployment

Once deployed, test this URL to validate Key Vault access:

```
https://goodvibes-backend-egdfa2amgua9gmbk.canadacentral-01.azurewebsites.net/api/validate-keyvault
```

**Expected result:**
```json
{
  "success": true,
  "apiKeySource": "WorkleapCOToken (Key Vault)",
  "apiKeyFound": true
}
```

If `success` is `false`, check the error message and contact IT.

---

## 📚 Full Guide

See [AZURE_SETUP_STEPS.md](AZURE_SETUP_STEPS.md) for detailed step-by-step instructions.

---

## ✅ What IT Already Set Up

- App Service created: `goodvibes-backend`
- System-assigned identity enabled
- Key Vault access granted
- Secret name: `WorkleapCOToken`

You're almost there! Just 4 quick steps and you're deployed.
