using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats;
using System;
using System.Collections.Generic;
//using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using UnityEngine;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using UnityEngine.Assertions;
using SixLabors.ImageSharp.Memory;
using System.Numerics;
using Image = SixLabors.ImageSharp.Image;
using UnityEngine.UI;

namespace Assets.Scripts
{
    //internal class ImageExtensions
    //{
    //}

    public static class ImageExtensions
    {
        #region Public Methods

        /// <summary>
        /// Extension method that converts a Image to an byte array
        /// </summary>
        /// <param name="imageIn">The Image to convert</param>
        /// <returns>An byte array containing the JPG format Image</returns>
        public static byte[] ToArray(this SixLabors.ImageSharp.Image imageIn)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                imageIn.Save(ms, JpegFormat.Instance);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Extension method that converts a Image to an byte array
        /// </summary>
        /// <param name="imageIn">The Image to convert</param>
        /// <param name="fmt"></param>
        /// <returns>An byte array containing the JPG format Image</returns>
        public static byte[] ToArray(this SixLabors.ImageSharp.Image imageIn, IImageFormat fmt)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                imageIn.Save(ms, fmt);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Extension method that converts a Image to an byte array
        /// </summary>
        /// <param name="imageIn">The Image to convert</param>
        /// <returns>An byte array containing the JPG format Image</returns>
        //public static byte[] ToArray(this global::System.Drawing.Image imageIn)
        //{
        //    return ToArray(imageIn, ImageFormat.Png);
        //}

        ///// <summary>
        ///// Converts the image data into a byte array.
        ///// </summary>
        ///// <param name="imageIn">The image to convert to an array</param>
        ///// <param name="fmt">The format to save the image in</param>
        ///// <returns>An array of bytes</returns>
        //public static byte[] ToArray(this global::System.Drawing.Image imageIn, ImageFormat fmt)
        //{
        //    using (MemoryStream ms = new MemoryStream())
        //    {
        //        imageIn.Save(ms, fmt);
        //        return ms.ToArray();
        //    }
        //}

        /// <summary>
        /// Extension method that converts a byte array with JPG data to an Image
        /// </summary>
        /// <param name="byteArrayIn">The byte array with JPG data</param>
        /// <returns>The reconstructed Image</returns>
        public static Image ToImage(this byte[] byteArrayIn)
        {
            using (MemoryStream ms = new MemoryStream(byteArrayIn))
            {
                Image returnImage = Image.Load(ms);
                return returnImage;
            }
        }

        //public static global::System.Drawing.Image ToNetImage(this byte[] byteArrayIn)
        //{
        //    using (MemoryStream ms = new MemoryStream(byteArrayIn))
        //    {
        //        global::System.Drawing.Image returnImage = global::System.Drawing.Image.FromStream(ms);
        //        return returnImage;
        //    }
        //}

        public static Texture2D ToUnityTexture(this SixLabors.ImageSharp.Image imageIn)
        {
            //Texture2D tex = new Texture2D(imageIn.Width, imageIn.Height, TextureFormat.PVRTC_RGBA4, false);
            Texture2D tex = new Texture2D(imageIn.Width, imageIn.Height, TextureFormat.RGBA32, false);
            using (MemoryStream ms = new MemoryStream())
            {
                //imageIn.Save(ms, JpegFormat.Instance);
                ////byte[] tmp = ms.ToArray();
                ////tex.LoadRawTextureData(tmp);
                ////UnityException: LoadRawTextureData: not enough data provided (will result in overread)
                //tex.LoadRawTextureData(ms.ToArray());//

                //imageIn.savepi
            }

            byte[] bts = ToArray(imageIn, PngFormat.Instance);
            Debug.Log("ToUnityTexture bts.length" + bts.Length + " wid:" + imageIn.Width + " hei:" + imageIn.Height + " format:" +
                imageIn.GetConfiguration().ImageFormatsManager.ImageFormats);//1030
            //tex.LoadRawTextureData(bts);

            byte[] jpegImage;
            using (MemoryStream buffer = new())
            {
                //using Image<Rgba32> image = new(100, 100);
                //image.SaveAsJpeg(buffer);
                imageIn.SaveAsPng(buffer);
                //var imageEncoder = imageIn.GetConfiguration().ImageFormatsManager.FindEncoder(imageFormat);
                imageIn.SaveAsPng("Assets/Stages/051/cry_1.png");
                jpegImage = buffer.ToArray();
            }
            Debug.Log("ToUnityTexture jpegImage.length" + jpegImage.Length );
            //tex.LoadRawTextureData(jpegImage);//报错!!!!!!error
            ImageConversion.LoadImage(tex, jpegImage);//ok!!!!!!!!!!!!!!!!!!!!!!!!
            //参考 https://docs.unity3d.com/ScriptReference/ImageConversion.LoadImage.html

            return tex;
        }

        public static byte[] ToArray1(this SixLabors.ImageSharp.Image imageIn)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                imageIn.Save(ms, PngFormat.Instance);
                return ms.ToArray();
            }
        }

        public static System.Drawing.Image ToDrawingImage(this byte[] byteArrayIn)
        {
            using (MemoryStream ms = new MemoryStream(byteArrayIn))
            {
                System.Drawing.Image returnImage = System.Drawing.Image.FromStream(ms);
                return returnImage;
            }
        }

        //https://www.gamedev.net/forums/topic/701489-memorystream-use-in-unity-for-systemdrawingimage/
        public static System.Drawing.Image byteArrayToImage(byte[] byteArrayIn)
        {
            MemoryStream ms = new MemoryStream(byteArrayIn);
            System.Drawing.Image returnImage = System.Drawing.Image.FromStream(ms);
            return returnImage;

        }

        //public static void drawImage(Image<TPixel> image)
        //{
        //    Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();
        //    Debug.Log("ToUnityTexture jpegImage.length" + jpegImage.Length);
        //    //tex.LoadRawTextureData(jpegImage);//报错!!!!!!error
        //    return tex;
        //}

        //https://gist.github.com/vurdalakov/00d9471356da94454b372843067af24e
        public static Byte[] ToArray<TPixel>(this Image<TPixel> image, IImageFormat imageFormat) where TPixel : unmanaged, IPixel<TPixel>
        {
            using (var memoryStream = new MemoryStream())
            {
                var imageEncoder = image.GetConfiguration().ImageFormatsManager.FindEncoder(imageFormat);
                image.Save(memoryStream, imageEncoder);
                return memoryStream.ToArray();
            }
        }

        //https://gist.github.com/vurdalakov/00d9471356da94454b372843067af24e
        //public static System.Drawing.Bitmap ImageSharpToBitmap(this SixLabors.ImageSharp.Image img)
        //{
        //    if (img == null) return new System.Drawing.Bitmap(0, 0);
        //    var stream = new MemoryStream();
        //    img.Save(stream, BmpFormat.Instance);
        //    stream.Position = 0;
        //    return new System.Drawing.Bitmap(stream);
        //}

        private static Image<Rgba32> WriteAndReadJpeg(Image<Rgba32> image)
        {
            using var memStream = new MemoryStream();
            image.SaveAsJpeg(memStream);
            image.Dispose();

            memStream.Position = 0;
            return Image.Load<Rgba32>(memStream);
        }

        private static Image<Rgba32> WriteAndReadPng(Image<Rgba32> image)
        {
            using var memStream = new MemoryStream();
            image.SaveAsPng(memStream);
            image.Dispose();

            memStream.Position = 0;
            return Image.Load<Rgba32>(memStream);
        }

        public static void DetectFormatAllocatesCleanBuffer()
        {
            byte[] jpegImage;
            using (MemoryStream buffer = new())
            {
                using Image<Rgba32> image = new(100, 100);
                image.SaveAsJpeg(buffer);
                jpegImage = buffer.ToArray();
            }

            IImageFormat format = Image.DetectFormat(jpegImage);
            //Assert.IsType<JpegFormat>(format);

            byte[] invalidImage = { 1, 2, 3 };
            //Assert.Throws<UnknownImageFormatException>(() => Image.DetectFormat(invalidImage));
        }

        //public static Byte[] ToArray<TPixel>(this Image<TPixel> image, IImageFormat imageFormat) where TPixel : unmanaged, IPixel<TPixel>
        //{
        //    using (var memoryStream = new MemoryStream())
        //    {
        //        var imageEncoder = image.GetConfiguration().ImageFormatsManager.FindEncoder(imageFormat);
        //        image.Save(memoryStream, imageEncoder);
        //        return memoryStream.ToArray();
        //    }
        //}

        //public static System.Drawing.Bitmap ToBitmap<TPixel>(this Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>
        //{
        //    using (var memoryStream = new MemoryStream())
        //    {
        //        var imageEncoder = image.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance);
        //        image.Save(memoryStream, imageEncoder);

        //        memoryStream.Seek(0, SeekOrigin.Begin);

        //        return new System.Drawing.Bitmap(memoryStream);
        //    }
        //}

        //public static Image<TPixel> ToImageSharpImage<TPixel>(this System.Drawing.Bitmap bitmap) where TPixel : unmanaged, IPixel<TPixel>
        //{
        //    using (var memoryStream = new MemoryStream())
        //    {
        //        bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);

        //        memoryStream.Seek(0, SeekOrigin.Begin);

        //        return Image.Load<TPixel>(memoryStream);
        //    }
        //}

        #endregion Public Methods
    }
}


//Rgba32[] finalImageByteArray = finalImage
//                        .GetPixelMemoryGroup()
//                        .SelectMany(group => group.ToArray())
//                        .ToArray();
//但是，这会产生 Rgba32[]，而不是 byte[]
//https://www.soinside.com/question/YWRtZ7HesN3HreZMJz3XhL

//试试这个：

//        var img = new Image<Rgba32>(100, 100);
//var bytes = new byte[img.Height * img.Width * img.PixelType.BitsPerPixel / 8];
//img.Frames.RootFrame.CopyPixelDataTo(bytes);
//一些注意事项：

//您需要提供预先分配的数组。因此，数组的大小是根据图像大小和每个像素的大小计算的（除以 8 将位数转换为字节数）
//您得到一个副本，而不是指向图像对象内部缓冲区的指针。

//https://gist.github.com/vurdalakov/00d9471356da94454b372843067af24e
//ImageSharpExtensions.cs