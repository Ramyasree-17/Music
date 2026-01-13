# How to Test Zoho Integration in Swagger

## Step 1: Open Swagger UI

1. Run your application
2. Open browser and go to: `https://localhost:7129/swagger` (or your Swagger URL)
3. You'll see all API endpoints

## Step 2: Authorize (Get JWT Token)

### First, Login to Get Token:

1. Find **POST** `/api/auth/login` endpoint
2. Click "Try it out"
3. Enter credentials:
   ```json
   {
     "email": "ramya@twsd.me",
     "password": "your_password"
   }
   ```
4. Click "Execute"
5. Copy the `token` from response

### Authorize in Swagger:

1. Click the **"Authorize"** button at the top right
2. In the "Value" field, enter: `Bearer YOUR_TOKEN_HERE`
   - Example: `Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`
3. Click "Authorize"
4. Click "Close"

## Step 3: Test Enterprise Creation

1. Find **POST** `/api/enterprises` endpoint
2. Click "Try it out"
3. Fill in the form fields:

   **Required Fields:**
   - `EnterpriseName`: `Test Enterprise`
   - `OwnerEmail`: `user@example.com`
   - `AgreementStartDate`: `2025-12-24T07:16:25.042Z`
   - `AgreementEndDate`: `2025-12-24T07:16:25.042Z`
   - `RevenueShare`: `100`
   - `QCRequired`: `true`

   **Optional Fields:**
   - `Domain`: `test.in`
   - `HasIsrcMasterCode`: `false`
   - `AudioMasterCode`: (leave empty)
   - `VideoMasterCode`: (leave empty)
   - `IsrcCertificateFile`: (leave empty)

4. Click "Execute"

## Step 4: Check Response

Look at the response body. You should see:

```json
{
  "status": "success",
  "message": "Enterprise created successfully",
  "enterpriseId": 21,
  "ownerUserId": 3,
  "ownerStatus": "EXISTING",
  "zohoCustomerId": "1234567890",           // ← Check this!
  "zohoRecurringInvoiceId": "9876543210",  // ← Check this!
  "billingDayOfMonth": 24
}
```

### If Zoho IDs are NULL:

- `zohoCustomerId: null` → Zoho token issue
- `zohoRecurringInvoiceId: null` → Zoho token issue

### If Zoho IDs have values:

- ✅ Integration working!
- ✅ IDs saved to database

## Step 5: Verify in Database (Optional)

You can also check directly in database:

```sql
SELECT EnterpriseID, EnterpriseName, 
       ZohoCustomerId, ZohoRecurringInvoiceId,
       BillingDayOfMonth, NextBillingDate
FROM Enterprises
WHERE EnterpriseID = YOUR_ENTERPRISE_ID
```

## Troubleshooting in Swagger

### Error: 401 Unauthorized
- **Fix:** Click "Authorize" and add your JWT token

### Error: 500 Internal Server Error
- **Check:** Application logs for error details
- **Common cause:** Database connection issue or stored procedure error

### Zoho IDs are NULL
- **Check:** Application logs for Zoho errors
- **Fix:** Get valid refresh token and update appsettings.json

## Quick Checklist

- [ ] Logged in and got JWT token
- [ ] Authorized in Swagger with Bearer token
- [ ] Created enterprise via POST /api/enterprises
- [ ] Checked response for zohoCustomerId and zohoRecurringInvoiceId
- [ ] Verified IDs in database (optional)























