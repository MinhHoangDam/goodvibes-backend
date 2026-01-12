# Good Vibes Backend - API Documentation

Complete API reference with request/response examples.

## Base URL

- **Local Development**: `http://localhost:5000`
- **Production**: `https://your-app-name.azurewebsites.net`

## Authentication

This API proxies authenticated requests to the Workleap API. The API key is configured server-side, so frontend applications don't need to handle authentication.

## Response Format

All responses are in JSON format. Successful responses return HTTP 200 with data. Errors return appropriate HTTP status codes with error details.

---

## Health & Diagnostics

### GET /health

Check if the API is running.

**Response:**
```json
{
  "status": "ok",
  "message": "Server is running"
}
```

---

### GET /debug

Get environment and configuration information.

**Response:**
```json
{
  "port": "5000",
  "environment": "Production",
  "urls": "http://0.0.0.0:5000"
}
```

---

## Good Vibes Endpoints

### GET /api/good-vibes

List Good Vibes with optional filters.

**Query Parameters:**
- `isPublic` (boolean, optional): Filter by public/private vibes
- `limit` (integer, optional): Number of results to return (default: 100)
- `continuationToken` (string, optional): Token for pagination

**Example Request:**
```bash
curl "http://localhost:5000/api/good-vibes?isPublic=true&limit=10"
```

**Example Response:**
```json
{
  "data": [
    {
      "goodVibeId": "67f9ac42506d9dac97d047d9",
      "creationDate": "2025-01-09T20:10:26.231Z",
      "senderUser": {
        "userId": "user123",
        "displayName": "John Doe",
        "emailAddress": "john@example.com",
        "avatarUrl": "https://example.com/avatar32x32.jpg"
      },
      "recipients": [
        {
          "userId": "user456",
          "displayName": "Jane Smith",
          "emailAddress": "jane@example.com",
          "avatarUrl": "https://example.com/avatar32x32.jpg"
        }
      ],
      "collectionName": [
        {
          "text": "Thank you",
          "locale": "en-US"
        }
      ],
      "message": "Great work on the presentation!",
      "messageHtml": "<p>Great work on the presentation!</p>",
      "isPublic": true,
      "replyCount": 2
    }
  ],
  "metadata": {
    "continuationToken": "eyJza2lwIjoxMCwidGFrZSI6MTB9"
  }
}
```

---

### GET /api/good-vibes/{goodVibeId}

Get a single Good Vibe with replies.

**Path Parameters:**
- `goodVibeId` (string, required): The Good Vibe ID

**Example Request:**
```bash
curl "http://localhost:5000/api/good-vibes/67f9ac42506d9dac97d047d9"
```

**Example Response:**
```json
{
  "goodVibeId": "67f9ac42506d9dac97d047d9",
  "creationDate": "2025-01-09T20:10:26.231Z",
  "senderUser": {
    "userId": "user123",
    "displayName": "John Doe",
    "emailAddress": "john@example.com",
    "avatarUrl": "https://example.com/avatar32x32.jpg"
  },
  "recipients": [
    {
      "userId": "user456",
      "displayName": "Jane Smith",
      "emailAddress": "jane@example.com",
      "avatarUrl": "https://example.com/avatar32x32.jpg"
    }
  ],
  "message": "Great work on the presentation!",
  "isPublic": true,
  "replyCount": 2,
  "replies": [
    {
      "replyId": "reply123",
      "authorUser": {
        "userId": "user456",
        "displayName": "Jane Smith",
        "avatarUrl": "https://example.com/avatar32x32.jpg"
      },
      "message": "Thank you!",
      "creationDate": "2025-01-09T20:15:00.000Z"
    }
  ]
}
```

---

### GET /api/good-vibes/cached

Fast cached endpoint for Good Vibes with date filtering. Ideal for carousels and dashboards.

**Query Parameters:**
- `monthsBack` (integer, optional): Get vibes from the last N months
- `daysBack` (integer, optional): Get vibes from the last N days (takes precedence over monthsBack)
- `avatarSize` (string, optional): Avatar size to fetch (default: "32x32")
  - Options: "24x24", "32x32", "48x48", "64x64", "128x128", "256x256"
- `skipAvatars` (boolean, optional): Skip avatar enrichment for faster response (default: false)

**Example Request:**
```bash
# Get last 30 days of vibes with avatars
curl "http://localhost:5000/api/good-vibes/cached?daysBack=30"

# Get last 3 months without avatars (faster)
curl "http://localhost:5000/api/good-vibes/cached?monthsBack=3&skipAvatars=true"

# Get vibes with large avatars
curl "http://localhost:5000/api/good-vibes/cached?daysBack=7&avatarSize=128x128"
```

**Example Response:**
```json
{
  "data": [
    {
      "goodVibeId": "67f9ac42506d9dac97d047d9",
      "message": "Great work!",
      "senderUser": {
        "userId": "user123",
        "displayName": "John Doe",
        "avatarUrl": "https://example.com/avatar32x32.jpg"
      }
    }
  ],
  "metadata": {
    "cacheReady": true,
    "totalCount": 222,
    "filteredCount": 45,
    "monthsBack": null
  }
}
```

---

### GET /api/good-vibes/collections

List available Good Vibes collections (custom prompts).

**Example Response:**
```json
{
  "data": [
    {
      "collectionId": "collection123",
      "name": [
        {
          "text": "Thank you",
          "locale": "en-US"
        }
      ],
      "isActive": true
    }
  ]
}
```

---

## User Endpoints

### GET /api/users/{userId}

Get user information including avatar URLs.

**Path Parameters:**
- `userId` (string, required): The user ID

**Example Response:**
```json
{
  "id": "user123",
  "userName": "john.doe@example.com",
  "displayName": "John Doe",
  "urn:workleap:params:scim:schemas:extension:user:2.0:User": {
    "imageUrls": {
      "24x24": "https://example.com/avatar24x24.jpg",
      "32x32": "https://example.com/avatar32x32.jpg",
      "48x48": "https://example.com/avatar48x48.jpg",
      "64x64": "https://example.com/avatar64x64.jpg",
      "128x128": "https://example.com/avatar128x128.jpg",
      "256x256": "https://example.com/avatar256x256.jpg"
    }
  }
}
```

---

### GET /api/debug/user-avatar/{userId}

Debug endpoint to test avatar fetching for a specific user.

**Example Response:**
```json
{
  "userId": "user123",
  "success": true,
  "hasExtensionSchema": true,
  "hasImageUrls": true,
  "imageUrls": {
    "32x32": "https://example.com/avatar32x32.jpg",
    "48x48": "https://example.com/avatar48x48.jpg"
  },
  "avatarFromCache": "https://example.com/avatar32x32.jpg"
}
```

---

## Statistics Endpoints

### GET /api/stats/top-senders

Get top Good Vibes senders (all-time).

**Query Parameters:**
- `limit` (integer, optional): Number of results (default: 10)

**Example Request:**
```bash
curl "http://localhost:5000/api/stats/top-senders?limit=5"
```

**Example Response:**
```json
{
  "topSenders": [
    {
      "user": {
        "userId": "user123",
        "displayName": "John Doe",
        "emailAddress": "john@example.com",
        "avatarUrl": "https://example.com/avatar32x32.jpg"
      },
      "count": 45
    },
    {
      "user": {
        "userId": "user456",
        "displayName": "Jane Smith",
        "emailAddress": "jane@example.com",
        "avatarUrl": "https://example.com/avatar32x32.jpg"
      },
      "count": 38
    }
  ]
}
```

---

### GET /api/stats/top-recipients

Get top Good Vibes recipients (all-time).

**Query Parameters:**
- `limit` (integer, optional): Number of results (default: 10)

**Example Response:**
```json
{
  "topRecipients": [
    {
      "user": {
        "userId": "user789",
        "displayName": "Alice Johnson",
        "avatarUrl": "https://example.com/avatar32x32.jpg"
      },
      "count": 52
    }
  ]
}
```

---

### GET /api/stats/available-months

Get all months that have Good Vibes data.

**Example Response:**
```json
{
  "months": [
    {
      "year": 2025,
      "month": 1,
      "label": "January 2025"
    },
    {
      "year": 2024,
      "month": 12,
      "label": "December 2024"
    }
  ]
}
```

---

### GET /api/stats/monthly/top-senders

Get top senders for a specific month.

**Query Parameters:**
- `year` (integer, optional): Year (default: current year)
- `month` (integer, optional): Month 1-12 (default: current month)
- `limit` (integer, optional): Number of results (default: 5)

**Example Request:**
```bash
curl "http://localhost:5000/api/stats/monthly/top-senders?year=2025&month=1&limit=5"
```

**Example Response:**
```json
{
  "topSenders": [
    {
      "user": {
        "userId": "user123",
        "displayName": "John Doe",
        "avatarUrl": "https://example.com/avatar32x32.jpg"
      },
      "count": 12
    }
  ],
  "year": 2025,
  "month": 1
}
```

---

### GET /api/stats/monthly/top-recipients

Get top recipients for a specific month.

**Query Parameters:**
- `year` (integer, optional): Year (default: current year)
- `month` (integer, optional): Month 1-12 (default: current month)
- `limit` (integer, optional): Number of results (default: 5)

**Example Response:**
```json
{
  "topRecipients": [
    {
      "user": {
        "userId": "user456",
        "displayName": "Jane Smith",
        "avatarUrl": "https://example.com/avatar32x32.jpg"
      },
      "count": 15
    }
  ],
  "year": 2025,
  "month": 1
}
```

---

### GET /api/stats/monthly/top-collections

Get top Good Vibes collections for a specific month.

**Query Parameters:**
- `year` (integer, required): Year
- `month` (integer, required): Month 1-12
- `limit` (integer, required): Number of results (max 100)

**Example Request:**
```bash
curl "http://localhost:5000/api/stats/monthly/top-collections?year=2025&month=1&limit=10"
```

**Example Response:**
```json
{
  "topCollections": [
    {
      "name": "Thank you",
      "count": 87
    },
    {
      "name": "Great work",
      "count": 65
    }
  ],
  "year": 2025,
  "month": 1
}
```

---

## Error Responses

### 400 Bad Request
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid parameter value"
}
```

### 404 Not Found
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Good Vibe not found"
}
```

### 500 Internal Server Error
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "Failed to fetch Good Vibes: Connection timeout"
}
```

---

## Rate Limiting

The API implements automatic rate limiting protection:
- 100ms delays between requests
- Automatic retry on 429 (Too Many Requests) responses
- Exponential backoff for persistent rate limits

If you encounter rate limiting, the API will automatically handle retries. Check logs for warnings about rate limiting.

---

## Caching Behavior

### Good Vibes Cache
- Refreshes every 5 minutes
- Includes all public Good Vibes
- Enriches vibes with replies
- Initial load: ~10-15 seconds

### Avatar Cache
- Successful fetches: cached for 1 hour
- Failed fetches: cached for 5 minutes
- Size-specific caching (32x32 vs 128x128)

### Cache Status

Check if cache is ready:
```bash
curl "http://localhost:5000/api/good-vibes/cached"
```

Look for `"cacheReady": true` in the response metadata.

---

## Frontend Integration Examples

### React Example

```javascript
import { useState, useEffect } from 'react';

function GoodVibesCarousel() {
  const [vibes, setVibes] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch('http://localhost:5000/api/good-vibes/cached?daysBack=30')
      .then(res => res.json())
      .then(data => {
        setVibes(data.data);
        setLoading(false);
      })
      .catch(error => console.error('Error:', error));
  }, []);

  if (loading) return <div>Loading...</div>;

  return (
    <div className="vibes-carousel">
      {vibes.map(vibe => (
        <div key={vibe.goodVibeId} className="vibe-card">
          <img src={vibe.senderUser.avatarUrl} alt={vibe.senderUser.displayName} />
          <p>{vibe.message}</p>
          <span>From: {vibe.senderUser.displayName}</span>
        </div>
      ))}
    </div>
  );
}
```

### Vanilla JavaScript Example

```javascript
async function fetchGoodVibes() {
  try {
    const response = await fetch('http://localhost:5000/api/good-vibes?isPublic=true&limit=50');
    const data = await response.json();

    displayVibes(data.data);
  } catch (error) {
    console.error('Error fetching vibes:', error);
  }
}

function displayVibes(vibes) {
  const container = document.getElementById('vibes-container');

  vibes.forEach(vibe => {
    const div = document.createElement('div');
    div.className = 'vibe';
    div.innerHTML = `
      <img src="${vibe.senderUser.avatarUrl}" />
      <p>${vibe.message}</p>
      <small>From: ${vibe.senderUser.displayName}</small>
    `;
    container.appendChild(div);
  });
}
```

---

## Best Practices

1. **Use the cached endpoint** for dashboards and carousels
2. **Implement pagination** for large datasets using continuationToken
3. **Handle loading states** - cache initialization takes 10-15 seconds
4. **Request appropriate avatar sizes** - use 32x32 for thumbnails, 128x128 for profiles
5. **Implement error handling** - API may return 500 errors if Workleap API is down
6. **Cache responses** in your frontend for better performance
7. **Use skipAvatars=true** when you don't need avatars for fastest response

---

## Support

For API issues or questions, contact your Workleap support team.
