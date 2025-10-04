using Newtonsoft.Json;
using Quantum;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace NSMB.Addon {
    public class AddonManager : MonoBehaviour {

        //---Static Variables
        public static event Action<LoadedAddon> OnAddonLoaded, OnAddonUnloaded;

        private static readonly string RemoteRepoUrl = "https://raw.githubusercontent.com/ipodtouch0218/NSMB-MarioVsLuigi-AddonRepository/main/";
        private static readonly string RemoteAddonsFile = RemoteRepoUrl + "addons.json";
        private static string LocalFolderPath = Path.Combine(Application.dataPath, "addons");
        private static readonly string LocalFolderDownloadedPath = Path.Combine(LocalFolderPath, "download");

        //---Private Variables
        private List<LoadedAddon> loadedAddons = new();

        public async void Start() {
            var before = QuantumUnityDB.Global.FindAssetGuids(new AssetObjectQuery { Type = typeof(Map) });
            Debug.LogWarning("Maps before loading addon: " + before.Count);

            var mountain = await LoadAddonByName("Mountain-v1.1");
            
            var after = QuantumUnityDB.Global.FindAssetGuids(new AssetObjectQuery { Type = typeof(Map) });
            Debug.LogWarning("Maps after loading addon: " + after.Count);

            // await UnloadAddon(mountain);
            // var afterUnload = QuantumUnityDB.Global.FindAssetGuids(new AssetObjectQuery { Type = typeof(Map) });
            // Debug.LogWarning("Maps after unloading addon: " + afterUnload.Count);
        }

        public async Task<bool> LoadAddonList(List<string> requestedAddons) {
            // Unload *ALL* addons. this is important as the order MATTERS.
            foreach (var addon in loadedAddons.ToList()) {
                await UnloadAddon(addon);
            }

            // Load addons
            foreach (var addonKey in requestedAddons) {
                var loadedAddon = await LoadAddonByName(addonKey);
                if (loadedAddon == null) {
                    // Failed. Abort.
                    return false;
                }
            }

            // Good to go.
            return true;
        }

        public async Task<LoadedAddon> LoadAddonByName(string addonName) {
            // Primary: check through all addons.
            if (Directory.Exists(LocalFolderPath)) {
                // Check for extracted addons
                foreach (var path in Directory.EnumerateDirectories(LocalFolderPath, addonName, new EnumerationOptions { RecurseSubdirectories = true })) {
                    // This is a folder with the {addonName}. Check for addon.json.
                    if (File.Exists(Path.Combine(path, "addon.json"))) {
                        // We already have this addon locally. Cool beans.
                        Debug.Log($"[Addon] Loading addon \"{addonName}\" from local source ({path})");
                        return await LoadAddonByPath(path);
                    }
                }

                // Check for .zip files.
                foreach (var zippedPath in Directory.EnumerateFiles(LocalFolderPath, addonName + ".zip", new EnumerationOptions { RecurseSubdirectories = true })) {
                    bool valid = false;
                    using (var zipArchive = ZipFile.Open(zippedPath, ZipArchiveMode.Read)) {
                        valid = zipArchive.GetEntry("addon.json") != null;
                    }

                    if (valid) {
                        // We already have this addon within this zip file. Cool beans.
                        Debug.Log($"[Addon] Found zipped addon at {zippedPath}. Extracting...");
                        string path = Path.Combine(Path.GetDirectoryName(zippedPath), addonName);
                        ZipFile.ExtractToDirectory(zippedPath, path);

                        Debug.Log($"[Addon] Loading addon \"{addonName}\" from local source ({path})");
                        return await LoadAddonByPath(path);
                    }
                }
            }

            // Fallback: check the remote repository.
            string downloadedPath = await DownloadAddonByName(addonName);
            if (downloadedPath == null) {
                Debug.Log($"[Addon] Failed to automatically download addon \"{addonName}\"");
                return null;
            }
            // Success!
            return await LoadAddonByPath(downloadedPath);
        }

        public async Task<LoadedAddon> LoadAddonByPath(string pathToFolder) {
            // Addon Definition
            string addonDefJson = await File.ReadAllTextAsync(Path.Combine(pathToFolder, "addon.json"));
            AddonDefinition addonDef = JsonConvert.DeserializeObject<AddonDefinition>(addonDefJson);

            // Catalog
            string platformFolderName = GetFolderForPlatform();
            string platformFolderPath = Path.Combine(pathToFolder, platformFolderName);
            if (!Directory.Exists(platformFolderPath)) {
                Debug.LogError($"[Addon] Failed to load addon at path {pathToFolder}, it does not seem to support our platform ({platformFolderName})!");
                return null;
            }
            string catalogPath = Directory.GetFiles(platformFolderPath, "*.json").FirstOrDefault();
            if (catalogPath == null) {
                // No catalog?
                Debug.LogError($"[Addon] Failed to load addon at path {pathToFolder}, it does not seem to support our platform ({platformFolderName})!");
                return null;
            }

            // Resolve paths + create a copy of the catalog.
            string catalogAsString = await File.ReadAllTextAsync(catalogPath);
            catalogAsString = catalogAsString.Replace("{MOD_PATH}", platformFolderPath.Replace(@"\", @"\\")); // JSON expects double escaped backslashes

            string tempCatalogPath = Path.Combine(Application.temporaryCachePath, $"catalog_{addonDef.FullName}.json");
            await File.WriteAllTextAsync(tempCatalogPath, catalogAsString);

            // Read temp catalog
            var catalogHandle = Addressables.LoadContentCatalogAsync(tempCatalogPath);
            var resourceLocator = await catalogHandle.Task;

            if (catalogHandle.Status == AsyncOperationStatus.Succeeded) {
                var loadAssetObjectsHandle = Addressables.LoadAssetsAsync<AssetObject>(resourceLocator.Keys, _ => {}, Addressables.MergeMode.Union);
                var assetObjects = await loadAssetObjectsHandle.Task;

                if (loadAssetObjectsHandle.Status == AsyncOperationStatus.Succeeded) {
                    foreach (var assetObject in assetObjects) {
                        try {
                            QuantumUnityDB.Global.AddAsset(assetObject);
                            Debug.Log($"[Addon] Successfully registered asset {assetObject.name} ({assetObject.Guid})");
                        } catch {
                            // Already added? Doesn't matter... ignore.
                            Debug.Log($"[Addon] Failed to register asset {assetObject.name} ({assetObject.Guid})");
                        }
                    }
                }

                LoadedAddon newAddon = new LoadedAddon {
                    Definition = addonDef,
                    CatalogHandle = catalogHandle,
                    AllAssetObjectsHandle = loadAssetObjectsHandle,
                    FullName = Path.GetFileName(pathToFolder),
                };
                loadedAddons.Add(newAddon);
                OnAddonLoaded?.Invoke(newAddon);
                return newAddon;
            }

            Debug.LogError($"[Addon] Failed to load addon at path {pathToFolder}");
            return null;
        }

        public async Task<string> DownloadAddonByName(string addonName) {
            string targetFileUrl = CombineUrl(RemoteRepoUrl, addonName + ".zip");
            Debug.Log($"[Addon] Attempting to download addon {addonName} from remote source ({targetFileUrl})");

            using UnityWebRequest zippedAddonRequest = UnityWebRequest.Get(targetFileUrl);
            zippedAddonRequest.SetRequestHeader("Accept", "*/*");
            zippedAddonRequest.SetRequestHeader("UserAgent", "ipodtouch0218/NSMB-MarioVsLuigi");
            await zippedAddonRequest.SendWebRequest();
            if (zippedAddonRequest.result != UnityWebRequest.Result.Success) {
                Debug.Log($"[Addon] Download failed: {zippedAddonRequest.error} ({zippedAddonRequest.responseCode})");
                return null;
            }

            string finalPath = Path.Combine(LocalFolderDownloadedPath, addonName);
            string platformFolderName = GetFolderForPlatform();

            using MemoryStream memoryStream = new(zippedAddonRequest.downloadHandler.data);
            using ZipArchive zipFile = new(memoryStream);

            if (zipFile.GetEntry(platformFolderName + "/catalog.json") == null) {
                // Our platform doesn't support this mod... oh well
                Debug.Log($"[Addon] Download failed: {addonName} doesn't support our platform ({platformFolderName})");
                return null;
            }
            zipFile.ExtractToDirectory(finalPath);
            Debug.Log($"[Addon] Successfully downloaded addon to {finalPath}");
            
            return finalPath;
        }

        public async Task UnloadAddon(LoadedAddon addon) {
            if (addon == null) {
                throw new ArgumentNullException("Tried to unload a null addon!");
            }

            if (!addon.AllAssetObjectsHandle.IsDone) {
                await addon.AllAssetObjectsHandle.Task;
            }

            foreach (var assetObject in addon.AllAssetObjectsHandle.Result) {
                QuantumUnityDB.Global.DisposeAsset(assetObject.Guid, true);
                QuantumUnityDB.Global.RemoveSource(assetObject.Guid);
                Debug.Log($"[Addon] Unloaded asset {assetObject.name} ({assetObject.Guid})");
            }

            OnAddonUnloaded?.Invoke(addon);
            addon.AllAssetObjectsHandle.Release();
            addon.CatalogHandle.Release();
            loadedAddons.Remove(addon);
        }

        public static string GetFolderForPlatform() {
#if UNITY_EDITOR
            return UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
#else
            switch (Application.platform) {
            case RuntimePlatform.WindowsPlayer:
                return IntPtr.Size == 8 ? "StandaloneWindows64" : "StandaloneWindows";
            case RuntimePlatform.OSXPlayer:
                return "StandaloneOSX";
            case RuntimePlatform.LinuxPlayer:
                return "StandaloneLinux64";
            case RuntimePlatform.Android:
                return "Android";
            case RuntimePlatform.IPhonePlayer:
                return "iOS";
            case RuntimePlatform.WebGLPlayer:
                return "WebGL";
            default:
                throw new Exception("Unsupported platform");
            }
#endif
        }

        private static string CombineUrl(string url1, string url2) {
            if (url1.Length == 0) {
                return url2;
            }

            if (url2.Length == 0) {
                return url1;
            }

            url1 = url1.TrimEnd('/', '\\');
            url2 = url2.TrimStart('/', '\\');

            return $"{url1}/{url2}";
        }
    }

    public class LoadedAddon {
        public AddonDefinition Definition;
        public AsyncOperationHandle<IResourceLocator> CatalogHandle;
        public AsyncOperationHandle<IList<AssetObject>> AllAssetObjectsHandle;
        public string FullName;
    }

    public enum AddonLoadResult {
        Failed,
        AlreadyLoaded,
        Success
    }
}