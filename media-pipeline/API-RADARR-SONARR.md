# Radarr & Sonarr API Reference

## Authentication
Both Radarr and Sonarr use API key header authentication:
```
X-Api-Key: YOUR_API_KEY
```

## Base URLs
```
Radarr: http://radarr.media.svc.cluster.local:7878/api/v3
Sonarr: http://sonarr.media.svc.cluster.local:8989/api/v3
```

---

# Radarr API

## 1. Get All Movies

**Endpoint**:
```
GET /api/v3/movie
```

**Response**:
```json
[
  {
    "id": 1,
    "title": "The Matrix",
    "year": 1999,
    "path": "/movies/The Matrix (1999)",
    "qualityProfileId": 1,
    "monitored": true,
    "tmdbId": 603,
    "imdbId": "tt0133093",
    "tags": [],
    "hasFile": true,
    "movieFile": {
      "id": 123,
      "movieId": 1,
      "relativePath": "The Matrix (1999) REMUX-2160p.mkv",
      "path": "/movies/The Matrix (1999)/The Matrix (1999) REMUX-2160p.mkv",
      "size": 68719476736,
      "dateAdded": "2025-12-15T10:30:00Z",
      "quality": {
        "quality": {
          "id": 20,
          "name": "Bluray-2160p Remux"
        }
      },
      "mediaInfo": {
        "audioChannels": 7.1,
        "audioCodec": "TrueHD",
        "audioLanguages": "eng",
        "subtitles": "eng",
        "videoCodec": "HEVC",
        "videoBitDepth": 10,
        "videoBitrate": 0,
        "videoFps": 23.976,
        "resolution": "2160p"
      }
    }
  }
]
```

**Key Fields**:
- `id`: Radarr internal movie ID (needed for deletion)
- `path`: Directory path
- `tags`: Array of tag IDs (use for "keep" logic)
- `hasFile`: Whether movie file exists
- `movieFile.path`: **Full file path (for Jellyfin mapping)**
- `movieFile.dateAdded`: When file was imported
- `movieFile.mediaInfo.audioCodec`: Verify TrueHD/DTS-HD

---

## 2. Get Movie by ID

**Endpoint**:
```
GET /api/v3/movie/{id}
```

---

## 3. Get Movie by TMDb ID

Useful for mapping from Jellyfin TMDb ID.

**Endpoint**:
```
GET /api/v3/movie?tmdbId={tmdbId}
```

**Example**:
```
GET /api/v3/movie?tmdbId=603
```

---

## 4. Get Movie by File Path

Not directly supported, use Python to match path from "Get All Movies".

**Python Helper**:
```python
def get_movie_by_path(radarr_url, api_key, file_path):
    response = requests.get(
        f"{radarr_url}/api/v3/movie",
        headers={"X-Api-Key": api_key}
    )
    movies = response.json()
    
    for movie in movies:
        if movie.get('hasFile') and movie['movieFile']['path'] == file_path:
            return movie
    return None
```

---

## 5. Delete Movie (WITH FILES)

**CRITICAL**: Must delete via Radarr API, not filesystem directly. This ensures database consistency.

**Endpoint**:
```
DELETE /api/v3/movie/{id}?deleteFiles=true&addImportExclusion=false
```

**Query Parameters**:
- `deleteFiles=true`: Delete movie file from disk
- `addImportExclusion=false`: Allow re-downloading if needed

**Example**:
```bash
curl -X DELETE \
  "http://radarr.media.svc.cluster.local:7878/api/v3/movie/1?deleteFiles=true&addImportExclusion=false" \
  -H "X-Api-Key: YOUR_API_KEY"
```

**Python Example**:
```python
def delete_movie(radarr_url, api_key, movie_id):
    response = requests.delete(
        f"{radarr_url}/api/v3/movie/{movie_id}",
        params={
            "deleteFiles": "true",
            "addImportExclusion": "false"
        },
        headers={"X-Api-Key": api_key}
    )
    return response.status_code == 200
```

---

## 6. Get Tags

Used to identify "keep" tag.

**Endpoint**:
```
GET /api/v3/tag
```

**Response**:
```json
[
  {
    "id": 1,
    "label": "keep"
  },
  {
    "id": 2,
    "label": "documentary"
  }
]
```

**Usage**:
```python
# Get tag ID for "keep"
response = requests.get(f"{radarr_url}/api/v3/tag", headers={"X-Api-Key": api_key})
tags = response.json()
keep_tag_id = next((tag['id'] for tag in tags if tag['label'] == 'keep'), None)

# Check if movie has keep tag
if keep_tag_id in movie['tags']:
    print("Skipping - movie is tagged 'keep'")
```

---

# Sonarr API

## 1. Get All Series

**Endpoint**:
```
GET /api/v3/series
```

**Response**:
```json
[
  {
    "id": 1,
    "title": "Breaking Bad",
    "year": 2008,
    "path": "/tv/Breaking Bad",
    "qualityProfileId": 1,
    "monitored": true,
    "tvdbId": 81189,
    "imdbId": "tt0903747",
    "tags": [],
    "seasons": [
      {
        "seasonNumber": 1,
        "monitored": true,
        "statistics": {
          "episodeFileCount": 7,
          "episodeCount": 7,
          "totalEpisodeCount": 7,
          "sizeOnDisk": 214748364800,
          "percentOfEpisodes": 100.0
        }
      }
    ],
    "statistics": {
      "seasonCount": 5,
      "episodeFileCount": 62,
      "episodeCount": 62,
      "totalEpisodeCount": 62,
      "sizeOnDisk": 2147483648000,
      "percentOfEpisodes": 100.0
    }
  }
]
```

**Key Fields**:
- `id`: Sonarr series ID
- `path`: Series directory
- `tags`: Tag IDs
- `seasons[].statistics.episodeFileCount`: Episodes downloaded per season
- `statistics.episodeFileCount`: Total episodes downloaded

---

## 2. Get Episodes for Series

**Endpoint**:
```
GET /api/v3/episode?seriesId={seriesId}
```

**Response**:
```json
[
  {
    "id": 1,
    "seriesId": 1,
    "episodeFileId": 123,
    "seasonNumber": 1,
    "episodeNumber": 1,
    "title": "Pilot",
    "airDate": "2008-01-20",
    "hasFile": true,
    "monitored": true,
    "episodeFile": {
      "id": 123,
      "seriesId": 1,
      "seasonNumber": 1,
      "relativePath": "Season 01/Breaking Bad - S01E01 - Pilot REMUX-2160p.mkv",
      "path": "/tv/Breaking Bad/Season 01/Breaking Bad - S01E01 - Pilot REMUX-2160p.mkv",
      "size": 30736613376,
      "dateAdded": "2025-12-10T08:00:00Z",
      "quality": {
        "quality": {
          "id": 20,
          "name": "Bluray-2160p Remux"
        }
      },
      "mediaInfo": {
        "audioChannels": 7.1,
        "audioCodec": "TrueHD",
        "audioLanguages": "eng"
      }
    }
  }
]
```

**Key Fields**:
- `id`: Episode ID (NOT used for deletion)
- `episodeFileId`: Episode file ID (**NEEDED for deletion**)
- `seasonNumber`: Season number
- `episodeNumber`: Episode number
- `hasFile`: Whether episode file exists
- `episodeFile.path`: **Full file path (for Jellyfin mapping)**

---

## 3. Get Episode File by ID

**Endpoint**:
```
GET /api/v3/episodefile/{id}
```

---

## 4. Delete Episode File

**CRITICAL**: Delete episode file, not episode metadata.

**Endpoint**:
```
DELETE /api/v3/episodefile/{episodeFileId}
```

**Example**:
```bash
curl -X DELETE \
  "http://sonarr.media.svc.cluster.local:8989/api/v3/episodefile/123" \
  -H "X-Api-Key: YOUR_API_KEY"
```

**Python Example**:
```python
def delete_episode_file(sonarr_url, api_key, episode_file_id):
    response = requests.delete(
        f"{sonarr_url}/api/v3/episodefile/{episode_file_id}",
        headers={"X-Api-Key": api_key}
    )
    return response.status_code == 200
```

---

## 5. Bulk Delete Episode Files

For deleting entire season or multiple episodes.

**Endpoint**:
```
DELETE /api/v3/episodefile/bulk
```

**Request Body**:
```json
{
  "episodeFileIds": [123, 124, 125, 126, 127, 128, 129]
}
```

**Example**:
```bash
curl -X DELETE \
  "http://sonarr.media.svc.cluster.local:8989/api/v3/episodefile/bulk" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"episodeFileIds": [123, 124, 125]}'
```

**Python Example**:
```python
def bulk_delete_episode_files(sonarr_url, api_key, episode_file_ids):
    response = requests.delete(
        f"{sonarr_url}/api/v3/episodefile/bulk",
        json={"episodeFileIds": episode_file_ids},
        headers={
            "X-Api-Key": api_key,
            "Content-Type": "application/json"
        }
    )
    return response.status_code == 200
```

---

## 6. Delete Entire Series (WITH FILES)

**Endpoint**:
```
DELETE /api/v3/series/{id}?deleteFiles=true&addImportListExclusion=false
```

**Query Parameters**:
- `deleteFiles=true`: Delete all files
- `addImportListExclusion=false`: Don't block from re-importing

---

## 7. Get Tags (Same as Radarr)

**Endpoint**:
```
GET /api/v3/tag
```

---

# Data Mapping Strategy

## Jellyfin → Radarr (Movies)

**Primary Method: File Path Matching**
```python
# From Jellyfin
jellyfin_path = "/media/movies/The Matrix (1999)/The Matrix (1999) REMUX-2160p.mkv"

# From Radarr
radarr_path = "/movies/The Matrix (1999)/The Matrix (1999) REMUX-2160p.mkv"

# Comparison (normalize paths)
jellyfin_normalized = jellyfin_path.replace("/media/movies/", "")
radarr_normalized = radarr_path.replace("/movies/", "")
# Both become: "The Matrix (1999)/The Matrix (1999) REMUX-2160p.mkv"

if jellyfin_normalized == radarr_normalized:
    # Match found
    pass
```

**Fallback Method: TMDb ID Matching**
```python
# From Jellyfin
tmdb_id = item['ProviderIds']['Tmdb']  # "603"

# Query Radarr
radarr_movie = get_movie_by_tmdb(radarr_url, api_key, tmdb_id)
```

---

## Jellyfin → Sonarr (Episodes)

**Primary Method: File Path Matching**
```python
# From Jellyfin
jellyfin_path = "/media/tv/Breaking Bad/Season 01/Breaking Bad - S01E01 - Pilot REMUX-2160p.mkv"

# From Sonarr episodes
sonarr_path = "/tv/Breaking Bad/Season 01/Breaking Bad - S01E01 - Pilot REMUX-2160p.mkv"

# Normalize and compare
jellyfin_normalized = jellyfin_path.replace("/media/tv/", "")
sonarr_normalized = sonarr_path.replace("/tv/", "")

if jellyfin_normalized == sonarr_normalized:
    # Match found - get episodeFileId for deletion
    episode_file_id = episode['episodeFileId']
```

**Fallback Method: Series + Season + Episode Number**
```python
# From Jellyfin
tvdb_id = item['ProviderIds']['Tvdb']
season_num = item['ParentIndexNumber']
episode_num = item['IndexNumber']

# Get Sonarr series by TVDb ID
series = get_series_by_tvdb(sonarr_url, api_key, tvdb_id)

# Get episodes for series
episodes = get_episodes(sonarr_url, api_key, series['id'])

# Find matching episode
episode = next(
    (ep for ep in episodes 
     if ep['seasonNumber'] == season_num 
     and ep['episodeNumber'] == episode_num),
    None
)
```

---

# Complete Mapping Example

```python
import requests
from urllib.parse import unquote

class MediaMapper:
    def __init__(self, radarr_url, radarr_key, sonarr_url, sonarr_key):
        self.radarr_url = radarr_url
        self.radarr_key = radarr_key
        self.sonarr_url = sonarr_url
        self.sonarr_key = sonarr_key
        
        # Cache all movies and episodes on init
        self.movies = self._get_all_movies()
        self.series_episodes = {}  # {series_id: [episodes]}
    
    def _get_all_movies(self):
        response = requests.get(
            f"{self.radarr_url}/api/v3/movie",
            headers={"X-Api-Key": self.radarr_key}
        )
        return {m['id']: m for m in response.json() if m.get('hasFile')}
    
    def _get_episodes(self, series_id):
        if series_id not in self.series_episodes:
            response = requests.get(
                f"{self.sonarr_url}/api/v3/episode",
                params={"seriesId": series_id},
                headers={"X-Api-Key": self.sonarr_key}
            )
            self.series_episodes[series_id] = [
                ep for ep in response.json() if ep.get('hasFile')
            ]
        return self.series_episodes[series_id]
    
    def normalize_path(self, path, prefix_to_remove):
        """Remove mount prefix and decode URL encoding"""
        normalized = path.replace(prefix_to_remove, "")
        normalized = unquote(normalized)  # Handle URL encoding
        return normalized.strip("/")
    
    def find_movie_by_path(self, jellyfin_path):
        """Map Jellyfin movie to Radarr movie ID"""
        jellyfin_norm = self.normalize_path(jellyfin_path, "/media/movies/")
        
        for movie_id, movie in self.movies.items():
            if not movie.get('hasFile'):
                continue
            
            radarr_norm = self.normalize_path(
                movie['movieFile']['path'], 
                "/movies/"
            )
            
            if jellyfin_norm == radarr_norm:
                return movie_id
        
        return None
    
    def find_episode_by_path(self, jellyfin_path, series_id):
        """Map Jellyfin episode to Sonarr episode file ID"""
        jellyfin_norm = self.normalize_path(jellyfin_path, "/media/tv/")
        episodes = self._get_episodes(series_id)
        
        for episode in episodes:
            if not episode.get('hasFile'):
                continue
            
            sonarr_norm = self.normalize_path(
                episode['episodeFile']['path'],
                "/tv/"
            )
            
            if jellyfin_norm == sonarr_norm:
                return episode['episodeFileId']
        
        return None

# Usage
mapper = MediaMapper(
    radarr_url="http://radarr.media.svc.cluster.local:7878",
    radarr_key="your-radarr-key",
    sonarr_url="http://sonarr.media.svc.cluster.local:8989",
    sonarr_key="your-sonarr-key"
)

# Map Jellyfin movie
movie_id = mapper.find_movie_by_path(
    "/media/movies/The Matrix (1999)/The Matrix (1999) REMUX-2160p.mkv"
)

# Map Jellyfin episode
episode_file_id = mapper.find_episode_by_path(
    "/media/tv/Breaking Bad/Season 01/Breaking Bad - S01E01 - Pilot REMUX-2160p.mkv",
    series_id=1
)
```
