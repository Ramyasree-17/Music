# Zoho Server-based Application Setup - Complete Guide

## Step 1: Create Server-based Application in Zoho API Console

1. **Go to Zoho API Console:**
   - Open: https://api-console.zoho.com/
   - Login with: ramya@twsd.me

2. **Add New Client:**
   - Click **"ADD CLIENT"** button
   - Select **"Server-based Applications"** (NOT Self Client)

3. **Fill in the Form:**
   - **Client Name:** `Tunewave Enterprise API`
   - **Homepage URL:** `https://spacestation.tunewave.in` (or your domain)
   - **Authorized Redirect URIs:** 
     ```
     http://localhost:8080
     ```
     (You can add multiple URIs separated by comma)

4. **Click "CREATE"**
   - You'll get a **Client ID** and **Client Secret**
   - **IMPORTANT:** Copy both immediately!

5. **Note Your Credentials:**
   - Client ID: `1000.xxxxx` (starts with 1000.)
   - Client Secret: `xxxxx` (long string)

## Step 2: Get Authorization Code

1. **Generate Authorization URL:**
   Replace `YOUR_CLIENT_ID` with your new Client ID:
   ```
   https://accounts.zoho.in/oauth/v2/auth?scope=ZohoBooks.fullaccess.ALL&client_id=YOUR_CLIENT_ID&response_type=code&access_type=offline&redirect_uri=http://localhost:8080
   ```

2. **Open URL in Browser:**
   - Login to Zoho if prompted
   - Click **"Allow"** or **"Accept"** to authorize
   - You'll be redirected to: `http://localhost:8080/?code=1000.xxxxx`

3. **Copy the Authorization Code:**
   - From the redirect URL, copy the `code` parameter
   - Example: `1000.abc123def456ghi789...`
   - **IMPORTANT:** Code expires in 10 minutes!

## Step 3: Exchange Code for Refresh Token

### Option A: Using PowerShell Script (Easier)

I'll create a script for you to run.

### Option B: Using Postman

1. **Create New Request:**
   - Method: `POST`
   - URL: `https://accounts.zoho.in/oauth/v2/token`

2. **Set Headers:**
   - `Content-Type: application/x-www-form-urlencoded`

3. **Set Body (x-www-form-urlencoded):**
   ```
   grant_type: authorization_code
   client_id: YOUR_CLIENT_ID
   client_secret: YOUR_CLIENT_SECRET
   redirect_uri: http://localhost:8080
   code: YOUR_AUTHORIZATION_CODE_FROM_STEP2
   ```

4. **Send Request:**
   - Click "Send"
   - You'll get a response like:
   ```json
   {
     "access_token": "1000.xxx...",
     "refresh_token": "1000.yyy...",
     "expires_in": 3600,
     "token_type": "Bearer"
   }
   ```

5. **Copy the Refresh Token:**
   - Copy the `refresh_token` value
   - This is what you need for appsettings.json!

## Step 4: Update appsettings.json

Update your `ZohoBooks` section:

```json
"ZohoBooks": {
  "ApiBaseUrl": "https://books.zoho.in/api/v3",
  "OrganizationId": "60062031469",
  "ClientId": "YOUR_NEW_CLIENT_ID",
  "ClientSecret": "YOUR_NEW_CLIENT_SECRET",
  "AccessToken": "",
  "RefreshToken": "YOUR_REFRESH_TOKEN_FROM_STEP3",
  "DefaultMonthlyAmount": 10000
}
```

**Important:**
- Use the **new Client ID** and **Client Secret** from Step 1
- Put the **Refresh Token** in `RefreshToken` field
- Leave `AccessToken` empty (service will auto-generate it)

## Step 5: Restart Application

1. Stop your application completely
2. Start it again
3. The service will automatically:
   - Use refresh token to get access token
   - Cache the access token
   - Auto-refresh when it expires

## Step 6: Test

1. Create an enterprise through your API
2. Check the response - `zohoCustomerId` and `zohoRecurringInvoiceId` should NOT be null
3. Check application logs for:
   - "Refreshing Zoho Books access token" (first time)
   - "Zoho customer created: {CustomerId}"

## Troubleshooting

### If Authorization Code Expires:
- Generate a new code (Step 2)
- Codes expire in 10 minutes

### If Getting Error 57:
- Make sure you're using the correct Organization ID
- Verify the scope is exactly: `ZohoBooks.fullaccess.ALL`
- Contact Zoho Support if needed

### If Refresh Token Not Working:
- Verify Client ID and Client Secret are correct
- Make sure redirect URI matches exactly: `http://localhost:8080`
- Check application logs for specific error messages

## Benefits of Server-based Application

✅ **Refresh Token Doesn't Expire** (or expires much later)  
✅ **Automatic Token Refresh** - No manual updates needed  
✅ **Production Ready** - Suitable for long-running applications  
✅ **More Secure** - Better OAuth flow than Self Client






















