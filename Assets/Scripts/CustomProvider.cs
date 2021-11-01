using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using ZStandard;

#if ENABLE_ZSTD
/// <summary>
/// 自定义加载，使用zstd解压bundle数据
/// </summary>
[DisplayName("Custom ZStd AssetBundle Provider")]
public class CustomProvider : AssetBundleProvider
{
    public override void Provide(ProvideHandle provideHandle)
    {
        new CutomeAssetBundleResource().Start(provideHandle);
        //base.Provide(provideHandle);
    }
}


public class CutomeAssetBundleResource : IAssetBundleResource
{
    private AssetBundle m_Bundle;
    public AssetBundle GetAssetBundle()
    {
        return m_Bundle;
    }

    public void Start(ProvideHandle provideHandle)
    {
        string bundleFilePath = GetBundleFilePath(ref provideHandle);
        DecompressBundleFile(provideHandle, bundleFilePath);
    }

    private void DecompressBundleFile(ProvideHandle provideHandle, string bundleFilePath)
    {
        using (FileStream fs = new FileStream(bundleFilePath, FileMode.Open))
        {
            var bufferLength = fs.Length;
            byte[] compressedBuffer = new byte[bufferLength];
            fs.Read(compressedBuffer, 0, (int)bufferLength);
            using (var ms = new MemoryStream(compressedBuffer))
            {
                //var newBuffer = GZip.Decompress(memoryStream);
                var decompressedBuffer = ZStd.Decompress(ms);

                var loadAsync = AssetBundle.LoadFromMemoryAsync(decompressedBuffer);
                loadAsync.completed += (o) =>
                {
                    AssetBundle bundle = (o as AssetBundleCreateRequest).assetBundle;
                    m_Bundle = bundle;
                    provideHandle.Complete(this, bundle != null, null);
                };
            }
        }
    }

    private static string GetBundleFilePath(ref ProvideHandle provideHandle)
    {
        //var path = Application.dataPath + "/../" + provideHandle.Location.ToString();
        string bundleFilePath = provideHandle.ResourceManager.TransformInternalId(provideHandle.Location);
        Debug.LogFormat("BundlePath : {0}", bundleFilePath);
        if (!File.Exists(bundleFilePath))
        {
            Debug.LogErrorFormat("Bundle doesn't exist! {0}", bundleFilePath);
        }
        return bundleFilePath;
    }
}

#endif
