# Unity SingletonBehaviour

[Japanese](./README.md) | [English](../en/README.md)

MonoBehaviour 向けの **型別（Type-per-singleton）シングルトン基底クラス**です。

Unity 6.3（6000.3 系）以降での利用を想定しています。

## Overview ✨

`SingletonBehaviour<T>` は次の機能を提供します：

- 🧩 **型ごとのシングルトン保証**（`GameManager` と `AudioManager` は別インスタンス）
- 🛡️ **型安全な継承**（CRTP 風制約 + ランタイムガードで誤用を検出）
- 🕰️ **遅延生成**（`Instance` アクセス時に未存在なら自動生成）
- 🔁 **シーン永続化**（`DontDestroyOnLoad`）
- 🧯 **終了時の安全性**（`Application.quitting` を考慮し、終了中の再生成を抑止）
- ⚙️ **Domain Reload 無効対応**（Play セッション識別子で型ごとの static キャッシュを無効化）
- 🧱 **誤配置への実用的な耐性**（子オブジェクト配置でも root に移動して永続化）
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

- `SingletonBehaviour<T>` と `SingletonRuntime` をプロジェクトに追加します（例：`Assets/Foundation/Singletons/`）。
- 名前空間はプロジェクト方針に合わせて調整してください。

### 名前空間のインポート
```csharp
using Foundation.Singletons;
```

## Design Intent（設計意図）🧠

### なぜ CRTP 制約を使うのか？

`SingletonBehaviour<T>` は以下の型制約を持ちます：
```csharp
public abstract class SingletonBehaviour<T> : MonoBehaviour
    where T : SingletonBehaviour<T>
```

これにより、誤った継承パターンがコンパイル時に検出されます：
```csharp
// ✅ 正しい実装
public sealed class GameManager : SingletonBehaviour<GameManager> { }

// ❌ コンパイルエラー（CS0311）
public sealed class A : SingletonBehaviour<B> { }
```

ただし C# の制約だけでは「誤って別型を指定した」などのケースを 100% 防ぎ切れないため、
**ランタイムガード**（`this as T` の検証）も併用して、運用上の事故を早期に検出します。

### なぜ `SingletonRuntime` が必要なのか？

Domain Reload を無効化すると、**static フィールドや static イベントのハンドラが Play 間で残留**し得ます。

この "残留" を前提に、Play セッションの開始ごとに **型ごとの static キャッシュを無効化**する必要があります。

そのため、

* Play 開始時に確実に呼ばれる非ジェネリックな場所で `PlaySessionId` を更新する（`SubsystemRegistration`）
* `SingletonBehaviour<T>` 側は `PlaySessionId` を参照して **キャッシュを無効化**する

という責務分離を行っています。

> 補足：Unity では「ジェネリック型内の `[RuntimeInitializeOnLoadMethod]` が期待どおり呼ばれない」ケースが知られており、
> その回避として非ジェネリック側に初期化を集約する設計は実用上有効です（Issue Tracker 参照）。

### Play セッション検出の仕組み

* 非ジェネリックな `SingletonRuntime.SubsystemRegistration`（`RuntimeInitializeLoadType.SubsystemRegistration`）が Play 開始前に必ず呼ばれる前提で、ここで `PlaySessionId` をインクリメント
* 同一フレーム内で二重に呼ばれた場合も `Time.frameCount` でガードして一度だけカウント
* `SingletonBehaviour<T>` 側は `PlaySessionId` を参照し、Play ごとに static キャッシュを無効化
* 初期化順が遅延した場合でも、`EnsureInitializedForCurrentPlaySession` がフォールバックしてフックを張り直し、`SynchronizationContext` が存在するときのみメインスレッド ID を遅延捕捉

### DontDestroyOnLoad の呼び出し管理

`DontDestroyOnLoad` は同一オブジェクトに複数回呼んでも問題ありませんが、
本実装では `_isPersistent` フラグで呼び出しを1回に制限し、不要な処理を回避しています。

## Dependencies（本実装が依存する Unity API の挙動）🔍

| API                                                          | 挙動（デフォルト）                                                          |
| ------------------------------------------------------------ | ------------------------------------------------------------------ |
| `Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude)` | **Assets / 非アクティブ / `HideFlags.DontSave` を返さない**（戻り値は呼び出し間で保証されない） |
| `Object.DontDestroyOnLoad()`                                 | **root GameObject（または root 上の Component）でのみ有効**                    |
| `Application.quitting`                                       | **Editor の Play Mode 終了時にも発火**。Android では pause 中に検出されない場合がある      |
| `RuntimeInitializeLoadType.SubsystemRegistration`            | **最初のシーンロード前**に呼ばれる（ただし実行順は不定）                                     |
| `Time.frameCount`                                            | **Play Mode 開始時に 0 にリセット**。二重初期化ガードに利用                         |
| `Application.isPlaying`                                      | **Play Mode では `true`、Edit Mode では `false`**                       |
| Domain Reload 無効                                             | **static フィールド値 / static イベントハンドラが Play 間で残留**                     |
| Scene Reload 無効                                              | **`OnEnable` / `OnDisable` / `OnDestroy` 等は "新規ロード同様に呼ばれる"**       |

## Public API 📌

### `static T Instance { get; }`

必須依存向け。シングルトンインスタンスを返します。未存在の場合は **検索 → 無ければ自動生成**します。終了中（quitting）やバックグラウンドスレッドでは `null` を返します。DEV/EDITOR では非アクティブが存在する場合に自動生成をブロック（例外）し、実体型が T と厳密一致しない候補は拒否します。
```csharp
GameManager.Instance.AddScore(10);
```

| 状態         | 戻り値             |
| ---------- | --------------- |
| インスタンス存在   | キャッシュ済みインスタンス   |
| 未存在        | 検索 → 無ければ生成して返却 |
| quitting 中 | `null`          |
| Edit Mode  | 検索のみ（生成・キャッシュなし） |
| 非アクティブが存在（DEV/EDITOR） | 例外（重複を防止） |
| 派生型が見つかった | `null`（破棄／拒否） |

### `static bool TryGetInstance(out T instance)`

任意依存向け。インスタンスが存在すれば取得します。**生成は行いません**。終了中（quitting）やバックグラウンドスレッドでは `false` を返します。実体型が T と厳密一致しない候補は拒否します。
```csharp
if (AudioManager.TryGetInstance(out var am))
{
    am.PlaySe("click");
}
```

| 状態         | 戻り値     | `instance` |
| ---------- | ------- | ---------- |
| インスタンス存在   | `true`  | 有効な参照      |
| 未存在        | `false` | `null`     |
| quitting 中 | `false` | `null`     |
| Edit Mode  | 検索結果    | 検索のみ（キャッシュなし） |
| 派生型が見つかった | `false` | `null`（拒否）  |

**典型ユースケース：終了処理での「うっかり生成」を防止 🧹**
```csharp
private void OnDisable()
{
    if (AudioManager.TryGetInstance(out var am))
    {
        am.Unregister(this);
    }
}
```

## Usage 🚀

### 1) 派生クラスの定義
```csharp
using Foundation.Singletons;

public sealed class GameManager : SingletonBehaviour<GameManager>
{
    public int Score { get; private set; }

    protected override void OnSingletonAwake()
    {
        // Playごとに確実に初期化したい処理
        this.Score = 0;
    }

    public void AddScore(int value) => this.Score += value;

    protected override void OnSingletonDestroy()
    {
        // 本当に破棄されるタイミングでの後始末（リソース解放、イベント解除など）
    }
}
```

| 項目     | 推奨                                     |
| ------ | -------------------------------------- |
| クラス修飾子 | `sealed`（意図しない継承事故を防ぐ）                 |
| 初期化処理  | `OnSingletonAwake()` に記述（Play ごとの再初期化） |
| 破棄処理   | `OnSingletonDestroy()` に記述（破棄時のみ）      |

---

### 2) `Instance` / `TryGetInstance` の使い分け

* ✅ **Instance**：その依存が「必ず必要」なとき（無ければ作ってでも動かす）
  例：`GameManager`, `InputManager` などゲーム進行に必須のマネージャ

* ✅ **TryGetInstance**：「あるなら使う」「無いなら何もしない」「終了処理で増やしたくない」
  例：`OnDisable` / `OnDestroy` / `OnApplicationPause` などの後片付け、任意機能の登録解除

> DEV/EDITOR では非アクティブが存在する場合に `Instance` が例外でブロックされます。終了処理や任意依存には `TryGetInstance` を使うと安全です。

---

### 3) アクセスパターン（キャッシュ徹底）🧠

❌ **毎フレーム `Instance` を呼ぶのは非推奨**です。探索が走る可能性があるため、初回に取得してキャッシュし、以降は参照を使うのが基本です。

✅ 推奨：初回に取得してキャッシュ
```csharp
using Foundation.Singletons;
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

## Soft Reset（Playごとの安全な再初期化）🧼

本実装は「同一個体を再利用しつつ、Play ごとに初期化を走らせる」運用を強く意識しています。

Domain Reload 無効では static 状態や static イベント購読が残留し得るため、`OnSingletonAwake()` は **再実行に耐える（idempotent）** 書き方が安全です。

> 実務上のコツ：static イベント購読は「解除→登録」の形にしておくと、Domain Reload 無効時の二重購読を潰しやすくなります。

## Constraints（重要な制約）⚠️

### ❌ 派生クラスで `Awake()` / `OnEnable()` / `OnDestroy()` を定義しない

基底クラスの Unity メッセージ関数は以下を担当しています：

* `_instance` の確立・重複排除
* Play セッションの検出と static キャッシュ無効化
* root 化（`DontDestroyOnLoad` の前提を満たす）
* `DontDestroyOnLoad` の適用
* `OnSingletonAwake` / `OnSingletonDestroy` の呼び出し制御（Playごとのソフトリセット）

派生側で `Awake()` / `OnEnable()` / `OnDestroy()` を定義すると、**基底の処理がスキップされて破綻**します。
初期化は `OnSingletonAwake()`、破棄時処理は `OnSingletonDestroy()` を使用してください。

> Unity のメッセージ関数は `virtual/override` ではなく「名前ベース」で呼ばれるため、言語機構で完全に禁止できません。チーム規約や IDE 検査で担保してください。

### ❌ 型パラメータには自分自身を指定する

CRTP 制約により、以下のような誤った継承はコンパイルエラーになります：
```csharp
// ❌ コンパイルエラー
public sealed class A : SingletonBehaviour<B> { }

// ✅ 正しい実装
public sealed class A : SingletonBehaviour<A> { }
```

## Scene Placement Notes 🧱

| 制約                    | 理由                               |
| --------------------- | -------------------------------- |
| 複数シーンに同一シングルトンを配置しない  | 初期化順で片方が Destroy される（先着が勝つ）      |
| root GameObject が望ましい | `DontDestroyOnLoad` は root にのみ有効 |

本実装は、誤って子オブジェクトに配置された場合でも **自動で root に移動**して永続化します。
ただし意図しない移動は混乱の元になり得るため、**Editor/Development ビルドのみ**警告ログを出す運用が合理的です（本実装もその方針）。

## Edit Mode Behavior 🖥️

Edit Mode（`Application.isPlaying == false`）では以下の動作になります：

* `Instance` / `TryGetInstance` は **検索のみ**実行（`FindAnyObjectByType`）
* **自動生成しない**
* **static キャッシュを更新しない**（副作用ゼロ）
* **Play セッション状態に影響しない**

これにより、エディタスクリプトやカスタムインスペクタから安全にシングルトンを参照できます。

## Threading / Main Thread（重要）🧵

`Instance` / `TryGetInstance` は内部で UnityEngine API（Find / GameObject 生成など）を呼びます。
これらは **メインスレッドから呼び出す前提**で運用してください。

## Initialization Order（初期化順の固定が必要な場合）⏱️

依存関係が複雑な場合、Bootstrap で順序を固定できます。
```csharp
using Foundation.Singletons;
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

### Android

`Application.quitting` は pause 中に検出されない場合があります。

必要に応じて `OnApplicationFocus` / `OnApplicationPause` を併用してください。

## FAQ ❓

### Q. なぜジェネリック側で `[RuntimeInitializeOnLoadMethod]` を使わないの？

ジェネリッククラス内の `RuntimeInitializeOnLoadMethod` が呼ばれないケースが知られており、
初期化は `SingletonRuntime`（非ジェネリック）に集約しています（Issue Tracker 参照）。

### Q. `Instance` を毎フレーム呼んでも動く？

動作はしますが推奨しません。`Start` / `Awake` などで取得してキャッシュしてください。

### Q. 派生で `Awake` を書いてしまったら？

基底の `Awake` が呼ばれず、`_instance` 設定・root 化・`DontDestroyOnLoad`・`OnSingletonAwake` 呼び出しがスキップされます。
`Awake` を削除し、`OnSingletonAwake()` を使ってください（`OnEnable` / `OnDestroy` も同様）。

### Q. `class A : SingletonBehaviour<B>` と書いたらどうなる？

CRTP 制約によりコンパイルエラー（CS0311）になります。型パラメータには必ず自分自身のクラスを指定してください。
加えて、ランタイムでも誤用検出（ガード）により早期に異常を検出します。

### Q. Edit Mode で `Instance` を呼んでも安全？

安全です。Edit Mode では検索のみ行い、static キャッシュの更新や自動生成は行いません。

### Q. `RuntimeInitializeOnLoadMethod` の実行順が不定なのに、なぜ動く？

非ジェネリックの `SubsystemRegistration` が Play 開始前に走り、`Time.frameCount` で同一フレームの二重実行を抑止しています。加えて、`SingletonBehaviour<T>` 側で `EnsureInitializedForCurrentPlaySession` を都度呼び、初期化が遅れた場合でもフックを再設定するフォールバックを持っています。

## References 📚

### Unity Scripting API / Manual

* Domain Reload 無効時の挙動（static フィールド/イベントの残留）
  [https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html](https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html)
* Scene Reload 無効時の挙動（OnEnable/OnDisable/OnDestroy 等の呼び出し）
  [https://docs.unity3d.com/6000.2/Documentation/Manual/scene-reloading.html](https://docs.unity3d.com/6000.2/Documentation/Manual/scene-reloading.html)
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
* MonoBehaviour.StopAllCoroutines
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.StopAllCoroutines.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.StopAllCoroutines.html)
* MonoBehaviour.CancelInvoke
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.CancelInvoke.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.CancelInvoke.html)
* SceneManager.sceneLoaded
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager-sceneLoaded.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.SceneManager-sceneLoaded.html)
* DefaultExecutionOrder
  [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html)
* RuntimeInitializeOnLoadMethodAttribute not invoked if class is generic（1019360）
  [https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic](https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic)
