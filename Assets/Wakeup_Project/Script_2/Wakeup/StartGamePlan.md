# Implementation Plan - Refined Game Start Sequence

# Goal Description
Refactor the "Start Game" sequence to be fully event-driven and decoupled, separating the logic into distinct phases: **Contract Signing** (Data Initialization), **World Loading** (Scene Transition), and **Simulation Start** (Gameplay Active). This ensures that `GlobalUIManager` acts as a coordinator rather than a monolith, and that UI/Data systems react to state changes rather than being manually poked.

## User Review Required
> [!IMPORTANT]
> This refactor introduces a `GameEvent` system (simple C# Actions) to decouple components further.
> The `StartGameLoop` will be broken down.

## Proposed Changes

### 1. GlobalArchitecture
#### [MODIFY] [GlobalUIManager.cs](file:///Users/macbookpro/Documents/memory/Assets/Wakeup_Project/Script_2/GlobalUIManager.cs)
- Introduce `public event Action OnGameStarted;`
- Introduce `public event Action OnSceneLoaded;`
- Refactor `StartGameLoop` to `InitiateGameSequence`.
- In `InitiateGameSequence`:
    1.  Trigger `OnContractSigned` (Internal logic to reset MentalModel).
    2.  Load Scene asynchronously.
    3.  In `OnSceneLoaded` callback, trigger `OnGameStarted`.

#### [MODIFY] [GlobalMentalState.cs](file:///Users/macbookpro/Documents/memory/Assets/Wakeup_Project/Script_2/GlobalMentalState.cs)
- Subscribe to `GlobalUIManager.OnGameStarted` to unfreeze itself.
- Remove `ResetAndStartGame()` public coupling; instead, expose a `ResetData()` method that listens to the "Contract Signed" phase.

#### [MODIFY] [MentalStatsUI.cs](file:///Users/macbookpro/Documents/memory/Assets/0_Bootstrap/MentalStatsUI.cs)
- Subscribe to `GlobalUIManager.OnGameStarted` to `ShowUI()`.
- Subscribe to `GlobalUIManager.OnGameEnded` (future proofing) or similar to `HideUI()`.
- Remove manual calls to `ShowUI()` from MentalState.

### 2. Wakeup Scene
#### [MODIFY] [WakeupSequenceManager.cs](file:///Users/macbookpro/Documents/memory/Assets/Wakeup_Project/Script_2/Wakeup/WakeupSequenceManager.cs)
- Call `GlobalUIManager.Instance.SignContractAndStart()` instead of generic loop.

## Verification Plan

### Manual Verification
1.  **Play Mode (Bootstrap)**:
    - Click **Start**.
    - Verify Wakeup Scene plays.
    - Click through dialogue -> End.
    - **Observe Console**:
        - "Contrac Signed" log (Data Reset).
        - "Loading Scene" log.
        - "Game Started" log (Mental State Unfrozen).
    - **Verify HUD**:
        - Check that HUD appears *only* after the new scene is loaded and game starts, not before.
