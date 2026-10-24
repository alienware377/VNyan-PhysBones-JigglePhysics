# VNyan PhysBones & Jiggle Physics

[![Latest Release](https://img.shields.io/github/v/release/alienware377/VNyan-PhysBones-JigglePhysics?label=download&color=brightgreen)](https://github.com/alienware377/VNyan-PhysBones-JigglePhysics/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Two free VNyan plugins that give your VRM VTuber avatar VRChat-style hair, skirt and tail physics, plus soft breast and belly jiggle. No VRChat SDK, no Unity skills needed.**

- **PhysBones:** hair, skirt and tail physics that behave like VRChat PhysBones, set up from a bone tree and live sliders.
- **Jiggle Physics:** soft-body jiggle with real squash and stretch.

Both have live tuning windows, and both save to your AppData folder, so you never need admin rights to tune or save.

> **New home:** these two plugins used to ship together with Pose Studio in
> [VNyan-PhysBones-BreastPhysics-PoseStudio](https://github.com/alienware377/VNyan-PhysBones-BreastPhysics-PoseStudio).
> Pose Studio stays there; PhysBones and Jiggle Physics now live here. Their earlier history and
> releases (v1.0.0 to v2.4.0) are in that repo.

> **New in v2.5.0:** **Copy / Paste** settings between bones or chains, and **Groups**: tick a
> set of bones and give them all the same settings at once, or edit them together live.
> See [Copy, paste and groups](#copy-paste-and-groups).

| Plugin | Button name | What it does |
|--------|-------------|--------------|
| **PhysBones** | "VRChat PhysBones" | VRChat-style dynamic bone physics for hair, skirts and tails. Verlet solver, chains picked from a bone tree, live sliders, colliders. |
| **Jiggle Physics** | "Jiggle Physics" | Soft-tissue jiggle (chest, pecs, belly, bum) with squash and stretch. Leaf-bone solver with volume-preserving scale. |

---

## Install: one click

> The prebuilt files in `dist/` load on VNyan's Unity 2022.3 runtime. You don't need Unity or a compiler.

1. Download the latest release zip (or clone this repository) and unzip it anywhere.
2. Fully close VNyan (check the system tray), then double-click **`install.bat`**.
3. The installer **looks for VNyan by itself** (a VNyan that's open, the log VNyan keeps from
   the last time it ran, your Steam libraries, and the itch app) and asks you to confirm what it
   found. If it can't find VNyan, or you say no, it asks you to **open your VNyan folder**: the
   one with `VNyan.exe` inside. VNyan has no installer of its own, so it can live anywhere, and the
   installer never assumes a location for you.
4. Admin rights are requested **only if your VNyan folder is write-protected**. A VNyan you
   unzipped yourself never sees a UAC prompt.
5. Updating from v1.x? The installer retires the old `Jayo*` plugin files automatically so both
   versions can't load at once. Your settings carry over.
6. Launch VNyan and open the **Plugins** window. You should see **VRChat PhysBones** and
   **Jiggle Physics**.

Pose Studio is installed separately from
[its own repo](https://github.com/alienware377/VNyan-PhysBones-BreastPhysics-PoseStudio/releases/latest).
Installing one never removes the other.

> Installing from a script? `powershell -ExecutionPolicy Bypass -File install.ps1 -Target "D:\Apps\VNyan" -Yes`
> installs without prompting. It still asks Windows for admin rights if that folder is
> write-protected, and stops if VNyan is open. `-ListFound` just prints the VNyan folders the
> search finds.

### If the Plugins panel is empty after installing

Two causes account for almost every case, in order of likelihood:

1. **"Allow Third Party Plugins" is turned off.**
   In VNyan go to **Settings > Misc > Allow Third Party Plugins** and make sure it's **enabled**.
   When it's off VNyan loads no plugins at all and logs nothing, so the panel is silently empty.

2. **You have more than one VNyan and launched the other one.**
   The plugins went into one folder but a different VNyan.exe is running. VNyan's log
   (`%APPDATA%\..\LocalLow\Suvidriel\VNyan\Player.log`) names the copy that launched in its first
   lines. Install into that copy and restart.

If neither applies, run **`TROUBLESHOOT.bat`**. It writes a `VNyan_Diagnostics_Report.txt` with
file placement, Windows download-block status, Unity runtime versus bundle version, and the VNyan
path in use. If it comes back clean and the panel is still empty, it's almost certainly cause 1.

---

## Copy, paste and groups

Both tuning windows have a **Copy / paste & groups** section at the bottom. It works the same in
both plugins; PhysBones talks about chains, Jiggle Physics about bones.

- **Copy** remembers every setting the window shows for the selected bone or chain.
  **Paste** puts them onto the one you have selected now. **Undo paste** takes back the last
  paste (or Apply).
- Only the tuning is copied: which bone it is, its direction and its collider list stay as they were.

**Groups** are hand-picked sets of bones or chains that should share settings:

- **New** makes a group (with the shown bone already in it) and opens a list on the right.
  Tick the members there. The **Find** box narrows a long list (try `hair`); **Tick shown** and
  **Untick shown** work on whatever the list is showing.
- `<` and `>` step through your groups. Rename one by typing in the name box and pressing Enter.
- **+/- this bone** (or chain) adds or removes the one you're looking at.
- **Apply to group** gives every member the settings of the shown bone. **Paste to group**
  gives them what you copied.
- **Edit together** (on for new groups): moving any slider on a member moves it on every member,
  live. Untick it to tune members one at a time again.
- **Delete** removes the group only; the bones keep their settings.

Groups are saved in your config, so press **Save** to keep them. Removing a bone or chain also
takes it out of its groups.

---

## PhysBones

VRChat-PhysBone-style dynamic bone physics for hair, skirts and tails. The simulation is written
from scratch: no VRChat SDK, and no runtime or licensing dependency on VRChat. Parameters mirror
VRChat's, so settings you made for a VRChat avatar carry over.

### Configuring (`physbones.json`)

The plugin looks in these places in order and loads the first it finds:

1. `%USERPROFILE%\AppData\LocalLow\Suvidriel\VNyan\physbones.json` (your saved copy)
2. `<your VNyan folder>\physbones.json`
3. `<your VNyan folder>\Items\Assemblies\PhysBones\physbones.json`

**Save** in the tuning window always writes to place 1 (no admin needed). Once saved there, that
copy wins on every future load. The file is easy to share: send it to anyone and they drop it in.

A minimal config looks like this:

```json
{
  "chains": [
    {
      "name": "hair",
      "rootBone": "HairRoot",
      "pull": 0.2,
      "spring": 0.4,
      "stiffness": 0.1,
      "gravity": 0.05
    }
  ]
}
```

Copy `config/physbones.example.json` for a fuller starting point.

### Live tuning window

Click **VRChat PhysBones** in the Plugins panel to open the draggable tuning window:

- **Chain** dropdown: which chain the sliders edit.
- **Physics enabled:** global on/off.
- **Angle limit:** switches the selected chain between the `angle` and `none` limit types.
- **Override native physics:** turns off the model's own SpringBone, DynamicBone and MagicaCloth
  solvers so only PhysBones drives the bones. Useful when two solvers fight over a bone and jitter.
  - **only my configured chains:** the narrower version; only solvers whose bones overlap your
    chains are turned off, everything else keeps running.
- **Sliders:** `Pull`, `Spring`, `Stiffness`, `Gravity` (-1 to 1), `Grav Falloff`, `Immobile`,
  `Max Angle`, `Radius`, `Max Stretch`. They apply **instantly**, no reload needed.
- **Bones** opens the bone browser (below). **Remove** deletes the selected chain.
- **Import / Show / Clear Colliders**, **Reload**, **Save**, **Close**.
- **Copy / paste & groups:** see [above](#copy-paste-and-groups).

### Bone browser

Click **Bones** to open a collapsible tree of the loaded model's real bone hierarchy. Click any
bone name to add a new chain rooted there. It's added straight away and selected in the Chain
dropdown. New chains stay in memory until you **Save**.

### Parameters

| Field | Range | Meaning |
|-------|-------|---------|
| `pull` | 0..1 | Force returning bones to their rest pose. Higher is snappier. |
| `spring` | 0..1 | Bounciness. Higher means more bounce. |
| `stiffness` | 0..1 | Resistance to bending away from the rest direction. |
| `gravity` | -1..1 | World gravity (negative pulls up). |
| `gravityFalloff` | 0..1 | Less gravity near the rest pose, so bones don't sag out of their authored shape. |
| `immobile` | 0..1 | How much bones ignore avatar movement (1 = don't react to walking). |
| `limitType` | `none` / `angle` | `angle` keeps each bone within `maxAngle` of its rest direction. |
| `maxAngle` | degrees | Cone limit when `limitType` is `angle`. |
| `radius` | metres | Bone collision radius. |
| `maxStretch` | 0..1 | Allowed stretch beyond rest length (0 = rigid). |
| `colliders` | names | Which colliders this chain collides with. |

Groups are stored next to the chains as `groups`: `[{ "name", "members": [chain names], "linked" }]`.

### Colliders

`type` is `sphere`, `capsule` or `plane`.

- **sphere:** `offset` (local centre) and `radius`.
- **capsule:** `offset`, then either `axis` + `height` or `offsetEnd`, and `radius`.
- **plane:** `offset` (a point on the plane) and `axis` (the normal). Bones stay on the side the normal points to.

Set `bone` to attach a collider to a bone (it follows the bone), or `null` for a fixed world-space
collider such as a floor. A chain's `colliders` list names the colliders it uses.

### Bone names

`rootBone`, `ignore` and a collider's `bone` are looked up as a Unity humanoid name first (`Head`,
`Chest`, `Hips`, `Spine`, ...), then as a transform name, ignoring capitals. For hair, tails and
skirts use the real bone names from your model.

### How a chain is built

A chain simulates the whole subtree under `rootBone`. A bone with one child swings to aim at it.
A bone with several children is treated as a fixed split point and each branch is solved on its
own (like VRChat's `multiChildType = Ignore`). Put bones you don't want simulated in `ignore`.

### Notes and limitations

- A close **approximation** of VRChat PhysBones. The feel may differ a little, so tune with the live sliders.
- Done: pull, spring, stiffness, gravity, gravity falloff, immobile, angle limit, max stretch,
  sphere/capsule/plane colliders, multi-child splits.
- Not done yet: grab/pose, squish, hinge/polar limits, per-bone radius curves, collider `inside` mode.
- Solver stability (since v2.3.0): collisions don't add energy, collider and length constraints
  settle together, velocity is capped, and teleports (avatar reloads, seat changes) move the whole
  simulation along instead of flinging it.

---

## Jiggle Physics

Soft-tissue jiggle for chest, pec, belly and bum bones, with **squash and stretch**. These bones
are usually leaf bones (no child), so a chain solver has nothing to aim at. Jiggle Physics makes a
virtual tip along the bone's forward axis, simulates it, turns the bone to follow, then drives its
scale:

- **Stretch:** when the tip sags, the bone gets longer and thinner (keeping its volume).
- **Squish:** when a collider presses in, the bone flattens along the contact and bulges sideways.

### Configuring (`jigglephysics.json`)

Same load and save places as PhysBones (the file is `jigglephysics.json`). The starter config
defines two bones, `Bust_L` and `Bust_R`. **Rename `bone` to match your model** (common names:
`Bust_L/R`, `Breast_L/R`), or add bones with the **Bones** browser instead.

| Field | Meaning |
|-------|---------|
| `bone` | Bone or transform name |
| `axis` | The local direction the bone points (swing tip direction); default `[0,0,1]` |
| `length` | Virtual swing-arm length in metres (default `0.08`) |
| `weight` `bounce` `stiffness` `damping` `pull` | 0..1 spring feel |
| `limitType` `maxAngle` | Bra limiter (`angle` plus degrees) |
| `stretch` `squish` | 0..1 deformation strength |
| `radius` | Collision radius (m) |
| `selfCollide` `selfRadius` | Left/right self-collision sphere |
| `colliders` | World collider names (same shapes as PhysBones) |

Global `settings`: `enabled`, `substeps`, `gravityDir`, `minScale`/`maxScale` (deform limits,
default `0.65`/`1.6`), `scaleSpeed` (how fast scale eases, default `14`). Groups are stored as
`groups`, the same way as in PhysBones.

### Tuning window

Click **Jiggle Physics**: `Weight`, `Bounce`, `Stiffness`, `Damping`, `Pull`, `Bra Angle`,
`Stretch`, `Squish` and `Radius` sliders, which all apply instantly, plus the bra limiter and
self-collision ticks and the [copy, paste and groups](#copy-paste-and-groups) section.
**Save** writes to AppData. Turning the plugin off fully restores the original rotation **and scale**.

### Notes

- Deformation writes the bone's scale every frame. If something else animates the same bones'
  scale, this plugin wins while it's on.
- If the swing looks wrong for a bone, change its `axis`.
- Works alongside PhysBones: point each plugin at different bones (PhysBones for hair and skirt,
  Jiggle Physics for chest).

---

## Rebuild from source

The prebuilt `dist/` files should load on any VNyan with Unity 2022.3. If they don't (check
VNyan's log for a bundle version error), you can rebuild against your own VNyan and Unity.

### DLL only (no Unity needed)

Run these from the repo root (the `.rsp` files use paths relative to it):

```
csc.exe -noconfig @build/build_physbones.rsp
csc.exe -noconfig @build/build_jigglephysics.rsp
```

Each `.rsp` takes its references from `C:\Program Files\VNyan\VNyan_Data\Managed`. If your VNyan
lives somewhere else, change those paths in the `.rsp` files. It uses the .NET Framework
`csc.exe` that comes with Windows (C# 5). Each DLL **must** keep its name (`PhysBones.dll`,
`JigglePhysics.dll`): the bundles find their scripts by assembly name.

### `.vnobj` bundles (needs Unity 2022.3.x)

```
Unity.exe -batchmode -quit -nographics -projectPath "<repo>\_unitybuild" -executeMethod PhysBoneBuild.Build
Unity.exe -batchmode -quit -nographics -projectPath "<repo>\_unitybuild" -executeMethod JiggleBuild.Build
```

> **Important:** VNyan loads the autostart prefab by the addressable name `vnyanitem`. The build
> scripts set it for you. If you rebuild by hand and the plugin button never shows up, check that
> the bundle's addressable name is `vnyanitem` (not the prefab's path).

After rebuilding, copy the new DLL and `.vnobj` into `dist/` before running the installer.

---

## Credits

PhysBones and Jiggle Physics were originally named `JayoPhysBones` and `JayoJigglePhysics`. They
were renamed in v2.0.0 out of respect for [Jayo](https://github.com/jayo-exe)'s brand; the
originals that inspired this project are his.

MIT licensed, see [LICENSE](LICENSE).
