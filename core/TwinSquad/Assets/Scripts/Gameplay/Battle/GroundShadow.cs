using UnityEngine;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 脚底圆形阴影：增强角色"立在地面"的空间感。
    ///
    /// 设计：
    /// - 独立 GameObject（不是 follow 的子物体），避免被父级 Billboard / 旋转影响
    /// - LateUpdate 追随目标 xz，y 固定贴地（默认 0.02，防 Z-fighting）
    /// - 自身 rotation X=90°，sprite 平铺地面
    /// </summary>
    public class GroundShadow : MonoBehaviour
    {
        private Transform _follow;
        private float _groundY = 0.02f;

        public void Bind(Transform follow, float groundY = 0.02f)
        {
            _follow = follow;
            _groundY = groundY;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void LateUpdate()
        {
            if (_follow == null)
            {
                // 目标销毁后阴影自销毁，避免悬挂
                Destroy(gameObject);
                return;
            }
            var p = _follow.position;
            transform.position = new Vector3(p.x, _groundY, p.z);
        }
    }
}
