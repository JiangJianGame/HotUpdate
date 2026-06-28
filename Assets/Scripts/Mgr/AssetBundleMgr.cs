using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace JiangJian
{
    /// <summary>
    /// AssetBundle 加载管理器（单例）。
    /// 路径策略：优先热更目录(persistentDataPath)，缺失则回退包体内置(streamingAssetsPath)。
    /// </summary>
    public class AssetBundleMgr : SingletonAutoMono<AssetBundleMgr>
    {
        // ==================== 字段 ====================

        private AssetBundle mainBundle;                                         // 主包，包含依赖清单
        private AssetBundleManifest manifest;                                   // 主包内的依赖清单
        private readonly Dictionary<string, AssetBundle> loadedBundles = new(); // 已加载的 AB 包缓存（不可重复加载）
        private readonly Dictionary<string, int> bundleRefCounts = new();       // 引用计数，归零时卸载

        private const string MANIFEST_ASSET_NAME = "AssetBundleManifest";       // manifest 固定资源名

        // ==================== 路径 ====================

        private string StreamingPathPrefix => Application.streamingAssetsPath + "/";  // 包体内置
        private string PersistentPathPrefix => Application.persistentDataPath + "/";  // 热更目录

        // ==================== 核心加载逻辑 ====================

        /// <summary>
        /// 获取 AB 包路径。不传参时返回当前平台主包路径。
        /// WebGL 无文件系统，始终走 streamingAssets；原生平台优先热更目录。
        /// </summary>
        private string GetBundlePath(string bundleName = null)
        {
            if (bundleName == null)
            {
                bundleName =
#if UNITY_IOS
                    "IOS";
#elif UNITY_ANDROID
                    "Android";
#elif UNITY_WEBGL
                    "WebGL";
#else
                    "PC";
#endif
            }

#if UNITY_WEBGL
            return StreamingPathPrefix + bundleName;
#else
            string persistentPath = PersistentPathPrefix + bundleName;
            return File.Exists(persistentPath) ? persistentPath : StreamingPathPrefix + bundleName;
#endif
        }

        // ---------- 引用计数 ----------

        /// <summary> 目标包及其依赖包引用计数各 +1 </summary>
        private void AddRefCounts(string bundleName)
        {
            TryIncrementRefCount(bundleName);

            if (manifest == null) return;
            foreach (var dep in manifest.GetAllDependencies(bundleName))
                TryIncrementRefCount(dep);
        }

        /// <summary> 单个包引用计数 +1 </summary>
        private void TryIncrementRefCount(string bundleName)
        {
            if (!bundleRefCounts.ContainsKey(bundleName))
                bundleRefCounts[bundleName] = 0;
            bundleRefCounts[bundleName]++;
        }

        /// <summary> 目标包及其依赖包引用计数各 -1，归零时卸载并移出缓存 </summary>
        private void DecRefCounts(string bundleName)
        {
            TryDecrementAndUnload(bundleName);

            if (manifest == null) return;
            foreach (var dep in manifest.GetAllDependencies(bundleName))
                TryDecrementAndUnload(dep);
        }

        /// <summary> 单个包引用计数 -1，归零时卸载并移出缓存 </summary>
        private void TryDecrementAndUnload(string bundleName)
        {
            if (!bundleRefCounts.ContainsKey(bundleName)) return;

            bundleRefCounts[bundleName]--;
            if (bundleRefCounts[bundleName] <= 0)
            {
                bundleRefCounts.Remove(bundleName);
                if (loadedBundles.TryGetValue(bundleName, out var bundle))
                {
                    bundle.Unload(false);
                    loadedBundles.Remove(bundleName);
                }
            }
        }

        // ---------- 异步加载（全平台） ----------

        /// <summary> 异步加载主包和 manifest，首次调用时生效 </summary>
        private IEnumerator LoadMainBundleAsync()
        {
            if (mainBundle != null) yield break;

            string path = GetBundlePath();
            string mainBundleName = Path.GetFileName(path);
            yield return LoadBundleAsyncInternal(path, mainBundleName, bundle => mainBundle = bundle);

            if (mainBundle == null) yield break;

            manifest = mainBundle.LoadAsset<AssetBundleManifest>(MANIFEST_ASSET_NAME);
            if (manifest == null)
                Debug.LogError($"[AssetBundleMgr] 主包内未找到 {MANIFEST_ASSET_NAME} 资源");
        }

        /// <summary> 异步加载依赖包 + 目标包 </summary>
        private IEnumerator LoadBundlesAsync(string bundleName)
        {
            yield return LoadMainBundleAsync();
            if (manifest == null) yield break;

            foreach (var dep in manifest.GetAllDependencies(bundleName))
                yield return LoadBundleIfNotLoadedAsync(dep);

            yield return LoadBundleIfNotLoadedAsync(bundleName);
        }

        /// <summary> 异步加载指定 AB 包，已缓存则跳过 </summary>
        private IEnumerator LoadBundleIfNotLoadedAsync(string bundleName)
        {
            if (loadedBundles.ContainsKey(bundleName)) yield break;

            string path = GetBundlePath(bundleName);
            yield return LoadBundleAsyncInternal(path, bundleName,
                bundle => loadedBundles.Add(bundleName, bundle));
        }

        /// <summary> AB 包异步加载底层实现：WebGL 走 UnityWebRequest，原生走 LoadFromFileAsync </summary>
        private IEnumerator LoadBundleAsyncInternal(string path, string bundleName, System.Action<AssetBundle> onLoaded)
        {
#if UNITY_WEBGL
            using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(path))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[AssetBundleMgr] AB 包加载失败：{bundleName} ({path})\n{request.error}");
                    yield break;
                }

                AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(request);
                if (bundle != null)
                    onLoaded?.Invoke(bundle);
                else
                    Debug.LogError($"[AssetBundleMgr] AB 包加载失败：{bundleName} ({path})");
            }
#else
            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);
            yield return request;

            if (request.assetBundle != null)
                onLoaded?.Invoke(request.assetBundle);
            else
                Debug.LogError($"[AssetBundleMgr] AB 包加载失败：{bundleName} ({path})");
#endif
        }

        // ---------- 工具方法 ----------

        /// <summary> 预制体自动实例化，其他类型原样返回 </summary>
        private Object InstantiateIfGameObject(Object asset)
        {
            return asset is GameObject prefab ? Instantiate(prefab) : asset;
        }

        // ==================== 公共 API：异步加载 ====================

        /// <summary>
        /// 异步加载资源。加载完成后回调，失败时回调参数为 null。
        /// </summary>
        public void LoadResAsync<T>(string bundleName, string resourceName, UnityAction<T> callback) where T : Object
        {
            StartCoroutine(LoadResAsyncCoroutine(bundleName, resourceName, typeof(T),
                asset => callback?.Invoke(asset as T)));
        }

        /// <summary> 异步加载协程：加载依赖包 → 加载目标包 → 增加引用计数 → 加载资源 → 回调 </summary>
        private IEnumerator LoadResAsyncCoroutine(string bundleName, string resourceName, System.Type type, UnityAction<Object> callback)
        {
            yield return LoadBundlesAsync(bundleName);
            AddRefCounts(bundleName);

            if (!loadedBundles.TryGetValue(bundleName, out var bundle))
            {
                callback?.Invoke(null);
                yield break;
            }

            AssetBundleRequest request = bundle.LoadAssetAsync(resourceName, type);
            yield return request;

            if (request.asset == null)
                Debug.LogError($"[AssetBundleMgr] 资源未找到：{bundleName}/{resourceName}");

            callback?.Invoke(InstantiateIfGameObject(request.asset));
        }

        // ==================== 公共 API：卸载 ====================

        /// <summary> 按引用计数卸载，归零时才真正释放。不销毁已实例化的对象。 </summary>
        public void UnloadBundle(string bundleName)
        {
            DecRefCounts(bundleName);
        }

        /// <summary> 强制清空所有 AB 包（含主包），无视引用计数。用于场景切换或退出。 </summary>
        public void ClearBundles()
        {
            foreach (var pair in loadedBundles)
                pair.Value.Unload(false);
            loadedBundles.Clear();
            bundleRefCounts.Clear();

            if (mainBundle != null)
            {
                mainBundle.Unload(false);
                mainBundle = null;
            }
            manifest = null;
        }
    }
}
