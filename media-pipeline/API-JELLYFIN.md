# Jellyfin API Reference - Watched State Detection

## Authentication
All requests require authentication via API key:
```
X-Emby-Token: YOUR_JELLYFIN_API_KEY
```

Or via query parameter:
```
?api_key=YOUR_JELLYFIN_API_KEY
```

## Base URL
```
http://jellyfin.media.svc.cluster.local:8096
```

---

## 1. Get All Users
Needed to iterate through users for watched state.

**Endpoint**:
```
GET /Users
```

**Headers**:
```
X-Emby-Token: YOUR_API_KEY
```

**Response**:
```json
[
  {
    "Name": "admin",
    "ServerId": "abc123",
    "Id": "user-id-123",
    "HasPassword": true,
    "HasConfiguredPassword": true,
    "HasConfiguredEasyPassword": false
  }
]
```

---

## 2. Get Watched Movies (Per User)

**Endpoint**:
```
GET /Users/{userId}/Items
```

**Query Parameters**:
```
?IncludeItemTypes=Movie
&Recursive=true
&IsPlayed=true
&Fields=Path,ProviderIds,UserData,DateCreated
```

**Full URL Example**:
```
GET /Users/user-id-123/Items?IncludeItemTypes=Movie&Recursive=true&IsPlayed=true&Fields=Path,ProviderIds,UserData,DateCreated
```

**Response**:
```json
{
  "Items": [
    {
      "Name": "The Matrix",
      "Id": "item-id-456",
      "Path": "/media/movies/The Matrix (1999)/The Matrix (1999) REMUX-2160p.mkv",
      "DateCreated": "2025-12-15T10:30:00.0000000Z",
      "UserData": {
        "PlaybackPositionTicks": 0,
        "PlayCount": 2,
        "IsFavorite": false,
        "Played": true,
        "Key": "item-id-456",
        "LastPlayedDate": "2025-12-18T21:45:30.0000000Z"
      },
      "ProviderIds": {
        "Tmdb": "603",
        "Imdb": "tt0133093"
      }
    }
  ],
  "TotalRecordCount": 1
}
```

**Key Fields**:
- `Path`: File path for matching to Radarr
- `UserData.Played`: Watched status
- `UserData.LastPlayedDate`: When last watched (for grace period calculation)
- `UserData.PlayCount`: Number of times played
- `ProviderIds.Tmdb`: For matching to Radarr if path matching fails

---

## 3. Get Watched Episodes (Per User)

**Endpoint**:
```
GET /Users/{userId}/Items
```

**Query Parameters**:
```
?IncludeItemTypes=Episode
&Recursive=true
&IsPlayed=true
&Fields=Path,ProviderIds,UserData,DateCreated,SeasonId,SeriesId,IndexNumber,ParentIndexNumber
```

**Full URL Example**:
```
GET /Users/user-id-123/Items?IncludeItemTypes=Episode&Recursive=true&IsPlayed=true&Fields=Path,ProviderIds,UserData,DateCreated,SeasonId,SeriesId,IndexNumber,ParentIndexNumber
```

**Response**:
```json
{
  "Items": [
    {
      "Name": "Pilot",
      "Id": "episode-id-789",
      "SeriesId": "series-id-111",
      "SeasonId": "season-id-222",
      "SeriesName": "Breaking Bad",
      "SeasonName": "Season 1",
      "IndexNumber": 1,  // Episode number
      "ParentIndexNumber": 1,  // Season number
      "Path": "/media/tv/Breaking Bad/Season 01/Breaking Bad - S01E01 - Pilot REMUX-2160p.mkv",
      "DateCreated": "2025-12-10T08:00:00.0000000Z",
      "UserData": {
        "PlaybackPositionTicks": 0,
        "PlayCount": 1,
        "IsFavorite": false,
        "Played": true,
        "Key": "episode-id-789",
        "LastPlayedDate": "2025-12-17T19:30:00.0000000Z"
      },
      "ProviderIds": {
        "Tvdb": "349232",
        "Imdb": "tt0959621",
        "TvRage": "637041"
      }
    }
  ],
  "TotalRecordCount": 1
}
```

**Key Fields**:
- `SeriesId`: To group episodes by series
- `SeasonId`: To group episodes by season
- `IndexNumber`: Episode number
- `ParentIndexNumber`: Season number
- `Path`: File path for matching to Sonarr
- `UserData.LastPlayedDate`: When last watched

---

## 4. Get All Episodes for a Series

Useful for determining if entire season is watched.

**Endpoint**:
```
GET /Shows/{seriesId}/Episodes
```

**Query Parameters**:
```
?userId={userId}
&Fields=Path,UserData,IndexNumber,ParentIndexNumber
```

**Response**:
```json
{
  "Items": [
    {
      "Name": "Pilot",
      "Id": "episode-id-789",
      "IndexNumber": 1,
      "ParentIndexNumber": 1,
      "Path": "/media/tv/Breaking Bad/Season 01/Breaking Bad - S01E01 - Pilot REMUX-2160p.mkv",
      "UserData": {
        "Played": true,
        "LastPlayedDate": "2025-12-17T19:30:00.0000000Z"
      }
    },
    {
      "Name": "Cat's in the Bag...",
      "Id": "episode-id-790",
      "IndexNumber": 2,
      "ParentIndexNumber": 1,
      "Path": "/media/tv/Breaking Bad/Season 01/Breaking Bad - S01E02 - Cat's in the Bag REMUX-2160p.mkv",
      "UserData": {
        "Played": false
      }
    }
  ],
  "TotalRecordCount": 7
}
```

---

## 5. Multi-User Playback Handling

### Strategy 1: OR Logic (Any user watched = considered watched)
Query each user separately, combine results. If ANY user has `Played=true` and grace period expired, consider for deletion.

### Strategy 2: AND Logic (All users watched = considered watched)
Query each user, only delete if ALL users have watched OR only specific "primary" user has watched.

### Recommended: Primary User Strategy
- Designate one user as "primary" (typically admin)
- Only track that user's watched state
- Simpler, more predictable
- Set via environment variable: `PRIMARY_USER_ID`

**Implementation**:
```python
# Get primary user ID
response = requests.get(
    f"{JELLYFIN_URL}/Users",
    headers={"X-Emby-Token": JELLYFIN_API_KEY}
)
users = response.json()
primary_user = next(u for u in users if u['Name'] == 'admin')
PRIMARY_USER_ID = primary_user['Id']

# Then query only that user's watched state
```

---

## 6. Last Played Timestamp

Located in `UserData.LastPlayedDate`:
```
"2025-12-18T21:45:30.0000000Z"
```

**Grace Period Calculation**:
```python
from datetime import datetime, timedelta

last_played = datetime.fromisoformat(item['UserData']['LastPlayedDate'].replace('Z', '+00:00'))
grace_period_days = 5
cutoff_date = datetime.now(timezone.utc) - timedelta(days=grace_period_days)

if last_played < cutoff_date:
    # Eligible for deletion
    pass
```

---

## 7. Get Item by Path (Reverse Lookup)

Sometimes needed to find Jellyfin item from filesystem path.

**Endpoint**:
```
GET /Items
```

**Query Parameters**:
```
?Path=/media/movies/The%20Matrix%20(1999)/The%20Matrix%20(1999)%20REMUX-2160p.mkv
&Recursive=true
```

**Response**: Same structure as Items endpoint.

---

## 8. Complete Example: Get Watched Movies

```bash
curl -X GET \
  "http://jellyfin.media.svc.cluster.local:8096/Users/user-id-123/Items?IncludeItemTypes=Movie&Recursive=true&IsPlayed=true&Fields=Path,ProviderIds,UserData,DateCreated" \
  -H "X-Emby-Token: YOUR_API_KEY"
```

**Python Example**:
```python
import requests
from datetime import datetime, timezone, timedelta

JELLYFIN_URL = "http://jellyfin.media.svc.cluster.local:8096"
API_KEY = "your-api-key"
USER_ID = "user-id-123"
GRACE_PERIOD_DAYS = 5

# Get watched movies
response = requests.get(
    f"{JELLYFIN_URL}/Users/{USER_ID}/Items",
    params={
        "IncludeItemTypes": "Movie",
        "Recursive": "true",
        "IsPlayed": "true",
        "Fields": "Path,ProviderIds,UserData,DateCreated"
    },
    headers={"X-Emby-Token": API_KEY}
)

movies = response.json()['Items']
cutoff_date = datetime.now(timezone.utc) - timedelta(days=GRACE_PERIOD_DAYS)

for movie in movies:
    last_played = datetime.fromisoformat(
        movie['UserData']['LastPlayedDate'].replace('Z', '+00:00')
    )
    
    if last_played < cutoff_date:
        print(f"Eligible for deletion: {movie['Name']}")
        print(f"  Path: {movie['Path']}")
        print(f"  Last played: {last_played}")
        print(f"  TMDb ID: {movie['ProviderIds'].get('Tmdb', 'N/A')}")
```

---

## Data Model Summary

| Field | Purpose |
|-------|---------|
| `Id` | Jellyfin internal ID |
| `Name` | Movie/Episode title |
| `Path` | **Primary key for mapping to *arr** |
| `ProviderIds.Tmdb` | Secondary key for Radarr |
| `ProviderIds.Tvdb` | Secondary key for Sonarr |
| `UserData.Played` | Watched status (boolean) |
| `UserData.LastPlayedDate` | ISO 8601 timestamp |
| `UserData.PlayCount` | Number of plays |
| `SeriesId` | Group episodes (TV only) |
| `ParentIndexNumber` | Season number (TV only) |
| `IndexNumber` | Episode number (TV only) |
