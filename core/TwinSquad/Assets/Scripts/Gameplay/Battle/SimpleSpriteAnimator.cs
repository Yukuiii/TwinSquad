using System;
using UnityEngine;

namespace TwinSquad.Gameplay.Battle
{
    /// <summary>
    /// 极简逐帧 sprite 动画播放器。
    ///
    /// 用法：
    ///   var anim = go.AddComponent&lt;SimpleSpriteAnimator&gt;();
    ///   anim.Play(idleFrames, fps: 8, loop: true);
    ///
    /// 设计原则：
    /// - 不依赖 Animator / Mecanim，零状态机心智负担
    /// - OnEnable 自动重置，配合 ObjectPool 复用
    /// - Play() 可在运行时切换动画（idle → run → attack）
    /// - 一次性动画结束触发 onComplete 回调
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SimpleSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float fps = 8f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnEnable = true;

        public event Action OnComplete;

        private SpriteRenderer _sr;
        private float _timer;
        private int _index;
        private bool _finished;
        private bool _paused;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            Reset();
            if (playOnEnable) ApplyFrame(0);
        }

        public void Play(Sprite[] newFrames, float newFps = 8f, bool newLoop = true)
        {
            frames = newFrames;
            fps = Mathf.Max(0.01f, newFps);
            loop = newLoop;
            Reset();
            ApplyFrame(0);
        }

        public void SetFlipX(bool flip)
        {
            if (_sr != null) _sr.flipX = flip;
        }

        public void Pause()
        {
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
        }

        private void Reset()
        {
            _timer = 0f;
            _index = 0;
            _finished = false;
            _paused = false;
        }

        private void Update()
        {
            if (_paused || _finished || frames == null || frames.Length <= 1) return;

            _timer += Time.deltaTime;
            var step = 1f / fps;
            while (_timer >= step)
            {
                _timer -= step;
                _index++;
                if (_index >= frames.Length)
                {
                    if (loop)
                    {
                        _index = 0;
                    }
                    else
                    {
                        _index = frames.Length - 1;
                        _finished = true;
                        ApplyFrame(_index);
                        OnComplete?.Invoke();
                        return;
                    }
                }
                ApplyFrame(_index);
            }
        }

        private void ApplyFrame(int i)
        {
            if (_sr == null || frames == null || frames.Length == 0) return;
            i = Mathf.Clamp(i, 0, frames.Length - 1);
            _sr.sprite = frames[i];
        }
    }
}
