#!/usr/bin/env python3
"""
Media ID Mapper - Map Jellyfin items to Radarr/Sonarr IDs

This script provides mapping functionality between Jellyfin library items
and their corresponding Radarr/Sonarr database entries for cleanup operations.
"""

import requests
from typing import Optional, Dict, List
from urllib.parse import unquote
import logging

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


class MediaMapper:
    """Maps Jellyfin library items to Radarr/Sonarr database IDs"""
    
    def __init__(
        self,
        jellyfin_url: str,
        jellyfin_key: str,
        radarr_url: str,
        radarr_key: str,
        sonarr_url: str,
        sonarr_key: str,
        jellyfin_movie_prefix: str = "/media/movies/",
        jellyfin_tv_prefix: str = "/media/tv/",
        radarr_prefix: str = "/movies/",
        sonarr_prefix: str = "/tv/"
    ):
        self.jellyfin_url = jellyfin_url.rstrip('/')
        self.jellyfin_key = jellyfin_key
        self.radarr_url = radarr_url.rstrip('/')
        self.radarr_key = radarr_key
        self.sonarr_url = sonarr_url.rstrip('/')
        self.sonarr_key = sonarr_key
        
        # Path prefixes for normalization
        self.jellyfin_movie_prefix = jellyfin_movie_prefix
        self.jellyfin_tv_prefix = jellyfin_tv_prefix
        self.radarr_prefix = radarr_prefix
        self.sonarr_prefix = sonarr_prefix
        
        # Caches
        self._movies_cache: Optional[Dict[int, dict]] = None
        self._series_cache: Optional[Dict[int, dict]] = None
        self._episodes_cache: Dict[int, List[dict]] = {}
        self._tags_cache: Dict[str, Dict[int, str]] = {}  # {service: {tag_id: label}}
    
    def _normalize_path(self, path: str, prefix_to_remove: str) -> str:
        """
        Normalize file path for comparison
        - Remove mount prefix
        - Decode URL encoding
        - Remove leading/trailing slashes
        """
        normalized = path.replace(prefix_to_remove, "")
        normalized = unquote(normalized)
        return normalized.strip("/")
    
    # ========== Jellyfin Methods ==========
    
    def get_jellyfin_users(self) -> List[dict]:
        """Get all Jellyfin users"""
        response = requests.get(
            f"{self.jellyfin_url}/Users",
            headers={"X-Emby-Token": self.jellyfin_key}
        )
        response.raise_for_status()
        return response.json()
    
    def get_jellyfin_watched_movies(self, user_id: str) -> List[dict]:
        """Get all watched movies for a user"""
        response = requests.get(
            f"{self.jellyfin_url}/Users/{user_id}/Items",
            params={
                "IncludeItemTypes": "Movie",
                "Recursive": "true",
                "IsPlayed": "true",
                "Fields": "Path,ProviderIds,UserData,DateCreated"
            },
            headers={"X-Emby-Token": self.jellyfin_key}
        )
        response.raise_for_status()
        return response.json()['Items']
    
    def get_jellyfin_watched_episodes(self, user_id: str) -> List[dict]:
        """Get all watched episodes for a user"""
        response = requests.get(
            f"{self.jellyfin_url}/Users/{user_id}/Items",
            params={
                "IncludeItemTypes": "Episode",
                "Recursive": "true",
                "IsPlayed": "true",
                "Fields": "Path,ProviderIds,UserData,DateCreated,SeasonId,SeriesId,IndexNumber,ParentIndexNumber,SeriesName"
            },
            headers={"X-Emby-Token": self.jellyfin_key}
        )
        response.raise_for_status()
        return response.json()['Items']
    
    # ========== Radarr Methods ==========
    
    def _get_all_radarr_movies(self) -> Dict[int, dict]:
        """Get all movies from Radarr (cached)"""
        if self._movies_cache is None:
            response = requests.get(
                f"{self.radarr_url}/api/v3/movie",
                headers={"X-Api-Key": self.radarr_key}
            )
            response.raise_for_status()
            movies = response.json()
            self._movies_cache = {
                m['id']: m for m in movies if m.get('hasFile')
            }
            logger.info(f"Cached {len(self._movies_cache)} Radarr movies")
        return self._movies_cache
    
    def get_radarr_tags(self) -> Dict[int, str]:
        """Get Radarr tags (cached)"""
        if 'radarr' not in self._tags_cache:
            response = requests.get(
                f"{self.radarr_url}/api/v3/tag",
                headers={"X-Api-Key": self.radarr_key}
            )
            response.raise_for_status()
            tags = response.json()
            self._tags_cache['radarr'] = {tag['id']: tag['label'] for tag in tags}
            logger.info(f"Cached {len(self._tags_cache['radarr'])} Radarr tags")
        return self._tags_cache['radarr']
    
    def find_radarr_movie_by_path(self, jellyfin_path: str) -> Optional[int]:
        """
        Map Jellyfin movie path to Radarr movie ID
        Returns: Radarr movie ID or None
        """
        movies = self._get_all_radarr_movies()
        jellyfin_norm = self._normalize_path(jellyfin_path, self.jellyfin_movie_prefix)
        
        for movie_id, movie in movies.items():
            if not movie.get('hasFile'):
                continue
            
            radarr_norm = self._normalize_path(
                movie['movieFile']['path'],
                self.radarr_prefix
            )
            
            if jellyfin_norm == radarr_norm:
                logger.debug(f"Matched movie: {movie['title']} (ID: {movie_id})")
                return movie_id
        
        logger.warning(f"No Radarr match for path: {jellyfin_path}")
        return None
    
    def find_radarr_movie_by_tmdb(self, tmdb_id: str) -> Optional[int]:
        """
        Map Jellyfin TMDb ID to Radarr movie ID (fallback method)
        Returns: Radarr movie ID or None
        """
        movies = self._get_all_radarr_movies()
        
        for movie_id, movie in movies.items():
            if str(movie.get('tmdbId')) == str(tmdb_id):
                logger.debug(f"Matched movie by TMDb: {movie['title']} (ID: {movie_id})")
                return movie_id
        
        logger.warning(f"No Radarr match for TMDb ID: {tmdb_id}")
        return None
    
    def delete_radarr_movie(self, movie_id: int, dry_run: bool = True) -> bool:
        """
        Delete movie from Radarr (and disk)
        Returns: True if successful
        """
        if dry_run:
            logger.info(f"[DRY RUN] Would delete Radarr movie ID: {movie_id}")
            return True
        
        response = requests.delete(
            f"{self.radarr_url}/api/v3/movie/{movie_id}",
            params={
                "deleteFiles": "true",
                "addImportExclusion": "false"
            },
            headers={"X-Api-Key": self.radarr_key}
        )
        
        if response.status_code == 200:
            logger.info(f"Deleted Radarr movie ID: {movie_id}")
            # Invalidate cache
            if self._movies_cache and movie_id in self._movies_cache:
                del self._movies_cache[movie_id]
            return True
        else:
            logger.error(f"Failed to delete movie {movie_id}: {response.status_code} - {response.text}")
            return False
    
    # ========== Sonarr Methods ==========
    
    def _get_all_sonarr_series(self) -> Dict[int, dict]:
        """Get all series from Sonarr (cached)"""
        if self._series_cache is None:
            response = requests.get(
                f"{self.sonarr_url}/api/v3/series",
                headers={"X-Api-Key": self.sonarr_key}
            )
            response.raise_for_status()
            series = response.json()
            self._series_cache = {s['id']: s for s in series}
            logger.info(f"Cached {len(self._series_cache)} Sonarr series")
        return self._series_cache
    
    def _get_sonarr_episodes(self, series_id: int) -> List[dict]:
        """Get episodes for a series (cached)"""
        if series_id not in self._episodes_cache:
            response = requests.get(
                f"{self.sonarr_url}/api/v3/episode",
                params={"seriesId": series_id},
                headers={"X-Api-Key": self.sonarr_key}
            )
            response.raise_for_status()
            episodes = response.json()
            self._episodes_cache[series_id] = [
                ep for ep in episodes if ep.get('hasFile')
            ]
            logger.debug(f"Cached {len(self._episodes_cache[series_id])} episodes for series {series_id}")
        return self._episodes_cache[series_id]
    
    def get_sonarr_tags(self) -> Dict[int, str]:
        """Get Sonarr tags (cached)"""
        if 'sonarr' not in self._tags_cache:
            response = requests.get(
                f"{self.sonarr_url}/api/v3/tag",
                headers={"X-Api-Key": self.sonarr_key}
            )
            response.raise_for_status()
            tags = response.json()
            self._tags_cache['sonarr'] = {tag['id']: tag['label'] for tag in tags}
            logger.info(f"Cached {len(self._tags_cache['sonarr'])} Sonarr tags")
        return self._tags_cache['sonarr']
    
    def find_sonarr_episode_by_path(self, jellyfin_path: str, series_id: Optional[int] = None) -> Optional[int]:
        """
        Map Jellyfin episode path to Sonarr episode file ID
        
        Args:
            jellyfin_path: Full path from Jellyfin
            series_id: Optional series ID to narrow search
        
        Returns: Sonarr episode file ID or None
        """
        jellyfin_norm = self._normalize_path(jellyfin_path, self.jellyfin_tv_prefix)
        
        # If series_id provided, only search that series
        if series_id:
            series_to_search = [series_id]
        else:
            # Search all series
            series_to_search = self._get_all_sonarr_series().keys()
        
        for sid in series_to_search:
            episodes = self._get_sonarr_episodes(sid)
            
            for episode in episodes:
                if not episode.get('hasFile'):
                    continue
                
                sonarr_norm = self._normalize_path(
                    episode['episodeFile']['path'],
                    self.sonarr_prefix
                )
                
                if jellyfin_norm == sonarr_norm:
                    episode_file_id = episode['episodeFileId']
                    logger.debug(f"Matched episode: S{episode['seasonNumber']:02d}E{episode['episodeNumber']:02d} (File ID: {episode_file_id})")
                    return episode_file_id
        
        logger.warning(f"No Sonarr match for path: {jellyfin_path}")
        return None
    
    def delete_sonarr_episode_file(self, episode_file_id: int, dry_run: bool = True) -> bool:
        """
        Delete episode file from Sonarr (and disk)
        Returns: True if successful
        """
        if dry_run:
            logger.info(f"[DRY RUN] Would delete Sonarr episode file ID: {episode_file_id}")
            return True
        
        response = requests.delete(
            f"{self.sonarr_url}/api/v3/episodefile/{episode_file_id}",
            headers={"X-Api-Key": self.sonarr_key}
        )
        
        if response.status_code == 200:
            logger.info(f"Deleted Sonarr episode file ID: {episode_file_id}")
            # Invalidate cache for affected series
            self._episodes_cache.clear()
            return True
        else:
            logger.error(f"Failed to delete episode file {episode_file_id}: {response.status_code} - {response.text}")
            return False
    
    def bulk_delete_sonarr_episode_files(self, episode_file_ids: List[int], dry_run: bool = True) -> bool:
        """
        Bulk delete episode files from Sonarr
        Returns: True if successful
        """
        if dry_run:
            logger.info(f"[DRY RUN] Would bulk delete {len(episode_file_ids)} Sonarr episode files: {episode_file_ids}")
            return True
        
        response = requests.delete(
            f"{self.sonarr_url}/api/v3/episodefile/bulk",
            json={"episodeFileIds": episode_file_ids},
            headers={
                "X-Api-Key": self.sonarr_key,
                "Content-Type": "application/json"
            }
        )
        
        if response.status_code == 200:
            logger.info(f"Bulk deleted {len(episode_file_ids)} episode files")
            # Invalidate cache
            self._episodes_cache.clear()
            return True
        else:
            logger.error(f"Failed to bulk delete: {response.status_code} - {response.text}")
            return False


# ========== Example Usage ==========

if __name__ == "__main__":
    import os
    
    # Environment variables (set these in production)
    JELLYFIN_URL = os.getenv("JELLYFIN_URL", "http://jellyfin.media.svc.cluster.local:8096")
    JELLYFIN_API_KEY = os.getenv("JELLYFIN_API_KEY", "your-jellyfin-api-key")
    RADARR_URL = os.getenv("RADARR_URL", "http://radarr.media.svc.cluster.local:7878")
    RADARR_API_KEY = os.getenv("RADARR_API_KEY", "your-radarr-api-key")
    SONARR_URL = os.getenv("SONARR_URL", "http://sonarr.media.svc.cluster.local:8989")
    SONARR_API_KEY = os.getenv("SONARR_API_KEY", "your-sonarr-api-key")
    
    # Initialize mapper
    mapper = MediaMapper(
        jellyfin_url=JELLYFIN_URL,
        jellyfin_key=JELLYFIN_API_KEY,
        radarr_url=RADARR_URL,
        radarr_key=RADARR_API_KEY,
        sonarr_url=SONARR_URL,
        sonarr_key=SONARR_API_KEY
    )
    
    # Example: Find Radarr movie from Jellyfin path
    movie_path = "/media/movies/The Matrix (1999)/The Matrix (1999) REMUX-2160p.mkv"
    radarr_movie_id = mapper.find_radarr_movie_by_path(movie_path)
    
    if radarr_movie_id:
        print(f"Found Radarr movie ID: {radarr_movie_id}")
        # mapper.delete_radarr_movie(radarr_movie_id, dry_run=True)
    
    # Example: Find Sonarr episode from Jellyfin path
    episode_path = "/media/tv/Breaking Bad/Season 01/Breaking Bad - S01E01 - Pilot REMUX-2160p.mkv"
    sonarr_episode_file_id = mapper.find_sonarr_episode_by_path(episode_path)
    
    if sonarr_episode_file_id:
        print(f"Found Sonarr episode file ID: {sonarr_episode_file_id}")
        # mapper.delete_sonarr_episode_file(sonarr_episode_file_id, dry_run=True)
