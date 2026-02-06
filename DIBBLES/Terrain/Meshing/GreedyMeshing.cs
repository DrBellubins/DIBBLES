namespace DIBBLES.Terrain.Meshing;

using DIBBLES.Scenes;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using static DIBBLES.Terrain.TerrainGeneration;

public class GreedyMeshing
{
    private struct GreedyCell
    {
        public bool Visible;
        public BlockType Type;
        public int FaceIdx;
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

                            // Atlas rect for this face (stretch over merged quad)
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
                                UVRect = uvRect
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
                                    !uvEqual(n.UVRect, cell.UVRect))
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
                                        !uvEqual(n.UVRect, cell.UVRect))
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

                            // Smooth per-vertex colors at merged corners
                            var rectColors = FaceUtils.GetRectFaceColors(
                                chunk, cell.FaceIdx,
                                (axis == 0) ? slice : x0 - ((axis == 1) ? 0 : 0), // x0 for corners is already set
                                (axis == 1) ? slice : y0 - ((axis == 0) ? 0 : 0),
                                (axis == 2) ? slice : z0 - ((axis == 2) ? 0 : 0),
                                du, dv
                            );

                            // Stretch atlas UVs (acceptable default)
                            var uvTL = new Vector2(cell.UVRect.X, cell.UVRect.Y + cell.UVRect.Height);
                            var uvBL = new Vector2(cell.UVRect.X, cell.UVRect.Y);
                            var uvBR = new Vector2(cell.UVRect.X + cell.UVRect.Width, cell.UVRect.Y);
                            var uvTR = new Vector2(cell.UVRect.X + cell.UVRect.Width, cell.UVRect.Y + cell.UVRect.Height);

                            if (!isTransparencyPass)
                            {
                                int baseOffset = vertices.Count;

                                vertices.Add(p0);
                                vertices.Add(p1);
                                vertices.Add(p2);
                                vertices.Add(p3);

                                normals.Add(normal);
                                normals.Add(normal);
                                normals.Add(normal);
                                normals.Add(normal);

                                texcoords.Add(uvTL);
                                texcoords.Add(uvBL);
                                texcoords.Add(uvBR);
                                texcoords.Add(uvTR);

                                colors.Add(rectColors[0]);
                                colors.Add(rectColors[1]);
                                colors.Add(rectColors[2]);
                                colors.Add(rectColors[3]);

                                indices.Add(baseOffset + 2);
                                indices.Add(baseOffset + 1);
                                indices.Add(baseOffset + 0);
                                indices.Add(baseOffset + 3);
                                indices.Add(baseOffset + 2);
                                indices.Add(baseOffset + 0);
                            }
                            else
                            {
                                var centerLocal = (p0 + p1 + p2 + p3) * 0.25f;
                                var centerWorld = centerLocal + chunk.Position.ToVector3();
                                var dist = Vector3.Distance(GameScene.PlayerCharacter.Camera.Position.ToVector3(), centerWorld);

                                transparentFaces.Add((dist, new FaceData
                                {
                                    Verts = new[] { p0, p1, p2, p3 },
                                    Normal = normal,
                                    UVs = new[] { uvTL, uvBL, uvBR, uvTR },
                                    Colors = rectColors,
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