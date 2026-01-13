# Zoho Token Generation - Step by Step Guide

## Problem
Error Code 57: "You are not authorized to perform this operation"

## Solution: Generate New Token Correctly

### Step 1: Go to Zoho API Console
1. Open: https://api-console.zoho.com/
2. Login with: ramya@twsd.me

### Step 2: Delete Old Self Client (Optional)
1. Find your existing Self Client
2. Delete it (to avoid confusion)

### Step 3: Create NEW Self Client
1. Click **"ADD CLIENT"**
2. Select **"Self Client"** (NOT Server-based Application)
3. Fill in the form:

**Scope:**
```
ZohoBooks.fullaccess.ALL
```
⚠️ **IMPORTANT:** Type this EXACTLY - case sensitive, no spaces

**Code expiry duration:**
- Select: **10 minutes** (or longer if available)

**Description:**
```
Tunewave Enterprise Integration
```

### Step 4: Generate Token
1. Click **"Generate"** button
2. **IMMEDIATELY copy the token** (don't wait!)
3. Token starts with: `1000.`

### Step 5: Test Token IMMEDIATELY (Within 1 minute)
Open Command Prompt and run:
```bash
curl -X GET "https://books.zoho.com/api/v3/organizations?organization_id=60062031469" -H "Authorization: Zoho-oauthtoken YOUR_NEW_TOKEN"
```

**Expected Success:**
```json
{"code":0,"message":"success","organizations":[...]}
```

**If Still Error 57:**
- Token might need a few seconds to activate
- Try again after 10-20 seconds
- If still fails, regenerate token

### Step 6: Update appsettings.json
Once token test succeeds:
```json
"ZohoBooks": {
  "AccessToken": "YOUR_NEW_TOKEN_HERE"
}
```

### Step 7: Restart Application
1. Stop application completely
2. Start application
3. Test enterprise creation IMMEDIATELY

## Important Notes

1. **Token Expires in 10 Minutes** - Test and use immediately
2. **Scope Must Be Exact** - `ZohoBooks.fullaccess.ALL`
3. **Test Token First** - Always test with curl before using in app
4. **Restart App** - Must restart after updating appsettings.json

## Alternative: Use Refresh Token (No Expiry)

For production, consider getting a refresh token:
1. Create Server-based Application (not Self Client)
2. Use OAuth flow to get refresh token
3. Refresh token doesn't expire
4. See: ZOHO_REFRESH_TOKEN_SETUP.md























