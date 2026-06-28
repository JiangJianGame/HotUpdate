using UnityEngine;

namespace JiangJian
{
    /// <summary>
    /// 自动创建型 <see cref="MonoBehaviour"/> 单例基类。
    /// 子类继承后无需在场景中预先挂载，第一次访问 <see cref="Instance"/> 时
    /// 会自动创建一个 <c>GameObject</c> 并附加该子类组件。
    /// 该 <c>GameObject</c> 会被标记为 <see cref="Object.DontDestroyOnLoad(Object)"/>，
    /// 跨场景切换时不会被销毁，保证整个应用生命周期内单例唯一。
    /// </summary>
    /// <typeparam name="T">具体子类类型，约束为 <see cref="MonoBehaviour"/>。</typeparam>
    public class SingletonAutoMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        // 静态字段缓存单例实例。Unity 主线程访问，暂不额外加锁。
        private static T instance;

        /// <summary>
        /// 获取单例实例。若实例尚未创建，则自动生成。
        /// </summary>
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    // 1. 用子类类型名（如 "GameManager"）作为 GameObject 名，方便在 Hierarchy 中识别
                    GameObject singletonObj = new GameObject(typeof(T).ToString());
                    // 2. 挂载目标组件并缓存到 instance
                    instance = singletonObj.AddComponent<T>();
                    // 3. 标记为过场景不销毁，确保单例在场景切换后仍然有效
                    DontDestroyOnLoad(singletonObj);
                }
                return instance;
            }
        }
    }
}
