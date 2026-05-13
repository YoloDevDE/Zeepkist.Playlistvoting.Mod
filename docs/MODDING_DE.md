# Zeepkist — Modding Dokumentation (Deutsch)

> Alle Code-Referenzen basieren auf dem dekompilierten `Zeepkist.dll`.  
> Methoden-Signaturen können durch den Dekompiler leicht verändert sein.  
> **Wichtig:** Nur Host/HasHostPowers darf Host-Aktionen ausführen. Prüfung:
`ZeepkistNetwork.IsMasterClient || ZeepkistNetwork.LocalPlayer.hasHostPowers`

---

## Inhaltsverzeichnis

1. [Alle Scenes](#1-alle-scenes)
2. [Online: Neues Level laden](#2-online-neues-level-laden)
3. [Freeplay / Offline Level laden](#3-freeplay--offline-level-laden)
4. [Chat-Commands abfangen](#4-chat-commands-abfangen)
5. [Rundentimer als Zahl](#5-rundentimer-als-zahl)
6. [Levels speichern und laden (Dateisystem)](#6-levels-speichern-und-laden)
7. [Level-Klassen und ihre Felder (v15JSON / aktuelles Format)](#7-level-klassen-und-ihre-felder)
8. [Audio dem Spiel hinzufügen](#8-audio-dem-spiel-hinzufügen)
9. [Das Gizmo-System im Level-Editor](#9-das-gizmo-system-im-level-editor)
10. [Medals als Sprites / Emotes?](#10-medals-als-sprites--emotes)
11. [Level-Editor Lifecycle](#11-level-editor-lifecycle)
12. [ZeepkistNetwork — Nützliche API-Übersicht](#12-zeepkistnetwork--nützliche-api-übersicht)
13. [Online-Gameloop: Lobby → Spiel → Podium](#13-online-gameloop-lobby--spiel--podium)
14. [Workshop: laden, speichern, verwalten](#14-workshop-laden-speichern-verwalten)
15. [ServerMessage & JoinMessage — Wie man Nachrichten FÜR ALLE sendet](#15-servermessage--joinmessage)
16. [TimerChanged vs TimeChanged — Der Unterschied](#16-timerchanged-vs-timechanged)
17. [Nächstes Level setzen, Runde beenden, Zeit verlängern (programmatisch)](#17-nächstes-level-setzen-runde-beenden-zeit-verlängern)
18. [ChatColor: Woher kommt die Farbe?](#18-chatcolor)
19. [Lifecycle: PlayerConnect & PlayerDisconnect](#19-lifecycle-playerconnect--playerconnect)
20. [Vollständige Methodenreferenz](#20-vollständige-methodenreferenz)

---

## 1. Alle Scenes

| Scene-Name                   | Beschreibung                                  |
|------------------------------|-----------------------------------------------|
| `3D_MainMenu`                | Hauptmenü                                     |
| `SelectLevelScene`           | Freeplay-Level-Auswahl                        |
| `GameScene`                  | Gameplay (online + offline + Test aus Editor) |
| `LevelEditor2`               | Level-Editor                                  |
| `WorkshopEditor`             | Workshop-Upload-Editor                        |
| `Online Lobby`               | Online-Lobby-Auswahl                          |
| `Character Select`           | Splitscreen-Charakter-Auswahl                 |
| `Avonturenkaart`             | Adventure Mode Karte                          |
| `MedalsScene`                | Medaillen-Übersicht                           |
| `Cosmetic Thumbnail Maker`   | Internes Kosmetik-Thumbnail-Tool              |
| `IntroScene` / `IntroScene2` | Intro-Screens (dynamisch geladen)             |

---

## 2. Online: Neues Level laden

### Verbindung → Lobby → Level

```csharp
// 1. Mit Master-Server verbinden:
NetworkClientManager.Instance.ConnectToMasterServer();
// Event: ZeepkistNetwork.ConnectedToMasterServer

// 2. Lobby erstellen oder beitreten:
ZeepkistNetwork.CreateLobby(lobbyName, maxPlayers, isPublic);
ZeepkistNetwork.JoinLobby(lobbyID); // ID = string
// Event: ZeepkistNetwork.ConnectedToGameServer → LoadScene("GameScene")

// 3. Level-Daten kommen vom Server (in SetupGame.cs):
ZeepkistNetwork.LevelDataReceived += OnLevelDataReceived;
ZeepkistNetwork.LevelDataFailed   += OnLevelDataFailed;

void OnLevelDataReceived(string levelname, string[] levellines, string adventureUID)
{
    LevelScriptableObject from =
        GeneralLevelLoadStatic.ReadRawLevelDataForScriptableObject(
            str, isCSV, isV15, false, "Online: " + levelname);
    from.LevelDataV15 = str;
    from.Name = levelname;
    GlobalLevel.Copy(from);
    manager.loader.PrimeForGameplay_v15(gameMaster, GlobalLevel.LevelDataV15, skybox);
    // → StartCoroutine(LoadLevelData()) → DoLoad() → DoStart()
}
```

---

## 3. Freeplay / Offline Level laden

```csharp
// Freeplay starten (aus Hauptmenü):
PlayerManager.amountOfPlayers = 1;
PlayerManager.singlePlayer = true;
SceneManager.LoadScene("SelectLevelScene");

// Navigationszustand:
PlayerManager.Instance.freeplay_directory   // DirectoryOrLevel: aktueller Ordner
PlayerManager.Instance.freeplay_currentpage // int: aktuelle Seite
PlayerManager.Instance.freeplay_currentcard // int: ausgewählte Karte

// Nach Level-Auswahl → GameScene:
// GlobalLevel ist bereits befüllt. SetupGame.LoadOfflineLevel() ruft auf:
manager.loader.PrimeForGameplay_v15(gameMaster, GlobalLevel.LevelDataV15, skybox);

// Level-Editor: externe Datei laden:
central.saveLoad.ExternalLoad(fullPath, retainCamera, isTestMap);
// → intern: central.manager.loader.PrimeForLevelEditor_v15(central, jsonStr, skybox)
```

---

## 4. Chat-Commands abfangen

Das Spiel interpretiert Chat-Commands **serverseitig** (Host-Seite).  
Für Mods fängst du das Event ab und reagierst clientseitig:

```csharp
ZeepkistNetwork.ChatMessageReceived += OnChatMessage;
ZeepkistNetwork.ChatMessageReceived -= OnChatMessage; // in OnDestroy

void OnChatMessage(ZeepkistChatMessage message)
{
    // message.Message  = Chat-Text (roh, vor Bad-Word-Filter)
    // message.Player   = ZeepkistNetworkPlayer (null = Server-Nachricht)
    // message.Badges   = List<string>

    if (message.Message.StartsWith("/meincommand"))
    {
        string[] parts = message.Message.Split(' ');
        // parts[1], parts[2] = Argumente
    }
}

// Chat-Nachricht senden:
ZeepkistNetwork.NetworkClient.SendPacket(new ChatMessagePacket
{
    Message = "/meincommand arg1", Badges = new List<string>()
});
```

**Wo das Spiel selbst Chat-Commands verarbeitet:**

- `OnlineChatUI.SendChatMessage()` sendet via `ChatMessagePacket`
- Der **Server** interpretiert Commands wie `/servermessage`, `/joinmessage`, `/skip`, `/time`
- Es gibt **keine clientseitige** Parsing-Klasse für diese Commands im dekompilierten Code
- Für eigene Mod-Commands: per `ZeepkistNetwork.ChatMessageReceived` abonnieren

---

## 5. Rundentimer als Zahl

```csharp
// Anzeige-String (fertig formatiert, z.B. "1:23"):
string timerText = ZeepkistNetwork.CurrentLobby.timeLeftString;

// Gesamte Rundenzeit (Sekunden):
double roundTime = ZeepkistNetwork.CurrentLobby.RoundTime;

// Zeitstempel Level-Start:
double loadedAt = ZeepkistNetwork.CurrentLobby.LevelLoadedAtTime;

// Verbleibende Zeit berechnen:
double timeLeft = (loadedAt + roundTime) - ZeepkistNetwork.Time;

// Verstrichene Zeit:
double elapsed = ZeepkistNetwork.Time - loadedAt;
string elapsedStr = elapsed.ToString("F0"); // "42"
```

---

## 6. Levels speichern und laden

### Dateipfade

```csharp
DirectoryInfo levelsDir    = ZeepkistFolders.GetLevelsFolder();
DirectoryInfo autosavesDir = ZeepkistFolders.GetLevelsAutosavesFolder();
DirectoryInfo backupsDir   = ZeepkistFolders.GetLevelsBackupFolder();
// Dateiendung: .zeeplevel (JSON-Format)
// Index-Datei: indexdata.zeepindex (JSON, neben jeder .zeeplevel)
```

### Speichern

```csharp
// Als JSON-String serialisieren:
string json = central.saveLoad.ConvertCurrentLevelStateToJSON_v15_string();

// Autosave / Backup:
central.saveLoad.SaveBackup(isAutosave: true);   // Autosave
central.saveLoad.SaveBackup(isAutosave: false);  // Backup

// Datei mit Name speichern:
central.saveLoad.ExternalSaveFile(intendedName: "MeinLevel", isTestMap: false);
```

### Laden (Level-Editor)

```csharp
// Datei laden (modernes JSON):
central.manager.loader.PrimeForLevelEditor_v15(central, jsonString, skybox, retainCamera: false);

// Format prüfen:
bool isModern = GeneralLevelLoadStatic.IsThisLevelDataStringV15(rawText);

// Roh-String → LevelScriptableObject:
LevelScriptableObject lso = GeneralLevelLoadStatic.ReadRawLevelDataForScriptableObject(
    rawString, isCSV: !isModern, isFullV15: isModern, isIndexV15: false, debugInfo: "Info");
```

---

## 7. Level-Klassen und ihre Felder

> **Hinweis:** Das aktuelle Level-Format heißt intern `v15LevelJSON` (Klasse), ist aber das *aktuelle* Format. `v15` ist
> der Klassenname, keine veraltete Version.

### `v15LevelJSON` — Hauptklasse (JSON-Dateiinhalt)

```csharp
v15LevelJSON levelData = JsonConvert.DeserializeObject<v15LevelJSON>(rawJson);

levelData.jsonVersion          // int = 3
levelData.level.name           // Level-Name
levelData.level.UID            // z.B. "abc123=PlayerName"
levelData.level.zeepHash       // SHA1-Hash

levelData.author.name          // Autor-Name
levelData.author.collaborators // List<string>
levelData.author.nameOverride  // optionaler Anzeigename

levelData.medals.author        // float (Sekunden)
levelData.medals.gold
levelData.medals.silver
levelData.medals.bronze
levelData.medals.isLegit       // bool: validiert?

levelData.enviro.skybox        // int: Skybox-Index
levelData.enviro.groundMat     // int: Bodenmaterial (-1 = aus)
levelData.enviro.overrideFog_b // bool
levelData.enviro.overrideFog_f // float
levelData.enviro.skyboxOverride// SkyboxV17Object

levelData.editcam.pos          // CV3 (= Vector3)
levelData.editcam.euler        // CV3
levelData.editcam.rotXY        // CV2

levelData.blox                 // List<BlockPropertyJSON>
```

### `BlockPropertyJSON` — Ein Block

```csharp
public int    i;    // Index in globalBlockList (Prefab-Index)
public CC3    p;    // Position
public CC3    e;    // Euler-Rotation
public CC3    s;    // Scale
public CC3    c;    // Primärfarbe
public CC3    c2;   // Sekundärfarbe
public CC3    c3;   // Tertiärfarbe
public CC3    c4;   // Quartärfarbe
public string uid;  // Block-UID (eindeutig im Level)
public PropertyDictionariesJSON customBlockEditProperties; // z.B. Booster-Speed
```

### `LevelScriptableObject` — Laufzeit-Repräsentation (zwischen Scenes geteilt)

```csharp
GlobalLevel.Name          // Level-Name
GlobalLevel.UID           // Level-UID
GlobalLevel.LevelDataV15  // Kompletter JSON-String
GlobalLevel.LevelData     // string[] (Legacy CSV)
GlobalLevel.WorkshopID    // ulong: Steam Workshop ID
GlobalLevel.Path          // Dateipfad
GlobalLevel.IsTestLevel   // bool: Test aus Editor?
GlobalLevel.TimeAuthor    // float
GlobalLevel.TimeGold / TimeSilver / TimeBronze // float
GlobalLevel.GetAuthorNameWithCollaborators()   // string
```

### Index-Datei (`v15LevelINDEX`)

```csharp
v15LevelINDEX index = JsonConvert.DeserializeObject<v15LevelINDEX>(indexContent);
index.indexVersion   // int (2 = aktuell)
index.authorName
index.UID
index.TimeAuthor / TimeGold / TimeSilver / TimeBronze
// Index neu erstellen:
GeneralLevelLoadStatic.CreateNewIndexFile(longPath, tempLevel, file, indexfile);
```

### Blöcke laden (Loader intern)

```csharp
// Alle Blöcke der aktuellen Scene:
List<BlockProperties> blocks = central.saveLoad.GetAllBlockPropertiesCurrentlyInLevel();

// Block → JSON:
BlockPropertyJSON bpj = block.ConvertBlockToJSON_v15();

// JSON → Block-Properties anwenden:
block.LoadProperties_v15(bpj, applyToTransform: true);
```

---

## 8. Audio dem Spiel hinzufügen

### Methode A: AudioManager (empfohlen für Mods)

```csharp
// AudioItemScriptableObject zur Laufzeit erstellen:
var item = ScriptableObject.CreateInstance<AudioItemScriptableObject>();
item.Clip       = myAudioClip; // via AssetBundle laden
item.BaseVolume = 1f;
item.Loop       = false;
// item.MixerGroup = null; → nutzt DefaultMixerGroup

AudioManager.Instance.Play(item);
AudioManager.Instance.StopAll();
```

### Methode B: FMOD Events (Spiel-intern)

```csharp
AudioEvents.MenuHover1.Play(transform);          // 3D
AudioEvents.DeleteBlock.Play();                  // 2D
AudioEvents.PitCollision.Play(transform);
AudioEvents.PitGrowsIntoTree.Play(transform);
```

---

## 9. Das Gizmo-System im Level-Editor

### Klassen-Übersicht

| Klasse                 | Funktion                           |
|------------------------|------------------------------------|
| `LEV_GizmoHandler`     | Hauptsteuerung: Grid, G-Mode, Drag |
| `LEV_SingleGizmo`      | Einzelne Achse (Hover, Click)      |
| `LEV_ColorMotherGizmo` | Farb-/Transparenzsteuerung         |

### Wichtige Felder von `LEV_GizmoHandler`

```csharp
Transform motherGizmo;          // Pivot-Punkt
LEV_SingleGizmo Xgizmo, Ygizmo, Zgizmo;
LEV_SingleGizmo XYgizmo, YZgizmo, XZgizmo;
LEV_SingleGizmo RXgizmo, RYgizmo, RZgizmo;
LEV_SingleGizmo currentGizmo;   // aktuell aktiv
float gridXZ, gridY, gridR;     // Grid-Werte
bool isGrabbing;                // G-Mode aktiv
bool isDragging;                // Achse wird gezogen
bool useGlobalRotation;
```

### Bewegung

```csharp
// Gizmo-Position setzen:
central.gizmos.SetMotherPosition(Vector3 position);

// Selektierte Blöcke bewegen:
central.selection.TranslatePositions(Vector3 delta);

// Rotation:
central.rotflip.RotateBlocks(Vector3 axis, float angle, Vector3 pivot);

// Grid:
central.gizmos.CycleGridXZ();
central.gizmos.CycleGridY();
central.gizmos.CycleGridR();
central.gizmos.ResetGridValues();
central.gizmos.SnapToGridXZ();
central.gizmos.SnapToGridY();
central.gizmos.Deactivate();
```

### Vertex-Snapping (kein Eingebaut — so macht man es per Mod)

```csharp
// Harmony-Prefix auf LEV_GizmoHandler.GrabGizmo() oder SetMotherPosition()
// Alle Vertices aller Blöcke sammeln:
List<BlockProperties> allBlocks = central.saveLoad.GetAllBlockPropertiesCurrentlyInLevel();
Vector3 mouseWorldPos = GetMouseWorldPosition(); // eigener Raycast
float minDist = float.MaxValue;
Vector3 snapTarget = mouseWorldPos;

foreach (var block in allBlocks)
{
    MeshFilter mf = block.GetComponentInChildren<MeshFilter>();
    if (mf == null) continue;
    foreach (Vector3 lv in mf.sharedMesh.vertices)
    {
        Vector3 wv = mf.transform.TransformPoint(lv);
        float d = Vector3.Distance(mouseWorldPos, wv);
        if (d < minDist) { minDist = d; snapTarget = wv; }
    }
}
// snapTarget = nächster Vertex → central.gizmos.SetMotherPosition(snapTarget)
```

---

## 10. Medals als Sprites / Emotes

**Kurze Antwort: Nein.**  
Es gibt keine fertig verfügbaren Medal-Sprites (Gold/Silver/Bronze/Author/"You Tried") als TMP-SpriteAsset.

Was existiert:

- `Medals_DataObject` enthält nur `float`-Zeitwerte
- `MedalCounterSign` zeigt Medaillen-Counts als Text (kein Sprite-Emote)
- Lokalisierungskeys: `I2.Loc.LocalizationManager.GetTranslation("Menu/GoldMedal")` etc.

**Für Mods:** Eigene Sprites in ein TMP-SpriteAsset laden und als Mod-Asset bereitstellen.

---

## 11. Level-Editor Lifecycle

```
1. EINSTIEG:
   ├─ Von Hauptmenü: SceneManager.LoadScene("LevelEditor2")
   │   PlayerManager.Instance.weLoadedLevelEditorFromMainMenu = true
   │
   └─ Von GameScene (nach TestMap-Ende):
       GlobalLevel.IsTestLevel = true → Editor lädt letzten Test zurück

2. LEV_LevelEditorCentral.Awake():
   ├─ manager.extendedTags.ClearDictionary()
   ├─ GiveAchievement("ACH_13_OPENLEVELEDITOR")
   ├─ cursorManager.SetEnabled(false) / SetCursorEnabled(true)
   └─ collaborators.PreventQuestionMarkBug()

3. LEV_TestMap.Start():
   ├─ Falls GlobalLevel.IsTestLevel == true:
   │   └─ central.saveload.ExternalLoad(GlobalLevel.Path, true, true)
   │       (lädt letzten Test-Stand zurück)
   └─ Falls weLoadedLevelEditorFromMainMenu:
       → Nichts laden, leerer Editor

4. AUTOSAVE (in LEV_LevelEditorCentral.Update()):
   ├─ Wenn Settings.editor_auto_save == true
   ├─ autoSaveTimer >= editor_auto_save_interval (Standard: 300 Sek.)
   └─ central.saveload.SaveBackup(isAutosave: true)

5. TEST-MAP (F5 / TestMap-Button):
   central.testMap.TestMap()
   ├─ Prüft: mind. 1 Start-Block vorhanden?
   ├─ SaveTestMap()
   │   ├─ ExternalSaveFile("TestLevel", isTestMap: true)
   │   ├─ GlobalLevel.Copy(LoadLevel(...))
   │   ├─ GlobalLevel.LevelDataV15 = ConvertCurrentLevelStateToJSON_v15_string()
   │   └─ GlobalLevel.IsTestLevel = true
   ├─ manager.ResetAll()
   ├─ PlayerManager.singlePlayer = true
   └─ SceneManager.LoadScene("GameScene")
   → Nach dem Test: Return zum Editor = LoadScene("LevelEditor2")
   → LEV_TestMap.Start() erkennt IsTestLevel=true → lädt Zustand zurück

6. SPEICHERN:
   central.saveload.ExternalSaveFile(name, isTestMap: false)
   central.saveload.SaveBackup(isAutosave)

7. LADEN (extern):
   central.saveload.ExternalLoad(fullPath, retainCamera, isTestMap)
   → PrimeForLevelEditor_v15(central, jsonString, skybox, retainCamera)

8. VERLASSEN:
   LEV_ReturnToMainMenu → SceneManager.LoadScene("3D_MainMenu")
   LEV_LevelEditorCentral.OnDestroy():
   └─ manager.cursorManager.SetEnabled(true)
```

### Alle Sub-Systeme von `LEV_LevelEditorCentral`

| Feld            | Klasse                    | Funktion                         |
|-----------------|---------------------------|----------------------------------|
| `cam`           | `LEV_MoveCamera`          | Kamera-Bewegung                  |
| `cursor`        | `LEV_CursorBoy`           | Maus-Cursor im Editor            |
| `click`         | `LEV_ClickScript`         | Block-Klick-Handling             |
| `gizmos`        | `LEV_GizmoHandler`        | Gizmo / Grid                     |
| `selection`     | `LEV_Selection`           | Selektion / Multi-Select         |
| `rotflip`       | `LEV_RotateFlip`          | Rotation / Spiegeln              |
| `inspector`     | `LEV_Inspector`           | Eigenschaften-Panel              |
| `tool`          | `LEV_ToolSwitch`          | Tool-Wechsel (Select/Paint/etc.) |
| `TREEGUNNNN`    | `LEV_TREEGUNNN`           | Baum-Pflanzer                    |
| `painter`       | `LEV_PaintTool`           | Malen-Tool                       |
| `pause`         | `LEV_PauseMenu`           | Pause-Menü                       |
| `saveload`      | `LEV_SaveLoad`            | Speichern/Laden                  |
| `settings`      | `LEV_SettingsMenu`        | Editor-Einstellungen             |
| `screenshot`    | `LEV_ScreenshotMaker`     | Screenshots                      |
| `colors`        | `LEV_ColorScheme`         | Farbschema                       |
| `recolor`       | `LEV_RecolorGUI`          | Umfärben-GUI                     |
| `hotkey`        | `LEV_Hotkey`              | Hotkeys                          |
| `input`         | `LEV_Input`               | Input-Actions                    |
| `validation`    | `LEV_ValidationLock`      | Level-Validierung                |
| `testMap`       | `LEV_TestMap`             | Test-Map-Funktion                |
| `medalTimes`    | `LEV_SetGoldSilverBronze` | Medaillen-Zeiten setzen          |
| `skyboxTool`    | `LEV_SkyboxTool`          | Skybox-Editor                    |
| `collaborators` | `LEV_CollaboratorsPanel`  | Kollaborateure                   |
| `undoRedo`      | `LEV_UndoRedo`            | Undo/Redo-Stack                  |
| `slotprefabs`   | `LEV_SlotPrefabHolder`    | Slot-Prefab-Verwaltung           |
| `connectorTool` | `LEV_ConnectorTool`       | Connector-Verbindungs-Tool       |

---

## 12. ZeepkistNetwork — Nützliche API-Übersicht

### Statische Properties

```csharp
ZeepkistNetwork.IsConnected           // bool: Master-Server verbunden
ZeepkistNetwork.IsConnectedToGame     // bool: auch im Game-Server
ZeepkistNetwork.IsMasterClient        // bool: LocalPlayer.isHost
ZeepkistNetwork.LocalPlayer           // ZeepkistNetworkPlayer
ZeepkistNetwork.PlayerList            // List<ZeepkistNetworkPlayer>
ZeepkistNetwork.Leaderboard           // List<LeaderboardItem>
ZeepkistNetwork.LeaderboardOverride   // List<LeaderboardOverrideItem>
ZeepkistNetwork.ChatMessages          // List<ZeepkistChatMessage> (max 20)
ZeepkistNetwork.AllLobbies            // List<ZeepkistLobby> (öffentliche Lobbies)
ZeepkistNetwork.ActiveLobbies         // int: aktive Lobbies
ZeepkistNetwork.OnlinePlayers         // int: globale Online-Spieleranzahl
ZeepkistNetwork.PlayersInLobbies      // int
ZeepkistNetwork.Time                  // double: Netzwerk-Zeitstempel
ZeepkistNetwork.GameSettings          // ServerGameSettings
ZeepkistNetwork.NetworkClient         // NetworkClient (für SendPacket)
ZeepkistNetwork.CurrentLobby          // ZeepkistLobby
```

### Verbindungs-Methoden

```csharp
ZeepkistNetwork.CreateLobby(name, maxPlayers, isPublic)
ZeepkistNetwork.JoinLobby(id)
ZeepkistNetwork.Disconnect("reason")
ZeepkistNetwork.TryGetPlayer(steamID, out ZeepkistNetworkPlayer player)
```

### Spieler-Verwaltung (Host-only)

```csharp
ZeepkistNetwork.KickPlayer(player)
ZeepkistNetwork.FavoritePlayer(player, shouldAllow)
ZeepkistNetwork.SetMasterClient(player)
ZeepkistNetwork.ResetChampionshipPoints(notifyPlayer)
```

### Custom Leaderboard (Host-only)

```csharp
ZeepkistNetwork.CustomLeaderBoard_SetPlayerChampionshipPoints(steamID, newPoints, displayChange, notifyPlayer)
ZeepkistNetwork.CustomLeaderBoard_ResetPointsDistribution()
ZeepkistNetwork.CustomLeaderBoard_SetPointsDistribution(values, baseline, dnf)
ZeepkistNetwork.CustomLeaderBoard_RemovePlayerFromLeaderboard(steamID, notifyPlayer)
ZeepkistNetwork.CustomLeaderBoard_SetPlayerLeaderboardOverrides(steamID, time, name, position, points, pointsWon)
ZeepkistNetwork.CustomLeaderBoard_SetPlayerTimeOnLeaderboard(steamID, time, notifyPlayer)
ZeepkistNetwork.CustomLeaderBoard_SetSmallLeaderboardSortingMethod(useChampionshipSorting)
ZeepkistNetwork.CustomLeaderBoard_BlockPlayerFromSettingTime(steamID, notifyPlayer)
ZeepkistNetwork.CustomLeaderBoard_UnblockPlayerFromSettingTime(steamID, notifyPlayer)
ZeepkistNetwork.CustomLeaderBoard_UnblockEveryoneFromSettingTime(notifyPlayer)
ZeepkistNetwork.CustomLeaderBoard_BlockEveryoneFromSettingTime(notifyPlayer)
```

### Chat (Host-only für Custom)

```csharp
// Custom Chat-Nachricht für alle oder einzelne Spieler:
ZeepkistNetwork.SendCustomChatMessage(
    sendToEveryone: true,
    optionalTargetSteamID: 0,
    message: "Hallo alle!",
    hostnamePREFERREDCAPS: "SERVER"
);
```

### Leaderboard abfragen

```csharp
LeaderboardItem entry = ZeepkistNetwork.GetLeaderboardEntry(steamID);
List<LeaderboardItem> lb = ZeepkistNetwork.GetLeaderboard();
LeaderboardOverrideItem override = ZeepkistNetwork.GetLeaderboardOverride(steamID);
Vector2Int points = ZeepkistNetwork.GetPlayerChampionshipPoints(steamID);
```

### ZeepkistLobby — Methoden

```csharp
ZeepkistNetwork.CurrentLobby.UpdateName(string name)         // Packet: ChangeLobbyNamePacket
ZeepkistNetwork.CurrentLobby.UpdateMaxPlayers(int maxPlayers) // Packet: ChangeLobbyMaxPlayersPacket
ZeepkistNetwork.CurrentLobby.UpdateVisibility(bool isPublic)  // Packet: ChangeLobbyVisibilityPacket
```

### ZeepkistNetworkPlayer — Wichtige Felder

```csharp
player.chatColor          // Color (erst nach erstem Positions-Update)
player.SteamID            // ulong
player.UID                // uint: Netzwerk-UID
player.isHost             // bool
player.hasHostPowers      // bool
player.IsLocal            // bool
player.ChampionshipPoints // Vector2Int
player.CurrentResult      // Result: { LevelUID, Time, Checkpoints }
player.Zeepkist           // NetworkedZeepkistGhost
player.SplitTimes         // List<WinCompare.SplitTime>

player.GetTaggedUsername()        // "[Tag] Name"
player.GetPureUserName()          // reiner Name
player.GetBackupName()            // Fallback
player.SetLocalResult(uid, time, checkpoints)
```

### Alle Events von `ZeepkistNetwork`

| Event                          | Typ                                                           | Wann                                                |
|--------------------------------|---------------------------------------------------------------|-----------------------------------------------------|
| `ConnectedToMasterServer`      | `Action`                                                      | Master-Server verbunden                             |
| `DisconnectedFromMasterServer` | `Action<string>`                                              | Getrennt (Reason: "version-invalid", "banned\|...") |
| `ConnectedToGameServer`        | `Action`                                                      | Game-Server beigetreten                             |
| `DisconnectedFromGameServer`   | `Action<string>`                                              | Getrennt vom Game-Server                            |
| `LobbyListUpdated`             | `Action`                                                      | Lobby-Liste aktualisiert                            |
| `JoinLobbyFailed`              | `Action<JoinLobbyResult>`                                     | Beitreten fehlgeschlagen                            |
| `PlayerConnected`              | `Action<ZeepkistNetworkPlayer>`                               | Spieler beigetreten                                 |
| `PlayerDisconnected`           | `Action<ZeepkistNetworkPlayer>`                               | Spieler verlassen                                   |
| `MasterChanged`                | `Action<ZeepkistNetworkPlayer>`                               | Host gewechselt                                     |
| `PlayerResultsChanged`         | `Action<ZeepkistNetworkPlayer>`                               | Spieler hat Zeit gesetzt                            |
| `LobbyGameStateChanged`        | `Action`                                                      | GameState 0/1/2 geändert                            |
| `LobbyNameChanged`             | `Action`                                                      | Lobby-Name geändert                                 |
| `LobbyVisibilityChanged`       | `Action`                                                      | Public/Private geändert                             |
| `LobbyMaxPlayersChanged`       | `Action`                                                      | Max-Spieler geändert                                |
| `LobbyPropertiesChanged`       | `Action`                                                      | Allgemeine Properties                               |
| `LeaderboardUpdated`           | `Action`                                                      | Leaderboard aktualisiert                            |
| `LobbyPlaylistChanged`         | `Action`                                                      | Playlist geändert                                   |
| `GameSettingsChanged`          | `Action`                                                      | Spieleinstellungen geändert                         |
| `LevelDataFailed`              | `Action`                                                      | Level-Download fehlgeschlagen                       |
| `LevelDataReceived`            | `Action<string, string[], string>`                            | Level angekommen (name, lines, adventureUID)        |
| `LobbyMessageReceived`         | `Action<byte, Color, string>`                                 | Server-Nachricht (type, color, text)                |
| `ChatMessageReceived`          | `Action<ZeepkistChatMessage>`                                 | Chat-Nachricht                                      |
| `PlayerPositionUpdate`         | `Action<ZeepkistNetworkPlayer, PlayerZeepkistPositionPacket>` | Positions-Update                                    |

---

## 13. Online-Gameloop: Lobby → Spiel → Podium

### GameState-Werte

| Wert | Bedeutung                    | Dauer                |
|------|------------------------------|----------------------|
| `0`  | **Spielend** — Runde läuft   | variable (RoundTime) |
| `1`  | **Runde endet** — Buffer     | ~3 Sekunden          |
| `2`  | **Podium** — Ergebnisanzeige | ~10 Sekunden         |

### Kompletter Gameloop

```
1. LobbyManager ("Online Lobby"):
   NetworkClientManager.Instance.ConnectToMasterServer()
   → ConnectedToMasterServer → ZeepkistNetwork.LobbyListUpdated
   → CreateLobby / JoinLobby
   → ConnectedToGameServer → SceneManager.LoadScene("GameScene")

2. GameScene - SetupGame.Awake():
   IsConnected? → LoadOnlineLevel()
   → LevelDataReceived → ReadRawLevelData → GlobalLevel.Copy()
   → PrimeForGameplay_v15() → StartCoroutine(LoadLevelData())
   → DoLoad() (frame-by-frame) → DoStart()
   → GameState = 0 → LobbyGameStateChanged

3. GameState 0 (Runde läuft):
   Timer: (LevelLoadedAtTime + RoundTime) - ZeepkistNetwork.Time
   PlayerResultsChanged wenn Spieler Zeit setzt
   LeaderboardUpdated wenn Leaderboard sich ändert

4. GameState 1 (Buffer, ~3s):
   LobbyGameStateChanged

5. GameState 2 (Podium, ~10s):
   LobbyGameStateChanged
   → Nächstes Level: LevelDataReceived + LobbyPlaylistChanged
   → zurück zu GameState 0

6. Verlassen:
   ZeepkistNetwork.Disconnect("reason")
   → DisconnectedFromGameServer
   → Players.Clear(), CurrentLobby = null
   → SceneManager.LoadScene("3D_MainMenu")
```

---

## 14. Workshop: laden, speichern, verwalten

### Dateipfade

```csharp
// Workshop-Projekte:
Application.persistentDataPath + "\\Belangrijk\\WorkshopProjects\\"
// Staging (temporär beim Upload):
Application.persistentDataPath + "\\Belangrijk\\WorkshopLaunchpad\\"
// Metadaten pro Item:
Path.Combine(item.Directory, "metadata.json")
```

### Workshop-Item-Events

```csharp
SteamUGC.OnItemInstalled    += OnItemInstalled;
SteamUGC.OnItemSubscribed   += OnItemSubscribed;
SteamUGC.OnItemUnsubscribed += OnItemUnsubscribe; // Ordner wird gelöscht!
SteamUGC.OnDownloadItemResult += OnItemDownloaded;
```

### WorkshopManager-API

```csharp
await WorkshopManager.Instance.ProcessItem(extendedItem);
await WorkshopManager.Instance.RemoveItem(extendedItem);
bool ok = await WorkshopManager.Instance.DownloadWorkshopLevel(publishedFileId);
WorkshopManager.Instance.CheckAllWorkshopItemsForUpdate();
string name = WorkshopManager.Instance.GetWorkshopItemName(ulong itemID);
string url  = WorkshopManager.Instance.GetWorkshopItemPreviewURL(ulong itemID);
bool downloading = WorkshopManager.Instance.IsDownloadingAnything();

WorkshopManager.Instance.OnItemDownloadProgress += (item, percent) =>
    Debug.Log($"{item.Title}: {percent:F0}%");
```

### ExtendedItem

```csharp
public ulong  itemID;
public string directory;       // lokaler Pfad
public string name;
public string previewURL;
public ulong  authorSteamID;
public string authorSteamName;

// Konvertieren:
ExtendedItem item = WorkshopManagerStatics.ConvertSteamItem(steamItem);
```

### WorkshopProject (lokales Upload-Projekt)

```csharp
// Gespeichert als .zeepworkshop (JsonUtility):
public ulong  publishFileID_long;  // 0 = neu
public string itemTitle;
public string previewFilePath;
public List<WorkshopLevel> levels;
public int    validLevels;
public int    amountOfLevels;

// Laden:
WorkshopProject proj = JsonUtility.FromJson<WorkshopProject>(File.ReadAllText(path));
// Speichern:
conductor.SaveProject(showMessage: true);
```

### Upload-Flow

```csharp
conductor.Upload_MoveAllItemsToStaging();    // Dateien in Staging-Ordner
steam.CreateSteamItem(isNew: true/false);    // Steam-API Call
// Callbacks:
conductor.WorkshopItemCreatedSuccessfully(publishID);
conductor.WorkshopItemUploadedSuccessfully(publishID, needsLegalAgreement);
conductor.WorkshopItemUploadedFailed(errorMessage);
```

---

## 15. ServerMessage & JoinMessage

### Übersicht der Nachrichtentypen

Das Spiel kennt diese `messageType`-Werte in `LobbyMessagePacket`:

| Type (byte) | Bedeutung                                                                        |
|-------------|----------------------------------------------------------------------------------|
| `0`         | **ServerMessage** — statische Anzeige oben im UI (gefiltert durch BadWordFilter) |
| `1`         | **VoteskipMessage** — separates TMP-Feld für Vote-Skip-Status                    |
| `2`         | **ServerMessage** (Variante 2, identisches Verhalten wie 0)                      |
| `3`         | **Clear** — alle Messages leeren                                                 |

Die **JoinMessage** ist eine separate Mechanik: Sie erscheint im **Chat** (nicht als Static-UI-Text). Beim Betreten der
Lobby sendet der Server eine `ChatMessagePacket` mit `Sender = 0` (Server-Absender, kein echter Spieler). Diese
erscheint dann ohne Spielername im Chat.

### Wie `ReceiveServerMessages` die Messages verarbeitet

```csharp
// ReceiveServerMessages.cs (MonoBehaviour in GameScene):
ZeepkistNetwork.LobbyMessageReceived += OnLobbyMessageReceived;

void OnLobbyMessageReceived(byte messageType, Color theColor, string theMessage)
{
    switch (messageType)
    {
        case 0:
        case 2:
            serverMessage = BWF_FilterString(theMessage, FilterPurpose.chat);
            serverColor   = theColor;
            break;
        case 1:
            voteskipMessage = theMessage;
            voteskipColor   = theColor;
            break;
        case 3:
            serverMessage   = "";
            voteskipMessage = "";
            break;
    }
    hasChanged = true;
    // In Update(): PlayerManager.Instance.currentMaster.OnlineGameplayUI
    //              .SetServerMessageText(serverMessage, serverColor)
    //              .SetVoteskipMessageText(voteskipMessage, voteskipColor)
}
```

### Lokal setzen (nur auf diesem Client)

```csharp
PlayerManager.Instance.currentMaster.OnlineGameplayUI.SetServerMessageText("Text", Color.cyan);
PlayerManager.Instance.currentMaster.OnlineGameplayUI.SetVoteskipMessageText("Vote: 3/5", Color.yellow);
PlayerManager.Instance.currentMaster.OnlineGameplayUI.spectatorUI.SetServerMessageText("Text", Color.cyan);
```

### FÜR ALLE SPIELER senden (Host-only, via Packet)

Das Spiel hat keine eingebaute öffentliche `SendServerMessage()`-Methode in `ZeepkistNetwork`.  
**Der korrekte Weg als Mod-Host:** Das Packet direkt senden:

```csharp
// Nur Host/HasHostPowers darf das!
if (ZeepkistNetwork.IsMasterClient || ZeepkistNetwork.LocalPlayer.hasHostPowers)
{
    // ServerMessage für alle (type 0):
    ZeepkistNetwork.NetworkClient.SendPacket(new LobbyMessagePacket
    {
        messageType = 0,          // 0/2 = ServerMessage, 1 = VoteSkip, 3 = Clear
        theMessage  = "Meine Nachricht",
        colorR      = (byte)(color.r * 255),
        colorG      = (byte)(color.g * 255),
        colorB      = (byte)(color.b * 255)
    });

    // Alles clearen (type 3):
    ZeepkistNetwork.NetworkClient.SendPacket(new LobbyMessagePacket
    {
        messageType = 3, theMessage = "", colorR = 255, colorG = 255, colorB = 255
    });
}
```

### JoinMessage für alle (via Custom Chat)

```csharp
// Eine Chat-Nachricht "vom Server" senden (kein Spielername):
ZeepkistNetwork.SendCustomChatMessage(
    sendToEveryone: true,
    optionalTargetSteamID: 0,
    message: "Willkommen in der Lobby!",
    hostnamePREFERREDCAPS: "SERVER"
);
// → erscheint als "[SERVER]: Willkommen in der Lobby!" im Chat aller Spieler
```

### TMP-Felder in `OnlineGameplayUI`

```csharp
[SerializeField] private TMP_Text serverMessageText;           // normales Layout
[SerializeField] private TMP_Text serverMessageText_alternate; // alternatives Layout
[SerializeField] private TMP_Text voteskipText;
[SerializeField] private TMP_Text voteskipText_alternate;

// Sichtbarkeit: ServerMessage nur wenn CanShowServerMessages() == true
// = GameState 0 + kein eigenes Ergebnis + kein Timeout-Text
bool canShow = PlayerManager.Instance.currentMaster.OnlineGameplayUI.CanShowServerMessages();
```

---

## 16. TimerChanged vs TimeChanged

Dies ist ein wichtiger Unterschied:

### `ChangeLobbyTimerPacket` → `OnChangeLobbyTimer()`

```csharp
// Was es ist: Regelmäßige Server-Broadcasts des aktuellen Timer-STRINGS
// Wird empfangen: jede Sekunde (oder wenn sich der String ändert)
// Was sich ändert: NUR der Anzeige-String
CurrentLobby.timeLeftString = packet.TimeLeftString;
// → "1:23", "0:59", "PODIUM" etc.

// Event: KEINS — kein ZeepkistNetwork-Event dafür
```

### `ChangeLobbyTimePacket` → `OnChangeLobbyTime()`

```csharp
// Was es ist: Vollständiges Update der Rundenzeit-Parameter
// Wird empfangen: wenn Host die Rundenzeit ändert oder Level wechselt
// Was sich ändert: RoundTime, PlaylistTime UND LevelLoadedAtTime
CurrentLobby.RoundTime          = packet.NewRoundTime;
CurrentLobby.PlaylistTime       = packet.NewRoundTime;
CurrentLobby.LevelLoadedAtTime  = packet.NewLevelLoadedAtTime;

// Event: KEINS direkt, aber LobbyPropertiesChanged kann folgen
```

### Zusammenfassung

| Packet                   | Zweck                        | Häufigkeit   | Beeinflusst                      |
|--------------------------|------------------------------|--------------|----------------------------------|
| `ChangeLobbyTimerPacket` | Anzeige-String aktualisieren | Jede Sekunde | `timeLeftString`                 |
| `ChangeLobbyTimePacket`  | Rundenzeit-Parameter setzen  | Bei Änderung | `RoundTime`, `LevelLoadedAtTime` |

---

## 17. Nächstes Level setzen, Runde beenden, Zeit verlängern

> **Alle diese Aktionen erfordern `IsMasterClient || hasHostPowers`!**

### Nächstes Level aus der Playlist setzen

```csharp
// Index des nächsten Levels in der Playlist setzen:
ZeepkistNetwork.CurrentLobby.NextPlaylistIndex = 3; // Level-Index (0-basiert)

// Dann die Playlist ans Server senden:
// (intern: SendLobbyPlaylistToServer - private, daher via Packet direkt)
ZeepkistNetwork.NetworkClient.SendPacket(new ChangeLobbyPlaylistPacket
{
    NewTime      = ZeepkistNetwork.CurrentLobby.RoundTime,
    IsRandom     = ZeepkistNetwork.CurrentLobby.PlaylistRandom,
    playlist_all = ZeepkistNetwork.CurrentLobby.Playlist,
    CurrentIndex = ZeepkistNetwork.CurrentLobby.CurrentPlaylistIndex,
    NextIndex    = ZeepkistNetwork.CurrentLobby.NextPlaylistIndex
});
```

### Runde sofort beenden (GameState auf Podium setzen)

```csharp
// GameState 1 = Runde endet (Buffer), 2 = Podium
ZeepkistNetwork.NetworkClient.SendPacket(new ChangeLobbyGameStatePacket
{
    GameState = 1  // oder 2 direkt für Podium
});
// → Server verarbeitet und broadcast an alle Spieler
// → LobbyGameStateChanged Event wird bei allen gefeuert
```

### Rundenzeit verlängern / ändern

```csharp
double neueZeit = 120.0; // 120 Sekunden ab jetzt
ZeepkistNetwork.NetworkClient.SendPacket(new ChangeLobbyTimePacket
{
    NewRoundTime         = neueZeit,
    NewLevelLoadedAtTime = ZeepkistNetwork.Time  // jetzt als neuen Startpunkt
});
// → Alle Clients erhalten OnChangeLobbyTime → RoundTime + LevelLoadedAtTime aktualisiert
```

### Level mit spezifischem Workshop-Item laden

```csharp
// 1. Playlist modifizieren:
var newLevel = new OnlineZeeplevel
{
    UID         = "level-uid",
    workshopID  = 12345678UL, // Steam Workshop ID
    Name        = "Mein Level"
};
ZeepkistNetwork.CurrentLobby.Playlist.Add(newLevel);
ZeepkistNetwork.CurrentLobby.NextPlaylistIndex = ZeepkistNetwork.CurrentLobby.Playlist.Count - 1;

// 2. Playlist senden:
ZeepkistNetwork.NetworkClient.SendPacket(new ChangeLobbyPlaylistPacket
{
    NewTime      = ZeepkistNetwork.CurrentLobby.RoundTime,
    IsRandom     = false,
    playlist_all = ZeepkistNetwork.CurrentLobby.Playlist,
    CurrentIndex = ZeepkistNetwork.CurrentLobby.CurrentPlaylistIndex,
    NextIndex    = ZeepkistNetwork.CurrentLobby.NextPlaylistIndex
});

// 3. Runde beenden → Server lädt nächstes Level:
ZeepkistNetwork.NetworkClient.SendPacket(new ChangeLobbyGameStatePacket { GameState = 1 });
```

### `OnlineChatUI.SendChatMessage()` — Der "normale" Weg

```csharp
// Normaler Chat (jeder Spieler):
ZeepkistNetwork.NetworkClient.SendPacket(new ChatMessagePacket
{
    Message = "/skip",  // Server interpretiert das
    Badges  = new List<string>()
});
```

---

## 18. ChatColor

```csharp
// Lokale Farbe berechnen (aus Settings):
Color myColor = PlayerManager.Instance.GetChatColor();
// → Color.HSVToRGB(Settings.online_name_color_H, Settings.online_name_color_S, Settings.online_name_color_V)

// Farbe wird mit jedem Positions-Update mitgesendet:
// packet.chatColor = PlayerManager.Instance.GetChatColor();
// Beim Empfang: zeepkistNetworkPlayer.chatColor = packet.chatColor;

// Im Chat-Rendering (OnlineChatUI.GetChatMessage):
string colorHex = ColorUtility.ToHtmlStringRGB(chatMessage.Player.chatColor);
// "<#FF8000><b>Spielername</b></color>: Nachricht"
```

---

## 19. Lifecycle: PlayerConnect & PlayerConnect

### Player-Connect

```
[PlayerConnectedPacket empfangen]
→ ZeepkistNetwork.OnPlayerConnected(packet)
→ new ZeepkistNetworkPlayer(uid, steamID, backupName, playerTag)
    ├─ isLocal=true: SetUsername(SteamClient.Name) → sofort
    └─ isLocal=false: RequestUsername()
        ├─ SteamFriends.RequestUserInformation(steamID)
        ├─ Bereits bekannt: SetUsername(friend.Name) → sofort
        └─ Noch nicht bekannt: wartet auf OnFriendPersonaStateChange
            → SetUsername() → PlayerConnected?.Invoke(this)
→ Players.Add(uid, player)
→ ZeepkistNetwork.PlayerConnected?.Invoke(player)
   ← Abonnenten: OnlineGameplayUI, HostControlsMenu, MutePlayerUI, ...
```

### Player-Disconnect

```
[PlayerDisconnectedPacket empfangen]
→ ZeepkistNetwork.OnPlayerDisconnected(packet)
→ Players.Remove(uid)
→ ZeepkistNetwork.PlayerDisconnected?.Invoke(player)
```

### Lobby-Lifecycle

```
1. ConnectToMasterServer() → ConnectedToMasterServer
2. CreateLobby/JoinLobby → ConnectedToGameServer → LoadScene("GameScene")
3. LevelDataReceived → DoLoad → GameState 0 → LobbyGameStateChanged
4. GameState 0: Spielen (PlayerResultsChanged, LeaderboardUpdated)
5. GameState 1: Buffer (~3s) → LobbyGameStateChanged
6. GameState 2: Podium (~10s) → LobbyGameStateChanged
7. → LevelDataReceived (nächstes Level) → zurück zu 3.
8. Disconnect() → DisconnectedFromGameServer → LoadScene("3D_MainMenu")
```

---

## 20. Vollständige Methodenreferenz

| Was                       | Methode / Property                                                                                                      |
|---------------------------|-------------------------------------------------------------------------------------------------------------------------|
| Netzwerk verbinden        | `NetworkClientManager.Instance.ConnectToMasterServer()`                                                                 |
| Lobby erstellen           | `ZeepkistNetwork.CreateLobby(name, maxPlayers, isPublic)`                                                               |
| Lobby beitreten           | `ZeepkistNetwork.JoinLobby(id)`                                                                                         |
| Trennen                   | `ZeepkistNetwork.Disconnect("reason")`                                                                                  |
| Ist Host?                 | `ZeepkistNetwork.IsMasterClient`                                                                                        |
| Alle Spieler              | `ZeepkistNetwork.PlayerList`                                                                                            |
| Leaderboard               | `ZeepkistNetwork.Leaderboard`                                                                                           |
| Chat senden               | `NetworkClient.SendPacket(new ChatMessagePacket { Message = msg, Badges = new List<string>() })`                        |
| Custom Chat an alle       | `ZeepkistNetwork.SendCustomChatMessage(true, 0, msg, "SERVER")`                                                         |
| Chat-Farbe (lokal)        | `PlayerManager.Instance.GetChatColor()`                                                                                 |
| Timer-String              | `ZeepkistNetwork.CurrentLobby.timeLeftString`                                                                           |
| Timer als Float           | `ZeepkistNetwork.CurrentLobby.RoundTime` (double)                                                                       |
| Verbleibende Zeit         | `(CurrentLobby.LevelLoadedAtTime + RoundTime) - ZeepkistNetwork.Time`                                                   |
| GameState                 | `ZeepkistNetwork.CurrentLobby.GameState` (0/1/2)                                                                        |
| Runde beenden             | `NetworkClient.SendPacket(new ChangeLobbyGameStatePacket { GameState = 1 })`                                            |
| Rundenzeit ändern         | `NetworkClient.SendPacket(new ChangeLobbyTimePacket { NewRoundTime = t, NewLevelLoadedAtTime = ZeepkistNetwork.Time })` |
| Nächstes Level (Playlist) | `CurrentLobby.NextPlaylistIndex = idx` + `SendPacket(new ChangeLobbyPlaylistPacket{...})`                               |
| ServerMessage an alle     | `NetworkClient.SendPacket(new LobbyMessagePacket { messageType=0, theMessage=msg, colorR/G/B=... })`                    |
| Alles clearen             | `NetworkClient.SendPacket(new LobbyMessagePacket { messageType=3 })`                                                    |
| ServerMessage lokal       | `OnlineGameplayUI.SetServerMessageText(text, color)`                                                                    |
| Lobby-Name ändern         | `ZeepkistNetwork.CurrentLobby.UpdateName(name)`                                                                         |
| Max-Spieler ändern        | `ZeepkistNetwork.CurrentLobby.UpdateMaxPlayers(max)`                                                                    |
| Freeplay starten          | `PlayerManager.singlePlayer=true` + `LoadScene("SelectLevelScene")`                                                     |
| Level für Gameplay        | `manager.loader.PrimeForGameplay_v15(gm, jsonStr, skybox)`                                                              |
| Level für Editor          | `central.manager.loader.PrimeForLevelEditor_v15(central, jsonStr, skybox)`                                              |
| Level serialisieren       | `central.saveLoad.ConvertCurrentLevelStateToJSON_v15_string()`                                                          |
| Level-Ordner              | `ZeepkistFolders.GetLevelsFolder()`                                                                                     |
| Backup speichern          | `central.saveLoad.SaveBackup(isAutosave: bool)`                                                                         |
| Level-Format prüfen       | `GeneralLevelLoadStatic.IsThisLevelDataStringV15(str)`                                                                  |
| Level aus String          | `GeneralLevelLoadStatic.ReadRawLevelDataForScriptableObject(...)`                                                       |
| Level-JSON parsen         | `JsonConvert.DeserializeObject<v15LevelJSON>(jsonStr)`                                                                  |
| Block → JSON              | `block.ConvertBlockToJSON_v15()`                                                                                        |
| JSON → Block              | `block.LoadProperties_v15(BlockPropertyJSON, true)`                                                                     |
| Audio abspielen           | `AudioManager.Instance.Play(AudioItemScriptableObject)`                                                                 |
| FMOD Audio                | `AudioEvents.XYZ.Play(transform)`                                                                                       |
| Gizmo-Position            | `central.gizmos.SetMotherPosition(Vector3)`                                                                             |
| Blöcke bewegen            | `central.selection.TranslatePositions(Vector3 delta)`                                                                   |
| Alle Blöcke               | `central.saveLoad.GetAllBlockPropertiesCurrentlyInLevel()`                                                              |
| Gizmo deaktivieren        | `central.gizmos.Deactivate()`                                                                                           |
| Grid snappen              | `central.gizmos.SnapToGridXZ()` / `SnapToGridY()`                                                                       |
| Test-Map starten          | `central.testMap.TestMap()`                                                                                             |
| Workshop laden            | `await WorkshopManager.Instance.DownloadWorkshopLevel(fileId)`                                                          |
| Workshop verarbeiten      | `await WorkshopManager.Instance.ProcessItem(extendedItem)`                                                              |
| Spieler-Event             | `ZeepkistNetwork.PlayerConnected += handler`                                                                            |
| Lobby-State-Event         | `ZeepkistNetwork.LobbyGameStateChanged += handler`                                                                      |
| Host-Wechsel-Event        | `ZeepkistNetwork.MasterChanged += handler`                                                                              |
| Spieler kicken            | `ZeepkistNetwork.KickPlayer(player)`                                                                                    |
| Host wechseln             | `ZeepkistNetwork.SetMasterClient(player)`                                                                               |
