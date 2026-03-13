# DependencyContractAnalyzer

英語版 README: [README.md](README.md)

`DependencyContractAnalyzer` は、型間の依存関係に対して契約を宣言的に付与し、その契約が依存先で満たされているかを DI 登録解析に頼らず静的解析で検証する Roslyn Analyzer パッケージです。

このツールの中核コンセプトは次です。

`型依存は、宣言された契約を満たす場合にのみ許可される。`

## 現在の状態

- 実装済み Analyzer ルール: `ProvidesContract`, `RequiresDependencyContract`, `ContractTarget`, `RequiresContractOnTarget`, `ContractScope`, `RequiresContractOnScope`, `ContractAlias`, `ContractHierarchy`
- 依存抽出の対象は現在、コンストラクタ引数、コンストラクタ以外のメソッド引数、プロパティ型、フィールド型、`new` 式、static メンバー利用、継承、実装インタフェースです
- パッケージ ID は `DependencyContractAnalyzer` です
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
- コンストラクタ以外のメソッド引数
- プロパティ型
- フィールド型
- `new` 式
- static メンバー利用
- 継承
- インタフェース実装

## パッケージ参照

公開後は、概ね次のように参照します。

```xml
<ItemGroup>
  <PackageReference Include="DependencyContractAnalyzer" Version="x.y.z" PrivateAssets="all" />
</ItemGroup>
```

## 利用例

依存先が提供する契約を宣言します。

```csharp
[ProvidesContract("thread-safe")]
public interface ICacheStore
{
}

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

この Analyzer は DI 解析を行わないため、必要契約は「実際に依存している型」自身、またはその基底型・実装インタフェースから取得できる必要があります。インタフェース依存を検証したい場合は、そのインタフェース側に契約を付けてください。

scope 単位のルールも利用できます。

```csharp
[ContractScope("repository")]
[ProvidesContract("thread-safe")]
public sealed class UserRepository
{
}

[RequiresContractOnScope("repository", "thread-safe")]
public sealed class OrderService
{
    public OrderService(UserRepository repository)
    {
    }
}
```

scope 名は契約名と同様に `Trim()` 後、`StringComparison.OrdinalIgnoreCase` で比較します。

target 単位のルールも利用できます。

```csharp
[ContractTarget("repository")]
[ProvidesContract("thread-safe")]
public sealed class UserRepository
{
}

[RequiresContractOnTarget("repository", "thread-safe")]
public sealed class OrderService
{
    public OrderService(UserRepository repository)
    {
    }
}
```

target 名も `Trim()` 後、`StringComparison.OrdinalIgnoreCase` で比較します。

assembly 単位の包含辺は `ContractAlias` と `ContractHierarchy` のどちらでも宣言できます。

```csharp
[assembly: ContractAlias("immutable", "thread-safe")]
[assembly: ContractHierarchy("snapshot-cache", "immutable")]

[ProvidesContract("snapshot-cache")]
public sealed class SnapshotCache
{
}
```

この定義がある場合、`snapshot-cache` は `immutable` と `thread-safe` の両方を満たすものとして扱われます。alias と hierarchy を混在した多段解決に対応し、循環する包含定義は `DCA202` として報告します。

`ContractAlias` は後方互換のための包含辺として維持し、`ContractHierarchy` が明示的な階層 API です。多親階層は属性の繰り返しで表現できます。target / scope では明示属性を優先しつつ、type-level metadata がない場合は namespace 最終セグメントから fallback 名を推定します。`ReadModel` は `read-model` として扱われます。scope は assembly-level `ContractScope` がある場合、それを明示定義として優先し namespace 推定は行いません。

## 既定 Severity

- `DCA001`, `DCA002`, `DCA100`, `DCA101`, `DCA102`, `DCA200`, `DCA201`, `DCA202`, `DCA203`, `DCA204`: `Warning`
- `DCA205`, `DCA206`: `Info`

これは製品の既定値です。すべての Diagnostic は `.editorconfig` で変更できます。

`.editorconfig` では、次の追加依存源に対する解析トグルも設定できます。

- `dependency_contract_analyzer.analyze_method_parameters`
- `dependency_contract_analyzer.analyze_properties`
- `dependency_contract_analyzer.analyze_object_creation`
- `dependency_contract_analyzer.analyze_static_members`
- `dependency_contract_analyzer.excluded_namespaces`
- `dependency_contract_analyzer.excluded_types`

4 つの `analyze_*` option はすべて既定で `true` です。コンストラクタ引数、フィールド型、継承、インタフェース実装は常に解析対象です。

`excluded_namespaces` は列挙した namespace とその subnamespace 配下の owner type 解析をスキップします。`excluded_types` は fully qualified owner type 名を指定して解析をスキップします。

## 推奨 CI 運用

- `DCA202`、`DCA203`、`DCA204` は CI では `Error` へ昇格推奨
- `DCA205`、`DCA206` は通常は `Info` のまま推奨
- `DCA101` は lower-kebab-case の contract 名と alias / hierarchy endpoint のみを対象とし、target / scope 名には適用しません

## Suppression モデル

v1 では Roslyn 標準の suppression 機構のみを利用します。

- `#pragma warning disable`
- `[SuppressMessage]`
- `.editorconfig` による severity 設定

独自 exclusion 属性や requirement 単位 suppression は v1 非対応です。

## 非対象

- DI コンテナーの登録解析
- runtime 依存解決
- Scrutor や factory registration の挙動
- コンテナー固有の配線ルール

## ドキュメント

- 英語版ユーザーガイド: [README.md](README.md)
- サンプル consumer project: [samples/DependencyContractAnalyzer.Sample](samples/DependencyContractAnalyzer.Sample)
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
