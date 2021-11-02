
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.Build.Pipeline;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.Util;
using ZStandard;
using static UnityEditor.AddressableAssets.Build.ContentUpdateScript;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
{
    [CreateAssetMenu(fileName = "ZStdBuildScriptPackedMode.asset", menuName = "Addressable Assets/Data Builders/ZStdBuildScriptPackedMode")]
    public class ZStdBuildScriptPackedMode : BuildScriptPackedMode
    {
#if ENABLE_ZSTD

        public override string Name
        {
            get { return "ZStd Compression Build"; }    //自定义Build的名字
        }

        protected override TResult DoBuild<TResult>(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
        {
            TResult opResult = base.DoBuild<TResult>(builderInput, aaContext);
            var groups = aaContext.Settings.groups;
            foreach (var assetGroup in groups)
            {
                if (aaContext.assetGroupToBundles.TryGetValue(assetGroup, out List<string> buildBundles))
                {
                    if (assetGroup.ReadOnly)
                    {
                        continue;
                    }
                    for (int i = 0; i < assetGroup.Schemas.Count; i++)
                    {
                        var schema = assetGroup.Schemas[i];
                        //if (schema is UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema)
                        //{
                        //    var s = schema as UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema;
                        //}
                        //else
                        if (schema is UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema)
                        {
                            var bundledAssetGroupSchema = schema as UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema;
                            bundledAssetGroupSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.Uncompressed;

                            if (schema != null)
                            {
                                Type t = schema.GetType();
                                FieldInfo[] fieldInfos = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                                foreach (var fieldIfo in fieldInfos)
                                {
                                    if (fieldIfo.Name == "m_AssetBundleProviderType")
                                    {
                                        SerializedType o = (SerializedType)fieldIfo.GetValue(schema);
                                        o.Value = typeof(CustomProvider);
                                        fieldIfo.SetValue(schema, (object)o);
                                        EditorUtility.SetDirty(assetGroup);
                                    }
                                }
                            }
                        }
                    }

                    var locations = aaContext.locations;
                    for (int i = 0; i < locations.Count; i++)
                    {
                        if (locations[i].Data != null)
                        {
                            var location = locations[i];
                            var assetBundleRequestOptions = location.Data as AssetBundleRequestOptions;

                            if (assetBundleRequestOptions != null)
                            {
                                string path = GetBundleFilePath(assetGroup, location);
                                CompressBundleFile(path);
                            }
                        }
                    }
                }
            }

            EditorUtility.RequestScriptReload(); // 重新加载，刷新面板

            return opResult;
        }

        private static void CompressBundleFile(string path)
        {
            using (FileStream orginalFs = new FileStream(path, FileMode.Open))
            {
                var bufferLength = orginalFs.Length;
                byte[] originalBuffer = new byte[bufferLength];
                orginalFs.Read(originalBuffer, 0, (int)bufferLength);
                //var memoryStream = GZip.Compress(bufferFile);
                using (var memoryStream = ZStd.Compress(originalBuffer))   // 调用Zstd压缩
                {
                    var compressedBuffer = TrimEndBytes(memoryStream.ToArray());
                    orginalFs.Close(); // 须要Close，否则执行下一行报错
                    using (FileStream compressedFs = new FileStream(path, FileMode.Truncate))
                    {
                        BinaryWriter bw = new BinaryWriter(compressedFs);
                        bw.Write(compressedBuffer);
                    }
                }
            }
        }

        private static string GetBundleFilePath(AddressableAssetGroup assetGroup, ContentCatalogDataEntry location)
        {
            var schema = assetGroup.GetSchema<BundledAssetGroupSchema>();
            var s = location.InternalId.Split('/');
            var bundleFilePath = Application.dataPath + "/../" + schema.BuildPath.GetValue(assetGroup.Settings) + "/" + s[s.Length - 1];

            UnityEngine.Debug.LogFormat("BundlePath : {0}", bundleFilePath);
            if (!File.Exists(bundleFilePath))
            {
                UnityEngine.Debug.LogErrorFormat("Bundle doesn't exist! {0}", bundleFilePath);
            }
            return bundleFilePath;
        }

        public static byte[] TrimEndBytes(byte[] bytes)
        {
            //List<byte> list = bytes.ToList();
            //for (int i = bytes.Length - 1; i >= 0; i--)
            //{
            //    if (bytes[i] == 0x00)
            //    {
            //        list.RemoveAt(i);
            //    }
            //    else
            //    {
            //        break;
            //    }
            //}
            //return list.ToArray();

            int endIndex = -1;
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                if (bytes[i] != 0x00)
                {
                    endIndex = i;
                    break;
                }
            }

            if (endIndex == -1)
            {
                return new byte[0];
            }
            else
            {
                byte[] result = new byte[endIndex + 1];
                Array.Copy(bytes, result, endIndex + 1);
                return result;
            }
        }

#endif

    }

    public class AutoSetting
    {
        [MenuItem("AA/GroupAutoSetting %a")]
        /// <summary>
        /// 设置资源打进包体里（大包）
        /// </summary>
        public static void SetLargePacket()
        {
            foreach (var addressableAssetGroup in addressableAssetSettings.groups)
            {
                if (/*addressableAssetGroup.IsDefaultGroup() ||*/ addressableAssetGroup.ReadOnly)
                    continue;
                for (int i = 0; i < addressableAssetGroup.Schemas.Count; i++)
                {
                    var schema = addressableAssetGroup.Schemas[i];
                    if (schema is UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema)
                    {
                        //(schema as UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema)
                        //    .StaticContent = true;
                        var s = schema as UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema;
                    }
                    else if (schema is UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema)
                    {
                        var bundledAssetGroupSchema = schema as UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema;
                        //bundledAssetGroupSchema.BuildPath.SetVariableByName(addressableAssetGroup.Settings,
                        //    AddressableAssetSettings.kLocalBuildPath);
                        //bundledAssetGroupSchema.LoadPath.SetVariableByName(addressableAssetGroup.Settings,
                        //    AddressableAssetSettings.kLocalLoadPath);

                        bundledAssetGroupSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZMA;

                        if (schema != null)
                        {
                            Type t = schema.GetType();
                            FieldInfo[] fieldInfos = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            foreach (var fieldIfo in fieldInfos)
                            {
                                if (fieldIfo.Name == "m_AssetBundleProviderType")
                                {
                                    SerializedType o = (SerializedType)fieldIfo.GetValue(schema);
                                    o.Value = typeof(CustomProvider);
                                    fieldIfo.SetValue(schema, (object)o);
                                    EditorUtility.SetDirty(addressableAssetGroup);
                                }
                            }
                        }
                    }
                }
            }

            EditorUtility.RequestScriptReload(); //
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("设置成大包资源完成");
        }

        private static AddressableAssetSettings m_AddressableAssetSettings;
        private static AddressableAssetSettings addressableAssetSettings
        {
            get
            {
                if (m_AddressableAssetSettings == null)
                {
                    m_AddressableAssetSettings = AddressableAssetSettingsDefaultObject.Settings;
                    if (m_AddressableAssetSettings == null)
                        m_AddressableAssetSettings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>("Assets/AddressableAssetsData/AddressableAssetSettings.asset");
                }
                return m_AddressableAssetSettings;
            }
        }

        void modifyGroupBundleProvider(BundledAssetGroupSchema schema)
        {


        }
    }
}

