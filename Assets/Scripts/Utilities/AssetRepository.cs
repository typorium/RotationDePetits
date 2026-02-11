using NSMB.Addons;
using Quantum;
using System.Collections.Generic;
using System.Linq;

namespace NSMB.Utilities {
    public static class AssetRepository<T> where T : AssetObject {

        static AssetRepository() {
            AddonManager.OnAddonLoaded += OnAddonListChanged;
            AddonManager.OnAddonUnloaded += OnAddonListChanged;
        }

        private static List<AssetRef<T>> _allAssetRefs;
        public static List<AssetRef<T>> AllAssetRefs {
            get {
                return _allAssetRefs ??=
                    QuantumUnityDB.Global.FindAssetGuids(new AssetObjectQuery { Type = typeof(T) })
                        .Select(ag => new AssetRef<T>(ag))
                        .ToList();
            }
        }

        private static List<T> _allAssets;
        public static IReadOnlyList<T> AllAssets {
            get {
                return _allAssets ??=
                    AllAssetRefs
                        .Select(ar => QuantumUnityDB.GetGlobalAsset(ar))
                        .ToList();
            }
        }

        public static void Invalidate() {
            _allAssetRefs = null;
            _allAssets = null;
        }

        private static void OnAddonListChanged(LoadedAddon _) {
            Invalidate();
        }
    }
}