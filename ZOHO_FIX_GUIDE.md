# How to Fix Zoho IDs Coming as NULL

## Problem
Zoho IDs (`zohoCustomerId` and `zohoRecurringInvoiceId`) are coming as `null` in the API response and database.

## Root Cause
The Zoho Books API is failing because:
1. **Invalid/Expired Token** - Access token is expired or invalid
2. **Missing Refresh Token** - No refresh token configured for auto-renewal
3. **API Permissions Not Enabled** - Zoho account doesn't have API access enabled (Error 57)

## Solution Steps

### Step 1: Get Valid Refresh Token

#### Option A: Using Browser + Postman

1. **Generate Authorization Code:**
   - Open this URL in browser:
   ```
   https://accounts.zoho.in/oauth/v2/auth?scope=ZohoBooks.contacts.CREATE&client_id=1000.4MI2D0R97EC67Y3FNVIBDZ8IPC775E&response_type=code&access_type=offline&redirect_uri=http://localhost:8080
   ```
   - Login and click "Allow"
   - Copy the `code` from redirect URL: `http://localhost:8080/?code=1000.xxxxx`

2. **Exchange Code for Tokens (Postman):**
   - Method: `POST`
   - URL: `https://accounts.zoho.in/oauth/v2/token`
   - Headers: `Content-Type: application/x-www-form-urlencoded`
   - Body (x-www-form-urlencoded):
     ```
     grant_type: authorization_code
     client_id: 1000.4MI2D0R97EC67Y3FNVIBDZ8IPC775E
     client_secret: YOUR_CLIENT_SECRET
     redirect_uri: http://localhost:8080
     code: YOUR_CODE_FROM_STEP1
     ```
   - Response will contain `refresh_token` - **COPY THIS!**

#### Option B: Using Zoho API Console

1. Go to: https://api-console.zoho.com/
2. Select your app (Client ID: `1000.4MI2D0R97EC67Y3FNVIBDZ8IPC775E`)
3. Click "Generate Code"
4. Copy the code
5. Exchange code for tokens (same as Option A, Step 2)

### Step 2: Test Token Works

**Test in Postman:**
- Method: `GET`
- URL: `https://books.zoho.in/api/v3/organizations?organization_id=60062031469`
- Headers: `Authorization: Zoho-oauthtoken YOUR_ACCESS_TOKEN`

**Expected Success:**
```json
{"code":0,"message":"success","organizations":[...]}
```

**If Error 57:** Contact Zoho Support (18003093036) to enable API permissions

### Step 3: Update appsettings.json

```json
"ZohoBooks": {
  "ApiBaseUrl": "https://books.zoho.in/api/v3",
  "OrganizationId": "60062031469",
  "ClientId": "1000.4MI2D0R97EC67Y3FNVIBDZ8IPC775E",
  "ClientSecret": "YOUR_CLIENT_SECRET",
  "RefreshToken": "YOUR_REFRESH_TOKEN_FROM_STEP1",
  "DefaultMonthlyAmount": 10000
}
```

**Important:**
- Use `RefreshToken` (not `AccessToken`) - it doesn't expire
- The service will auto-refresh access tokens using refresh token

### Step 4: Restart Application

1. Stop your application
2. Start it again
3. New token will be loaded

### Step 5: Test Enterprise Creation

Create an enterprise and check response:
```json
{
  "zohoCustomerId": "1234567890",           // Should have value!
  "zohoRecurringInvoiceId": "9876543210"    // Should have value!
}
```

## Troubleshooting

### If Still Getting NULL:

1. **Check Application Logs:**
   - Look for: "Zoho token refresh failed"
   - Look for: "Create Zoho customer failed"
   - Look for: "Access token not available"

2. **Verify Token in Postman:**
   - Test token directly (Step 2)
   - If error 57 → Contact Zoho Support
   - If error 14 → Generate new token

3. **Check Database:**
   ```sql
   SELECT EnterpriseID, ZohoCustomerId, ZohoRecurringInvoiceId
   FROM Enterprises
   WHERE EnterpriseID = YOUR_ID
   ```

4. **Verify Configuration:**
   - `RefreshToken` is not empty
   - `ClientId` and `ClientSecret` are correct
   - `OrganizationId` is correct

## Quick Checklist

- [ ] Generated refresh token
- [ ] Tested token in Postman (works, no error 57)
- [ ] Updated appsettings.json with refresh token
- [ ] Restarted application
- [ ] Tested enterprise creation
- [ ] Verified Zoho IDs in response and database

## Contact Zoho Support (If Error 57)

**Phone:** 18003093036

**Tell them:**
- "I'm getting error code 57 when accessing Zoho Books API"
- "Organization ID: 60062031469"
- "I need API access enabled for my organization"
- "I'm using OAuth Client ID: 1000.4MI2D0R97EC67Y3FNVIBDZ8IPC775E"























