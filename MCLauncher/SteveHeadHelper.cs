using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace MCLauncher
{
    public static class SteveHeadHelper
    {
        public static GeometryModel3D CreateHeadModel(ImageSource skinTexture)
        {
            var mesh = new MeshGeometry3D();

            // Cube from -0.5 to 0.5
            // Standard Minecraft Skin 64x64 UV mapping:
            // Texture coordinates normalized (0.0 to 1.0)
            // Top:    X [8..16]/64, Y [0..8]/64   => U [0.125, 0.25], V [0.0, 0.125]
            // Bottom: X [16..24]/64, Y [0..8]/64  => U [0.25, 0.375], V [0.0, 0.125]
            // Right:  X [0..8]/64, Y [8..16]/64   => U [0.0, 0.125], V [0.125, 0.25]
            // Front:  X [8..16]/64, Y [8..16]/64  => U [0.125, 0.25], V [0.125, 0.25]
            // Left:   X [16..24]/64, Y [8..16]/64 => U [0.25, 0.375], V [0.125, 0.25]
            // Back:   X [24..32]/64, Y [8..16]/64 => U [0.375, 0.5], V [0.125, 0.25]

            // 1. FRONT FACE (Z = +0.5)
            AddQuad(mesh,
                new Point3D(-0.5, -0.5, 0.5), new Point(0.125, 0.250), // bottom-left
                new Point3D( 0.5, -0.5, 0.5), new Point(0.250, 0.250), // bottom-right
                new Point3D( 0.5,  0.5, 0.5), new Point(0.250, 0.125), // top-right
                new Point3D(-0.5,  0.5, 0.5), new Point(0.125, 0.125)  // top-left
            );

            // 2. BACK FACE (Z = -0.5)
            AddQuad(mesh,
                new Point3D( 0.5, -0.5, -0.5), new Point(0.375, 0.250),
                new Point3D(-0.5, -0.5, -0.5), new Point(0.500, 0.250),
                new Point3D(-0.5,  0.5, -0.5), new Point(0.500, 0.125),
                new Point3D( 0.5,  0.5, -0.5), new Point(0.375, 0.125)
            );

            // 3. TOP FACE (Y = +0.5)
            AddQuad(mesh,
                new Point3D(-0.5, 0.5,  0.5), new Point(0.125, 0.125),
                new Point3D( 0.5, 0.5,  0.5), new Point(0.250, 0.125),
                new Point3D( 0.5, 0.5, -0.5), new Point(0.250, 0.000),
                new Point3D(-0.5, 0.5, -0.5), new Point(0.125, 0.000)
            );

            // 4. BOTTOM FACE (Y = -0.5)
            AddQuad(mesh,
                new Point3D(-0.5, -0.5, -0.5), new Point(0.250, 0.000),
                new Point3D( 0.5, -0.5, -0.5), new Point(0.375, 0.000),
                new Point3D( 0.5, -0.5,  0.5), new Point(0.375, 0.125),
                new Point3D(-0.5, -0.5,  0.5), new Point(0.250, 0.125)
            );

            // 5. RIGHT FACE (X = -0.5)
            AddQuad(mesh,
                new Point3D(-0.5, -0.5, -0.5), new Point(0.000, 0.250),
                new Point3D(-0.5, -0.5,  0.5), new Point(0.125, 0.250),
                new Point3D(-0.5,  0.5,  0.5), new Point(0.125, 0.125),
                new Point3D(-0.5,  0.5, -0.5), new Point(0.000, 0.125)
            );

            // 6. LEFT FACE (X = +0.5)
            AddQuad(mesh,
                new Point3D(0.5, -0.5,  0.5), new Point(0.250, 0.250),
                new Point3D(0.5, -0.5, -0.5), new Point(0.375, 0.250),
                new Point3D(0.5,  0.5, -0.5), new Point(0.375, 0.125),
                new Point3D(0.5,  0.5,  0.5), new Point(0.250, 0.125)
            );

            var brush = new ImageBrush(skinTexture)
            {
                // Nearest neighbor scaling gives crisp Minecraft pixel art
                ViewportUnits = BrushMappingMode.Absolute
            };
            RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);

            var material = new DiffuseMaterial(brush);
            return new GeometryModel3D(mesh, material);
        }

        private static void AddQuad(MeshGeometry3D mesh,
            Point3D p0, Point uv0,
            Point3D p1, Point uv1,
            Point3D p2, Point uv2,
            Point3D p3, Point uv3)
        {
            int baseIndex = mesh.Positions.Count;

            mesh.Positions.Add(p0);
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);

            mesh.TextureCoordinates.Add(uv0);
            mesh.TextureCoordinates.Add(uv1);
            mesh.TextureCoordinates.Add(uv2);
            mesh.TextureCoordinates.Add(uv3);

            // Triangle 1: p0, p1, p2
            mesh.TriangleIndices.Add(baseIndex + 0);
            mesh.TriangleIndices.Add(baseIndex + 1);
            mesh.TriangleIndices.Add(baseIndex + 2);

            // Triangle 2: p0, p2, p3
            mesh.TriangleIndices.Add(baseIndex + 0);
            mesh.TriangleIndices.Add(baseIndex + 2);
            mesh.TriangleIndices.Add(baseIndex + 3);
        }

        public static BitmapImage LoadSkinFromFile(string filePath)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(filePath, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
    }
}
