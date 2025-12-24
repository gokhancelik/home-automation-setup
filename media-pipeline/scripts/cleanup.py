#!/usr/bin/env python3
"""
Media Cleanup Script - Automated deletion of watched content

This script implements the core cleanup logic:
1. Query Jellyfin for watched movies and episodes
2. Apply grace period filter (default: 5 days since last watched)
3. Check for "keep" tags in Radarr/Sonarr
4. Map Jellyfin items to Radarr/Sonarr IDs
5. Delete via Radarr/Sonarr APIs (preserves database integrity)

Features:
- Dry-run mode (default)
- Idempotent (safe to run multiple times)
- Comprehensive logging
- Tag-based exclusions
- Configurable grace period
"""

import os
import sys
import logging
from datetime import datetime, timezone, timedelta
from typing import List, Dict, Tuple
from dataclasses import dataclass, field

# Add parent directory to path for imports
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from scripts.media_mapper import MediaMapper

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(),
        logging.FileHandler('/var/log/media-cleanup.log') if os.path.exists('/var/log') else logging.NullHandler()
    ]
)
logger = logging.getLogger(__name__)


@dataclass
class CleanupConfig:
    """Configuration for cleanup job"""
    grace_period_days: int = 5
    dry_run: bool = True
    keep_tag_label: str = "keep"
    primary_user_name: str = "admin"  # Only track this user's watched state
    
    # API credentials
    jellyfin_url: str = ""
    jellyfin_api_key: str = ""
    radarr_url: str = ""
    radarr_api_key: str = ""
    sonarr_url: str = ""
    sonarr_api_key: str = ""
    
    # Path prefixes
    jellyfin_movie_prefix: str = "/media/movies/"
    jellyfin_tv_prefix: str = "/media/tv/"
    radarr_prefix: str = "/movies/"
    sonarr_prefix: str = "/tv/"
    
    @classmethod
    def from_env(cls) -> 'CleanupConfig':
        """Load configuration from environment variables"""
        return cls(
            grace_period_days=int(os.getenv("GRACE_PERIOD_DAYS", "5")),
            dry_run=os.getenv("DRY_RUN", "true").lower() == "true",
            keep_tag_label=os.getenv("KEEP_TAG_LABEL", "keep"),
            primary_user_name=os.getenv("PRIMARY_USER_NAME", "admin"),
            jellyfin_url=os.getenv("JELLYFIN_URL", "http://jellyfin.media.svc.cluster.local:8096"),
            jellyfin_api_key=os.getenv("JELLYFIN_API_KEY", ""),
            radarr_url=os.getenv("RADARR_URL", "http://radarr.media.svc.cluster.local:7878"),
            radarr_api_key=os.getenv("RADARR_API_KEY", ""),
            sonarr_url=os.getenv("SONARR_URL", "http://sonarr.media.svc.cluster.local:8989"),
            sonarr_api_key=os.getenv("SONARR_API_KEY", ""),
            jellyfin_movie_prefix=os.getenv("JELLYFIN_MOVIE_PREFIX", "/media/movies/"),
            jellyfin_tv_prefix=os.getenv("JELLYFIN_TV_PREFIX", "/media/tv/"),
            radarr_prefix=os.getenv("RADARR_PREFIX", "/movies/"),
            sonarr_prefix=os.getenv("SONARR_PREFIX", "/tv/")
        )


@dataclass
class CleanupStats:
    """Statistics for cleanup run"""
    movies_watched: int = 0
    movies_eligible: int = 0
    movies_skipped_tag: int = 0
    movies_skipped_not_found: int = 0
    movies_deleted: int = 0
    movies_failed: int = 0
    
    episodes_watched: int = 0
    episodes_eligible: int = 0
    episodes_skipped_tag: int = 0
    episodes_skipped_not_found: int = 0
    episodes_deleted: int = 0
    episodes_failed: int = 0
    
    def print_summary(self):
        """Print cleanup statistics"""
        logger.info("=" * 60)
        logger.info("CLEANUP SUMMARY")
        logger.info("=" * 60)
        logger.info(f"Movies:")
        logger.info(f"  Watched: {self.movies_watched}")
        logger.info(f"  Eligible for deletion: {self.movies_eligible}")
        logger.info(f"  Skipped (keep tag): {self.movies_skipped_tag}")
        logger.info(f"  Skipped (not found): {self.movies_skipped_not_found}")
        logger.info(f"  Deleted: {self.movies_deleted}")
        logger.info(f"  Failed: {self.movies_failed}")
        logger.info(f"")
        logger.info(f"Episodes:")
        logger.info(f"  Watched: {self.episodes_watched}")
        logger.info(f"  Eligible for deletion: {self.episodes_eligible}")
        logger.info(f"  Skipped (keep tag): {self.episodes_skipped_tag}")
        logger.info(f"  Skipped (not found): {self.episodes_skipped_not_found}")
        logger.info(f"  Deleted: {self.episodes_deleted}")
        logger.info(f"  Failed: {self.episodes_failed}")
        logger.info("=" * 60)


class MediaCleanup:
    """Main cleanup orchestrator"""
    
    def __init__(self, config: CleanupConfig):
        self.config = config
        self.stats = CleanupStats()
        self.mapper = MediaMapper(
            jellyfin_url=config.jellyfin_url,
            jellyfin_key=config.jellyfin_api_key,
            radarr_url=config.radarr_url,
            radarr_key=config.radarr_api_key,
            sonarr_url=config.sonarr_url,
            sonarr_key=config.sonarr_api_key,
            jellyfin_movie_prefix=config.jellyfin_movie_prefix,
            jellyfin_tv_prefix=config.jellyfin_tv_prefix,
            radarr_prefix=config.radarr_prefix,
            sonarr_prefix=config.sonarr_prefix
        )
        
        # Get primary user ID
        self.primary_user_id = self._get_primary_user_id()
        
        # Get keep tag IDs
        self.radarr_keep_tag_id = self._get_keep_tag_id('radarr')
        self.sonarr_keep_tag_id = self._get_keep_tag_id('sonarr')
    
    def _get_primary_user_id(self) -> str:
        """Get Jellyfin user ID for primary user"""
        users = self.mapper.get_jellyfin_users()
        primary_user = next(
            (u for u in users if u['Name'].lower() == self.config.primary_user_name.lower()),
            None
        )
        
        if not primary_user:
            logger.error(f"Primary user '{self.config.primary_user_name}' not found")
            logger.info(f"Available users: {[u['Name'] for u in users]}")
            raise ValueError(f"Primary user not found: {self.config.primary_user_name}")
        
        logger.info(f"Primary user: {primary_user['Name']} (ID: {primary_user['Id']})")
        return primary_user['Id']
    
    def _get_keep_tag_id(self, service: str) -> int:
        """Get 'keep' tag ID for Radarr or Sonarr"""
        if service == 'radarr':
            tags = self.mapper.get_radarr_tags()
        else:
            tags = self.mapper.get_sonarr_tags()
        
        keep_tag_id = next(
            (tag_id for tag_id, label in tags.items() if label.lower() == self.config.keep_tag_label.lower()),
            None
        )
        
        if keep_tag_id:
            logger.info(f"{service.capitalize()} 'keep' tag ID: {keep_tag_id}")
        else:
            logger.warning(f"{service.capitalize()} 'keep' tag not found - all items eligible for deletion")
        
        return keep_tag_id
    
    def _is_past_grace_period(self, last_played_date: str) -> bool:
        """Check if item is past grace period"""
        last_played = datetime.fromisoformat(last_played_date.replace('Z', '+00:00'))
        cutoff_date = datetime.now(timezone.utc) - timedelta(days=self.config.grace_period_days)
        return last_played < cutoff_date
    
    def _has_keep_tag(self, item_tags: List[int], keep_tag_id: int) -> bool:
        """Check if item has keep tag"""
        if keep_tag_id is None:
            return False
        return keep_tag_id in item_tags
    
    def cleanup_movies(self):
        """Clean up watched movies"""
        logger.info("=" * 60)
        logger.info("PHASE: Cleaning up movies")
        logger.info("=" * 60)
        
        # Get watched movies from Jellyfin
        watched_movies = self.mapper.get_jellyfin_watched_movies(self.primary_user_id)
        self.stats.movies_watched = len(watched_movies)
        logger.info(f"Found {self.stats.movies_watched} watched movies")
        
        for movie in watched_movies:
            movie_name = movie['Name']
            movie_path = movie.get('Path', 'unknown')
            last_played = movie['UserData'].get('LastPlayedDate')
            
            if not last_played:
                logger.warning(f"Skipping {movie_name}: No LastPlayedDate")
                continue
            
            # Check grace period
            if not self._is_past_grace_period(last_played):
                days_ago = (datetime.now(timezone.utc) - datetime.fromisoformat(last_played.replace('Z', '+00:00'))).days
                logger.info(f"Skipping {movie_name}: Within grace period ({days_ago} days ago)")
                continue
            
            self.stats.movies_eligible += 1
            days_since_watched = (datetime.now(timezone.utc) - datetime.fromisoformat(last_played.replace('Z', '+00:00'))).days
            
            # Find Radarr movie
            radarr_movie_id = self.mapper.find_radarr_movie_by_path(movie_path)
            
            if not radarr_movie_id:
                # Try fallback: TMDb ID
                tmdb_id = movie.get('ProviderIds', {}).get('Tmdb')
                if tmdb_id:
                    radarr_movie_id = self.mapper.find_radarr_movie_by_tmdb(tmdb_id)
            
            if not radarr_movie_id:
                logger.warning(f"Cannot delete {movie_name}: Not found in Radarr")
                self.stats.movies_skipped_not_found += 1
                continue
            
            # Check for keep tag
            radarr_movies = self.mapper._get_all_radarr_movies()
            radarr_movie = radarr_movies[radarr_movie_id]
            
            if self._has_keep_tag(radarr_movie.get('tags', []), self.radarr_keep_tag_id):
                logger.info(f"Skipping {movie_name}: Has 'keep' tag")
                self.stats.movies_skipped_tag += 1
                continue
            
            # Delete movie
            logger.info(f"Deleting {movie_name} (watched {days_since_watched} days ago)")
            success = self.mapper.delete_radarr_movie(radarr_movie_id, dry_run=self.config.dry_run)
            
            if success:
                self.stats.movies_deleted += 1
            else:
                self.stats.movies_failed += 1
    
    def cleanup_episodes(self):
        """Clean up watched episodes"""
        logger.info("=" * 60)
        logger.info("PHASE: Cleaning up episodes")
        logger.info("=" * 60)
        
        # Get watched episodes from Jellyfin
        watched_episodes = self.mapper.get_jellyfin_watched_episodes(self.primary_user_id)
        self.stats.episodes_watched = len(watched_episodes)
        logger.info(f"Found {self.stats.episodes_watched} watched episodes")
        
        # Group by series for potential bulk deletion
        episodes_by_series: Dict[str, List[dict]] = {}
        for episode in watched_episodes:
            series_name = episode.get('SeriesName', 'Unknown')
            if series_name not in episodes_by_series:
                episodes_by_series[series_name] = []
            episodes_by_series[series_name].append(episode)
        
        logger.info(f"Across {len(episodes_by_series)} series")
        
        for series_name, episodes in episodes_by_series.items():
            logger.info(f"\nProcessing series: {series_name} ({len(episodes)} watched episodes)")
            
            for episode in episodes:
                episode_title = f"{series_name} - S{episode.get('ParentIndexNumber', 0):02d}E{episode.get('IndexNumber', 0):02d}"
                episode_path = episode.get('Path', 'unknown')
                last_played = episode['UserData'].get('LastPlayedDate')
                
                if not last_played:
                    logger.warning(f"Skipping {episode_title}: No LastPlayedDate")
                    continue
                
                # Check grace period
                if not self._is_past_grace_period(last_played):
                    days_ago = (datetime.now(timezone.utc) - datetime.fromisoformat(last_played.replace('Z', '+00:00'))).days
                    logger.debug(f"Skipping {episode_title}: Within grace period ({days_ago} days ago)")
                    continue
                
                self.stats.episodes_eligible += 1
                days_since_watched = (datetime.now(timezone.utc) - datetime.fromisoformat(last_played.replace('Z', '+00:00'))).days
                
                # Find Sonarr episode file
                episode_file_id = self.mapper.find_sonarr_episode_by_path(episode_path)
                
                if not episode_file_id:
                    logger.warning(f"Cannot delete {episode_title}: Not found in Sonarr")
                    self.stats.episodes_skipped_not_found += 1
                    continue
                
                # TODO: Check series/episode for keep tag (requires series lookup)
                # For now, skip tag check on episodes (implement in Phase 4)
                
                # Delete episode
                logger.info(f"Deleting {episode_title} (watched {days_since_watched} days ago)")
                success = self.mapper.delete_sonarr_episode_file(episode_file_id, dry_run=self.config.dry_run)
                
                if success:
                    self.stats.episodes_deleted += 1
                else:
                    self.stats.episodes_failed += 1
    
    def run(self):
        """Execute cleanup"""
        logger.info("=" * 60)
        logger.info("MEDIA CLEANUP STARTING")
        logger.info("=" * 60)
        logger.info(f"Mode: {'DRY RUN' if self.config.dry_run else 'LIVE'}")
        logger.info(f"Grace period: {self.config.grace_period_days} days")
        logger.info(f"Primary user: {self.config.primary_user_name}")
        logger.info(f"Keep tag: '{self.config.keep_tag_label}'")
        logger.info("=" * 60)
        
        try:
            self.cleanup_movies()
            self.cleanup_episodes()
        except Exception as e:
            logger.error(f"Cleanup failed: {e}", exc_info=True)
            raise
        finally:
            self.stats.print_summary()
        
        logger.info("MEDIA CLEANUP COMPLETE")
        return self.stats


def main():
    """Main entry point"""
    # Load configuration
    config = CleanupConfig.from_env()
    
    # Validate configuration
    if not config.jellyfin_api_key:
        logger.error("JELLYFIN_API_KEY environment variable not set")
        sys.exit(1)
    if not config.radarr_api_key:
        logger.error("RADARR_API_KEY environment variable not set")
        sys.exit(1)
    if not config.sonarr_api_key:
        logger.error("SONARR_API_KEY environment variable not set")
        sys.exit(1)
    
    # Run cleanup
    cleanup = MediaCleanup(config)
    stats = cleanup.run()
    
    # Exit with error if any deletions failed (in non-dry-run mode)
    if not config.dry_run and (stats.movies_failed > 0 or stats.episodes_failed > 0):
        sys.exit(1)
    
    sys.exit(0)


if __name__ == "__main__":
    main()
