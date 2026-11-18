# Four Corners Salvage Yard

A desktop vehicular-combat slice in Unity 6.3 URP. Draw out the wrecker, take the asphalt loop or cut through the grass, mud, and creek, and bait it into the gate at the sandy east bank. The south-side gap fits the muscle car while the gate is intact. After a win, keep driving to inspect the tracks, breach, and wreckage.

## Open the project

1. Install the Unity editor version listed in [`ProjectVersion.txt`](ProjectSettings/ProjectVersion.txt) through Unity Hub.
2. Add the repository root as an existing project in Unity Hub and let the first import finish.
3. Open [`FourCornersSalvageYard.unity`](Assets/Scenes/FourCornersSalvageYard.unity) and press Play. Click the Game view to give it input focus.

A fresh checkout requires `Assets/`, `Packages/`, and `ProjectSettings/`. Version project assets with their `.meta` files and keep the package lockfile with the manifest. Unity generates machine-local caches and IDE files on import.

## Controls and tuning

| Input | Action |
| --- | --- |
| W / S | Accelerate / brake, then reverse |
| A / D | Steer |
| Space | Handbrake |
| Left mouse | Fire the forward gun |
| Right mouse | Drop one of three mines |
| R | Restart the round |

Vehicle handling lives in [`Vehicles/Tuning`](Assets/FourCorners/Vehicles/Tuning). Per-wheel grip, resistance, marks, particles, and sound live in [`Surfaces/Tuning`](Assets/FourCorners/Surfaces/Tuning). The scene's `Encounter`, `Yard Camera`, and `Tow Truck` objects hold the encounter, camera, and pursuit settings. Vehicle, surface, and arena authoring code is in [`Assets/FourCorners/Editor`](Assets/FourCorners/Editor).

The scene and its prefabs are saved in the project. **Four Corners > Rebuild Playable Scene** regenerates them from the authoring code. **Four Corners > Build Windows Player** creates the standalone player under `Builds/FourCorners/`.

## Verify a change

Run from the repository root in PowerShell 7 on Windows. Close this project in Unity before starting a batch check. The script picks the pinned editor from the standard Unity Hub location. For a custom install, set `UNITY_EDITOR` or pass `-EditorPath` to the matching `Unity.exe`.

```powershell
./tools/verify-unity.ps1                               # Import and compile
./tools/verify-unity.ps1 -Mode PlayMode -TestFilter 'FourCorners\.'
```

A filter that executes zero tests fails the check. The command prints a short result and writes the full editor log and test XML under `Logs/Verification/`. For input, physics, UI, or rendering changes, also check the scene in the editor. The batch checks run without a graphics device. [Unity Editor command-line reference](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/editor)

## Work across agent chats

Start one chat per independently verifiable change. Give it the desired player-facing behavior, the relevant scene or script if known, and a concrete way to verify success. Point to repo files and specific log lines from earlier work.

When resuming a larger change in a new chat, carry forward a short handoff. Keep enduring design decisions with the feature they affect and link to those decisions from the handoff.

```text
Goal and acceptance check:
Relevant files and current state:
Decisions or constraints that still apply:
Verification run and outcome:
Next step or blocker:
```

[`AGENTS.md`](AGENTS.md) is the default repo guidance. Keep it short as the game grows. Add scoped guidance when a subtree has durable rules of its own, and link to longer design notes from the nearest relevant documentation. [Codex repository guidance](https://learn.chatgpt.com/docs/agent-configuration/agents-md)
