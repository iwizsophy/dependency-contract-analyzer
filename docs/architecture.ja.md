# DependencyContractAnalyzer アーキテクチャ

この文書は、`DependencyContractAnalyzer` の最終完成形として想定しているアーキテクチャを整理したものです。

このツールの芯は次です。

`Type dependency is allowed only when declared contracts are satisfied.`

日本語では、

`型依存は、宣言された契約を満たす場合にのみ許可される。`

これは単なる属性アナライザではなく、次を組み合わせた静的アーキテクチャ検証基盤です。

- 型依存
- 宣言された契約
- target / scope のメタデータ

## 1. 全体像

```text
[Type / Symbol]
    |
    +-- ProvidesContract
    +-- ContractTarget
    +-- ContractScope

[Dependency Extraction]
    |
    +-- どの型がどの型に依存しているかを抽出

[Rule Declaration]
    |
    +-- RequiresDependencyContract
    +-- RequiresContractOnTarget
    +-- RequiresContractOnScope

[Rule Engine]
    |
    +-- 契約一致判定
    +-- Alias / 包含関係解決
    +-- Roslyn 標準 suppression、exact requirement suppression、owner type exclusion
    +-- Diagnostic 発行
```

## 2. 概念モデル

### Contract

任意文字列の契約です。

例:

- `thread-safe`
- `retry-safe`
- `no-blocking`
- `tenant-aware`

### Provider

契約を提供する型です。

```csharp
[ProvidesContract("thread-safe")]
public class RedisCacheStore : ICacheStore
{
}
```

### Consumer

依存先に契約を要求する型です。

```csharp
[RequiresDependencyContract(typeof(ICacheStore), "thread-safe")]
public class CacheCoordinator
{
    public CacheCoordinator(ICacheStore store) {}
}
```

### Target

`repository` や `controller` のような型カテゴリです。

```csharp
[ContractTarget("repository")]
public class UserRepository : IUserRepository
{
}
```

### Scope

`application` や `repository` のようなアーキテクチャ層またはコード領域です。

```csharp
[ContractScope("application")]
public class OrderService
{
}
```

## 3. 最終形の属性群

### 3.1 提供契約

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ProvidesContractAttribute : Attribute
{
    public string Name { get; }
    public ProvidesContractAttribute(string name) => Name = name;
}
```

### 3.2 型単位の要求

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresDependencyContractAttribute : Attribute
{
    public Type DependencyType { get; }
    public string ContractName { get; }

    public RequiresDependencyContractAttribute(Type dependencyType, string contractName)
    {
        DependencyType = dependencyType;
        ContractName = contractName;
    }
}
```

### 3.3 カテゴリ単位の要求

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ContractTargetAttribute : Attribute
{
    public string Name { get; }
    public ContractTargetAttribute(string name) => Name = name;
}
```

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresContractOnTargetAttribute : Attribute
{
    public string TargetName { get; }
    public string ContractName { get; }

    public RequiresContractOnTargetAttribute(string targetName, string contractName)
    {
        TargetName = targetName;
        ContractName = contractName;
    }
}
```

### 3.4 スコープ単位の要求

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
public sealed class ContractScopeAttribute : Attribute
{
    public string Name { get; }
    public ContractScopeAttribute(string name) => Name = name;
}
```

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiresContractOnScopeAttribute : Attribute
{
    public string ScopeName { get; }
    public string ContractName { get; }

    public RequiresContractOnScopeAttribute(string scopeName, string contractName)
    {
        ScopeName = scopeName;
        ContractName = contractName;
    }
}
```

### 3.5 契約包含辺

```csharp
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ContractAliasAttribute : Attribute
{
    public string From { get; }
    public string To { get; }

    public ContractAliasAttribute(string from, string to)
    {
        From = from;
        To = to;
    }
}
```

```csharp
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ContractHierarchyAttribute : Attribute
{
    public string Child { get; }
    public string Parent { get; }

    public ContractHierarchyAttribute(string child, string parent)
    {
        Child = child;
        Parent = parent;
    }
}
```

例:

```csharp
[assembly: ContractAlias("immutable", "thread-safe")]
[assembly: ContractHierarchy("snapshot-cache", "immutable")]
```

意味:

`snapshot-cache` を提供していれば `immutable` と `thread-safe` の両方を満たすとみなします。

現在のセマンティクス:

- `child -> parent` を契約包含の有向辺として扱う
- 同じ child に対して属性を繰り返すことで多親を表現できる
- 後方互換のため `ContractAlias` も同じ包含辺モデルとして解釈する
- alias と hierarchy の辺は 1 つの DAG として解決する
- `DCA202` は combined graph 全体の cycle を報告する
- 契約充足判定は combined graph の推移閉包を使う

これにより既存 alias の互換性を維持しつつ、多段・多親の契約階層を明示的に表現できます。

## 4. ルール評価の優先順位

ルールエンジン内での評価順は明示した方がよいです。

1. `RequiresDependencyContract`
2. `RequiresContractOnTarget`
3. `RequiresContractOnScope`
4. 包含グラフ解決

これは依存評価時の優先順位であり、リリース順とは別です。

## 5. 依存評価モデル

初期実装では、強い依存だけを対象にすれば十分です。

```text
Consumer Type
   +-- constructor parameter
   +-- method parameter
   +-- property
   +-- field
   +-- new expression
   +-- static member usage
   +-- base type
   +-- interface
```

各依存先型について次を参照します。

```text
Dependency Type
   +-- ProvidesContract
   +-- ContractTarget
   +-- ContractScope
```

## 6. 具体例

### 6.1 型単位要求

```csharp
[RequiresDependencyContract(typeof(ICacheStore), "thread-safe")]
public class CacheCoordinator
{
    public CacheCoordinator(ICacheStore store) {}
}
```

```csharp
[ProvidesContract("thread-safe")]
public class RedisCacheStore : ICacheStore
{
}
```

結果: 問題なし。

### 6.2 Target 要求

```csharp
[RequiresContractOnTarget("repository", "thread-safe")]
public class OrderService
{
    public OrderService(IUserRepository repository) {}
}
```

```csharp
[ContractTarget("repository")]
public class UserRepository : IUserRepository
{
}
```

`UserRepository` に `thread-safe` がなければ違反です。

### 6.3 Scope 要求

```csharp
[ContractScope("application")]
[RequiresContractOnScope("repository", "retry-safe")]
public class BillingService
{
    public BillingService(IBillingRepository repository) {}
}
```

```csharp
[ContractScope("repository")]
public class BillingRepository : IBillingRepository
{
}
```

`BillingRepository` が `retry-safe` を持たなければ違反です。

### 6.4 包含グラフベース requirement

```csharp
[assembly: ContractAlias("immutable", "thread-safe")]
[assembly: ContractHierarchy("snapshot-cache", "immutable")]
```

```csharp
[ProvidesContract("snapshot-cache")]
public class SnapshotStore : IStore
{
}
```

```csharp
[RequiresDependencyContract(typeof(IStore), "thread-safe")]
public class StoreConsumer
{
    public StoreConsumer(IStore store) {}
}
```

結果: 包含グラフにより適合です。

## 7. 実装アーキテクチャ

Analyzer 本体より先に内部表現を明確にすると実装しやすくなります。

```csharp
internal sealed record ContractDescriptor(string Name);

internal sealed record TargetDescriptor(string Name);

internal sealed record ScopeDescriptor(string Name);

internal sealed record DependencyEdge(
    INamedTypeSymbol Consumer,
    INamedTypeSymbol Dependency,
    DependencyKind Kind);

internal sealed record RequirementDescriptor(
    RequirementKind Kind,
    string SubjectName,
    string ContractName,
    INamedTypeSymbol OwnerType);
```

### 7.1 RequirementKind

```csharp
internal enum RequirementKind
{
    DependencyType,
    Target,
    Scope
}
```

### 7.2 DependencyKind

```csharp
internal enum DependencyKind
{
    ConstructorParameter,
    MethodParameter,
    Property,
    Field,
    ObjectCreation,
    StaticMemberAccess,
    BaseType,
    InterfaceImplementation
}
```

## 8. Analyzer の流れ

```text
CompilationStart
   |
   +-- Attribute symbol を解決
   |
   +-- 型ごとの metadata を抽出
   |     +-- provided contracts
   |     +-- targets
   |     +-- scopes
   |     +-- requirements
   |
   +-- dependency edge を抽出
   |
   +-- rule engine で評価
   |
   +-- diagnostics を発行
```

## 9. Rule Engine の形

本質はこの関数で捉えると分かりやすいです。

```text
Evaluate(consumer, dependency) -> violations
```

内部では次を行います。

1. consumer の requirement を列挙する
2. dependency が requirement の対象に一致するか判定する
3. required contract を満たすか確認する
4. 必要なら包含グラフ解決を適用する
5. それでも満たさなければ diagnostic を発行する

## 10. 診断体系の最終形

### 基本診断

- `DCA001`: 依存先が要求契約を提供していない
- `DCA002`: 指定依存型が存在しない

### 契約定義診断

- `DCA100`: 契約名が空
- `DCA101`: 契約名フォーマット違反
- `DCA102`: 重複契約指定

`DCA101` は contract 名、requirement suppression の contract 引数、alias / hierarchy endpoint のみを対象とし、target 名 / scope 名には適用しません。v1 で適用する形式は lower-kebab-case です。

### ルール定義診断

- `DCA200`: 存在しない target を要求
- `DCA201`: 存在しない scope を要求
- `DCA202`: 契約包含定義が循環している
- `DCA203`: scope 名が空
- `DCA204`: target 名が空
- `DCA205`: target 要求に一致する依存が存在しない
- `DCA206`: scope 要求に一致する依存が存在しない

## 11. OSS として強い理由

この設計にすると、用途は DI 依存の検証に留まりません。

表現できるもの:

- 依存ごとの契約検証
- 層ごとの設計ルール
- カテゴリごとの設計ルール
- チーム独自規約
- 将来的な ArchUnit 的拡張

パッケージ名は `DependencyContractAnalyzer` のままで成立しますが、思想としてはかなり Architecture Analyzer 寄りです。

## 12. 実装ロードマップ

リポジトリの実装ロードマップと、ルールエンジン内の評価優先順位は別の話です。

このリポジトリでは、現時点では次の段階的導入を推奨します。

1. v1: `ProvidesContract`, `RequiresDependencyContract`, 強い依存抽出, `DCA001`, `DCA002`
2. v2: `ContractScope`, `RequiresContractOnScope`
3. v3: `ContractTarget`, `RequiresContractOnTarget`
4. v4: `ContractAlias`, alias 解決, 循環検知

実装リスクや需要に応じて v2 と v3 は入れ替えても、最終形のアーキテクチャ自体は変わりません。

現在の `.editorconfig` 対応は Diagnostic severity に加え、`behavior_preset`、field / base type / interface implementation / method parameter / property / object creation / static member 利用の依存抽出トグル、`report_unused_requirement_diagnostics`、`report_undeclared_requirement_diagnostics`、namespace / fully qualified type 名による owner type exclusion、leaf または trailing 2-segment fallback を選ぶ `namespace_inference_max_segments`、そして参照先 assembly を無視するか explicit metadata を読むかを切り替える `external_dependency_policy` を含みます。独自 exclusion 属性は assembly と owner type に対して実装済みです。`ExcludeDependencyContractSourceAttribute` は constructor / method / property / field に対して実装済みです。dependency / target / scope requirement に対する exact-match suppression 属性も owner type に実装済みです。`behavior_preset` は source toggle、namespace inference、external dependency policy の既定挙動をまとめて切り替えますが、個別 option が常に優先します。requirement diagnostic switch は `behavior_preset` とは独立で、undeclared requirement diagnostic を無効化すると target / scope evaluation は undeclared check を越えて継続します。namespace ベースの target / scope 推定は既定で current compilation 内の最終セグメント fallback を使い、必要に応じて trailing 2-segment fallback も使えます。`metadata` の external dependency mode では、参照先 assembly は explicit provided-contract / target / scope metadata に加えて `ContractAlias` / `ContractHierarchy` の implication edge も供給します。local と referenced の implication graph は外部 dependency に対してまとめて展開し、参照先包含定義の診断は consumer compilation では抑止します。namespace inference と undeclared target / scope 判定は、undeclared diagnostic switch を無効化しない限り current compilation のままです。より高度な推定は引き続き非対応です。
