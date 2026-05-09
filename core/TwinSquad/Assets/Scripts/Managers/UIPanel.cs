using UnityEngine;

namespace TwinSquad.Managers
{
    /// <summary>
    /// UI 层级。数字越大渲染越靠前，弹窗永远盖在 Normal 之上。
    /// </summary>
    public enum UILayer
    {
        HUD = 0,        // 战斗 HUD、常驻顶栏
        Normal = 100,   // 主城、背包等主要界面
        Popup = 200,    // 弹窗（确认框、详情）
        Loading = 300,  // 加载、引导（最高优先级）
    }

    /// <summary>
    /// 所有 UI 面板的基类。子类继承后由 UIManager.Open/Close 管理生命周期。
    /// </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        // 该面板归属的层级
        public virtual UILayer Layer => UILayer.Normal;

        // 是否响应"返回"按键自动关闭（弹窗常用）
        public virtual bool CloseOnBack => Layer == UILayer.Popup;

        // 打开时回调（已加入到层级节点之后调用）
        public virtual void OnOpen() { }

        // 关闭时回调（销毁前调用）
        public virtual void OnClose() { }
    }
}
