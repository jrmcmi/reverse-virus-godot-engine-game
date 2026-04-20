# Anti Virus Virus Game — A Godot Attack & Escort Game

> **Kill the zombies. Cure the infected. Guide the humans to safety.**

---

## 📖 Table of Contents

- [About the Game](#about-the-game)
- [Objective](#objective)
- [How the Game Works](#how-the-game-works)
- [Game Mechanics](#game-mechanics)
- [Controls](#controls)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Importing the Source Code in Godot](#importing-the-source-code-in-godot)
  - [Building on Arch Linux](#building-on-arch-linux)
  - [Building on Windows](#building-on-windows)
- [Exporting / Running a Build](#exporting--running-a-build)
- [Team](#team)

---

## About the Game

**Reverse Virus** is a 2D top-down action/escort game built with the [Godot 4](https://godotengine.org/) engine using **C# (.NET)**. The player fights waves of zombies in a grassy open arena, but instead of simply eliminating enemies, the twist is that defeated zombies *transform back into humans* — and those humans need to be escorted to safety before they can be lost again.

The game features a hand-crafted environment complete with apple trees, big trees, rocks, bushes, and a central school building that serves as the safe zone.

---

## Objective

**Defeat zombies to convert them back into humans, then escort those humans to the School (safe zone) before they can be re-infected or lost.**

The more humans you successfully guide to the school, the better your score. Survive as many waves as possible and keep the human count alive!

---

## How the Game Works

1. **Zombies spawn** at multiple spawn points scattered across the map.
2. **The player attacks** zombies to reduce their health.
3. **When a zombie is defeated**, it transforms into a human survivor.
4. **Human survivors must be guided** toward the School in the upper-center of the map — this is the safe zone.
5. **The school area** acts as the win condition zone; once a human enters it, they are safely saved.
6. New waves of zombies continue to spawn, increasing in difficulty.
7. The game ends when the player is overwhelmed or the 5 minute mark is finished.

---

## Game Mechanics

### Player
- Moves freely around the 2D grasslands map.
- Can attack nearby zombies to damage and eventually defeat them.
- Acts as a guide/shepherd for converted human survivors.

### Zombies
- Spawn from 8 fixed spawn points distributed around the map.
- Move toward and attack the player or survivors.
- Upon defeat, they convert into a human survivor entity.

### Human Survivors
- Spawned in place of a defeated zombie.
- Wander or follow the player toward the safe zone.
- Must reach the **School** building to be counted as saved.

### Safe Zone (School)
- Located near the top-center of the map.
- Any human survivor who enters the school area is rescued and added to the saved count.
- Represented visually on the map by the school building.

### Environment
- The play area is a bounded grasslands map (1920×1080) surrounded by invisible walls.
- Decorative and obstacle objects include: apple trees, big trees, regular trees, rocks, bushes, and stumps.
- A HUD bar runs across the top of the screen displaying game information.

---

## Controls

| Action | Key / Input |
|---|---|
| Move | `W A S D` or Arrow Keys |
| Attack | `Left Click` or assigned attack key |
| Dash | `Space` |
| Restart | `R` |

> ⚠️ Controls may vary — check the in-game settings or `scripts/` for input bindings.

---

## Project Structure

```
reverse-virus-godot-engine-game/
├── .godot/                  # Godot engine cache (auto-generated, do not edit)
├── PNG/                     # Sprite and texture assets (e.g. grass_lands.png)
├── objects/                 # Packed scenes for environment objects
│   ├── appleTree.tscn
│   ├── bigTree.tscn
│   ├── regularTree.tscn
│   ├── rocks.tscn
│   ├── bush.tscn
│   ├── school.tscn
│   └── stump.tscn
├── resources/               # Shared Godot resources
├── scripts/                 # C# game logic scripts
│   └── GameManager.cs       # Core game loop and state management
├── sounds/                  # Audio assets
├── Main.tscn                # Main gameplay scene
├── MainMenu.tscn            # Main menu scene
├── project.godot            # Godot project configuration
├── reverse-virus-v2.csproj  # C# project file
├── reverse-virus-v2.sln     # Visual Studio solution file
├── export_presets.cfg       # Export configuration
└── icon.svg                 # Project icon
```

---

## Getting Started

### Prerequisites

Before opening or building the project, make sure you have the following installed:

- **Godot Engine 4.x — Mono/.NET version** (required for C# support)
  - Download: https://godotengine.org/download
  - ⚠️ You **must** use the **.NET** build of Godot 4, not the standard build. C# will not work otherwise.
- **.NET SDK 6.0 or later**
  - Download: https://dotnet.microsoft.com/download
- **Git** (to clone the repository)

---

### Importing the Source Code in Godot

1. **Clone the repository:**
   ```bash
   git clone https://github.com/jrmcmi/reverse-virus-godot-engine-game.git
   cd reverse-virus-godot-engine-game
   ```

2. **Open Godot 4 (.NET version).**

3. In the **Project Manager**, click **Import**.

4. Navigate to the cloned folder and select the `project.godot` file.

5. Click **Import & Edit**.

6. Godot will automatically import all assets and build the C# solution on first open. Wait for the import process to complete in the bottom panel.

7. Once imported, press **F5** (or click the ▶ Play button) to run the game.

> 💡 If you see C# build errors, make sure the .NET SDK is installed and Godot can find it. Check **Editor → Editor Settings → Mono → Builds → Build Tool**.

---

### Building on Arch Linux

#### 1. Install Dependencies

```bash
# Install Godot 4 (from AUR, using yay or paru)
yay -S godot-mono-bin

# Or install manually via the official download
# https://godotengine.org/download/linux/

# Install the .NET SDK
sudo pacman -S dotnet-sdk

# Verify .NET is installed
dotnet --version
```

#### 2. Clone and Open the Project

```bash
git clone https://github.com/jrmcmi/reverse-virus-godot-engine-game.git
cd reverse-virus-godot-engine-game

# Launch Godot from terminal (or open it from your app menu)
godot-mono project.godot
```

#### 3. Build the C# Solution

Inside Godot, go to **Build → Build Solution** (or press `Alt+B`). Check the bottom panel for any errors.

#### 4. Run the Game

Press **F5** in the Godot editor to run from the editor, or export a Linux binary (see [Exporting](#exporting--running-a-build)).

---

### Building on Windows

#### 1. Install Dependencies

- **Godot 4 .NET** — Download the Windows `.NET` version from https://godotengine.org/download/windows/
- **.NET SDK** — Download from https://dotnet.microsoft.com/download (version 6.0+)
- **Git for Windows** — https://git-scm.com/download/win

#### 2. Clone the Repository

Open **Git Bash** or **PowerShell**:

```powershell
git clone https://github.com/jrmcmi/reverse-virus-godot-engine-game.git
cd reverse-virus-godot-engine-game
```

#### 3. Open in Godot

- Launch `Godot_v4.x_mono_win64.exe`
- In the Project Manager, click **Import**
- Browse to the cloned folder and select `project.godot`
- Click **Import & Edit**

#### 4. Build the C# Solution

Inside Godot, go to **Build → Build Solution**. Watch the Output panel at the bottom for build success or errors.

#### 5. Run the Game

Press **F5** or click the **▶ Play** button in the Godot toolbar.

> 💡 **Visual Studio / VS Code users:** You can also open `reverse-virus-v2.sln` directly in Visual Studio or Rider for a richer C# editing experience, then switch back to Godot to run.

---

## Exporting / Running a Build

To export a standalone executable:

1. In Godot, go to **Project → Export**.
2. Select your target platform (Windows Desktop, Linux/X11, etc.).
3. Make sure export templates are installed. If not, click **Manage Export Templates** and download them.
4. Click **Export Project** and choose your output folder.

> The repo already includes `export_presets.cfg` with pre-configured export settings.

---

## Team

| Role | Name | GitHub |
|---|---|---|
| **Project Leader** | Cimafranca, Jearim Raglen | [@jrmcmi](https://github.com/jrmcmi) |
| Member | Avila, Fresius Jane J. | — |
| Member | Canlas, Hailie Jade U. | — |
| Member | Baldemoro, Jeremiah    | — |

---

## License

This project is for educational purposes. All assets and code belong to their respective creators.

---

*Built with ❤️ using [Godot Engine 4](https://godotengine.org/) and C#*
