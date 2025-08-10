using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using static DIBBLES.Systems.TerrainGeneration;

namespace DIBBLES.Systems;

public class TerrainMesh
{
    private readonly Dictionary<Vector3, Chunk> chunks;
    
    public TerrainMesh(Dictionary<Vector3, Chunk> chunks)
    {
        this.chunks = chunks;
    }
    
    public Model GenerateChunkMesh(Chunk chunk)
    {
        List<Vector3> vertices = [];
        List<int> indices = [];
        List<Vector3> normals = [];
        List<Vector2> texcoords = [];
        List<Color> colors = [];
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    if (chunk.Blocks[x, y, z]?.Info.Type == BlockType.Air) continue; // Skip air blocks
                    
                    var pos = new Vector3(x, y, z);
                    var blockType = chunk.Blocks[x, y, z].Info.Type;
                    
                    var lightLevel = chunk.Blocks[x, y, z].LightLevel;
                    
                    // Normalize light level from 0-15 to 0-1, ensuring minimum visibility
                    float lightValue = Math.Max(0.1f, lightLevel / 15.0f);
                    
                    var color = new Color(
                        (int)(255 * lightValue),
                        (int)(255 * lightValue),
                        (int)(255 * lightValue),
                        255
                    );
                    
                    int vertexOffset = vertices.Count;

                    // Define cube vertices (8 corners)
                    Vector3[] cubeVertices =
                    [
                        pos + new Vector3(0, 0, 0), pos + new Vector3(1, 0, 0),
                        pos + new Vector3(1, 1, 0), pos + new Vector3(0, 1, 0),
                        pos + new Vector3(0, 0, 1), pos + new Vector3(1, 0, 1),
                        pos + new Vector3(1, 1, 1), pos + new Vector3(0, 1, 1)
                    ];
                    
                    // Get UV coordinates from atlas
                    Vector2[] uvCoords;
                    
                    if (Block.AtlasUVs.TryGetValue(blockType, out var uvRect))
                    {
                        uvCoords = new Vector2[]
                        {
                            new Vector2(uvRect.X, uvRect.Y + uvRect.Height), // Top-left
                            new Vector2(uvRect.X + uvRect.Width, uvRect.Y + uvRect.Height), // Top-right
                            new Vector2(uvRect.X + uvRect.Width, uvRect.Y), // Bottom-right
                            new Vector2(uvRect.X, uvRect.Y) // Bottom-left
                        };
                    }
                    else
                    {
                        // Fallback UVs (e.g., for Air or missing textures)
                        uvCoords = new Vector2[]
                        {
                            new Vector2(0, 1), new Vector2(1, 1),
                            new Vector2(1, 0), new Vector2(0, 0)
                        };
                    }
                    
                    // UVs need to be rotated for some reason
                    Vector2[] rotatedUvCoords = new Vector2[]
                    {
                        uvCoords[1], uvCoords[2], uvCoords[3], uvCoords[0] // Rotate 90 degrees CW
                    };
                    
                    // Check each face and add only if not occluded
                    // Front face (-Z)
                    if (!IsVoxelSolid(chunk, x, y, z - 1))
                    {
                        vertices.AddRange([cubeVertices[0], cubeVertices[3], cubeVertices[2], cubeVertices[1]]);
                        normals.AddRange([new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(0, 0, -1)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 2, vertexOffset + 3, vertexOffset, vertexOffset + 1, vertexOffset + 2]);
                        vertexOffset += 4;
                    }

                    // Back face (+Z)
                    if (!IsVoxelSolid(chunk, x, y, z + 1))
                    {
                        vertices.AddRange([cubeVertices[5], cubeVertices[6], cubeVertices[7], cubeVertices[4]]);
                        normals.AddRange([new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Left face (-X)
                    if (!IsVoxelSolid(chunk, x - 1, y, z))
                    {
                        vertices.AddRange([cubeVertices[4], cubeVertices[7], cubeVertices[3], cubeVertices[0]]);
                        normals.AddRange([new Vector3(-1, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 0, 0)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Right face (+X)
                    if (!IsVoxelSolid(chunk, x + 1, y, z))
                    {
                        vertices.AddRange([cubeVertices[1], cubeVertices[2], cubeVertices[6], cubeVertices[5]]);
                        normals.AddRange([new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Bottom face (-Y)
                    if (!IsVoxelSolid(chunk, x, y - 1, z))
                    {
                        vertices.AddRange([cubeVertices[4], cubeVertices[0], cubeVertices[1], cubeVertices[5]]);
                        normals.AddRange([new Vector3(0, -1, 0), new Vector3(0, -1, 0), new Vector3(0, -1, 0), new Vector3(0, -1, 0)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Top face (+Y)
                    if (!IsVoxelSolid(chunk, x, y + 1, z))
                    {
                        vertices.AddRange([cubeVertices[3], cubeVertices[7], cubeVertices[6], cubeVertices[2]]);
                        normals.AddRange([new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                    }
                }
            }
        }

        // Create mesh
        Mesh mesh = new Mesh
        {
            VertexCount = vertices.Count,
            TriangleCount = indices.Count / 3
        };

        // Upload mesh data
        unsafe
        {
            mesh.Vertices = (float*)Raylib.MemAlloc((uint)mesh.VertexCount * 3 * sizeof(float));
                
            for (int i = 0; i < vertices.Count; i++)
            {
                mesh.Vertices[i * 3] = vertices[i].X;
                mesh.Vertices[i * 3 + 1] = vertices[i].Y;
                mesh.Vertices[i * 3 + 2] = vertices[i].Z;
            }

            mesh.Normals = (float*)Raylib.MemAlloc((uint)mesh.VertexCount * 3 * sizeof(float));
                
            for (int i = 0; i < normals.Count; i++)
            {
                mesh.Normals[i * 3] = normals[i].X;
                mesh.Normals[i * 3 + 1] = normals[i].Y;
                mesh.Normals[i * 3 + 2] = normals[i].Z;
            }
            
            mesh.TexCoords = (float*)Raylib.MemAlloc((uint)mesh.VertexCount * 2 * sizeof(float));
            
            for (int i = 0; i < texcoords.Count; i++)
            {
                mesh.TexCoords[i * 2] = texcoords[i].X;
                mesh.TexCoords[i * 2 + 1] = texcoords[i].Y;
            }

            mesh.Colors = (byte*)Raylib.MemAlloc((uint)mesh.VertexCount * 4 * sizeof(byte));
                
            for (int i = 0; i < colors.Count; i++)
            {
                mesh.Colors[i * 4] = colors[i].R;
                mesh.Colors[i * 4 + 1] = colors[i].G;
                mesh.Colors[i * 4 + 2] = colors[i].B;
                mesh.Colors[i * 4 + 3] = colors[i].A;
            }

            mesh.Indices = (ushort*)Raylib.MemAlloc((uint)indices.Count * sizeof(ushort));
                
            for (int i = 0; i < indices.Count; i++)
            {
                mesh.Indices[i] = (ushort)indices[i];
            }
            
            Raylib.UploadMesh(&mesh, false);
        }
        
        Model model = Raylib.LoadModelFromMesh(mesh);
        
        // Assign texture atlas
        if (Block.TextureAtlas.Id != 0)
        {
            unsafe
            {
                model.Materials[0].Shader = Raylib.LoadMaterialDefault().Shader;
                model.Materials[0].Maps[(int)MaterialMapIndex.Albedo].Texture = Block.TextureAtlas;
            }
        }
        
        return model;
    }
    
    private bool IsVoxelSolid(Chunk chunk, int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkHeight || z < 0 || z >= ChunkSize)
        {
            // Calculate the current chunk's coordinates from its Position
            Vector3 chunkCoord = new Vector3(
                (int)(chunk.Position.X / ChunkSize),
                0f,
                (int)(chunk.Position.Z / ChunkSize)
            );

            // Adjust chunk coordinates based on out-of-bounds voxel
            Vector3 neighborCoord = chunkCoord;
            int nx = x, nz = z;

            if (x < 0) { nx = ChunkSize - 1; neighborCoord.X -= 1; }
            else if (x >= ChunkSize) { nx = 0; neighborCoord.X += 1; }

            if (z < 0) { nz = ChunkSize - 1; neighborCoord.Z -= 1; }
            else if (z >= ChunkSize) { nz = 0; neighborCoord.Z += 1; }

            if (y < 0 || y >= ChunkHeight) return false;

            // Look up the neighboring chunk
            if (chunks.TryGetValue(neighborCoord, out var neighborChunk))
            {
                return neighborChunk.Blocks[nx, y, nz]?.Info.Type != BlockType.Air;
            }

            return false;
        }

        return chunk.Blocks[x, y, z]?.Info.Type != BlockType.Air;
    }
}