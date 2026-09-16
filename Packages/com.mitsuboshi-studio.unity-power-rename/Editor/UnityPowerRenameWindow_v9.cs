// Unity Power Rename v9 — based on v7; Asset / Hierarchy GameObject modes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Rinya.AssetTools
{
    public sealed class UnityPowerRenameWindow : UnityEditor.EditorWindow
    {
        private enum RenameMode { Asset, HierarchyGameObject }
        private RenameMode _mode;
        private bool _includeChildren;

        [Flags]
        private enum AssetTypeFilter
        {
            None = 0,
            Texture = 1 << 0,
            Material = 1 << 1,
            Prefab = 1 << 2,
            AnimationClip = 1 << 3,
            AnimatorController = 1 << 4,
            Audio = 1 << 5,
            Script = 1 << 6,
            Shader = 1 << 7,
            Scene = 1 << 8,
            Other = 1 << 9,
            All = Texture | Material | Prefab | AnimationClip | AnimatorController |
                  Audio | Script | Shader | Scene | Other
        }

        private enum NumberPosition
        {
            Suffix,
            Prefix
        }

        private sealed class RenameItem
        {
            public UnityEngine.Object Asset;
            public string OldPath;
            public string OldBaseName;
            public string Extension;
            public bool IsFolder;
            public AssetTypeFilter Category;

            public string NewBaseName;
            public string NewPath;
            public string Error;
            public bool WillChange;
            public List<Vector2Int> MatchRanges = new List<Vector2Int>();
        }

        private sealed class StagedRename
        {
            public RenameItem Item;
            public string TempBaseName;
            public string TempPath;
            public bool Finalized;
        }

        private readonly List<RenameItem> _items = new List<RenameItem>();

        private string _searchText = "";
        private string _replaceText = "";
        private string _prefixText = "";
        private string _suffixText = "";
        private bool _caseSensitive;

        private bool _includeSelectedFolders;
        private bool _includeFolderContentsRecursive = true;
        private AssetTypeFilter _assetTypeFilter = AssetTypeFilter.All;
        private bool _filterFoldout = false;

        private bool _useNumbering;
        private int _numberStart = 1;
        private int _numberDigits = 2;
        private string _numberSeparator = "_";
        private NumberPosition _numberPosition = NumberPosition.Suffix;

        private Vector2 _scroll;
        private string _globalError = "";

        private GUIStyle _richNameStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _headerStyle;

        [UnityEditor.MenuItem("Assets/Unity Power Rename...", false, 2000)]
        private static void OpenFromAssetsMenu()
        {
            OpenWindow();
            GetWindow<UnityPowerRenameWindow>().SetMode(RenameMode.Asset);
        }

        [UnityEditor.MenuItem("Assets/Unity Power Rename...", true)]
        private static bool ValidateAssetsMenu()
        {
            return UnityEditor.Selection.objects
                .Select(UnityEditor.AssetDatabase.GetAssetPath)
                .Any(path => !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal));
        }

        [UnityEditor.MenuItem("Tools/Mitsuboshi_Studio/Unity Power Rename")]
        private static void OpenFromToolsMenu()
        {
            OpenWindow();
        }

        private static void OpenWindow()
        {
            UnityPowerRenameWindow window = GetWindow<UnityPowerRenameWindow>("Unity Power Rename");
            window.minSize = new Vector2(880f, 560f);
            window.RefreshSelection();
            window.Show();
        }

        private void OnEnable()
        {
            UnityEditor.Undo.undoRedoPerformed += RefreshHierarchy;
            UnityEditor.EditorApplication.hierarchyChanged += RefreshHierarchy;
            RefreshSelection();
        }

        private void OnDisable()
        {
            UnityEditor.Undo.undoRedoPerformed -= RefreshHierarchy;
            UnityEditor.EditorApplication.hierarchyChanged -= RefreshHierarchy;
        }

        private void RefreshHierarchy()
        {
            if (_mode != RenameMode.HierarchyGameObject) return;
            RefreshSelection();
            Repaint();
        }

        private void SetMode(RenameMode mode)
        {
            _mode = mode;
            _scroll = Vector2.zero;
            RefreshSelection();
        }

        private static bool IsSceneObject(GameObject go)
        {
            return go != null && !UnityEditor.EditorUtility.IsPersistent(go) &&
                go.scene.IsValid() && go.scene.isLoaded &&
                (go.hideFlags & HideFlags.NotEditable) == 0;
        }

        private static string HierarchyOrder(GameObject go)
        {
            string key = "";
            for (Transform t = go.transform; t != null; t = t.parent)
                key = t.GetSiblingIndex().ToString("D10") + "/" + key;
            return go.scene.handle.ToString("D10") + "/" + key;
        }

        private string HierarchyPath(GameObject go, bool preview)
        {
            var names = new List<string>();
            for (Transform t = go.transform; t != null; t = t.parent)
            {
                RenameItem item = preview ? _items.Find(x => x.Asset == t.gameObject) : null;
                names.Add(item != null ? item.NewBaseName : t.name);
            }
            names.Reverse();
            return go.scene.name + "/" + string.Join("/", names);
        }

        private void CollectHierarchy()
        {
            var objects = new HashSet<GameObject>();
            foreach (GameObject selected in UnityEditor.Selection.gameObjects)
            {
                if (!IsSceneObject(selected)) continue;
                objects.Add(selected);
                if (_includeChildren)
                    foreach (Transform child in selected.GetComponentsInChildren<Transform>(true))
                        if (IsSceneObject(child.gameObject)) objects.Add(child.gameObject);
            }
            foreach (GameObject go in objects.OrderBy(HierarchyOrder, StringComparer.Ordinal))
                _items.Add(new RenameItem {
                    Asset = go, OldBaseName = go.name, Extension = "",
                    OldPath = HierarchyPath(go, false)
                });
        }

        private void OnSelectionChange()
        {
            RefreshSelection();
            Repaint();
        }

        private void EnsureStyles()
        {
            if (_richNameStyle == null)
            {
                _richNameStyle = new GUIStyle(UnityEditor.EditorStyles.label)
                {
                    richText = true,
                    clipping = TextClipping.Clip
                };
            }

            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(UnityEditor.EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(UnityEditor.EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleLeft
                };
            }
        }

        private void RefreshSelection()
        {
            _items.Clear();
            if (_mode == RenameMode.HierarchyGameObject)
            {
                CollectHierarchy();
                RebuildPreview();
                return;
            }

            IEnumerable<string> selectedPaths = UnityEditor.Selection.objects
                .Select(UnityEditor.AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Select(NormalizePath)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            HashSet<string> collectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string selectedPath in selectedPaths)
            {
                bool isFolder = UnityEditor.AssetDatabase.IsValidFolder(selectedPath);

                if (!isFolder)
                {
                    collectedPaths.Add(selectedPath);
                    continue;
                }

                if (_includeSelectedFolders)
                    collectedPaths.Add(selectedPath);

                if (_includeFolderContentsRecursive)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets(string.Empty, new[] { selectedPath });
                    foreach (string guid in guids)
                    {
                        string path = NormalizePath(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                        if (string.IsNullOrEmpty(path) || string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // 再帰取得ではファイルを対象にする。
                        // フォルダ自体は「選択したフォルダ自体も対象」で明示的に選ばれたものだけを扱う。
                        if (UnityEditor.AssetDatabase.IsValidFolder(path))
                            continue;

                        collectedPaths.Add(path);
                    }
                }
            }

            foreach (string path in collectedPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                bool isFolder = UnityEditor.AssetDatabase.IsValidFolder(path);
                AssetTypeFilter category = isFolder ? AssetTypeFilter.Other : GetAssetCategory(path);

                if (!isFolder && (_assetTypeFilter & category) == 0)
                    continue;

                string fileName = Path.GetFileName(path);
                string extension = isFolder ? "" : Path.GetExtension(fileName);
                string baseName = isFolder
                    ? fileName
                    : Path.GetFileNameWithoutExtension(fileName);

                _items.Add(new RenameItem
                {
                    Asset = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path),
                    OldPath = path,
                    OldBaseName = baseName,
                    Extension = extension,
                    IsFolder = isFolder,
                    Category = category
                });
            }

            RebuildPreview();
        }

        private void OnGUI()
        {
            EnsureStyles();
            int mode = GUILayout.Toolbar((int)_mode, new[] { "Asset", "Hierarchy GameObject" });
            if (mode != (int)_mode) SetMode((RenameMode)mode);
            DrawHeader();

            UnityEditor.EditorGUI.BeginChangeCheck();

            DrawRenameRules();
            UnityEditor.EditorGUILayout.Space(6f);
            DrawTargetOptions();

            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                RefreshSelection();
            }

            UnityEditor.EditorGUILayout.Space(8f);
            DrawPreview();

            UnityEditor.EditorGUILayout.Space(8f);
            DrawExecuteArea();
        }

        private void DrawHeader()
        {
            using (new UnityEditor.EditorGUILayout.HorizontalScope())
            {
                UnityEditor.EditorGUILayout.LabelField(
                    $"対象: {_items.Count} 件",
                    UnityEditor.EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("選択を再読込", GUILayout.Width(110f)))
                    RefreshSelection();
            }

            UnityEditor.EditorGUILayout.HelpBox(
                _mode == RenameMode.HierarchyGameObject
                    ? "Hierarchyで選択したGameObjectを一括リネームします。子を含める場合は非アクティブも対象です。連番はHierarchy順。同名も使用でき、Undoでまとめて戻せます。"
                    : "Projectウィンドウで選択したアセットを一括リネームします。拡張子は変更せず、AssetDatabase.RenameAsset を使うため .meta のGUIDは維持されます。",
                UnityEditor.MessageType.Info);
        }

        private void DrawRenameRules()
        {
            UnityEditor.EditorGUILayout.LabelField("名前変更ルール", UnityEditor.EditorStyles.boldLabel);

            _searchText = UnityEditor.EditorGUILayout.TextField("検索", _searchText);
            _replaceText = UnityEditor.EditorGUILayout.TextField("置換", _replaceText);

            _caseSensitive = UnityEditor.EditorGUILayout.ToggleLeft(
                "大文字・小文字を区別",
                _caseSensitive);

            _prefixText = UnityEditor.EditorGUILayout.TextField("接頭辞を追加", _prefixText);
            _suffixText = UnityEditor.EditorGUILayout.TextField("接尾辞を追加", _suffixText);

            UnityEditor.EditorGUILayout.Space(3f);
            _useNumbering = UnityEditor.EditorGUILayout.ToggleLeft("連番を使用", _useNumbering);

            if (_useNumbering)
            {
                using (new UnityEditor.EditorGUI.IndentLevelScope())
                {
                    using (new UnityEditor.EditorGUILayout.HorizontalScope())
                    {
                        _numberStart = UnityEditor.EditorGUILayout.IntField("開始番号", _numberStart);
                        _numberDigits = Mathf.Clamp(UnityEditor.EditorGUILayout.IntField("桁数", _numberDigits), 1, 8);
                    }

                    _numberSeparator = UnityEditor.EditorGUILayout.TextField("区切り文字", _numberSeparator);
                    _numberPosition = (NumberPosition)UnityEditor.EditorGUILayout.EnumPopup("{n}未使用時の位置", _numberPosition);

                    UnityEditor.EditorGUILayout.HelpBox(
                        "置換・接頭辞・接尾辞の好きな位置に {n} を書くと、そこへ連番を挿入します。{n} がどこにも無い場合は従来どおり前または後ろへ連番を付加します。",
                        UnityEditor.MessageType.None);
                }
            }
        }

        private void DrawTargetOptions()
        {
            if (_mode == RenameMode.HierarchyGameObject)
            {
                _includeChildren = UnityEditor.EditorGUILayout.ToggleLeft(
                    "選択GameObject配下の子を再帰的に含める（非アクティブを含む）", _includeChildren);
                return;
            }
            UnityEditor.EditorGUILayout.LabelField("対象", UnityEditor.EditorStyles.boldLabel);

            _includeFolderContentsRecursive = UnityEditor.EditorGUILayout.ToggleLeft(
                "選択したフォルダ内のアセットを再帰的に含める",
                _includeFolderContentsRecursive);

            _includeSelectedFolders = UnityEditor.EditorGUILayout.ToggleLeft(
                "選択したフォルダ自体も対象に含める",
                _includeSelectedFolders);

            _filterFoldout = UnityEditor.EditorGUILayout.Foldout(
                _filterFoldout,
                "アセット種別フィルタ",
                true);

            if (_filterFoldout)
            {
                using (new UnityEditor.EditorGUI.IndentLevelScope())
                {
                    using (new UnityEditor.EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("すべて", GUILayout.Width(70f)))
                        {
                            _assetTypeFilter = AssetTypeFilter.All;
                            GUI.changed = true;
                        }

                        if (GUILayout.Button("解除", GUILayout.Width(70f)))
                        {
                            _assetTypeFilter = AssetTypeFilter.None;
                            GUI.changed = true;
                        }
                    }

                    DrawFilterToggle("Texture", AssetTypeFilter.Texture);
                    DrawFilterToggle("Material", AssetTypeFilter.Material);
                    DrawFilterToggle("Prefab", AssetTypeFilter.Prefab);
                    DrawFilterToggle("Animation Clip", AssetTypeFilter.AnimationClip);
                    DrawFilterToggle("Animator Controller / Override", AssetTypeFilter.AnimatorController);
                    DrawFilterToggle("Audio", AssetTypeFilter.Audio);
                    DrawFilterToggle("Script", AssetTypeFilter.Script);
                    DrawFilterToggle("Shader", AssetTypeFilter.Shader);
                    DrawFilterToggle("Scene", AssetTypeFilter.Scene);
                    DrawFilterToggle("Other", AssetTypeFilter.Other);
                }
            }
        }

        private void DrawFilterToggle(string label, AssetTypeFilter flag)
        {
            bool current = (_assetTypeFilter & flag) != 0;
            bool next = UnityEditor.EditorGUILayout.ToggleLeft(label, current);

            if (next == current)
                return;

            if (next)
                _assetTypeFilter |= flag;
            else
                _assetTypeFilter &= ~flag;
        }

        private void DrawPreview()
        {
            UnityEditor.EditorGUILayout.LabelField("プレビュー", UnityEditor.EditorStyles.boldLabel);

            float statusWidth = 90f;
            float availableWidth = Mathf.Max(300f, position.width - statusWidth - 38f);
            float columnWidth = availableWidth * 0.5f;

            using (new UnityEditor.EditorGUILayout.HorizontalScope(UnityEditor.EditorStyles.toolbar))
            {
                GUILayout.Label("変更前", _headerStyle, GUILayout.Width(columnWidth));
                GUILayout.Label("変更後", _headerStyle, GUILayout.Width(columnWidth));
                GUILayout.Label("状態", _headerStyle, GUILayout.Width(statusWidth));
            }

            _scroll = UnityEditor.EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            if (_items.Count == 0)
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    _mode == RenameMode.HierarchyGameObject
                        ? "Hierarchyで対象のGameObjectを選択してください。"
                        : "現在の選択・フォルダ設定・種別フィルタに一致するアセットがありません。",
                    UnityEditor.MessageType.None);
            }
            else
            {
                foreach (RenameItem item in _items)
                {
                    using (new UnityEditor.EditorGUILayout.VerticalScope(UnityEditor.EditorStyles.helpBox))
                    {
                        using (new UnityEditor.EditorGUILayout.HorizontalScope())
                        {
                            string oldRich = BuildOldHighlightedName(item) + EscapeRichText(item.Extension);
                            string newRich = BuildNewHighlightedName(item) + EscapeRichText(item.Extension);

                            GUILayout.Label(new GUIContent(oldRich, item.OldPath), _richNameStyle, GUILayout.Width(columnWidth));
                            GUILayout.Label(new GUIContent(newRich, item.NewPath), _richNameStyle, GUILayout.Width(columnWidth));

                            if (!string.IsNullOrEmpty(item.Error))
                                GUILayout.Label(new GUIContent("エラー", item.Error), _statusStyle, GUILayout.Width(statusWidth));
                            else if (item.WillChange)
                                GUILayout.Label("変更", _statusStyle, GUILayout.Width(statusWidth));
                            else
                                GUILayout.Label("変更なし", _statusStyle, GUILayout.Width(statusWidth));
                        }

                        if (!string.IsNullOrEmpty(item.Error))
                            UnityEditor.EditorGUILayout.HelpBox(item.Error, UnityEditor.MessageType.Error);
                    }
                }
            }

            UnityEditor.EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_globalError))
                UnityEditor.EditorGUILayout.HelpBox(_globalError, UnityEditor.MessageType.Error);
        }

        private void DrawExecuteArea()
        {
            int changeCount = _items.Count(item => item.WillChange);
            bool hasErrors = !string.IsNullOrEmpty(_globalError) || _items.Any(item => !string.IsNullOrEmpty(item.Error));

            using (new UnityEditor.EditorGUI.DisabledScope(changeCount == 0 || hasErrors))
            {
                if (GUILayout.Button($"名前変更を実行 ({changeCount} 件)", GUILayout.Height(36f)))
                {
                    ExecuteRename();
                }
            }
        }

        private void RebuildPreview()
        {
            _globalError = "";

            for (int i = 0; i < _items.Count; i++)
            {
                RenameItem item = _items[i];
                item.Error = "";
                item.MatchRanges.Clear();

                string result = item.OldBaseName;
                string number = (_numberStart + i).ToString(new string('0', _numberDigits));
                bool hasNumberToken = ContainsNumberToken(_replaceText) ||
                                      ContainsNumberToken(_prefixText) ||
                                      ContainsNumberToken(_suffixText);

                CollectMatchRanges(item);

                if (!string.IsNullOrEmpty(_searchText))
                {
                    string processedReplacement = ApplyNumberToken(_replaceText ?? "", number);

                    result = _caseSensitive
                        ? result.Replace(_searchText, processedReplacement)
                        : ReplaceIgnoreCase(result, _searchText, processedReplacement);
                }

                result = ApplyNumberToken(_prefixText ?? "", number) +
                         result +
                         ApplyNumberToken(_suffixText ?? "", number);

                if (_useNumbering && !hasNumberToken)
                {
                    string token = (_numberSeparator ?? "") + number;
                    result = _numberPosition == NumberPosition.Prefix
                        ? token + result
                        : result + token;
                }

                item.NewBaseName = result;

                if (_mode == RenameMode.HierarchyGameObject)
                {
                    item.WillChange = !string.Equals(item.OldBaseName, result, StringComparison.Ordinal);
                    if (!IsSceneObject(item.Asset as GameObject))
                        item.Error = "対象が削除されたか、編集できない状態です。選択を再読込してください。";
                    else if (item.Asset.name != item.OldBaseName)
                        item.Error = "対象の名前が変更されています。選択を再読込してください。";
                    else if (item.WillChange && string.IsNullOrWhiteSpace(result))
                        item.Error = "変更後の名前が空です。";
                    continue;
                }

                string directory = NormalizePath(Path.GetDirectoryName(item.OldPath) ?? "");
                item.NewPath = string.IsNullOrEmpty(directory)
                    ? result + item.Extension
                    : directory + "/" + result + item.Extension;

                item.WillChange = !string.Equals(
                    item.OldBaseName,
                    item.NewBaseName,
                    StringComparison.Ordinal);

                ValidateBasicName(item);
            }

            if (_mode == RenameMode.HierarchyGameObject)
            {
                foreach (RenameItem item in _items)
                    item.NewPath = item.Asset != null ? HierarchyPath((GameObject)item.Asset, true) : "";
                return;
            }
            ValidateParentChildFolderRenames();
            ValidateDuplicateTargets();
            ValidateExistingTargets();
        }

        private void CollectMatchRanges(RenameItem item)
        {
            if (string.IsNullOrEmpty(_searchText))
                return;

            StringComparison comparison = _caseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            int start = 0;
            while (start <= item.OldBaseName.Length - _searchText.Length)
            {
                int index = item.OldBaseName.IndexOf(_searchText, start, comparison);
                if (index < 0)
                    break;

                item.MatchRanges.Add(new Vector2Int(index, _searchText.Length));
                start = index + Math.Max(1, _searchText.Length);
            }
        }

        private static string ReplaceIgnoreCase(string source, string search, string replacement)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(search))
                return source ?? "";

            replacement = replacement ?? "";

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            int cursor = 0;

            while (cursor < source.Length)
            {
                int index = source.IndexOf(search, cursor, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    builder.Append(source, cursor, source.Length - cursor);
                    break;
                }

                builder.Append(source, cursor, index - cursor);
                builder.Append(replacement);
                cursor = index + search.Length;
            }

            return builder.ToString();
        }

        private static bool ContainsNumberToken(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Contains("{n}");
        }

        private string ApplyNumberToken(string text, string number)
        {
            if (!_useNumbering || string.IsNullOrEmpty(text))
                return text ?? "";

            return text.Replace("{n}", number);
        }

        private string BuildOldHighlightedName(RenameItem item)
        {
            if (item.MatchRanges == null || item.MatchRanges.Count == 0)
                return EscapeRichText(item.OldBaseName);

            List<Vector2Int> ranges = item.MatchRanges
                .Where(range => range.x >= 0 && range.y > 0 && range.x + range.y <= item.OldBaseName.Length)
                .OrderBy(range => range.x)
                .ToList();

            if (ranges.Count == 0)
                return EscapeRichText(item.OldBaseName);

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            int cursor = 0;

            foreach (Vector2Int range in ranges)
            {
                if (range.x < cursor)
                    continue;

                builder.Append(EscapeRichText(item.OldBaseName.Substring(cursor, range.x - cursor)));
                builder.Append("<b><color=#FFD45A>");
                builder.Append(EscapeRichText(item.OldBaseName.Substring(range.x, range.y)));
                builder.Append("</color></b>");
                cursor = range.x + range.y;
            }

            if (cursor < item.OldBaseName.Length)
                builder.Append(EscapeRichText(item.OldBaseName.Substring(cursor)));

            return builder.ToString();
        }

        private string BuildNewHighlightedName(RenameItem item)
        {
            string oldName = item.OldBaseName ?? "";
            string newName = item.NewBaseName ?? oldName;

            if (!item.WillChange)
                return EscapeRichText(newName);

            int prefix = 0;
            int maxPrefix = Math.Min(oldName.Length, newName.Length);
            while (prefix < maxPrefix && oldName[prefix] == newName[prefix])
                prefix++;

            int suffix = 0;
            int oldRemaining = oldName.Length - prefix;
            int newRemaining = newName.Length - prefix;
            int maxSuffix = Math.Min(oldRemaining, newRemaining);

            while (suffix < maxSuffix &&
                   oldName[oldName.Length - 1 - suffix] == newName[newName.Length - 1 - suffix])
            {
                suffix++;
            }

            string before = newName.Substring(0, prefix);
            string changed = newName.Substring(prefix, newName.Length - prefix - suffix);
            string after = suffix > 0 ? newName.Substring(newName.Length - suffix) : "";

            if (changed.Length == 0)
                return "<b><color=#7ED6FF>" + EscapeRichText(newName) + "</color></b>";

            return EscapeRichText(before) +
                   "<b><color=#7ED6FF>" + EscapeRichText(changed) + "</color></b>" +
                   EscapeRichText(after);
        }

        private static string EscapeRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static void ValidateBasicName(RenameItem item)
        {
            if (!item.WillChange)
                return;

            if (string.IsNullOrWhiteSpace(item.NewBaseName))
            {
                item.Error = "変更後の名前が空です。";
                return;
            }

            if (item.NewBaseName.EndsWith(" ", StringComparison.Ordinal) ||
                item.NewBaseName.EndsWith(".", StringComparison.Ordinal))
            {
                item.Error = "ファイル名の末尾に半角スペースまたはピリオドは使用できません。";
                return;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (item.NewBaseName.IndexOfAny(invalidChars) >= 0 ||
                item.NewBaseName.Contains("/") ||
                item.NewBaseName.Contains("\\"))
            {
                item.Error = "変更後の名前に使用できない文字が含まれています。";
            }
        }

        private void ValidateParentChildFolderRenames()
        {
            List<RenameItem> changedFolders = _items
                .Where(item => item.IsFolder && item.WillChange && string.IsNullOrEmpty(item.Error))
                .ToList();

            if (changedFolders.Count == 0)
                return;

            foreach (RenameItem folder in changedFolders)
            {
                string prefix = folder.OldPath.TrimEnd('/') + "/";

                foreach (RenameItem other in _items)
                {
                    if (ReferenceEquals(folder, other) || !other.WillChange)
                        continue;

                    if (other.OldPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        folder.Error = "親フォルダと、その配下のアセットを同じバッチで同時に変更することはできません。";
                        other.Error = "名前変更対象の親フォルダも同時に選択されています。";
                    }
                }
            }
        }

        private void ValidateDuplicateTargets()
        {
            var groups = _items
                .Where(item => item.WillChange && string.IsNullOrEmpty(item.Error))
                .GroupBy(item => item.NewPath, StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string, RenameItem> group in groups)
            {
                if (group.Count() <= 1)
                    continue;

                foreach (RenameItem item in group)
                    item.Error = "複数のアセットが同じ変更後パスになります。";
            }
        }

        private void ValidateExistingTargets()
        {
            HashSet<string> movableOldPaths = new HashSet<string>(
                _items
                    .Where(item => item.WillChange && string.IsNullOrEmpty(item.Error))
                    .Select(item => item.OldPath),
                StringComparer.OrdinalIgnoreCase);

            foreach (RenameItem item in _items)
            {
                if (!item.WillChange || !string.IsNullOrEmpty(item.Error))
                    continue;

                if (!PathExistsInProject(item.NewPath))
                    continue;

                // A→B / B→A のように、変更対象同士で場所を入れ替える場合は許可。
                if (movableOldPaths.Contains(item.NewPath))
                    continue;

                item.Error = "変更後の名前と同名のアセットがすでに存在します。";
            }
        }

        private void ExecuteRename()
        {
            RebuildPreview();

            if (!string.IsNullOrEmpty(_globalError) ||
                _items.Any(item => !string.IsNullOrEmpty(item.Error)))
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Unity Power Rename",
                    "エラーがあるため実行できません。プレビューを確認してください。",
                    "OK");
                return;
            }

            List<RenameItem> changed = _items
                .Where(item => item.WillChange)
                .ToList();

            if (changed.Count == 0)
                return;

            if (_mode == RenameMode.HierarchyGameObject)
            {
                ExecuteHierarchyRename(changed);
                return;
            }

            List<StagedRename> staged = new List<StagedRename>();

            try
            {
                // 1. 全対象を一意な一時名へ退避。
                //    これにより A→B / B→A のような名前交換でも衝突しない。
                foreach (RenameItem item in changed)
                {
                    string tempBase = CreateUniqueTempBaseName(item);
                    string error = UnityEditor.AssetDatabase.RenameAsset(item.OldPath, tempBase);

                    if (!string.IsNullOrEmpty(error))
                        throw new InvalidOperationException(item.OldPath + "\n" + error);

                    string directory = NormalizePath(Path.GetDirectoryName(item.OldPath) ?? "");
                    string tempPath = string.IsNullOrEmpty(directory)
                        ? tempBase + item.Extension
                        : directory + "/" + tempBase + item.Extension;

                    staged.Add(new StagedRename
                    {
                        Item = item,
                        TempBaseName = tempBase,
                        TempPath = tempPath,
                        Finalized = false
                    });
                }

                // 2. 一時名から最終名へ変更。
                foreach (StagedRename stage in staged)
                {
                    string error = UnityEditor.AssetDatabase.RenameAsset(
                        stage.TempPath,
                        stage.Item.NewBaseName);

                    if (!string.IsNullOrEmpty(error))
                        throw new InvalidOperationException(stage.TempPath + "\n" + error);

                    stage.Finalized = true;
                }

                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();

                RefreshSelection();

                UnityEditor.EditorUtility.DisplayDialog(
                    "Unity Power Rename",
                    changed.Count + " 件の名前変更が完了しました。",
                    "OK");
            }
            catch (Exception ex)
            {
                Rollback(staged);

                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();

                RefreshSelection();

                Debug.LogError("[Unity Power Rename] Rename failed and rollback was attempted.\n" + ex);
                UnityEditor.EditorUtility.DisplayDialog(
                    "Unity Power Rename",
                    "名前変更中にエラーが発生しました。\n可能な範囲で元の名前へロールバックしました。\n\nConsoleも確認してください。",
                    "OK");
            }
        }

        private void ExecuteHierarchyRename(List<RenameItem> changed)
        {
            UnityEditor.Undo.IncrementCurrentGroup();
            int group = UnityEditor.Undo.GetCurrentGroup();
            UnityEditor.Undo.SetCurrentGroupName("Unity Power Rename: GameObjects");
            try
            {
                UnityEditor.Undo.RegisterCompleteObjectUndo(
                    changed.Select(item => item.Asset).ToArray(), "Unity Power Rename: GameObjects");
                foreach (RenameItem item in changed)
                {
                    GameObject go = (GameObject)item.Asset;
                    go.name = item.NewBaseName;
                    if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(go))
                        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
                }
                UnityEditor.Undo.CollapseUndoOperations(group);
            }
            catch (Exception ex)
            {
                UnityEditor.Undo.RevertAllDownToGroup(group);
                Debug.LogException(ex);
                UnityEditor.EditorUtility.DisplayDialog("Unity Power Rename",
                    "名前変更に失敗したため元に戻しました。Consoleを確認してください。", "OK");
            }
            finally
            {
                UnityEditor.Undo.IncrementCurrentGroup();
                RefreshSelection();
                UnityEditor.EditorApplication.RepaintHierarchyWindow();
                Repaint();
            }
        }

        private static void Rollback(List<StagedRename> staged)
        {
            for (int i = staged.Count - 1; i >= 0; i--)
            {
                StagedRename stage = staged[i];
                if (!stage.Finalized)
                    continue;

                string error = UnityEditor.AssetDatabase.RenameAsset(
                    stage.Item.NewPath,
                    stage.TempBaseName);

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError(
                        "[Unity Power Rename] Failed to move finalized asset back to temp name:\n" +
                        stage.Item.NewPath + "\n" + error);
                }
                else
                {
                    stage.Finalized = false;
                }
            }

            for (int i = staged.Count - 1; i >= 0; i--)
            {
                StagedRename stage = staged[i];

                if (stage.Finalized)
                    continue;

                if (!PathExistsInProject(stage.TempPath))
                    continue;

                string error = UnityEditor.AssetDatabase.RenameAsset(
                    stage.TempPath,
                    stage.Item.OldBaseName);

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError(
                        "[Unity Power Rename] Failed to restore original asset name:\n" +
                        stage.TempPath + "\n" + error);
                }
            }
        }

        private static string CreateUniqueTempBaseName(RenameItem item)
        {
            string directory = NormalizePath(Path.GetDirectoryName(item.OldPath) ?? "");

            while (true)
            {
                string candidate = "__RINYA_BATCH_RENAME_TMP__" + Guid.NewGuid().ToString("N");
                string candidatePath = string.IsNullOrEmpty(directory)
                    ? candidate + item.Extension
                    : directory + "/" + candidate + item.Extension;

                if (!PathExistsInProject(candidatePath))
                    return candidate;
            }
        }

        private static AssetTypeFilter GetAssetCategory(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();

            switch (extension)
            {
                case ".prefab":
                    return AssetTypeFilter.Prefab;
                case ".anim":
                    return AssetTypeFilter.AnimationClip;
                case ".controller":
                case ".overridecontroller":
                    return AssetTypeFilter.AnimatorController;
                case ".wav":
                case ".mp3":
                case ".ogg":
                case ".aiff":
                case ".aif":
                    return AssetTypeFilter.Audio;
                case ".cs":
                case ".js":
                    return AssetTypeFilter.Script;
                case ".shader":
                case ".compute":
                case ".shadergraph":
                case ".shadersubgraph":
                    return AssetTypeFilter.Shader;
                case ".unity":
                    return AssetTypeFilter.Scene;
            }

            Type type = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type == null)
                return AssetTypeFilter.Other;

            if (typeof(Texture).IsAssignableFrom(type))
                return AssetTypeFilter.Texture;

            if (typeof(Material).IsAssignableFrom(type))
                return AssetTypeFilter.Material;

            if (typeof(AnimationClip).IsAssignableFrom(type))
                return AssetTypeFilter.AnimationClip;

            if (typeof(AudioClip).IsAssignableFrom(type))
                return AssetTypeFilter.Audio;

            string typeName = type.FullName ?? type.Name;

            if (typeName == "UnityEditor.MonoScript")
                return AssetTypeFilter.Script;

            if (typeName.IndexOf("AnimatorController", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("AnimatorOverrideController", StringComparison.OrdinalIgnoreCase) >= 0)
                return AssetTypeFilter.AnimatorController;

            if (typeName == "UnityEditor.SceneAsset")
                return AssetTypeFilter.Scene;

            if (typeName.IndexOf("Shader", StringComparison.OrdinalIgnoreCase) >= 0)
                return AssetTypeFilter.Shader;

            return AssetTypeFilter.Other;
        }

        private static bool PathExistsInProject(string assetPath)
        {
            if (UnityEditor.AssetDatabase.IsValidFolder(assetPath))
                return true;

            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                return true;

            string fullPath = Path.GetFullPath(assetPath);
            return File.Exists(fullPath) || Directory.Exists(fullPath);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? "").Replace('\\', '/');
        }
    }
}
