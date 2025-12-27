# Unity SingletonBehaviour

[Japanese](./README.md) | [English](../en/README.md)

MonoBehaviour 向けの **ポリシー駆動型シングルトン基底クラス** です。

Unity 6.3（6000.3 系）以降での利用を想定しています。

## Overview ✨

本ライブラリは 2 種類のシングルトン基底クラスを提供します：

| クラス | 永続化 | 自動生成 | 用途 |
| --- | --- | --- | --- |
| `PersistentSingletonBehaviour<T>` | ✅ `DontDestroyOnLoad` | ✅ する | ゲーム全体で生存するマネージャ |
| `SceneSingletonBehaviour<T>` | ❌ しない | ❌ しない | シーン固有のコントローラ |

共通機能：

- 🧩 **型ごとのシングルトン保証**（`GameManager` と `AudioManager` は別インスタンス）
- 🛡️ **型安全な継承**（CRTP 風制約 + ランタイムガードで誤用を検出）
- 🧯 **終了時の安全性**（`Application.quitting` を考慮し、終了中の再生成を抑止）
- ⚙️ **Domain Reload 無効対応**（Play セッション識別子で型ごとの static キャッシュを無効化）
- 🧱 **誤配置への実用的な耐性**（子オブジェクト配置でも root に移動して永続化 ※Persistent のみ）
- 🧼 **ソフトリセット指向**（Play ごとに `OnSingletonAwake()` を走らせ、同一個体でも再初期化できる設計）
- 🖥️ **Edit Mode 安全**（Edit Mode では検索のみ、static キャッシュに副作用なし）
- 🎯 **厳密な型チェック**（派生型を拒否し、T が実体型であることを強制）
- 🚦 **非アクティブが存在する場合の自動生成ブロック（DEV/EDITOR）**—隠れ重複を防止
- 🧵 **メインスレッドガード**（Play 中の公開 API はメインスレッドを強制）

## Requirements ✅

- Unity 6.3 (6000.3.x) 以降
- Enter Play Mode Options で **Reload Domain を無効化**しても破綻しにくい設計
- （任意）Reload Scene を無効化した場合でも、Play ごとに再初期化できる運用を想定

## Installation 📦

- `Singletons/` フォルダをプロジェクトに追加します（例：`Assets/Plugins/Singletons/`）。
- 名前空間はプロジェクト方針に合わせて調整してください。

### 名前空間のインポート
```csharp
using Singletons;
```

## Usage 🚀

### 1) 永続シングルトン（PersistentSingletonBehaviour）

シーンを跨いで生存し、未配置なら自動生成されます。

```csharp
using Singletons;

public sealed class GameManager : PersistentSingletonBehaviour<GameManager>
{
    public int Score { get; private set; }

    protected override void OnSingletonAwake()
    {
        // Play セッションごとに走る初期化
        this.Score = 0;
    }

    public void AddScore(int value) => this.Score += value;

    protected override void OnSingletonDestroy()
    {
        // 実体が破棄されるタイミングでの後始末
    }
}
```

### 2) シーンスコープシングルトン（SceneSingletonBehaviour）

シーンに配置必須。シーンアンロードで破棄され、自動生成されません。

```csharp
using Singletons;

public sealed class LevelController : SceneSingletonBehaviour<LevelController>
{
    protected override void OnSingletonAwake()
    {
        // シーンごとの初期化
    }
}
```

> ⚠️ **未配置で `Instance` を呼ぶと DEV/EDITOR では例外**、Player では `null` を返します。

### 3) `Instance` / `TryGetInstance` の使い分け

| 項目 | 推奨 |
| --- | --- |
| クラス修飾子 | `sealed`（意図しない継承事故を防ぐ） |
| 初期化処理 | `OnSingletonAwake()` に記述（Play ごとの再初期化） |
| 破棄処理 | `OnSingletonDestroy()` に記述（破棄時のみ） |

* ✅ **Instance**：その依存が「必ず必要」なとき（Persistent なら無ければ作ってでも動かす）
  例：`GameManager`, `AudioManager` などゲーム進行に必須のマネージャ

* ✅ **TryGetInstance**：「あるなら使う」「無いなら何もしない」「終了処理で増やしたくない」
  例：`OnDisable` / `OnDestroy` / `OnApplicationPause` などの後片付け、任意機能の登録解除

```csharp
// 終了処理での安全なパターン
private void OnDisable()
{
    if (AudioManager.TryGetInstance(out var am))
    {
        am.Unregister(this);
    }
}
```

### 4) アクセスパターン（キャッシュ徹底）🧠

❌ **毎フレーム `Instance` を呼ぶのは非推奨**です。探索が走る可能性があるため、初回に取得してキャッシュし、以降は参照を使うのが基本です。

✅ 推奨：初回に取得してキャッシュ
```csharp
using Singletons;
using UnityEngine;

public sealed class ScoreHUD : MonoBehaviour
{
    private GameManager _gm;

    private void Start()
    {
        this._gm = GameManager.Instance; // キャッシュ
    }

    private void Update()
    {
        if (this._gm == null) return;
        // this._gm.Score を使用
    }
}
```

## Public API 📌

### `static T Instance { get; }`

シングルトンインスタンスを返します。

| 状態 | Persistent | Scene |
| --- | --- | --- |
| インスタンス存在 | キャッシュ済みインスタンス | キャッシュ済みインスタンス |
| 未存在 | 検索 → 無ければ**自動生成** | 検索 → 無ければ **例外(DEV/EDITOR)** or `null`(Player) |
| quitting 中 | `null` | `null` |
| Edit Mode | 検索のみ（生成・キャッシュなし） | 検索のみ（生成・キャッシュなし） |
| 非アクティブが存在（DEV/EDITOR） | 例外（重複を防止） | 例外（重複を防止） |
| 派生型が見つかった | `null`（Play: 破棄、Edit: ログのみ） | `null`（Play: 破棄、Edit: ログのみ） |

### `static bool TryGetInstance(out T instance)`

インスタンスが存在すれば取得します。**生成は行いません**。

| 状態 | 戻り値 | `instance` |
| --- | --- | --- |
| インスタンス存在 | `true` | 有効な参照 |
| 未存在 | `false` | `null` |
| quitting 中 | `false` | `null` |
| Edit Mode | 検索結果 | 検索のみ（キャッシュなし） |
| 派生型が見つかった | `false` | `null`（Play: 破棄、Edit: ログのみ） |

## Design Intent（設計意図）🧠

### なぜポリシーパターンを使うのか？

シングルトンの挙動（永続化・自動生成）をポリシーで分離することで、同じコアロジックを共有しつつ用途別のクラスを提供しています。

```csharp
public interface ISingletonPolicy
{
    bool PersistAcrossScenes { get; }
    bool AutoCreateIfMissing { get; }
}
```

### なぜ `SingletonRuntime` が必要なのか？

Domain Reload を無効化すると、**static フィールドや static イベントのハンドラが Play 間で残留**し得ます。

この "残留" を前提に、Play セッションの開始ごとに **型ごとの static キャッシュを無効化**する必要があります。

そのため、

* Play 開始時に確実に呼ばれる非ジェネリックな場所で `PlaySessionId` を更新する（`SubsystemRegistration`）
* `SingletonBehaviour<T, TPolicy>` 側は `PlaySessionId` を参照して **キャッシュを無効化**する

という責務分離を行っています。

> 補足：Unity では「ジェネリック型内の `[RuntimeInitializeOnLoadMethod]` が期待どおり呼ばれない」ケースが知られており、
> その回避として非ジェネリック側に初期化を集約する設計は実用上有効です（Issue Tracker 参照）。

### DontDestroyOnLoad の呼び出し管理

`DontDestroyOnLoad` は同一オブジェクトに複数回呼んでも問題ありませんが、
本実装では `_isPersistent` フラグで呼び出しを1回に制限し、不要な処理を回避しています。

## Constraints（重要な制約）⚠️

### ❌ `Awake()` / `OnEnable()` / `OnDestroy()` をオーバーライドする場合は base 呼び出し必須

基底クラスの Unity メッセージ関数は以下を担当しています：

* `_instance` の確立・重複排除
* Play セッションの検出と static キャッシュ無効化
* root 化（`DontDestroyOnLoad` の前提を満たす ※Persistent のみ）
* `DontDestroyOnLoad` の適用（※Persistent のみ）
* `OnSingletonAwake` / `OnSingletonDestroy` の呼び出し制御

派生側でこれらをオーバーライドする場合は、**必ず `base.Awake()` 等を呼び出してください**。
推奨は `OnSingletonAwake()` / `OnSingletonDestroy()` の使用です。

```csharp
// OK: base を呼ぶ
protected override void Awake()
{
    base.Awake();
    // 追加処理
}

// 推奨: OnSingletonAwake を使う
protected override void OnSingletonAwake()
{
    // 初期化処理
}
```

### ❌ 型パラメータには自分自身を指定する

CRTP 制約により、以下のような誤った継承はコンパイルエラーになります：
```csharp
// ❌ コンパイルエラー
public sealed class A : PersistentSingletonBehaviour<B> { }

// ✅ 正しい実装
public sealed class A : PersistentSingletonBehaviour<A> { }
```

## Scene Placement Notes 🧱

| 制約 | 理由 |
| --- | --- |
| 複数シーンに同一シングルトンを配置しない | 初期化順で片方が Destroy される（先着が勝つ） |
| root GameObject が望ましい（Persistent） | `DontDestroyOnLoad` は root にのみ有効 |

本実装は、誤って子オブジェクトに配置された場合でも **自動で root に移動**して永続化します（Persistent のみ）。
ただし意図しない移動は混乱の元になり得るため、**Editor/Development ビルドのみ**警告ログを出します。

## Edit Mode Behavior 🖥️

Edit Mode（`Application.isPlaying == false`）では以下の動作になります：

* `Instance` / `TryGetInstance` は **検索のみ**実行（`FindAnyObjectByType`）
* **自動生成しない**
* **static キャッシュを更新しない**（副作用ゼロ）
* **Play セッション状態に影響しない**
* **派生型が見つかった場合は破棄せずログのみ**（Undo/Inspector への影響を回避）

これにより、エディタスクリプトやカスタムインスペクタから安全にシングルトンを参照できます。

## Soft Reset（Playごとの安全な再初期化）🧼

本実装は「同一個体を再利用しつつ、Play ごとに初期化を走らせる」運用を強く意識しています。

Domain Reload 無効では static 状態や static イベント購読が残留し得るため、`OnSingletonAwake()` は **再実行に耐える（idempotent）** 書き方が安全です。

> 実務上のコツ：static イベント購読は「解除→登録」の形にしておくと、Domain Reload 無効時の二重購読を潰しやすくなります。

## Threading / Main Thread（重要）🧵

`Instance` / `TryGetInstance` は内部で UnityEngine API（Find / GameObject 生成など）を呼びます。
これらは **メインスレッドから呼び出す前提**で運用してください。

## Initialization Order（初期化順の固定が必要な場合）⏱️

依存関係が複雑な場合、Bootstrap で順序を固定できます。
```csharp
using Singletons;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class Bootstrap : MonoBehaviour
{
    private void Awake()
    {
        _ = GameManager.Instance;
        _ = AudioManager.Instance;
        _ = InputManager.Instance;
    }
}
```

## Dependencies（本実装が依存する Unity API の挙動）🔍

| API | 挙動（デフォルト） |
| --- | --- |
| `Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude)` | **Assets / 非アクティブ / `HideFlags.DontSave` を返さない** |
| `Object.DontDestroyOnLoad()` | **root GameObject（または root 上の Component）でのみ有効** |
| `Application.quitting` | **Editor の Play Mode 終了時にも発火**。モバイルでは OS 都合で呼ばれないケースがあり得る |
| `RuntimeInitializeLoadType.SubsystemRegistration` | **最初のシーンロード前**に呼ばれる（ただし実行順は不定） |
| `Time.frameCount` | **Play Mode 開始時に 0 にリセット**。二重初期化ガードに利用 |
| `Application.isPlaying` | **Play Mode では `true`、Edit Mode では `false`** |
| Domain Reload 無効 | **static フィールド値 / static イベントハンドラが Play 間で残留** |
| Scene Reload 無効 | **`OnEnable` / `OnDisable` / `OnDestroy` 等は "新規ロード同様に呼ばれる"** |

## IDE Configuration（Rider / ReSharper）🧰

### `StaticMemberInGenericType` 警告

ジェネリック型内の static フィールドに対して警告が出ます。
これは「static が型引数ごとに分離される」ことへの注意喚起ですが、シングルトンでは **意図どおりの動作**です。

（チーム方針により、コメント抑制ではなく `.DotSettings` で Severity を調整する運用も有効です。）

## Testing（PlayMode テスト）🧪

`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` はテスト開始時にも実行されます。

* ✅ テスト間で `PlaySessionId` が更新され、キャッシュが無効化されるため、通常は問題なし
* ⚠️ テスト固有の初期化が必要なら、テスト環境用のガードを追加してください

## Platform Notes 📱

### Mobile (Android / iOS)

`Application.quitting` は OS 都合で呼ばれないケースがあり得ます。
必要に応じて `OnApplicationFocus` / `OnApplicationPause` を併用してください。

## FAQ ❓

### Q. なぜジェネリック側で `[RuntimeInitializeOnLoadMethod]` を使わないの？

ジェネリッククラス内の `RuntimeInitializeOnLoadMethod` が呼ばれないケースが知られており、
初期化は `SingletonRuntime`（非ジェネリック）に集約しています（Issue Tracker 参照）。

### Q. `Instance` を毎フレーム呼んでも動く？

動作はしますが推奨しません。`Start` / `Awake` などで取得してキャッシュしてください。

### Q. 派生で `Awake` を書いてしまったら？

`base.Awake()` を呼ばないと、`_instance` 設定・root 化・`DontDestroyOnLoad`・`OnSingletonAwake` 呼び出しがスキップされます。
必ず `base.Awake()` を呼ぶか、`OnSingletonAwake()` を使ってください。

### Q. `class A : PersistentSingletonBehaviour<B>` と書いたらどうなる？

CRTP 制約によりコンパイルエラー（CS0311）になります。型パラメータには必ず自分自身のクラスを指定してください。
加えて、ランタイムでも誤用検出（ガード）により早期に異常を検出します。

### Q. Edit Mode で `Instance` を呼んでも安全？

安全です。Edit Mode では検索のみ行い、static キャッシュの更新や自動生成は行いません。
派生型が見つかった場合も破棄せずログ出力のみで、Undo システムや Inspector に影響を与えません。

### Q. SceneSingletonBehaviour で未配置だとどうなる？

DEV/EDITOR では `InvalidOperationException` が発生します。Player では `null` を返します。
シーンに必ず配置してください。

## References 📚

### Unity Scripting API / Manual

* Domain Reload 無効時の挙動（static フィールド/イベントの残留）
  [https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html](https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html)
* Scene Reload 無効時の挙動（OnEnable/OnDisable/OnDestroy 等の呼び出し）
  [https://docs.unity3d.com/6000.3/Documentation/Manual/scene-reloading.html](https://docs.unity3d.com/6000.3/Documentation/Manual/scene-reloading.html)
* RuntimeInitializeOnLoadMethodAttribute
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html)
* RuntimeInitializeLoadType.SubsystemRegistration
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html)
* Time.frameCount
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-frameCount.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-frameCount.html)
* Application.isPlaying
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-isPlaying.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-isPlaying.html)
* Object.FindAnyObjectByType
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.FindAnyObjectByType.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.FindAnyObjectByType.html)
* Object.DontDestroyOnLoad
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.DontDestroyOnLoad.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.DontDestroyOnLoad.html)
* Application.quitting
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-quitting.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-quitting.html)
* DefaultExecutionOrder
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html)
* RuntimeInitializeOnLoadMethodAttribute not invoked if class is generic（1019360）
  [https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic](https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic)
