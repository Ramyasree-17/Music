# Test Zoho Books API Directly

## Test 1: Verify Organization Access

```bash
curl -X GET "https://books.zoho.com/api/v3/organizations?organization_id=60062031469" \
  -H "Authorization: Zoho-oauthtoken 1000.793b72f06a7ade43ed59cf46fd7dcca7.3f0eff7aae0e9226d9ffbc40ea88348c"
```

**Expected:** Should return organization details if token is valid.

## Test 2: Create a Test Customer

```bash
curl -X POST "https://books.zoho.com/api/v3/customers?organization_id=60062031469" \
  -H "Authorization: Zoho-oauthtoken 1000.793b72f06a7ade43ed59cf46fd7dcca7.3f0eff7aae0e9226d9ffbc40ea88348c" \
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

**Expected:** Should return customer details with `customer_id` if successful.

## Common Error Responses

### 401 Unauthorized
```json
{
  "code": 4,
  "message": "Authentication Failure"
}
```
**Solution:** Token expired or invalid. Generate a new token.

### 404 Not Found
```json
{
  "code": 0,
  "message": "Organization not found"
}
```
**Solution:** Check Organization ID is correct.

### 403 Forbidden
```json
{
  "code": 0,
  "message": "Insufficient permissions"
}
```
**Solution:** Token doesn't have required scopes. Regenerate with `ZohoBooks.fullaccess.ALL` scope.























