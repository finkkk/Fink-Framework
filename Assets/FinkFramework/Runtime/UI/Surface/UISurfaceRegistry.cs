using System.Collections.Generic;
using UnityEngine;

namespace FinkFramework.Runtime.UI.Surface
{
    /// <summary>集中维护当前可用的 UI Surface，不参与面板导航和加载。</summary>
    internal sealed class UISurfaceRegistry
    {
        private readonly Dictionary<UISurfaceId, UISurface> surfaces = new();

        public bool TryGet(UISurfaceId id, out UISurface surface)
        {
            if (!surfaces.TryGetValue(id, out surface))
                return false;

            if (surface.IsValid)
                return true;

            surfaces.Remove(id);
            surface = null;
            return false;
        }

        public bool Register(UISurface surface, bool replace)
        {
            if (surface == null || !surface.IsValid)
                return false;

            if (surfaces.ContainsKey(surface.Id) && !replace)
                return false;

            surfaces[surface.Id] = surface;
            return true;
        }

        public bool Remove(UISurfaceId id) => surfaces.Remove(id);

        public bool IsOwnedBy(UISurfaceId id, Canvas owner) =>
            surfaces.TryGetValue(id, out UISurface surface) && surface.Canvas == owner;

        public bool Remove(UISurfaceId id, Canvas owner)
        {
            if (!surfaces.TryGetValue(id, out UISurface surface) || surface.Canvas != owner)
                return false;

            return surfaces.Remove(id);
        }

        public List<UISurface> Snapshot() => new(surfaces.Values);
    }
}
