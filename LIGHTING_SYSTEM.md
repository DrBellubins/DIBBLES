# Voxel Lighting System Implementation

This document describes the Minecraft-style voxel lighting system implemented for DIBBLES.

## Overview

The lighting system implements two types of light that closely match Minecraft's mechanics:

1. **Skylight (Sunlight)**: Natural light from the sky
2. **Blocklight**: Artificial light from light sources like torches and glowstone

## Key Features

### Dual Light System
- Each block stores both skylight (0-15) and blocklight (0-15) values
- The effective light level is the maximum of both values
- Lighting data is serialized with world saves

### Skylight Propagation
- Starts at level 15 at the top of the world
- Propagates down through transparent blocks (air, water) without decreasing
- Decreases by 1 when blocked by opaque blocks
- Spreads horizontally using BFS with distance-based attenuation

### Blocklight Propagation
- Emitted by light sources:
  - Torch: Light level 14
  - Glowstone: Light level 15
- Propagates in all 6 directions through transparent blocks
- Decreases by 1 per block distance
- Uses BFS flood fill for optimal performance

### Cross-Chunk Updates
- When blocks are placed or removed, lighting updates propagate to neighboring chunks
- Handles light that crosses chunk boundaries correctly
- Updates mesh rendering for all affected chunks

## Block Properties

### Transparency
Blocks are classified as transparent or opaque:
- **Transparent**: Air, Water, Torch (allow light to pass through)
- **Opaque**: Dirt, Stone, Grass, Sand, Snow (block light)

### Light Emission
Light-emitting blocks:
- **Torch**: Emits light level 14
- **Glowstone**: Emits light level 15
- **Normal blocks**: Emit light level 0

## Rendering

### Light Modulation
- Block vertex colors are modulated by effective light level
- Uses gamma correction (γ=1.4) for realistic lighting curves
- Minimum visibility threshold prevents complete darkness

### Controls
Players can place different block types:
- Key 1 + Right-click: Place Dirt
- Key 2 + Right-click: Place Stone  
- Key 3 + Right-click: Place Torch
- Key 4 + Right-click: Place Glowstone
- Left-click: Break blocks

## Technical Implementation

### Algorithm Efficiency
- BFS (Breadth-First Search) ensures optimal light propagation
- Light updates are batched for performance
- Cross-chunk updates minimize redundant calculations

### Data Storage
- Lighting data is stored per block in chunks
- Serialized to disk with world save data
- Backwards compatible with existing save files

### Integration Points
- `TerrainGeneration.generateLighting()`: Main lighting calculation
- `Block.EffectiveLightLevel`: Combines skylight and blocklight
- `WorldSave`: Serializes lighting data
- Mesh generation: Applies lighting to vertex colors

## Performance Considerations

- Lighting is calculated only when chunks are generated or modified
- BFS ensures each block is processed only once per update
- Neighbor chunk updates are limited to affected areas only
- Mesh regeneration is batched with lighting updates

## Minecraft Compatibility

The implementation closely follows Minecraft's lighting rules:
- Light levels range from 0-15
- Skylight propagates down through transparent blocks
- Blocklight attenuates by 1 per block distance
- Maximum of skylight and blocklight determines effective illumination
- Similar gamma correction and minimum visibility thresholds