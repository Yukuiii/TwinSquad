using UnityEngine;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 让 Sprite 永远面向相机的简单 Billboard。
    /// 适用于 2.5D Billboard Sprite 风格（Don't Starve / Octopath Traveler 等）。
    ///
    /// 模式：
    /// - freezeTilt = true（默认）：仅绕 Y 轴旋转，sprite 始终竖直，俯视时自然产生压缩感
    /// - freezeTilt = false：完全朝向相机，sprite 永不变形（适合子弹、特效、UI 飘字）
    /// </summary>
    [DisallowMultipleComponent]
    public class Billboard : MonoBehaviour
    {
        [SerializeField] private bool freezeTilt = true;

        private Transform _cam;

        private void OnEnable()
        {
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
        }

        private void LateUpdate()
        {
            if (_cam == null)
            {
                if (Camera.main == null) return;
                _cam = Camera.main.transform;
            }

            if (freezeTilt)
            {
                // 只绕 Y 轴：sprite 直立，永远面对相机水平方向
                var dir = _cam.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) return;
                transform.rotation = Quaternion.LookRotation(-dir);
            }
            else
            {
                // 完全朝相机
                transform.rotation = Quaternion.LookRotation(transform.position - _cam.position);
            }
        }
    }
}
