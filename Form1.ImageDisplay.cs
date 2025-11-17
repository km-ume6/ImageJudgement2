using System.Diagnostics;

namespace ImageJudgement2
{
    /// <summary>
    /// 画像表示関連の処理
    /// </summary>
    public partial class Form1
    {
        // 判定結果のキャッシュ
        private string? cachedJudgeResult;

        private async Task LoadSelectedImageAsync(string path)
        {
            try
            {
                selectedImagePath = path;
                var bmp = await Task.Run(() =>
                           {
                               using var fs = File.OpenRead(path);
                               using var img = Image.FromStream(fs);
                               return new Bitmap(img);
                           }).ConfigureAwait(false);

                this.BeginInvoke(() =>
                  {
                      try
                      {
                          if (selectedImagePath != path)
                          {
                              bmp.Dispose();
                              return;
                          }
                          currentImage?.Dispose();
                          currentImage = bmp;
                          rightTopPanel?.Invalidate();

                          // 自動検出が有効な場合は円検出とAI判定を実行
                          if (chkAutoDetect?.Checked == true)
                          {
                              _ = AutoDetectCircleAndAiAsync();
                          }
                          else
                          {
                              // 自動検出が無効な場合は検出画像とAI判定結果をクリア
                              detectedCircleImage?.Dispose();
                              detectedCircleImage = null;
                              detectedCompositeImage?.Dispose();
                              detectedCompositeImage = null;
                              currentAiResult = null;
                              if (rightBottomTextBox != null)
                                  rightBottomTextBox.Text = string.Empty;
                              rightBottomLeftPanel?.Invalidate();
                              rightBottomRightPanel?.Invalidate();
                          }
                      }
                      catch (Exception ex)
                      {
                          Debug.WriteLine($"Failed to assign loaded image: {ex}");
                          bmp.Dispose();
                      }
                  });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadSelectedImageAsync failed for '{path}': {ex}");
                this.BeginInvoke(() =>
                 {
                     if (selectedImagePath == path)
                     {
                         selectedImagePath = null;
                         currentImage?.Dispose();
                         currentImage = null;
                         rightTopPanel?.Invalidate();
                     }
                 });
            }
        }

        private void ClearDisplayedImages()
        {
            selectedImagePath = null;
            currentImage?.Dispose();
            currentImage = null;
            detectedCircleImage?.Dispose();
            detectedCircleImage = null;
            detectedCompositeImage?.Dispose();
            detectedCompositeImage = null;
            currentAiResult = null;
            cachedJudgeResult = null; // キャッシュもクリア
            if (rightBottomTextBox != null)
                rightBottomTextBox.Text = string.Empty;
            rightTopPanel?.Invalidate();
            rightBottomLeftPanel?.Invalidate();
            rightBottomRightPanel?.Invalidate();
        }

        private void RightTopPanel_Paint(object? sender, PaintEventArgs e)
        {
            var img = currentImage;
            var panel = sender as Panel;
            if (panel == null || img == null) return;

            int iw = img.Width, ih = img.Height;
            if (iw <= 0 || ih <= 0) return;

            float scale = Math.Min((float)panel.Width / iw, (float)panel.Height / ih);
            int dw = Math.Max(1, (int)Math.Round(iw * scale));
            int dh = Math.Max(1, (int)Math.Round(ih * scale));
            int dx = (panel.Width - dw) / 2;
            int dy = (panel.Height - dh) / 2;

            try
            {
                e.Graphics.DrawImage(img, dx, dy, dw, dh);
            }
            catch (ArgumentException)
            {
                // Image disposed while painting
            }
        }

        private void RightBottomPanel_Paint(object? sender, PaintEventArgs e)
        {
            // このイベントは使用しない（rightBottomPanelはコンテナとして機能）
        }

        private void RightBottomLeftPanel_Paint(object? sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null) return;

            try
            {
                var g = e.Graphics;
                var panelWidth = panel.Width;
                var panelHeight = panel.Height;

                // 背景をクリア
                g.Clear(panel.BackColor);

                if (currentAiResult == null)
                {
                    DrawCenteredText(g, panelWidth, panelHeight, "AI判定結果なし", SystemFonts.DefaultFont, Brushes.Gray);
                    return;
                }

                // AI判定結果詳細を表示
                int yPos = 20;
                int lineHeight = 30;

                if (currentAiResult.IsProcessing)
                {
                    DrawCenteredText(g, panelWidth, panelHeight, "処理中...",
                        new Font(SystemFonts.DefaultFont.FontFamily, 12, FontStyle.Bold), Brushes.Orange);
                }
                else if (currentAiResult.TopClassResult != null)
                {
                    var result = currentAiResult.TopClassResult;

                    // 判定モードと判定結果を表示
                    DrawText(g, 20, yPos, $"判定モード: {GetJudgeModeShortName(detectionMode)}",
                        new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Regular), Brushes.DarkBlue);
                    yPos += lineHeight;

                    DrawText(g, 20, yPos, $"判定結果: {cachedJudgeResult ?? "未判定"}",
                        new Font(SystemFonts.DefaultFont.FontFamily, 12, FontStyle.Bold),
                        cachedJudgeResult == "OK" ? Brushes.Green : cachedJudgeResult == "NG" ? Brushes.Red : Brushes.Orange);
                    yPos += lineHeight;

                    // カテゴリ名
                    DrawText(g, 20, yPos, $"カテゴリ: {result.CategoryName}",
                        new Font(SystemFonts.DefaultFont.FontFamily, 12, FontStyle.Bold), Brushes.Black);
                    yPos += lineHeight;

                    // スコア
                    DrawText(g, 20, yPos, $"スコア: {result.Score:F4}",
                        SystemFonts.DefaultFont, Brushes.Black);
                    yPos += lineHeight;

                    // クラス
                    DrawText(g, 20, yPos, $"クラス: {result.Class}",
                        SystemFonts.DefaultFont, Brushes.DarkGray);
                    yPos += lineHeight;

                    // カテゴリID
                    DrawText(g, 20, yPos, $"ID: {result.CategoryId}",
                        SystemFonts.DefaultFont, Brushes.DarkGray);
                }
                else
                {
                    DrawCenteredText(g, panelWidth, panelHeight, "判定結果が取得できませんでした",
                        SystemFonts.DefaultFont, Brushes.Red);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RightBottomLeftPanel_Paint error: {ex}");
            }
        }

        /// <summary>
        /// 判定モードの短縮表示名を取得
        /// </summary>
        private string GetJudgeModeShortName(JudgeMode mode)
        {
            return mode switch
            {
                JudgeMode.TopClass => "TopClass",
                JudgeMode.ScoreAverage => "ScoreAverage",
                JudgeMode.ScoreRanking => "ScoreRanking",
                _ => "Unknown"
            };
        }

        private void RightBottomRightPanel_Paint(object? sender, PaintEventArgs e)
        {
            var panel = sender as Panel;
            if (panel == null) return;

            try
            {
                var g = e.Graphics;
                var panelWidth = panel.Width;
                var panelHeight = panel.Height;

                // 背景をクリア
                g.Clear(panel.BackColor);

                // Show processing indicator when AI is running
                if (currentAiResult != null && currentAiResult.IsProcessing)
                {
                    string displayText = "処理中...";
                    Color textColor = Color.White;
                    Color backgroundColor = Color.FromArgb(255, 143, 0); // orange

                    g.Clear(backgroundColor);

                    int fontSize = Math.Min(panelWidth, panelHeight) / 3;
                    fontSize = Math.Max(24, Math.Min(fontSize, 200));

                    using var font = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold);
                    using var brush = new SolidBrush(textColor);

                    DrawCenteredText(g, panelWidth, panelHeight, displayText, font, brush);
                    return;
                }

                if (currentAiResult == null)
                {
                    return;
                }

                if (currentAiResult.TopClassResult != null)
                {
                    var categoryName = currentAiResult.TopClassResult.CategoryName?.ToUpper() ?? "";

                    string displayText;
                    Color textColor;
                    Color backgroundColor;

                    // キャッシュされた判定結果を使用
                    string JudgeResult = cachedJudgeResult ?? "Unknown";

                    if (JudgeResult == "OK")
                    {
                        displayText = "OK";
                        textColor = Color.White;
                        backgroundColor = Color.FromArgb(46, 125, 50); // 濃い緑
                    }
                    else if (JudgeResult == "NG")
                    {
                        displayText = "NG";
                        textColor = Color.White;
                        backgroundColor = Color.FromArgb(198, 40, 40); // 濃い赤
                    }
                    else
                    {
                        // OKでもNGでもない場合
                        displayText = "Unknown";
                        textColor = Color.Black;
                        backgroundColor = Color.FromArgb(255, 235, 59); // 黄色
                    }

                    // 背景色を設定
                    g.Clear(backgroundColor);

                    // 大きなフォントで判定結果を中央に表示
                    int fontSize = Math.Min(panelWidth, panelHeight) / 3;
                    fontSize = Math.Max(24, Math.Min(fontSize, 200)); // 24?200の範囲

                    using var font = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, FontStyle.Bold);
                    using var brush = new SolidBrush(textColor);

                    DrawCenteredText(g, panelWidth, panelHeight, displayText, font, brush);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RightBottomRightPanel_Paint error: {ex}");
            }
        }

        /// <summary>
        /// テキストを描画
        /// </summary>
        private void DrawText(Graphics g, int x, int y, string text, Font font, Brush brush)
        {
            g.DrawString(text, font, brush, new PointF(x, y));
        }

        /// <summary>
        /// テキストを中央に描画
        /// </summary>
        private void DrawCenteredText(Graphics g, int width, int height, string text, Font font, Brush brush)
        {
            var size = g.MeasureString(text, font);
            float x = (width - size.Width) / 2;
            float y = (height - size.Height) / 2;
            g.DrawString(text, font, brush, new PointF(x, y));
        }

        /// <summary>
        /// 自動検出：円検出とAI判定を実行
        /// </summary>
        private async Task AutoDetectCircleAndAiAsync()
        {
            if (currentImage == null || string.IsNullOrEmpty(selectedImagePath))
            {
                return;
            }

            try
            {
                Debug.WriteLine($"[AutoDetect] 開始: {selectedImagePath}");

                // AI判定結果をクリア（処理中状態に設定）
                this.Invoke(() =>
                {
                    currentAiResult = new AiModelResult { IsProcessing = true };
                    cachedJudgeResult = null; // キャッシュもクリア
                    if (rightBottomTextBox != null)
                        rightBottomTextBox.Text = "処理中...";
                    rightBottomLeftPanel?.Invalidate();
                    rightBottomRightPanel?.Invalidate();
                });

                //string ocrText = await Task.Run(() => ExtractTextFromImageAsync(selectedImagePath));
                //Debug.Write(ocrText);

                // 円を検出
                var circles = await Task.Run(() => DetectCircles(selectedImagePath));

                if (circles == null || circles.Length == 0)
                {
                    Debug.WriteLine("[AutoDetect] 円が検出できませんでした");
                    this.Invoke(() =>
                    {
                        currentAiResult = null;
                        cachedJudgeResult = null;
                        if (rightBottomTextBox != null)
                            rightBottomTextBox.Text = string.Empty;
                        rightBottomLeftPanel?.Invalidate();
                        rightBottomRightPanel?.Invalidate();
                    });
                    return;
                }

                var circle = circles[0];
                var croppedImage = await Task.Run(() => CropCircleFromImage(selectedImagePath, circle));

                if (croppedImage == null)
                {
                    Debug.WriteLine("[AutoDetect] 円形領域の切り抜きに失敗しました");
                    this.Invoke(() =>
                    {
                        currentAiResult = null;
                        cachedJudgeResult = null;
                        if (rightBottomTextBox != null)
                            rightBottomTextBox.Text = string.Empty;
                        rightBottomLeftPanel?.Invalidate();
                        rightBottomRightPanel?.Invalidate();
                    });
                    return;
                }

                // 円検出画像を表示（右上パネルに表示する場合はコメントアウトを解除）
                // this.Invoke(() =>
                // {
                //     detectedCircleImage?.Dispose();
                //     detectedCircleImage = croppedImage;
                //     rightBottomLeftPanel?.Invalidate();
                // });

                // 一時ファイルに保存
                string tempPath = Path.Combine(Path.GetTempPath(), $"circle_{DateTime.Now:yyyyMMddHHmmssffff}.png");
                croppedImage.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);

                // AI判定を実行
                var aiResult = await ExecuteAiPredictionAsync(tempPath, selectedImagePath);

                // 一時ファイルを削除
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AutoDetect] 一時ファイル削除エラー: {ex.Message}");
                }

                // 判定結果を計算してキャッシュ（ここで1回だけ呼ぶ）
                string judgeResult = aiResult?.Judge(detectionMode) ?? "結果なし";

                // 結果を表示
                Debug.WriteLine($"[AutoDetect] 完了: {judgeResult}");

                this.Invoke(() =>
                {
                    currentAiResult = aiResult;
                    cachedJudgeResult = judgeResult; // 判定結果をキャッシュ

                    // 左下テキストボックスにトップクラス情報を表示
                    if (rightBottomTextBox != null)
                    {
                        if (aiResult == null)
                        {
                            rightBottomTextBox.Text = string.Empty;
                        }
                        else
                        {
                            rightBottomTextBox.Text = FormatAiResult(aiResult);
                        }
                    }

                    rightBottomLeftPanel?.Invalidate();
                    rightBottomRightPanel?.Invalidate();
                });

                // 円検出画像を破棄
                croppedImage.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoDetect] エラー: {ex}");
                this.Invoke(() =>
                {
                    currentAiResult = null;
                    cachedJudgeResult = null;
                    if (rightBottomTextBox != null)
                        rightBottomTextBox.Text = string.Empty;
                    rightBottomLeftPanel?.Invalidate();
                    rightBottomRightPanel?.Invalidate();
                });
            }
        }

        private string FormatAiResult(AiModelResult result)
        {
            if (result == null) return string.Empty;

            var sb = new System.Text.StringBuilder();

            // 判定モードを最初に表示
            sb.AppendLine($"判定モード: {GetJudgeModeDisplayName(detectionMode)}");
            sb.AppendLine($"判定結果: {cachedJudgeResult ?? "未判定"}");
            sb.AppendLine();
            sb.AppendLine($"処理中: {result.IsProcessing}");

            if (result.TopClassResult != null)
            {
                sb.AppendLine("--- TopClassResult ---");
                sb.AppendLine($"カテゴリ: {result.TopClassResult.CategoryName}");
                sb.AppendLine($"スコア: {result.TopClassResult.Score:F4}");
                sb.AppendLine($"クラス: {result.TopClassResult.Class}");
                sb.AppendLine($"カテゴリID: {result.TopClassResult.CategoryId}");
            }

            if (result.AllClassResults != null && result.AllClassResults.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- OK vs NG ---");

                int okCount = 0, ngCount = 0;
                double okScore = 0.0, ngScore = 0.0;
                foreach (var r in result.AllClassResults)
                {
                    if (r.CategoryName != null && r.CategoryName.ToUpper().Contains("OK"))
                    {
                        okCount++;
                        okScore += r.Score;
                    }
                    else if (r.CategoryName != null && r.CategoryName.ToUpper().Contains("NG"))
                    {
                        ngCount++;
                        ngScore += r.Score;
                    }
                }
                sb.AppendLine($"OK* Score Average : {okScore / okCount:F4}");
                sb.AppendLine($"NG* Score Average : {ngScore / ngCount:F4}");
                sb.AppendLine($"OK* Ranking Score : {result.ComputeOkRankingScore()}");
                sb.AppendLine($"NG* Ranking Score : {result.ComputeNgRankingScore()}");

                sb.AppendLine();
                sb.AppendLine("--- AllClassResults ---");
                foreach (var r in result.AllClassResults)
                {
                    sb.AppendLine($"{r.Class}: {r.CategoryName} ({r.Score:F4})");
                }
            }

            if (!string.IsNullOrEmpty(result.RawResponse))
            {
                sb.AppendLine();
                sb.AppendLine("--- RawResponse ---");
                sb.AppendLine(result.RawResponse);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 判定モードの表示名を取得
        /// </summary>
        private string GetJudgeModeDisplayName(JudgeMode mode)
        {
            return mode switch
            {
                JudgeMode.TopClass => "TopClass (トップクラスのカテゴリ名)",
                JudgeMode.ScoreAverage => "ScoreAverage (スコア平均値)",
                JudgeMode.ScoreRanking => "ScoreRanking (ランキング合計)",
                _ => "Unknown"
            };
        }
    }
}
