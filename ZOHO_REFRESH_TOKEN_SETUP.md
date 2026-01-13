# Zoho Books Refresh Token Setup

## Why Use Refresh Tokens?

- **Access tokens expire** (typically 1 hour for Zoho)
- **Refresh tokens don't expire** (or expire much later)
- **Automatic token refresh** - No manual intervention needed
- **Production-ready** - Better for long-running applications

## Step 1: Get Refresh Token

### Option A: Using OAuth Authorization Code Flow

1. **Generate Authorization URL:**
   ```
   https://accounts.zoho.in/oauth/v2/auth?scope=ZohoBooks.fullaccess.ALL&client_id=YOUR_CLIENT_ID&response_type=code&access_type=offline&redirect_uri=YOUR_REDIRECT_URI
   ```

2. **Replace placeholders:**
   - `YOUR_CLIENT_ID`: `1000.X8NNZ6J9PO0QOOA2AOG8CP1F6JOF7H`
   - `YOUR_REDIRECT_URI`: Your registered redirect URI (e.g., `https://yourdomain.com/oauth/callback`)

3. **Open URL in browser** and authorize

4. **Get Authorization Code** from redirect URL:
   ```
   https://yourdomain.com/oauth/callback?code=AUTHORIZATION_CODE
   ```

5. **Exchange Code for Tokens:**
   ```bash
   curl -X POST "https://accounts.zoho.in/oauth/v2/token" \
     -d "grant_type=authorization_code" \
     -d "client_id=1000.X8NNZ6J9PO0QOOA2AOG8CP1F6JOF7H" \
     -d "client_secret=c7c52fdb72aaa96ded3c147b3ce67ffc30fb48edba" \
     -d "redirect_uri=YOUR_REDIRECT_URI" \
     -d "code=AUTHORIZATION_CODE"
   ```

6. **Response will contain:**
   ```json
   {
     "access_token": "1000.xxx...",
     "refresh_token": "1000.yyy...",
     "expires_in": 3600
   }
   ```

### Option B: Using Zoho API Console (Easier)

1. Go to https://api-console.zoho.com/
2. Create a **Server-based Application** (not Self Client)
3. Set redirect URI
4. Authorize and get refresh token

## Step 2: Update appsettings.json

```json
"ZohoBooks": {
  "ApiBaseUrl": "https://books.zoho.com/api/v3",
  "OrganizationId": "60062031469",
  "ClientId": "1000.X8NNZ6J9PO0QOOA2AOG8CP1F6JOF7H",
  "ClientSecret": "c7c52fdb72aaa96ded3c147b3ce67ffc30fb48edba",
  "AccessToken": "",
  "RefreshToken": "YOUR_REFRESH_TOKEN_HERE",
  "DefaultMonthlyAmount": 10000
}
```

## How It Works

1. **First Request:** Service uses refresh token to get access token
2. **Subsequent Requests:** Uses cached access token
3. **Token Expiry:** Automatically refreshes 5 minutes before expiry
4. **No Manual Intervention:** Tokens refresh automatically

## Benefits

✅ **No Expiry Issues** - Refresh tokens last much longer  
✅ **Automatic Refresh** - No manual token updates needed  
✅ **Production Ready** - Suitable for long-running applications  
✅ **Error Handling** - Graceful fallback if refresh fails  

## Testing

After setup, test by:
1. Restart application
2. Create enterprise
3. Check logs for "Refreshing Zoho Books access token" (first time)
4. Subsequent requests use cached token

## Troubleshooting

### Refresh Token Invalid
- Regenerate refresh token using OAuth flow
- Make sure redirect URI matches exactly

### Access Token Not Refreshing
- Check logs for refresh errors
- Verify ClientId and ClientSecret are correct
- Ensure refresh token is valid























