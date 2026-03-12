# DependencyContractAnalyzer

英語版 README: [README.md](README.md)

`DependencyContractAnalyzer` は、型間の依存関係に対して契約を宣言的に付与し、その契約が依存先で満たされているかを DI 登録解析に頼らず静的解析で検証する Roslyn Analyzer パッケージです。

このツールの中核コンセプトは次です。

`型依存は、宣言された契約を満たす場合にのみ許可される。`

## 現在の状態

- 初回リリースに向けた OSS 用ドキュメント一式を先行整備しています
- 想定パッケージ ID は `DependencyContractAnalyzer` です
- 初期実装スコープは [docs/specification.ja.md](docs/specification.ja.md) にまとめています
- 最終完成形の設計は [docs/architecture.ja.md](docs/architecture.ja.md) にまとめています

## 免責事項

- アーキテクチャ検証および依存契約検証を目的とした非公式ツールです
- Microsoft、.NET Foundation、Roslyn チームとは提携・関係がありません

## 背景

.NET アプリケーションには、スレッドセーフ性、インフラ境界、副作用制約のように型システムだけでは表現しにくい設計前提が存在します。この Analyzer はそうした前提を属性で明示し、実際の型依存に対して機械的に検証できるようにします。

## 初回リリースの対象スコープ

初期版では以下のみを解析対象に絞ります。

- コンストラクタ引数
- フィールド型
- 継承
- インタフェース実装

初期版では、プロパティ、メソッド引数、`new` 式、static メンバー利用は解析対象外です。

## 想定インストール方法

初回公開後は、概ね次のように参照する想定です。

```xml
<ItemGroup>
  <PackageReference Include="DependencyContractAnalyzer" Version="x.y.z" PrivateAssets="all" />
</ItemGroup>
```

## 想定利用例

依存先が提供する契約を宣言します。

```csharp
[ProvidesContract("thread-safe")]
public sealed class RedisCacheStore : ICacheStore
{
}
```

依存元が必要とする契約を宣言します。

```csharp
[RequiresDependencyContract(typeof(ICacheStore), "thread-safe")]
public sealed class CacheCoordinator
{
    public CacheCoordinator(ICacheStore store)
    {
    }
}
```

一致する依存が必要契約を提供していない場合、Analyzer は次を報告します。

- Diagnostic ID: `DCA001`
- 既定 Severity: `Warning`
- メッセージ: `Dependency '{DependencyType}' does not provide required contract '{ContractName}'.`

契約名は前後空白を除去し、`StringComparison.OrdinalIgnoreCase` で比較します。

## 非対象

- DI コンテナーの登録解析
- runtime 依存解決
- Scrutor や factory registration の挙動
- コンテナー固有の配線ルール

## ドキュメント

- 英語版ユーザーガイド: [README.md](README.md)
- 実装スコープ: [docs/specification.ja.md](docs/specification.ja.md)
- 英語版実装スコープ: [docs/specification.md](docs/specification.md)
- 最終アーキテクチャ: [docs/architecture.ja.md](docs/architecture.ja.md)
- English architecture: [docs/architecture.md](docs/architecture.md)
- コントリビュートガイド: [CONTRIBUTING.ja.md](CONTRIBUTING.ja.md)
- 行動規範: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- 開発ガイド: [docs/development.ja.md](docs/development.ja.md)
- Trusted Publishing ガイド: [docs/trusted-publishing.ja.md](docs/trusted-publishing.ja.md)
- セキュリティポリシー: [SECURITY.md](SECURITY.md)
- サポートポリシー: [.github/SUPPORT.md](.github/SUPPORT.md)
- 変更履歴: [CHANGELOG.md](CHANGELOG.md)

## ライセンス

- 本プロジェクトは MIT License の下で提供されます。本文は [LICENSE](LICENSE) を参照してください。
- 日本語訳は [LICENSE.ja.md](LICENSE.ja.md) にあります。
- サードパーティ関連の告知は [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) に記載します。
