# DependencyContractAnalyzer 仕様書

この文書は、`DependencyContractAnalyzer` の現在の実装スコープを整理したものです。最終完成形の設計全体は `docs/architecture.ja.md` を参照してください。

## 1. 目的

クラスおよびインタフェースに依存契約を宣言し、依存元が要求する契約を依存先が満たしているかを静的解析で検証します。

解析対象は型依存のみであり、DI 登録解析には依存しません。

## 2. 現在のスコープ

現在、解析対象としている依存種別は次のとおりです。

| 依存種別 | 対象 |
| --- | --- |
| コンストラクタ引数 | 対象 |
| コンストラクタ以外のメソッド引数 | 対象 |
| プロパティ型 | 対象 |
| フィールド型 | 対象 |
| `new` 式 | 対象 |
| static メンバー利用 | 対象 |
| 継承 | 対象 |
| インタフェース実装 | 対象 |

次の依存種別は `.editorconfig` で無効化でき、既定値は `true` です。

- `dependency_contract_analyzer.analyze_fields`
- `dependency_contract_analyzer.analyze_base_types`
- `dependency_contract_analyzer.analyze_interface_implementations`
- `dependency_contract_analyzer.analyze_method_parameters`
- `dependency_contract_analyzer.analyze_properties`
- `dependency_contract_analyzer.analyze_object_creation`
- `dependency_contract_analyzer.analyze_static_members`

コンストラクタ引数は常に解析対象です。

現在、実装済みのルールファミリは次のとおりです。

| ルール | 実装 |
| --- | --- |
| `ProvidesContract` | 実装済み |
| `RequiresDependencyContract` | 実装済み |
| `ContractTarget` | 実装済み |
| `RequiresContractOnTarget` | 実装済み |
| `ContractScope` | 実装済み |
| `RequiresContractOnScope` | 実装済み |
| `ContractAlias` | 実装済み |
| `ContractHierarchy` | 実装済み |

次は引き続き対象外です。

| 項目 | 理由 |
| --- | --- |
| namespace ベース推定の高度化 | 現状は最終セグメント推定に加え、設定時の trailing 2-segment fallback まで |

## 3. 属性仕様

### 3.1 提供契約

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ProvidesContractAttribute : Attribute
{
    public string Name { get; }

    public ProvidesContractAttribute(string name)
    {
        Name = name;
    }
}
```

### 3.2 依存契約要求

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

### 3.3 target 宣言

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class ContractTargetAttribute : Attribute
{
    public string Name { get; }

    public ContractTargetAttribute(string name)
    {
        Name = name;
    }
}
```

### 3.4 target 単位の契約要求

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

### 3.5 scope 宣言

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
public sealed class ContractScopeAttribute : Attribute
{
    public string Name { get; }

    public ContractScopeAttribute(string name)
    {
        Name = name;
    }
}
```

### 3.6 scope 単位の契約要求

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

### 3.7 契約 alias

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

### 3.8 契約階層

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

### 3.9 包含グラフの意味論

- `ContractAlias` と `ContractHierarchy` はどちらも有向の包含辺を宣言します
- alias は `from -> to`、hierarchy は `child -> parent` を表します
- 契約充足関係は combined implication graph 上で推移的です
- 契約は自分自身と、alias / hierarchy をたどって到達可能な契約を満たします
- 同じ child に属性を繰り返すことで多親階層を表現できます
- combined implication graph の循環は無効であり、`DCA202` で報告します

### 3.10 独自 exclusion 属性

```csharp
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class ExcludeDependencyContractAnalysisAttribute : Attribute
{
}
```

assembly または owner type に付与すると、その owner type に対する analyzer 実行をスキップします。

### 3.11 requirement suppression 属性

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SuppressRequiredDependencyContractAttribute : Attribute
{
    public Type DependencyType { get; }
    public string ContractName { get; }

    public SuppressRequiredDependencyContractAttribute(Type dependencyType, string contractName)
    {
        DependencyType = dependencyType;
        ContractName = contractName;
    }
}
```

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SuppressRequiredTargetContractAttribute : Attribute
{
    public string TargetName { get; }
    public string ContractName { get; }

    public SuppressRequiredTargetContractAttribute(string targetName, string contractName)
    {
        TargetName = targetName;
        ContractName = contractName;
    }
}
```

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SuppressRequiredScopeContractAttribute : Attribute
{
    public string ScopeName { get; }
    public string ContractName { get; }

    public SuppressRequiredScopeContractAttribute(string scopeName, string contractName)
    {
        ScopeName = scopeName;
        ContractName = contractName;
    }
}
```

これらの属性は owner class 上で exact match した requirement の診断だけを抑止します。dependency discovery 自体は除外しません。

### 3.12 member-level dependency source exclusion

```csharp
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ExcludeDependencyContractSourceAttribute : Attribute
{
}
```

constructor / method / property / field に付けると、その member に由来する declared dependency source と syntax-based dependency discovery を除外します。

DI 解析を行わないため、依存がインタフェースや基底型で表現されている場合は、契約もその抽象側に宣言してください。

## 4. ルール評価モデル

現在の Analyzer は、次の順で要件を評価します。

1. `RequiresDependencyContract`
2. `RequiresContractOnTarget`
3. `RequiresContractOnScope`
4. 包含グラフによる提供契約の展開

現在の振る舞い:

- `RequiresDependencyContract` は一致する依存が存在し、なおかつ必要契約が満たされない場合に診断します
- `RequiresDependencyContract` は宣言した依存型が未使用なら `DCA002` を報告します
- `RequiresContractOnTarget` は正規化後の target 名が一致した依存だけを評価します
- `RequiresContractOnScope` は正規化後の scope 名が一致した依存だけを評価します
- 現在の compilation assembly 外にある依存は、未提供契約診断の対象外です
- type-level の target / scope では明示属性を優先します
- type-level target が未指定なら current compilation 内の namespace 最終セグメントから推定します
- `dependency_contract_analyzer.namespace_inference_max_segments = 2` の場合は trailing 2-segment fallback target 名も推定します
- type-level scope が未指定かつ assembly-level scope がなければ、current compilation 内の namespace 最終セグメントから推定します
- `dependency_contract_analyzer.namespace_inference_max_segments = 2` の場合は、assembly-level scope がないときに trailing 2-segment fallback scope 名も推定します
- assembly-level scope は明示定義として扱い、scope 推定より優先します
- assembly/type-level の `ExcludeDependencyContractAnalysisAttribute` は owner type の analyzer 実行をスキップします
- `ExcludeDependencyContractSourceAttribute` は owner type exclusion 適用後に、対応する constructor / method / property / field の dependency source を除外します
- exact match する requirement suppression 属性は、その requirement に対応する `DCA001`、`DCA002`、`DCA200`、`DCA201`、`DCA205`、`DCA206` を抑止します

## 5. 名前の正規化ルール

現在の Analyzer は、宣言された名前を共通ルールで正規化します。

- 前後空白を削除する
- 大文字小文字を無視する
- Ordinal 比較を使う

対象:

- 契約名
- target 名
- scope 名
- alias / hierarchy の endpoint

## 6. 依存関係取得

対象型から次の依存情報を収集します。

### 6.1 コンストラクタ依存

```csharp
public A(B b)
```

取得元:

- `INamedTypeSymbol`
- `Constructors`
- `Parameters`

### 6.2 メソッド依存

```csharp
public void Execute(B b)
```

取得元:

- `IMethodSymbol`
- `Parameters`

対象に含めるメソッド:

- 通常メソッド
- explicit interface implementation メソッド

対象外:

- コンストラクタ
- property / event accessor
- operator / conversion
- 暗黙宣言メソッド

### 6.3 プロパティ依存

```csharp
public B Dependency { get; set; }
```

取得元:

- `IPropertySymbol.Type`

### 6.4 フィールド依存

```csharp
private B _b;
```

取得元:

- `IFieldSymbol.Type`

### 6.5 `new` 式依存

```csharp
var dependency = new B();
```

```csharp
B dependency = new();
```

取得元:

- `ObjectCreationExpressionSyntax`
- `ImplicitObjectCreationExpressionSyntax`
- semantic model による型解決

### 6.6 static メンバー依存

代表的な取得元:

- static メソッド呼び出し
- static プロパティ参照
- static フィールド参照
- `using static` で取り込んだメンバー参照

対象外:

- reduced form の extension method
- `const` フィールド
- enum member

### 6.7 継承

```csharp
class A : B
```

取得元:

- `BaseType`

### 6.8 インタフェース実装

```csharp
class A : IFoo
```

取得元:

- `Interfaces`

## 7. メタデータ取得

現在の Analyzer は次の範囲からメタデータを取得します。

- 提供契約: 依存先自身、実装インタフェース、基底型
- target: 依存先自身、実装インタフェース、基底型
- scope: 依存先自身、実装インタフェース、基底型、assembly-level scope 宣言
- 包含辺: assembly-level `ContractAliasAttribute` と `ContractHierarchyAttribute`

提供契約は、包含グラフの推移閉包を適用した後に requirement と照合します。

assembly-level scope は、型に付いた scope に加えて既定 scope として扱います。

## 8. 診断仕様

| ID | 既定 Severity | 意味 |
| --- | --- | --- |
| `DCA001` | `Warning` | 依存先が必要契約を提供していない |
| `DCA002` | `Warning` | 宣言した依存型が使われていない |
| `DCA100` | `Warning` | 契約名が空 |
| `DCA101` | `Warning` | 契約名フォーマット違反 |
| `DCA102` | `Warning` | 契約または requirement 宣言が重複している |
| `DCA200` | `Warning` | 要求した target が compilation 内で未宣言 |
| `DCA201` | `Warning` | 要求した scope が compilation 内で未宣言 |
| `DCA202` | `Warning` | 契約包含定義が循環している |
| `DCA203` | `Warning` | scope 名が空 |
| `DCA204` | `Warning` | target 名が空 |
| `DCA205` | `Info` | 要求した target を持つ analyzable dependency が存在しない |
| `DCA206` | `Info` | 要求した scope を持つ analyzable dependency が存在しない |

Severity は `.editorconfig` により変更可能です。

既定 Severity は製品仕様であり、CI での推奨 Severity は別文書で扱う運用ガイドです。

### 8.1 EditorConfig option

`DependencyContractAnalyzer` は次の boolean `.editorconfig` option をサポートします。

- `dependency_contract_analyzer.analyze_fields`（既定: `true`）
- `dependency_contract_analyzer.analyze_base_types`（既定: `true`）
- `dependency_contract_analyzer.analyze_interface_implementations`（既定: `true`）
- `dependency_contract_analyzer.analyze_method_parameters`（既定: `true`）
- `dependency_contract_analyzer.analyze_properties`（既定: `true`）
- `dependency_contract_analyzer.analyze_object_creation`（既定: `true`）
- `dependency_contract_analyzer.analyze_static_members`（既定: `true`）

値が未設定または不正な場合は既定値へフォールバックします。

あわせて、次の global integer `.editorconfig` option もサポートします。

- `dependency_contract_analyzer.namespace_inference_max_segments`（既定: `1`、対応値: `1`、`2`）

あわせて、次の list-valued `.editorconfig` option もサポートします。

- `dependency_contract_analyzer.excluded_namespaces`
- `dependency_contract_analyzer.excluded_types`

`excluded_namespaces` は列挙した namespace とその subnamespace 配下の owner type 解析をスキップします。`excluded_types` は fully qualified owner type 名を指定して解析をスキップします。list 値は comma、semicolon、newline 区切りを受け付けます。`namespace_inference_max_segments` は fallback 推定に最終セグメントのみを使うか（`1`）、最終セグメントに加えて trailing 2-segment 組み合わせも使うか（`2`）を制御します。

### 8.2 命名ルール

`DCA101` は契約名フォーマットを検証する Diagnostic です。

- 形式: lower-kebab-case
- 正規表現: `^[a-z0-9]+(-[a-z0-9]+)*$`
- 適用対象は contract 名と alias / hierarchy endpoint のみ
- requirement suppression 属性の contract 引数にも適用します
- target 名と scope 名には適用しません

対象:

- `ProvidesContract`
- `RequiresDependencyContract` の contract 引数
- `RequiresContractOnTarget` の contract 引数
- `RequiresContractOnScope` の contract 引数
- `SuppressRequiredDependencyContract` の contract 引数
- `SuppressRequiredTargetContract` の contract 引数
- `SuppressRequiredScopeContract` の contract 引数
- `ContractAlias` の `from` / `to`
- `ContractHierarchy` の `child` / `parent`

### 8.3 Suppression モデル

現在の実装では次をサポートします。

- `#pragma warning disable`
- `[SuppressMessage]`
- `.editorconfig` による severity 設定
- `.editorconfig` の `excluded_namespaces` / `excluded_types` による owner type exclusion
- assembly / owner type に付ける `ExcludeDependencyContractAnalysisAttribute`
- constructor / method / property / field に付ける `ExcludeDependencyContractSourceAttribute`
- owner type に付ける `SuppressRequiredDependencyContractAttribute` / `SuppressRequiredTargetContractAttribute` / `SuppressRequiredScopeContractAttribute` による exact-match suppression

member-level exclusion は dependency source のみを外し、requirement 自体は suppress しません。

## 9. 現在のプロジェクト構成

```text
src/
 └ DependencyContractAnalyzer
   ├ Analyzers
   │  └ DependencyContractAnalyzer.cs
   ├ Attributes
   │  ├ ContractAliasAttribute.cs
   │  ├ ContractHierarchyAttribute.cs
   │  ├ ContractScopeAttribute.cs
   │  ├ ContractTargetAttribute.cs
   │  ├ ExcludeDependencyContractAnalysisAttribute.cs
   │  ├ ExcludeDependencyContractSourceAttribute.cs
   │  ├ ProvidesContractAttribute.cs
   │  ├ RequiresContractOnScopeAttribute.cs
   │  ├ RequiresContractOnTargetAttribute.cs
   │  ├ RequiresDependencyContractAttribute.cs
   │  ├ SuppressRequiredDependencyContractAttribute.cs
   │  ├ SuppressRequiredScopeContractAttribute.cs
   │  └ SuppressRequiredTargetContractAttribute.cs
   ├ Diagnostics
   │  └ DiagnosticDescriptors.cs
   ├ Helpers
   │  ├ ContractAliasResolver.cs
   │  ├ ContractNameNormalizer.cs
   │  └ DependencyCollector.cs
   └ Utilities
      └ SymbolExtensions.cs
samples/
 └ DependencyContractAnalyzer.Sample
   ├ DependencyContractAnalyzer.Sample.csproj
   ├ Program.cs
   └ README.md
```

## 10. Analyzer 処理フロー

```text
CompilationStart
        |
        +-- 属性シンボルを解決
        |
        +-- assembly-level の包含定義を取得
        |
        +-- compilation end で包含定義の診断を報告
        |
        +-- SymbolAction(TypeSymbol)
              |
              +-- 契約 / target / scope 宣言を検証
              |
              +-- dependency / target / scope requirement を取得
              |
              +-- 依存型を収集
              |
              +-- 提供契約 / target / scope を取得
              |
              +-- 包含辺を通して提供契約を展開
              |
              +-- requirement 未充足時に診断を報告
```

## 11. テスト方針

`Microsoft.CodeAnalysis.Testing` を使用します。

代表ケース:

- 依存先が直接必要契約を提供している場合は Diagnostic なし
- 非コンストラクタのメソッド引数だけで依存が表現される場合も Diagnostic なし
- プロパティ型だけで依存が表現される場合も Diagnostic なし
- `new` 式だけで依存が表現される場合も Diagnostic なし
- static メンバー利用だけで依存が表現される場合も Diagnostic なし
- `.editorconfig` で field / base type / interface implementation / method parameter / property / object creation / static member の解析を無効化した場合は `DCA002`
- `.editorconfig` で owner type を namespace / type 単位 exclusion した場合は Diagnostic なし
- 一致する dependency source が member-level exclusion された結果、`DCA002` / `DCA205` / `DCA206` になるケース
- owner type 上で一致する dependency / target / scope requirement を suppression した場合は Diagnostic なし
- 一致する依存先が必要契約を提供していない場合は `DCA001`
- `RequiresDependencyContract` が未使用依存型を指している場合は `DCA002`
- 型レベル / assembly-level scope による scope マッチング
- 直接宣言と継承経由の target マッチング
- alias / hierarchy / mixed chain による契約一致
- 空名、重複宣言、循環する包含グラフの診断
- custom exclusion attribute による owner type 解析スキップ

## 12. 将来拡張
- 依存抽出トグルを超える EditorConfig ベースのポリシー制御
- trailing 2-segment 推定を超える namespace ベースのメタデータ推定

## 13. 非対象

- DI 登録解析
- runtime 依存解決
- Scrutor の挙動
- Factory registration の挙動
- DI コンテナーの挙動

## 14. コーディング規約

- Analyzer の allocation を抑制する
- `ImmutableArray` を優先する
- `SymbolEqualityComparer.Default` を使う
- 文字列比較は ordinal の大文字小文字無視を使う

## 15. 完了条件

- この文書にある属性を実装する
- この文書にあるルール評価を実装する
- この文書にある診断を実装する
- 対応済みルールファミリの単体テストを追加する
- README と仕様書を実装状態に合わせて維持する
