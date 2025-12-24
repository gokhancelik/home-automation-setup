# Phase 4: Series-Safe Deletion Logic

## Overview
Phase 4 implements intelligent deletion logic for TV series that:
- Deletes only fully watched episodes
- Prefers deleting complete seasons over individual episodes
- Never deletes partially watched seasons
- Handles edge cases (ongoing series, latest season protection)

---

## Core Principles

### 1. Per-Episode Deletion (Default)
Delete individual episodes that have been watched and passed grace period.

**Pros**:
- Granular control
- Immediate space savings
- Simple logic

**Cons**:
- Leaves partial seasons on disk
- May cause season folder clutter
- Less efficient for binge-watched content

### 2. Full-Season Deletion (Preferred)
Only delete when ALL episodes in a season are watched.

**Pros**:
- Cleaner filesystem (removes entire season folders)
- Better for completed/binge-watched series
- More user-friendly (season-level granularity)

**Cons**:
- Slower space reclamation
- Requires aggregating episode state
- Complex edge case handling

### 3. Hybrid Approach (Recommended)
- For completed series: use full-season deletion
- For ongoing series: use per-episode deletion
- Configurable threshold (e.g., delete season if 80%+ watched)

---

## Implementation

### Season Completion Check

**Algorithm**:
```python
def is_season_fully_watched(
    sonarr_url: str,
    api_key: str,
    series_id: int,
    season_number: int,
    jellyfin_watched_episodes: List[dict],
    grace_period_days: int
) -> Tuple[bool, List[int]]:
    """
    Check if entire season is watched and past grace period
    
    Returns:
        (is_fully_watched, episode_file_ids)
    """
    # Get all episodes for this season from Sonarr
    response = requests.get(
        f"{sonarr_url}/api/v3/episode",
        params={"seriesId": series_id},
        headers={"X-Api-Key": api_key}
    )
    all_episodes = response.json()
    
    season_episodes = [
        ep for ep in all_episodes
        if ep['seasonNumber'] == season_number and ep.get('hasFile')
    ]
    
    if not season_episodes:
        return False, []
    
    # Check if each episode is watched in Jellyfin
    episode_file_ids = []
    cutoff_date = datetime.now(timezone.utc) - timedelta(days=grace_period_days)
    
    for episode in season_episodes:
        # Find matching Jellyfin episode
        jellyfin_episode = next(
            (je for je in jellyfin_watched_episodes
             if je.get('ParentIndexNumber') == season_number
             and je.get('IndexNumber') == episode['episodeNumber']),
            None
        )
        
        if not jellyfin_episode:
            # Episode not watched
            return False, []
        
        # Check grace period
        last_played = datetime.fromisoformat(
            jellyfin_episode['UserData']['LastPlayedDate'].replace('Z', '+00:00')
        )
        
        if last_played >= cutoff_date:
            # Episode still in grace period
            return False, []
        
        episode_file_ids.append(episode['episodeFileId'])
    
    # All episodes watched and past grace period
    return True, episode_file_ids
```

**Usage**:
```python
# Check if Season 1 is fully watched
is_complete, file_ids = is_season_fully_watched(
    sonarr_url="http://sonarr.media.svc.cluster.local:8989",
    api_key="your-api-key",
    series_id=1,
    season_number=1,
    jellyfin_watched_episodes=watched_episodes,
    grace_period_days=5
)

if is_complete:
    # Bulk delete entire season
    bulk_delete_episode_files(sonarr_url, api_key, file_ids)
```

---

## Edge Cases

### 1. Ongoing Series (Latest Season Protection)

**Problem**: Latest season may still have unwatched episodes airing.

**Solution**: Never delete current/latest season automatically.

```python
def should_protect_season(
    series: dict,
    season_number: int,
    current_date: datetime
) -> bool:
    """
    Protect latest season of ongoing series
    """
    # Get series status
    if series['status'] != 'continuing':
        # Series ended, no protection needed
        return False
    
    # Get latest season number
    latest_season = max(s['seasonNumber'] for s in series['seasons'])
    
    if season_number == latest_season:
        # Protect latest season of ongoing series
        return True
    
    return False
```

### 2. Partially Watched Seasons

**Problem**: User watched S01E01-E05 but not E06-E10.

**Solution**: Use threshold-based deletion.

```python
def should_delete_partial_season(
    watched_count: int,
    total_count: int,
    threshold_percent: float = 0.8
) -> bool:
    """
    Delete season if >= threshold% watched
    Default: 80% watched = delete entire season
    """
    if total_count == 0:
        return False
    
    watched_percent = watched_count / total_count
    return watched_percent >= threshold_percent
```

**Configuration**:
```yaml
# In cleanup-config ConfigMap
PARTIAL_SEASON_THRESHOLD: "0.8"  # 80% watched = delete all
```

### 3. Multi-User Edge Case

**Problem**: User A watched all episodes, User B only watched half.

**Solution**: Use primary user strategy (Phase 2). Only track one user's state.

Alternatively, implement "any user" logic:
```python
def is_episode_watched_by_any_user(
    mapper: MediaMapper,
    episode: dict,
    users: List[str]
) -> Tuple[bool, str]:
    """
    Check if episode watched by any user
    Returns: (is_watched, last_played_date)
    """
    most_recent_play = None
    
    for user_id in users:
        watched_episodes = mapper.get_jellyfin_watched_episodes(user_id)
        
        match = next(
            (je for je in watched_episodes
             if je.get('Path') == episode.get('Path')),
            None
        )
        
        if match:
            last_played = match['UserData']['LastPlayedDate']
            if most_recent_play is None or last_played > most_recent_play:
                most_recent_play = last_played
    
    return most_recent_play is not None, most_recent_play
```

### 4. Specials/Season 0

**Problem**: Specials (Season 0) may not follow normal season structure.

**Solution**: Treat specials as individual episodes, never bulk delete.

```python
if season_number == 0:
    # Use per-episode deletion for specials
    deletion_mode = "per-episode"
```

---

## Concrete Example

**Scenario**: Breaking Bad
- Season 1: 7 episodes (all watched 10 days ago)
- Season 2: 13 episodes (all watched 7 days ago)
- Season 3: 13 episodes (5 watched, 8 unwatched)
- Season 4: 13 episodes (none watched)
- Season 5: 16 episodes (ongoing, last episode aired yesterday)

**Deletion Logic** (with 5-day grace period):
1. **Season 1**: ✅ Delete (all watched + past grace period)
2. **Season 2**: ✅ Delete (all watched + past grace period)
3. **Season 3**: ❌ Skip (partially watched)
   - With 80% threshold: ❌ Skip (38% watched < 80%)
   - Alternative: Delete 5 watched episodes individually
4. **Season 4**: ❌ Skip (not watched)
5. **Season 5**: ❌ Skip (latest season + ongoing series)

**Sonarr API Calls**:
```python
# Get all series info
series = get_series(sonarr_url, api_key, series_id=1)  # Breaking Bad

# Process Season 1
season1_episodes = get_episodes(sonarr_url, api_key, series_id=1, season=1)
season1_file_ids = [ep['episodeFileId'] for ep in season1_episodes]

# Bulk delete Season 1
bulk_delete_episode_files(sonarr_url, api_key, season1_file_ids)
# Result: DELETE /api/v3/episodefile/bulk
# Body: {"episodeFileIds": [1,2,3,4,5,6,7]}

# Process Season 2 (same logic)
bulk_delete_episode_files(sonarr_url, api_key, season2_file_ids)

# Season 3 - Individual episode deletion (if per-episode mode)
for episode in watched_season3_episodes:
    delete_episode_file(sonarr_url, api_key, episode['episodeFileId'])
```

---

## Configuration Options

Add to [cleanup-config ConfigMap](../clusters/microk8s/media/cleanup-cronjob.yaml):

```yaml
data:
  # Series deletion strategy
  SERIES_DELETION_MODE: "season"  # Options: "episode", "season", "hybrid"
  
  # Partial season handling
  PARTIAL_SEASON_THRESHOLD: "0.8"  # 80% watched = delete all
  
  # Latest season protection
  PROTECT_LATEST_SEASON: "true"
  PROTECT_ONGOING_SERIES: "true"
  
  # Special handling
  DELETE_SPECIALS_INDIVIDUALLY: "true"
```

---

## Integration into cleanup.py

Add new method to `MediaCleanup` class:

```python
def cleanup_episodes_season_aware(self):
    """
    Clean up episodes with season-aware logic
    """
    # Get all watched episodes from Jellyfin
    watched_episodes = self.mapper.get_jellyfin_watched_episodes(self.primary_user_id)
    
    # Group by series and season
    episodes_by_series_season = {}
    for ep in watched_episodes:
        series_name = ep.get('SeriesName')
        season_num = ep.get('ParentIndexNumber')
        key = (series_name, season_num)
        
        if key not in episodes_by_series_season:
            episodes_by_series_season[key] = []
        episodes_by_series_season[key].append(ep)
    
    # Process each series/season combo
    for (series_name, season_num), episodes in episodes_by_series_season.items():
        # Get Sonarr series ID (via path lookup)
        first_episode_path = episodes[0]['Path']
        sonarr_series_id = self._find_sonarr_series_id(first_episode_path)
        
        if not sonarr_series_id:
            logger.warning(f"Series not found in Sonarr: {series_name}")
            continue
        
        # Check if entire season is watched
        is_complete, episode_file_ids = self._is_season_fully_watched(
            sonarr_series_id,
            season_num,
            episodes
        )
        
        if is_complete:
            # Bulk delete entire season
            logger.info(f"Deleting complete season: {series_name} S{season_num:02d}")
            self.mapper.bulk_delete_sonarr_episode_files(
                episode_file_ids,
                dry_run=self.config.dry_run
            )
        else:
            # Fall back to per-episode deletion
            logger.info(f"Partial season: {series_name} S{season_num:02d} - using per-episode deletion")
            # ... per-episode logic
```

---

## Testing Recommendations

1. **Test with completed series**: Entire series watched → should delete all seasons
2. **Test with ongoing series**: Latest season protected despite being watched
3. **Test with partial season**: Only watched episodes deleted (or none if threshold not met)
4. **Test with specials**: Season 0 handled individually
5. **Test with mixed states**: Some seasons complete, some partial

---

## Future Enhancements

1. **Smart season packing**: If 90% of season watched, download remaining 10% to enable full-season deletion
2. **Stale content detection**: Delete unwatched content after X months (separate from watched grace period)
3. **User preference overrides**: Per-series "keep" settings via tags
4. **Multi-quality handling**: Keep higher quality version if multiple exist
