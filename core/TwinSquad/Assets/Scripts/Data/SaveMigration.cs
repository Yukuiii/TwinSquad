using UnityEngine;

namespace TwinSquad.Data
{
    /// <summary>
    /// 存档版本迁移。
    /// 当 PlayerSaveData 结构变更（字段重命名 / 删除 / 数据重组）导致旧存档不兼容时，
    /// 在此添加 vN → vN+1 的迁移函数。
    ///
    /// 仅追加字段时无需迁移（JsonUtility 默认填充零值即可）。
    /// </summary>
    public static class SaveMigration
    {
        public static PlayerSaveData Migrate(PlayerSaveData data)
        {
            if (data == null) return null;

            int from = data.saveVersion;
            int to = SaveVersion.Current;

            if (from == to) return data;

            if (from > to)
            {
                Debug.LogError(
                    $"[SaveMigration] 存档版本 v{from} 高于当前游戏版本 v{to}，可能由更新版本生成。" +
                    "强制按当前版本加载（部分新字段可能丢失）。");
                data.saveVersion = to;
                return data;
            }

            Debug.Log($"[SaveMigration] 迁移存档：v{from} → v{to}");

            // 顺序应用每一版升级
            while (data.saveVersion < to)
            {
                switch (data.saveVersion)
                {
                    // case 1: MigrateV1ToV2(data); break;
                    // case 2: MigrateV2ToV3(data); break;
                    default:
                        Debug.LogWarning($"[SaveMigration] 没有 v{data.saveVersion} 的迁移函数，跳过");
                        data.saveVersion = to;
                        return data;
                }
                data.saveVersion++;
            }

            return data;
        }

        // ===== 迁移函数模板（破坏性修改时启用） =====
        //
        // private static void MigrateV1ToV2(PlayerSaveData data)
        // {
        //     // 例：把 playerInfo.nickname 从字段 A 迁移到字段 B
        //     // data.playerInfo.newField = data.playerInfo.oldField;
        //     // data.playerInfo.oldField = null;
        // }
    }
}
