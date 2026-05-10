using UnityEngine;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 无限滚动地面：跟随玩家但位置量化到 tileSize 网格。
    ///
    /// 原理：
    /// - 地面 GameObject 每帧"瞄准"玩家位置，但实际位置量化到 tileSize 整数倍
    /// - 玩家在地面 sprite 内部相对移动 → 视觉上看起来"地面无限延伸"
    /// - 因为地砖无缝平铺，整体跳一格时肉眼无法察觉
    ///
    /// 前提：
    /// - 地砖图必须是 Seamless Tileable（左右/上下边缘能完美对接）
    /// - SpriteRenderer.drawMode = Tiled
    /// - SpriteRenderer.size 大于相机视野（建议 ≥ 视野直径 + 2*tileSize 余量）
    /// - tileSize 必须等于 sprite 的世界宽度（像素宽 / PPU）
    ///
    /// 注意：本组件只负责"视觉地面"。所有逻辑实体（玩家/敌人/子弹/掉落物）
    /// 都在真实世界坐标系，与本组件无关。
    /// </summary>
    public class InfiniteGround : MonoBehaviour
    {
        [SerializeField] private Transform follow;
        [SerializeField] private float tileSize = 4f;

        /// <summary>
        /// 绑定跟随目标。tileSize 自动从 SpriteRenderer.sprite.bounds 读取
        /// （= 单个 tile 的世界宽度，由像素宽 / PPU 决定）。
        /// </summary>
        public void Bind(Transform target)
        {
            follow = target;
            if (TryGetComponent<SpriteRenderer>(out var sr) && sr.sprite != null)
            {
                tileSize = sr.sprite.bounds.size.x;
            }
            if (tileSize < 0.01f) tileSize = 4f;  // 兜底
        }

        private void LateUpdate()
        {
            if (follow == null) return;
            var p = follow.position;
            transform.position = new Vector3(
                Mathf.Round(p.x / tileSize) * tileSize,
                Mathf.Round(p.y / tileSize) * tileSize,
                transform.position.z
            );
        }
    }
}
