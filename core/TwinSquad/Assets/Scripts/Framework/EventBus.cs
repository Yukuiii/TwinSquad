using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwinSquad.Framework
{
    /// <summary>
    /// 类型安全的全局事件总线。
    /// 一个事件 = 一个数据类型（class 或 struct），避免字符串拼写错误。
    ///
    /// 用法：
    ///     EventBus.Subscribe&lt;LevelUpEvent&gt;(OnLevelUp);
    ///     EventBus.Publish(new LevelUpEvent { Level = 10 });
    ///     EventBus.Unsubscribe&lt;LevelUpEvent&gt;(OnLevelUp);
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new();

        // 订阅事件
        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            var type = typeof(T);
            _handlers.TryGetValue(type, out var existing);
            _handlers[type] = Delegate.Combine(existing, handler);
        }

        // 取消订阅
        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var existing)) return;

            var combined = Delegate.Remove(existing, handler);
            if (combined == null) _handlers.Remove(type);
            else _handlers[type] = combined;
        }

        // 发布事件（任何订阅者抛异常都不会影响其他订阅者）
        public static void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var existing)) return;
            foreach (var d in existing.GetInvocationList())
            {
                try { ((Action<T>)d).Invoke(evt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        // 清除某一类事件的所有订阅
        public static void Clear<T>() => _handlers.Remove(typeof(T));

        // 清除全部订阅（应用退出 / 场景重置时调用）
        public static void ClearAll() => _handlers.Clear();

        // 调试：当前订阅事件类型数（仅 Editor 可用）
        public static int HandlerTypeCount => _handlers.Count;
    }
}
