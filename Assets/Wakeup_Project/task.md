# Task Checklist - Refactor Game Start Sequence

- [x] Modify `GlobalUIManager.cs` to add `OnContractSigned` and `OnGameStarted` events. <!-- id: 0 -->
- [x] Implement `InitiateGameSequence` in `GlobalUIManager.cs`. <!-- id: 1 -->
- [x] Modify `GlobalMentalState.cs` to subscribe to `OnContractSigned` (reset data) and `OnGameStarted` (unfreeze). <!-- id: 2 -->
- [x] Modify `MentalStatsUI.cs` to subscribe to `OnGameStarted` (Show UI) and `OnGameEnded` (Hide UI). <!-- id: 3 -->
- [x] Update `WakeupSequenceManager.cs` to call `InitiateGameSequence`. <!-- id: 4 -->
- [ ] Create `walkthrough.md` (in Chinese) to verify the changes. <!-- id: 5 -->
