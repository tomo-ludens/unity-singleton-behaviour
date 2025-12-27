# Unity SingletonBehaviour<T>

[日本語 (Japanese)](./docs/ja/README.md) | [English](./docs/en/README.md)

`SingletonBehaviour<T>` は **MonoBehaviour 向けの型別（Type-per-singleton）シングルトン基底クラス**です。

特に **Enter Play Mode Options で Domain Reload を無効化した環境でも破綻しにくい**運用を目的にしています。

Domain Reload 無効時は `static` が Play のたびに自動リセットされず残留し得るため、通常の “シンプルな Singleton” が壊れやすくなります。

---

`SingletonBehaviour<T>` is a **type-per-singleton base class for MonoBehaviour**.

It is designed to remain **robust even when Domain Reload is disabled** (Enter Play Mode Options).

With Domain Reload disabled, `static` state can persist between play sessions, making “simple singletons” prone to breakage.

## Requirements / 動作環境
- Unity 6.3 (6000.3.x) or later

## Features / 特長
- ✅ 型別シングルトン（SingletonBehaviour<T>）
- ✅ Instance（自動生成）/ TryGetInstance（生成しない）
- ✅ DontDestroyOnLoad によるシーン跨ぎの永続化
- ✅ Domain Reload 無効（Enter Play Mode Options）でも安全な設計
- ✅ Application.quitting を考慮した終了時の安全性
- ✅ CRTP 風制約 + ランタイムガードで誤用を検出
- ✅ Edit Mode では検索のみ（static キャッシュに副作用なし）
- ✅ 派生型を拒否する厳密型チェック／非アクティブが存在する場合の自動生成ブロック（DEV/EDITOR）
- ✅ 公開 API は Play 中メインスレッドを強制

---
- ✅ Type-per-singleton (SingletonBehaviour<T>)
- ✅ Instance (auto-creates) / TryGetInstance (no creation)
- ✅ Persistent across scenes via DontDestroyOnLoad
- ✅ Safe design even with Domain Reload disabled (Enter Play Mode Options)
- ✅ Shutdown safety via Application.quitting
- ✅ CRTP-style constraint + runtime guard to catch misuse
- ✅ Edit Mode safe (search only, no side effects on static cache)
- ✅ Rejects derived types; blocks auto-create when an inactive instance exists (DEV/EDITOR)
- ✅ Public API requires main thread in Play Mode

> For design details and constraints (Awake/OnDestroy handling, duplicate detection, creation policy, etc.), see the language-specific READMEs.

## Quick Start / 使い方

```csharp
using Foundation.Singletons; // Adjust namespace to your project

public sealed class AudioManager : SingletonBehaviour<AudioManager>
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
// AudioManager.Instance.DoSomething();
````

* 生成を伴わずに参照したい場合は `TryGetInstance(out var x)` を使用してください。

  Use `TryGetInstance(out var x)` when you want to reference the instance without creating it.

* “Domain Reload 無効時の注意点” と “推奨の初期化/破棄フロー” は言語別 README に集約しています。

  Notes for “Domain Reload disabled” and recommended init/teardown flow are documented in the language-specific READMEs.

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
