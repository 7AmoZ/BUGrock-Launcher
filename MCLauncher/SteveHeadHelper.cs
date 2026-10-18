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
                var sharpBlock = UpscaleCrisp(skinBitmap, 256, 256);
                var mat = CreateMaterial(sharpBlock);

                group.Children.Add(CreateFaceModel(mat, FaceType.Front, 0.5));
                group.Children.Add(CreateFaceModel(mat, FaceType.Back, 0.5));
                group.Children.Add(CreateFaceModel(mat, FaceType.Top, 0.5));
                group.Children.Add(CreateFaceModel(mat, FaceType.Bottom, 0.5));
                group.Children.Add(CreateFaceModel(mat, FaceType.Right, 0.5));
                group.Children.Add(CreateFaceModel(mat, FaceType.Left, 0.5));
                return group;
            }

            // 1. BASE LAYER (Inner Head Cube: size 1.0, bounds -0.5 to +0.5)
            var baseFront = ExtractSingleLayer(skinBitmap, new Int32Rect(8, 8, 8, 8), fillOpaque: true);
            var baseBack = ExtractSingleLayer(skinBitmap, new Int32Rect(24, 8, 8, 8), fillOpaque: true);
            var baseRight = ExtractSingleLayer(skinBitmap, new Int32Rect(0, 8, 8, 8), fillOpaque: true);
            var baseLeft = ExtractSingleLayer(skinBitmap, new Int32Rect(16, 8, 8, 8), fillOpaque: true);
            var baseTop = ExtractSingleLayer(skinBitmap, new Int32Rect(8, 0, 8, 8), fillOpaque: true);
            var baseBottom = ExtractSingleLayer(skinBitmap, new Int32Rect(16, 0, 8, 8), fillOpaque: true);

            group.Children.Add(CreateFaceModel(CreateMaterial(baseFront.bmp), FaceType.Front, 0.5));
            group.Children.Add(CreateFaceModel(CreateMaterial(baseBack.bmp), FaceType.Back, 0.5));
            group.Children.Add(CreateFaceModel(CreateMaterial(baseTop.bmp), FaceType.Top, 0.5));
            group.Children.Add(CreateFaceModel(CreateMaterial(baseBottom.bmp), FaceType.Bottom, 0.5));
            group.Children.Add(CreateFaceModel(CreateMaterial(baseRight.bmp), FaceType.Right, 0.5));
            group.Children.Add(CreateFaceModel(CreateMaterial(baseLeft.bmp), FaceType.Left, 0.5));

            // 2. OUTER LAYER / OVERLAY (Outer Cube: bounds -0.56 to +0.56, floats above base layer)
            const double outerR = 0.56;
            var outerFront = ExtractSingleLayer(skinBitmap, new Int32Rect(40, 8, 8, 8), fillOpaque: false);
            var outerBack = ExtractSingleLayer(skinBitmap, new Int32Rect(56, 8, 8, 8), fillOpaque: false);
            var outerRight = ExtractSingleLayer(skinBitmap, new Int32Rect(32, 8, 8, 8), fillOpaque: false);
            var outerLeft = ExtractSingleLayer(skinBitmap, new Int32Rect(48, 8, 8, 8), fillOpaque: false);
            var outerTop = ExtractSingleLayer(skinBitmap, new Int32Rect(40, 0, 8, 8), fillOpaque: false);
            var outerBottom = ExtractSingleLayer(skinBitmap, new Int32Rect(48, 0, 8, 8), fillOpaque: false);

            if (outerFront.hasVisible) group.Children.Add(CreateFaceModel(CreateMaterial(outerFront.bmp), FaceType.Front, outerR));
            if (outerBack.hasVisible) group.Children.Add(CreateFaceModel(CreateMaterial(outerBack.bmp), FaceType.Back, outerR));
            if (outerTop.hasVisible) group.Children.Add(CreateFaceModel(CreateMaterial(outerTop.bmp), FaceType.Top, outerR));
            if (outerBottom.hasVisible) group.Children.Add(CreateFaceModel(CreateMaterial(outerBottom.bmp), FaceType.Bottom, outerR));
            if (outerRight.hasVisible) group.Children.Add(CreateFaceModel(CreateMaterial(outerRight.bmp), FaceType.Right, outerR));
            if (outerLeft.hasVisible) group.Children.Add(CreateFaceModel(CreateMaterial(outerLeft.bmp), FaceType.Left, outerR));

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

        private static GeometryModel3D CreateFaceModel(Material material, FaceType face, double r = 0.5)
        {
            var mesh = new MeshGeometry3D();

            switch (face)
            {
                case FaceType.Front:
                    AddQuad(mesh,
                        new Point3D(-r, -r,  r), new Point(0, 1),
                        new Point3D( r, -r,  r), new Point(1, 1),
                        new Point3D( r,  r,  r), new Point(1, 0),
                        new Point3D(-r,  r,  r), new Point(0, 0));
                    break;

                case FaceType.Back:
                    AddQuad(mesh,
                        new Point3D( r, -r, -r), new Point(0, 1),
                        new Point3D(-r, -r, -r), new Point(1, 1),
                        new Point3D(-r,  r, -r), new Point(1, 0),
                        new Point3D( r,  r, -r), new Point(0, 0));
                    break;

                case FaceType.Top:
                    AddQuad(mesh,
                        new Point3D(-r,  r,  r), new Point(0, 1),
                        new Point3D( r,  r,  r), new Point(1, 1),
                        new Point3D( r,  r, -r), new Point(1, 0),
                        new Point3D(-r,  r, -r), new Point(0, 0));
                    break;

                case FaceType.Bottom:
                    AddQuad(mesh,
                        new Point3D(-r, -r, -r), new Point(0, 1),
                        new Point3D( r, -r, -r), new Point(1, 1),
                        new Point3D( r, -r,  r), new Point(1, 0),
                        new Point3D(-r, -r,  r), new Point(0, 0));
                    break;

                case FaceType.Right: // player's right, viewer's left
                    AddQuad(mesh,
                        new Point3D(-r, -r, -r), new Point(0, 1),
                        new Point3D(-r, -r,  r), new Point(1, 1),
                        new Point3D(-r,  r,  r), new Point(1, 0),
                        new Point3D(-r,  r, -r), new Point(0, 0));
                    break;

                case FaceType.Left: // player's left, viewer's right
                    AddQuad(mesh,
                        new Point3D( r, -r,  r), new Point(0, 1),
                        new Point3D( r, -r, -r), new Point(1, 1),
                        new Point3D( r,  r, -r), new Point(1, 0),
                        new Point3D( r,  r,  r), new Point(0, 0));
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

        private static (BitmapSource bmp, bool hasVisible) ExtractSingleLayer(BitmapSource source, Int32Rect rect, bool fillOpaque)
        {
            FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            converted.CopyPixels(pixels, stride, 0);

            uint[] layer8x8 = new uint[8 * 8];
            bool hasVisible = false;

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int px = rect.X + x;
                    int py = rect.Y + y;
                    uint color = 0x00000000;
                    if (px < width && py < height)
                    {
                        int idx = py * stride + px * 4;
                        color = BitConverter.ToUInt32(pixels, idx);
                    }

                    byte a = (byte)((color >> 24) & 0xFF);
                    if (fillOpaque)
                    {
                        color = 0xFF000000 | (color & 0x00FFFFFF);
                        hasVisible = true;
                    }
                    else
                    {
                        if (a > 15)
                        {
                            hasVisible = true;
                        }
                        else
                        {
                            color = 0x00000000;
                        }
                    }

                    layer8x8[y * 8 + x] = color;
                }
            }

            const int targetSize = 256;
            const int scale = 32;
            uint[] upscaled = new uint[targetSize * targetSize];

            for (int y = 0; y < targetSize; y++)
            {
                int srcY = y / scale;
                for (int x = 0; x < targetSize; x++)
                {
                    int srcX = x / scale;
                    upscaled[y * targetSize + x] = layer8x8[srcY * 8 + srcX];
                }
            }

            WriteableBitmap result = new WriteableBitmap(targetSize, targetSize, 96, 96, PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, targetSize, targetSize), upscaled, targetSize * 4, 0);
            result.Freeze();
            return (result, hasVisible);
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

        public static BitmapSource LoadSkinFromFile(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            using (var ms = new MemoryStream(bytes))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }
    }
}
