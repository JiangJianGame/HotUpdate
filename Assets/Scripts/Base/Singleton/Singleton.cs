using System;

namespace JiangJian
{
    /// <summary>
    /// 纯 C# 单例基类（不继承 <see cref="UnityEngine.MonoBehaviour"/>）。
    /// 适用于：数据管理类、配置类、工具类、状态机、网络客户端等不需要挂载到 GameObject 的对象。
    /// </summary>
    public class Singleton<T> where T : class, new()
    {
        // Lazy<T> 自身实现线程安全，无需额外加锁
        private static readonly Lazy<T> lazyInstance = new Lazy<T>(() => new T());

        /// <summary>
        /// 获取单例实例。首次访问时创建，之后所有调用都返回同一实例。
        /// </summary>
        public static T Instance => lazyInstance.Value;
    }
}
