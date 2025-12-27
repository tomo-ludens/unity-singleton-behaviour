# Unity SingletonBehaviour

[日本語 (Japanese)](./docs/ja/README.md) | [English](./docs/en/README.md)

**ポリシー駆動型シングルトン基底クラス** for MonoBehaviour.

特に **Enter Play Mode Options で Domain Reload を無効化した環境でも破綻しにくい**運用を目的にしています。

---

**Policy-driven singleton base classes** for MonoBehaviour.

Designed to remain **robust even when Domain Reload is disabled** (Enter Play Mode Options).

## Requirements / 動作環境
- Unity 6.3 (6000.3.x) or later

## Provided Classes / 提供クラス

| Class | Persistence | Auto-create | Use Case |
| --- | --- | --- | --- |
| `PersistentSingletonBehaviour<T>` | ✅ `DontDestroyOnLoad` | ✅ Yes | Game-wide managers |
| `SceneSingletonBehaviour<T>` | ❌ No | ❌ No | Scene-specific controllers |

## Features / 特長

- ✅ ポリシー駆動（Persistent / Scene-scoped）
- ✅ Instance（条件付き自動生成）/ TryGetInstance（生成しない）
- ✅ DontDestroyOnLoad によるシーン跨ぎの永続化（Persistent のみ）
- ✅ Domain Reload 無効（Enter Play Mode Options）でも安全な設計
- ✅ Application.quitting を考慮した終了時の安全性
- ✅ CRTP 風制約 + ランタイムガードで誤用を検出
- ✅ Edit Mode では検索のみ（static キャッシュに副作用なし）
- ✅ 派生型を拒否する厳密型チェック／非アクティブが存在する場合の自動生成ブロック（DEV/EDITOR）
- ✅ 公開 API は Play 中メインスレッドを強制

---

- ✅ Policy-driven (Persistent / Scene-scoped)
- ✅ Instance (conditional auto-create) / TryGetInstance (no creation)
- ✅ Persistent across scenes via DontDestroyOnLoad (Persistent only)
- ✅ Safe design even with Domain Reload disabled (Enter Play Mode Options)
- ✅ Shutdown safety via Application.quitting
- ✅ CRTP-style constraint + runtime guard to catch misuse
- ✅ Edit Mode safe (search only, no side effects on static cache)
- ✅ Rejects derived types; blocks auto-create when an inactive instance exists (DEV/EDITOR)
- ✅ Public API requires main thread in Play Mode

> For design details and constraints, see the language-specific READMEs.

## Quick Start / 使い方

### Persistent Singleton（永続シングルトン）
```csharp
using Singletons;

public sealed class GameManager : PersistentSingletonBehaviour<GameManager>
{
    protected override void OnSingletonAwake()
    {
        // Initialization / 初期化
    }

    protected override void OnSingletonDestroy()
    {
        // Cleanup / 後始末
    }
}

// Access / 利用例:
// GameManager.Instance.DoSomething();
```

### Scene-scoped Singleton（シーンスコープシングルトン）
```csharp
using Singletons;

public sealed class LevelController : SceneSingletonBehaviour<LevelController>
{
    protected override void OnSingletonAwake()
    {
        // Per-scene initialization / シーンごとの初期化
    }
}

// ⚠️ Must be placed in scene / シーンに配置必須
// LevelController.Instance.DoSomething();
```

* 生成を伴わずに参照したい場合は `TryGetInstance(out var x)` を使用してください。

  Use `TryGetInstance(out var x)` when you want to reference the instance without creating it.

* "Domain Reload 無効時の注意点" と "推奨の初期化/破棄フロー" は言語別 README に集約しています。

  Notes for "Domain Reload disabled" and recommended init/teardown flow are documented in the language-specific READMEs.

## Background / 背景

Enter Play Mode Options で **Domain Reload を無効**にすると、**static フィールドや static イベント購読が Play のたびに自動リセットされず残留**し得ます。

When **Domain Reload is disabled**, **static fields and static event subscriptions may persist** across play sessions instead of being reset automatically.

そのため、シングルトンやグローバル状態は、Play を止めて再開した際に二重初期化・二重購読・参照切れ等が起きやすくなります。本シングルトンはそこを前提に設計しています。

As a result, singletons and global state can easily suffer from double-initialization, duplicated subscriptions, or stale references when you stop and restart Play Mode. This singleton is designed with that behavior in mind.

## Contributing / コントリビュート

Issue / PR 歓迎です。

Issues and PRs are welcome.

## License

See [LICENSE](./LICENSE).
