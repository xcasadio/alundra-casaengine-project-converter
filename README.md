# Alundra port on CasaEngine

**Bringing Alundra into a modern game engine.**

This project aims to rebuild Alundra as a fully structured, editable and extensible game project using [CasaEngine](https://github.com/xcasadio/CasaEngineMonogame), a C# game engine built on MonoGame.

Rather than emulating the original PlayStation hardware or simply reproducing its visuals, the goal is to bring the game's entire world, assets and systems into a modern game development environment.

Every aspect of Alundra is being reimagined through modern engine concepts: worlds, entities, components, physics, rendering, animations, dialogues, gameplay systems and asset management.

**The ambition is to preserve the original game while giving it a modern foundation for exploration, modification and future enhancements.**

## A Complete Game World, Rebuilt

Alundra is no longer just a collection of PlayStation-specific data. Its content is reconstructed as a native CasaEngine project, where game elements are organized into independent, reusable and editable resources.

A full conversion already produces:

| | Converted content |
|---|---|
| **483** | Game maps and worlds |
| **9,620** | 2D animations |
| **~21,000** | Registered game assets |
| **395** | Sprite banks |
| **9,741** | Map entity instances |
| **3,316** | Portals |
| **1,714** | Map events |
| **1,041** | Audio files |

These figures represent an actual full project conversion, not estimated targets.

## Prerequisites

- **.NET 9 SDK** (`net9.0-windows`).

> **Legal note** – You must own a legitimate copy of the original *Alundra* disc to extract and analyse game data.  No copyrighted assets are distributed in this repository.

## Modernizing Every Game System

### Worlds and Level Design

The original locations are recreated as independent CasaEngine worlds.

Maps, tile layers, entities, portals and environmental properties are organized using a modern world and scene architecture.

This creates the foundation for inspecting and editing Alundra's environments through a game editor rather than relying on PlayStation-specific tools.

### Rendering and Visuals

The original 2D graphics are integrated into CasaEngine's rendering architecture.

Sprites, textures, tilemaps, animation frames, image transformations and scrolling background layers are represented as engine resources.

The goal is to faithfully reproduce the visual identity of Alundra while removing its dependency on the original PlayStation rendering pipeline.

This also establishes a foundation for future graphical improvements, including enhanced rendering and potentially HD-2D environments.

### Physics and Collisions

Alundra's original collision geometry is represented using CasaEngine's component-based collision system.

Entity bodies and animation-dependent collision volumes become modern physics components and collision timelines.

The underlying geometry can therefore be inspected and reused within the engine, while gameplay-specific collision behavior remains part of the ongoing reconstruction.

### Animations and Characters

Characters, enemies and other animated objects are organized as reusable assets.

Their original animations are represented using CasaEngine's animation system, including individual sprite parts, transformations, timing and collision keyframes.

Instead of being locked inside proprietary game data, animations become structured resources that can be inspected and extended.

### Dialogues, Events and Gameplay

The game's dialogue content is integrated with **Yarn Spinner**, providing a modern dialogue authoring and execution environment.

Alundra's original event data is preserved alongside its worlds, establishing the foundation for rebuilding gameplay interactions, scripted sequences and other behaviors.

The long-term goal is to reproduce the original game logic through modern C# gameplay systems and reusable engine components.

### Asset Management

The original game's content is organized into a modern asset library.

Textures, sprites, animations, audio, dialogues, UI elements and worlds are managed as individual resources, with centralized asset references and logical project organization.

Assets can be loaded and managed through CasaEngine rather than relying on the original game's proprietary resource structures.

### Audio and User Interface

Music, sound effects, fonts, interface graphics and other visual resources are brought into the CasaEngine project.

These resources provide the foundation for reconstructing the original game's menus, interface and audio experience using modern engine systems.

## Powered by CasaEngine

[CasaEngine](https://github.com/xcasadio/CasaEngineMonogame) is an open-source C# game engine built on top of MonoGame.

It provides a game development environment with an integrated editor and a component-based architecture.

Its systems include:

- **World and entity management** - structured scenes and reusable game objects.
- **2D rendering and animation** - sprites, tilemaps and animation timelines.
- **Physics** - collision geometry and physics components.
- **Asset management** - centralized resources and asset references.
- **Game UI** - interface development through MGUI.
- **Gameplay architecture** - extensible C# game systems.

Alundra serves as a real-world project for CasaEngine, demonstrating how a complex commercial-era game can be represented within a modern, general-purpose engine.

## Project Status

**The game content reconstruction and the playable game reimplementation are two distinct milestones.**

A substantial portion of the original game's content is already represented as CasaEngine resources, including its maps, sprites, animations, collision geometry, dialogue text and audio assets.

However, rebuilding the complete playable experience is still ongoing.

Original event scripts, gameplay interactions, AI behaviors, camera logic and other game-specific systems require further integration or reimplementation.

The ultimate objective is a faithful, fully playable version of Alundra powered by CasaEngine.

## Related Projects

- [CasaEngine](https://github.com/xcasadio/CasaEngineMonogame) - The modern C# / MonoGame engine and editor.
- [Alundra Data Analyser](https://github.com/xcasadio/alundra-datas-analyser) - Reverse engineering, analysis and extraction of the original PlayStation game data.

## License

Source code is released under the **MIT License**.  Original *Alundra* assets remain the property of their respective rights holders and are **not** included in this repository.

## Legal Notice

Alundra is the intellectual property of its respective rights holders.

This is an independent, unofficial fan and reverse-engineering project, not affiliated with or endorsed by the original creators or publishers.

Original copyrighted game assets are not distributed as part of this project. A legally obtained copy of the original game is required to extract its content.