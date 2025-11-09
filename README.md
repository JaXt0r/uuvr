# Universal Unity VR

> [!NOTE]
> Use [Rai Pal](https://pal.raicuparta.com) to install this mod.

> [!NOTE]
> [![Raicuparta's VR mods](https://raicuparta.com/img/badge.svg)](https://raicuparta.com)


## Configuration

UUVR uses a BepInEx `ConfigFile` and generates a config on first launch under the game's `BepInEx\config` folder.
The table below lists all entries, their sections/keys (as they appear in the config), default values, and notes.

Note about build variants:
- MODERN builds: Enable some extra options and slightly different defaults.
- LEGACY builds: A few entries are unavailable and some defaults differ. These cases are noted in the table.

| Section          | Key                                   | Type        | Default                                 | Values/Range                                           | Description                                                                                              | Build notes                                |
|------------------|----------------------------------------|-------------|-----------------------------------------|--------------------------------------------------------|----------------------------------------------------------------------------------------------------------|--------------------------------------------|
| General          | Preferred VR APi                       | VrApi       | OpenXr                                  | OpenVr, OpenXr                                         | VR API to use. Falls back automatically if unavailable.                                                  | Only in MODERN builds                      |
| General          | Force physics rate to match headset refresh rate | bool | false                                   | true/false                                             | May reduce jitter in physics-heavy games; can break some games.                                          |                                            |
| Camera           | Camera Tracking Mode                   | CameraTrackingMode | MODERN: RelativeTransform; LEGACY: RelativeMatrix | Absolute, RelativeMatrix, RelativeTransform (MODERN), Child | How camera tracking is done. Relative is usually preferred. May require restarting the level.           | RelativeTransform only in MODERN           |
| Relative Camera  | Use SetStereoView for Relative Camera  | bool        | false                                   | true/false                                             | Try on/off to see which works better. May require restarting the level.                                   |                                            |
| Camera           | Align To Horizon                       | bool        | false                                   | true/false                                             | Locks camera pitch/roll; allows yaw only.                                                                 |                                            |
| Camera           | Camera Position Offset X               | float       | 0.0                                     | any                                                    | Adjusts tracked VR camera position (X).                                                                  |                                            |
| Camera           | Camera Position Offset Y               | float       | 0.0                                     | any                                                    | Adjusts tracked VR camera position (Y).                                                                  |                                            |
| Camera           | Camera Position Offset Z               | float       | 0.0                                     | any                                                    | Adjusts tracked VR camera position (Z).                                                                  |                                            |
| Camera           | Override Depth                         | bool        | false                                   | true/false                                             | Some games render nothing unless camera depth is overridden.                                             |                                            |
| Camera           | Depth Value                            | int         | 1                                       | -100 .. 100                                            | Requires 'Override Depth'. Use the lowest value that fixes visibility.                                    |                                            |
| UI               | UI Patch Mode                          | UiPatchMode | Mirror                                  | None, Mirror, CanvasRedirect                            | Method used to adapt UI for VR.                                                                           |                                            |
| UI               | VR UI Layer Override                   | int         | -1                                      | -1 .. 31                                               | Layer for VR UI. Default -1 lets UUVR pick an unused layer automatically.                                |                                            |
| UI               | VR UI Position                         | Vector3     | (0, 0, 1)                               | any                                                    | Position of the VR UI plane relative to the camera.                                                      |                                            |
| UI               | VR UI Scale                            | float       | 1.0                                     | any                                                    | Scale of the VR UI projection.                                                                            |                                            |
| UI               | VR UI Shader                           | string      | ""                                      | name of a Unity shader                                  | Shader used for the VR UI plane (passed to `Shader.Find`). Leave empty to auto-pick.                      |                                            |
| UI               | VR UI Render Queue                     | int         | 5000                                    | 0 .. 5000                                              | Render queue for the VR UI material. 5000 matches Unity's default canvas material.                        |                                            |
| UI               | Screen-space UI Elements to Patch      | ScreenSpaceCanvasType | NotToTexture                    | None, NotToTexture, All                               | Screen-space UI is visible by default; patching can improve VR visibility in some games.                  |                                            |
| UI               | Preferred UI Plane Render Mode         | UiRenderMode| MODERN: InWorld; LEGACY: OverlayCamera  | OverlayCamera, InWorld                                  | How to render the VR UI plane. Overlay is usually better but may not work in every game.                  | Default differs by build (see Default)     |
| Fixes            | Objects to Deactivate by Component     | string      | ""                                      | list of FQCNs separated by '/'                          | Any object containing a listed component is deactivated. Example: `Canvas, UnityEngine/HUD, Assembly-CSharp` |                                            |
| Fixes            | Components to Disable.                 | string      | ""                                      | list of FQCNs separated by '/'                          | Components to disable. Example: `Canvas, UnityEngine/HUD, Assembly-CSharp`                                 |                                            |
| Fixes            | Component Search Interval              | float       | 1.0                                     | 0.5 .. 30                                              | Seconds between searches for components to disable.                                                       |                                            |

## License

    Universal Unity VR (UUVR)
    Copyright (C) 2025  Raicuparta

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
