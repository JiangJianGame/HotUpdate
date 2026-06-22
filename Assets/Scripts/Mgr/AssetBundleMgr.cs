using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace JiangJian
{
    /// <summary>
    /// AssetBundle 加载管理器（单例）。
    /// 负责加载主包、解析依赖、按需加载各业务 AB 包，并对外提供同步/异步加载接口。
    /// 加载路径优先 <see cref="Application.persistentDataPath"/>（热更目录），
    /// 不存在时回退到 <see cref="Application.streamingAssetsPath"/>（包体内置）。
    /// </summary>
    /// <remarks>
    /// <para>使用流程：</para>
    /// <list type="number">
    ///   <item>通过 <c>AssetBundleMgr.Instance</c> 获取单例</item>
    ///   <item>调用 <see cref="LoadRes{T}"/> 或 <see cref="LoadResAsync{T}"/> 加载资源</item>
    ///   <item>切换场景或退出游戏时调用 <see cref="ClearBundles"/> 释放</item>
    /// </list>
    /// </remarks>
    public class AssetBundleMgr : SingletonAutoMono<AssetBundleMgr>
    {
        // ==================== 字段 ====================

        // 主包（包含所有依赖关系的"主清单"包）
        private AssetBundle _mainBundle;
        // 主包内嵌的依赖清单
        private AssetBundleManifest _manifest;
        // 已加载的 AB 包缓存，避免重复加载（AB 包不能重复加载，会报错）
        private readonly Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>();

        // Unity 打包时约定的 manifest 资源固定名称
        private const string MANIFEST_ASSET_NAME = "AssetBundleManifest";

        // ==================== 路径与名称 ====================

        /// <summary>
        /// 包体内置 AB 目录前缀
        /// </summary>
        private string StreamingPathPrefix => Application.streamingAssetsPath + "/";

        /// <summary>
        /// 热更 AB 目录前缀
        /// </summary>
        private string PersistentPathPrefix => Application.persistentDataPath + "/";

        // ==================== 核心加载逻辑（内部） ====================

        /// <summary>
        /// 加载主包及其内嵌的 manifest。仅在第一次调用时真正加载。
        /// </summary>
        private void LoadMainBundle()
        {
            if (_mainBundle != null) return;

            // 主包名按平台区分，匹配 BuildPipeline 打包时的输出目录
            string mainBundleName =
#if UNITY_IOS
                "IOS";
#elif UNITY_ANDROID
                "Android";
#elif UNITY_WEBGL
                "WebGL";
#else
                "PC";
#endif

            string path = GetBundlePath(mainBundleName);
            _mainBundle = AssetBundle.LoadFromFile(path);
            if (_mainBundle == null)
            {
                Debug.LogError($"[AssetBundleMgr] 主包加载失败：{path}");
                return;
            }

            _manifest = _mainBundle.LoadAsset<AssetBundleManifest>(MANIFEST_ASSET_NAME);
            if (_manifest == null)
            {
                Debug.LogError($"[AssetBundleMgr] 主包内未找到 {MANIFEST_ASSET_NAME} 资源");
            }
        }

        /// <summary>
        /// 解析并加载指定 AB 包的全部依赖包。已加载的会自动跳过。
        /// </summary>
        private void LoadDependencies(string bundleName)
        {
            LoadMainBundle();
            if (_manifest == null) return;

            string[] dependencies = _manifest.GetAllDependencies(bundleName);
            foreach (var dep in dependencies)
            {
                LoadBundleIfNotLoaded(dep);
            }
        }

        /// <summary>
        /// 若指定 AB 包尚未加载，则加载并加入缓存。
        /// </summary>
        private void LoadBundleIfNotLoaded(string bundleName)
        {
            if (_loadedBundles.ContainsKey(bundleName)) return;

            string path = GetBundlePath(bundleName);
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Debug.LogError($"[AssetBundleMgr] AB 包加载失败：{path}");
                return;
            }
            _loadedBundles.Add(bundleName, bundle);
        }

        /// <summary>
        /// 获取 AB 包的可加载路径：优先热更目录，缺失则用包体内置。
        /// </summary>
        private string GetBundlePath(string bundleName)
        {
            string persistentPath = PersistentPathPrefix + bundleName;
            return File.Exists(persistentPath) ? persistentPath : StreamingPathPrefix + bundleName;
        }

        /// <summary>
        /// 资源若是预制体（GameObject）则自动实例化；否则原样返回。
        /// </summary>
        private Object InstantiateIfGameObject(Object asset)
        {
            return asset is GameObject prefab ? Instantiate(prefab) : asset;
        }

        // ==================== 公共 API：同步加载 ====================

        /// <summary>
        /// 同步加载 AB 包内的资源（泛型版本，类型由 <typeparamref name="T"/> 指定）。
        /// </summary>
        /// <typeparam name="T">资源类型，约束为 <see cref="Object"/>。</typeparam>
        /// <param name="bundleName">AB 包名</param>
        /// <param name="resourceName">包内资源名</param>
        /// <returns>若资源是预制体则返回实例化后的对象，否则返回原资源；找不到时返回 <c>null</c>。</returns>
        public T LoadRes<T>(string bundleName, string resourceName) where T : Object
        {
            return LoadRes(bundleName, resourceName, typeof(T)) as T;
        }

        /// <summary>
        /// 同步加载 AB 包内的资源（按 <see cref="System.Type"/> 指定类型）。
        /// </summary>
        public Object LoadRes(string bundleName, string resourceName, System.Type type)
        {
            LoadDependencies(bundleName);
            LoadBundleIfNotLoaded(bundleName);

            if (!_loadedBundles.TryGetValue(bundleName, out var bundle)) return null;

            Object asset = bundle.LoadAsset(resourceName, type);
            return InstantiateIfGameObject(asset);
        }

        /// <summary>
        /// 同步加载 AB 包内的资源（按名称，返回 <see cref="Object"/>）。
        /// </summary>
        public Object LoadRes(string bundleName, string resourceName)
        {
            return LoadRes(bundleName, resourceName, typeof(Object));
        }

        // ==================== 公共 API：异步加载 ====================

        /// <summary>
        /// 异步加载 AB 包内的资源（泛型版本，类型由 <typeparamref name="T"/> 指定）。
        /// </summary>
        /// <remarks>
        /// 加载完成后会通过 <paramref name="callback"/> 回调。
        /// 若加载失败（包/资源不存在），回调收到的对象为 <c>null</c>。
        /// </remarks>
        public void LoadResAsync<T>(string bundleName, string resourceName, UnityAction<T> callback) where T : Object
        {
            StartCoroutine(LoadResAsyncCoroutine(bundleName, resourceName, typeof(T),
                asset => callback?.Invoke(asset as T)));
        }

        /// <summary>
        /// 异步加载 AB 包内的资源（按 <see cref="System.Type"/> 指定类型）。
        /// </summary>
        public void LoadResAsync(string bundleName, string resourceName, System.Type type, UnityAction<Object> callback)
        {
            StartCoroutine(LoadResAsyncCoroutine(bundleName, resourceName, type, callback));
        }

        /// <summary>
        /// 异步加载 AB 包内的资源（按名称）。
        /// </summary>
        public void LoadResAsync(string bundleName, string resourceName, UnityAction<Object> callback)
        {
            StartCoroutine(LoadResAsyncCoroutine(bundleName, resourceName, typeof(Object), callback));
        }

        /// <summary>
        /// 异步加载的协程实现（公共内部）。三个公共 Async 重载最终都走这里。
        /// </summary>
        private IEnumerator LoadResAsyncCoroutine(string bundleName, string resourceName, System.Type type, UnityAction<Object> callback)
        {
            LoadDependencies(bundleName);
            LoadBundleIfNotLoaded(bundleName);

            if (!_loadedBundles.TryGetValue(bundleName, out var bundle))
            {
                callback?.Invoke(null);
                yield break;
            }

            AssetBundleRequest request = bundle.LoadAssetAsync(resourceName, type);
            yield return request;

            callback?.Invoke(InstantiateIfGameObject(request.asset));
        }

        // ==================== 公共 API：卸载 ====================

        /// <summary>
        /// 卸载指定的 AB 包（不会销毁已加载的资产实例，遵循 <see cref="AssetBundle.Unload(bool)"/> 语义）。
        /// </summary>
        public void UnloadBundle(string bundleName)
        {
            if (_loadedBundles.TryGetValue(bundleName, out var bundle))
            {
                bundle.Unload(false);
                _loadedBundles.Remove(bundleName);
            }
        }

        /// <summary>
        /// 清空所有已加载的 AB 包（包括主包和 manifest）。常用于场景切换或退出游戏时释放内存。
        /// </summary>
        public void ClearBundles()
        {
            AssetBundle.UnloadAllAssetBundles(false);
            _loadedBundles.Clear();
            _mainBundle = null;
            _manifest = null;
        }
    }
}
