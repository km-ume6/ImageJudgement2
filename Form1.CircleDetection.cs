using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Diagnostics;

namespace ImageJudgement2
{
    /// <summary>
    /// 円検出関連の処理
    /// </summary>
    public partial class Form1
    {
        /// <summary>
        /// 円を検出してファイルに保存し、AI判定を実行
        /// </summary>
        private async Task DetectAndSaveCirclesAsync()
        {
            if (currentImage == null || string.IsNullOrEmpty(selectedImagePath))
            {
                MessageBox.Show("画像が選択されていません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnDetectCircles.Enabled = false;
                btnDetectCircles.Text = "処理中...";

                var circles = await Task.Run(() => DetectCircles(selectedImagePath));

                if (circles == null || circles.Length == 0)
                {
                    MessageBox.Show("円が検出できませんでした。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var circle = circles[0];
                var croppedImage = await Task.Run(() => CropCircleFromImage(selectedImagePath, circle));

                if (croppedImage != null)
                {
                    // 円検出画像を表示
                    this.Invoke(() =>
                    {
                        detectedCircleImage?.Dispose();
                        detectedCircleImage = croppedImage;
                        rightBottomLeftPanel?.Invalidate(); // 右下左パネルを再描画
                    });

                    // 一時ファイルに保存
                    string tempPath = Path.Combine(Path.GetTempPath(), $"circle_{DateTime.Now:yyyyMMddHHmmss}.png");
                    croppedImage.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);

                    // AI判定を実行
                    var aiResult = await ExecuteAiPredictionAsync(tempPath);

                    // 一時ファイルを削除
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"一時ファイル削除エラー: {ex.Message}");
                    }

                    // 結果を表示
                    ShowAiResult(aiResult, circles.Length);

                    // AI判定結果を保存して右下右パネルに表示
                    currentAiResult = aiResult;
                    rightBottomRightPanel?.Invalidate();
                }
                else
                {
                    MessageBox.Show("円形領域の切り抜きに失敗しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"円検出処理中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Debug.WriteLine($"DetectAndSaveCirclesAsync error: {ex}");
            }
            finally
            {
                btnDetectCircles.Enabled = true;
                btnDetectCircles.Text = "円検出";
            }
        }

        /// <summary>
        /// AI判定を実行（IsAoiOK を同期的に呼び出す）
        /// </summary>
        private async Task<AiModelResult?> ExecuteAiPredictionAsync(string imagePath, string selectedImagePath = "")
        {
            try
            {
                // 共有クライアントを取得（usingを削除してソケット枯渇を防止）
                var aiClient = GetOrCreateSharedAiClient();

                if (aiClient == null)
                {
                    Debug.WriteLine("AI判定APIの設定が不完全です。");
                    return null;
                }

                if (await IsAoiOK())
                {
                    Debug.WriteLine($"AI判定開始: {imagePath}");

                    // アプリケーション終了時のキャンセルをサポート
                    var result = await aiClient.PredictImageAsync(imagePath, _appLifetimeCts.Token);

                    Debug.WriteLine($"AI判定完了: {result.TopClassResult}");
                    return result;
                }
                else
                {
                    var result = new AiModelResult();
                    // TopClassResult が null の可能性を考慮して初期化
                    if (result.TopClassResult == null)
                    {
                        try
                        {
                            result.TopClassResult = new ClassificationResult();
                        }
                        catch
                        {
                            // ClassificationResult 型の定義が異なる場合でも例外を避ける
                        }
                    }

                    try
                    {
                        if (result.TopClassResult != null)
                        {
                            result.TopClassResult.CategoryName = "NG by AOI";
                            result.TopClassResult.Score = 1.0;
                            result.IsProcessing = false;
                            result.AllClassResults = new List<ClassificationResult>
                            {
                                new ClassificationResult
                                {
                                    CategoryName = "NG by AOI",
                                    Score = 1.0
                                }
                            };
                        }

                    }
                    catch
                    {
                        // 安全に無視
                    }

                    return result;
                }

            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"AI判定がキャンセルされました: {imagePath}");
                return null;
            }
            catch (ObjectDisposedException)
            {
                Debug.WriteLine($"AIクライアントが破棄されました: {imagePath}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI判定エラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 共有AIクライアントを取得または作成（ソケット枯渇防止のため再利用）
        /// </summary>
        private AiModelClient? GetOrCreateSharedAiClient()
        {
            lock (_aiClientLock)
            {
                if (_sharedAiClient != null)
                {
                    return _sharedAiClient;
                }

                if (string.IsNullOrWhiteSpace(aiApiKey) ||
                    string.IsNullOrWhiteSpace(aiModelId) ||
                    string.IsNullOrWhiteSpace(aiApiUrl))
                {
                    Debug.WriteLine("AI判定API接続情報が設定されていません。");
                    return null;
                }

                try
                {
                    _sharedAiClient = new AiModelClient(aiApiKey, aiModelId, aiModelType, aiApiUrl);
                    return _sharedAiClient;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AiModelClient作成エラー: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// AI判定結果を表示
        /// </summary>
        private void ShowAiResult(AiModelResult? aiResult, int circleCount)
        {
            if (aiResult == null)
            {
                MessageBox.Show(
                    $"円形領域を検出しました({circleCount}個中1番目を表示)\n\nAI判定: 設定が不完全または実行エラー",
                    "完了",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (aiResult.IsProcessing)
            {
                MessageBox.Show(
                    $"円形領域を検出しました({circleCount}個中1番目を表示)\n\nAI判定: まだ処理中です",
                    "完了",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string resultMessage = $"円形領域を検出しました({circleCount}個中1番目を表示)\n\n" +
                                   $"AI判定結果: {aiResult.TopClassResult?.CategoryName ?? "不明"}";

            MessageBox.Show(resultMessage, "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private CircleSegment[] DetectCircles(string imagePath)
        {
            using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (src.Empty())
            {
                return Array.Empty<CircleSegment>();
            }

            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            using var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(9, 9), 2, 2);

            int minRadius = 10;
            int maxRadius = 0;
            int param1 = 100;
            int param2 = 30;

            if (nudMinRadius?.InvokeRequired == true)
            {
                nudMinRadius.Invoke(() =>
                {
                    minRadius = (int)nudMinRadius.Value;
                    maxRadius = (int)nudMaxRadius.Value;
                    param1 = (int)nudParam1.Value;
                    param2 = (int)nudParam2.Value;
                });
            }
            else if (nudMinRadius != null)
            {
                minRadius = (int)nudMinRadius.Value;
                maxRadius = (int)nudMaxRadius.Value;
                param1 = (int)nudParam1.Value;
                param2 = (int)nudParam2.Value;
            }

            var circles = Cv2.HoughCircles(
                blurred,
                HoughModes.Gradient,
                dp: 1,
                minDist: gray.Rows / 8.0,
                param1: param1,
                param2: param2,
                minRadius: minRadius,
                maxRadius: maxRadius
            );

            return circles;
        }

        private Bitmap? CropCircleFromImage(string imagePath, CircleSegment circle)
        {
            try
            {
                using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (src.Empty())
                {
                    return null;
                }

                int x = (int)circle.Center.X;
                int y = (int)circle.Center.Y;
                int radius = (int)circle.Radius;

                int left = Math.Max(0, x - radius);
                int top = Math.Max(0, y - radius);
                int width = Math.Min(src.Width - left, radius * 2);
                int height = Math.Min(src.Height - top, radius * 2);

                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                var rect = new OpenCvSharp.Rect(left, top, width, height);
                using var cropped = new Mat(src, rect);

                using var mask = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
                var centerInCrop = new OpenCvSharp.Point(x - left, y - top);
                Cv2.Circle(mask, centerInCrop, radius, Scalar.White, -1);

                using var result = new Mat();
                cropped.CopyTo(result, mask);

                return BitmapConverter.ToBitmap(result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CropCircleFromImage error: {ex}");
                return null;
            }
        }
    }
}
