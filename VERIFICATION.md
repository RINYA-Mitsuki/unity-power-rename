# VPM 構築・検証結果

確認日: 2026-09-15

## 公開したもの

- パッケージ: https://github.com/RINYA-Mitsuki/unity-power-rename
- Release: https://github.com/RINYA-Mitsuki/unity-power-rename/releases/tag/v1.0.0-beta.1
- 共通 Listing リポジトリ: https://github.com/RINYA-Mitsuki/vpm-repository
- VCC 登録 URL: https://rinya-mitsuki.github.io/vpm-repository/index.json
- 標準仕様: https://github.com/RINYA-Mitsuki/vpm-repository/blob/main/docs/STANDARD.md

## 成功した検証

| 項目 | 結果 |
|---|---|
| 元コードの保持 | 公開 ZIP の Editor/UnityPowerRenameWindow.cs と元の UnityPowerRenameWindow_v7.cs がバイト単位で一致 |
| C# コンパイル | Unity 2022.3.22f1 付属のコンパイラー、UnityEditor / UnityEngine / .NET Standard 2.1 参照で成功 |
| パッケージ自動テスト | ZIP ルート、再生成、SemVer の2件に加え、下書き Release 取得の回帰テスト1件が成功 |
| Listing 自動テスト | manifest 整合、ZIP パス、欠落 ZIP、旧版消失・変更、SHA-256、ページ送りの6件が成功 |
| GitHub Release | Release VPM package #2 が成功、beta.1 公開、ZIP / package.json / SHA256SUMS を添付 |
| GitHub Pages | Build VPM listing #1 の再実行が成功 |
| 匿名ダウンロード | Listing が HTTP 200。Listing の URL から認証なしで ZIP を取得できた |
| 配布物の整合 | 公開 ZIP の全13ファイルがローカルのパッケージと一致。ZIP の SHA-256 が Listing と一致 |

Release 実行: https://github.com/RINYA-Mitsuki/unity-power-rename/actions/runs/34964310740

Listing 実行: https://github.com/RINYA-Mitsuki/vpm-repository/actions/runs/34963403793

元コード SHA-256:
`76334bd4ceb8d6fab2d3a022cb76a057a72af3f9700dacc7c5709a26b220bf3b`

公開 ZIP SHA-256:
`7dc7d6b21bee4bef7cf5e25b068cbee2ca8c1e32818bbaffbfc767334e7018ef`

ZIP の圧縮バイト列は異なる Python / zlib 環境で変わる場合があります。公開済み ZIP の内容を変更・再添付せず、Listing は実際の公開 ZIP からハッシュを計算します。

## 未検証の項目

- Unity Editor 起動後の実動作: 独立したテストプロジェクトで起動を試みたが、Unity のキャッシュ書き込み制限により起動時に停止。追加アクセスは得られず、実機テストは未実施。コンパイル成功と実動作確認は別扱い。
- VCC GUI での登録・導入と別バージョンへの更新操作: 未実施。この実行環境ではネイティブアプリ操作が利用できない。
- 公式 VPM CLI 0.1.28 でも確認を試みたが、起動時に既存ログを削除する処理がアクセス制限で停止。公式 CLI による適合判定を通過したとは扱わない。

このため初回版は安定版ではなく `1.0.0-beta.1` として公開しています。

## 初回の導入確認

1. 使用中の Unity プロジェクトをバックアップ。
2. 既存の同ツールの .cs と対応 .meta をプロジェクト外へ退避。Mitsuboshi_Studio/Editor 全体は削除しない。
3. VCC の Settings → Packages → Add Repository に上記 URL を追加。
4. Show Pre-Release Packages を有効にし、テスト用プロジェクトの Manage Project から Unity Power Rename を追加。
5. Unity でコンパイルとメニュー表示を確認し、複製したアセットで検索・置換、連番、GUID 保持を確認。
6. 通常の Undo には非対応。元の v7 の仕様を維持している。

## 更新と再実行

コードの更新時は package.json の version / url と CHANGELOG を更新し、main 反映後に Release VPM package を実行するか、対応タグを push します。共通 Listing は毎時17分（UTC）の定期実行で反映します。即時反映は Build VPM listing を手動実行します。

同じ版の処理を再試行するときは元の実行の Re-run jobs を使用してください。公開後に README 等の別コミットを追加した main から同じ版を再リリースしようとすると、タグのコミット不一致を検出して停止します。
