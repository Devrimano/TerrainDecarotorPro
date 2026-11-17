# Terrain Decorator Pro

A modular Unity Editor extension for procedural mountain generation, 6-layer PBR splatmapping, rock cluster instancing, and vegetation scattering in Unity URP.

## Features

- **Procedural Mountains**: Ridged multifractal border elevation with flat center transition masks.
- **6-Layer Biome Splatmapping**: Automatic slope and height texturing (grass, soil, verge pebbles, cliffs, crags).
- **Rock & Boulder Scatter**: Naturally embedded rock clusters with custom random seed support.
- **Vegetation Placement**: Separate density controls for tall forest pines, garden trees, bushes, and wildflowers.
- **Dynamic Exclusion**: Keeps asphalt roads and building foundations clear of foliage.
- **Performance Tuning**: One-click GPU instancing, billboard distance calibration, and draw distance optimization.

## Installation

### Via Unity Package Manager (Git URL)

1. In Unity, open `Window` -> `Package Manager`.
2. Click the `+` icon in the top-left corner.
3. Select `Add package from git URL...`.
4. Enter:
   ```text
   https://github.com/Devrimano/TerrainDecarotorPro.git
   ```

### Manual Installation

Clone or extract this repository into your project's `Packages/com.islamvalizada.terraindecorator` directory.

## Usage

1. Open `Tools` -> `Terrain Decorator` -> `Open Decorator Window`.
2. Select or auto-detect your active `Terrain`.
3. Configure your mountain height, rock counts, and vegetation densities.
4. Click `🚀 GENERATE ALL` to apply full decoration, or run individual actions as needed.
