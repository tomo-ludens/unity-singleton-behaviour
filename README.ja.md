# ポリシー駆動型Unityシングルトン

[English README](./README.md)

MonoBehaviour 向けの **ポリシー駆動型シングルトン基底クラス**です。

Unity 6.3（6000.3 系）以降での利用を想定しており、特に **Enter Play Mode Options で Reload Domain を無効化した環境**でも破綻しにくい堅牢な設計を目的としています。

## Requirements / 動作環境

* **Unity 6.3** (6000.3.x) 以降
* **Enter Play Mode Options** の **Reload Domain** を無効化した環境に完全対応
* （任意）Reload Scene を無効化した場合でも、Play ごとに再初期化される運用を想定

## Overview / 概要

本ライブラリは、用途別に 2 種類のシングルトン基底クラスを提供します。

共通のコアロジックを持ちながら、**ポリシー**によってライフサイクル（永続化や自動生成の有無）を制御します。

### 提供クラス

| クラス | シーン間永続 | 自動生成 | 用途 |
| --- | --- | --- | --- |
| **`PersistentSingletonBehaviour<T>`** | ✅ する | ✅ する | ゲーム全体で常に存在するマネージャ（GameManager など） |
| **`SceneSingletonBehaviour<T>`** | ❌ しない | ❌ しない | 特定のシーン内でのみ動作するコントローラ（LevelController など） |

### 主な特長

* **ポリシー駆動**: 永続化（DontDestroyOnLoad）や自動生成の挙動をポリシーで分離します。
* **Domain Reload 無効化対応**: static フィールドが残留する環境でも、Play セッションIDを用いて確実にキャッシュをリセットします。
* **安全なライフサイクル**:
  * **終了処理**: `Application.quitting` を考慮し、終了中の生成やアクセスを防ぎます。
  * **Edit Mode**: エディタ実行中は「検索のみ」を行い、生成や static キャッシュ更新といった副作用を起こしません。
  * **再初期化 (Soft Reset)**: 状態リセットは **Play セッション境界**で行い、Play ごとに `OnSingletonAwake()` を実行して状態を初期化します（方針は `PlaySessionId` に寄せています）。
* **厳密な型チェック**: ジェネリック型 `T` と実体型が一致しない参照は拒否し、誤用を防ぎます。
* **開発時の安全性 (DEV/EDITOR)**:
  * `FindAnyObjectByType(...Exclude)` が **非アクティブを見ない**ため、非アクティブなシングルトンが存在すると「見つからない扱い → 自動生成 → 隠れ重複」になり得ます。これを避けるため、DEV/EDITOR では非アクティブ検出時に **fail-fast（例外）** にします。
  * SceneSingleton をシーンに置き忘れた状態でアクセスした場合も、DEV/EDITOR では **fail-fast（例外）** にします。
* **リリースビルド最適化**: ログや例外チェックはストリップされ、エラー時は `null` / `false` を返す設計です（利用側はハンドリングが必要です）。

## Directory Structure / ディレクトリ構成

```text
Singletons/
├── PersistentSingletonBehaviour.cs   # Public API (永続・自動生成あり)
├── SceneSingletonBehaviour.cs        # Public API (シーン限定・自動生成なし)
├── Core/
│   ├── SingletonBehaviour.cs         # コア実装
│   ├── SingletonRuntime.cs           # 内部ランタイム (Domain Reload対策)
│   └── SingletonLogger.cs            # 条件付きロガー (リリースで除去)
└── Policy/
    ├── ISingletonPolicy.cs           # ポリシーIF
    ├── PersistentPolicy.cs           # 永続ポリシーの実装
    └── SceneScopedPolicy.cs          # シーンスコープポリシーの実装
````

## Dependencies / 前提としている Unity API の挙動

本実装は、次の Unity API 仕様を前提に設計しています（Unity 側の挙動が変わると設計の前提も変わります）。

| API / 機能                                                     | 前提としている挙動                                                                                                           |
| ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------- |
| Domain Reload 無効                                             | **static 変数**と **static event の購読**が Play 間で残ります。これを前提に `PlaySessionId` でキャッシュを無効化します。                              |
| Scene Reload 無効                                              | Scene Reload を無効にすると、シーンは再読み込みされません。**通常起動時と同じコールバック順（新規ロード前提）を期待しません**。状態リセットは Play セッション境界（`PlaySessionId`）に寄せます。 |
| `Object.FindAnyObjectByType<T>(FindObjectsInactive.Exclude)` | 既定では **非アクティブは対象外**です。そのため非アクティブなシングルトンは「存在しても見つからない」扱いになり得ます。これが DEV/EDITOR での fail-fast 方針につながります。                |
| `Object.DontDestroyOnLoad`                                   | **ルート GameObject**に対して適用する必要があります（そのため Persistent は必要に応じてルートへ移動します）。                                                |

## Installation / インストール

1. プロジェクトの任意の場所（例: `Assets/Plugins/Singletons/`）に `Singletons` フォルダを配置してください。
2. 必要に応じて名前空間やアセンブリ定義（Assembly Definition）を調整してください。

## Usage / 使い方

### 1. Persistent Singleton（永続シングルトン）

シーンを跨いで生存し、アクセス時に見つからなければ自動生成します。

```csharp
using Singletons;

// 継承禁止 (sealed) を推奨します
public sealed class GameManager : PersistentSingletonBehaviour<GameManager>
{
    public int Score { get; private set; }

    // Awake の代わりに OnSingletonAwake を使用します
    protected override void OnSingletonAwake()
    {
        // Play セッションごとに必ず走る初期化処理
        Score = 0;
    }

    protected override void OnSingletonDestroy()
    {
        // 実体が破棄されるときだけ呼ばれます
    }

    public void AddScore(int value) => Score += value;
}

// 利用例:
// GameManager.Instance.AddScore(10);
```

### 2. Scene-scoped Singleton（Sceneスコープシングルトン）

Scene 上に配置して使用します。自動生成は行わず、Scene アンロードと共に破棄されます。

```csharp
using Singletons;

public sealed class LevelController : SceneSingletonBehaviour<LevelController>
{
    protected override void OnSingletonAwake()
    {
        // Sceneごとの初期化
    }
}

// ⚠️ Sceneに配置必須
// 置き忘れた場合、DEV/EDITORでは例外、リリースビルドでは null になります
// LevelController.Instance.DoSomething();
```

### 3. `Instance` と `TryGetInstance` の使い分け（典型例）

リリースビルドでは検証例外がストリップされ、エラー時に `null` / `false` が返ります。利用者に刺さる運用は「どこで `Instance` を使い、どこで `TryGetInstance` に倒すか」を明確にすることです。

| 使い分け                 | 原則                                                | 代表例                                                                      |
| -------------------- | ------------------------------------------------- | ------------------------------------------------------------------------ |
| **`Instance`**       | その機能が **必須** で、存在しない状態を許容しません。                    | ゲーム進行に必須の Manager（`GameManager`, `AudioManager` など）の **起動時/初期化時**        |
| **`TryGetInstance`** | 「あるなら使う」「ないなら何もしない」を徹底します。<br>勝手な生成・復活・順序依存を避けます。 | **後片付け・解除・一時停止/復帰**（`OnDisable` / `OnDestroy` / `OnApplicationPause` など） |

#### 典型例: 解除系は TryGetInstance を原則にします

```csharp
private void OnDisable()
{
    if (AudioManager.TryGetInstance(out var am))
    {
        am.Unregister(this);
    }
}

private void OnDestroy()
{
    if (GameManager.TryGetInstance(out var gm))
    {
        gm.Unregister(this);
    }
}

private void OnApplicationPause(bool paused)
{
    if (paused && Telemetry.TryGetInstance(out var t))
    {
        t.Flush();
    }
}
```

#### 典型例: 起動時に必要なものは Instance で確実に確立します（キャッシュ前提）

```csharp
private GameManager _gm;

private void Awake()
{
    _gm = GameManager.Instance; // 必須なので Instance
}

private void Update()
{
    if (_gm == null) return; // リリースでは null になり得るため保険
    // ...
}
```

### 4. キャッシュの推奨（重要）

`Instance` は内部で検索や検証を行うため、**`Update` 等で毎フレーム呼び出す運用は避けてください**。`Start` / `Awake` で取得してキャッシュし、その参照を使ってください。

```csharp
public class Player : MonoBehaviour
{
    private GameManager _gameManager;

    private void Start()
    {
        _gameManager = GameManager.Instance; // ここでキャッシュ
    }

    private void Update()
    {
        if (_gameManager == null) return;
        // _gameManager.DoSomething();
    }
}
```

## Public API Details

### `static T Instance { get; }`

| 状態              | 挙動                                                            |
| --------------- | ------------------------------------------------------------- |
| **Play 中 (正常)** | 確立済みならキャッシュを返します。未確立なら検索し、Persistent は必要なら生成します。              |
| **終了処理中**       | 常に `null` を返します。                                              |
| **Edit Mode**   | 検索のみ行います（生成せず、static キャッシュも更新しません）。                           |
| **非アクティブを検出**   | DEV/EDITOR では例外、Player では `null` です。                          |
| **型不一致**        | 派生型など型が一致しない参照は拒否し、`null` を返します（Play 中は破棄します）。                |
| **Scene に置き忘れ** | SceneSingleton が見つからない場合、DEV/EDITOR では例外、Player では `null` です。 |

### `static bool TryGetInstance(out T instance)`

インスタンスが存在すれば取得します。**未生成時の自動生成は行いません**。

| 状態            | 挙動                      |
| ------------- | ----------------------- |
| **存在すれば**     | `true` と参照を返します。        |
| **存在しなければ**   | `false` と `null` を返します。 |
| **終了処理中**     | 常に `false` です。          |
| **Edit Mode** | 検索のみ行います（キャッシュしません）。    |

## Design Intent / 設計意図（補足）

### なぜポリシーで挙動を分けるのですか？

永続化・自動生成などの「挙動」をポリシー（`ISingletonPolicy`）で分離し、コアロジックを共有するためです。

### なぜ `SingletonRuntime` が必要なのですか？

Domain Reload を無効化すると、static フィールドや static イベント購読が Play 間で残る前提になります。そのため、Play 開始ごとに **型ごとの static キャッシュを無効化する仕組み** が必要です。

1. Play 開始時に確実に呼ばれる場所（`SubsystemRegistration`）で `PlaySessionId` を更新します。
2. シングルトン側は `PlaySessionId` を参照して、古いセッションのキャッシュを破棄・再検索します。

### なぜ `SingletonRuntime` に初期化を集約するのですか？

Domain Reload を無効化すると、Play のたびに「static が初期状態に戻る」保証がありません。Unity 公式ドキュメントでも、Domain Reload 無効時は static 変数と static event の購読が保持されることが明記されています。

そのため本実装では、Play の開始ごとに `PlaySessionId` を更新し、`SingletonBehaviour` 側で「古いセッションの static キャッシュ」を確実に無効化します。

また、Unity では **ジェネリック型に付けた `RuntimeInitializeOnLoadMethod` が期待どおり呼ばれない**問題が報告されています（Issue Tracker）。そのため、初期化は非ジェネリック側（`SingletonRuntime`）へ集約します。

## Constraints & Best Practices / 制約と推奨事項

### 1. 具象クラスは `sealed` を推奨します

具象シングルトン（例：`GameManager`）をさらに継承することは推奨しません。
`class Derived : GameManager` のような継承は型チェック機構により実行時に拒否されます。

### 2. Unity メッセージのオーバーライド時は `base` 呼び出しが必須です

`Awake`, `OnEnable`, `OnDestroy` をオーバーライドする場合、**必ず基底クラス（base）のメソッドを呼んでください**。

```csharp
protected override void Awake()
{
    base.Awake(); // 必須です
    // 追加の初期化
}
```

呼ばない場合でも、最初のアクセス時に初期化が走る「保険」はありますが、初期化順が見えにくくなるため非推奨です。基本的には `OnSingletonAwake` / `OnSingletonDestroy` を使用してください。

### 3. 配置上の注意

* **多重配置しないでください**: 同一シングルトンを複数シーンに置かないでください（初期化順により、後から読み込まれた方が破棄されます）。
* **Persistent はルート配置が前提です**: 子オブジェクトに付いていた場合でも自動でルートへ移動して永続化しますが、DEV/EDITOR では警告ログが出ます。
* **無効のまま運用しないでください**: シングルトンコンポーネントを Disabled のまま置く運用は避けてください（見つからない扱いになり、隠れ重複の原因になります）。

## Advanced Topics

### Soft Reset（Play ごとの再初期化）

Domain Reload 無効環境では static 状態が残ります。本実装は Play セッション境界（`PlaySessionId`）でキャッシュを無効化し、Play ごとに `OnSingletonAwake()` を実行して状態を初期化します。

`OnSingletonAwake()` は **再実行に耐える（冪等）** 書き方にしてください（例：イベント購読は「解除 → 登録」で行います）。

### Threading / Main Thread

`Instance` / `TryGetInstance` は UnityEngine API（Find / GameObject 生成など）を呼ぶため、Play 中は **メインスレッドからの呼び出し** が必須です。

### Initialization Order

初期化順序を厳密に制御したい場合は、`DefaultExecutionOrder` を設定した Bootstrap クラス等で順序を固定してください。

```csharp
[DefaultExecutionOrder(-10000)]
public class Bootstrap : MonoBehaviour
{
    void Awake()
    {
        _ = GameManager.Instance;
        _ = AudioManager.Instance;
        _ = InputManager.Instance;
    }
}
```

## Edit Mode の挙動（詳細）

Edit Mode（`Application.isPlaying == false`）では、次の挙動に固定しています。

* `Instance` / `TryGetInstance` は **検索のみ**行います（自動生成しません）。
* **static キャッシュは更新しません**（副作用を発生させません）。
* そのため、カスタムインスペクタやエディタ拡張から参照しても、Play モードの状態に影響しません。

> 補足: 本実装が使用する `FindAnyObjectByType<T>(FindObjectsInactive.Exclude)` は、既定では **非アクティブを対象外**にします。非アクティブなシングルトンが紛れていると「見つからない扱い」になり得るため、DEV/EDITOR では fail-fast を選びます。

## IDE Configuration（Rider / ReSharper）

### `StaticMemberInGenericType` 警告について

`SingletonBehaviour<T, TPolicy>` の `static` フィールド（`_instance` など）は、ジェネリック型のインスタンス化ごとに分離されます。

シングルトンの用途では **意図どおりの挙動**なので、チーム方針に合わせて次のどちらかに統一してください。

* コード上の抑制コメントで運用
* `.DotSettings` 等で Severity を調整

## Testing（PlayMode テスト）

`RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)` は、PlayMode テスト環境でも実行されます。

* テスト間で `PlaySessionId` が進むため、通常は static キャッシュが残っても破綻しにくい設計です。
* ただし、テストコード側で static な購読やキャッシュを持つ場合は、テストの `TearDown` 等で解除・初期化を徹底してください。

## FAQ

**Q. `Instance` を毎フレーム呼んでも動きますか？**
動きますが推奨しません。`Start` / `Awake` 等で取得してキャッシュしてください。

**Q. `Awake` を書いて `base.Awake()` を呼び忘れたらどうなりますか？**
確立処理が遅れ、最初の `Instance` / `TryGetInstance` アクセス時に初期化されます。動作はしますが、初期化タイミングが予期せず遅れるため `base` 呼び出しを徹底してください。

**Q. シーンに置き忘れたらどうなりますか？**
SceneSingleton は DEV/EDITOR で例外、Player では `null` / `false` です。PersistentSingleton は見つからなければ自動生成します。

## References

Domain Reload（Manual）
[https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html](https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html)

Scene Reload（Manual）
[https://docs.unity3d.com/6000.2/Documentation/Manual/scene-reloading.html](https://docs.unity3d.com/6000.2/Documentation/Manual/scene-reloading.html)

RuntimeInitializeOnLoadMethodAttribute（Scripting API）
[https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html)

RuntimeInitializeLoadType.SubsystemRegistration（Scripting API）
[https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html)

Object.DontDestroyOnLoad（Scripting API）
[https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.DontDestroyOnLoad.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.DontDestroyOnLoad.html)

Application.quitting（Scripting API）
[https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-quitting.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-quitting.html)

DefaultExecutionOrder（Scripting API）
[https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/DefaultExecutionOrder.html)

Unity Issue Tracker: RuntimeInitializeOnLoadMethodAttribute not invoked if class is generic
[https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic](https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic)

## License

See [LICENSE](./LICENSE).
