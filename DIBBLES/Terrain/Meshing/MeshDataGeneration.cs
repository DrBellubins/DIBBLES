using DIBBLES.Scenes;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;

using static DIBBLES.Terrain.TerrainGeneration;
using static DIBBLES.Terrain.TerrainMesh;
using static DIBBLES.Terrain.Meshing.Helpers;

namespace DIBBLES.Terrain.Meshing;

// TODO: When placing along z axis, anti tiling shifts on blocks around placed block
public class MeshDataGeneration
{
    // MeshData generation (thread-safe, basic data for meshing)
    public MeshData Generate(Chunk chunk, bool isTransparencyPass)
    {
        // Switch to greedy meshing when enabled
        if (UseGreedyMeshing)
            return GreedyMeshing.Generate(chunk, isTransparencyPass);
        
        var neighborCache = NeighborCache.Build(chunk);
        
        List<FaceData> transparentFaces = new();
        List<Vector3> vertices = [];
        List<int> indices = [];
        List<Vector3> normals = [];
        List<Vector2> texcoords = [];
        List<Color> colors = [];
        
        List<Vector4> uvRects = new();
        List<Vector4> uvBasis = new();
        List<Vector4> emissiveUVRects = new();
        List<Vector4> emissiveUVBasis = new();
        
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
                    
                    // samples surrounding voxels
                    Color[] faceColors;

                    if (SmoothLighting)
                    {
                        faceColors = FaceUtils.GetFaceColors(chunk, pos, faceIdx, blockInfo.Brightness);
                    }
                    else
                    {
                        float faceLight = FaceUtils.GetFaceLightFlat(chunk, pos, faceIdx, blockInfo.Brightness);
                        Color flatColor = FaceUtils.ToColor(faceLight);

                        faceColors = new[] { flatColor, flatColor, flatColor, flatColor };
                    }
                    
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
                    
                    // Base atlas rect + basis (ordered per-face UVs already computed in baseUVs)
                    RectangleF baseRect;

                    if (blockInfo.FaceUVs != null && blockInfo.FaceUVs.TryGetValue(faceIdx, out baseRect))
                    {
                    }
                    else if (!BlockData.AtlasUVs.TryGetValue((blockType, faceIdx), out baseRect))
                    {
                        baseRect = new RectangleF(0, 0, 1, 1);
                    }

                    Vector2 baseBL = baseUVs[0];
                    Vector4 baseBasis4 = FaceUtils.ComputeUVBasis(baseUVs);
                    Vector4 baseRect4 = new Vector4(baseBL.X, baseBL.Y, baseRect.Width, baseRect.Height);

                    // Emissive atlas rect + basis
                    Vector2[] emisUVs;

                    if (!BlockData.EmissiveFaceUVsOrdered.TryGetValue((blockType, faceIdx), out emisUVs))
                    {
                        // Fallback to base orientation if emissive face UVs are missing
                        emisUVs = baseUVs;
                    }

                    RectangleF emisRect;

                    if (!BlockData.EmissiveAtlasUVs.TryGetValue((blockType, faceIdx), out emisRect))
                    {
                        // Fallback: use base rect (means emissive atlas layout matches or is absent)
                        emisRect = baseRect;
                    }

                    Vector2 emisBL = emisUVs[0];
                    Vector4 emisBasis4 = FaceUtils.ComputeUVBasis(emisUVs);
                    Vector4 emisRect4 = new Vector4(emisBL.X, emisBL.Y, emisRect.Width, emisRect.Height);
                    
                    if (isTransparencyPass)
                    {
                        transparentFaces.Add(new FaceData
                        {
                            Verts = faceVerts,
                            Normal = normal,
                            UVs = new[] { uv0, uv1, uv2, uv3 },
                            Colors = faceColors,
                            VertexOffset = vertexOffset,
                            Type = blockType,
                            FaceIdx = faceIdx,
                            
                            BaseRect = baseRect4,
                            BaseBasis = baseBasis4,
                            EmissiveRect = emisRect4,
                            EmissiveBasis = emisBasis4
                        });
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
        
                        uvRects.Add(baseRect4);
                        uvRects.Add(baseRect4);
                        uvRects.Add(baseRect4);
                        uvRects.Add(baseRect4);

                        uvBasis.Add(baseBasis4);
                        uvBasis.Add(baseBasis4);
                        uvBasis.Add(baseBasis4);
                        uvBasis.Add(baseBasis4);

                        emissiveUVRects.Add(emisRect4);
                        emissiveUVRects.Add(emisRect4);
                        emissiveUVRects.Add(emisRect4);
                        emissiveUVRects.Add(emisRect4);

                        emissiveUVBasis.Add(emisBasis4);
                        emissiveUVBasis.Add(emisBasis4);
                        emissiveUVBasis.Add(emisBasis4);
                        emissiveUVBasis.Add(emisBasis4);
                        
                        vertexOffset += 4;
                    }
                }
            }
        }
        
        // If transparent: sort faces back-to-front and build arrays
        if (isTransparencyPass)
        {
            int vertexOffset = 0;
            
            foreach (var face in transparentFaces)
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

                // Append base and emissive UV data for transparent faces
                uvRects.Add(face.BaseRect);
                uvRects.Add(face.BaseRect);
                uvRects.Add(face.BaseRect);
                uvRects.Add(face.BaseRect);

                uvBasis.Add(face.BaseBasis);
                uvBasis.Add(face.BaseBasis);
                uvBasis.Add(face.BaseBasis);
                uvBasis.Add(face.BaseBasis);

                emissiveUVRects.Add(face.EmissiveRect);
                emissiveUVRects.Add(face.EmissiveRect);
                emissiveUVRects.Add(face.EmissiveRect);
                emissiveUVRects.Add(face.EmissiveRect);

                emissiveUVBasis.Add(face.EmissiveBasis);
                emissiveUVBasis.Add(face.EmissiveBasis);
                emissiveUVBasis.Add(face.EmissiveBasis);
                emissiveUVBasis.Add(face.EmissiveBasis);

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

        for (int i = 0; i < uvRects.Count; i++)
        {
            meshData.UVRects[i * 4 + 0] = uvRects[i].X;
            meshData.UVRects[i * 4 + 1] = uvRects[i].Y;
            meshData.UVRects[i * 4 + 2] = uvRects[i].Z;
            meshData.UVRects[i * 4 + 3] = uvRects[i].W;
        }

        for (int i = 0; i < uvBasis.Count; i++)
        {
            meshData.UVBasis[i * 4 + 0] = uvBasis[i].X;
            meshData.UVBasis[i * 4 + 1] = uvBasis[i].Y;
            meshData.UVBasis[i * 4 + 2] = uvBasis[i].Z;
            meshData.UVBasis[i * 4 + 3] = uvBasis[i].W;
        }

        for (int i = 0; i < emissiveUVRects.Count; i++)
        {
            meshData.EmissiveUVRects[i * 4 + 0] = emissiveUVRects[i].X;
            meshData.EmissiveUVRects[i * 4 + 1] = emissiveUVRects[i].Y;
            meshData.EmissiveUVRects[i * 4 + 2] = emissiveUVRects[i].Z;
            meshData.EmissiveUVRects[i * 4 + 3] = emissiveUVRects[i].W;
        }

        for (int i = 0; i < emissiveUVBasis.Count; i++)
        {
            meshData.EmissiveUVBasis[i * 4 + 0] = emissiveUVBasis[i].X;
            meshData.EmissiveUVBasis[i * 4 + 1] = emissiveUVBasis[i].Y;
            meshData.EmissiveUVBasis[i * 4 + 2] = emissiveUVBasis[i].Z;
            meshData.EmissiveUVBasis[i * 4 + 3] = emissiveUVBasis[i].W;
        }
        
        return meshData;
    }
}