// *********************************************************************************
// # Project: JFramework
// # Unity: 2022.3.5f1c1
// # Author: Charlotte
// # Version: 1.0.0
// # History: 2023-10-24  23:53
// # Copyright: 2023, Charlotte
// # Description: This is an automatically generated comment.
// *********************************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JFramework.Core;
using Newtonsoft.Json;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace JFramework
{
    /// <summary>
    /// AB包更新相关
    /// </summary>
    public static class AssetRequest
    {
        /// <summary>
        /// 本地数据列表
        /// </summary>
        private static Dictionary<string, AssetData> clientDataList = new Dictionary<string, AssetData>();

        /// <summary>
        /// 远端数据列表
        /// </summary>
        private static Dictionary<string, AssetData> remoteDataList = new Dictionary<string, AssetData>();

        /// <summary>
        /// 资源数据列表
        /// </summary>
        private static readonly List<string> assetDataList = new List<string>();

        /// <summary>
        /// 资源加载进度
        /// </summary>
        public static event Action<string, float> OnLoadProgress;


        /// <summary>
        /// 检测是否需要更新
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> UpdateAssetBundles()
        {
            if (!GlobalManager.Runtime) return false;
            assetDataList.Clear();
            clientDataList.Clear();
            remoteDataList.Clear();
            var success = await GetRemoteInfo();
            if (success)
            {
                var remoteInfo = await File.ReadAllTextAsync(GlobalSetting.remoteInfoPath);
                var jsonData = JsonConvert.DeserializeObject<List<AssetData>>(remoteInfo);
                remoteDataList = jsonData.ToDictionary(data => data.name);
                Debug.Log("解析远端对比文件完成");

                if (File.Exists(GlobalSetting.clientInfoPath))
                {
                    var clientInfo = await File.ReadAllTextAsync(GlobalSetting.clientInfoPath);
                    jsonData = JsonConvert.DeserializeObject<List<AssetData>>(clientInfo);
                    clientDataList = jsonData.ToDictionary(data => data.name);
                }
                else if (File.Exists(GlobalSetting.streamingInfoPath))
                {
                    var clientInfo = await File.ReadAllTextAsync(GlobalSetting.streamingInfoPath);
                    jsonData = JsonConvert.DeserializeObject<List<AssetData>>(clientInfo);
                    clientDataList = jsonData.ToDictionary(data => data.name);
                }

                Debug.Log("解析本地对比文件完成");
                foreach (var fileName in remoteDataList.Keys)
                {
                    if (!clientDataList.ContainsKey(fileName))
                    {
                        assetDataList.Add(fileName);
                    }
                    else
                    {
                        if (clientDataList[fileName] != remoteDataList[fileName])
                        {
                            assetDataList.Add(fileName);
                        }

                        clientDataList.Remove(fileName);
                    }
                }

                Debug.Log("删除无用的AB包文件");
                var files = clientDataList.Keys.Where(file => File.Exists(GlobalSetting.GetPersistentPath(file)));
                foreach (var file in files)
                {
                    File.Delete(GlobalSetting.GetPersistentPath(file));
                }

                Debug.Log("下载和更新AB包文件");
                success = await GetAssetBundles();
                if (success)
                {
                    Debug.Log("更新本地AB包对比文件为最新");
                    await File.WriteAllTextAsync(GlobalSetting.clientInfoPath, remoteInfo);
                    return true;
                }
            }

            Debug.Log("更新失败。");
            return false;
        }

        /// <summary>
        /// 从服务器下载校对文件
        /// </summary>
        private static async Task<bool> GetRemoteInfo()
        {
            var reloads = 5;
            var success = false;
            while (!success && reloads-- > 0)
            {
                success = await GetRequest(GlobalSetting.clientInfoName, GlobalSetting.remoteInfoPath);
            }

            return success;
        }

        /// <summary>
        /// 下载待下载列表中的AB包文件
        /// </summary>
        private static async Task<bool> GetAssetBundles()
        {
            var reloads = 5;
            var curProgress = 0f;
            var maxProgress = assetDataList.Count;
            var copyList = assetDataList.ToList();
            while (assetDataList.Count > 0 && reloads-- > 0)
            {
                foreach (var fileName in copyList)
                {
                    var success = await GetRequest(fileName, GlobalSetting.GetPersistentPath(fileName), curProgress++, maxProgress);
                    if (!success) continue;
                    if (assetDataList.Contains(fileName))
                    {
                        assetDataList.Remove(fileName);
                    }
                }
            }

            return assetDataList.Count == 0;
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="localPath"></param>
        /// <param name="curProgress"></param>
        /// <param name="maxProgress"></param>
        /// <returns></returns>
        private static async Task<bool> GetRequest(string fileName, string localPath, float curProgress = 0, float maxProgress = 0)
        {
            var fileUri = GlobalSetting.GetRemoteFilePath(fileName);
            using (var request = UnityWebRequest.Head(fileUri))
            {
                request.timeout = 1;
                var result = request.SendWebRequest();
                while (!result.isDone && GlobalManager.Runtime)
                {
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"下载 {fileName} 文件失败\n");
                    return false;
                }
            }

            using (var request = UnityWebRequest.Get(fileUri))
            {
                var result = request.SendWebRequest();
                while (!result.isDone && GlobalManager.Runtime)
                {
                    if (maxProgress != 0)
                    {
                        var value = curProgress / maxProgress + request.downloadProgress / maxProgress;
                        OnLoadProgress?.Invoke(fileName, Math.Clamp(value, 0f, 1f));
                    }

                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"下载 {fileName} 文件失败\n");
                    return false;
                }

                await File.WriteAllBytesAsync(localPath, request.downloadHandler.data);
                Debug.Log($"下载 {fileName} 文件成功");
            }

            return true;
        }
    }
}