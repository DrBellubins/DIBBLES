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

                            // Deterministic flip flags (same idea as MeshDataGeneration)
                            int uvFlipDirection = 0;

                            // Per-cell RNG to avoid obvious patterns
                            long cellSeed =
                                Seed ^
                                (x * 73428767L) ^
                                (y * 9127841L) ^
                                (z * 192837465L);

                            var rng = new SeededRandom(cellSeed);
                            int rndOffset = (int)(rng.NextFloat() * ChunkSize);

                            var worldRNG = new Vector3Int(x + rndOffset, y + rndOffset, z + rndOffset);
                            int flipRandom = ((worldRNG.X) ^ (worldRNG.Y) ^ (worldRNG.Z) ^ faceIdx) & 3;

                            // Honor AntiTile flags: sides use flags, top/bottom may always flip (to match your non-greedy)
                            if (faceIdx >= 0 && faceIdx <= 3) // sides
                            {
                                if (curInfo.AntiTileUVsHorizontally && (flipRandom & 1) != 0) uvFlipDirection |= 1;
                                if (curInfo.AntiTileUVsVertically   && (flipRandom & 2) != 0) uvFlipDirection |= 2;
                            }
                            else
                            {
                                // Top/bottom: keep your behavior (comment in non-greedy path)
                                if (curInfo.AntiTileUVsHorizontally || curInfo.AntiTileUVsVertically)
                                    uvFlipDirection = flipRandom;
                                else
                                    uvFlipDirection = 0;
                            }

                            mask[u, v] = new GreedyCell
                            {
                                Visible = true,
                                Type = curType,
                                FaceIdx = faceIdx,
                                UVRect = uvRect,
                                UVFlipDirection = uvFlipDirection
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
                            
                            Vector3 vBL = p1;
                            Vector3 vBR = p2;
                            Vector3 vTR = p3;
                            Vector3 vTL = p0;

                            // Smooth per-vertex colors at merged corners
                            var rectColors = FaceUtils.GetRectFaceColors(chunk, cell.FaceIdx, baseX, baseY, baseZ, du, dv);
                            
                            // Reordered colors to match BL, BR, TR, TL vertex order
                            Color cBL = rectColors[0];
                            Color cBR = rectColors[1];
                            Color cTR = rectColors[2];
                            Color cTL = rectColors[3];

                            // Build base atlas UV corners (TL, BL, BR, TR)
                            var uvTL = new Vector2(cell.UVRect.X, cell.UVRect.Y + cell.UVRect.Height);
                            var uvBL = new Vector2(cell.UVRect.X, cell.UVRect.Y);
                            var uvBR = new Vector2(cell.UVRect.X + cell.UVRect.Width, cell.UVRect.Y);
                            var uvTR = new Vector2(cell.UVRect.X + cell.UVRect.Width, cell.UVRect.Y + cell.UVRect.Height);
                            
                            // Apply the same flip parity as non-greedy
                            Vector2[] uvBase = new[] { uvTL, uvBL, uvBR, uvTR };
                            Vector2[] uvFlipped = FaceUtils.FlipUVsAtlas(uvBase, cell.FaceIdx, cell.UVFlipDirection);
                            
                            // Per-face vertex, UV, color order to match FaceUtils.GetFaceVertices() winding
                            Vector3 q0, q1, q2, q3;
                            Vector2 t0, t1, t2, t3;
                            Color col0, col1, col2, col3;

                            switch (cell.FaceIdx)
                            {
                                case 0: // Front (-Z): [BL, TL, TR, BR]
                                {
                                    q0 = p1; q1 = p0; q2 = p3; q3 = p2;
                                    t0 = uvFlipped[1]; t1 = uvFlipped[0]; t2 = uvFlipped[3]; t3 = uvFlipped[2];
                                    col0 = cBL; col1 = cTL; col2 = cTR; col3 = cBR;
                                    break;
                                }
                                case 1: // Back (+Z): [BR, TR, TL, BL]
                                {
                                    q0 = p2; q1 = p3; q2 = p0; q3 = p1;
                                    t0 = uvFlipped[2]; t1 = uvFlipped[3]; t2 = uvFlipped[0]; t3 = uvFlipped[1];
                                    col0 = cBR; col1 = cTR; col2 = cTL; col3 = cBL;
                                    break;
                                }
                                case 2: // Left (-X): [BL, TL, TR, BR]
                                {
                                    q0 = p1; q1 = p0; q2 = p3; q3 = p2;
                                    t0 = uvFlipped[1]; t1 = uvFlipped[0]; t2 = uvFlipped[3]; t3 = uvFlipped[2];
                                    col0 = cBL; col1 = cTL; col2 = cTR; col3 = cBR;
                                    break;
                                }
                                case 3: // Right (+X): [BR, TR, TL, BL]
                                {
                                    q0 = p2; q1 = p3; q2 = p0; q3 = p1;
                                    t0 = uvFlipped[2]; t1 = uvFlipped[3]; t2 = uvFlipped[0]; t3 = uvFlipped[1];
                                    col0 = cBR; col1 = cTR; col2 = cTL; col3 = cBL;
                                    break;
                                }
                                case 4: // Bottom (-Y): [BL, TL, TR, BR]
                                {
                                    q0 = p1; q1 = p0; q2 = p3; q3 = p2;
                                    t0 = uvFlipped[1]; t1 = uvFlipped[0]; t2 = uvFlipped[3]; t3 = uvFlipped[2];
                                    col0 = cBL; col1 = cTL; col2 = cTR; col3 = cBR;
                                    break;
                                }
                                case 5: // Top (+Y): [TL, TR, BR, BL]
                                {
                                    q0 = p0; q1 = p3; q2 = p2; q3 = p1;
                                    t0 = uvFlipped[0]; t1 = uvFlipped[3]; t2 = uvFlipped[2]; t3 = uvFlipped[1];
                                    col0 = cTL; col1 = cTR; col2 = cBR; col3 = cBL;
                                    break;
                                }
                                default:
                                {
                                    q0 = p1; q1 = p0; q2 = p3; q3 = p2;
                                    t0 = uvFlipped[1]; t1 = uvFlipped[0]; t2 = uvFlipped[3]; t3 = uvFlipped[2];
                                    col0 = cBL; col1 = cTL; col2 = cTR; col3 = cBR;
                                    break;
                                }
                            }
                            
                            if (!isTransparencyPass)
                            {
                                int baseOffset = vertices.Count;

                                vertices.Add(q0); vertices.Add(q1); vertices.Add(q2); vertices.Add(q3);
                                normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);
                                texcoords.Add(t0); texcoords.Add(t1); texcoords.Add(t2); texcoords.Add(t3);
                                colors.Add(col0); colors.Add(col1); colors.Add(col2); colors.Add(col3);

                                // Keep indices CCW like the per-voxel path
                                indices.Add(baseOffset + 2);
                                indices.Add(baseOffset + 1);
                                indices.Add(baseOffset + 0);
                                indices.Add(baseOffset + 3);
                                indices.Add(baseOffset + 2);
                                indices.Add(baseOffset + 0);
                            }
                            else
                            {
                                var centerLocal = (q0 + q1 + q2 + q3) * 0.25f;
                                var centerWorld = centerLocal + chunk.Position.ToVector3();
                                var dist = Vector3.Distance(GameScene.PlayerCharacter.Camera.Position.ToVector3(), centerWorld);

                                transparentFaces.Add((dist, new FaceData
                                {
                                    Verts = new[] { q0, q1, q2, q3 },
                                    Normal = normal,
                                    UVs = new[] { t0, t1, t2, t3 },
                                    Colors = new[] { col0, col1, col2, col3 },
                                    VertexOffset = 0,
                                    CenterDistance = dist
                                }));
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

        return meshData;
    }

    private static bool uvEqual(RectangleF a, RectangleF b)
    {
        return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
    }
}