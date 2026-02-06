using DIBBLES.Scenes;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Meshing;

public class GreedyMeshing
{
    private struct GreedyCell
    {
        public bool Visible;
        public BlockType Type;
        public int FaceIdx;
        public int UVFlipDirection; // 0 = horizontal, 1 = vertical
        public RectangleF UVRect;
    }

    public static MeshData Generate(Chunk chunk, bool isTransparencyPass)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var normals = new List<Vector3>();
        var texcoords = new List<Vector2>();
        var colors = new List<Color>();
        
        // Storage for per-vertex atlas rects
        var uvRects = new List<Vector4>();
        var uvBasis = new List<Vector4>();

        var transparentFaces = new List<(float dist, FaceData face)>();

        var nc = NeighborCache.Build(chunk);

        // Scan along each axis and both face directions
        for (int axis = 0; axis < 3; axis++)
        {
            for (int dirSign = -1; dirSign <= 1; dirSign += 2)
            {
                int size = ChunkSize;

                int faceIdx;
                Vector3 normal;

                if (axis == 0) // X
                {
                    faceIdx = dirSign > 0 ? 3 : 2;
                    normal = dirSign > 0 ? new Vector3(1, 0, 0) : new Vector3(-1, 0, 0);
                }
                else if (axis == 1) // Y
                {
                    faceIdx = dirSign > 0 ? 5 : 4;
                    normal = dirSign > 0 ? new Vector3(0, 1, 0) : new Vector3(0, -1, 0);
                }
                else // Z
                {
                    faceIdx = dirSign > 0 ? 1 : 0;
                    normal = dirSign > 0 ? new Vector3(0, 0, 1) : new Vector3(0, 0, -1);
                }

                // Build 2D mask for this slice
                var mask = new GreedyCell[size, size];

                for (int slice = 0; slice < size; slice++)
                {
                    // Fill mask for the current slice
                    for (int u = 0; u < size; u++)
                    {
                        for (int v = 0; v < size; v++)
                        {
                            int x, y, z;
                            int nx, ny, nz;

                            if (axis == 0) // X slice, u=Y, v=Z
                            {
                                x = slice;
                                y = u;
                                z = v;

                                nx = x + dirSign;
                                ny = y;
                                nz = z;
                            }
                            else if (axis == 1) // Y slice, u=X, v=Z
                            {
                                x = u;
                                y = slice;
                                z = v;

                                nx = x;
                                ny = y + dirSign;
                                nz = z;
                            }
                            else // Z slice, u=X, v=Y
                            {
                                x = u;
                                y = v;
                                z = slice;

                                nx = x;
                                ny = y;
                                nz = z + dirSign;
                            }

                            // Current block
                            var curType = chunk.IsInBounds(x, y, z) ? chunk.GetTypeAt(x, y, z) : BlockType.Air;
                            var curInfo = BlockData.Prefabs[curType];

                            // Neighbor block across the face plane (solid check)
                            bool neighborSolid = Helpers.IsVoxelSolid(chunk, nx, ny, nz, nc);

                            // Billboard exclusion
                            if (curInfo.IsBillboard)
                            {
                                mask[u, v] = new GreedyCell { Visible = false };
                                continue;
                            }

                            // Decide visibility depending on pass
                            if (!isTransparencyPass)
                            {
                                // Opaque pass: solid face against non-solid neighbor
                                bool curSolid = (curType != BlockType.Air) && !curInfo.IsTransparent;

                                if (!(curSolid && !neighborSolid))
                                {
                                    mask[u, v] = new GreedyCell { Visible = false };
                                    continue;
                                }
                            }
                            else
                            {
                                // Transparent pass: transparent block face against non-solid neighbor
                                bool curTrans = (curType != BlockType.Air) && curInfo.IsTransparent;

                                if (!(curTrans && !neighborSolid))
                                {
                                    mask[u, v] = new GreedyCell { Visible = false };
                                    continue;
                                }
                            }

                            // Atlas rect for this face
                            RectangleF uvRect;
                            if (BlockData.Prefabs[curType].FaceUVs != null &&
                                BlockData.Prefabs[curType].FaceUVs.TryGetValue(faceIdx, out uvRect))
                            {
                                // per-face rect
                            }
                            else if (!BlockData.AtlasUVs.TryGetValue((curType, 0), out uvRect))
                            {
                                uvRect = new RectangleF(0, 0, 1, 1);
                            }

                            mask[u, v] = new GreedyCell
                            {
                                Visible = true,
                                Type = curType,
                                FaceIdx = faceIdx,
                                UVRect = uvRect,
                                UVFlipDirection = 0 // TEMP
                            };
                        }
                    }

                    // Rectangle extraction
                    int vRow = 0;

                    while (vRow < size)
                    {
                        int uCol = 0;

                        while (uCol < size)
                        {
                            var cell = mask[uCol, vRow];

                            if (!cell.Visible)
                            {
                                uCol++;
                                continue;
                            }

                            // Find width
                            int w = 1;

                            while (uCol + w < size)
                            {
                                var n = mask[uCol + w, vRow];
                                if (!n.Visible ||
                                    n.Type != cell.Type ||
                                    n.FaceIdx != cell.FaceIdx ||
                                    !uvEqual(n.UVRect, cell.UVRect) ||
                                    n.UVFlipDirection != cell.UVFlipDirection)
                                    break;
                                w++;
                            }

                            // Find height (maximize rectangles)
                            int h = 1;

                            for (; vRow + h < size; h++)
                            {
                                bool rowMatches = true;

                                for (int k = 0; k < w; k++)
                                {
                                    var n = mask[uCol + k, vRow + h];

                                    if (!n.Visible ||
                                        n.Type != cell.Type ||
                                        n.FaceIdx != cell.FaceIdx ||
                                        !uvEqual(n.UVRect, cell.UVRect) ||
                                        n.UVFlipDirection != cell.UVFlipDirection)
                                    {
                                        rowMatches = false;
                                        break;
                                    }
                                }

                                if (!rowMatches)
                                    break;
                            }

                            // Compute quad corners in local voxel space
                            int x0, y0, z0;

                            if (axis == 0) // X faces at plane X = slice (+ optional +1 for +X)
                            {
                                x0 = slice + (dirSign > 0 ? 1 : 0);
                                y0 = uCol;
                                z0 = vRow;
                            }
                            else if (axis == 1) // Y faces at plane Y = slice (+ optional +1 for +Y)
                            {
                                x0 = uCol;
                                y0 = slice + (dirSign > 0 ? 1 : 0);
                                z0 = vRow;
                            }
                            else // Z faces at plane Z = slice (+ optional +1 for +Z)
                            {
                                x0 = uCol;
                                y0 = vRow;
                                z0 = slice + (dirSign > 0 ? 1 : 0);
                            }

                            int du = w;
                            int dv = h;

                            Vector3 p0, p1, p2, p3;

                            if (axis == 0) // X face: plane X = x0; u=Y v=Z
                            {
                                float fx = x0;
                                p0 = new Vector3(fx, y0,      z0 + dv); // TL
                                p1 = new Vector3(fx, y0,      z0);      // BL
                                p2 = new Vector3(fx, y0 + du, z0);      // BR
                                p3 = new Vector3(fx, y0 + du, z0 + dv); // TR
                            }
                            else if (axis == 1) // Y face: plane Y = y0; u=X v=Z
                            {
                                float fy = y0;
                                p0 = new Vector3(x0,      fy, z0 + dv); // TL
                                p1 = new Vector3(x0,      fy, z0);      // BL
                                p2 = new Vector3(x0 + du, fy, z0);      // BR
                                p3 = new Vector3(x0 + du, fy, z0 + dv); // TR
                            }
                            else // Z face: plane Z = z0; u=X v=Y
                            {
                                float fz = z0;
                                p0 = new Vector3(x0,      y0 + dv, fz); // TL
                                p1 = new Vector3(x0,      y0,      fz); // BL
                                p2 = new Vector3(x0 + du, y0,      fz); // BR
                                p3 = new Vector3(x0 + du, y0 + dv, fz); // TR
                            }
                            
                            // Base voxel for lighting (do NOT use the face plane coordinate with +1)
                            int baseX, baseY, baseZ;

                            if (axis == 0) // X slices: u=Y, v=Z
                            {
                                baseX = slice;
                                baseY = uCol;
                                baseZ = vRow;
                            }
                            else if (axis == 1) // Y slices: u=X, v=Z
                            {
                                baseX = uCol;
                                baseY = slice;
                                baseZ = vRow;
                            }
                            else // Z slices: u=X, v=Y
                            {
                                baseX = uCol;
                                baseY = vRow;
                                baseZ = slice;
                            }

                            // Smooth per-vertex colors at merged corners
                            var rectColors = FaceUtils.GetRectFaceColors(chunk, cell.FaceIdx, baseX, baseY, baseZ, du, dv);
                            
                            // Build tile UVs exactly like per-voxel path
                            // FaceUtils.GetFaceUVs returns UVs ordered [TL, BL, BR, TR]
                            Vector2[] tileUVs = FaceUtils.GetFaceUVs(cell.Type, cell.FaceIdx);

                            // Optional anti-tiling flips (kept off unless you enable the toggle)
                            if (TerrainMesh.GreedyRespectAntiTileFlips && cell.UVFlipDirection != 0)
                            {
                                tileUVs = FaceUtils.FlipUVsAtlas(tileUVs, cell.FaceIdx, cell.UVFlipDirection);
                            }
                            
                            // Reorder to match the per-face vertex order (same as q0..q3 below)
                            Vector2[] orderedUVs = FaceUtils.MapUVsToFaceVertexOrder(tileUVs, cell.FaceIdx);
                            
                            // Compute UV basis from the per-face orientation (atlas space)
                            // IMPORTANT: use indices that match the new order
                            Vector2 uvBL = orderedUVs[0];
                            Vector2 uvTL = orderedUVs[1];
                            Vector2 uvBR = orderedUVs[3];
                            
                            Vector2 basisU = uvBR - uvBL; // +U direction in atlas
                            Vector2 basisV = uvTL - uvBL; // +V direction in atlas
                            
                            Vector4 basis4 = new Vector4(basisU.X, basisU.Y, basisV.X, basisV.Y);
                            
                            // Corner colors come back as [BL, BR, TR, TL]
                            Color cBL = rectColors[0];
                            Color cBR = rectColors[1];
                            Color cTR = rectColors[2];
                            Color cTL = rectColors[3];
                            
                            // Atlas sub-rect for shader tiling
                            RectangleF rect;
                            
                            if (BlockData.Prefabs[cell.Type].FaceUVs != null &&
                                BlockData.Prefabs[cell.Type].FaceUVs.TryGetValue(cell.FaceIdx, out rect)) { }
                            else if (!BlockData.AtlasUVs.TryGetValue((cell.Type, 0), out rect))
                            {
                                rect = new RectangleF(0, 0, 1, 1);
                            }
                            
                            Vector4 rect4 = new Vector4(rect.X, rect.Y, rect.Width, rect.Height);

                            // LOCAL tile-space UVs (0..du, 0..dv) — these must line up with the per-face vertex order
                            Vector2 t0, t1, t2, t3;
                            
                            // Per-face vertex order (q0..q3) and colors (col0..col3); keep CCW under CullCounterClockwise
                            Vector3 q0, q1, q2, q3;
                            Color col0, col1, col2, col3;

                            switch (cell.FaceIdx)
                            {
                                case 0: // Front (-Z): q = [BL, TL, TR, BR]
                                {
                                    q0 = p1; q1 = p0; q2 = p3; q3 = p2;
                                    col0 = cBL; col1 = cTL; col2 = cTR; col3 = cBR;
                            
                                    t0 = new Vector2(0, 0);
                                    t1 = new Vector2(0, dv);
                                    t2 = new Vector2(du, dv);
                                    t3 = new Vector2(du, 0);
                                    break;
                                }
                                case 1: // Back (+Z): q = [BR, TR, TL, BL]
                                {
                                    q0 = p2; q1 = p3; q2 = p0; q3 = p1;
                                    col0 = cBR; col1 = cTR; col2 = cTL; col3 = cBL;
                            
                                    t0 = new Vector2(du, 0);
                                    t1 = new Vector2(du, dv);
                                    t2 = new Vector2(0,  dv);
                                    t3 = new Vector2(0,  0);
                                    break;
                                }
                                case 2: // Left (-X): q = [BL, TL, TR, BR]
                                {
                                    q0 = p1; q1 = p0; q2 = p3; q3 = p2;
                                    col0 = cBL; col1 = cTL; col2 = cTR; col3 = cBR;
                            
                                    t0 = new Vector2(0, 0);
                                    t1 = new Vector2(0, dv);
                                    t2 = new Vector2(du, dv);
                                    t3 = new Vector2(du, 0);
                                    break;
                                }
                                case 3: // Right (+X): q = [BR, TR, TL, BL]
                                {
                                    q0 = p2; q1 = p3; q2 = p0; q3 = p1;
                                    col0 = cBR; col1 = cTR; col2 = cTL; col3 = cBL;
                            
                                    t0 = new Vector2(du, 0);
                                    t1 = new Vector2(du, dv);
                                    t2 = new Vector2(0,  dv);
                                    t3 = new Vector2(0,  0);
                                    break;
                                }
                                case 4: // Bottom (-Y): q = [TL, BL, BR, TR]
                                {
                                    q0 = p0; q1 = p1; q2 = p2; q3 = p3;
                                    col0 = cTL; col1 = cBL; col2 = cBR; col3 = cTR;
                            
                                    t0 = new Vector2(0,  dv);
                                    t1 = new Vector2(0,  0);
                                    t2 = new Vector2(du, 0);
                                    t3 = new Vector2(du, dv);
                                    break;
                                }
                                case 5: // Top (+Y): q = [TL, TR, BR, BL]
                                {
                                    q0 = p0; q1 = p3; q2 = p2; q3 = p1;
                                    col0 = cTL; col1 = cTR; col2 = cBR; col3 = cBL;
                            
                                    t0 = new Vector2(0,  dv);
                                    t1 = new Vector2(du, dv);
                                    t2 = new Vector2(du, 0);
                                    t3 = new Vector2(0,  0);
                                    break;
                                }
                                default:
                                {
                                    q0 = p1; q1 = p0; q2 = p3; q3 = p2;
                                    col0 = cBL; col1 = cTL; col2 = cTR; col3 = cBR;
                            
                                    t0 = new Vector2(0, 0);
                                    t1 = new Vector2(0, dv);
                                    t2 = new Vector2(du, dv);
                                    t3 = new Vector2(du, 0);
                                    break;
                                }
                            }
                            
                            // Compute distance for transparent sorting
                            var centerLocal = (q0 + q1 + q2 + q3) * 0.25f;
                            var centerWorld = centerLocal + chunk.Position.ToVector3();
                            float dist = Vector3.Distance(GameScene.PlayerCharacter.Camera.Position.ToVector3(), centerWorld);
                            
                            if (!isTransparencyPass)
                            {
                                int baseOffset = vertices.Count;

                                vertices.Add(q0); vertices.Add(q1); vertices.Add(q2); vertices.Add(q3);
                                normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);
                                texcoords.Add(t0); texcoords.Add(t1); texcoords.Add(t2); texcoords.Add(t3);
                                colors.Add(col0); colors.Add(col1); colors.Add(col2); colors.Add(col3);

                                uvRects.Add(rect4); uvRects.Add(rect4); uvRects.Add(rect4); uvRects.Add(rect4);
                                uvBasis.Add(basis4); uvBasis.Add(basis4); uvBasis.Add(basis4); uvBasis.Add(basis4);

                                indices.Add(baseOffset + 2);
                                indices.Add(baseOffset + 1);
                                indices.Add(baseOffset + 0);
                                indices.Add(baseOffset + 3);
                                indices.Add(baseOffset + 2);
                                indices.Add(baseOffset + 0);
                            }
                            else
                            {
                                transparentFaces.Add((dist, new FaceData
                                {
                                    Verts = new[] { q0, q1, q2, q3 },
                                    Normal = normal,
                                    UVs = new[] { t0, t1, t2, t3 },
                                    Colors = new[] { col0, col1, col2, col3 },
                                    VertexOffset = 0,
                                    CenterDistance = dist
                                }));

                                uvRects.Add(rect4); uvRects.Add(rect4); uvRects.Add(rect4); uvRects.Add(rect4);
                                uvBasis.Add(basis4); uvBasis.Add(basis4); uvBasis.Add(basis4); uvBasis.Add(basis4);
                            }

                            // Clear mask area
                            for (int vv = 0; vv < h; vv++)
                            {
                                for (int uu = 0; uu < w; uu++)
                                    mask[uCol + uu, vRow + vv].Visible = false;
                            }

                            uCol += w;
                        }

                        vRow++;
                    }
                }
            }
        }

        // Transparent: sort and append
        if (isTransparencyPass && transparentFaces.Count > 0)
        {
            transparentFaces.Sort((a, b) => b.dist.CompareTo(a.dist));

            foreach (var (_, face) in transparentFaces)
            {
                int baseOffset = vertices.Count;

                vertices.AddRange(face.Verts);
                normals.AddRange(Enumerable.Repeat(face.Normal, 4));
                texcoords.AddRange(face.UVs);
                colors.AddRange(face.Colors);

                indices.Add(baseOffset + 2);
                indices.Add(baseOffset + 1);
                indices.Add(baseOffset + 0);
                indices.Add(baseOffset + 3);
                indices.Add(baseOffset + 2);
                indices.Add(baseOffset + 0);
            }
        }

        // Pack MeshData
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
        
        return meshData;
    }

    private static bool uvEqual(RectangleF a, RectangleF b)
    {
        return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
    }
}