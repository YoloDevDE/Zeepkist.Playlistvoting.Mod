# Playlist Voting Flow Plan

## 1. Existing Client Mod Classes

- **Plugin**: Bootstrap class. Initializes Harmony, `VotingConfig`, and `VotingController`.
- **VotingController** (formerly VotingManager): Thin controller owning the high-level lifecycle and state machine.
- **VotingConfig**: BepInEx configuration wrapper for API URL, token, and other settings.
- **VotingApiClient** (formerly RestController): HTTP client for backend communication.
- **VotingBackendService**: High-level service managing backend connection, polling, and WebSocket events.
- **VotingWebSocketClient**: Handles real-time updates from the backend.
- **IVotingState / VotingStateBase**: Abstractions for the state machine.
- **VotingActiveState / VotingDisabledState**: Current state implementations.
- **VotingDisplayManager**: Manages UI updates like the server message.
- **VotingChatManager**: Registers and manages local and remote chat commands.
- **ZeepkistPlaylistService**: Infrastructure for interacting with ZeepSDK's playlist API.
- **ZeepkistMetadataProvider**: Provides level metadata from the game.

## 2. Existing Backend Endpoints and DTOs (from dev branch/issue)

Current `VotingApiClient` uses:

- `POST /auth/steam/ticket` -> `SteamLoginResponse`
- `GET /sessions/active/result` -> `VotingResultResponse`
- `GET /votes` -> Submit vote
- `POST /sessions/active/level` -> Set current level
- `DELETE /sessions/active/votes` -> Reset all votes for active session

Likely/Required Endpoints:

- `GET /api/playlistvoting/sessions/active`: Full metadata for active session.
- `GET /api/playlistvoting/sessions/latest-active` or `latest-resumable`: Find latest session to resume.
- `GET /api/playlistvoting/sessions/active/metadata`: Session state, playlist mode, online playlist info.
- `PATCH /api/playlistvoting/sessions/active`: Enable/disable playlist mode.
- `GET /api/playlistvoting/sessions/active/playlist/toBeVoted`: Download pending levels.
- `GET /api/playlistvoting/sessions/active/playlist/final`: Download YES levels.
- `POST /api/playlistvoting/sessions/active/level/finalize`: Close current level and record results.
- `DELETE /api/playlistvoting/sessions/active/level/votes`: Reset votes for specific level (Simple Mode).

DTOs:

- `PlaylistSessionInfo`: id, name, state, playlistModeEnabled, hasOnlinePlaylist, currentLevel, remainingLevels,
  finalizedLevels.
- `LevelMetadata`: uid, name, author, workshopID.

## 3. Backend Gaps

- **Session Resume**: Need `latest-resumable` endpoint.
- **Playlist Mode Toggle**: Need endpoint to set `playlistModeEnabled` on session.
- **Playlist Download**: Need endpoints for `toBeVoted` and `final` playlists.
- **Level Finalization**: Need `finalize` endpoint to move level to finalized state.
- **Level-specific Vote Reset**: Simple Mode requires resetting only the current level's votes.

## 4. Exact Startup Flow

1. **InitState**: Checks for active/latest resumable session.
2. **Session Check**:
    - If no session: Show "No active Playlist Voting session found..." -> `NoSessionState`.
    - If session exists: Fetch metadata.
3. **Mode Selection**:
    - If `playlistModeEnabled` is false OR no online playlist:
        - Check `PlaylistVotingStartupMode` config:
            - `AlwaysAsk`: Prompt with `/vote playlistmode` and `/vote simplemode` -> `AwaitingModeSelectionState`.
            - `AlwaysPlaylistMode`: -> `PlaylistStartupState`.
            - `AlwaysSimpleMode`: -> `SimpleVotingActiveState`.
    - If `playlistModeEnabled` is true:
        - Automatically continue to `PlaylistStartupState` (or `PlaylistVotingActiveState` if already synced).

## 5. Exact Playlist Mode OnLevelLoaded Flow

1. Broadcast result for `previousVotingLevel`.
2. Call `POST /sessions/active/level/finalize` for `previousVotingLevel`.
3. Check remaining levels in `toBeVoted`.
4. If no levels left:
    - Call `GET /sessions/active/playlist/final`.
    - Save as `{PlaylistName}-Final`.
    - Show completion message.
    - Transition to `FinishedState`.
5. If levels remain:
    - Check if new level UID is in `toBeVoted`.
    - If yes:
        - Set as current level in backend: `POST /sessions/active/level`.
        - Update server message with remaining count.
    - If no:
        - Show "Level not part of active voting playlist..."
        - Wait for next level.

## 6. Exact Simple Mode OnLevelLoaded Flow

1. Broadcast result for current/previous level.
2. Reset votes for that level ONLY: `DELETE /sessions/active/level/votes`.
3. (Optional) Broadcast that votes were reset.

## 7. Refactoring Plan

- **VotingController**: Stays as composition root.
- **VotingApiClient**: Refactor to use new endpoints. Rename to `PlaylistVotingBackendClient`?
- **States**:
    - `InitState`
    - `NoSessionState`
    - `AwaitingModeSelectionState`
    - `PlaylistStartupState`
    - `PlaylistVotingActiveState`
    - `SimpleVotingActiveState`
    - `FinishedState`
    - `PlaylistConflictState`
- **Services**:
    - `PlaylistFileService`: Logic for saving/loading `.zeeplist` files.
    - `PlaylistSyncService`: Comparison and merge logic.
    - `VotingResultBroadcaster`: Handles chat broadcasts.
    - `ServerMessageService`: Builds server message strings.
- **Commands**:
    - Add `/vote resume`, `/vote playlistmode`, `/vote simplemode`, `/vote use-local`, `/vote use-online`,
      `/vote merge`.

## 8. Required ZeepSDK / YoloDevUtils APIs

- **ZeepSDK.Playlist.PlaylistApi**: Managing playlists.
- **ZeepSDK.Racing.RacingApi**: Level loaded events.
- **ZeepSDK.ChatCommands.ChatCommandApi**: Registering commands.
- **YoloDevUtils.Zeepkist.ToastNotification**: User alerts.
- **YoloDevUtils.Text.TMPRichTextBuilder**: Rich text formatting.

## 9. Risks and TODOs

- **Backend Readiness**: Endpoints for playlist sync and level finalization must be available.
- **Playlist Sync**: Edge cases in merging (duplicates, UID matching).
- **Broken Levels**: TODO: Implement broken-level handling (e.g., skip button).
- **Concurrency**: Handling WebSocket updates while performing HTTP calls.
