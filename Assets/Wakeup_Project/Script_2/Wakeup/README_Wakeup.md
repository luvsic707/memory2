# Wakeup Scene Dialogue Setup Guide

## 1. Create the UI
1.  In the **Wakeup** scene, create a new **Canvas** (or use existing).
2.  Create a **Panel** for the dialogue background (set color to black or transparent).
3.  Add the `WakeupDialogueUI` component to this Panel (or a child object).
4.  Create child objects for:
    *   **Speaker Name** (TextMeshProUGUI)
    *   **Dialogue Text** (TextMeshProUGUI)
    *   **Choice Container** (Empty GameObject with VerticalLayoutGroup)
5.  Create a **Button Prefab**:
    *   Create a Button with a TextMeshProUGUI child.
    *   Save it as a Prefab.
6.  Assign these references to the `WakeupDialogueUI` component in the Inspector.

## 2. Create Dialogue Data
1.  Right-click in the **Project** window -> **Create** -> **Wakeup** -> **DialogueData**.
2.  Name it `WakeupIntroDialogue`.
3.  Add nodes to the list:
    *   **Node 0**: Speaker "???", Text "Darkness...", Choices: "Where am I?".
    *   **Node 1**: Speaker "Reptilian Brain", Text "You are nowhere...", Choices: "Who are you?".
    *   ...
    *   **Last Node**: Check `Is End Node`.

## 3. Setup Logic
1.  Create an empty GameObject named `WakeupManager`.
2.  Add the `WakeupSequenceManager` component.
3.  Assign:
    *   **Dialogue UI**: The object with `WakeupDialogueUI`.
    *   **Dialogue Data**: The `WakeupIntroDialogue` asset you created.
    *   **Timeline**: The `PlayableDirector` component that controls the camera/lights animation.

## 4. Run Verification
1.  Start from **Bootstrap** scene (to load Global Managers).
2.  Click Start in Main Menu.
3.  Watch the Wakeup scene:
    *   Black screen for 1 second.
    *   Dialogue UI fades in.
    *   Clicking advances text.
    *   Choices work.
    *   End of dialogue -> UI Fades out -> Timeline plays -> Debt starts accumulating (check Console).
