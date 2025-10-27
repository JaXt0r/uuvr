# UUVR — Installation Guide (End Users)

This mod ships as a single folder you can drop into your game’s BepInEx plugins directory. The loader will automatically pick the correct implementation for your game (Mono or IL2CPP, legacy or modern Unity), so you don’t have to choose anything.

## Requirements
- BepInEx installed for your game
  - Mono games: BepInEx 5
  - IL2CPP games: BepInEx 6 (Bleeding Edge)

## Install steps
1. Close the game.
2. Open your game folder and locate the BepInEx folder.
3. Copy the entire `Uuvr` folder to: `BepInEx\plugins\`
   - Final path should be: `BepInEx\plugins\Uuvr\`
4. Start the game.

That’s it. The loader in `Uuvr` will detect your game’s backend (Mono/IL2CPP) and Unity version and load the matching implementation automatically.

## What’s inside the Uuvr folder
```
BepInEx\plugins\Uuvr\
├─ Uuvr.Loader.Mono.Legacy.dll          (used in Mono/BepInEx 5 games, Unity 2019 or older)
├─ Uuvr.Loader.Mono.Modern.dll          (used in Mono/BepInEx 5 games, Unity 2020 or newer)
├─ Uuvr.Loader.Il2cpp.Legacy.dll        (used in IL2CPP/BepInEx 6 games, Unity 2019 or older)
├─ Uuvr.Loader.Il2cpp.Modern.dll        (used in IL2CPP/BepInEx 6 games, Unity 2020 or newer)
└─ implementation\
   ├─ Uuvr.Mono.Legacy.dll       (Unity 2019 or older, Mono)
   ├─ Uuvr.Mono.Modern.dll       (Unity 2020 or newer, Mono)
   ├─ Uuvr.IL2CPP.Legacy.dll     (Unity 2019 or older, IL2CPP)
   ├─ Uuvr.IL2CPP.Modern.dll     (Unity 2020 or newer, IL2CPP)
   └─ Assets\ ...                (assets used by the mod)
```

Keep the folder structure exactly as shown. You can ship both loader DLLs together—BepInEx will only load the one that matches your game.

## Configuration
- A config file named something like `UUVR.cfg` will be created the first time the mod runs (BepInEx config system).
- You can adjust settings (camera, UI, etc.) in that config file while the game is closed.

## Troubleshooting
- If you see an error saying an implementation DLL was not found, make sure the `implementation` subfolder is present and contains the 4 DLLs.
- Ensure you’re using the correct BepInEx version for your game (BepInEx 5 for Mono, BepInEx 6 for IL2CPP).
- Some games might need to be launched once after installing BepInEx to generate the folder structure before adding mods.

## Uninstall
Delete the `BepInEx\plugins\Uuvr` folder. This removes the mod completely.
