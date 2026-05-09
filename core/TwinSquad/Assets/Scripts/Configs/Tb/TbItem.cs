using System.Collections.Generic;
using TwinSquad.Configs.Cfg;
using UnityEngine;

namespace TwinSquad.Configs.Tb
{
    /// <summary>
    /// 物品表（item.json）。提供查询接口：
    ///   - Get(id)           按主键查询
    ///   - TryGet(id, out)   尝试查询
    ///   - DataList          全表只读列表
    ///   - DataMap           主键 → 配置 只读字典
    /// </summary>
    public class TbItem
    {
        private readonly Dictionary<int, ItemConfig> _data = new();
        private readonly List<ItemConfig> _dataList = new();

        public TbItem(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[TbItem] 加载 JSON 为空");
                return;
            }

            // JSON 文件本身是顶层数组；ConfigManager 加载时会自动包装为 {"dataList":[...]}。
            var wrapper = JsonUtility.FromJson<ItemConfigList>(json);
            if (wrapper?.dataList == null)
            {
                Debug.LogError("[TbItem] JSON 解析失败");
                return;
            }

            foreach (var cfg in wrapper.dataList)
            {
                if (cfg == null) continue;
                if (_data.ContainsKey(cfg.id))
                {
                    Debug.LogError($"[TbItem] 主键冲突：id={cfg.id}");
                    continue;
                }
                _data[cfg.id] = cfg;
                _dataList.Add(cfg);
            }
        }

        public ItemConfig Get(int id) => _data.TryGetValue(id, out var v) ? v : null;
        public bool TryGet(int id, out ItemConfig v) => _data.TryGetValue(id, out v);
        public bool Contains(int id) => _data.ContainsKey(id);
        public int Count => _data.Count;
        public IReadOnlyList<ItemConfig> DataList => _dataList;
        public IReadOnlyDictionary<int, ItemConfig> DataMap => _data;
    }

    [System.Serializable]
    internal class ItemConfigList
    {
        public List<ItemConfig> dataList;
    }
}
