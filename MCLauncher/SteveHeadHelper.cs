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
        public static Model3DGroup CreateHeadModel(BitmapSource skinBitmap, bool isBlock = false)
        {
            var group = new Model3DGroup();

            if (isBlock)
            {
                // Upscale block texture to be razor sharp
                var sharpBlock = UpscaleCrisp(skinBitmap, 256, 256);
                var mat = CreateMaterial(sharpBlock);

                group.Children.Add(CreateFaceModel(mat, FaceType.Front));
                group.Children.Add(CreateFaceModel(mat, FaceType.Back));
                group.Children.Add(CreateFaceModel(mat, FaceType.Top));
                group.Children.Add(CreateFaceModel(mat, FaceType.Bottom));
                group.Children.Add(CreateFaceModel(mat, FaceType.Right));
                group.Children.Add(CreateFaceModel(mat, FaceType.Left));
                return group;
            }

            // Standard Minecraft Skin (64x64 or 64x32)
            // Extract and composite the 6 faces (Base layer + Outer Hat/Hair layer)
            var frontBmp = ExtractFace(skinBitmap, new Int32Rect(8, 8, 8, 8), new Int32Rect(40, 8, 8, 8));
            var backBmp = ExtractFace(skinBitmap, new Int32Rect(24, 8, 8, 8), new Int32Rect(56, 8, 8, 8));
            var rightBmp = ExtractFace(skinBitmap, new Int32Rect(0, 8, 8, 8), new Int32Rect(32, 8, 8, 8));
            var leftBmp = ExtractFace(skinBitmap, new Int32Rect(16, 8, 8, 8), new Int32Rect(48, 8, 8, 8));
            var topBmp = ExtractFace(skinBitmap, new Int32Rect(8, 0, 8, 8), new Int32Rect(40, 0, 8, 8));
            var bottomBmp = ExtractFace(skinBitmap, new Int32Rect(16, 0, 8, 8), new Int32Rect(48, 0, 8, 8));

            group.Children.Add(CreateFaceModel(CreateMaterial(frontBmp), FaceType.Front));
            group.Children.Add(CreateFaceModel(CreateMaterial(backBmp), FaceType.Back));
            group.Children.Add(CreateFaceModel(CreateMaterial(topBmp), FaceType.Top));
            group.Children.Add(CreateFaceModel(CreateMaterial(bottomBmp), FaceType.Bottom));
            group.Children.Add(CreateFaceModel(CreateMaterial(rightBmp), FaceType.Right));
            group.Children.Add(CreateFaceModel(CreateMaterial(leftBmp), FaceType.Left));

            return group;
        }

        private static DiffuseMaterial CreateMaterial(BitmapSource bmp)
        {
            var brush = new ImageBrush(bmp);
            RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(brush, EdgeMode.Aliased);
            return new DiffuseMaterial(brush);
        }

        private enum FaceType { Front, Back, Top, Bottom, Right, Left }

        private static GeometryModel3D CreateFaceModel(Material material, FaceType face)
        {
            var mesh = new MeshGeometry3D();

            switch (face)
            {
                case FaceType.Front:
                    AddQuad(mesh,
                        new Point3D(-0.5, -0.5, 0.5), new Point(0, 1),
                        new Point3D( 0.5, -0.5, 0.5), new Point(1, 1),
                        new Point3D( 0.5,  0.5, 0.5), new Point(1, 0),
                        new Point3D(-0.5,  0.5, 0.5), new Point(0, 0));
                    break;

                case FaceType.Back:
                    AddQuad(mesh,
                        new Point3D( 0.5, -0.5, -0.5), new Point(0, 1),
                        new Point3D(-0.5, -0.5, -0.5), new Point(1, 1),
                        new Point3D(-0.5,  0.5, -0.5), new Point(1, 0),
                        new Point3D( 0.5,  0.5, -0.5), new Point(0, 0));
                    break;

                case FaceType.Top:
                    AddQuad(mesh,
                        new Point3D(-0.5, 0.5,  0.5), new Point(0, 1),
                        new Point3D( 0.5, 0.5,  0.5), new Point(1, 1),
                        new Point3D( 0.5, 0.5, -0.5), new Point(1, 0),
                        new Point3D(-0.5, 0.5, -0.5), new Point(0, 0));
                    break;

                case FaceType.Bottom:
                    AddQuad(mesh,
                        new Point3D(-0.5, -0.5, -0.5), new Point(0, 1),
                        new Point3D( 0.5, -0.5, -0.5), new Point(1, 1),
                        new Point3D( 0.5, -0.5,  0.5), new Point(1, 0),
                        new Point3D(-0.5, -0.5,  0.5), new Point(0, 0));
                    break;

                case FaceType.Right: // player's right, viewer's left
                    AddQuad(mesh,
                        new Point3D(-0.5, -0.5, -0.5), new Point(0, 1),
                        new Point3D(-0.5, -0.5,  0.5), new Point(1, 1),
                        new Point3D(-0.5,  0.5,  0.5), new Point(1, 0),
                        new Point3D(-0.5,  0.5, -0.5), new Point(0, 0));
                    break;

                case FaceType.Left: // player's left, viewer's right
                    AddQuad(mesh,
                        new Point3D(0.5, -0.5,  0.5), new Point(0, 1),
                        new Point3D(0.5, -0.5, -0.5), new Point(1, 1),
                        new Point3D(0.5,  0.5, -0.5), new Point(1, 0),
                        new Point3D(0.5,  0.5,  0.5), new Point(0, 0));
                    break;
            }

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

            mesh.TriangleIndices.Add(baseIndex + 0);
            mesh.TriangleIndices.Add(baseIndex + 1);
            mesh.TriangleIndices.Add(baseIndex + 2);

            mesh.TriangleIndices.Add(baseIndex + 0);
            mesh.TriangleIndices.Add(baseIndex + 2);
            mesh.TriangleIndices.Add(baseIndex + 3);
        }

        private static BitmapSource ExtractFace(BitmapSource source, Int32Rect baseRect, Int32Rect outerRect)
        {
            // Convert source to Bgra32 format for easy pixel manipulation
            FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            converted.CopyPixels(pixels, stride, 0);

            // Create 8x8 composite face
            uint[] composite8x8 = new uint[8 * 8];

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int bx = baseRect.X + x;
                    int by = baseRect.Y + y;

                    uint baseColor = 0xFF000000;
                    if (bx < width && by < height)
                    {
                        int idx = by * stride + bx * 4;
                        baseColor = BitConverter.ToUInt32(pixels, idx);
                    }

                    // Check outer hat/hair layer
                    if (outerRect.X + 8 <= width && outerRect.Y + 8 <= height)
                    {
                        int ox = outerRect.X + x;
                        int oy = outerRect.Y + y;
                        int oidx = oy * stride + ox * 4;
                        uint outerColor = BitConverter.ToUInt32(pixels, oidx);
                        byte alpha = (byte)((outerColor >> 24) & 0xFF);

                        // If outer pixel is visible (not transparent), draw it on top
                        if (alpha > 20)
                        {
                            baseColor = outerColor;
                        }
                    }

                    composite8x8[y * 8 + x] = baseColor;
                }
            }

            // Upscale 8x8 to 256x256 (32x magnification) with nearest-neighbor for razor-sharp pixel art
            const int targetSize = 256;
            const int scale = 32; // 8 * 32 = 256
            uint[] upscaled = new uint[targetSize * targetSize];

            for (int y = 0; y < targetSize; y++)
            {
                int srcY = y / scale;
                for (int x = 0; x < targetSize; x++)
                {
                    int srcX = x / scale;
                    upscaled[y * targetSize + x] = composite8x8[srcY * 8 + srcX];
                }
            }

            WriteableBitmap result = new WriteableBitmap(targetSize, targetSize, 96, 96, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, targetSize, targetSize), upscaled, targetSize * 4, 0);
            result.Freeze();
            return result;
        }

        private static BitmapSource UpscaleCrisp(BitmapSource source, int targetWidth, int targetHeight)
        {
            FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int srcW = converted.PixelWidth;
            int srcH = converted.PixelHeight;
            int srcStride = srcW * 4;
            byte[] srcPixels = new byte[srcH * srcStride];
            converted.CopyPixels(srcPixels, srcStride, 0);

            uint[] upscaled = new uint[targetWidth * targetHeight];
            for (int y = 0; y < targetHeight; y++)
            {
                int srcY = (y * srcH) / targetHeight;
                for (int x = 0; x < targetWidth; x++)
                {
                    int srcX = (x * srcW) / targetWidth;
                    int idx = srcY * srcStride + srcX * 4;
                    upscaled[y * targetWidth + x] = BitConverter.ToUInt32(srcPixels, idx);
                }
            }

            WriteableBitmap result = new WriteableBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, targetWidth, targetHeight), upscaled, targetWidth * 4, 0);
            result.Freeze();
            return result;
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
