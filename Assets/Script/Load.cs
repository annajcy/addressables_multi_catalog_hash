using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Script
{
    public class Load : MonoBehaviour
    {
        private void Start()
        {
            var cleanBundleCache = Addressables.CleanBundleCache();
            cleanBundleCache.Completed += clearCacheHandle =>
            {
                var dlc1Handle =
                Addressables.LoadContentCatalogAsync("http://127.0.0.1:8085/StandaloneOSX/DLC1_0.1.0.bin");
                dlc1Handle.Completed += resHandle =>
                {
                    if (resHandle.Status == AsyncOperationStatus.Succeeded)
                    {
                        var handle1 = Addressables.LoadAssetAsync<GameObject>("Cube (1)");
                        handle1.Completed += handle =>
                        {
                            if (handle.Status == AsyncOperationStatus.Succeeded)
                                Instantiate(handle.Result);
                        };

                        var handle4 = Addressables.LoadAssetAsync<GameObject>("Cube (2)");
                        handle4.Completed += handle =>
                        {
                            if (handle.Status == AsyncOperationStatus.Succeeded)
                                Instantiate(handle.Result);
                        };
                    }
                    else Debug.LogError("load failed: dlc1 json");
                };

                var dlc2Handle = Addressables.LoadContentCatalogAsync("http://127.0.0.1:8085/StandaloneOSX/DLC2_0.1.0.bin");
                dlc2Handle.Completed += resHandle =>
                {
                    if (resHandle.Status == AsyncOperationStatus.Succeeded)
                    {
                        var handle2 = Addressables.LoadAssetAsync<GameObject>("Cube (3)");
                        handle2.Completed += handle =>
                        {
                            if (handle.Status == AsyncOperationStatus.Succeeded)
                                Instantiate(handle.Result);
                        };

                        var handle3 = Addressables.LoadAssetAsync<GameObject>("Cube (4)");
                        handle3.Completed += handle =>
                        {
                            if (handle.Status == AsyncOperationStatus.Succeeded)
                                Instantiate(handle.Result);
                        };
                    }
                    else Debug.LogError("load failed: dlc2 json");
                };

                var handle0 = Addressables.LoadAssetAsync<GameObject>("Cube");
                handle0.Completed += handle =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                        Instantiate(handle.Result);
                };
            };
        }
    }
}
