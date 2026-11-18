# Phase 8: UI, customisation, shop and monetisation

Phase 8 adds three things:

- **Game UI:** all 22 screens, built on UI Toolkit with one shared theme.
- **Customisation:** the locker and a 3D menu stage.
- **Monetisation client:** the shop, season pass, missions and daily reward, plus real-money gem packs through Unity IAP, which the backend verifies.

## 1. How the UI is put together

| Piece | File | Role |
|---|---|---|
| `AppUI` | `Scripts/UI/Framework/AppUI.cs` | The single UI Toolkit root. It is created at startup and survives scene loads. It also handles the safe area, toasts and busy state. It is never created on dedicated servers, bot clients or headless players. |
| `UiFlow` | `Scripts/UI/Framework/UiFlow.cs` | Maps game state to screens: Boot → Splash, MainMenu → Home stack, a running match → screens per `MatchPhase`, other scenes → Offline HUD. |
| `ScreenNavigator` | `Scripts/UI/Framework/ScreenNavigator.cs` | The screen stack. There are three layers. **Page** screens hide whatever is below them. **Overlay** screens (dialogs, loading) sit on top of the page. **Hud** screens use `PickingMode.Ignore`, so on-screen touch controls keep working. |
| `UIScreen` | `Scripts/UI/Framework/UIScreen.cs` | Base class for screens. `Build` runs once. `OnShow` runs every time the screen appears. `Tick` runs each frame while the screen is visible. `OnBack` handles Android back and Escape. |
| `Ui` | `Scripts/UI/Framework/Ui.cs` | Element factory: buttons, headers, currency pills, progress bars, swatches, inputs. |
| `UiData` | `Scripts/UI/Framework/UiData.cs` | Profile helpers. When offline, ownership falls back to the starter items plus the last inventory seen online. It also equips items and holds the selected mode and practice toggle. |
| Theme | `Art/UI/KnockRush.uss` + `KnockRushRuntimeTheme.tss` | Brand colours, buttons, cards, HUD and fonts. Body text uses Nunito; `.kr-display` text uses Lilita One. Both fonts are SIL OFL 1.1; the licences are in `Art/UI/Fonts`. |
| Panel | `Settings/UI/KnockRushPanelSettings.asset` | Scales with screen size against a 1920×1080 reference, matching width and height equally, so it works on any phone aspect ratio. |
| Stage | `Scripts/UI/Stage/MenuBeanStage.cs` | The MainMenu 3D bean. It wears the equipped loadout or a locker preview, can be dragged to spin, plays emotes, and slides aside while the locker is open. |

`KnockRush > Build > UI and Main Menu` (`UiContentBuilder`) generates the PanelSettings, `UiResources` and the `MainMenu` scene. `Build All Generated Content` includes it.

## 2. The 22 screens

| # | Screen | Class | File |
|---|---|---|---|
| 1 | Splash | `SplashScreen` | `Screens/SystemScreens.cs` |
| 2 | Loading | `LoadingScreen` | `Screens/SystemScreens.cs` |
| 3 | Login / Registration | `LoginScreen` | `Screens/SystemScreens.cs` |
| 4 | Home | `HomeScreen` | `Screens/HomeScreen.cs` |
| 5 | Game Mode Selection | `ModeSelectScreen` | `Screens/PlayScreens.cs` |
| 6 | Matchmaking | `MatchmakingScreen` | `Screens/PlayScreens.cs` |
| 7 | Waiting Lobby | `WaitingLobbyScreen` | `Screens/PlayScreens.cs` |
| 8 | Private Room | `PrivateRoomScreen` | `Screens/PlayScreens.cs` |
| 9 | Friends & Party | `FriendsScreen` | `Screens/SocialScreens.cs` |
| 10 | Character Customization | `CustomizeScreen` (Locker) | `Screens/LockerScreens.cs` |
| 11 | Character Selection | `CharacterSelectScreen` | `Screens/LockerScreens.cs` |
| 12 | Shop | `ShopScreen` | `Screens/StoreScreens.cs` |
| 13 | Inventory | `InventoryScreen` | `Screens/LockerScreens.cs` |
| 14 | Season Pass | `SeasonPassScreen` | `Screens/StoreScreens.cs` |
| 15 | Leaderboard | `LeaderboardScreen` | `Screens/SocialScreens.cs` |
| 16 | Player Profile | `ProfileScreen` | `Screens/SocialScreens.cs` |
| 17 | Match HUD | `MatchHudScreen` | `Screens/MatchScreens.cs` |
| 18 | Round Qualification | `QualifiedScreen` | `Screens/MatchScreens.cs` |
| 19 | Elimination | `EliminatedScreen` | `Screens/MatchScreens.cs` |
| 20 | Final Victory | `VictoryScreen` | `Screens/MatchScreens.cs` |
| 21 | Match Results | `MatchResultsScreen` | `Screens/MatchScreens.cs` |
| 22 | Settings | `SettingsScreen` | `Screens/SystemScreens.cs` |

There are also three helper screens:

- `DialogScreen`: reusable confirm dialog.
- `OfflineHudScreen`: a "back to menu" button for solo map runs and the playground.
- A DEVELOPER section in Settings, shown in development builds only, with buttons for the Playground and Hammer Rush solo.

The in-match screens read only the replicated `MatchController` state, so they behave the same on a host, a client or in practice mode.

## 3. Economy on the client

The backend is the source of truth for every grant. The client only displays results and sends requests:

- **Daily reward and missions:** shown on Home. They call `claimDailyReward`, `getMissions` and `claimMission`.
- **Shop:**
  - 6 featured items per UTC day plus a reset countdown (`getShop`).
  - Bundles (`purchaseBundle`).
  - Single items (`purchaseShopItem`).
  - Gem packs (IAP, see below).
  - Free coins from rewarded video.
- **Season pass:** 30 tiers with free and premium tracks (`getSeason`, `claimSeasonReward`, `buyPremiumPass`). Premium costs 950 gems and is cosmetic only.
- **Locker:** the preview applies to the stage bean straight away.
  - EQUIP saves locally (so it works offline) and on the backend (`equipItems`, which checks ownership).
  - Locked items show their price and can be bought from the locker.
  - Season exclusives show "SEASON PASS REWARD".
- **Friends:** add by username, accept or decline requests (`getFriends` is new in this phase), and invite by sharing a private-room code.

## 4. Real-money purchases (Unity IAP 4.15)

`Scripts/Monetization/UnityIapPurchaseService.cs`:

1. The store is initialised lazily, the first time the Gems tab opens, with the four consumables `knockrush.gems.100/550/1200/2500`. These ids must match `GEM_PACKS` in the backend and the store listings.
2. `ProcessPurchase` returns **Pending**. The receipt goes to `validatePurchase`, which verifies it with Google Play and grants gems once per order id. Only then is the purchase confirmed (consumed). If the player is offline, the purchase stays pending and the store re-delivers it on the next launch.
3. The editor and development builds use Unity's fake store. Its receipts are sent as the `test` store, which only the Firebase emulator accepts.

### Store setup (needs your accounts)

- **Google Play Console:**
  1. Create the 4 managed products with the ids above.
  2. Link the Firebase project's service account under Users and permissions, with the "View financial data" and "Manage orders" permissions, so `validatePurchase` can call the Android Publisher API.
- **App Store Connect:**
  1. Create the same 4 consumables.
  2. Apple validation is the `apple` branch of `validatePurchase`. It is enabled in Phase 10, when the App Store Server API key is added as a secret.

## 5. Rewarded ads

`IRewardedAdService` is registered as `UnconfiguredRewardedAds`, so the button reads "VIDEOS UNAVAILABLE" until an ad SDK is added. To enable ads:

1. Import the Google Mobile Ads Unity plugin.
2. Implement `IRewardedAdService` with your rewarded ad unit ids.
3. Set the ad's server-side verification options: `userId` = the Firebase uid (`IBackendService.Uid`).
4. In AdMob, set the SSV callback URL to the deployed `adRewardCallback` function.

Rewards (50 coins, up to 5 per day) are granted only by that signed callback, never by the client.

## 6. Verifying

| What | How |
|---|---|
| Compile | `Tools/unity.sh compile` |
| UI flow tests (`UiFlowTests`, part of `Tools/unity.sh playmode`) | Splash → Home with the dressed stage bean and resolved fonts; every menu screen opens and closes; locker preview and revert; dialog overlay; a practice match drives Lobby → HUD → Results → Home |
| Screenshots of every screen | `Tools/unity.sh windows`, then `Builds/Windows/KnockRush.exe -uicapture <folder> -screen-width 1920 -screen-height 1080 -screen-fullscreen 0` (`UiCaptureRunner`) |
| Backend | `Tools/backend-test.sh` (the `getFriends` integration test is included) |
