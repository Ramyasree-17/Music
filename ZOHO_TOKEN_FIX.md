# Fix Zoho Token Authorization Error

## Problem
Error Code 57: "You are not authorized to perform this operation"

## Solution: Generate New Token with Correct Scope

### Step 1: Go to Zoho API Console
1. Open: https://api-console.zoho.com/
2. Login with: ramya@twsd.me

### Step 2: Create New Self Client
1. Click **"ADD CLIENT"**
2. Select **"Self Client"**
3. Fill in:
   - **Scope**: `ZohoBooks.fullaccess.ALL` (IMPORTANT - must be exact)
   - **Code expiry duration**: 10 minutes (or longer if available)
   - **Description**: Tunewave Enterprise

### Step 3: Generate Token
1. Click **"Generate"**
2. **IMMEDIATELY copy the token** (it expires quickly)

### Step 4: Test Token
Run this command to verify:
```bash
curl -X GET "https://books.zoho.com/api/v3/organizations?organization_id=60062031469" -H "Authorization: Zoho-oauthtoken YOUR_NEW_TOKEN"
```

**Expected Response (Success):**
```json
{
  "code": 0,
  "message": "success",
  "organizations": [...]
}
```

### Step 5: Update appsettings.json
```json
"ZohoBooks": {
  "AccessToken": "YOUR_NEW_TOKEN_HERE"
}
```

### Step 6: Restart Application
Restart your .NET application

## Important Notes

1. **Scope Must Be Exact**: `ZohoBooks.fullaccess.ALL` (case-sensitive)
2. **Token Expires**: Copy immediately after generation
3. **Test Before Using**: Always test token with curl first

## Alternative: Check Organization ID

If token generation doesn't work, verify Organization ID:
1. Login to Zoho Books: https://books.zoho.in/app/60062031469
2. Go to Settings → Organization → Organization Details
3. Verify the Organization ID matches: `60062031469`























