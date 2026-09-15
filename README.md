# Unity Power Rename

Mitsuboshi_Studio の Unity Editor 用アセット一括リネームツール。従来の v7 を VPM パッケージに移行した試験版です。

## 導入
共通 Listing: https://rinya-mitsuki.github.io/vpm-repository/index.json

VCC の Settings → Packages → Add Repository で上記 URL を追加し、プロジェクトの Manage Project から Unity Power Rename を追加します。beta 版は VCC の Show Pre-Release Packages を有効にしてください。公開状態と検証結果はリポジトリの README を確認してください。

## 旧版からの移行
導入前にプロジェクトをバックアップし、既存の UnityPowerRenameWindow_v7.cs（または同じクラスを定義する旧版）と対応する .meta をプロジェクト外へ退避してください。VPM 版と同時に残すとメニューやクラスが重複します。共有の Mitsuboshi_Studio/Editor フォルダー全体を削除しないでください。このパッケージは旧ファイルを自動削除しません。

## 使い方
Project で対象アセット／フォルダーを選択し、Tools → Mitsuboshi_Studio → Unity Power Rename を開きます。検索・置換、接頭辞／接尾辞、連番を設定し、プレビューを確認して実行します。

- 大文字・小文字を区別しない通常検索に対応。正規表現はありません。
- 連番を有効にすると、置換／接頭辞／接尾辞に {n} を使用できます。
- 再帰取得と種別フィルターに対応。フィルターは初期状態で閉じています。
- プレビューはファイル名のみ、パスはツールチップに表示します。
- AssetDatabase.RenameAsset を使用し、拡張子とアセット GUID を維持します。
- 通常の Unity Undo には非対応です。エラー時は可能な範囲でロールバックを試みます。
- 親フォルダーと配下アセットの同時変更は拒否します。

Unity 2022.3 向け。VRChat SDK は不要です。

## 利用上の注意・サポート方針

本ツールは作者が完全に個人用として作成したものを、現状のまま公開しています。利用はご自身の判断と責任で行い、実行前に必ずプロジェクトのバックアップを取ってください。

動作・品質・特定環境への適合性は保証しません。サポート、不具合修正、機能追加、問い合わせへの回答をお約束するものではなく、**利用したことによる苦情は一切受け付けません**。

本ツールは MIT ライセンスで提供します。無保証・責任制限の条件は [LICENSE.md](LICENSE.md) を参照してください。

## 開発・リリース

標準仕様は共通リポジトリ `vpm-repository/docs/STANDARD.md` を参照してください。
`package.json` の version と url、CHANGELOG を更新し、main へ反映後に Actions の **Release VPM package** を手動実行するか、対応する `v{version}` タグを push します。
ZIP と manifest を作成して Release に添付します。同じ版のアセットは上書きできません。共通 Listing は毎時17分（UTC）に確認するため、即時反映には共通リポジトリの **Build VPM listing** を手動実行します。

```sh
python -m unittest discover -s tests -v
python scripts/package.py Packages/com.mitsuboshi-studio.unity-power-rename --repository RINYA-Mitsuki/unity-power-rename
```

Unity コンパイル・動作検証は別途必要です。上記 CI はパッケージ形式と配布物を検証します。

## 検証状況

公開 ZIP と Listing の整合および C# コンパイルを確認済みです。VCC GUI と Unity Editor 内の実動作は未検証です。詳細は [検証結果](VERIFICATION.md) を参照してください。
