using UnityEngine;

namespace JiangJian
{
    /// <summary>
    /// MonoBehaviour 单例基类（不自动创建 GameObject）。
    /// 与 <see cref="SingletonAutoMono{T}"/> 的区别：本类要求子类组件必须已存在于场景中
    /// （手动放置或通过代码挂载到指定对象上），不会在访问 <see cref="Instance"/> 时新建 GameObject。
    /// 适用于：场景中已预设的 Manager、UI 控制器、需要序列化字段配置的组件等。
    /// </summary>
    public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        // 静态字段缓存单例实例。Unity 主线程访问，无需加锁。
        private static T Instance;

        /// <summary>
        /// 子类可重写以执行自定义初始化逻辑；若重写务必调用 <c>base.Awake()</c>。
        /// </summary>
        protected virtual void Awake()
        {
            if (Instance!= null && Instance!= this)
            {
                // 场景中已存在其他单例，销毁重复的组件实例（不连带销毁 GameObject，避免误伤其他组件）
                Debug.LogWarning($"[{typeof(T).Name}] 检测到重复实例，销毁当前组件：{name}");
                Destroy(this);
                return;
            }
            Instance = this as T;
        }

        /// <summary>
        /// 子类销毁时清理静态引用，避免下次访问时返回"已销毁但 C# 引用不为 null"的对象。
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (Instance== this)
            {
                Instance = null;
            }
        }
    }
}
