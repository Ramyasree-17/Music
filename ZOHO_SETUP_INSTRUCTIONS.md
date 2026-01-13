# Zoho Books Integration Setup Instructions

## Why Zoho IDs are NULL?

The Zoho Books integration requires valid credentials to be configured in `appsettings.json`. If `OrganizationId` or `AccessToken` are empty, the integration will be skipped and return `null` values.

## Step 1: Get Zoho Books Credentials

### 1.1 Get Organization ID
1. Log in to your Zoho Books account: https://books.zoho.com
2. Go to **Settings** → **Organization** → **Organization Details**
3. Copy the **Organization ID** (it's a long number like `123456789`)

### 1.2 Get Access Token (OAuth)
You need to create a Zoho API application and get an access token:

#### Option A: Using Zoho Developer Console (Recommended)
1. Go to https://api-console.zoho.com/
2. Click **Add Client** → Select **Server-based Applications**
3. Fill in:
   - **Client Name**: Tunewave API
   - **Homepage URL**: https://yourdomain.com
   - **Authorized Redirect URIs**: https://yourdomain.com/oauth/callback
4. Click **Create**
5. Copy the **Client ID** and **Client Secret**
6. Generate an access token using OAuth flow

#### Option B: Using Self-Client (For Testing)
1. Go to https://api-console.zoho.com/
2. Click **Add Client** → Select **Self Client**
3. Select scopes:
   - `ZohoBooks.fullaccess.ALL`
4. Generate and copy the **Access Token**

## Step 2: Update appsettings.json

Update the `ZohoBooks` section in `appsettings.json`:

```json
"ZohoBooks": {
  "ApiBaseUrl": "https://books.zoho.com/api/v3",
  "OrganizationId": "YOUR_ORGANIZATION_ID_HERE",
  "AccessToken": "YOUR_ACCESS_TOKEN_HERE",
  "DefaultMonthlyAmount": 10000
}
```

**Example:**
```json
"ZohoBooks": {
  "ApiBaseUrl": "https://books.zoho.com/api/v3",
  "OrganizationId": "123456789",
  "AccessToken": "1000.abc123def456ghi789jkl012mno345pqr678stu901vwx234yz",
  "DefaultMonthlyAmount": 10000
}
```

## Step 3: Restart Application

After updating `appsettings.json`, restart your application for the changes to take effect.

## Step 4: Test the Integration

1. Create a new enterprise using the API
2. Check the response - you should see:
   ```json
   {
     "zohoCustomerId": "1234567890",
     "zohoRecurringInvoiceId": "9876543210",
     "billingDayOfMonth": 24
   }
   ```

## Troubleshooting

### Still Getting NULL Values?

1. **Check Application Logs:**
   - Look for warnings: "Zoho Books credentials not configured"
   - Look for errors: "Failed to create customer in Zoho Books"

2. **Verify Credentials:**
   - Make sure `OrganizationId` is correct
   - Make sure `AccessToken` is valid and not expired
   - Access tokens expire after a certain time - you may need to refresh them

3. **Check Zoho API Response:**
   - The service logs API errors - check your application logs
   - Common errors:
     - `401 Unauthorized` - Invalid access token
     - `404 Not Found` - Invalid organization ID
     - `403 Forbidden` - Insufficient permissions

4. **Test Zoho API Directly:**
   ```bash
   curl -X GET "https://books.zoho.com/api/v3/organizations?organization_id=YOUR_ORG_ID" \
     -H "Authorization: Zoho-oauthtoken YOUR_ACCESS_TOKEN"
   ```

### Access Token Expired?

Zoho access tokens expire. You have two options:

1. **Use Refresh Token** (Recommended for Production):
   - Implement token refresh logic
   - Store refresh token securely
   - Automatically refresh when token expires

2. **Generate New Token** (For Testing):
   - Go to Zoho API Console
   - Generate a new access token
   - Update `appsettings.json`

## Important Notes

- **DefaultMonthlyAmount**: Set this to 0 if you don't want to create recurring invoices automatically
- **Access Token Security**: Never commit access tokens to version control
- **Token Expiry**: Access tokens typically expire after 1 hour (for self-client) or based on your OAuth settings
- **Production**: Use environment variables or secure configuration management instead of hardcoding in appsettings.json

## Next Steps

Once configured:
1. Enterprise creation will automatically create customers in Zoho Books
2. Recurring invoices will be set up automatically (if `DefaultMonthlyAmount` > 0)
3. Payment recording will update Zoho Books recurring invoice schedules























