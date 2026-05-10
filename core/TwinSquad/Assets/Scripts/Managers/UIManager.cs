using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TwinSquad.Managers
{
    /// <summary>
    /// UI 总管：分层渲染、栈式弹窗、统一打开/关闭入口。
    /// 简化原则（KISS / YAGNI）：
    ///   - 调用方提供 prefab，避免提前依赖 Resources / Addressables
    ///   - 同类型 Panel 只允许一个实例（重复 Open 会复用并置顶）
    ///   - 仅 Popup 层使用栈管理；HUD / Normal / Loading 直接 add/remove
    /// 后续接入 Addressables 后追加 Open&lt;T&gt;(string addressableKey) 重载。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private Transform hudLayer;
        [SerializeField] private Transform normalLayer;
        [SerializeField] private Transform popupLayer;
        [SerializeField] private Transform loadingLayer;

        private readonly Dictionary<Type, UIPanel> _opened = new();
        private readonly Stack<UIPanel> _popupStack = new();

        private void Awake()
        {
            if (rootCanvas == null) BuildDefaultCanvas();
            EnsureEventSystem();
        }

        // ===== 公共 API =====

        public T Open<T>(GameObject prefab) where T : UIPanel
        {
            var type = typeof(T);

            // 已打开则置顶并返回
            if (_opened.TryGetValue(type, out var exist))
            {
                exist.transform.SetAsLastSibling();
                return exist as T;
            }

            if (prefab == null)
            {
                Debug.LogError($"[UIManager] Open<{type.Name}> 失败：prefab 为空");
                return null;
            }

            var instance = Instantiate(prefab);
            var panel = instance.GetComponent<T>();
            if (panel == null)
            {
                Debug.LogError($"[UIManager] prefab 上找不到组件 {type.Name}");
                Destroy(instance);
                return null;
            }

            var parent = GetLayerRoot(panel.Layer);
            instance.transform.SetParent(parent, false);
            instance.transform.SetAsLastSibling();

            _opened[type] = panel;
            if (panel.Layer == UILayer.Popup) _popupStack.Push(panel);

            try { panel.OnOpen(); }
            catch (Exception e) { Debug.LogException(e); }

            return panel;
        }

        public void Close<T>() where T : UIPanel
        {
            if (!_opened.TryGetValue(typeof(T), out var panel)) return;
            CloseInternal(panel);
        }

        public void CloseTopPopup()
        {
            while (_popupStack.Count > 0)
            {
                var top = _popupStack.Pop();
                if (top == null) continue;
                CloseInternal(top, popFromStack: false);
                return;
            }
        }

        public void CloseAll(UILayer layer)
        {
            var pending = new List<UIPanel>();
            foreach (var p in _opened.Values)
                if (p != null && p.Layer == layer) pending.Add(p);

            foreach (var p in pending) CloseInternal(p);

            if (layer == UILayer.Popup) _popupStack.Clear();
        }

        public bool IsOpen<T>() where T : UIPanel => _opened.ContainsKey(typeof(T));

        public T Get<T>() where T : UIPanel
            => _opened.TryGetValue(typeof(T), out var p) ? p as T : null;

        // ===== 内部 =====

        private void CloseInternal(UIPanel panel, bool popFromStack = true)
        {
            if (panel == null) return;

            try { panel.OnClose(); }
            catch (Exception e) { Debug.LogException(e); }

            _opened.Remove(panel.GetType());

            if (popFromStack && panel.Layer == UILayer.Popup)
                RebuildPopupStackExcluding(panel);

            Destroy(panel.gameObject);
        }

        private void RebuildPopupStackExcluding(UIPanel removed)
        {
            if (_popupStack.Count == 0) return;
            var arr = _popupStack.ToArray();   // Stack.ToArray 返回栈顶在前
            _popupStack.Clear();
            for (int i = arr.Length - 1; i >= 0; i--)
            {
                if (arr[i] != null && arr[i] != removed)
                    _popupStack.Push(arr[i]);
            }
        }

        private Transform GetLayerRoot(UILayer layer) => layer switch
        {
            UILayer.HUD => hudLayer,
            UILayer.Normal => normalLayer,
            UILayer.Popup => popupLayer,
            UILayer.Loading => loadingLayer,
            _ => normalLayer,
        };

        // ===== 默认 Canvas / Layer 自动构建（无需 Inspector 配置即可运行）=====

        private void BuildDefaultCanvas()
        {
            var canvasGo = new GameObject("UI_Root");
            canvasGo.transform.SetParent(transform, false);

            rootCanvas = canvasGo.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 0;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            hudLayer = CreateLayer("Layer_HUD", UILayer.HUD, canvasGo.transform);
            normalLayer = CreateLayer("Layer_Normal", UILayer.Normal, canvasGo.transform);
            popupLayer = CreateLayer("Layer_Popup", UILayer.Popup, canvasGo.transform);
            loadingLayer = CreateLayer("Layer_Loading", UILayer.Loading, canvasGo.transform);
        }

        private static Transform CreateLayer(string name, UILayer layer, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // 用子 Canvas 控制该层 sortingOrder，独立排序避免重建
            var sub = go.AddComponent<Canvas>();
            sub.overrideSorting = true;
            sub.sortingOrder = (int)layer;
            go.AddComponent<GraphicRaycaster>();
            return rt;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
