using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

using System.IO;
using System.IO.Compression;
using System.Text;

public class AALoad : MonoBehaviour
{
    void Start()
    {
        //Test();
    }

    void Test()
    {
        string myString = "testCompress111", //测试用string
        result;// 解压后结果

        MemoryStream outFile = new MemoryStream();//outFile就是压缩后存放的地方，用于网络传输的对象
        outFile = GZip.Compress(Encoding.UTF8.GetBytes(myString));// 压缩源字符串

        result = Encoding.UTF8.GetString(GZip.Decompress(outFile));// 解压得到源字符串

        print(result);
    }

    void OnGUI()
    {
        if (GUILayout.Button("实例化"))
        {
            //Addressables.InstantiateAsync(m_refPrefab, Random.insideUnitSphere * 3, Quaternion.identity);
            Addressables.InstantiateAsync("LocalSphere", Random.insideUnitSphere * 3, Quaternion.identity);

            //Addressables.InstantiateAsync("RemoteCube", Random.insideUnitSphere * 3, Quaternion.identity);


        }
    }
}

public static class GZip
{
    public static MemoryStream Compress(byte[] inBytes)
    {
        MemoryStream outStream = new MemoryStream();

        using (MemoryStream intStream = new MemoryStream(inBytes))
        {
            using (GZipStream Compress = new GZipStream(outStream, CompressionMode.Compress))
            {
                intStream.CopyTo(Compress);
            }
        }

        return outStream;
    }

    public static byte[] Decompress(MemoryStream inStream)
    {
        byte[] result = null;
        MemoryStream compressedStream = new MemoryStream(inStream.ToArray());

        using (MemoryStream outStream = new MemoryStream())
        {
            using (GZipStream Decompress = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                Decompress.CopyTo(outStream);
                result = outStream.ToArray();
            }
        }

        compressedStream.Dispose();
        compressedStream.Close();
        return result;
    }
}
