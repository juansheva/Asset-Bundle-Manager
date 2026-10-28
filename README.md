# UniTask Asset Bundle Manager

A lightweight, high-performance AssetBundle and Scene management tool for Unity, built using **UniTask** for asynchronous operations, automatic memory management, and zero-allocation asset lookups.

## Features
- **Async Loading with UniTask**: Non-blocking AssetBundle and Scene loading.
- **Auto Caching**: Automatically caches extracted assets in an internal memory container.
- **Zero-Allocation Lookups**: Fast `O(1)` generic asset retrieval (`GetAsset<T>`).
- **Streamed Scene AssetBundle Support**: Safe scene streaming and unloading (`GetScenePaths`, `UnloadSceneBundle`).
- **Defensive Error Handling**: Clear, descriptive exceptions (`ArgumentException`, `KeyNotFoundException`, `InvalidCastException`, `InvalidDataException`, `InvalidOperationException`).

## Installation via UPM (Git URL)
1. In Unity, open **Window** > **Package Manager**.
2. Click **+** > **Add package from git URL...**
3. Enter:
   ```text
   https://github.com/juansheva/UniTask-Asset-Bundle-Manager.git
   ```

## Dependencies
- [UniTask](https://github.com/Cysharp/UniTask) (`com.cysharp.unitask`) >= 2.5.10

## Quick Start
```csharp
// 1. Get an asset (auto-loads bundle if not cached)
GameObject heroPrefab = await UniTaskAssetBundleManager.GetAsset<GameObject>("heroes.bundle", "Knight");

// 2. Get all assets of a specific type
Dictionary<string, Sprite> icons = await UniTaskAssetBundleManager.GetAllAssets<Sprite>("icons.bundle");

// 3. Load a Scene from a bundle
string[] scenePaths = await UniTaskAssetBundleManager.GetScenePaths("dungeon.bundle");
await SceneManager.LoadSceneAsync(scenePaths[0]);
UniTaskAssetBundleManager.UnloadSceneBundle("dungeon.bundle");
```

## License
MIT License.
