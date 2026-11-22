# VR-fire-simulation · VR Campus Fire Evacuation Practice

A Unity VR course project set in a fictitious four-storey teaching building. Starting in Classroom 401, players learn the controller interactions, respond to an alarm, practise towel use, choose an alternative stair route, and reach the outdoor assembly point. An optional encounter lets players escort a wheelchair-using student to the second-floor refuge area and call for help before continuing their own evacuation.

This README describes the integrated source at [2fa72a9](https://github.com/zhangoyini/VR-fire-simulation/commit/2fa72a968684b39050dd715044aa337e63684f58). Implementation and earlier editor checks do not establish final headset acceptance or measured learning improvement.

> This is a practice prototype for a fictitious building. Smoke, health loss, protection and scores are simplified game mechanics. The application is not an official emergency procedure, a real building evacuation plan or a certification of fire-safety competence.

## Requirements

| Item | Project configuration |
| --- | --- |
| Unity | **2022.3.62f3** |
| Render pipeline | Universal Render Pipeline **14.0.12** |
| XR packages | OpenXR **1.14.3** and the bundled SteamVR/OpenVR package; Standalone settings include OpenVR and OpenXR loaders |
| Input | The supplied SteamVR action bindings, with Unity XR feature input where supported by the shared input adapter |
| Intended setup | Windows PC VR; Quest 3 connected through Quest Link / Air Link with SteamVR |
| Repository contents | Unity source project; export a runnable Windows build separately |

Keep the supplied SDK folders and input bindings in place. A standalone Quest Android APK workflow and other headset/runtime combinations have not received final acceptance.

## Open and run

1. Clone the repository. In Unity Hub, select **Add → Add project from disk** and choose the repository root.
2. Open it with **Unity 2022.3.62f3** and allow the initial asset and package import to finish.
3. Connect the headset and both controllers. Confirm tracking in the PC VR runtime before starting Play Mode. Use a floor-level tracking origin and a clear physical play area.
4. Open `Assets/Scenes/MainMenu/MainMenu.unity`, press **Play**, and select **Start** using a controller ray and trigger.
5. Follow the tutorial. The main exercise is in `Assets/Scenes/Campus/CampusTraining.unity`.

If controller tracking or menu interaction fails, restore tracking and check the active runtime and bindings before continuing. The current main-menu navigation has no implemented A-button entry shortcut. Use **Exit** on the main menu to quit a built application; stop Play Mode to leave the Editor preview.

### Export a Windows build

In **File → Build Settings**, select **PC, Mac & Linux Standalone → Windows** and confirm these enabled scenes in order:

1. `Assets/Scenes/MainMenu/MainMenu.unity`
2. `Assets/Scenes/Campus/CampusTraining.unity`

These scenes are already listed in the project settings. Build into a separate output directory. Distribute the **complete generated build directory**, including the executable, data folder and runtime files. Verify the SteamVR bindings at `Assets/StreamingAssets/SteamVR/actions.json` and test the exported build on the intended PC/headset combination.

### Editor preview without a headset

In the campus scene, **Backspace** bypasses the control tutorial and enters the Ready screen. After its reading delay, **Enter** starts the opening sequence. Enter is not a general tutorial-page advance key. Full interaction still requires tracked controllers.

The saved campus scene uses teleport-only locomotion. The code's WASD preview is available only when `CampusLocomotion.teleportOnly` is disabled; it is not enabled in the saved scene.

## Controls

The button names below use the Quest controller layout and the supplied bindings.

| Action | Input |
| --- | --- |
| Menu buttons, dialogue and tutorial **SKIP** | Aim a controller ray at the button, then press its **trigger** |
| Grab a towel or practice book | Hold the **trigger** near the object; release to let go |
| Open a door or window | Bring a hand to the highlighted edge, hold the **trigger**, move along the hinge arc, then release |
| Teleport | Push either thumbstick forward, aim at a valid green landing spot, then release the stick to centre |
| Map | Left controller **X** to open/close |
| Tasks | Left controller **Y** to open/close |
| Pause | **Left thumbstick click**; use **Resume** in the pause menu, including the tutorial's pause exercise |
| Start from the Ready screen, when shown | After the reading delay, hold right controller **A** for about **0.75 seconds** |
| Quit | Main menu **Exit**; in the Editor, stop Play Mode |

Teleport destinations are constrained by range, height, standing clearance and obstacles. The saved campus scene limits hops to 3 m and rejects jumps between floors or through windows. During wheelchair following, stair travel is refused so the player can finish the escort on the second floor.

## Exercise flow

1. **Learn the controls.** Complete looking/hand checks, teleporting, window operation, button pressing, picking up and replacing a book, pausing/resuming, and opening/closing the map and tasks. Explanation and demonstration pages wait for the player to select **SKIP**. Practical steps require the corresponding action and cannot be skipped using that button.
2. **Finish the tutorial.** Reach the highlighted spot near the classroom door and advance the completion page. The opening sequence then starts. If a Ready screen is shown instead, follow its A-button prompt after the reading delay.
3. **Watch the opening and alarm.** Scripted movement and object interaction are locked during the short classroom sequence. Standing is handled by the sequence; the player does not need to sit down physically or press A to stand. An eight-second main-task briefing follows before the timed exercise begins.
4. **Find the towel.** Open the classroom door and follow the scene cues to Restroom 411. Task 1 is activated after the briefing; its short notification appears after a brief delay.
5. **Wet and hold the towel.** Turn on the tap and hold the towel in running water for about 1.5 seconds. Continue holding the wet towel over the mouth/nose area to maintain the simulated protection effect.
6. **Choose the stair route.** Stair A's downward route is blocked on the third floor. Return to the corridor and use **Stair B**. Taking the safe route directly also satisfies the route objective. The map tracks the current floor; its room labels currently remain in Chinese.
7. **Optionally assist the student on 2F.** Approach the wheelchair-using student, open the dialogue and choose **Follow me**. The student follows valid movement/teleport paths. Stay on the second floor, open the refuge entrance and allow the student to arrive. Then press the refuge call button to complete the assistance task. Arrival alone is not the completed rescue condition.
8. **Continue to the outdoor assembly point.** The refuge call completes the optional task and prompts the player to continue evacuating. It does not end the main exercise. Descend to the ground floor (**1F**), pass through the exit and reach the schoolyard assembly zone. Opening the exit door alone does not win; assembly arrival completes the exercise. Health depletion ends the attempt in failure.
9. **Review and restart.** The outcome screen shows artwork and task-based stars. Select **View details** for five manually browsed report pages covering the attempt, tasks, route, health and feedback. **Restart** and **Main menu** are inside the details view. Restart reloads the campus scene and returns to the tutorial with the same reading/practical rules.

## Smoke and health feedback

The reviewed scene and shared settings use these simplified rates:

| State | Nominal health loss |
| --- | --- |
| Outside smoke | **0.02 HP/s** |
| Full-strength smoke after the smoke grace interval, without protection | **1.1 HP/s**: 0.1 base + 1.0 smoke damage |
| The same smoke with settled wet-towel face covering | **0.22 HP/s**, using a 0.2 multiplier |

Smoke has a **3-second grace interval**, protection has a **0.25-second settling interval**, and exposure strength varies by position. These are source-derived steady-state rates, not measured participant outcomes or guaranteed losses immediately after entry.

The final HUD uses a protection icon for active towel covering and an orange **Smoke · Move out** warning when exposed without protection. Earlier versions' three explanatory protection messages are not all retained. Removing or lowering the towel ends protection; holding it throughout the exercise does not make smoke harmless. The health bar is not a fixed countdown.

## Phase 3 changes and implementation evidence

The preparation baseline is [86b315b5](https://github.com/zhangoyini/VR-fire-simulation/commit/86b315b5a27932ff480420e90904d6689f313b69). These records identify subsequent changes and their integrated behavior.

| Area | Changes and GitHub records |
| --- | --- |
| Smoke/health | JIAO adjusted damage rates and protective feedback in [4e6eb04](https://github.com/zhangoyini/VR-fire-simulation/commit/4e6eb04). Later HUD work simplified protection text to an icon. |
| Retry, results and prompt timing | JIANG's [35ef461](https://github.com/zhangoyini/VR-fire-simulation/commit/35ef461) initially skipped repeated teaching and removed debrief rotation. Final integration retains manual review but returns retries to teaching. |
| Route and map | ZHU corrected third-floor arrows in [5ff1641](https://github.com/zhangoyini/VR-fire-simulation/commit/5ff1641), ground-floor cues in [aa11db4](https://github.com/zhangoyini/VR-fire-simulation/commit/aa11db4), room labels in [5090c75](https://github.com/zhangoyini/VR-fire-simulation/commit/5090c75), and debris contact prompts in [efd0a27](https://github.com/zhangoyini/VR-fire-simulation/commit/efd0a27). |
| Wheelchair assistance | Henry added dialogue-triggered following in [eaa9302](https://github.com/zhangoyini/VR-fire-simulation/commit/eaa9302) and stair/recovery constraints in [677fc90](https://github.com/zhangoyini/VR-fire-simulation/commit/677fc90). |
| Tutorial, refuge and debrief | WU refined demonstrations, refuge call confirmation and results in [d285ef9](https://github.com/zhangoyini/VR-fire-simulation/commit/d285ef9). |
| Task flow and integration | ZHAO refined timing/layout in [b88c1fa](https://github.com/zhangoyini/VR-fire-simulation/commit/b88c1fa), [cc34782](https://github.com/zhangoyini/VR-fire-simulation/commit/cc34782), [ef07f82](https://github.com/zhangoyini/VR-fire-simulation/commit/ef07f82) and [2fa72a9](https://github.com/zhangoyini/VR-fire-simulation/commit/2fa72a9). |

These records demonstrate implementation and integration. They do not by themselves establish issue acceptance or improved usability after the changes.

## Project structure

```text
Assets/
├── Scenes/
│   ├── MainMenu/MainMenu.unity         # Entry menu; first build scene
│   ├── Campus/CampusTraining.unity     # Integrated campus exercise
│   ├── Legacy/                        # Earlier scenes and backups
│   └── Tests/                         # Component/system UI test scenes
├── Scripts/
│   ├── Modules/
│   │   ├── A_Player/                  # Campus locomotion and practice objects
│   │   ├── B_DoorsWindowsElevator/     # Hinged interactions and refuge call
│   │   ├── C_TowelWater/              # Taps, wetting, holding and face covering
│   │   ├── D_FireSmokeHealth/          # Staged smoke and exposure
│   │   ├── E_LessonTasksUI/            # Tutorial, tasks, map, HUD and debrief
│   │   └── F_RescueStudent/            # Dialogue, following and rescue stages
│   ├── Campus/                        # Scene tools and regression utilities
│   ├── Health/                        # Health controller, audio and HUD
│   ├── Interaction/                   # Shared interaction components
│   ├── Journey/Runtime/               # Journey states and session logging
│   ├── Player/                        # Controller tracking and teleportation
│   ├── Shared/Campus/                 # Shared input and hand interactions
│   └── SystemUI/                      # Menu, pause and outcome navigation
├── Prefabs/                           # Environment, UI and student prefabs
├── Art/ Audio/ Fonts/                 # Visual, audio and font assets
├── Settings/Rendering/                # URP settings
├── ThirdParty/                        # Imported asset packs
├── SteamVR/ SteamVR_Input/ XR/ XRI/    # SDK and input configuration
└── StreamingAssets/SteamVR/           # Action manifests and bindings
Documentation/                         # Design notes, changes and check records
```

## Documentation

The following documents are in Chinese. Earlier design notes describe their own revision and may differ from the integrated behavior above.

- [Manual tutorial update](Documentation/Manual_Tutorial_Update.md) — reader-controlled explanations, required practical actions and restart behavior.
- [Task notice and flow update](Documentation/Task_Notice_Flow_Update.md) — briefing, task timing, route triggers and task-card layout.
- [Health HUD update](Documentation/HealthHUD_Style_Update.md) — health display and protection icon.
- [UI integration](Documentation/HandDrawn_UI_Integration.md) and [compact warnings](Documentation/Compact_Warning_Update.md) — interface assets and warning placement.
- [Campus guide](Documentation/CampusGuide.md), [revision guide](Documentation/CampusRevisionGuide.md) and [guided opening](Documentation/GuidedOpening.md) — scene setup and earlier integration decisions.
- [Restroom interactions](Documentation/RestroomInteractions.md) — tap and towel mechanics.
- [Earlier rescue design](Documentation/RescueStudentSideQuest.md) — historical two-handle interaction; see the current flow and commits above for dialogue-based following.
- [Team workflow](Documentation/TeamModules/GUI_操作说明.md) — module packaging and collaboration.
- [Play Mode checks](Documentation/PlayModeChecks.txt), [revision checks](Documentation/RevisionChecks.txt), [lesson checks](Documentation/LessonChecks.txt) and [teleport checks](Documentation/TeleportChecks.txt) — historical checks with limited scopes.

## Verification and remaining work

Existing editor records cover tutorial reading/action gates, HUD states and sampled task-card/notice layouts. The tutorial check explicitly verifies return to teaching on restart. These are scoped historical results, not a complete pass for the final integrated build.

- **Headset acceptance:** final stereo readability, controller tracking, comfort, frame rate and complete task flows still need verification on the frozen build and target equipment.
- **Runtime/preview errors:** existing lesson and rescue preview logs contain a destroyed-Transform exception and a missing lesson-card background error. Their affected paths need reproduction and clean verification; they do not alone establish a player-build failure.
- **Retry policy:** the final tutorial reentry differs from the earlier goal of skipping repeated teaching. Resolve the intended policy and verify health, tasks, rescue and results reset together.
- **Comprehension:** English map room labels, protection-icon meaning, discovery of the assistance task and deliberate access to debrief details remain to be evaluated.
- **Release:** a complete exported Windows build and cold-launch check on the target lab PC/headset combination remain required. Do not treat Editor preview or a source merge as release acceptance.

The campus is saved in the `.unity` scene. Legacy scene builders can overwrite manual changes; use the documented `Tools → Campus` workflows with a backup when rebuilding scene content. Review the permissions for third-party assets before redistribution.

## Team members

| Name | Student ID | GitHub account |
| --- | --- | --- |
| Zhao Yining | 26040695g | zhangoyini |
| CHEUNG Kwan Kit (Henry) | 26007063g | cheungkk704 |
| Wu Zhengyang | 26071286g | wuxiaoyang |
| ZHU Zijie | 26051898g | zJaey-00 |
| JIAO Ruikang | 26103438g | RJ |
| JIANG Chenghao | 26057555g | Cheng-Jimmy |
