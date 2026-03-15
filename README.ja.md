# DependencyContractAnalyzer

英語版 README: [README.md](README.md)

`DependencyContractAnalyzer` は、型間の依存関係に対して契約を宣言的に付与し、その契約が依存先で満たされているかを DI 登録解析に頼らず静的解析で検証する Roslyn Analyzer パッケージです。

このツールの中核コンセプトは次です。

`型依存は、宣言された契約を満たす場合にのみ許可される。`

## 現在の状態

- 実装済み Analyzer ルール: `ProvidesContract`, `RequiresDependencyContract`, `ContractTarget`, `RequiresContractOnTarget`, `ContractScope`, `RequiresContractOnScope`, `ContractHierarchy`
- 依存抽出の対象は現在、コンストラクタ引数、コンストラクタ以外のメソッド引数、プロパティ型、フィールド型、`new` 式、static メンバー利用（static event と `using static` を含み、enum member は除外）、継承、実装インタフェースです
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

assembly 単位の包含辺は `ContractHierarchy` で宣言します。

```csharp
[assembly: ContractHierarchy("snapshot-cache", "immutable")]
[assembly: ContractHierarchy("immutable", "thread-safe")]

[ProvidesContract("snapshot-cache")]
public sealed class SnapshotCache
{
}
```

この定義がある場合、`snapshot-cache` は `immutable` と `thread-safe` の両方を満たすものとして扱われます。多段・多親の hierarchy 解決に対応し、循環する包含定義は `DCA202` として報告します。

`ContractHierarchy` が包含辺 API です。多親階層は属性の繰り返しで表現できます。target / scope では明示属性を優先します。既定では type-level metadata がない場合に namespace 最終セグメントから fallback 名を推定し、`ReadModel` は `read-model` として扱われます。`dependency_contract_analyzer.namespace_inference_max_segments = 2` を設定すると、`ReadModels.Query` -> `read-models-query` のような trailing 2-segment fallback も推定します。scope は assembly-level `ContractScope` がある場合、それを明示定義として優先し namespace 推定は行いません。current compilation 外の dependency は既定では無視しますが、`dependency_contract_analyzer.external_dependency_policy = metadata` を設定すると、参照先 assembly の explicit provided-contract / target / scope metadata に加えて `ContractHierarchy` の包含辺も読み取ります。参照先包含定義の診断は consumer compilation には出しません。なお undeclared target / scope 判定は引き続き current compilation 内の宣言だけを対象にします。

## 既定 Severity

- `DCA001`, `DCA002`, `DCA100`, `DCA101`, `DCA102`, `DCA200`, `DCA201`, `DCA202`, `DCA203`, `DCA204`: `Warning`
- `DCA205`, `DCA206`: `Info`

これは製品の既定値です。すべての Diagnostic は `.editorconfig` で変更できます。

`.editorconfig` では、次の追加依存源に対する解析トグルも設定できます。

- `dependency_contract_analyzer.behavior_preset`
- `dependency_contract_analyzer.analyze_fields`
- `dependency_contract_analyzer.analyze_base_types`
- `dependency_contract_analyzer.analyze_interface_implementations`
- `dependency_contract_analyzer.analyze_method_parameters`
- `dependency_contract_analyzer.analyze_properties`
- `dependency_contract_analyzer.analyze_object_creation`
- `dependency_contract_analyzer.analyze_static_members`
- `dependency_contract_analyzer.report_unused_requirement_diagnostics`
- `dependency_contract_analyzer.report_undeclared_requirement_diagnostics`
- `dependency_contract_analyzer.excluded_namespaces`
- `dependency_contract_analyzer.excluded_types`
- `dependency_contract_analyzer.namespace_inference_max_segments`
- `dependency_contract_analyzer.external_dependency_policy`

`behavior_preset = default` では、すべての `analyze_*` option は既定で `true` です。コンストラクタ引数は preset に関係なく常に解析対象です。

`behavior_preset` は global option で、`default`、`strict`、`relaxed` をサポートし、不正値は `default` へフォールバックします。

- `default`: 現在の製品既定値
- `strict`: すべての optional dependency-source toggle を有効化し、`namespace_inference_max_segments = 2`、`external_dependency_policy = metadata` を既定にします
- `relaxed`: optional dependency-source toggle を無効化し、namespace inference を無効化し、`external_dependency_policy = ignore` を既定にします

個別 option は常に preset より優先します。たとえば `analyze_method_parameters = true`、`namespace_inference_max_segments = 2`、`external_dependency_policy = metadata` は `behavior_preset` より優先されます。exclusion list と diagnostic severity は別制御です。

source-scoped option は partial owner type のすべての宣言ファイルをまとめて評価します。boolean の source-scoped option（`analyze_*`、`report_*`）は保守的に merge され、どこか 1 つの宣言で明示的に `false` が指定されるとその type 全体で無効になります。list-valued の source-scoped option（`excluded_namespaces`、`excluded_types`）は宣言全体で重複を除いて union されます。`behavior_preset`、`namespace_inference_max_segments`、`external_dependency_policy` のような global option は compilation 単位のままです。

`report_unused_requirement_diagnostics` は `DCA002`、`DCA205`、`DCA206` を制御し、`report_undeclared_requirement_diagnostics` は `DCA200`、`DCA201` を制御します。どちらも既定値は `true` で、不正値は既定値へフォールバックします。undeclared requirement diagnostics を無効化した場合、target / scope requirement は undeclared check で停止せず、そのまま一致する dependency 評価を続けます。

`excluded_namespaces` は列挙した namespace とその subnamespace 配下の owner type 解析をスキップします。`excluded_types` は fully qualified owner type 名を指定して解析をスキップします。`namespace_inference_max_segments` は global option で、`1` と `2` をサポートし、既定値は `1`、不正値は preset 由来の既定値へフォールバックします。`external_dependency_policy` も global option で、`ignore` と `metadata` をサポートし、既定値は `ignore`、不正値は preset 由来の既定値へフォールバックします。`metadata` モードでも namespace inference は current compilation 内の型に限定し、参照先 assembly からは explicit metadata と包含辺のみを使用します。

## 推奨 CI 運用

- `DCA202`、`DCA203`、`DCA204` は CI では `Error` へ昇格推奨
- `DCA205`、`DCA206` は通常は `Info` のまま推奨
- `DCA101` は lower-kebab-case の contract 名、requirement suppression の contract 引数、hierarchy endpoint のみを対象とし、target / scope 名には適用しません

## Suppression モデル

現在の実装では次をサポートします。

- `#pragma warning disable`
- `[SuppressMessage]`
- `.editorconfig` による severity 設定
- `.editorconfig` の `excluded_namespaces` / `excluded_types` による owner type exclusion
- assembly / owner type に付ける `[ExcludeDependencyContractAnalysis]`
- constructor / method / property / field に付ける `[ExcludeDependencyContractSource]` による member source exclusion
- owner type に付ける `[SuppressRequiredDependencyContract]` / `[SuppressRequiredTargetContract]` / `[SuppressRequiredScopeContract]` による exact-match suppression

member-level exclusion は dependency source だけを外し、対応する requirement 自体は suppress しません。

## 非対象

- DI コンテナーの登録解析
- runtime 依存解決
- Scrutor や factory registration の挙動
- コンテナー固有の配線ルール
- layer dependency enforcement
- namespace / package boundary rules
- generic forbidden dependency graph rules
- architectural layer の cycle detection
- contract と無関係な naming analyzer
- file / directory layout rules
- project / solution structure validation
- ArchUnit のような general architecture DSL

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
