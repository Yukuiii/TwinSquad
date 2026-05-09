using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using TwinSquad.Data;
using TwinSquad.Framework;

namespace TwinSquad.Managers
{
    /// <summary>
    /// 本地存档管理器（单机版）。
    ///
    /// 核心特性：
    /// - 序列化：JsonUtility（零依赖）
    /// - 完整性：SHA256 + 盐 校验，防小白玩家直接改 JSON
    /// - 安全写入：write .tmp → backup → atomic rename，断电不丢档
    /// - 自动保存：MarkDirty + 定时 + Quit/Pause 强制
    /// - 版本兼容：通过 SaveMigration 处理旧存档
    ///
    /// 不做的事（YAGNI）：
    /// - 加密：上线前可在 EncryptPayload/DecryptPayload 处接入 AES
    /// - 异步：存档体积小（&lt;1MB），同步 IO 几毫秒
    /// - 双槽位：单文件 + .bak 已足够防护
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        // ===== 文件名 =====
        private const string SaveFileName = "save.json";
        private const string BackupFileName = "save.bak";
        private const string TempFileName = "save.tmp";

        // 校验盐：仅用于阻止小白玩家篡改，不是真正的加密密钥
        private const string ChecksumSalt = "TwinSquad_2026_v1";

        // 自动保存间隔（秒）
        [SerializeField] private float autoSaveInterval = 30f;

        // ===== 状态 =====
        public PlayerSaveData Current { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool IsDirty { get; private set; }
        public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);
        private string TempPath => Path.Combine(Application.persistentDataPath, TempFileName);

        private float _autoSaveTimer;

        // ===== 生命周期 =====
        public void Init()
        {
            Load();
            IsLoaded = true;
            EventBus.Publish(new SaveLoadedEvent { IsNewSave = Current.saveTimeUnix == 0 });
            Debug.Log($"[SaveManager] 存档路径：{SavePath}");
        }

        private void Update()
        {
            if (!IsLoaded || !IsDirty) return;
            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                Save();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && IsDirty) Save();
        }

        private void OnApplicationQuit()
        {
            if (IsDirty) Save();
        }

        // ===== 公共 API =====

        /// <summary>立即保存到磁盘。</summary>
        public bool Save()
        {
            if (Current == null)
            {
                Debug.LogWarning("[SaveManager] Current 为空，跳过保存");
                return false;
            }

            try
            {
                Current.saveTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                WriteToFile(Current);
                IsDirty = false;
                _autoSaveTimer = 0f;
                EventBus.Publish(new GameSavedEvent());
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 保存失败：{e}");
                EventBus.Publish(new SaveFailedEvent { Reason = e.Message });
                return false;
            }
        }

        /// <summary>标记数据已变更，定时器到点会自动保存。</summary>
        public void MarkDirty() => IsDirty = true;

        /// <summary>删除存档（重新开始游戏）。</summary>
        public bool DeleteSave()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                if (File.Exists(TempPath)) File.Delete(TempPath);

                Current = CreateNew();
                IsDirty = false;
                EventBus.Publish(new SaveDeletedEvent());
                Debug.Log("[SaveManager] 存档已删除并重置");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 删除存档失败：{e}");
                return false;
            }
        }

        /// <summary>强制重新加载（调试用）。</summary>
        public void Reload()
        {
            Load();
            EventBus.Publish(new SaveLoadedEvent { IsNewSave = false });
        }

        // ===== 内部：加载 =====

        private void Load()
        {
            // 1. 主文件
            if (File.Exists(SavePath) && TryReadFromFile(SavePath, out var data))
            {
                Current = SaveMigration.Migrate(data);
                Debug.Log("[SaveManager] 主存档加载成功");
                return;
            }

            // 2. 备份文件
            if (File.Exists(BackupPath))
            {
                Debug.LogWarning("[SaveManager] 主存档损坏，尝试备份...");
                if (TryReadFromFile(BackupPath, out var backupData))
                {
                    Current = SaveMigration.Migrate(backupData);
                    Debug.LogWarning("[SaveManager] 备份存档加载成功");
                    return;
                }
            }

            // 3. 全部失败 → 创建新存档
            Current = CreateNew();
            Debug.Log("[SaveManager] 未找到存档（或全部损坏），创建新存档");
        }

        private static PlayerSaveData CreateNew()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return new PlayerSaveData
            {
                saveVersion = SaveVersion.Current,
                createdTimeUnix = now,
                saveTimeUnix = 0,    // 0 表示从未保存过
            };
        }

        // ===== 内部：写入 / 读取 =====

        private void WriteToFile(PlayerSaveData data)
        {
            // 序列化 payload
            var payload = JsonUtility.ToJson(data, prettyPrint: false);
            var checksum = ComputeChecksum(payload);

            var wrapper = new SaveFileWrapper
            {
                payload = EncryptPayload(payload),
                checksum = checksum,
            };
            var fileText = JsonUtility.ToJson(wrapper, prettyPrint: false);

            // 1. 写到临时文件
            File.WriteAllText(TempPath, fileText, Encoding.UTF8);

            // 2. 备份当前主文件
            if (File.Exists(SavePath))
            {
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                File.Move(SavePath, BackupPath);
            }

            // 3. 临时文件晋升为主文件（原子 rename）
            if (File.Exists(SavePath)) File.Delete(SavePath); // 防御性
            File.Move(TempPath, SavePath);
        }

        private bool TryReadFromFile(string path, out PlayerSaveData data)
        {
            data = null;
            try
            {
                var fileText = File.ReadAllText(path, Encoding.UTF8);
                var wrapper = JsonUtility.FromJson<SaveFileWrapper>(fileText);
                if (wrapper == null || string.IsNullOrEmpty(wrapper.payload))
                {
                    Debug.LogError($"[SaveManager] {path} 内容为空或格式错误");
                    return false;
                }

                var payload = DecryptPayload(wrapper.payload);
                var actualChecksum = ComputeChecksum(payload);
                if (!string.Equals(actualChecksum, wrapper.checksum, StringComparison.Ordinal))
                {
                    Debug.LogError($"[SaveManager] {path} 校验失败（数据被篡改或损坏）");
                    return false;
                }

                data = JsonUtility.FromJson<PlayerSaveData>(payload);
                return data != null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] 读取 {path} 异常：{e}");
                return false;
            }
        }

        // ===== 加密占位（上线前替换为 AES） =====
        private static string EncryptPayload(string plain) => plain;
        private static string DecryptPayload(string cipher) => cipher;

        // ===== 校验和 =====
        private static string ComputeChecksum(string content)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content + ChecksumSalt));
            return Convert.ToBase64String(bytes);
        }

        // ===== 文件包装 =====
        [Serializable]
        private class SaveFileWrapper
        {
            public string payload;
            public string checksum;
        }
    }

    // ===== 存档相关全局事件 =====
    public struct SaveLoadedEvent { public bool IsNewSave; }
    public struct GameSavedEvent { }
    public struct SaveFailedEvent { public string Reason; }
    public struct SaveDeletedEvent { }
}
