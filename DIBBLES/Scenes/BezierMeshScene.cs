using System.Numerics;
using Raylib_cs;
using DIBBLES.Utils;
using System.Collections.Generic;
using DIBBLES.Systems;

namespace DIBBLES.Scenes;

public class BezierMeshScene : Scene
{
    private List<Vector2> controlPoints = new List<Vector2>();
    private List<Vector2> splinePoints = new List<Vector2>();
    private Camera3D camera;
    private Model rectangleMesh;
    private Material material;
    private Texture2D groundTexture;
    private BoundingBox groundBox;
    private int selectedPointIndex = -1;
    private float extrusionHeight = 1.0f;
    private float rectangleWidth = 0.5f;
    private const int splineSegments = 50;
    private bool meshNeedsUpdate = true;

    private Model groundModel;

    public override void Start()
    {
        // Initialize control points for the Bézier spline (in X-Z plane)
        controlPoints.Add(new Vector2(-5.0f, -5.0f));
        controlPoints.Add(new Vector2(0.0f, -2.0f));
        controlPoints.Add(new Vector2(2.0f, 2.0f));
        controlPoints.Add(new Vector2(5.0f, 5.0f));

        // Generate initial spline points
        UpdateSplinePoints();

        // Initialize camera (top-down perspective)
        camera = new Camera3D
        {
            Position = new Vector3(0.0f, 20.0f, 0.0f),
            Target = new Vector3(0.0f, 0.0f, 0.0f),
            Up = new Vector3(0.0f, 0.0f, -1.0f), // Top-down view
            FovY = 45.0f,
            Projection = CameraProjection.Perspective
        };

        // Load ground texture and material
        groundTexture = Resource.Load<Texture2D>("grass_dark.png");
        material = Raylib.LoadMaterialDefault();
        Raylib.SetMaterialTexture(ref material, MaterialMapIndex.Albedo, groundTexture);

        // Create ground plane
        var planeMesh = MeshUtils.GenMeshPlaneTiled(50.0f, 50.0f, 1, 1, 1.0f, 1.0f);
        groundModel = Raylib.LoadModelFromMesh(planeMesh);
        
        unsafe { groundModel.Materials[0] = material; }
        
        groundBox = new BoundingBox(
            new Vector3(-25.0f, -0.1f, -25.0f),
            new Vector3(25.0f, 0.0f, 25.0f)
        );

        // Generate initial rectangle mesh
        material = Raylib.LoadMaterialDefault();
        Raylib.SetMaterialTexture(ref material, MaterialMapIndex.Albedo, groundTexture);
        UpdateRectangleMesh();
    }

    public override void Update()
    {
        camera.Position -= Vector3.UnitY * Raylib.GetMouseWheelMove();
        
        // Handle mouse input for editing control points
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 mousePos = GetMouseWorldPosition();
            selectedPointIndex = -1;
            float minDist = 0.5f;
            for (int i = 0; i < controlPoints.Count; i++)
            {
                if (Vector2.Distance(mousePos, controlPoints[i]) < minDist)
                {
                    selectedPointIndex = i;
                    break;
                }
            }
        }
        else if (Raylib.IsMouseButtonDown(MouseButton.Left) && selectedPointIndex != -1)
        {
            Vector2 mousePos = GetMouseWorldPosition();
            controlPoints[selectedPointIndex] = mousePos;
            meshNeedsUpdate = true; // Mark mesh for update
        }
        else if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            selectedPointIndex = -1;
        }

        // Add new control point on right-click
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            Vector2 mousePos = GetMouseWorldPosition();
            controlPoints.Add(mousePos);
            meshNeedsUpdate = true; // Mark mesh for update
        }

        // Update mesh only if needed
        if (meshNeedsUpdate)
        {
            UpdateSplinePoints();
            UpdateRectangleMesh();
            meshNeedsUpdate = false;
        }
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);

        Raylib.BeginMode3D(camera);

        // Draw ground plane
        Raylib.DrawModel(groundModel, Vector3.Zero, 1.0f, Color.White);

        // Draw the 3D rectangle mesh if it exists
        unsafe
        {
            if (rectangleMesh.Materials != null)
                Raylib.DrawModel(rectangleMesh, Vector3.Zero, 1.0f, Color.White);
        }

        // Draw spline and control points
        for (int i = 0; i < splinePoints.Count - 1; i++)
        {
            Raylib.DrawLine3D(
                new Vector3(splinePoints[i].X, 0.1f, splinePoints[i].Y),
                new Vector3(splinePoints[i + 1].X, 0.1f, splinePoints[i + 1].Y),
                Color.Red);
        }

        for (int i = 0; i < controlPoints.Count; i++)
        {
            Raylib.DrawSphere(
                new Vector3(controlPoints[i].X, 0.1f, controlPoints[i].Y),
                0.2f,
                i == selectedPointIndex ? Color.Yellow : Color.Blue);
        }

        Raylib.EndMode3D();

        // Draw instructions
        Raylib.DrawTextEx(Engine.MainFont,
            "Left-click to select/move points, Right-click to add points",
            new Vector2(10, 10), 18f, 2f, Color.White);

        Raylib.EndDrawing();
    }

    private Vector2 GetMouseWorldPosition()
    {
        var mousePos = Raylib.GetMousePosition();
        var ray = Raylib.GetMouseRay(mousePos, camera);
        float t = -ray.Position.Y / ray.Direction.Y; // Intersect with Y=0 plane
        Vector3 worldPos = ray.Position + ray.Direction * t;
        return new Vector2(worldPos.X, worldPos.Z);
    }

    private void UpdateSplinePoints()
    {
        splinePoints.Clear();
        if (controlPoints.Count < 2) return;

        for (int i = 0; i < controlPoints.Count - 1; i += 3)
        {
            for (int j = 0; j <= splineSegments; j++)
            {
                float t = j / (float)splineSegments;
                Vector2 point;
                if (i + 2 < controlPoints.Count)
                {
                    point = CalculateQuadraticBezierPoint(
                        controlPoints[i],
                        controlPoints[i + 1],
                        controlPoints[i + 2],
                        t);
                }
                else if (i + 1 < controlPoints.Count)
                {
                    point = Vector2.Lerp(controlPoints[i], controlPoints[i + 1], t);
                }
                else
                {
                    point = controlPoints[i];
                }
                splinePoints.Add(point);
            }
        }
    }

    private Vector2 CalculateQuadraticBezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        return uu * p0 + 2 * u * t * p1 + tt * p2;
    }

    private void UpdateRectangleMesh()
    {
        if (splinePoints.Count < 2) return;
    
        List<float> vertices = new List<float>();
        List<float> normals = new List<float>();
        List<float> texcoords = new List<float>();
        List<ushort> indices = new List<ushort>();
    
        // Pre-calculate all vertices for the entire mesh
        for (int i = 0; i < splinePoints.Count; i++)
        {
            Vector2 p = splinePoints[i];
            Vector2 nextP = i < splinePoints.Count - 1 ? splinePoints[i + 1] : p; // Last point uses itself for direction
            Vector2 dir = Vector2.Normalize(nextP - p);
            Vector2 perp = new Vector2(-dir.Y, dir.X);
    
            // Base vertices
            Vector3 v0 = new Vector3(p.X + perp.X * rectangleWidth * 0.5f, 0.0f, p.Y + perp.Y * rectangleWidth * 0.5f);
            Vector3 v1 = new Vector3(p.X - perp.X * rectangleWidth * 0.5f, 0.0f, p.Y - perp.Y * rectangleWidth * 0.5f);
            // Top vertices
            Vector3 v2 = v0 + new Vector3(0, extrusionHeight, 0);
            Vector3 v3 = v1 + new Vector3(0, extrusionHeight, 0);
    
            int baseIndex = vertices.Count / 3;
            vertices.AddRange(new[] { v0.X, v0.Y, v0.Z, v1.X, v1.Y, v1.Z, v2.X, v2.Y, v2.Z, v3.X, v3.Y, v3.Z });
            for (int j = 0; j < 4; j++)
                normals.AddRange(new[] { 0.0f, 1.0f, 0.0f }); // Upward normals for simplicity
            float u = i / (float)(splinePoints.Count - 1);
            texcoords.AddRange(new[] { u, 0.0f, u, 1.0f, u, 0.0f, u, 1.0f }); // Simple UV mapping
        }
    
        // Generate indices for a continuous strip
        for (int i = 0; i < splinePoints.Count - 1; i++)
        {
            int baseIndex = i * 4;
            indices.AddRange(new ushort[] {
                (ushort)(baseIndex + 0), (ushort)(baseIndex + 4), (ushort)(baseIndex + 1),
                (ushort)(baseIndex + 1), (ushort)(baseIndex + 4), (ushort)(baseIndex + 5),
                (ushort)(baseIndex + 2), (ushort)(baseIndex + 6), (ushort)(baseIndex + 3),
                (ushort)(baseIndex + 3), (ushort)(baseIndex + 6), (ushort)(baseIndex + 7)
            });
        }
    
        unsafe
        {
            Mesh mesh = new Mesh
            {
                VertexCount = vertices.Count / 3,
                TriangleCount = indices.Count / 3,
                Vertices = (float*)System.Runtime.InteropServices.Marshal.AllocHGlobal(vertices.Count * sizeof(float)),
                Normals = (float*)System.Runtime.InteropServices.Marshal.AllocHGlobal(normals.Count * sizeof(float)),
                TexCoords = (float*)System.Runtime.InteropServices.Marshal.AllocHGlobal(texcoords.Count * sizeof(float)),
                Indices = (ushort*)System.Runtime.InteropServices.Marshal.AllocHGlobal(indices.Count * sizeof(ushort))
            };
    
            System.Runtime.InteropServices.Marshal.Copy(vertices.ToArray(), 0, (System.IntPtr)mesh.Vertices, vertices.Count);
            System.Runtime.InteropServices.Marshal.Copy(normals.ToArray(), 0, (System.IntPtr)mesh.Normals, normals.Count);
            System.Runtime.InteropServices.Marshal.Copy(texcoords.ToArray(), 0, (System.IntPtr)mesh.TexCoords, texcoords.Count);
    
            byte[] indicesBytes = new byte[indices.Count * sizeof(ushort)];
            Buffer.BlockCopy(indices.ToArray(), 0, indicesBytes, 0, indicesBytes.Length);
            System.Runtime.InteropServices.Marshal.Copy(indicesBytes, 0, (System.IntPtr)mesh.Indices, indicesBytes.Length);
    
            Raylib.UploadMesh(ref mesh, false);
    
            // Unload previous mesh if it exists
            if (rectangleMesh.Materials != null)
                Raylib.UnloadModel(rectangleMesh);
    
            rectangleMesh = Raylib.LoadModelFromMesh(mesh);
            rectangleMesh.Materials[0] = material;
        }
    }

    public void Unload()
    {
        Raylib.UnloadTexture(groundTexture);
        
        unsafe
        {
            if (rectangleMesh.Materials != null)
                Raylib.UnloadModel(rectangleMesh);
        }
    }
}