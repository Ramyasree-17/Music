# Bulk Upload API Documentation

## Overview

Production-ready bulk upload system for music releases and tracks. Uploads Excel/CSV files and processes them in the background using Hangfire.

## Endpoints

### 1. Upload File
**POST** `/api/bulk-upload`

Upload an Excel (.xlsx, .xls) or CSV (.csv) file for bulk processing.

**Request:**
- Content-Type: `multipart/form-data`
- Body: `file` (form field)

**Response:**
```json
{
  "jobId": 1,
  "fileName": "releases.xlsx",
  "totalRows": 50,
  "status": "Pending",
  "createdAt": "2024-01-15T10:30:00Z",
  "message": "File uploaded successfully. Processing 50 rows in background."
}
```

**Status Codes:**
- `200 OK` - File uploaded, job queued
- `400 Bad Request` - Invalid file or no data rows
- `401 Unauthorized` - Missing authentication

---

### 2. Get Job Status
**GET** `/api/bulk-upload/{jobId}/status`

Get real-time status and progress of a bulk upload job.

**Response:**
```json
{
  "jobId": 1,
  "fileName": "releases.xlsx",
  "totalRows": 50,
  "processedRows": 25,
  "successfulRows": 23,
  "failedRows": 2,
  "currentRowNumber": 26,
  "status": "Processing",
  "createdAt": "2024-01-15T10:30:00Z",
  "startedAt": "2024-01-15T10:30:05Z",
  "completedAt": null,
  "estimatedCompletionTime": "2024-01-15T11:20:00Z",
  "remainingTime": "00:50:00",
  "progressPercentage": 50.0,
  "errorMessage": null
}
```

**Status Values:**
- `Pending` - Job queued, not started
- `Processing` - Currently processing rows
- `Completed` - All rows processed
- `Failed` - Job failed with error

---

### 3. Get Job Logs
**GET** `/api/bulk-upload/{jobId}/logs?page=1&pageSize=50`

Get detailed logs for each row processed in a job.

**Query Parameters:**
- `page` (optional) - Page number (default: 1)
- `pageSize` (optional) - Items per page (default: 50)

**Response:**
```json
{
  "jobId": 1,
  "page": 1,
  "pageSize": 50,
  "logs": [
    {
      "id": 1,
      "rowNumber": 1,
      "status": "Success",
      "message": "Release 'Summer Hits' and Track 'Summer Vibes' created successfully",
      "createdAt": "2024-01-15T10:30:10Z",
      "releaseTitle": "Summer Hits",
      "trackTitle": "Summer Vibes",
      "releaseId": 100,
      "trackId": 200
    },
    {
      "id": 2,
      "rowNumber": 2,
      "status": "Failed",
      "message": "Error: LabelId is required",
      "createdAt": "2024-01-15T10:32:10Z",
      "releaseTitle": "Winter Album",
      "trackTitle": null,
      "releaseId": null,
      "trackId": null
    }
  ]
}
```

---

### 4. List All Jobs
**GET** `/api/bulk-upload?page=1&pageSize=20`

Get a list of all bulk upload jobs.

**Query Parameters:**
- `page` (optional) - Page number (default: 1)
- `pageSize` (optional) - Items per page (default: 20)

**Response:**
```json
{
  "page": 1,
  "pageSize": 20,
  "jobs": [
    {
      "jobId": 1,
      "fileName": "releases.xlsx",
      "totalRows": 50,
      "processedRows": 50,
      "successfulRows": 48,
      "failedRows": 2,
      "status": "Completed",
      "createdAt": "2024-01-15T10:30:00Z",
      "completedAt": "2024-01-15T11:30:00Z"
    }
  ]
}
```

---

## Processing Details

### Background Processing
- Jobs are processed asynchronously using Hangfire
- Each row takes approximately 2 minutes to process
- Rows are processed sequentially (one at a time per job)
- Multiple jobs can run in parallel

### ETA Calculation
- Estimated time per row: 2 minutes
- `estimatedTotalTime = totalRows × 2 minutes`
- `remainingTime = (totalRows - processedRows) × 2 minutes`
- `estimatedCompletionTime = currentTime + remainingTime`

### Error Handling
- Individual row failures don't stop the job
- Failed rows are logged with error messages
- Job continues processing remaining rows
- Job status set to "Failed" only on fatal errors

### Data Validation
**Required Fields:**
- ReleaseTitle
- TrackTitle
- LabelId

**Validation Errors:**
- Missing required fields → Row fails, logged
- Invalid date formats → Row fails, logged
- Invalid artist IDs → Row fails, logged
- Database constraints → Row fails, logged

---

## Example cURL Requests

### Upload File
```bash
curl -X POST "https://api.example.com/api/bulk-upload" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "file=@releases.xlsx"
```

### Get Job Status
```bash
curl -X GET "https://api.example.com/api/bulk-upload/1/status" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Get Job Logs
```bash
curl -X GET "https://api.example.com/api/bulk-upload/1/logs?page=1&pageSize=50" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## Hangfire Dashboard

Access the Hangfire dashboard at: `/hangfire`

**Note:** Secure this endpoint in production!

---

## Database Tables

### BulkJobs
- Tracks job metadata and progress
- Status: Pending, Processing, Completed, Failed

### BulkJobLogs
- Detailed logs for each row
- Links to created Release and Track IDs
- Error messages for failed rows

---

## Setup Instructions

1. **Run Database Script:**
   ```sql
   -- Execute: Database/CreateBulkUploadTables.sql
   ```

2. **Restore NuGet Packages:**
   ```bash
   dotnet restore
   ```

3. **Build Project:**
   ```bash
   dotnet build
   ```

4. **Run Application:**
   ```bash
   dotnet run
   ```

5. **Access Hangfire Dashboard:**
   - Navigate to: `https://localhost:7129/hangfire`

---

## Production Considerations

1. **Secure Hangfire Dashboard** - Add authentication
2. **File Cleanup** - Implement file deletion after processing
3. **Rate Limiting** - Add rate limits for upload endpoint
4. **File Size Limits** - Configure max file size
5. **Monitoring** - Set up alerts for failed jobs
6. **Retry Logic** - Implement retry for transient failures
7. **Progress Webhooks** - Optional: Add webhook notifications


