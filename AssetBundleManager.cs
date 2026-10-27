using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AssetBundleManager
{
    /// <summary>
    /// Container penyimpanan asset yang sudah dimuat dari AssetBundle.
    /// Struktur: [NamaBundle] -> [NamaAsset] -> Objek Asset (GameObject, Texture, Audio, dll.)
    /// </summary>
    private static Dictionary<string, Dictionary<string, Object>> _assetContainer = new();

    /// <summary>
    /// Container penyimpanan AssetBundle khusus Scene yang sedang aktif di memori.
    /// Struktur: [NamaBundle] -> Objek AssetBundle aktif
    /// </summary>
    private static Dictionary<string, AssetBundle> _sceneBundleContainer = new();

    /// <summary>
    /// Path folder dasar tempat file AssetBundle disimpan (Read-only / konstan).
    /// Menggunakan static readonly karena Application.streamingAssetsPath dihitung saat runtime oleh Unity.
    /// </summary>
    private static readonly string _basePath = Application.streamingAssetsPath;

    /// <summary>
    /// Memuat file AssetBundle dari disk secara async.
    /// </summary>
    private static async UniTask<AssetBundle> LoadBundle(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("[AssetBundleController] Parameter 'fileName' tidak boleh kosong atau null! Harap masukkan nama file yang valid.", nameof(fileName));

        // Tentukan path lengkap file (jika fileName sudah merupakan path absolut, gunakan langsung)
        string filePath = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(_basePath, fileName);

        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                $"[AssetBundleController] File AssetBundle tidak ditemukan di path: '{filePath}'. Pastikan file sudah berada di folder '{_basePath}'.",
                filePath);

        AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(filePath);
        await bundleRequest.WithCancellation(cancellationToken);

        AssetBundle bundle = bundleRequest.assetBundle;
        if (bundle == null)
            throw new InvalidDataException(
                $"[AssetBundleController] Gagal memuat AssetBundle dari path: '{filePath}'. Kemungkinan file korup / bukan format AssetBundle yang valid / tidak kompatibel dengan platform ini.");

        return bundle;
    }

    /// <summary>
    /// Mengambil seluruh asset dari suatu bundle berdasarkan tipe generic T yang diinginkan (bebas tipe apa saja).
    /// Jika bundle belum ada di AssetContainer, method ini otomatis memanggil LoadBundle dari disk,
    /// mengekstrak seluruh asset ke AssetContainer, lalu me-unload wadah bundle-nya (Unload false).
    /// </summary>
    /// <typeparam name="T">Tipe asset yang diinginkan (GameObject, Sprite, AudioClip, Material, Object, dll.).</typeparam>
    /// <param name="bundleName">Nama bundle atau nama file bundle yang ingin diambil asset-nya.</param>
    /// <param name="cancellationToken">Token pembatalan operasi async.</param>
    /// <returns>Dictionary berisi seluruh asset bertipe T di dalam bundle tersebut.</returns>
    public static async UniTask<Dictionary<string, T>> GetAllAssets<T>(string bundleName, 
        CancellationToken cancellationToken = default) where T : Object
    {
        if (string.IsNullOrEmpty(bundleName))
            throw new ArgumentException("[AssetBundleController] Parameter 'bundleName' tidak boleh kosong atau null! Harap masukkan nama bundle yang valid.");
        
        // Jika belum ada di dictionary container, muat bundle dari disk dan tambahkan ke AssetContainer
        if (!_assetContainer.TryGetValue(bundleName, out var bundleDict))
        {
            AssetBundle bundle = await LoadBundle(bundleName, cancellationToken);

            if (bundle.isStreamedSceneAssetBundle)
            {
                bundle.Unload(true);
                throw new InvalidOperationException($"[AssetBundleController] Bundle '{bundleName}' adalah Scene AssetBundle. Gunakan 'GetScenePaths' untuk memuat scene, bukan 'GetAllAssets'.");
            }

            AssetBundleRequest assetsRequest = bundle.LoadAllAssetsAsync();
            await assetsRequest.WithCancellation(cancellationToken);

            if (assetsRequest.allAssets == null)
            {
                bundle.Unload(true);
                throw new InvalidDataException($"[AssetBundleController] Gagal mengekstrak asset dari bundle '{bundleName}'. Data asset di dalam file korup atau tidak dapat dideserialisasi.");
            }

            bundleDict = new Dictionary<string, Object>();
            for (int i = 0; i < assetsRequest.allAssets.Length; i++)
            {
                Object asset = assetsRequest.allAssets[i];
                if (asset != null)
                {
                    bundleDict[asset.name] = asset;
                }
            }

            _assetContainer[bundleName] = bundleDict;
            bundle.Unload(false);

            Debug.Log($"[AssetBundleController] Berhasil memuat bundle '{bundleName}' ({bundleDict.Count} asset disimpan, wadah bundle sudah di-unload).");
        }

        var typedDict = new Dictionary<string, T>(bundleDict.Count);
        foreach (var kvp in bundleDict)
        {
            if (kvp.Value is T typedAsset)
            {
                typedDict.Add(kvp.Key, typedAsset);
            }
        }

        return typedDict;
    }

    /// <summary>
    /// Mengambil satu asset spesifik bertipe T berdasarkan nama bundle dan nama asset.
    /// Otomatis memanggil GetAllAssets untuk memuat bundle dari disk jika belum ada di container.
    /// </summary>
    /// <typeparam name="T">Tipe asset yang diharapkan (GameObject, Sprite, AudioClip, Material, dll.).</typeparam>
    /// <param name="bundleName">Nama bundle tempat asset disimpan.</param>
    /// <param name="assetName">Nama asset yang ingin diambil.</param>
    /// <param name="cancellationToken">Token pembatalan operasi async.</param>
    /// <returns>Objek asset bertipe T.</returns>
    public static async UniTask<T> GetAsset<T>(
        string bundleName, 
        string assetName, 
        CancellationToken cancellationToken = default) where T : Object
    {
        if (string.IsNullOrEmpty(bundleName))
            throw new ArgumentException("[AssetBundleController] Parameter 'bundleName' tidak boleh kosong atau null! Harap masukkan nama bundle yang valid.");
        
        if (string.IsNullOrEmpty(assetName))
            throw new ArgumentException("[AssetBundleController] Parameter 'assetName' tidak boleh kosong atau null! Harap masukkan nama asset yang valid.");

        // Jika bundle belum ada di container, muat bundle terlebih dahulu
        if (!_assetContainer.TryGetValue(bundleName, out var bundleDict))
        {
            await GetAllAssets<Object>(bundleName, cancellationToken);
            bundleDict = _assetContainer[bundleName];
        }

        // Langsung cari asset dari dictionary tanpa menduplikasi isi bundle (O(1) & 0 GC Alloc)
        if (!bundleDict.TryGetValue(assetName, out Object rawAsset))
        {
            throw new KeyNotFoundException($"[AssetBundleController] Asset '{assetName}' tidak ditemukan di dalam bundle '{bundleName}'.");
        }

        if (rawAsset is T typedAsset)
        {
            return typedAsset;
        }

        throw new InvalidCastException($"[AssetBundleController] Asset '{assetName}' ditemukan di dalam bundle '{bundleName}', tetapi bertipe '{rawAsset.GetType().Name}', bukan '{typeof(T).Name}'.");
    }

    /// <summary>
    /// Mengambil seluruh path scene dari sebuah Scene AssetBundle dan menyimpan referensi bundle-nya di cache memori.
    /// Wadah bundle tetap aktif di memori agar scene dapat dimuat menggunakan SceneManager.
    /// </summary>
    /// <param name="bundleName">Nama file AssetBundle yang berisi scene.</param>
    /// <param name="cancellationToken">Token pembatalan operasi async.</param>
    /// <returns>Array berisi seluruh path scene di dalam bundle (misal: "Assets/Scenes/Level1.unity").</returns>
    public static async UniTask<string[]> GetScenePaths(
        string bundleName, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bundleName))
            throw new ArgumentException("[AssetBundleController] Parameter 'bundleName' tidak boleh kosong atau null! Harap masukkan nama bundle yang valid.", nameof(bundleName));

        // Jika bundle sudah ada di cache, langsung gunakan tanpa membaca ulang dari disk
        if (!_sceneBundleContainer.TryGetValue(bundleName, out AssetBundle bundle))
        {
            bundle = await LoadBundle(bundleName, cancellationToken);

            if (!bundle.isStreamedSceneAssetBundle)
            {
                bundle.Unload(true);
                throw new InvalidOperationException($"[AssetBundleController] Bundle '{bundleName}' bukan merupakan Scene AssetBundle.");
            }

            _sceneBundleContainer[bundleName] = bundle;
        }

        string[] scenePaths = bundle.GetAllScenePaths();
        if (scenePaths == null || scenePaths.Length == 0)
        {
            _sceneBundleContainer.Remove(bundleName);
            bundle.Unload(true);
            throw new InvalidDataException($"[AssetBundleController] Tidak ada file scene yang ditemukan di dalam bundle '{bundleName}'.");
        }

        return scenePaths;
    }

    /// <summary>
    /// Me-unload Scene AssetBundle dari cache memori setelah proses pemuatan scene selesai.
    /// Panggil method ini setelah SceneManager.LoadSceneAsync selesai 100%.
    /// Selalu menggunakan Unload(false) agar objek-objek di dalam scene yang sedang aktif tetap aman.
    /// </summary>
    /// <param name="bundleName">Nama bundle yang ingin di-unload dari cache.</param>
    public static void UnloadSceneBundle(string bundleName)
    {
        if (string.IsNullOrEmpty(bundleName))
        {
            Debug.LogWarning("[AssetBundleController] Parameter 'bundleName' tidak boleh kosong atau null! Harap masukkan nama bundle yang valid.");
            return;
        }

        if (_sceneBundleContainer.TryGetValue(bundleName, out AssetBundle bundle))
        {
            bundle.Unload(false);
            _sceneBundleContainer.Remove(bundleName);
            Debug.Log($"[AssetBundleController] Berhasil me-unload Scene AssetBundle '{bundleName}' dari cache.");
        }
    }
}
