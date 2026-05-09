using System;
using UnityEngine;
using TwinSquad.Configs;
using TwinSquad.Framework;

namespace TwinSquad.Managers
{
    /// <summary>
    /// 配表管理器。
    ///
    /// 工作流：
    ///   开发者编辑 Resources/Configs/*.json
    ///                              ↓
    ///                  ConfigManager.Init() 加载
    ///                              ↓
    ///                  业务侧：ConfigManager.Tables.TbItem.Get(1001)
    ///
    /// JSON 格式约定：
    ///   推荐：顶层数组         [ {...}, {...} ]
    ///   也支持：包装对象        { "dataList": [ {...} ] }
    ///   ConfigManager 会自动把顶层数组包装成 {"dataList":[...]}，让 JsonUtility 能解析。
    ///
    /// 设计原则：
    /// - 静态访问点 Tables，业务零样板代码
    /// - JSON 放 Resources/Configs/，跨平台零适配
    /// - 启动时一次性加载（配表通常 &lt;10MB，启动几十毫秒）
    /// </summary>
    public class ConfigManager : MonoBehaviour
    {
        public static Tables Tables { get; private set; }
        public bool IsLoaded { get; private set; }

        private const string ResourcesRoot = "Configs";

        public void Init()
        {
            try
            {
                Tables = new Tables(LoadJsonByName);
                IsLoaded = true;
                Debug.Log($"[ConfigManager] 配表加载完成（Item 数量：{Tables.TbItem.Count}）");
                EventBus.Publish(new ConfigsLoadedEvent());
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigManager] 配表加载失败：{e}");
                EventBus.Publish(new ConfigsLoadFailedEvent { Reason = e.Message });
                Tables = new Tables(_ => "{\"dataList\":[]}");
                IsLoaded = false;
            }
        }

        /// <summary>重新加载配表（仅 Editor / Debug 用）。</summary>
        public void Reload()
        {
            Resources.UnloadUnusedAssets();
            Init();
        }

        private static string LoadJsonByName(string tableName)
        {
            var path = $"{ResourcesRoot}/{tableName}";
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogError($"[ConfigManager] 找不到配表：Resources/{path}.json");
                return "{\"dataList\":[]}";
            }
            return WrapIfTopLevelArray(asset.text);
        }

        /// <summary>
        /// JsonUtility 不支持反序列化顶层数组。
        /// 若 JSON 形如 [{...},{...}]，自动包装成 {"dataList":[...]} 后返回；已包装格式原样返回。
        /// </summary>
        private static string WrapIfTopLevelArray(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "{\"dataList\":[]}";
            var trimmed = raw.TrimStart();
            if (trimmed.StartsWith("["))
                return "{\"dataList\":" + raw + "}";
            return raw;
        }
    }

    // ===== 配表相关全局事件 =====
    public struct ConfigsLoadedEvent { }
    public struct ConfigsLoadFailedEvent { public string Reason; }
}
