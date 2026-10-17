# VR Fire Evacuation Training

COMP5424 Extended Reality — Phase 2 Prototype · Team 11 · The Hong Kong Polytechnic University

A VR fire-evacuation drill prototype in a four-storey teaching building. The player recognises a fire emergency,
follows evacuation signs, recovers from blocked routes and unsafe shortcuts, and debriefs after reaching a safe
endpoint (2F refuge or 1F assembly point). Built with Unity 2022.3.62f3, targeting Quest 3 via SteamVR/OpenXR on
Windows PC.

## Team members

| Name | Student ID | GitHub account |
| --- | --- | --- |
| Zhao Yining | 26040695 | zhangoyini |
| CHEUNG Kwan Kit (Henry) | [student ID] | cheungkk704 |
| Wu Zhengyang | [student ID] | wuxiaoyang |
| ZHU Zijie | [student ID] | zJaey-00 |
| JIAO Ruikang | [student ID] | RJ |
| JIANG Chenghao | [student ID] | Cheng-Jimmy |

## Repository structure

- `Assets/Scenes/Campus/CampusTraining.unity` — main consolidated gameplay scene
- `Assets/Scenes/MainMenu.unity` — bilingual main menu
- `Assets/Scripts/Modules/` — module scripts A–F (scene, locomotion, environment, hazard, lesson, UI)
- `Documentation/Post-AI Scene Reconstruction Regression Test/` — six acceptance reports (01–06)
- Phase 2 deliverables (root):
  - `COMP5424-Group-Project-Phase2-ActivityRecord-Team11.pdf`
  - `COMP5424-Phase2-Technical-Note-Team11.pdf`
  - `COMP5424-Phase2-Individual-Essay-ZhaoYining.docx`

## How to run (development)

1. Open the project with Unity 2022.3.62f3.
2. Open `Assets/Scenes/Campus/CampusTraining.unity` (or play from `MainMenu`).
3. Requires a Windows PC with SteamVR and a Meta Quest 3 (Link / Air Link) for headset play; keyboard/mouse
   fallback is not provided — the build is a PC-VR executable.

## How to build (PQ604A/B/C lab PCs)

1. `File > Build Settings`: platform Windows (x86_64), scenes `MainMenu` and `CampusTraining`.
2. Ensure `SteamVR` (OpenVR) and `OpenXR` loaders are enabled in `Project Settings > XR Plug-in Management`.
3. Build to a folder, zip it, and upload the `.zip` (see the technical note for details).
