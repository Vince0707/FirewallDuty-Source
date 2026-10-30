# Firewall Duty: 3D Cybersecurity Simulation (source code and documentation)

A first-person mobile game (Android) where the player investigates cybersecurity threats in an office and a bank. Each environment has 10 story missions. The threats covered are phishing, malware, spam, fake pop-ups, social engineering and unauthorized access.

This repository contains the **first-party source code and the documentation** of the project, for review. The complete Unity project (scenes, 3D models, textures) is not included, because it uses third-party asset-store content that cannot be redistributed.

## Documentation
- [Software features](docs/SOFTWARE_FEATURES.md)
- [Technical documentation](docs/TECHNICAL_DOCUMENTATION.md): architecture, backend, mission system, build and configuration
- [Game flowchart](docs/flowchart.html): download the file and open it in a browser

## What is in this repository

| Path | Content |
|------|---------|
| `Assets/Scripts/Menu/` | Menu app, login, Firebase client, player session, trivia, leaderboard, achievements, settings, generated sounds |
| `Assets/Scripts/Mission/` | Mission session and timer, mission scenarios (data), investigation screens, Cyber Tablet, floor-arrow guide, arrival sequence |
| `Assets/Scripts/` | Player controller, floating joystick, interaction system, doors, NPC behaviours |
| `Assets/Scripts/Editor/` | Editor tools used to set up the scenes (`Tools > Firewall Duty`) |
| `Assets/Plugins/Android/` | `FirewallBridge.java`: Google sign-in and photo picker on a phone |
| `Assets/Resources/MenuUI/` | Android layout XML for the Cyber Tablet, trivia questions, config templates |
| `Packages/manifest.json`, `ProjectSettings/` | Unity packages and editor version; `ProjectSettings.asset` (Android package id, min SDK, ARM64, input handler), `EditorBuildSettings.asset` (scenes in the build), `QualitySettings.asset`. The Unity cloud project and organization ids are removed. |

## Technical summary
- Unity 6 (`6000.4.6f1`), Universal Render Pipeline 17, Input System, AI Navigation
- Android, package id `com.example.firewall_3d`, min SDK 25, ARM64
- Backend: Firebase Authentication and Firestore over the REST interface (no Firebase SDK)
- About 37,000 lines of C#

## Configuration
The real keys are not published. Templates are in `Assets/Resources/MenuUI/` (`firebase_config.example.json`, `google_device_config.example.json`, `facebook_config.example.json`).

## Third-party content not included
The full project also uses these asset-store packages, which are left out: `ithappy` (Casino Free), `PolygonalMind` (Low Poly Office), `Mnostva Art` (FREE Interiors 2), `PandazoleHome`, `nappin`, `TV LED 30`, TextMesh Pro essentials and Unity's tutorial assets.

## Reading guide
Start with the [technical documentation](docs/TECHNICAL_DOCUMENTATION.md), then:
- `Assets/Scripts/Mission/MissionSession.cs`: timer, objectives and scoring
- `Assets/Scripts/Mission/MissionScenarios.cs`: the missions as data
- `Assets/Scripts/Menu/FirebaseRest.cs` and `PlayerSession.cs`: backend and player data
- `Assets/Scripts/PlayerInteraction.cs`: how the player interacts with the world
