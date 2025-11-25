using System.Diagnostics;

namespace ImageJudgement2
{
    /// <summary>
    /// ツリービュー関連の処理
    /// </summary>
    public partial class Form1
    {
        private ContextMenuStrip? folderContextMenu;

        private void CreateBrowserPanels()
        {
            leftPanel = new Panel { BackColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle };

            // 左パネルを上下に分割
            leftTopPanel = new Panel { BackColor = SystemColors.Window, BorderStyle = BorderStyle.None };
            leftBottomPanel = new Panel { BackColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle };

            rightPanel = new Panel { BackColor = SystemColors.Control, BorderStyle = BorderStyle.None };
            rightTopPanel = new Panel { BackColor = SystemColors.Control, BorderStyle = BorderStyle.FixedSingle };
            rightBottomPanel = new Panel { BackColor = SystemColors.ControlDark, BorderStyle = BorderStyle.None };

            // 右下パネルを左右に分割
            rightBottomLeftPanel = new Panel { BackColor = SystemColors.ControlDark, BorderStyle = BorderStyle.FixedSingle };
            rightBottomRightPanel = new Panel { BackColor = SystemColors.Control, BorderStyle = BorderStyle.FixedSingle };

            // フォルダツリービュー (左上) - フォルダのみ表示
            treeBrowser = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
            treeBrowser.BeforeExpand += TreeBrowser_BeforeExpand;
            treeBrowser.NodeMouseDoubleClick += TreeBrowser_NodeMouseDoubleClick;
            treeBrowser.AfterSelect += TreeBrowser_AfterSelect;
            treeBrowser.NodeMouseHover += TreeBrowser_NodeMouseHover;
            treeBrowser.NodeMouseClick += TreeBrowser_NodeMouseClick;

            // コンテキストメニューの作成
            CreateFolderContextMenu();

            // ツールチップ設定
            folderTooltip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ReshowDelay = 100
            };

            // 画像リストビュー (左下) - 選択フォルダ内の画像ファイル一覧
            imageListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false
            };
            imageListView.Columns.Add("ファイル名", 350);
            imageListView.Columns.Add("更新日時", 150);
            imageListView.SelectedIndexChanged += ImageListView_SelectedIndexChanged;
            imageListView.MouseDoubleClick += ImageListView_MouseDoubleClick;

            leftTopPanel.Controls.Add(treeBrowser);
            leftBottomPanel.Controls.Add(imageListView);

            leftPanel.Controls.Add(leftTopPanel);
            leftPanel.Controls.Add(leftBottomPanel);

            rightTopPanel.Paint += RightTopPanel_Paint;
            rightBottomLeftPanel.Paint += RightBottomLeftPanel_Paint;
            rightBottomRightPanel.Paint += RightBottomRightPanel_Paint;

            rightBottomPanel.Controls.Add(rightBottomLeftPanel);
            rightBottomPanel.Controls.Add(rightBottomRightPanel);

            rightPanel.Controls.Add(rightTopPanel);
            rightPanel.Controls.Add(rightBottomPanel);

            this.Controls.Add(rightPanel);
            this.Controls.Add(leftPanel);
        }

        private void CreateFolderContextMenu()
        {
            folderContextMenu = new ContextMenuStrip();

            var menuItem = new ToolStripMenuItem("学習データV2作成");
            menuItem.Click += MenuItem_CreateTrainingDataV2_Click;

            folderContextMenu.Items.Add(menuItem);
        }

        private void TreeBrowser_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // 右クリックされたノードを選択
                treeBrowser.SelectedNode = e.Node;

                // フォルダパスが有効な場合のみコンテキストメニューを表示
                var path = e.Node.Tag as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    folderContextMenu?.Show(treeBrowser, e.Location);
                }
            }
        }

        private void MenuItem_CreateTrainingDataV2_Click(object? sender, EventArgs e)
        {
            if (treeBrowser?.SelectedNode == null)
                return;

            var path = treeBrowser.SelectedNode.Tag as string;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show("有効なフォルダが選択されていません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            CreateTrainingDataV2(path);
        }

        private const string TrainingDataSuffix = "_v2.png";
        private const string CircleDetectionSuffix = "_v2c.png";
        private const string RectangleDetectionSuffix = "_v2r.png";

        private void CreateTrainingDataV2(string folderPath)
        {
            try
            {
                var imageFiles = CollectImageFiles(folderPath);
                if (imageFiles.Count == 0)
                {
                    MessageBox.Show("画像ファイルが見つかりませんでした。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var db = CreateDatabaseHelper();
                if (db == null)
                {
                    Debug.WriteLine("データベース接続が設定されていません。");
                    MessageBox.Show("データベース接続が設定されていません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ProcessImageFilesWithProgress(folderPath, imageFiles, db);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateTrainingDataV2 error: {ex}");
                MessageBox.Show($"処理中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<string> CollectImageFiles(string folderPath)
        {
            var dirOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                RecurseSubdirectories = false
            };

            var files = Directory.EnumerateFiles(folderPath, "*", dirOptions);
            return files.Where(IsImageFile).ToList();
        }

        private void ProcessImageFilesWithProgress(string folderPath, List<string> imageFiles, DatabaseHelper db)
        {
            using var progressForm = CreateProgressForm(imageFiles.Count);
            var progressBar = progressForm.Controls.OfType<ProgressBar>().First();
            var statusLabel = progressForm.Controls.OfType<Label>().First();

            progressForm.Show(this);

            int processedCount = 0;

            foreach (var file in imageFiles)
            {
                try
                {
                    ProcessSingleImageFile(folderPath, file, db);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ファイル '{file}' のDB処理中にエラー: {ex}");
                }
                finally
                {
                    processedCount++;
                    progressBar.Value = Math.Min(processedCount, progressBar.Maximum);
                    statusLabel.Text = $"{processedCount} / {imageFiles.Count}";
                    Application.DoEvents();
                }
            }

            Thread.Sleep(500);
            progressForm.Close();
            MessageBox.Show($"処理が完了しました。\n処理件数: {processedCount}", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private Form CreateProgressForm(int totalCount)
        {
            var progressForm = new Form
            {
                Text = "処理中",
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = totalCount - 1,
                Value = 0,
                Dock = DockStyle.Top,
                Height = 30
            };

            var statusLabel = new Label
            {
                Text = $"0 / {totalCount}",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            progressForm.Controls.Add(statusLabel);
            progressForm.Controls.Add(progressBar);

            return progressForm;
        }

        /// <summary>
        /// 単一画像ファイルを処理して検出画像を生成
        /// </summary>
        /// <param name="folderPath">出力先フォルダパス</param>
        /// <param name="file">処理対象のファイルパス</param>
        /// <param name="db">データベースヘルパー</param>
        private void ProcessSingleImageFile(string folderPath, string file, DatabaseHelper db)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(fileNameWithoutExtension))
            {
                Debug.WriteLine($"[ProcessSingleImageFile] ファイル名が無効: {file}");
                return;
            }

            var outputPaths = GenerateOutputPaths(folderPath, fileNameWithoutExtension);

            // 1. 直接ファイルが存在する場合はそのまま処理
            if (TryProcessExistingFile(file, outputPaths))
            {
                return;
            }

            // 2. DB検索にフォールバック
            ProcessFileViaDatabase(file, fileNameWithoutExtension, outputPaths, db);
        }

        /// <summary>
        /// 既存ファイルの処理を試行
        /// </summary>
        /// <returns>処理成功した場合true</returns>
        private bool TryProcessExistingFile(string file, (string Composite, string Circle, string Rectangle) outputPaths)
        {
            try
            {
                if (!File.Exists(file))
                {
                    return false;
                }

                Debug.WriteLine($"[ProcessSingleImageFile] 直接ファイル処理: {file}");
                ExecuteDetectionTasks(file, outputPaths);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessSingleImageFile] 直接ファイル処理失敗 '{file}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// データベース経由でファイルを検索して処理
        /// </summary>
        private void ProcessFileViaDatabase(
            string file,
            string fileNameWithoutExtension,
            (string Composite, string Circle, string Rectangle) outputPaths,
            DatabaseHelper db)
        {
            var searchKey = ExtractSearchKey(fileNameWithoutExtension);
            if (string.IsNullOrEmpty(searchKey))
            {
                Debug.WriteLine($"[ProcessSingleImageFile] 検索キーが抽出できません: {file}");
                return;
            }

            var sourceImagePath = FindImagePathInDatabase(searchKey, db);
            if (string.IsNullOrEmpty(sourceImagePath))
            {
                Debug.WriteLine($"[ProcessSingleImageFile] DB検索結果なし: {searchKey}");
                return;
            }

            if (!File.Exists(sourceImagePath))
            {
                Debug.WriteLine($"[ProcessSingleImageFile] DB検索結果のファイルが存在しません: {sourceImagePath}");
                return;
            }

            Debug.WriteLine($"[ProcessSingleImageFile] DB経由で処理: {sourceImagePath}");
            ExecuteDetectionTasks(sourceImagePath, outputPaths);
        }

        /// <summary>
        /// データベースから画像パスを検索
        /// </summary>
        /// <param name="searchKey">検索キー</param>
        /// <param name="db">データベースヘルパー</param>
        /// <returns>見つかった画像のフルパス。見つからない場合はnull</returns>
        private string? FindImagePathInDatabase(string searchKey, DatabaseHelper db)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@param", $"%{searchKey}%" }
                };

                var dataTable = db.ExecuteQuery(
                    "SELECT フルパス FROM ScreenshotList WHERE フルパス LIKE @param",
                    parameters);

                if (dataTable?.Rows.Count == 1)
                {
                    return dataTable.Rows[0]["フルパス"]?.ToString();
                }

                if (dataTable?.Rows.Count > 1)
                {
                    Debug.WriteLine($"[FindImagePathInDatabase] 複数の結果が見つかりました: {searchKey} ({dataTable.Rows.Count}件)");
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FindImagePathInDatabase] DB検索エラー '{searchKey}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 出力ファイルパス群を生成
        /// </summary>
        /// <param name="folderPath">出力先フォルダパス</param>
        /// <param name="fileNameWithoutExtension">拡張子なしファイル名</param>
        /// <returns>複合画像・円検出・矩形検出の出力パス</returns>
        private (string Composite, string Circle, string Rectangle) GenerateOutputPaths(string folderPath, string fileNameWithoutExtension)
        {
            var composite = Path.Combine(folderPath, fileNameWithoutExtension + TrainingDataSuffix);
            var circle = Path.Combine(folderPath, fileNameWithoutExtension + CircleDetectionSuffix);
            var rectangle = Path.Combine(folderPath, fileNameWithoutExtension + RectangleDetectionSuffix);
            return (composite, circle, rectangle);
        }

        /// <summary>
        /// ファイル名から検索キーを抽出
        /// </summary>
        /// <param name="fileNameWithoutExtension">拡張子なしファイル名</param>
        /// <returns>検索キー</returns>
        private string? ExtractSearchKey(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension))
                return null;

            // サフィックスを除去して検索キーを抽出
            var suffixes = new[] { TrainingDataSuffix, CircleDetectionSuffix, RectangleDetectionSuffix };
            foreach (var suffix in suffixes)
            {
                var suffixWithoutExt = Path.GetFileNameWithoutExtension(suffix);
                if (fileNameWithoutExtension.EndsWith(suffixWithoutExt, StringComparison.OrdinalIgnoreCase))
                {
                    return fileNameWithoutExtension.Substring(0, fileNameWithoutExtension.Length - suffixWithoutExt.Length);
                }
            }

            // サフィックスがない場合はそのまま返す
            return fileNameWithoutExtension;
        }

        /// <summary>
        /// 検出タスクを実行（円・矩形検出）
        /// </summary>
        private void ExecuteDetectionTasks(
            string sourceImagePath,
            (string Composite, string Circle, string Rectangle) outputPaths)
        {
            try
            {
                // 非同期タスクを並列実行（結果は待機しない）
                // 注: 必要に応じてタスク完了を待機する実装に変更可能
                //_ = Task.WhenAll(
                //DetectAndSaveCompositeImageAsync(sourceImagePath, outputPaths.Composite),
                //DetectAndSaveCircleImageAsync(sourceImagePath, outputPaths.Circle)
                //DetectAndSaveRectangleImageAsync(sourceImagePath, outputPaths.Rectangle)
                //);
                _ = DetectAndSaveCircleImageAsync(sourceImagePath, outputPaths.Circle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExecuteDetectionTasks] タスク実行エラー '{sourceImagePath}': {ex.Message}");
                throw;
            }
        }

        private void AddNetworkShare(string path)
        {
            try
            {
                path = path.Trim();

                if (!path.StartsWith("\\\\", StringComparison.Ordinal) && !Directory.Exists(path))
                {
                    MessageBox.Show($"指定されたパスは存在しません: {path}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 既に同じパスが登録されているか確認
                foreach (TreeNode n in treeBrowser.Nodes)
                {
                    if (string.Equals(n.Tag as string, path, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show($"このフォルダは既に登録されています。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                // フォルダ名のみを表示（フルパスはToolTipで表示）
                var folderName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(folderName))
                {
                    // ルートドライブやUNCパスの場合
                    folderName = path;
                }

                var node = new TreeNode(folderName) { Tag = path };
                // ダミーノードはサブフォルダが存在する場合のみ追加する
                try
                {
                    if (HasAnyChild(path))
                    {
                        node.Nodes.Add(new TreeNode()); // ダミーノードで展開可にする
                    }
                    else
                    {
                        // サブフォルダがない場合は画像ファイル数を表示
                        int count = GetImageFileCount(path);
                        node.Text = $"{folderName} ({count})";
                    }
                }
                catch { }

                // ノードを昇順で挿入
                int insertIndex = treeBrowser.Nodes.Count; // デフォルトは末尾
                if (treeBrowser.Nodes.Count == 0)
                {
                    insertIndex = 0;
                }
                else if (treeSortAscending)
                {
                    for (int i = 0; i < treeBrowser.Nodes.Count; i++)
                    {
                        var existing = treeBrowser.Nodes[i];
                        if (string.Compare(existing.Text, node.Text, StringComparison.OrdinalIgnoreCase) > 0)
                        {
                            insertIndex = i;
                            break;
                        }
                    }
                }
                else
                {
                    // 降順の場合は既存ノードのテキストが小さい位置を探す
                    for (int i = 0; i < treeBrowser.Nodes.Count; i++)
                    {
                        var existing = treeBrowser.Nodes[i];
                        if (string.Compare(existing.Text, node.Text, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            insertIndex = i;
                            break;
                        }
                    }
                }

                treeBrowser.Nodes.Insert(insertIndex, node);

                try
                {
                    if (!topLevelFolders.Contains(path, StringComparer.OrdinalIgnoreCase))
                    {
                        topLevelFolders.Add(path);
                        // リストも昇順に保つ
                        if (treeSortAscending)
                            topLevelFolders.Sort(StringComparer.OrdinalIgnoreCase);
                        else
                            topLevelFolders.Sort((a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase));

                        // 設定が完全に読み込まれていない場合は自動保存しない
                        if (settingsLoaded)
                        {
                            SaveAppSettings(); // 追加後に自動保存
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ネットワーク共有を追加できません: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveTopLevelFolder(string path)
        {
            try
            {
                // ツリーからノードを削除
                TreeNode? nodeToRemove = null;
                foreach (TreeNode n in treeBrowser.Nodes)
                {
                    if (string.Equals(n.Tag as string, path, StringComparison.OrdinalIgnoreCase))
                    {
                        nodeToRemove = n;
                        break;
                    }
                }

                if (nodeToRemove != null)
                {
                    treeBrowser.Nodes.Remove(nodeToRemove);
                }

                // リストから削除
                topLevelFolders.RemoveAll(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));

                // 設定を保存
                SaveAppSettings();

                // 画像表示をクリア
                if (string.Equals(lastSelectedPath, path, StringComparison.OrdinalIgnoreCase) ||
                       lastSelectedPath?.StartsWith(path, StringComparison.OrdinalIgnoreCase) == true)
                {
                    ClearDisplayedImages();
                    imageListView?.Items.Clear();
                    lastSelectedPath = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RemoveTopLevelFolder failed: {ex}");
                MessageBox.Show($"フォルダの削除に失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemovePreviousBrowsedFolder()
        {
            // この機能は削除（明示的な削除ボタンを使用するため）<
        }

        private void TreeBrowser_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            try
            {
                if (node.Nodes.Count == 1 && node.Nodes[0].Tag == null && node.Nodes[0].Text == "")
                {
                    node.Nodes.Clear();
                    var loadingNode = new TreeNode("読み込み中...") { ForeColor = SystemColors.GrayText };
                    node.Nodes.Add(loadingNode);

                    _ = Task.Run(() => ExpandNodeAsync(node));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BeforeExpand handler error: {ex}");
            }
        }

        private void TreeBrowser_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            var path = e.Node.Tag as string;
            if (path == null) return;

            if (Directory.Exists(path))
            {
                // フォルダの場合は展開/折りたたみ
                if (!e.Node.IsExpanded)
                    e.Node.Expand();
                else
                    e.Node.Collapse();
            }
        }

        private void TreeBrowser_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            var path = e.Node.Tag as string;
            if (path == null)
            {
                ClearDisplayedImages();
                imageListView?.Items.Clear();
                return;
            }

            lastSelectedPath = path;

            // フォルダが選択された場合は、左下パネルに画像ファイル一覧を表示
            if (Directory.Exists(path))
            {
                _ = LoadImageListAsync(path);
                ClearDisplayedImages();
            }
            else
            {
                // ファイルの場合（通常は到達しない）
                ClearDisplayedImages();
                imageListView?.Items.Clear();
            }
        }

        private void TreeBrowser_NodeMouseHover(object? sender, TreeNodeMouseHoverEventArgs e)
        {
            var path = e.Node.Tag as string;
            if (!string.IsNullOrEmpty(path))
            {
                // フルパスをToolTipで表示
                folderTooltip?.SetToolTip(treeBrowser, path);
            }
        }

        private async Task LoadImageListAsync(string folderPath)
        {
            try
            {
                if (imageListView == null) return;

                // UI更新 - クリア
                imageListView.BeginUpdate();
                imageListView.Items.Clear();

                // バックグラウンドで画像ファイルを列挙
                var imageFiles = await Task.Run(() =>
                   {
                       var files = new List<(string Path, DateTime Modified)>();
                       try
                       {
                           var dirOptions = new EnumerationOptions { IgnoreInaccessible = true, ReturnSpecialDirectories = false };
                           foreach (var file in Directory.EnumerateFiles(folderPath, "*", dirOptions))
                           {
                               if (IsImageFile(file))
                               {
                                   var fileInfo = new FileInfo(file);
                                   files.Add((file, fileInfo.LastWriteTime));
                               }
                           }
                       }
                       catch (Exception ex)
                       {
                           Debug.WriteLine($"LoadImageListAsync enumeration error: {ex}");
                       }
                       return files;
                   });

                // UI更新 - リスト追加
                foreach (var fileInfo in imageFiles)
                {
                    var item = new ListViewItem(Path.GetFileName(fileInfo.Path));
                    item.SubItems.Add(fileInfo.Modified.ToString("yyyy/MM/dd HH:mm:ss"));
                    item.Tag = fileInfo.Path;
                    imageListView.Items.Add(item);
                }

                imageListView.EndUpdate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadImageListAsync error: {ex}");
            }
        }

        private void ImageListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (imageListView?.SelectedItems.Count > 0)
            {
                var item = imageListView.SelectedItems[0];
                var path = item.Tag as string;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    _ = LoadSelectedImageAsync(path);
                }
            }
        }

        private void ImageListView_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (imageListView?.SelectedItems.Count > 0)
            {
                var item = imageListView.SelectedItems[0];
                var path = item.Tag as string;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"ファイルを開けません: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private async Task ExpandNodeAsync(TreeNode node)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            string path = node.Tag as string ?? node.Text;

            try
            {
                Debug.WriteLine($"[ExpandNode] 開始: {path}");

                // 直下のフォルダのみを列挙（再帰なし）
                var dirOptions = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false,
                    RecurseSubdirectories = false, // 明示的にサブフォルダを除外
                    MatchCasing = MatchCasing.CaseInsensitive
                };

                var dirNames = new List<string>();

                try
                {
                    var enumerateStartTime = stopwatch.Elapsed;

                    // タイムアウト付きでフォルダを列挙（直下のみ）- 10秒
                    using var cancellationTokenSource = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));

                    await Task.Run(() =>
                    {
                        try
                        {
                            // Directory.EnumerateDirectories は既定で直下のみを列挙
                            foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
                            {
                                // キャンセル要求を定期的にチェック
                                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                                dirNames.Add(d);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine($"ExpandNodeAsync: キャンセルされました '{path}'");
                            throw; // 外側のcatchで処理
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            Debug.WriteLine($"ExpandNodeAsync: アクセス拒否 '{path}': {ex.Message}");
                            throw; // 外側のcatchで処理
                        }
                    }, cancellationTokenSource.Token);

                    var enumerateTime = stopwatch.Elapsed - enumerateStartTime;
                    Debug.WriteLine($"[ExpandNode] 列挙完了: {path} - {dirNames.Count}個のフォルダ - {enumerateTime.TotalMilliseconds:F2}ms");
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine($"[ExpandNode] タイムアウト: {path} - {stopwatch.ElapsedMilliseconds}ms");
                    this.Invoke(() =>
                    {
                        node.Nodes.Clear();
                        var timeoutNode = new TreeNode("(タイムアウト - 再試行してください)") { ForeColor = System.Drawing.Color.Red };
                        node.Nodes.Add(timeoutNode);
                    });
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine($"[ExpandNode] アクセス拒否: {path} - {stopwatch.ElapsedMilliseconds}ms");
                    this.Invoke(() =>
                    {
                        node.Nodes.Clear();
                        var inac = new TreeNode("(アクセス拒否)") { ForeColor = System.Drawing.Color.Orange };
                        node.Nodes.Add(inac);
                    });
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ExpandNode] エラー: {path} - {stopwatch.ElapsedMilliseconds}ms - {ex.Message}");
                    this.Invoke(() =>
                    {
                        node.Nodes.Clear();
                        var inac = new TreeNode("(到達不可)") { ForeColor = SystemColors.GrayText };
                        node.Nodes.Add(inac);
                    });
                    return;
                }

                var nodeCreationStartTime = stopwatch.Elapsed;

                // ノード作成をバックグラウンドで実行
                var nodesToAdd = await Task.Run(() =>
                 {
                     var nodes = new List<TreeNode>(dirNames.Count);
                     var subfolderCount = dirNames.Count;

                     // サブフォルダ数が閾値以上の場合は全てにダミーノードを追加
                     // それ以外の場合はサブフォルダの有無をチェック
                     bool alwaysAddDummy = subfolderCount >= treeDummyNodeThreshold;

                     // 設定に従って表示順をソート
                     if (treeSortAscending)
                     {
                         dirNames.Sort(StringComparer.OrdinalIgnoreCase);
                     }
                     else
                     {
                         dirNames.Sort((a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase));
                     }

                     foreach (var d in dirNames)
                     {
                         var folderName = Path.GetFileName(d);
                         if (string.IsNullOrEmpty(folderName)) folderName = d;
                         var tn = new TreeNode(folderName) { Tag = d };

                         if (alwaysAddDummy)
                         {
                             // 閾値以上の場合は常にダミーノードを追加
                             tn.Nodes.Add(new TreeNode());
                         }
                         else
                         {
                             // 閾値未満の場合はサブフォルダがあるかチェック
                             if (HasAnyChild(d))
                             {
                                 tn.Nodes.Add(new TreeNode());
                             }
                             else
                             {
                                 // サブフォルダが無ければ画像ファイル数を表示
                                 int cnt = GetImageFileCount(d);
                                 tn.Text = $"{folderName} ({cnt})";
                             }
                         }

                         nodes.Add(tn);
                     }
                     return nodes;
                 });

                var nodeCreationTime = stopwatch.Elapsed - nodeCreationStartTime;
                Debug.WriteLine($"[ExpandNode] ノード作成完了: {path} - {nodesToAdd.Count}個作成 - {nodeCreationTime.TotalMilliseconds:F2}ms");

                var uiUpdateStartTime = stopwatch.Elapsed;

                // UI更新のみをメインスレッドで実行
                this.Invoke(() =>
          {
              try
              {
                  treeBrowser.BeginUpdate();
                  node.Nodes.Clear();

                  if (nodesToAdd.Count > 0)
                  {
                      node.Nodes.AddRange(nodesToAdd.ToArray());
                  }
                  else
                  {
                      node.Nodes.Add(new TreeNode("(空)") { ForeColor = SystemColors.GrayText });
                  }

                  treeBrowser.EndUpdate();

                  var uiUpdateTime = stopwatch.Elapsed - uiUpdateStartTime;
                  Debug.WriteLine($"[ExpandNode] UI更新完了: {path} - {nodesToAdd.Count}個追加 - {uiUpdateTime.TotalMilliseconds:F2}ms");
              }
              catch (Exception ex)
              {
                  treeBrowser.EndUpdate();
                  Debug.WriteLine($"UI update failed: {ex}");
              }
          });

                stopwatch.Stop();
                Debug.WriteLine($"[ExpandNode] 完了: {path} - 合計 {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Debug.WriteLine($"[ExpandNode] 予期しないエラー: {path} - {stopwatch.ElapsedMilliseconds}ms - {ex}");
                try
                {
                    this.Invoke(() =>
                                    {
                                        node.Nodes.Clear();
                                        node.Nodes.Add(new TreeNode("(エラー)") { ForeColor = SystemColors.GrayText });
                                    });
                }
                catch { }
            }
        }

        private bool HasAnyChild(string path)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // 直下にサブフォルダがあるかのみをチェック（再帰なし）
                // 1件でも見つかればtrueを返す（高速化）
                using var e = Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly).GetEnumerator();
                var result = e.MoveNext();
                sw.Stop();
                if (sw.ElapsedMilliseconds > 100) // 100ms以上かかった場合のみログ出力
                {
                    Debug.WriteLine($"[HasAnyChild] 遅延検出: {path} - {sw.ElapsedMilliseconds}ms");
                }
                return result;
            }
            catch { sw.Stop(); return false; }
        }

        private int GetImageFileCount(string path)
        {
            try
            {
                int count = 0;
                var dirOptions = new EnumerationOptions { IgnoreInaccessible = true, ReturnSpecialDirectories = false };
                foreach (var f in Directory.EnumerateFiles(path, "*", dirOptions))
                {
                    if (IsImageFile(f)) count++;
                }
                return count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetImageFileCount error: {ex}");
                return 0;
            }
        }

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
      {
      ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".heic"
        };

        private static bool IsImageFile(string path)
        {
            try
            {
                var ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext)) return false;
                return ImageExtensions.Contains(ext);
            }
            catch
            {
                return false;
            }
        }
    }
}
