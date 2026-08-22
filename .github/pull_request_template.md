<!--
English first, 日本語はその下。Fill in one language or both.
Delete every section that does not apply to this change. A short, accurate description is worth
more than a completed form.
英語が先、日本語がその下です。どちらか一方でも両方でも構いません。
当てはまらない節は消してください。埋めきった書式より、短くても正確な説明のほうが価値があります。
-->

## Summary / 概要

<!--
What changed, and why the change is necessary. / 何をどう変えたか、なぜその変更が必要か。
-->

## Linked issue / 関連する課題

<!--
Larger bug fixes, new features, behavioural changes, public API changes, compatibility changes and
architectural changes need an issue first, so that the intended behaviour and scope are agreed
before implementation. Typo fixes, documentation corrections and trivial bug fixes may go straight
to a pull request; write "not required" in that case.
大きめの不具合修正、新機能、挙動の変更、公開APIの変更、互換性の変更、構造の変更は、実装の前に
意図する挙動と範囲を合意するため、先に課題が必要です。誤字修正、文書の修正、軽微な不具合修正は
そのままプルリクエストで構いません。その場合は「不要」と書いてください。
-->

Closes #

## Kind / 種別

<!-- Keep the ones that apply. / 当てはまるものを残してください。 -->

- Bug fix / 不具合修正
- New public API / 公開APIの追加
- Behaviour change with no API change / API変更を伴わない挙動の変更
- Performance / 性能
- Analyzer or generator / アナライザーまたはジェネレーター
- Documentation / 文書
- Build, packaging or CI / ビルド、パッケージ、CI

## Behaviour change / 挙動の変更

<!--
What an existing caller would observe differently, including exception types and the timing of
failures. Write "none" if nothing observable changes.
既存の呼び出し側から見て何が変わるか。例外の種別や、失敗が起きる時期の変化も含みます。
観測できる変化が無ければ「なし」と書いてください。
-->

## Verification / 検証

<!--
Paste the results you actually ran. Do not judge a run by its total failure count alone: compare the
failing test names and failure modes against the same suites before your change, because a total
that stays the same can still hide a swap. Say plainly if you could not run a suite, and why.
Do not build and test the same working tree at the same time.
実際に走らせた結果を貼ってください。総数だけで判断しないでください。失敗した試験の名前と失敗の
様子を、変更前の同じスイートと突き合わせてください。総数が同じでも中身が入れ替わっていることが
あります。走らせられなかったスイートは、その理由とともに正直に書いてください。
同じ作業ツリーでビルドと試験を同時に走らせないでください。
-->

```console
dotnet build ComputeWeave.sln -c Release -p:Platform=x64

dotnet test tests/ComputeWeave.Tests.SourceGenerators/ComputeWeave.Tests.SourceGenerators.csproj -c Release -p:Platform=x64
dotnet test tests/ComputeWeave.Tests.Internals/ComputeWeave.Tests.Internals.csproj -c Release -p:Platform=x64
dotnet test tests/ComputeWeave.Tests/ComputeWeave.Tests.csproj -c Release -p:Platform=x64
dotnet test tests/ComputeWeave.Tests.DeviceLost/ComputeWeave.Tests.DeviceLost.csproj -c Release -p:Platform=x64
```

<!--
Results, and how they compare to the same suites before the change.
結果と、変更前の同じスイートとの比較。
-->

Adapter and driver tested / 試したアダプターとドライバー:

## Checklist / 確認

- [ ] The pull request is one logical change, with no unrelated refactoring or formatting. / 一つの論理的な変更にまとまっており、無関係な整理や整形を含まない。
- [ ] Every commit builds on its own. / 各コミットが単独でビルドできる。
- [ ] Implementation changes and their verification tests are separate commits. / 実装の変更と検証テストを別のコミットに分けた。
- [ ] No commit already merged into the default branch or referenced by a release tag was rewritten. / 既定ブランチへ入った、またはリリースタグが指すコミットを書き換えていない。
- [ ] The change follows the implementation pattern already established in the subsystem. / 変更は、その部分で既に確立された実装パターンに従っている。
- [ ] Public and protected APIs carry the XML documentation the repository requires. / public と protected のAPIに、リポジトリが求めるXMLドキュメントを付けた。
- [ ] Existing documentation comments were preserved. / 既存のドキュメントコメントを維持した。
- [ ] New internal runtime code follows the local convention for implementation comments. / 新しい内部の実行時コードは、その場所の実装コメントの作法に従っている。
- [ ] No silent fallback behaviour or compatibility shim was introduced. / 暗黙の代替動作や互換性のための繕いを持ち込んでいない。
- [ ] No unrelated dependency was updated. / 無関係な依存関係を更新していない。
- [ ] If a file inherited from upstream changed, the divergence ledger in CONTRIBUTING.md was updated, or the change was judged not to be a divergence. / 上流から受け継いだファイルを変更した場合、CONTRIBUTING.md の乖離台帳を更新したか、乖離ではないと判断した。

## If this touches a guarded area / 慎重を要する箇所に触れる場合

<!--
Public APIs, analyzer diagnostics, generated descriptor formats, resource lifetime or hazard
tracking, synchronization and Direct3D interoperation, disposal and failure handling.
Delete this section if the change touches none of them.
公開API、アナライザー診断、生成される記述子の形式、資源の寿命や危険の追跡、同期とDirect3D相互運用、
破棄と失敗処理。いずれにも触れない変更では、この節ごと消してください。
-->

- [ ] Lifetime tracking and hazard tracking were considered separately, not as one guarantee. / 寿命の追跡と危険の追跡を、一つの保証としてではなく別々に検討した。
- [ ] Tests exercise the guarded behaviour itself, and I confirmed they fail when that behaviour is deliberately broken. / 試験が守るべき挙動そのものを動かしており、その挙動を意図的に壊すと落ちることを確認した。
- [ ] No test depends on arbitrary delays, fixed retry counts, or assumptions about how fast asynchronous work completes. / 任意の待ち時間、固定の再試行回数、非同期処理の速さへの仮定に依存する試験が無い。
- [ ] For changes to command ordering, resource states, barriers, queue synchronization or interoperation, the Direct3D debug layer and GPU validation were used. / コマンドの順序、資源の状態、バリア、キューの同期、相互運用を変えた場合、Direct3Dデバッグレイヤーとその検証機能を使った。
- [ ] An established allocation contract was preserved, or the change to it is justified with evidence. / 確立された確保の契約を維持した。変える場合は根拠を示した。
- [ ] For a performance-sensitive runtime structure, the managed layout was measured with `Unsafe.SizeOf<T>()` and the layout tests updated. / 性能に効く実行時構造体は `Unsafe.SizeOf<T>()` で管理レイアウトを測り、レイアウトの試験を更新した。
- [ ] For analyzer diagnostics that produce build errors, the complete solution was verified in addition to the analyzer tests. / ビルドエラーを出すアナライザー診断は、アナライザーの試験に加えて解全体を検証した。
- [ ] For public API or generated descriptor changes, the compatibility, deterministic-generation or golden-data checks of the affected subsystem were run. / 公開APIまたは生成される記述子を変えた場合、該当部分の互換性、生成の決定性、または基準データの検査を走らせた。

## If this claims a performance improvement / 性能の改善を主張する場合

<!--
Delete this section otherwise. / それ以外の場合は、この節ごと消してください。
-->

- [ ] The baseline and the candidate were compared repeatedly under the same hardware, driver, configuration and power conditions. / 同じ機材、ドライバー、構成、電源条件のもとで、基準と候補を繰り返し比較した。
- [ ] Enough measurements are reported to distinguish the change from normal run-to-run variation. / 通常の実行ごとのばらつきと区別できるだけの測定値を示した。
- [ ] Correctness validation and performance measurement were run separately. / 正しさの検証と性能の測定を別々に走らせた。

<!--
Measurements. / 測定値。
-->

## Notes for the reviewer / 査読者への注記

<!--
Anything you are unsure about, a decision you would like challenged, or something you deliberately
left out and why.
判断に迷った点、異論が欲しい判断、意図して外した部分とその理由。
-->
