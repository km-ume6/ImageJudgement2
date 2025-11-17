using System.Diagnostics;

namespace ImageJudgement2
{
    /// <summary>
    /// 自動連続判定処理
    /// </summary>
    public partial class Form1
    {
        private Button? btnAutoBatch;
        private Button? btnAutoBatchCompare;

        /// <summary>
        /// 自動連続判定を開始
        /// </summary>
        private async Task StartAutoBatchProcessAsync()
        {
            // 選択されているフォルダパスを取得
            string? selectedFolderPath = GetSelectedFolderPath();

            if (string.IsNullOrEmpty(selectedFolderPath))
            {
                MessageBox.Show(
                    "フォルダを選択してください。",
                    "情報",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!Directory.Exists(selectedFolderPath))
            {
                MessageBox.Show(
                    "選択されたフォルダが見つかりません。",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // 画像ファイルを取得
            var imageFiles = GetImageFilesInDirectory(selectedFolderPath);

            if (imageFiles.Count == 0)
            {
                MessageBox.Show(
                    "選択されたフォルダに画像ファイルがありません。",
                    "情報",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // 確認ダイアログ
            var confirmResult = MessageBox.Show(
                $"選択されたフォルダ内の {imageFiles.Count} 個の画像ファイルに対して自動判定を実行します。\n\nよろしいですか？",
                "確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }

            // 待ち時間を設定（デフォルト1000ms = 1秒）
            int delayMilliseconds = 1000;
            using var delayDialog = new Form
            {
                Text = "待ち時間設定",
                Size = new System.Drawing.Size(350, 160),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lblDelay = new Label
            {
                Text = "各判定後の待ち時間 (ミリ秒):",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(300, 20)
            };

            var nudDelay = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 60000,
                Value = delayMilliseconds,
                Increment = 100,
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(300, 25)
            };

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(130, 75),
                Size = new System.Drawing.Size(80, 30)
            };

            var btnCancel = new Button
            {
                Text = "キャンセル",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(220, 75),
                Size = new System.Drawing.Size(100, 30)
            };

            delayDialog.Controls.Add(lblDelay);
            delayDialog.Controls.Add(nudDelay);
            delayDialog.Controls.Add(btnOk);
            delayDialog.Controls.Add(btnCancel);
            delayDialog.AcceptButton = btnOk;
            delayDialog.CancelButton = btnCancel;

            if (delayDialog.ShowDialog(this) == DialogResult.OK)
            {
                delayMilliseconds = (int)nudDelay.Value;
            }
            else
            {
                return;
            }

            // 進捗ダイアログを表示して処理を開始
            await ExecuteAutoBatchProcessAsync(imageFiles, delayMilliseconds);
        }

        /// <summary>
        /// 自動連続判定を実行
        /// </summary>
        private async Task ExecuteAutoBatchProcessAsync(List<string> imageFiles, int delayMilliseconds)
        {
            var progressDialog = new AutoBatchProcessDialog();

            // バックグラウンド処理の Task を保持
            Task? processingTask = null;

            // UI スレッドで安全に処理を行うヘルパー（同期的に実行）
            void SafeInvoke(Action action)
            {
                try
                {
                    if (!progressDialog.IsDisposed)
                    {
                        progressDialog.Invoke(new Action(() =>
                        {
                            if (!progressDialog.IsDisposed)
                            {
                                try { action(); } catch (Exception ex) 
                                { 
                                    Debug.WriteLine($"[SafeInvoke] Action内例外: {ex.Message}");
                                }
                            }
                        }));
                    }
                    else
                    {
                        Debug.WriteLine("[SafeInvoke] progressDialog is Disposed");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SafeInvoke] Invoke例外: {ex.Message}");
                }
            }

            // Show 時に処理を開始
            void StartProcessing(object? sender, EventArgs e)
            {
                progressDialog.Shown -= StartProcessing;

                processingTask = Task.Run(async () =>
                {
                    var totalStopwatch = Stopwatch.StartNew();
                    int totalCount = imageFiles.Count;
                    int successCount = 0;
                    int failureCount = 0;
                    
                    // 正解率計算用の変数を追加
                    var statistics = JudgementEvaluator.CreateStatistics();

                    // 処理開始ログ
                    SafeInvoke(() =>
                    {
                        progressDialog.AddLog("=".PadRight(60, '='));
                        progressDialog.AddLog($"自動連続判定を開始します");
                        progressDialog.AddLog($"対象画像数: {totalCount} 件");
                        progressDialog.AddLog($"待ち時間: {delayMilliseconds} ms");
                        
                        // 判定モードを取得
                        string modeText = GetJudgeModeDisplayName(detectionMode);
                        progressDialog.AddLog($"判定モード: {modeText}");
                        progressDialog.AddLog("=".PadRight(60, '='));
                        progressDialog.AddLog("");
                        
                        // 初期正解率を表示
                        progressDialog.UpdateAccuracy(0, 0);
                    });

                    for (int i = 0; i < totalCount; i++)
                    {
                        // 停止チェック（UI スレッド経由）
                        bool stopped = false;
                        SafeInvoke(() => stopped = progressDialog.IsStopped);
                        if (stopped)
                        {
                            SafeInvoke(() => progressDialog.AddLog("\n処理を中断しました"));
                            break;
                        }

                        // 一時停止ループ（UI スレッド経由でフラグを確認）
                        while (true)
                        {
                            bool isPaused = false;
                            bool isStoppedNow = false;
                            
                            try
                            {
                                SafeInvoke(() =>
                                {
                                    isPaused = progressDialog.IsPaused;
                                    isStoppedNow = progressDialog.IsStopped;
                                });
                                
                                Debug.WriteLine($"[一時停止ループ] isPaused={isPaused}, isStoppedNow={isStoppedNow} (画像{i + 1}/{totalCount})");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[一時停止ループ] SafeInvoke例外: {ex.Message}");
                                // Invoke失敗時は停止とみなす
                                isStoppedNow = true;
                            }

                            if (isStoppedNow)
                            {
                                Debug.WriteLine($"[一時停止ループ] 停止フラグを検知しました (画像{i + 1}/{totalCount})");
                                try
                                {
                                    SafeInvoke(() => progressDialog.AddLog("\n処理を中断しました"));
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[一時停止ループ] AddLog例外: {ex.Message}");
                                }
                                break;
                            }

                            if (!isPaused)
                            {
                                Debug.WriteLine($"[一時停止ループ] 一時停止が解除されました (画像{i + 1}/{totalCount})");
                                break;
                            }

                            Debug.WriteLine($"[一時停止ループ] 一時停止中... (画像{i + 1}/{totalCount})");
                            await Task.Delay(100).ConfigureAwait(false);
                        }

                        // 再度停止チェック
                        SafeInvoke(() => stopped = progressDialog.IsStopped);
                        if (stopped)
                        {
                            Debug.WriteLine($"[停止チェック2] 停止フラグを検知しました (画像{i + 1}/{totalCount})");
                            SafeInvoke(() => progressDialog.AddLog("\n処理を中断しました"));
                            break;
                        }

                        string imageFile = imageFiles[i];
                        string fileName = Path.GetFileName(imageFile);
                        var imageStopwatch = Stopwatch.StartNew();

                        // ステータス更新（UI スレッド）
                        SafeInvoke(() =>
                        {
                            progressDialog.UpdateStatus($"処理中: {fileName}");
                            progressDialog.UpdateProgress(i + 1, totalCount);
                            progressDialog.AddLog($"[{i + 1}/{totalCount}] {fileName}");
                        });

                        try
                        {
                            // 単一画像処理（内部で UI 操作がある場合は元の実装に任せる）
                            var result = await ProcessSingleImageWithDetailsAsync(imageFile, progressDialog).ConfigureAwait(false);
                            
                            imageStopwatch.Stop();

                            if (result.Success)
                            {
                                successCount++;
                                
                                // JudgementEvaluatorを使用して正解ラベルを抽出
                                string? groundTruth = JudgementEvaluator.ExtractGroundTruthFromPath(imageFile);
                                
                                // 統計情報に結果を追加
                                statistics.AddResult(result.JudgeResult, groundTruth, false);
                        
                                string accuracyInfo = JudgementEvaluator.GetComparisonText(result.JudgeResult, groundTruth);
                                
                                SafeInvoke(() =>
                                {
                                    progressDialog.AddLog($"  ✓ 判定成功{(string.IsNullOrEmpty(accuracyInfo) ? "" : $" {accuracyInfo}")}");
                                    progressDialog.AddLog($"  判定結果: {result.JudgeResult}");
                                    if (!string.IsNullOrEmpty(result.CategoryName))
                                    {
                                        progressDialog.AddLog($"  カテゴリ: {result.CategoryName} (スコア: {result.Score:F4})");
                                    }
                                    progressDialog.AddLog($"  処理時間: {imageStopwatch.ElapsedMilliseconds} ms");
                                    
                                    // デバッグ：統計情報を出力
                                    Debug.WriteLine($"[正解率更新] CorrectCount={statistics.CorrectCount}, LabeledCount={statistics.LabeledCount}, GroundTruth={groundTruth}");
                                    
                                    // リアルタイムで正解率を更新
                                    progressDialog.UpdateAccuracy(statistics.CorrectCount, statistics.LabeledCount);
                                });
                            }
                            else
                            {
                                failureCount++;
                                
                                // エラーの場合も統計情報に追加
                                string? groundTruth = JudgementEvaluator.ExtractGroundTruthFromPath(imageFile);
                                statistics.AddResult(null, groundTruth, true);
                                
                                SafeInvoke(() =>
                                {
                                    progressDialog.AddLog($"  ✗ 判定失敗");
                                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                                    {
                                        progressDialog.AddLog($"  エラー: {result.ErrorMessage}");
                                    }
                                    progressDialog.AddLog($"  処理時間: {imageStopwatch.ElapsedMilliseconds} ms");
                                    
                                    // リアルタイムで正解率を更新
                                    progressDialog.UpdateAccuracy(statistics.CorrectCount, statistics.LabeledCount);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            imageStopwatch.Stop();
                            failureCount++;
                            
                            // 例外の場合も統計情報に追加
                            string? groundTruth = JudgementEvaluator.ExtractGroundTruthFromPath(imageFile);
                            statistics.AddResult(null, groundTruth, true);
                            
                            SafeInvoke(() =>
                            {
                                progressDialog.AddLog($"  ✗ 例外エラー");
                                progressDialog.AddLog($"  詳細: {ex.Message}");
                                progressDialog.AddLog($"  処理時間: {imageStopwatch.ElapsedMilliseconds} ms");
                                
                                // リアルタイムで正解率を更新
                                progressDialog.UpdateAccuracy(statistics.CorrectCount, statistics.LabeledCount);
                            });
                            Debug.WriteLine($"ProcessSingleImageAsync error: {ex}");
                        }

                        SafeInvoke(() => progressDialog.AddLog("")); // 空行

                        // 待ち時間
                        if (delayMilliseconds > 0 && i < totalCount - 1)
                        {
                            await Task.Delay(delayMilliseconds).ConfigureAwait(false);
                        }
                    }

                    totalStopwatch.Stop();

                    // 完了／中断の最終処理（UI スレッドで実行）
                    bool finalStopped = false;
                    SafeInvoke(() => finalStopped = progressDialog.IsStopped);
                    
                    Debug.WriteLine($"[最終処理] finalStopped={finalStopped}");

                    SafeInvoke(() =>
                    {
                        progressDialog.AddLog("=".PadRight(60, '='));
                        
                        if (finalStopped)
                        {
                            Debug.WriteLine("[最終処理] SetStopped()を呼び出します");
                            progressDialog.SetStopped();
                            progressDialog.AddLog("処理を中断しました");
                        }
                        else
                        {
                            Debug.WriteLine("[最終処理] SetCompleted()を呼び出します");
                            progressDialog.SetCompleted();
                            progressDialog.AddLog("すべての処理が完了しました");
                        }

                        progressDialog.AddLog("");
                        progressDialog.AddLog($"処理結果:");
                        progressDialog.AddLog($"  成功: {successCount} 件");
                        progressDialog.AddLog($"  失敗: {failureCount} 件");
                        progressDialog.AddLog($"  合計: {successCount + failureCount} 件");
                        
                        // 正解率情報を追加
                        if (statistics.LabeledCount > 0)
                        {
                            progressDialog.AddLog("");
                            progressDialog.AddLog($"正解率情報:");
                            progressDialog.AddLog($"  正解ラベル付き: {statistics.LabeledCount} 件");
                            progressDialog.AddLog($"  正解: {statistics.CorrectCount} 件");
                            progressDialog.AddLog($"  不正解: {statistics.IncorrectCount} 件");
                            progressDialog.AddLog($"  精度: {JudgementEvaluator.FormatAccuracy(statistics.CorrectCount, statistics.LabeledCount)}");
                        }
                        
                        double avgTimePerImage = totalCount > 0 ? totalStopwatch.ElapsedMilliseconds / (double)totalCount : 0;
                        progressDialog.AddLog($"  総処理時間: {totalStopwatch.ElapsedMilliseconds} ms ({totalStopwatch.Elapsed:mm\\:ss\\.fff})");
                        progressDialog.AddLog($"  平均処理時間: {avgTimePerImage:F2} ms/件");
                        progressDialog.AddLog("=".PadRight(60, '='));
                    });
                    
                    Debug.WriteLine("[最終処理] 完了");

                });
            }

            progressDialog.Shown += StartProcessing;

            // モーダル表示して終了を待つ
            try
            {
                progressDialog.ShowDialog(this);
            }
            finally
            {
                try
                {
                    if (processingTask != null) await processingTask.ConfigureAwait(false);
                }
                catch { }

                progressDialog.Dispose();
            }
        }

        /// <summary>
        /// 単一画像を処理（詳細情報付き）
        /// </summary>
        private async Task<ImageProcessResult> ProcessSingleImageWithDetailsAsync(string imageFilePath, AutoBatchProcessDialog progressDialog)
        {
            var result = new ImageProcessResult();

            // 停止フラグをチェックするヘルパーメソッド
            bool CheckStopped()
            {
                bool stopped = false;
                try
                {
                    progressDialog.Invoke(new Action(() =>
                    {
                        stopped = progressDialog.IsStopped;
                    }));
                }
                catch
                {
                    // Invoke失敗時は停止とみなす
                    stopped = true;
                }
                return stopped;
            }

            try
            {
                // 停止フラグをチェック
                if (CheckStopped())
                {
                    result.ErrorMessage = "処理が中断されました";
                    return result;
                }

                progressDialog.AddLog($"  画像読込中...");

                // UIスレッドでTreeViewの選択を変更
                bool selectionChanged = false;
                this.Invoke(() =>
                {
                    // ImageListViewで該当する画像を探して選択
                    if (imageListView != null)
                    {
                        foreach (ListViewItem item in imageListView.Items)
                        {
                            string? itemPath = item.Tag as string;
                            if (itemPath == imageFilePath)
                            {
                                // 既存の選択をクリア
                                imageListView.SelectedItems.Clear();

                                // 該当する項目を選択
                                item.Selected = true;
                                item.EnsureVisible();

                                selectionChanged = true;
                                break;
                            }
                        }
                    }
                });

                if (!selectionChanged)
                {
                    result.ErrorMessage = "リストビューで画像が見つかりませんでした";
                    return result;
                }

                // 停止フラグをチェック
                if (CheckStopped())
                {
                    result.ErrorMessage = "処理が中断されました";
                    return result;
                }

                progressDialog.AddLog($"  円検出中...");

                // 画像の読み込みとAI判定が完了するまで待機
                // AutoDetectCircleAndAiAsync は LoadSelectedImageAsync 内で自動的に呼ばれる
                await Task.Delay(500); // UI更新の時間を確保

                // 停止フラグをチェック
                if (CheckStopped())
                {
                    result.ErrorMessage = "処理が中断されました";
                    return result;
                }

                // 自動検出が有効かチェック
                bool autoDetectEnabled = false;
                this.Invoke(() =>
                {
                    autoDetectEnabled = chkAutoDetect?.Checked ?? false;
                });

                if (!autoDetectEnabled)
                {
                    progressDialog.AddLog($"  AI判定実行中...");
                    // 自動検出が無効の場合は手動でAI判定を実行
                    await AutoDetectCircleAndAiAsync();
                }
                else
                {
                    progressDialog.AddLog($"  AI判定実行中（自動検出モード）...");
                }

                // AI判定の完了を待つ（最大30秒）
                int waitCount = 0;
                while (waitCount < 300) // 30秒 (100ms * 300)
                {
                    // 停止フラグをチェック
                    if (CheckStopped())
                    {
                        result.ErrorMessage = "処理が中断されました";
                        return result;
                    }

                    bool isProcessing = false;
                    this.Invoke(() =>
                    {
                        isProcessing = currentAiResult?.IsProcessing ?? false;
                    });

                    if (!isProcessing)
                    {
                        // 処理完了
                        break;
                    }

                    await Task.Delay(100);
                    waitCount++;
                }

                if (waitCount >= 300)
                {
                    result.ErrorMessage = "AI判定がタイムアウトしました (30秒)";
                    return result;
                }

                // 停止フラグをチェック
                if (CheckStopped())
                {
                    result.ErrorMessage = "処理が中断されました";
                    return result;
                }

                // 結果を取得
                this.Invoke(() =>
                {
                    if (currentAiResult != null && cachedJudgeResult != null)
                    {
                        result.Success = true;
                        result.JudgeResult = cachedJudgeResult;

                        if (currentAiResult.TopClassResult != null)
                        {
                            result.CategoryName = currentAiResult.TopClassResult.CategoryName;
                            result.Score = currentAiResult.TopClassResult.Score;
                        }
                    }
                    else
                    {
                        result.ErrorMessage = "AI判定結果を取得できませんでした";
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"処理中に例外が発生: {ex.Message}";
                Debug.WriteLine($"ProcessSingleImageWithDetailsAsync error: {ex}");
                return result;
            }
        }

        /// <summary>
        /// 選択されているフォルダパスを取得
        /// </summary>
        private string? GetSelectedFolderPath()
        {
            if (treeBrowser?.SelectedNode == null)
            {
                return null;
            }

            var selectedNode = treeBrowser.SelectedNode;
            return selectedNode.Tag as string;
        }

        /// <summary>
        /// 指定ディレクトリ内の画像ファイルを取得
        /// </summary>
        private List<string> GetImageFilesInDirectory(string directoryPath)
        {
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };
            var imageFiles = new List<string>();

            try
            {
                var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(f => f)
                    .ToList();

                imageFiles.AddRange(files);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetImageFilesInDirectory error: {ex}");
            }

            return imageFiles;
        }

        /// <summary>
        /// 判定モード比較を開始
        /// </summary>
        private async Task StartAutoBatchComparisonAsync()
        {
            // 選択されているフォルダパスを取得
            string? selectedFolderPath = GetSelectedFolderPath();

            if (string.IsNullOrEmpty(selectedFolderPath))
            {
                MessageBox.Show(
                    "フォルダを選択してください。",
                    "情報",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!Directory.Exists(selectedFolderPath))
            {
                MessageBox.Show(
                    "選択されたフォルダが見つかりません。",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // 画像ファイルを取得
            var imageFiles = GetImageFilesInDirectory(selectedFolderPath);

            if (imageFiles.Count == 0)
            {
                MessageBox.Show(
                    "選択されたフォルダに画像ファイルがありません。",
                    "情報",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // 確認ダイアログ
            var confirmResult = MessageBox.Show(
                $"選択されたフォルダ内の {imageFiles.Count} 個の画像ファイルに対して\n全判定モード（3種類）で判定を実行し、精度を比較します。\n\nよろしいですか？",
                "確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }

            // 待ち時間を設定（デフォルト500ms）
            int delayMilliseconds = 500;
            using var delayDialog = new Form
            {
                Text = "待ち時間設定",
                Size = new System.Drawing.Size(350, 160),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lblDelay = new Label
            {
                Text = "各判定後の待ち時間 (ミリ秒):",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(300, 20)
            };

            var nudDelay = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 60000,
                Value = delayMilliseconds,
                Increment = 100,
                Location = new System.Drawing.Point(20, 45),
                Size = new System.Drawing.Size(300, 25)
            };

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(130, 75),
                Size = new System.Drawing.Size(80, 30)
            };

            var btnCancel = new Button
            {
                Text = "キャンセル",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(220, 75),
                Size = new System.Drawing.Size(100, 30)
            };

            delayDialog.Controls.Add(lblDelay);
            delayDialog.Controls.Add(nudDelay);
            delayDialog.Controls.Add(btnOk);
            delayDialog.Controls.Add(btnCancel);
            delayDialog.AcceptButton = btnOk;
            delayDialog.CancelButton = btnCancel;

            if (delayDialog.ShowDialog(this) == DialogResult.OK)
            {
                delayMilliseconds = (int)nudDelay.Value;
            }
            else
            {
                return;
            }

            // 進捗ダイアログを表示して処理を開始
            await ExecuteAutoBatchComparisonAsync(imageFiles, delayMilliseconds);
        }

        /// <summary>
        /// 判定モード比較を実行
        /// </summary>
        private async Task ExecuteAutoBatchComparisonAsync(List<string> imageFiles, int delayMilliseconds)
        {
            var progressDialog = new AutoBatchProcessDialog();
            Task? processingTask = null;

            void SafeInvoke(Action action)
            {
                try
                {
                    if (!progressDialog.IsDisposed)
                    {
                        progressDialog.Invoke(new Action(() =>
                        {
                            if (!progressDialog.IsDisposed)
                            {
                                try { action(); } catch { }
                            }
                        }));
                    }
                }
                catch { }
            }

            void StartProcessing(object? sender, EventArgs e)
            {
                progressDialog.Shown -= StartProcessing;

                processingTask = Task.Run(async () =>
                {
                    var totalStopwatch = Stopwatch.StartNew();

                    // 3つの判定モードで実行
                    var modes = new[] { JudgeMode.TopClass, JudgeMode.ScoreAverage, JudgeMode.ScoreRanking };
                    var modeResults = new Dictionary<JudgeMode, JudgementEvaluator.JudgementStatistics>();

                    SafeInvoke(() =>
                    {
                        progressDialog.AddLog("=".PadRight(80, '='));
                        progressDialog.AddLog("判定モード比較を開始します");
                        progressDialog.AddLog($"対象画像数: {imageFiles.Count} 件");
                        progressDialog.AddLog($"判定モード: {modes.Length} 種類");
                        progressDialog.AddLog($"待ち時間: {delayMilliseconds} ms");
                        progressDialog.AddLog("=".PadRight(80, '='));
                        progressDialog.AddLog("");
                    });

                    // 元の判定モードを保存
                    var originalMode = detectionMode;

                    foreach (var mode in modes)
                    {
                        // 停止チェック
                        bool stopped = false;
                        SafeInvoke(() => stopped = progressDialog.IsStopped);
                        if (stopped) break;

                        // 判定モードを切り替え
                        detectionMode = mode;
                        string modeText = GetJudgeModeDisplayName(mode);

                        var statistics = JudgementEvaluator.CreateStatistics();
                        modeResults[mode] = statistics;

                        SafeInvoke(() =>
                        {
                            progressDialog.AddLog($"--- {modeText} で判定開始 ---");
                            progressDialog.AddLog("");
                            // 初期正解率を表示
                            progressDialog.UpdateAccuracy(0, 0);
                        });

                        for (int i = 0; i < imageFiles.Count; i++)
                        {
                            // 停止・一時停止チェック
                            SafeInvoke(() => stopped = progressDialog.IsStopped);
                            if (stopped) break;

                            while (true)
                            {
                                bool isPaused = false;
                                SafeInvoke(() => isPaused = progressDialog.IsPaused);
                                if (!isPaused) break;
                                await Task.Delay(100).ConfigureAwait(false);
                            }

                            string imageFile = imageFiles[i];
                            string fileName = Path.GetFileName(imageFile);

                            SafeInvoke(() =>
                            {
                                progressDialog.UpdateStatus($"[{modeText}] 処理中: {fileName}");
                                progressDialog.UpdateProgress(i + 1 + (Array.IndexOf(modes, mode) * imageFiles.Count),
                                                             imageFiles.Count * modes.Length);
                            });

                            try
                            {
                                // JudgementEvaluatorを使用して正解ラベルを抽出
                                string? groundTruth = JudgementEvaluator.ExtractGroundTruthFromPath(imageFile);

                                var result = await ProcessSingleImageWithDetailsAsync(imageFile, progressDialog).ConfigureAwait(false);

                                if (result.Success && !string.IsNullOrEmpty(result.JudgeResult))
                                {
                                    // 統計情報に結果を追加
                                    statistics.AddResult(result.JudgeResult, groundTruth, false);

                                    // JudgementEvaluatorを使用してログメッセージを生成
                                    string logMessage = JudgementEvaluator.FormatJudgementLog(
                                        fileName,
                                        result.JudgeResult,
                                        groundTruth,
                                        result.CategoryName,
                                        result.Score);

                                    SafeInvoke(() =>
                                    {
                                        progressDialog.AddLog($"  [{i + 1}/{imageFiles.Count}] {logMessage}");
                                        // リアルタイムで正解率を更新
                                        progressDialog.UpdateAccuracy(statistics.CorrectCount, statistics.LabeledCount);
                                    });
                                }
                                else
                                {
                                    statistics.AddResult(null, groundTruth, true);
                                    SafeInvoke(() =>
                                    {
                                        progressDialog.AddLog($"  [{i + 1}/{imageFiles.Count}] {fileName}: エラー - {result.ErrorMessage}");
                                        // リアルタイムで正解率を更新
                                        progressDialog.UpdateAccuracy(statistics.CorrectCount, statistics.LabeledCount);
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                statistics.ErrorCount++;
                                SafeInvoke(() =>
                                {
                                    progressDialog.AddLog($"  [{i + 1}/{imageFiles.Count}] {fileName}: 例外 - {ex.Message}");
                                    // エラー時も正解率を更新
                                    progressDialog.UpdateAccuracy(statistics.CorrectCount, statistics.LabeledCount);
                                });
                            }

                            if (delayMilliseconds > 0 && i < imageFiles.Count - 1)
                            {
                                await Task.Delay(delayMilliseconds).ConfigureAwait(false);
                            }
                        }

                        SafeInvoke(() =>
                        {
                            progressDialog.AddLog("");
                            progressDialog.AddLog($"--- {modeText} 完了 ---");
                            progressDialog.AddLog($"  処理件数: {statistics.TotalCount} 件");
                            if (statistics.LabeledCount > 0)
                            {
                                progressDialog.AddLog($"  正解ラベル付き: {statistics.LabeledCount} 件");
                                progressDialog.AddLog($"  正解: {statistics.CorrectCount} 件");
                                progressDialog.AddLog($"  不正解: {statistics.IncorrectCount} 件");
                                progressDialog.AddLog($"  精度: {JudgementEvaluator.FormatAccuracy(statistics.CorrectCount, statistics.LabeledCount)}");
                            }
                            progressDialog.AddLog($"  エラー: {statistics.ErrorCount} 件");
                            progressDialog.AddLog("");
                        });

                    }

                    // 元の判定モードに戻す
                    detectionMode = originalMode;

                    totalStopwatch.Stop();

                    // 完了／中断の最終処理（UI スレッドで実行）
                    bool finalStopped = false;
                    SafeInvoke(() => finalStopped = progressDialog.IsStopped);

                    // 最終レポート
                    SafeInvoke(() =>
                    {
                        progressDialog.AddLog("=".PadRight(80, '='));
                        progressDialog.AddLog("判定モード比較レポート");
                        progressDialog.AddLog("=".PadRight(80, '='));
                        progressDialog.AddLog("");

                        // 精度でソート（降順）
                        var sortedResults = modeResults
                            .Where(kv => kv.Value.LabeledCount > 0)
                            .OrderByDescending(kv => kv.Value.Accuracy)
                            .ToList();

                        if (sortedResults.Count > 0)
                        {
                            progressDialog.AddLog("【精度ランキング】");
                            int rank = 1;
                            foreach (var (mode, stats) in sortedResults)
                            {
                                string modeText = GetJudgeModeDisplayName(mode);
                                string rankText = rank == 1 ? "[1位]" : rank == 2 ? "[2位]" : "[3位]";
                                progressDialog.AddLog($"{rankText} {modeText}");
                                progressDialog.AddLog($"      精度: {JudgementEvaluator.FormatAccuracy(stats.CorrectCount, stats.LabeledCount)}");
                                progressDialog.AddLog($"      正解: {stats.CorrectCount} 件, 不正解: {stats.IncorrectCount} 件");
                                progressDialog.AddLog("");
                                rank++;
                            }
                        }
                        else
                        {
                            progressDialog.AddLog("※ 正解ラベル付きの画像がないため、精度を計算できませんでした。");
                            progressDialog.AddLog("  フルパスに「合格」または「不合格」を含めてください。");
                            progressDialog.AddLog("");
                        }

                        progressDialog.AddLog("【詳細統計】");
                        foreach (var mode in modes)
                        {
                            if (modeResults.ContainsKey(mode))
                            {
                                string modeText = GetJudgeModeDisplayName(mode);
                                var stats = modeResults[mode];
                                progressDialog.AddLog($"■ {modeText}");
                                progressDialog.AddLog($"  総処理件数: {stats.TotalCount}");
                                progressDialog.AddLog($"  正解ラベル付き: {stats.LabeledCount}");
                                if (stats.LabeledCount > 0)
                                {
                                    progressDialog.AddLog($"  正解数: {stats.CorrectCount}");
                                    progressDialog.AddLog($"  不正解数: {stats.IncorrectCount}");
                                    progressDialog.AddLog($"  精度: {JudgementEvaluator.FormatAccuracy(stats.CorrectCount, stats.LabeledCount)}");
                                }
                                progressDialog.AddLog($"  エラー数: {stats.ErrorCount}");
                                progressDialog.AddLog("");
                            }
                        }

                        progressDialog.AddLog($"総処理時間: {totalStopwatch.Elapsed:mm\\:ss\\.fff}");
                        progressDialog.AddLog($"処理画像数: {imageFiles.Count} 件 × {modes.Length} モード");
                        progressDialog.AddLog("=".PadRight(80, '='));

                        // 完了または停止状態に設定
                        if (finalStopped)
                        {
                            progressDialog.SetStopped();
                        }
                        else
                        {
                            progressDialog.SetCompleted();
                        }
                    });

                });
            }

            progressDialog.Shown += StartProcessing;

            try
            {
                progressDialog.ShowDialog(this);
            }
            finally
            {
                try
                {
                    if (processingTask != null) await processingTask.ConfigureAwait(false);
                }
                catch { }

                progressDialog.Dispose();
            }
        }

        // 処理結果を格納する構造体
        private class ImageProcessResult
        {
            public bool Success { get; set; }
            public string? JudgeResult { get; set; }
            public string? CategoryName { get; set; }
            public double Score { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}
