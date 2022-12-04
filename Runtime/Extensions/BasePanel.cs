using System;
using UnityEngine;

namespace JFramework.Basic
{
    public abstract class BasePanel : MonoBehaviour,IPanel
    {
        public string path;
        
        protected virtual void Awake() => MonoManager.Instance.AddListener(OnUpdate);

        [Obsolete("[JFramework] 请使用OnUpdate来代替Update管理生命周期", true)]
        protected void Update() { }

        protected virtual void OnDestroy() => MonoManager.Instance.RemoveListener(OnUpdate);
        
        protected abstract void OnUpdate();

        public abstract void Show();

        public abstract void Hide();
    }
}