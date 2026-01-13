#!/bin/bash
echo "Testing Zoho Books API with your token..."
echo ""

curl -X GET "https://books.zoho.com/api/v3/organizations?organization_id=60062031469" \
  -H "Authorization: Zoho-oauthtoken 1000.d076e94023b4b967509912de5d0f38ca.08ac8d9903ebf0f19f2f8d46b53f58f7"

echo ""
echo ""
echo "If you see 401 Unauthorized, the token expired. Generate a new one."























