# Unity SingletonBehaviour

[日本語](./README.md) | [英語](./README.en.md)

MonoBehaviour 向けのシングルトン基底クラスです。Unity 6.3（6000.3 系）以降での利用を想定しています。

## Overview ✨

`SingletonBehaviour<T>` は次の機能を提供します：

- 🧩 **型ごとのシングルトン保証**（`GameManager` と `AudioManager` は別インスタンス）
- 🕰️ **遅延生成**（`Instance` アクセス時に未存在なら自動生成）
- 🔁 **シーン永続化**（`DontDestroyOnLoad`）
- 🧯 **終了時の安全性**（`Application.quitting` で再生成を抑止）
- ⚙️ **Domain Reload 無効対応**（Play セッション識別子でキャッシュを無効化）
- 🧱 **誤配置への実用的な耐性**（子オブジェクト配置でも root に移動して永続化）

## Requirements ✅

- Unity 6.3 (6000.3.x) 以降
- Enter Play Mode Options で Domain Reload を無効化しても破綻しにくい設計

## Design Intent（設計意図）🧠

### なぜ `SingletonRuntime` が必要なのか？

`SingletonBehaviour<T>` は **ジェネリック型**です。Unity には「`[RuntimeInitializeOnLoadMethod]` をジェネリック型の中に置けない（または期待通りに呼ばれない）」という制約があり、実際に Unity 側でも既知問題として扱われています。

そのため「起動タイミングで確実に呼べる非ジェネリックな場所」に初期化責務を集約する目的で、`SingletonRuntime` を用意しています。

- `SingletonRuntime.Initialize()` は `SubsystemRegistration` で実行される
- そこで `PlaySessionId` を更新し、`SingletonBehaviour<T>` 側がそれを見て **型ごとの static キャッシュを無効化**する

この分離により、単一責任（SRP）を維持したまま、Domain Reload 無効時の “static 残留” 問題に現実的に対処します。

## Dependencies（本実装が依存する Unity API の挙動）🔍

| API | 挙動（デフォルト） |
|-----|-------------------|
| `Object.FindAnyObjectByType<T>()` | Assets / 非アクティブ / `HideFlags.DontSave` を返さない |
| `Object.DontDestroyOnLoad()` | root GameObject（またはその Component）でのみ有効 |
| `Application.quitting` | Editor の Play Mode 終了時にも発火。Android では pause 中に未検出の場合あり |
| `RuntimeInitializeLoadType.SubsystemRegistration` | 最初のシーンロード前に呼ばれる |

## Public API 📌

### `static T Instance { get; }`

必須依存向け。シングルトンインスタンスを返します。未存在の場合は **自動生成**します。quitting 中は `null` を返します。

```csharp
GameManager.Instance.AddScore(10);
````

| 状態         | 戻り値             |
| ---------- | --------------- |
| インスタンス存在   | キャッシュ済みインスタンス   |
| 未存在        | 検索 → 無ければ生成して返却 |
| quitting 中 | `null`          |

---

### `static bool TryGetInstance(out T instance)`

任意依存向け。インスタンスが存在すれば取得します。**生成は行いません**。

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
public sealed class GameManager : SingletonBehaviour<GameManager>
{
    public int Score { get; private set; }

    protected override void OnSingletonAwake()
    {
        Score = 0;
    }

    public void AddScore(int value) => Score += value;

    protected override void OnSingletonDestroy()
    {
        // リソース解放、イベント解除など
    }
}
```

| 項目     | 推奨                         |
| ------ | -------------------------- |
| クラス修飾子 | `sealed`（意図しない継承事故を防ぐ）     |
| 初期化処理  | `OnSingletonAwake()` に記述   |
| 破棄処理   | `OnSingletonDestroy()` に記述 |

---

### 2) `Instance` / `TryGetInstance` の使い分け

* ✅ **Instance**：その依存が「必ず必要」なとき（無ければ作ってでも動かす）
  例：`GameManager`, `InputManager` などゲーム進行に必須のマネージャ

* ✅ **TryGetInstance**：「あるなら使う」「無いなら何もしない」「終了処理で増やしたくない」
  例：`OnDisable` / `OnDestroy` / `OnApplicationPause` などの後片付け、任意機能の登録解除

---

### 3) アクセスパターン（キャッシュ徹底）🧠

❌ **毎フレーム `Instance` を呼ぶのは非推奨**です。
探索が走る可能性があるため、初回に取得してキャッシュし、以降は参照を使うのが基本です。

✅ 推奨：初回に取得してキャッシュ

```csharp
public sealed class ScoreHUD : MonoBehaviour
{
    private GameManager _gm;

    private void Start()
    {
        _gm = GameManager.Instance; // キャッシュ
    }

    private void Update()
    {
        if (_gm == null) return;
        // _gm.Score を使用
    }
}
```

## Constraints（重要な制約）⚠️

### ❌ 派生クラスで `Awake()` / `OnDestroy()` を定義しない

基底クラスの `Awake` / `OnDestroy` は以下を担当しています：

* `_instance` の確立・重複排除
* root 化（`DontDestroyOnLoad` の前提を満たすため）
* `DontDestroyOnLoad` の適用
* `OnSingletonAwake` / `OnSingletonDestroy` の呼び出し

派生側で `Awake()` / `OnDestroy()` を定義すると、**基底の処理がスキップされて破綻します**。

💡 Unity のメッセージ関数は C# の `virtual/override` ではなく「名前ベース」で呼ばれるため、言語機構で完全に禁止できません。チーム規約や IDE 検査で担保してください。

## Scene Placement Notes 🧱

| 制約                    | 理由                      |
| --------------------- | ----------------------- |
| 複数シーンに同一シングルトンを配置しない  | 後から読まれた方が Destroy される   |
| root GameObject が望ましい | `DontDestroyOnLoad` の仕様 |

本実装は、誤って子オブジェクトに配置された場合でも **自動で root に移動**して永続化します。

ただし、意図しない移動は混乱の元になり得るため、**Editor/Development ビルドのみ**警告ログを出すのが合理的です（本実装もその方針です）。

## Threading / Main Thread（重要）🧵

`Instance` / `TryGetInstance` は内部で UnityEngine API（Find / GameObject 生成など）を呼びます。
これらは **メインスレッドから呼び出す前提**で運用してください。

## Initialization Order（初期化順の固定が必要な場合）⏱️

依存関係が複雑な場合、Bootstrap で順序を固定できます。

```csharp
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
これは「static が型引数ごとに分離される」ことへの注意喚起ですが、シングルトンでは **意図通りの動作**です。

（チーム方針により、コメント抑制ではなく `.DotSettings` で Severity を調整する運用も有効です。）

## Testing（PlayMode テスト）🧪

`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` はテスト開始時にも実行されます。

* ✅ テスト間で `PlaySessionId` が更新され、キャッシュが無効化されるため、通常は問題なし
* ⚠️ テスト固有の初期化が必要ならガード処理を追加

## Platform Notes 📱

### Android

`Application.quitting` は pause 中に検出されない場合があります。
必要に応じて `OnApplicationFocus` / `OnApplicationPause` を併用してください。

## FAQ ❓

### Q. なぜジェネリック側で `[RuntimeInitializeOnLoadMethod]` を使わないの？

Unity の制約で、ジェネリッククラス内では呼び出されません。
代わりに `SingletonRuntime`（非ジェネリック）で `PlaySessionId` を更新し、各 `SingletonBehaviour<T>` がそれを見てキャッシュを無効化します。

### Q. `Instance` を毎フレーム呼んでも動く？

動作はしますが推奨しません。`Start` / `Awake` などで取得してキャッシュしてください。

### Q. 派生で `Awake` を書いてしまったら？

基底の `Awake` が呼ばれず、`_instance` 設定・root 化・`DontDestroyOnLoad`・`OnSingletonAwake` 呼び出しがすべてスキップされます。 `Awake` を削除し、`OnSingletonAwake()` を使ってください。

## References 📚

* RuntimeInitializeOnLoadMethodAttribute: [https://docs.unity3d.com/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html](https://docs.unity3d.com/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html)
* RuntimeInitializeLoadType.SubsystemRegistration (Unity 6.3): [https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RuntimeInitializeLoadType.SubsystemRegistration.html)
* Object.FindAnyObjectByType: [https://docs.unity3d.com/ScriptReference/Object.FindAnyObjectByType.html](https://docs.unity3d.com/ScriptReference/Object.FindAnyObjectByType.html)
* Object.DontDestroyOnLoad: [https://docs.unity3d.com/ScriptReference/Object.DontDestroyOnLoad.html](https://docs.unity3d.com/ScriptReference/Object.DontDestroyOnLoad.html)
* Application.quitting: [https://docs.unity3d.com/ScriptReference/Application-quitting.html](https://docs.unity3d.com/ScriptReference/Application-quitting.html)
* Application.logMessageReceivedThreaded (thread safety note): [https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Application-logMessageReceivedThreaded.html](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Application-logMessageReceivedThreaded.html)
* GameObject.Find (Update での使用非推奨): [https://docs.unity3d.com/ScriptReference/GameObject.Find.html](https://docs.unity3d.com/ScriptReference/GameObject.Find.html)
* Issue Tracker: RuntimeInitializeOnLoadMethodAttribute not invoked if class is generic: [https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic](https://issuetracker.unity3d.com/issues/runtimeinitializeonloadmethodattribute-not-invoked-if-class-is-generic)
* Unity Discussions: “RuntimeInitializeOnLoad methods cannot be in generic classes” (error report): [https://discussions.unity.com/t/method-init-is-in-a-generic-class-but-runtimeinitializeonload-methods-cannot-be-in-generic-classes/1698250](https://discussions.unity.com/t/method-init-is-in-a-generic-class-but-runtimeinitializeonload-methods-cannot-be-in-generic-classes/1698250)

## License

MIT
