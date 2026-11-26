using Core;
using Cysharp.Threading.Tasks;
using FairyGUI;
using Tools;
using UI;
using UnityEngine;

namespace XingShuRuntime.Social
{
    internal static class NativePraiseAssets
    {
        private static NTexture _atlas, _blue, _color;
        private static float _retryAt;
        private static int _generation;

        internal static NTexture Get(bool selected)
        {
            if (_atlas != null && !_atlas.disposed)
                return selected ? _color : _blue;

            if (Time.unscaledTime < _retryAt || !SimpleSingletonProvider<TextureManager>.hasInstance)
                return null;

            _retryAt = Time.unscaledTime + 5f;
            int generation = _generation;
            SimpleSingletonProvider<TextureManager>.inst.AsyncLoad("Common_Internal_atlas0", atlas =>
            {
                if (generation != _generation || atlas == null || atlas.disposed
                    || atlas.width != 1920 || atlas.height != 768 || _atlas == atlas) return;
                DisposeRegions();
                _atlas = atlas;
                _blue = new NTexture(atlas, new Rect(475, 530, 150, 150), false);
                _color = new NTexture(atlas, new Rect(475, 371, 150, 150), false);
            }, null).Forget();
            return null;
        }

        internal static void Reset()
        {
            _generation++;
            _retryAt = 0f;
            DisposeRegions();
        }

        private static void DisposeRegions()
        {
            if (_blue != null && !_blue.disposed) _blue.Dispose();
            if (_color != null && !_color.disposed) _color.Dispose();
            // The game owns the loaded atlas.
            _blue = _color = _atlas = null;
        }
    }
}
