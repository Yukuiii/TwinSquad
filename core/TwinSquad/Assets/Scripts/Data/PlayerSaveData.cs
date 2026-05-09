using System;

namespace TwinSquad.Data
{
    /// <summary>
    /// 玩家整体存档（顶层）。
    /// 设计原则：集中式存档结构 —— 各业务模块在此添加字段，SaveManager 只负责整体读写。
    /// 后续模块（背包、角色、抽卡、关卡）数据按需追加为 [Serializable] 字段。
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        // ===== 元数据 =====
        public int saveVersion = SaveVersion.Current;       // 存档版本（用于迁移）
        public long saveTimeUnix;                           // 上次保存时间（Unix 秒）
        public long createdTimeUnix;                        // 首次创建时间
        public long playTimeSeconds;                        // 累计游戏时长

        // ===== 玩家基础数据 =====
        public PlayerInfoData playerInfo = new();

        // ===== 设置 =====
        public SettingsData settings = new();

        // ===== 各业务模块预留扩展位（按模块完成度逐步启用）=====
        // public InventorySaveData inventory = new();
        // public CharacterCollectionSaveData characters = new();
        // public GachaSaveData gacha = new();
        // public StageProgressSaveData stages = new();
    }

    /// <summary>
    /// 当前存档版本号。每次破坏性修改 PlayerSaveData 结构时 +1，
    /// 并在 SaveMigration 中编写对应迁移逻辑。
    /// </summary>
    public static class SaveVersion
    {
        public const int Current = 1;
    }

    [Serializable]
    public class PlayerInfoData
    {
        public string nickname = "Player";
        public int level = 1;
        public int exp = 0;
        public string avatarId = "default";
    }

    [Serializable]
    public class SettingsData
    {
        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
        public float voiceVolume = 1f;
        public string language = "zh-CN";
        public bool vibrationEnabled = true;
        public int graphicsQuality = 2;     // 0=低 1=中 2=高
        public int targetFps = 60;
    }
}
