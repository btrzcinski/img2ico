using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Img2Ico
{
    class Program
    {
        private static void PrintUsage()
        {
            Console.WriteLine(@"
Usage: Img2Ico [source.png] [output.ico]
The source PNG file should be 256x256 with 32-bit color.
");
        }

        static IEnumerable<Bitmap> GetBitmapsForColorDepth(Bitmap source, PixelFormat format)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var sizes = new int[] { 16, 24, 32, 48 };

            var bitmaps = new List<Bitmap>();
            foreach (var size in sizes)
            {
                var newBitmap = new Bitmap(size, size, format);
                using (var g = Graphics.FromImage(newBitmap))
                {
                    g.DrawImage(source, 0, 0, size, size);
                }

                bitmaps.Add(newBitmap);
            }

            return bitmaps;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 6)]
        struct ICONDIR
        {
            public UInt16 reserved; // 0
            public UInt16 imageType; // 1 for ICO
            public UInt16 numImages;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
        struct ICONDIRENTRY
        {
            public byte imageWidth; // 0 == 256
            public byte imageHeight; // 0 == 256
            public byte numColors; // 0 for PNG
            public byte reserved; // 0
            public UInt16 colorPlanes; // 0 or 1
            public UInt16 bitsPerPixel;
            public UInt32 imageDataSize;
            public UInt32 imageDataOffset; // offset from top of file
        }

        // Layout: ICONDIR, ICONDIRENTRY[], PngData[]
        static void WriteIcoFile(string fileName,
            IEnumerable<Bitmap> smallerBmps,
            Bitmap big256PxSource)
        {
            var dir = new ICONDIR
            {
                reserved = 0,
                imageType = 1,
                numImages = (UInt16)(1 + smallerBmps.Count())
            };

            var entries = new ICONDIRENTRY[dir.numImages];

            var pngsToWrite = new Queue<MemoryStream>();

            // Smaller entries go first
            var i = 0;
            var offset = Marshal.SizeOf(dir) + 
                (Marshal.SizeOf(entries[0]) * entries.Length);
            foreach (var img in smallerBmps)
            {
                entries[i].imageWidth = (byte)img.Size.Width;
                entries[i].imageHeight = (byte)img.Size.Height;
                entries[i].numColors = 0;
                entries[i].reserved = 0;
                entries[i].colorPlanes = 0;
                entries[i].bitsPerPixel = 32;

                var stream = new MemoryStream();
                img.Save(stream, ImageFormat.Png);
                pngsToWrite.Enqueue(stream);

                entries[i].imageDataSize = (UInt32)stream.Length;
                entries[i].imageDataOffset = (UInt32)offset;

                Debug.WriteLine($"Storing {img.Size.Width}x{img.Size.Height} image with size {stream.Length} at offset {offset}");

                offset += (int)stream.Length;
                i++;
            }

            // 256x256 goes last
            entries[i].imageWidth = (byte)big256PxSource.Size.Width;
            entries[i].imageHeight = (byte)big256PxSource.Size.Height;
            entries[i].numColors = 0;
            entries[i].reserved = 0;
            entries[i].colorPlanes = 0;
            entries[i].bitsPerPixel = 32;

            var bigImageStream = new MemoryStream();
            big256PxSource.Save(bigImageStream, ImageFormat.Png);
            pngsToWrite.Enqueue(bigImageStream);

            entries[i].imageDataSize = (UInt32)bigImageStream.Length;
            entries[i].imageDataOffset = (UInt32)offset;

            Debug.WriteLine($"Storing {big256PxSource.Size.Width}x{big256PxSource.Size.Height} image with size {bigImageStream.Length} at offset {offset}");

            offset = 0;
            // Write all of the data out to the ICO file
            using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var header = GetBytesForStruct(dir);
                fileStream.Write(header, 0, header.Length);
                
                Debug.WriteLine($"Wrote header ({header.Length} bytes, offset = {offset})");
                offset += header.Length;

                i = 0;
                foreach (var entry in entries)
                {
                    var entryBytes = GetBytesForStruct(entry);
                    fileStream.Write(entryBytes, 0, entryBytes.Length);
                    
                    Debug.WriteLine($"Wrote directory entry {i++} ({entryBytes.Length} bytes, offset = {offset})");
                    offset += entryBytes.Length;
                }
                while (pngsToWrite.Count > 0)
                {
                    var pngStream = pngsToWrite.Dequeue();
                    pngStream.Position = 0;
                    pngStream.CopyTo(fileStream);
                    Debug.WriteLine($"Wrote PNG ({pngStream.Length} bytes, offset = {offset})");
                    offset += (int)pngStream.Length;
                }
            }
        }

        static byte[] GetBytesForStruct(object o)
        {
            var size = Marshal.SizeOf(o);
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(o, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                PrintUsage();
                return;
            }

            var sourceFile = args[0];
            if (!File.Exists(sourceFile))
            {
                Console.WriteLine($"File does not exist: {sourceFile}");
                PrintUsage();
                return;
            }

            Bitmap source = null;

            try
            {
                source = (Bitmap)Image.FromFile(sourceFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred reading {sourceFile}");
                Console.WriteLine(ex);
                return;
            }

            if (source.Size.Height != 256 ||
                source.Size.Width != 256)
            {
                Console.WriteLine($"{sourceFile} should be exactly 256x256");
                return;
            }

            if (source.PixelFormat != PixelFormat.Format32bppArgb)
            {
                Console.WriteLine($"{sourceFile} should be 32-bit ARGB format");
                return;
            }

            var smallerBmps = GetBitmapsForColorDepth(source, PixelFormat.Format32bppArgb);

            var outputFile = args[1];
            try
            {
                WriteIcoFile(outputFile, smallerBmps, source);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred creating new ICO file");
                Console.WriteLine(ex);
                return;
            }

            Console.WriteLine($"Wrote output: {outputFile}");
        }
    }
}
