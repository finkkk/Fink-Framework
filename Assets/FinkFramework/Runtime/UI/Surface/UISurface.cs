using System.Collections.Generic;
using UnityEngine;

namespace FinkFramework.Runtime.UI.Surface
{
    /// <summary>一个可独立承载 UI 的屏幕、世界空间画布或手部菜单画布。</summary>
    internal sealed class UISurface
    {
        private readonly Dictionary<UILayer, Transform> layerRoots;

        public UISurfaceId Id { get; }
        public Canvas Canvas { get; }
        public UISurfaceLifetime Lifetime { get; }
        public int OwnerSceneHandle { get; }
        public string OwnerSceneName { get; }

        public bool IsValid => Canvas;
        public bool IsActive => Canvas && Canvas.isActiveAndEnabled;
        public bool IsWorldSpace => Canvas && Canvas.renderMode == RenderMode.WorldSpace;

        public UISurface(
            UISurfaceId id,
            Canvas canvas,
            Transform fallbackRoot,
            Dictionary<UILayer, Transform> layerRoots = null,
            UISurfaceLifetime lifetime = UISurfaceLifetime.Scene)
        {
            Id = id;
            Canvas = canvas;
            Lifetime = lifetime;
            OwnerSceneHandle = canvas ? canvas.gameObject.scene.handle : -1;
            OwnerSceneName = canvas ? canvas.gameObject.scene.name : string.Empty;
            Transform root = fallbackRoot ? fallbackRoot : canvas ? canvas.transform : null;
            this.layerRoots = layerRoots ?? BuildLayerRoots(root);
        }

        public Transform GetRoot(UILayer layer)
        {
            if (layerRoots.TryGetValue(layer, out Transform root) && root)
                return root;

            return Canvas ? Canvas.transform : null;
        }

        public static Dictionary<UILayer, Transform> BuildLayerRoots(Transform root)
        {
            var result = new Dictionary<UILayer, Transform>();
            foreach (UILayer layer in System.Enum.GetValues(typeof(UILayer)))
            {
                Transform child = root ? root.Find(layer.ToString()) : null;
                result[layer] = child ? child : root;
            }

            return result;
        }
    }
}
