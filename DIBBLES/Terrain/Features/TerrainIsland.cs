using DIBBLES.Utils;
using Microsoft.Xna.Framework;

namespace DIBBLES.Terrain.Features;

using static TerrainGeneration;

public class TerrainIsland
{
    public void Generate(Chunk chunk)
    {
        var noise = new FastNoiseLite();
        noise.SetSeed(Seed);
        
        const float islandThreshold = 0.6f;
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    int worldX = chunk.Position.X + x;
                    int worldY = chunk.Position.Y + y;
                    int worldZ = chunk.Position.Z + z;
                    
                    // Island noise
                    noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
                    noise.SetFrequency(0.01f);
                    noise.SetFractalType(FastNoiseLite.FractalType.FBm);
                    noise.SetFractalOctaves(4);
                    noise.SetFractalLacunarity(2.0f);
                    noise.SetFractalGain(0.5f);
                    
                    float islandNoise = noise.GetNoise(worldX, worldY, worldZ) * 0.5f + 0.5f;
                    
                    if (islandNoise > islandThreshold)
                    {
                        // Start as stone
                        chunk.SetTypeAt(x, y, z, BlockType.Stone);
                    }
                    else
                        chunk.SetTypeAt(x, y, z, BlockType.Air);
                }
            }
        }
    }
    
    // Perlin “worms” cave carver (classic MC-style). Call this after filling islands with stone.
    public void GeneratePerlinWormCaves(Chunk chunk)
    {
        // Directional noise for smooth perturbations
        var dirNoise = new FastNoiseLite();
        dirNoise.SetSeed(Seed + 1337);
        dirNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        dirNoise.SetFrequency(0.06f);
        dirNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
        dirNoise.SetFractalOctaves(2);
        dirNoise.SetFractalLacunarity(2.0f);
        dirNoise.SetFractalGain(0.5f);
    
        // Per-chunk seed
        long chunkSeed =
            Seed ^
            (chunk.Position.X * 73428767L) ^
            (chunk.Position.Y * 9127841L) ^
            (chunk.Position.Z * 192837465L);
    
        var baseRng = new SeededRandom(chunkSeed);
    
        // Worm parameters (tuned for 16³ chunks; adjust to taste)
        int wormsPerCellMin = 6;
        int wormsPerCellMax = 12;
        int stepsMin = 90;
        int stepsMax = 160;
        float stepSize = 1.25f;
    
        float radiusHMin = 1.8f;
        float radiusHMax = 4.2f;
        float radiusVScaleMin = 0.65f;
        float radiusVScaleMax = 0.9f;
    
        float perturbScale = 0.7f;   // Larger = twistier
        float verticalBias = 0.4f;   // < 1 flattens tunnels (horizontal preference)
    
        float dirFreq = 0.06f;       // Direction noise frequency (overrides if you tweak dirNoise)
    
        // Consider seeds from a 3x3x3 neighborhood of chunk-cells to guarantee cross-chunk continuity.
        // We only carve voxels that fall inside the current chunk’s bounds.
        for (int ox = -1; ox <= 1; ox++)
        for (int oy = -1; oy <= 1; oy++)
        for (int oz = -1; oz <= 1; oz++)
        {
            var cellChunkPos = new Vector3Int(
                chunk.Position.X + ox * ChunkSize,
                chunk.Position.Y + oy * ChunkSize,
                chunk.Position.Z + oz * ChunkSize
            );
    
            long cellSeed =
                Seed ^
                (cellChunkPos.X * 11400714819323198485L) ^
                (cellChunkPos.Y * 14029467366897019727L) ^
                (cellChunkPos.Z * 1609587929392839161L);
    
            var rng = new SeededRandom(cellSeed);
    
            int wormsInCell = wormsPerCellMin + (int)(rng.NextFloat() * (wormsPerCellMax - wormsPerCellMin + 1));
    
            for (int w = 0; w < wormsInCell; w++)
            {
                // Start anywhere inside this cell
                var pos = new Vector3(
                    cellChunkPos.X + rng.NextFloat() * ChunkSize,
                    cellChunkPos.Y + rng.NextFloat() * ChunkSize,
                    cellChunkPos.Z + rng.NextFloat() * ChunkSize
                );
    
                // Initial direction: mostly horizontal
                var dir = new Vector3(
                    rng.NextFloat(-1f, 1f),
                    rng.NextFloat(-0.25f, 0.25f),
                    rng.NextFloat(-1f, 1f)
                );
    
                if (dir.LengthSquared() < 1e-6f)
                    dir = new Vector3(1, 0, 0);
    
                dir.Normalize();
    
                int steps = stepsMin + (int)(rng.NextFloat() * (stepsMax - stepsMin + 1));
    
                // Radii
                float rHBase = radiusHMin + rng.NextFloat() * (radiusHMax - radiusHMin);
                float rVBase = rHBase * (radiusVScaleMin + rng.NextFloat() * (radiusVScaleMax - radiusVScaleMin));
    
                // Optional small branch budget per worm
                int branchesLeft = 2;
                float branchChance = 0.06f;
    
                // Worklist supports simple branching
                var heads = new Stack<(Vector3 pos, Vector3 dir, int steps, float rH, float rV, int depth)>();
    
                heads.Push((pos, dir, steps, rHBase, rVBase, 0));
    
                while (heads.Count > 0)
                {
                    var head = heads.Pop();
                    pos = head.pos;
                    dir = head.dir;
                    steps = head.steps;
                    float rH = head.rH;
                    float rV = head.rV;
                    int depth = head.depth;
    
                    for (int s = 0; s < steps; s++)
                    {
                        // Slight radius breathing over the path
                        float t = (s / (float)steps);
                        float wobble = 0.5f + 0.5f * MathF.Sin(t * MathF.PI * 2f + rng.NextFloat() * 6.28318f);
                        float rx = rH * (0.85f + 0.3f * wobble);
                        float ry = rV * (0.85f + 0.3f * (1f - wobble));
                        float rz = rx;
    
                        // Early reject if outside this chunk's expanded AABB
                        if (intersectsChunk(chunk, pos, rx, ry, rz))
                            carveEllipsoidInChunk(chunk, pos, rx, ry, rz);
    
                        // Branching: occasionally spawn a new head
                        if (branchesLeft > 0 && depth < 2 && s > 12 && rng.NextChance(branchChance))
                        {
                            branchesLeft--;
    
                            var bDir = dir;
                            var bend = new Microsoft.Xna.Framework.Vector3(
                                dirNoise.GetNoise(pos.X * dirFreq + 17.0f, pos.Y * dirFreq + 17.0f, pos.Z * dirFreq + 17.0f),
                                dirNoise.GetNoise(pos.X * dirFreq + 37.0f, pos.Y * dirFreq + 37.0f, pos.Z * dirFreq + 37.0f) * verticalBias,
                                dirNoise.GetNoise(pos.X * dirFreq + 59.0f, pos.Y * dirFreq + 59.0f, pos.Z * dirFreq + 59.0f)
                            );
    
                            bDir += bend * (perturbScale * 1.1f);
                            bDir.Normalize();
    
                            int bSteps = (int)((steps - s) * (0.4f + 0.2f * rng.NextFloat()));
                            float bRH = rH * (0.85f + 0.3f * rng.NextFloat());
                            float bRV = rV * (0.85f + 0.3f * rng.NextFloat());
    
                            heads.Push((pos, bDir, bSteps, bRH, bRV, depth + 1));
                        }
    
                        // Direction perturbation by noise field
                        var n =
                            new Vector3(
                                dirNoise.GetNoise(pos.X * dirFreq + 101.0f, pos.Y * dirFreq + 11.0f, pos.Z * dirFreq + 31.0f),
                                dirNoise.GetNoise(pos.X * dirFreq + 211.0f, pos.Y * dirFreq + 19.0f, pos.Z * dirFreq + 41.0f) * verticalBias,
                                dirNoise.GetNoise(pos.X * dirFreq + 313.0f, pos.Y * dirFreq + 23.0f, pos.Z * dirFreq + 53.0f)
                            );
    
                        dir += n * perturbScale;
                        
                        if (dir.LengthSquared() < 1e-6f)
                            dir = new Vector3(1, 0, 0);
                        
                        dir.Normalize();
    
                        // Advance
                        pos += dir * stepSize;
                    }
                }
            }
        }
    
        // Checks if an ellipsoid centered at 'center' with radii (rx, ry, rz) intersects this chunk
        static bool intersectsChunk(Chunk chunk, Vector3 center, float rx, float ry, float rz)
        {
            var cmin = chunk.Position;
            int cmaxX = cmin.X + ChunkSize;
            int cmaxY = cmin.Y + ChunkSize;
            int cmaxZ = cmin.Z + ChunkSize;
    
            if (center.X + rx < cmin.X || center.X - rx >= cmaxX) return false;
            if (center.Y + ry < cmin.Y || center.Y - ry >= cmaxY) return false;
            if (center.Z + rz < cmin.Z || center.Z - rz >= cmaxZ) return false;
    
            return true;
        }
    
        // Carves an ellipsoid into the current chunk only (does not touch neighbors).
        static void carveEllipsoidInChunk(Chunk chunk, Microsoft.Xna.Framework.Vector3 center, float rx, float ry, float rz)
        {
            int minX = Math.Max(chunk.Position.X, (int)MathF.Floor(center.X - rx));
            int maxX = Math.Min(chunk.Position.X + ChunkSize - 1, (int)MathF.Floor(center.X + rx));
            int minY = Math.Max(chunk.Position.Y, (int)MathF.Floor(center.Y - ry));
            int maxY = Math.Min(chunk.Position.Y + ChunkSize - 1, (int)MathF.Floor(center.Y + ry));
            int minZ = Math.Max(chunk.Position.Z, (int)MathF.Floor(center.Z - rz));
            int maxZ = Math.Min(chunk.Position.Z + ChunkSize - 1, (int)MathF.Floor(center.Z + rz));
    
            if (minX > maxX || minY > maxY || minZ > maxZ)
                return;
    
            float invRx2 = 1.0f / (rx * rx);
            float invRy2 = 1.0f / (ry * ry);
            float invRz2 = 1.0f / (rz * rz);
    
            for (int wx = minX; wx <= maxX; wx++)
            for (int wy = minY; wy <= maxY; wy++)
            for (int wz = minZ; wz <= maxZ; wz++)
            {
                float cx = (wx + 0.5f) - center.X;
                float cy = (wy + 0.5f) - center.Y;
                float cz = (wz + 0.5f) - center.Z;
    
                float e = (cx * cx) * invRx2 + (cy * cy) * invRy2 + (cz * cz) * invRz2;
    
                if (e <= 1.0f)
                {
                    int lx = wx - chunk.Position.X;
                    int ly = wy - chunk.Position.Y;
                    int lz = wz - chunk.Position.Z;
    
                    var t = chunk.GetTypeAt(lx, ly, lz);
                    
                    if (t != BlockType.Air)
                    {
                        chunk.SetTypeAt(lx, ly, lz, BlockType.Air);
                        chunk.SetCaveAt(lx, ly, lz, true);
                    }
                }
            }
        }
    }
}
