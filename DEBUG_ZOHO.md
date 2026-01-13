# Debug Zoho Books Integration

## Step 1: Test Zoho API Directly

Test if your token works with Zoho API:

```bash
curl -X GET "https://books.zoho.com/api/v3/organizations?organization_id=60062031469" \
  -H "Authorization: Zoho-oauthtoken 1000.d076e94023b4b967509912de5d0f38ca.08ac8d9903ebf0f19f2f8d46b53f58f7"
```

**Expected Response (Success):**
```json
{
  "code": 0,
  "message": "success",
  "organizations": [...]
}
```

**If you get 401 Unauthorized:**
- Token expired (10 minutes)
- Generate a new token

## Step 2: Test Customer Creation Directly

```bash
curl -X POST "https://books.zoho.com/api/v3/customers?organization_id=60062031469" \
  -H "Authorization: Zoho-oauthtoken 1000.d076e94023b4b967509912de5d0f38ca.08ac8d9903ebf0f19f2f8d46b53f58f7" \
  -H "Content-Type: application/json" \
  -d '{
    "customer_name": "Test Customer",
    "email": "test@example.com",
    "contact_name": "Test Customer",
    "billing_address": {
      "address": "",
      "city": "",
      "state": "",
      "zip": "",
      "country": "India"
    }
  }'
```

## Step 3: Check Application Logs

Look for these messages in your application logs:

1. **"Zoho Books OrganizationId not configured"**
   - Solution: Check appsettings.json

2. **"Zoho Books access token not available"**
   - Solution: Token not loaded or expired

3. **"Failed to create customer in Zoho Books"**
   - Check the error message for details

4. **"Zoho Books API error"**
   - Check the error message

## Step 4: Common Issues

### Issue 1: Token Expired
- **Symptom:** 401 Unauthorized
- **Solution:** Generate new token (10 minutes expiry)

### Issue 2: Organization ID Wrong
- **Symptom:** 404 Not Found
- **Solution:** Verify Organization ID: 60062031469

### Issue 3: API Call Failing Silently
- **Symptom:** No error, but null values
- **Solution:** Check application logs for errors

## Step 5: Enable Detailed Logging

Add to appsettings.json:
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "TunewaveAPIDB1.Services.ZohoBooksService": "Debug"
  }
}
```























