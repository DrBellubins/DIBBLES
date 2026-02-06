using DIBBLES.Scenes;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;

using static DIBBLES.Terrain.TerrainGeneration;
using static DIBBLES.Terrain.TerrainMesh;
using static DIBBLES.Terrain.Meshing.Helpers;

namespace DIBBLES.Terrain.Meshing;

public class MeshDataGeneration
{
    // MeshData generation (thread-safe, basic data for meshing)
    public MeshData Generate(Chunk chunk, bool isTransparencyPass)
    {
        // Switch to greedy meshing when enabled
        if (UseGreedyMeshing)
            return GreedyMeshing.Generate(chunk, isTransparencyPass);
        
        var cameraPosition = GameScene.PlayerCharacter.Camera.Position.ToVector3();
        
        var neighborCache = NeighborCache.Build(chunk);
        
        List<(float dist, FaceData face)> transparentFaces = new();
        List<Vector3> vertices = [];
        List<int> indices = [];
        List<Vector3> normals = [];
        List<Vector2> texcoords = [];
        List<Color> colors = [];
        
        var rng = new SeededRandom(1337); // TEMP
        
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var pos = new Vector3Int(x, y, z);
            var blockType = chunk.GetTypeAt(x, y, z);
            var blockInfo = chunk.GetInfoAt(x, y, z);
            
            long chunkBlockSeed = Seed 
                             ^ (pos.X * 73428767L)
                             ^ (pos.Y * 9127841L)
                             ^ (pos.Z * 192837465L);
            
            rng.SetSeed(chunkBlockSeed);
            
            // Billboard path for transparent mesh
            if (isTransparencyPass && blockInfo.IsBillboard && blockType != BlockType.Air)
            {
                // Billboards are handled by instanced pipeline; skip adding faces here
                continue;
            }
            
            // Opaque mesh pass: skip transparent and air blocks
            if (!isTransparencyPass && (blockInfo.IsTransparent || blockType == BlockType.Air)) continue;

            // Transparent mesh pass: skip opaque and air blocks
            if (isTransparencyPass && (!blockInfo.IsTransparent || blockType == BlockType.Air)) continue;
            
            int vertexOffset = vertices.Count;
            
            foreach (var (faceIdx, normal, neighborOffset) in FaceUtils.VoxelFaceInfos())
            {
                int nx = x + neighborOffset.X;
                int ny = y + neighborOffset.Y;
                int nz = z + neighborOffset.Z;
                
                if (!IsVoxelSolid(chunk, nx, ny, nz, neighborCache))
                {
                    var faceVerts = FaceUtils.GetFaceVertices(pos.ToVector3(), faceIdx);
                    
                    Vector2[] baseUVs;

                    if (!BlockData.FaceUVsOrdered.TryGetValue((blockType, faceIdx), out baseUVs))
                    {
                        // Fallback only if not present: compute once at runtime
                        var canonical = FaceUtils.GetFaceUVs(blockType, faceIdx);
                        baseUVs = FaceUtils.MapUVsToFaceVertexOrder(canonical, faceIdx);
                    }
                    
                    // Deterministic random uv flipping
                    int rotationSteps = 0;
                    int flipMask = NonGreedyRespectAntiTileFlips
                        ? Helpers.ComputeRndFlipMask(rng, blockInfo, pos, faceIdx)
                        : 0;
                    
                    Vector2 uv0 = baseUVs[0];
                    Vector2 uv1 = baseUVs[1];
                    Vector2 uv2 = baseUVs[2];
                    Vector2 uv3 = baseUVs[3];

                    if (flipMask != 0)
                    {
                        float minX = MathF.Min(MathF.Min(uv0.X, uv1.X), MathF.Min(uv2.X, uv3.X));
                        float maxX = MathF.Max(MathF.Max(uv0.X, uv1.X), MathF.Max(uv2.X, uv3.X));
                        float minY = MathF.Min(MathF.Min(uv0.Y, uv1.Y), MathF.Min(uv2.Y, uv3.Y));
                        float maxY = MathF.Max(MathF.Max(uv0.Y, uv1.Y), MathF.Max(uv2.Y, uv3.Y));

                        // 1 = horizontal flip (reflect X), 2 = vertical flip (reflect Y)
                        if ((flipMask & 1) != 0)
                        {
                            uv0.X = minX + maxX - uv0.X;
                            uv1.X = minX + maxX - uv1.X;
                            uv2.X = minX + maxX - uv2.X;
                            uv3.X = minX + maxX - uv3.X;
                        }

                        if ((flipMask & 2) != 0)
                        {
                            uv0.Y = minY + maxY - uv0.Y;
                            uv1.Y = minY + maxY - uv1.Y;
                            uv2.Y = minY + maxY - uv2.Y;
                            uv3.Y = minY + maxY - uv3.Y;
                        }
                    }
                    
                    float faceLight = FaceUtils.GetFaceLightFlat(chunk, pos, faceIdx); // samples surrounding voxels
                    Color flatColor = FaceUtils.ToColor(faceLight);

                    Color[] faceColors = SmoothLighting
                        ? FaceUtils.GetFaceColors(chunk, pos, faceIdx)
                        : new[] { flatColor, flatColor, flatColor, flatColor };
                    
                    // Ambient occlusion - Disabled because it looks to "grungey"
                    /*float[] ao = FaceUtils.GetFaceAmbientOcclusion(chunk, neighborCache, pos, faceIdx); // AO factors [0..1]
                    
                    // Multiply AO into vertex colors
                    for (int vi = 0; vi < 4; vi++)
                    {
                        var c = faceColors[vi];
                        float f = ao[vi];

                        faceColors[vi] = new Color(
                            (byte)(c.R * f),
                            (byte)(c.G * f),
                            (byte)(c.B * f),
                            c.A
                        );
                    }*/
                    
                    if (isTransparencyPass)
                    {
                        // For transparent faces, store for sorting
                        var center = (faceVerts[0] + faceVerts[1] + faceVerts[2] + faceVerts[3]) / 4f;
                        var dist = Vector3.Distance(cameraPosition, center);
                        
                        transparentFaces.Add((dist, new FaceData
                        {
                            Verts = faceVerts,
                            Normal = normal,
                            UVs = new[] { uv0, uv1, uv2, uv3 },
                            Colors = faceColors,
                            VertexOffset = vertexOffset,
                            CenterDistance =  dist
                        }));
                    }
                    else
                    {
                        // Opaque faces: add immediately
                        vertices.AddRange(faceVerts);
                        normals.AddRange(Enumerable.Repeat(normal, 4));
                        texcoords.AddRange(new []{uv0, uv1, uv2, uv3});
                        //texcoords.AddRange(faceUVs);
                        
                        colors.AddRange(faceColors);
                        
                        indices.AddRange(new int[]
                        {
                            vertexOffset + 2, vertexOffset + 1, vertexOffset + 0,
                            vertexOffset + 3, vertexOffset + 2, vertexOffset + 0
                        });
        
                        vertexOffset += 4;
                    }
                }
            }
        }
        
        // If transparent: sort faces back-to-front and build arrays
        if (isTransparencyPass)
        {
            transparentFaces.Sort((a, b) => b.dist.CompareTo(a.dist));
            
            int vertexOffset = 0;
            
            foreach (var (_, face) in transparentFaces)
            {
                vertices.AddRange(face.Verts);
                normals.AddRange(Enumerable.Repeat(face.Normal, 4));
                texcoords.AddRange(face.UVs);
                colors.AddRange(face.Colors);
                
                indices.AddRange(new int[]
                {
                    vertexOffset + 2, vertexOffset + 1, vertexOffset + 0,
                    vertexOffset + 3, vertexOffset + 2, vertexOffset + 0
                });
                
                vertexOffset += 4;
            }
        }

        // Convert lists to arrays
        int vcount = vertices.Count;
        int icount = indices.Count / 3;
        var meshData = new MeshData(vcount, icount);
        
        for (int i = 0; i < vertices.Count; i++)
        {
            meshData.Vertices[i * 3 + 0] = vertices[i].X;
            meshData.Vertices[i * 3 + 1] = vertices[i].Y;
            meshData.Vertices[i * 3 + 2] = vertices[i].Z;
        }
        
        for (int i = 0; i < normals.Count; i++)
        {
            meshData.Normals[i * 3 + 0] = normals[i].X;
            meshData.Normals[i * 3 + 1] = normals[i].Y;
            meshData.Normals[i * 3 + 2] = normals[i].Z;
        }
        
        for (int i = 0; i < texcoords.Count; i++)
        {
            meshData.TexCoords[i * 2 + 0] = texcoords[i].X;
            meshData.TexCoords[i * 2 + 1] = texcoords[i].Y;
        }
        
        for (int i = 0; i < colors.Count; i++)
        {
            meshData.Colors[i * 4 + 0] = colors[i].R;
            meshData.Colors[i * 4 + 1] = colors[i].G;
            meshData.Colors[i * 4 + 2] = colors[i].B;
            meshData.Colors[i * 4 + 3] = colors[i].A;
        }
        
        for (int i = 0; i < indices.Count; i++)
            meshData.Indices[i] = (ushort)indices[i];

        return meshData;
    }
}