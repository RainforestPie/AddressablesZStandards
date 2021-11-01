using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZStandard
{
    public static class API
    {
#if (UNITY_IPHONE || UNITY_WEBGL || UNITY_SWITCH) && !UNITY_EDITOR
        const string ZSTDDLL = "__Internal";
#else
        const string ZSTDDLL = "zstd";
#endif

        /// <summary>
        /// decompress zstd
        /// return 1 is error. 0 is success.
        /// </summary>
        /// <example>
        /// byte[] src;
        /// byte[] dst;
        /// var result = Uzstd.decompress(dst, dst.Length, src, src.Length);
        /// </example>
        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int decompress([Out] byte[] dst, int dstSize, [In] byte[] src, int srcSize);

        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int compress([Out] byte[] dst, int dstSize, [In] byte[] src, int srcSize, int compressionLevel);

        /// <summary>
        /// decompress zstd with dictionary.
        /// return 1. decompress error.
        /// </summary>
        /// <example>
        /// byte[] src;
        /// byte[] dst;
        /// byte[] dict;
        /// using(var context = DecompressContext.CreateDictionary(dict, dict.Length))
        /// {
        ///   var result = Uzstd.decompressDictionary(context, dst, dst.Length, src, src.Length);
        /// }
        /// </example>
        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int decompressDictionary([In][Out] DecompressContext context, [Out] byte[] dst, int dstSize, [In] byte[] src, int srcSize);

        /// <summary>
        /// streaming decompress zstd
        /// return > 0. decompressed size
        /// return = 0. done decompress.
        /// return < 0. error.
        /// </summary>
        /// <example>
        /// using(var context = DecompressContext.Create()) //DecompressContext.CreateStreamDictionary(dict, dict.Length)
        /// {
        ///   byte[] compressed;
        ///   byte[] decompressed;
        ///   byte[] work; //it can be smaller than decompressed.
        ///   int totalDecompressedSize = 0;
        ///   do
        ///   {
        ///      var decompressedSize = decompressStream(context, work, work.Length, compressed, compressed.Length );
        ///      if( decompressedSize == 0 ) { break; } //success to read.
        ///      if( decompressedSize < 0 ) { throw new System.Exception("..."); }
        ///      System.Array.Copy( work, 0, decompressed, totalDecompressedSize, decompressedSize );
        ///      totalDecompressedSize += decompressedSize;
        ///   } while (totalDecompressedSize < decompressed.Length);
        /// }
        /// </example>
        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int decompressStream([In][Out] DecompressContext context, [Out] byte[] dst, int dstSize, [In] byte[] src, int srcSize);

        /// <summary>
        /// streaming decompress zstd (with dictionary)
        /// <see cref="decompressStream(DecompressContext, byte[], int, byte[], int)"/>
        /// </summary>
        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int decompressStreamDictionary([In][Out] DecompressContext context, [Out] byte[] dst, int dstSize, [In] byte[] src, int srcSize);

        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int compressBound(int srcSize);

        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int decompress_getFrameContentSize([In] byte[] src, int srcSize);  // unsign long long
    }

    /// <summary>
    /// streaming decompress informations.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class DecompressContext : IDisposable
    {
#if (UNITY_IPHONE || UNITY_WEBGL || UNITY_SWITCH) && !UNITY_EDITOR
        const string ZSTDDLL = "__Internal";
#else
        const string ZSTDDLL = "zstd";
#endif

        /// <summary>
        /// create for stream.
        /// </summary>
        public static DecompressContext CreateWithStream()
        {
            var context = new DecompressContext();
            initializeDecompressContext(context);
            return context;
        }

        /// <summary>
        /// create for dictionary.
        /// </summary>
        public static DecompressContext CreateWithDictionary(byte[] dict)
        {
            var context = new DecompressContext();
            initializeDecompressContextDictionary(context, dict, dict.Length);
            return context;
        }

        /// <summary>
        /// create for stream with dictionary.
        /// </summary>
        public static DecompressContext CreateWithStreamDictionary(byte[] dict)
        {
            var context = new DecompressContext();
            initializeDecompressContextStreamDictionary(context, dict, dict.Length);
            return context;
        }

        public IntPtr dctx;
        public IntPtr ddict;
        public IntPtr dstream;
        public int totalReadSize;

        public void Dispose()
        {
            finalizeDecompressContext(this);
        }

        DecompressContext()
        {
            this.dctx = default(IntPtr);
            this.ddict = default(IntPtr);
            this.dstream = default(IntPtr);
            this.totalReadSize = 0;
        }

        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void initializeDecompressContext([In][Out] DecompressContext context);

        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void initializeDecompressContextDictionary([In][Out] DecompressContext context, [In] byte[] dict, int dictSize);

        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void initializeDecompressContextStreamDictionary([In][Out] DecompressContext context, [In] byte[] dict, int dictSize);

        [DllImport(ZSTDDLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void finalizeDecompressContext([In][Out] DecompressContext context);
    }

    public class ZStd
    {
        private const int COMPRESSION_LEVEL = 1;
        public static MemoryStream Compress(byte[] original)
        {
            int compressSize = API.compressBound(original.Length);
            byte[] compressed = new byte[compressSize];
            var result = API.compress(compressed, compressSize, original, original.Length, COMPRESSION_LEVEL); // compression level : [1, 22]
            if (result != 0)
            {
                Debug.LogFormat("compress error, result: {0}", result);
                return null;
            }
            MemoryStream compressedStream = new MemoryStream(compressed);

            return compressedStream;
        }

        public static byte[] Decompress(MemoryStream ms)
        {
            byte[] decompressed = null;
            using (MemoryStream outStream = new MemoryStream())
            {
                byte[] compressed = ms.ToArray();
                var decompressedSize = API.decompress_getFrameContentSize(compressed, compressed.Length);
                decompressed = new byte[decompressedSize];
                var result = API.decompress(decompressed, decompressed.Length, compressed, compressed.Length);
                if (result != 0)
                {
                    Debug.LogFormat("decompress error, result: {0}", result);
                    return null;
                }
            }

            return decompressed;
        }
    }

}
