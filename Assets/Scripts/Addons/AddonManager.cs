using Newtonsoft.Json;
using NSMB.Networking;
using NSMB.Sound;
using Photon.Client;
using Photon.Realtime;
using Quantum;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace NSMB.Addons {
    public class AddonManager : MonoBehaviour, IOnEventCallback, IMatchmakingCallbacks {

        //---Static Variables
        public static event Action<LoadedAddon> OnAddonLoaded, OnAddonUnloaded;
        public static event Action OnAvailableAddonListLoaded;

        private static readonly byte EventBroadcastAddonList = 101;
        public static readonly DisconnectCause DisconnectCauseMissingAddon = (DisconnectCause) 101;

        private static readonly string RemoteRepoUrl = "https://raw.githubusercontent.com/ipodtouch0218/NSMB-MarioVsLuigi-AddonRepository/main/";
        public static readonly string AddonExtension = ".mvladdon";

        public static string LocalFolderPath;
        private static string AddonCachePath;
        private static string PlatformFolder;

        //---Properties
        public List<LoadedAddon> LoadedAddons { get; private set; } = new();

        //---Private Variables
        private List<AddonFile> availableAddons = new();
        private bool waitingForAddons;
#if UNITY_STANDALONE
        private FileSystemWatcher watcher;
#endif

        public void Start() {
            LocalFolderPath = Path.Combine(Application.dataPath, "addons");
            AddonCachePath = Path.Combine(Application.persistentDataPath, "addoncache");
            PlatformFolder = GetFolderForPlatform();
            _ = FindAvailableAddons();

#if UNITY_STANDALONE
            watcher = new(LocalFolderPath);
            watcher.Filter = "*.mvladdon";
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.EnableRaisingEvents = true;

            watcher.Changed += (x, y) => {
                Debug.Log("Changed: " + y.FullPath);
            };
            watcher.Deleted += (x, y) => {
                Debug.Log("Deleted: " + y.FullPath);
            };
            watcher.Created += (x, y) => {
                Debug.Log("Created: " + y.FullPath);
            };
#endif

            NetworkHandler.Client.AddCallbackTarget(this);
        }

        public void OnDestroy() {
#if UNITY_STANDALONE
            watcher.Dispose();
#endif
        }

        public async Awaitable FindAvailableAddons() {
            // Background thread this sh*t
            await Awaitable.BackgroundThreadAsync();
            List<AddonFile> results = new();

            string filter = "*" + AddonExtension;
            foreach (var filepath in Directory.EnumerateFiles(LocalFolderPath, filter, new EnumerationOptions { RecurseSubdirectories = true })) {
                _ = await RegisterAddon(filepath, results);
            }

#if UNITY_STANDALONE
            foreach (var filepath in Directory.EnumerateFiles(AddonCachePath, filter)) {
                // Find all `.mvladdon` files.
                _ = await RegisterAddon(filepath, results);
            }
#endif

            // Main thread the events
            await Awaitable.MainThreadAsync();
            availableAddons = results;
            OnAvailableAddonListLoaded?.Invoke();
        }

        public async Task<LoadAllAddonsResult> LoadAllAddons(List<Guid> requestedAddons) {
            // Unload *ALL* addons. this is important as the order MATTERS.
            foreach (var addon in LoadedAddons.ToList()) {
                await UnloadAddon(addon);
            }

            // Load addons
            List<Guid> tryDownloading = new();
            foreach (var guid in requestedAddons) {
                var loadedAddon = await LoadAddon(guid);
                if (loadedAddon == null) {
                    // Failed.
                    tryDownloading.Add(guid);
                }
            }

            if (tryDownloading.Count > 0) {
                if (!Settings.Instance.generalAddonDownloads) {
                    return LoadAllAddonsResult.FailureDownloadsDisabled;
                }

                List<Guid> failedDownloads = new();
                foreach (var guid in tryDownloading) {
                    var downloadedAddon = await DownloadAddon(guid);
                    if (downloadedAddon == null) {
                        failedDownloads.Add(guid);
                        continue;
                    }

                    using (MemoryStream ms = new(downloadedAddon)) {
                        var loadedAddon = await LoadAddonStream(ms);
                        if (loadedAddon == null) {
                            // Failed.
                            failedDownloads.Add(guid);
                            continue;
                        }
                    }

#if UNITY_STANDALONE
                    // Cache
                    await SaveAddonToCache(guid, downloadedAddon);
#endif
                }
                if (failedDownloads.Count > 0) {
                    return LoadAllAddonsResult.Failure;
                }
            }

            // Good to go.
            return LoadAllAddonsResult.Success;
        }

        public async Awaitable<LoadedAddon> LoadAddon(Guid addonGuid) {
            var availableAddon = availableAddons.FirstOrDefault(addon => addon.Definition.ReleaseGuid == addonGuid);
            if (availableAddon == null) {
                return null;
            }

            try {
                using FileStream fs = new(availableAddon.FilePath, FileMode.Open);
                return await LoadAddonStream(fs);
            } catch (Exception e) {
                Debug.Log($"[Addon] Failed to load addon {availableAddon.Definition.FullName} ({addonGuid}) from file \"{availableAddon.FilePath}\": {e.Message}");
                return null;
            }
        }

        public async Awaitable<LoadedAddon> LoadAddonStream(Stream stream) {
            await Awaitable.BackgroundThreadAsync();
            using ZipArchive zipFile = new(stream, ZipArchiveMode.Read);
            var addonDef = await GetAddonDefinition(zipFile);

            Debug.Log($"[Addon] Loading addon {addonDef.FullName} ({addonDef.ReleaseGuid})");

            List<AssetBundle> loadedBundles = new();
            List<(string,MemoryStream)> decompressedBundles = new();
            List<UnityEngine.Object> registeredAssets = new();
            try {
                // Load bundles
                var zippedBundles = zipFile.Entries.Where(zae => zae.FullName.StartsWith(PlatformFolder));
                foreach (var zippedBundle in zippedBundles) {
                    using Stream bundleStream = zippedBundle.Open();
                    MemoryStream memoryStream = new((int) zippedBundle.Length);
                    bundleStream.CopyTo(memoryStream);
                    decompressedBundles.Add((zippedBundle.Name, memoryStream));
                }

                // Load into asset database
                await Awaitable.MainThreadAsync();

                foreach (var bundle in decompressedBundles) {
                    var loadTask = AssetBundle.LoadFromStreamAsync(bundle.Item2);
                    await loadTask;
                    if (!loadTask.assetBundle) {
                        // failure?
                        Debug.Log($"[Addon] Failed to load addon {addonDef.FullName} ({addonDef.ReleaseGuid}): loading AssetBundle \"{bundle.Item1}\" failed");
                        throw new Exception("Bundle failed to load");
                    }
                    loadedBundles.Add(loadTask.assetBundle);
                }

                if (loadedBundles.Count == 0) {
                    Debug.Log($"[Addon] Failed to load addon {addonDef.FullName} ({addonDef.ReleaseGuid}): it does not support our platform ({PlatformFolder})");
                    return null;
                }

                // Register to Quantum DB
                foreach (var assetBundle in loadedBundles) {
                    if (assetBundle.isStreamedSceneAssetBundle) {
                        Debug.Log(string.Join(",", assetBundle.GetAllScenePaths()));
                        continue;
                    }

                    var loadTask = assetBundle.LoadAllAssetsAsync<ScriptableObject>();
                    await loadTask;

                    foreach (ScriptableObject so in loadTask.allAssets.Cast<ScriptableObject>()) {
                        if (so is AssetObject assetObject) {
                            try {
                                QuantumUnityDB.Global.AddAsset(assetObject);
                                registeredAssets.Add(assetObject);
                            } catch {
                                // Already added? Doesn't matter... ignore.
                                Debug.Log($"[Addon] Failed to register asset {assetObject.name} ({assetObject.Guid})");
                            }
                        } else if (so is GlobalSoundEffectOverrides sfxOverride) {
                            SoundEffectResolver.Instance.GlobalProviders.Add(sfxOverride);
                            registeredAssets.Add(sfxOverride);
                        }
                    }
                }
                if (registeredAssets.Count > 0) {
                    Debug.Log($"[Addon] Registered {registeredAssets.Count} assets");
                }

                var newAddon = new LoadedAddon {
                    Definition = addonDef,
                    LoadedAssetBundles = loadedBundles,
                    RegisteredAssets = registeredAssets,
                };
                LoadedAddons.Add(newAddon);
                OnAddonLoaded?.Invoke(newAddon);

                return newAddon;
            } catch (Exception e) {
                foreach (var bundle in loadedBundles) {
                    bundle.Unload(true);
                }
                foreach (var asset in registeredAssets) {
                    UnloadAsset(asset);
                }
                
                Debug.Log($"[Addon] Failed to load addon {addonDef.FullName} ({addonDef.ReleaseGuid}): An exception was thrown {e.Message}");
                Debug.LogError(e);
                return null;
            } finally {
                foreach (var bundleStream in decompressedBundles) {
                    bundleStream.Item2?.Dispose();
                }
            }
        }

        public async Awaitable<byte[]> DownloadAddon(Guid addonGuid) {
            await Awaitable.MainThreadAsync();

            if (!Settings.Instance.generalAddonDownloads) {
                Debug.Log($"[Addon] Automatic downloads are disabled! Skipping download for {addonGuid}");
                return null;
            }

            string targetFileUrl = CombineUrl(RemoteRepoUrl, addonGuid + AddonExtension);
            Debug.Log($"[Addon] Attempting to download addon {addonGuid} from remote source ({targetFileUrl})");

            using UnityWebRequest zippedAddonRequest = UnityWebRequest.Get(targetFileUrl);
            zippedAddonRequest.SetRequestHeader("Accept", "*/*");
            zippedAddonRequest.SetRequestHeader("UserAgent", "ipodtouch0218/NSMB-MarioVsLuigi");
            await zippedAddonRequest.SendWebRequest();
            if (zippedAddonRequest.result != UnityWebRequest.Result.Success) {
                Debug.Log($"[Addon] Download failed: {zippedAddonRequest.error} ({zippedAddonRequest.responseCode})");
                return null;
            }
            return zippedAddonRequest.downloadHandler.data;

        }

        public async Task UnloadAddon(Guid addonGuid) {
            await UnloadAddon(LoadedAddons.FirstOrDefault(la => la.Definition.ReleaseGuid == addonGuid));
        }

        public async Task UnloadAddon(LoadedAddon addon) {
            if (addon == null) {
                throw new ArgumentNullException("Tried to unload a null addon!");
            }

            foreach (var asset in addon.RegisteredAssets) {
                UnloadAsset(asset);
            }
            if (addon.RegisteredAssets.Count > 0) {
                Debug.Log($"[Addon] Unloaded {addon.RegisteredAssets.Count} assets");
            }

            foreach (var assetBundle in addon.LoadedAssetBundles) {
                await assetBundle.UnloadAsync(true);
            }

            LoadedAddons.Remove(addon);
            OnAddonUnloaded?.Invoke(addon);
        }

        private void UnloadAsset(UnityEngine.Object obj) {
            if (obj is AssetObject assetObject) {
                QuantumUnityDB.Global.DisposeAsset(assetObject.Guid, true);
                QuantumUnityDB.Global.RemoveSource(assetObject.Guid);
            } else if (obj is GlobalSoundEffectOverrides sfxOverride) {
                SoundEffectResolver.Instance.GlobalProviders.Remove(sfxOverride);
            }
        }

        public bool IsAddonLoaded(Guid guid) {
            return LoadedAddons.Any(la => la.Definition.ReleaseGuid == guid);
        }

        public async Awaitable<AddonFile> RegisterAddon(string fullPath, List<AddonFile> results) {
            // Parse file to see if we need to add this.
            await Awaitable.BackgroundThreadAsync();
            try {
                fullPath = new FileInfo(fullPath).FullName; // Clean up file paths.
                using var zipFile = ZipFile.OpenRead(fullPath);
                var addonDef = await GetAddonDefinition(zipFile);
                if (addonDef == null) {
                    return null;
                }
                
                if (availableAddons.Any(af => af.Definition.ReleaseGuid == addonDef.ReleaseGuid)
                    || results.Any(af => af.Definition.ReleaseGuid == addonDef.ReleaseGuid)) {
                    Debug.Log($"[Addon] Duplicate addon found \"{addonDef.FullName}\" ({addonDef.ReleaseGuid}) at \"{fullPath}\"");
                    return null;
                }

                AddonFile addon = new() {
                    Definition = addonDef,
                    FilePath = fullPath
                };

                await Awaitable.MainThreadAsync();
                results.Add(addon);
                Debug.Log($"[Addon] Registered addon \"{addonDef.FullName}\" ({addonDef.ReleaseGuid}) at \"{fullPath}\"");
                return addon;
            } catch (Exception e) {
                Debug.LogWarning($"[Addon] Failed to read addon file {fullPath}: {e.Message}");
            }
            return null;
        }

        public static async Awaitable SaveAddonToCache(Guid guid, byte[] data) {
            await Awaitable.BackgroundThreadAsync();
            await File.WriteAllBytesAsync($"{AddonCachePath}/{guid}{AddonExtension}", data);
        }

        public static async Awaitable<AddonDefinition> GetAddonDefinition(ZipArchive zipFile) {
            await Awaitable.BackgroundThreadAsync();
            try {
                var entry = zipFile.GetEntry("addon.json");
                if (entry == null) {
                    return null;
                }
                using StreamReader reader = new(entry.Open());
                var addonDefJson = await reader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<AddonDefinition>(addonDefJson);
            } catch {
                return null;
            }
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

        public async void OnEvent(EventData photonEvent) {
            if (photonEvent.Code == EventBroadcastAddonList && waitingForAddons) {
                waitingForAddons = false;
                try {
                    List<Guid> guids = ((string[]) photonEvent.CustomData).Select(Guid.Parse).ToList();
                    Debug.Log($"[Addon] Got addon list of {guids.Count} addons: [{string.Join(", ", guids)}]");
                    var loadAddonResult = await GlobalController.Instance.addonManager.LoadAllAddons(guids);
                    if (loadAddonResult == LoadAllAddonsResult.Success) {
                        _ = NetworkHandler.Instance.StartQuantum();
                    } else {
                        NetworkHandler.ThrowError(
                            loadAddonResult == LoadAllAddonsResult.FailureDownloadsDisabled
                                ? "ui.error.join.addons.downloadsdisabled"
                                : "ui.error.join.addons.downloadfailed",
                            false);
                        NetworkHandler.Client.Disconnect(DisconnectCauseMissingAddon);
                    }
                } catch (Exception e) {
                    Debug.LogError($"[Addon] Failed to activate proper addons! Disconnecting. ({e.Message})");
                    NetworkHandler.Client.Disconnect(DisconnectCauseMissingAddon);
                    throw;
                }
            }
        }

        public void OnFriendListUpdate(List<FriendInfo> friendList) { }

        public void OnCreatedRoom() {
            // Send addon list
            NetworkHandler.Client.OpRaiseEvent(EventBroadcastAddonList,
                GlobalController.Instance.addonManager.LoadedAddons.Select(la => la.Definition.ReleaseGuid.ToString()).ToArray(),
                new RaiseEventArgs {
                    CachingOption = EventCaching.AddToRoomCacheGlobal
                }, SendOptions.SendReliable);
        }

        public void OnCreateRoomFailed(short returnCode, string message) { }

        public void OnJoinedRoom() {
            waitingForAddons = true;
        }

        public void OnJoinRoomFailed(short returnCode, string message) { }

        public void OnJoinRandomFailed(short returnCode, string message) { }

        public void OnLeftRoom() {
            waitingForAddons = false;
        }
    }

    public class LoadedAddon {
        public AddonDefinition Definition;
        public List<AssetBundle> LoadedAssetBundles;
        public List<UnityEngine.Object> RegisteredAssets;
    }

    public class AddonFile {
        public AddonDefinition Definition;
        public string FilePath;
    }

    public enum AddonLoadResult {
        Failed,
        AlreadyLoaded,
        Success
    }

    public enum LoadAllAddonsResult {
        Failure,
        FailureDownloadsDisabled,
        Success,
    }

}