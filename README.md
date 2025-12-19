# README.md

### Unity SingletonBehaviour<T>

[Japanese](./README.ja.md) | [English](./README.en.md)

MonoBehaviour 向けのシングルトン基底クラスです。詳細は各言語の README を参照してください。

- ✅ 型ごとのシングルトン (`SingletonBehaviour<T>`)
- ✅ Domain Reload 無効（Enter Play Mode Options）でも安全な設計
- ✅ `Instance`（自動生成）/ `TryGetInstance`（生成しない）
- ✅ `Application.quitting` による終了時の安全性

----

A singleton base class for MonoBehaviour. See the language-specific README for details.

- ✅ Type-per-singleton (`SingletonBehaviour<T>`)
- ✅ Safe design even with Domain Reload disabled (Enter Play Mode Options)
- ✅ `Instance` (auto-creates) / `TryGetInstance` (no creation)
- ✅ Shutdown safety via `Application.quitting`
