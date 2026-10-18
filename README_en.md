VR-fire-simulation · VR Campus Fire Evacuation Training

A Unity + SteamVR/OpenXR fire-evacuation VR training set in a four-storey teaching building. Starting in Classroom 401 on the fourth floor, the player completes the VR control tutorial, responds to the fire alarm, wets a towel for protection, evacuates via the stairs, and either waits for rescue at the refuge area or assembles outdoors — learning correct fire-escape behaviour along the way.

> Smoke spread, health and time values in this application are training approximations. They do not constitute a fire-compliance design for a real building, nor do they guarantee safety in a real fire.

 ⚠ Entering & Exiting When Tracking Is Unstable (Read First)

On some devices the headset may fail to track the hands at start-up, so the ray pointer cannot click "Start":

- **If the ray cannot click "Start", use the A button on the right controller to enter.** Once in the training scene, press and hold the **A button for about 0.75 seconds** to officially begin (as shown on the on-screen prompt). On tutorial reading pages, a single A press is enough to continue/skip — no ray click required.
- **To exit the program: press `Ctrl + Esc` on the keyboard.**
- Pause / resume: **click the left thumbstick** (press it straight down).

 Requirements

| Item | Requirement |
| --- | --- |
| Unity | 2022.3.62f3 |
| Render pipeline | URP (Universal Render Pipeline) 14.0.12 |
| VR SDK | OpenXR 1.14.3 + SteamVR (OpenVR), using the project's existing SteamVR input bindings |
| Target device | Quest 3 (Quest Link / Air Link to a PC with SteamVR), and other PC VR headsets compatible with SteamVR |
| Project type | Unity source project (not a Windows .exe and not a Quest APK) |

Quick Start

1. Clone this repository, then in Unity Hub choose `Add → Add project from disk` and select the repository root folder.
2. Open the project with **Unity 2022.3.62f3**; the first import takes several minutes.
3. Open `Assets/Scenes/MainMenu/MainMenu.unity` and press Play. For a build, make sure Build Settings contains the two enabled scenes `MainMenu` and `CampusTraining` (already configured).
4. Before putting on the headset, confirm SteamVR recognises the headset and both controllers. Prefer room-scale (Floor) tracking origin; do not force the viewpoint to 1.70 m.

Without a VR device you can preview the layout in Editor Play Mode: `Enter` confirms tutorial pages, `Backspace` skips the control tutorial, and `WASD` moves around. This is a layout preview only — hand interaction still requires real controllers.

 Controls

| Action | Input |
| --- | --- |
| Ray interaction / UI clicks | Aim either controller's ray at a button until it turns green, then squeeze the corresponding **trigger** |
| Grab, push doors/windows, throw | Hold the **trigger** to grab, push or swing, then release |
| Teleport | Push either thumbstick forward, aim at a green landing spot, then re-centre; up to 3 m per hop, only on valid standing spots — no floor-to-floor jumps or window jumps |
| Pause / resume | **Left thumbstick click** |
| Tutorial confirm / officially begin / skip reading pages | Right controller **A button** (hold ~0.75 s to begin) |
| Fallback entry when the ray does not work | Right controller **A button** |
| Quit the program | Keyboard **`Ctrl + Esc`** |

 Training Flow

1. On the main menu, point the ray at **Start**; you spawn in Classroom 401 on the fourth floor.
2. Watch the 3D control tutorial (look around, two-handle grab / push-door / throw pages), then enter free practice: move around, pick up and place practice books, and open the rear window.
3. Press and hold the right controller A button for 0.75 s to begin** (the trigger is not used). A ~22-second first-person cut-scene plays; locomotion and object interaction are locked during the animation and you do not need to physically sit down.
4. The alarm sounds. Read the stand-up instruction and press A to stand up virtually; free control is restored.
5. Push the classroom door open (at least 15°), observe the conditions outside, and read the fire-safety knowledge page.
6. Task 1 · Find a towel**: follow the cyan floor arrows and glowing door sign to Restroom 411 opposite and pick up a dry towel.
7. Task 2 · Wet it and evacuate**: use your free hand to turn on the tap, hold the towel in the running water for 1.5 s to wet it, keep holding it over your mouth and nose, and head downstairs.
8. Route choice: the downward branch of Stair A on the third floor is blocked by training debris (a tipped cabinet and boxes) — return to the corridor and take **Stair B**, which leads continuously down to the ground floor.
9. (Optional side quest) On the second-floor corridor, click the left and right push handles of the wheelchair with both hands to escort the wheelchair-using student; push them into the 2F refuge area and stay for 1 second to complete it. Skipping this does not affect the pass.
10. Finish in one of two ways: use the walkie-talkie / call bell in the 2F refuge area to confirm you are waiting for rescue; or continue down to the first floor, exit through the lobby to the outdoor assembly point and stay there for 2 seconds.
11. The results screen shows your practice time and a short debrief. You can retry or return to the main menu.

Fire-Safety Boundaries

- Never use the elevator and  never jump from a window during a fire. Interacting with the elevator buttons or windows triggers bilingual warnings; a window operated a second time closes automatically after 2 seconds.
- A wet towel held over your mouth and nose only  slows smoke damage — it does not filter carbon monoxide** and cannot replace real evacuation. Damage returns to normal when you release the towel or leave the smoke, so leave the smoke quickly.
- The smoke is a staged visual approximation, not a fluid simulation or a real fire-prediction model. The stairs, refuge area and assembly point are virtual training routes and are not a fire-compliance design for a real building.
- Before formal teaching use, fire-safety professionals should review the routes and copy.

 Feature Modules

| Module | Folder | Contents |
| --- | --- | --- |
| A · Player locomotion | `Assets/Scripts/Modules/A_Player` | Comfort teleport/walk, footstep sounds, practice-book grab and throwing |
| B · Doors, windows, elevator | `Assets/Scripts/Modules/B_DoorsWindowsElevator` | Two-handed push doors/windows, bilingual elevator / window-jump warnings, automatic window closing |
| C · Towel & water | `Assets/Scripts/Modules/C_TowelWater` | Tap on/off, running water, dry/wet towels, pickup/drop/face-covering |
| D · Fire, smoke & health | `Assets/Scripts/Modules/D_FireSmokeHealth` | Fire, floor-by-floor natural smoke, smoke damage, wet-towel mitigation |
| E · Lesson & UI | `Assets/Scripts/Modules/E_LessonTasksUI` | 3D tutorial, opening animation, task/route/destination highlights, HUD and real-time minimap |
| F · Rescue side quest | `Assets/Scripts/Modules/F_RescueStudent` | Two-handed wheelchair pushing, follow-in-front behaviour, delivery to the refuge area (optional) |

 Project Structure

Assets/
├── Scenes/
│   ├── MainMenu/MainMenu.unity        # Entry menu (first build scene)
│   ├── Campus/CampusTraining.unity    # Main training scene (four-storey building)
│   ├── Legacy/                        # Historical versions and old scene backups (do not delete)
│   └── Tests/                         # Health/time bar and system UI test scenes
├── Scripts/
│   ├── Modules/A_Player … F_RescueStudent   # The six feature modules
│   ├── Campus/                        # Scene integration and Tools > Campus editor utilities
│   ├── Interaction/                   # Doors, windows, elevator and safety-ray interactions
│   ├── Health/                        # Timing, smoke, towels, audio and HUD
│   ├── SystemUI/                      # Main menu, pause, results and navigation
│   ├── Player/                        # Controllers, hand models, VR locomotion and tutorial
│   ├── Journey/Runtime/               # Training flow and SessionLogger
│   └── Shared/Campus/                 # Shared input (CampusControllerInput, etc.)
├── Prefabs/                           # Environment, HUD, UI and rescue-student prefabs
├── Art/  Audio/  Fonts/               # Art, audio and bilingual TextMesh Pro fonts
├── Settings/Rendering/                # URP rendering configuration
├── ThirdParty/                        # Third-party model and VFX asset packs
├── SteamVR/  SteamVR_Input/  XR/  XRI/   # SDK convention paths — do not move or delete
└── StreamingAssets/SteamVR/           # SteamVR action bindings (actions.json)
Documentation/                         # Design, update, acceptance and team-collaboration docs


Documentation Index

- [CampusGuide.md](Documentation/CampusGuide.md) — Four-storey campus scene and integration overview (authoritative entry point, Chinese)
- [CampusRevisionGuide.md](Documentation/CampusRevisionGuide.md) — Continuous walking, two-handed push/pull, restrooms, fire/smoke and minimap version (Chinese)
- [GuidedOpening.md](Documentation/GuidedOpening.md) — Guided tutorial, first-person opening animation and two-step towel tasks (Chinese)
- [RescueStudentSideQuest.md](Documentation/RescueStudentSideQuest.md) — Wheelchair-student rescue side quest (Chinese)
- [RestroomInteractions.md](Documentation/RestroomInteractions.md) — Restroom tap and wet-towel mechanics (Chinese)
- [PresentationUpdate.md](Documentation/PresentationUpdate.md) — UI anti-occlusion, natural smoke and environment details (Chinese)
- [EvacuationUpdate.md](Documentation/EvacuationUpdate.md) / [ComfortUpdate.md](Documentation/ComfortUpdate.md) — Evacuation and comfort updates (Chinese)
- [TeamModules/GUI_操作说明.md](Documentation/TeamModules/GUI_操作说明.md) — Six-package packaging, branching and PR workflow (Chinese)
- `Documentation/PlayModeChecks.txt`, `RevisionChecks.txt`, `LessonChecks.txt` — Headset-free automated regression records

 Known Limitations

- Quest 3 on-device verification (Link / Air Link / Android APK) has not been completed; stereo display, tracking, haptics, comfort and frame rate still need team members to test with a headset.
- This repository is a Unity source project: it contains no runnable .exe or APK. Before building, verify the SteamVR action bindings (`Assets/StreamingAssets/SteamVR/actions.json`).
- Redistribution licences for third-party models, audio and fonts must be checked before any public upload.
- The main scene is not generated at runtime; everything is saved into the `.unity` scene. Do not rebuild the main scene with the legacy AssemblyAreaSceneBuilder. See the documentation for the `Tools > Campus` menus, and back up manual changes before rebuilding.

 Team Members

| Name | Student ID | GitHub account |
| --- | --- | --- |
| Zhao Yining | 26040695g | zhangoyini |
| CHEUNG Kwan Kit (Henry) | 26007063g | cheungkk704 |
| Wu Zhengyang | 26071286g | wuxiaoyang |
| ZHU Zijie | 26051898g | zJaey-00 |
| JIAO Ruikang | 26103438g | RJ |
| JIANG Chenghao | 26057555g | Cheng-Jimmy |
