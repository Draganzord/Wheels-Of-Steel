# Wheels of Steel

## Find the relevant context

- Start with `git status --short` and the files named in the task. Use `git diff --stat` to orient in an existing change. Use `rg --files` and `rg -n` in the relevant source directories. Read matching spans of scenes, prefabs, settings, and logs. Keep generated directories such as `Library/`, `Temp/`, `Logs/`, `obj/`, and `UserSettings/` out of broad searches.
- Use `ProjectSettings/ProjectVersion.txt` for the Unity version and `Packages/manifest.json` for dependencies. Find enabled scenes in `ProjectSettings/EditorBuildSettings.asset`, input bindings in `Assets/InputSystem_Actions.inputactions`, and URP assets in `Assets/Settings/`. Scripts in `Assets/TutorialInfo/` belong to the Unity template.
- Open the relevant section of `README.md` for setup, verification, or handoff. Follow definitions and references only as far as the current task requires.

## Make Unity changes

- Preserve `.meta` GUIDs. Move and rename assets in Unity, or move each file with its `.meta` and preserve folder metadata. Include Unity-generated `.meta` files with new assets and folders under `Assets/`.
- Prefer Unity's editor or serialization APIs for scene and prefab changes. For targeted text edits, preserve GUIDs and `fileID` links and review the diff for unexpected reserialization.
- Keep package, project settings, and gameplay design changes tied to the task. Check existing Input System actions and render settings before adding parallel configuration.

## Verify and hand off

- Run the smallest check that exercises the change. On Windows, `./tools/verify-unity.ps1` checks import and compilation. See `README.md` for filtered tests and Play Mode checks. Check input, physics, and visuals in Play Mode when the change depends on them.
- Report changed paths, what was verified, and what still needs checking. For a task that spans chats, pass along the acceptance check, relevant files, decisions, verification status, and next step. Record durable decisions near the affected feature and link to them from the handoff.
