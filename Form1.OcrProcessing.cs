using OpenCvSharp;
using System.Diagnostics;
using System.Text;
using Windows.Graphics.Imaging;

namespace ImageJudgement2
{
    /// <summary>
    /// OCR処理関連（簡素化版・最適化版）
    /// - 目的: 画像を拡大して Windows OCR でテキストを抽出する最小実装
    /// - 最適化: アップスケール画像再利用、ROI検証共通化、一時ファイル削減、並列処理
    /// </summary>
    public partial class Form1
    {
        // Maintain two separate Windows OCR engines: English and Japanese
        private Windows.Media.Ocr.OcrEngine? windowsOcrEngineEn;
        private Windows.Media.Ocr.OcrEngine? windowsOcrEngineJa;

        // Language selection for OCR calls
        private enum OcrLang
        {
            Auto,
            English,
            Japanese
        }

        /// <summary>
        /// アップスケール画像キャッシュ管理
        /// </summary>
        private class UpscaledImageCache : IDisposable
        {
            public string OriginalPath { get; }
            public string UpscaledPath { get; }
            private bool _disposed;

            public UpscaledImageCache(string originalPath, string upscaledPath)
            {
                OriginalPath = originalPath;
                UpscaledPath = upscaledPath;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    try
                    {
                        if (File.Exists(UpscaledPath) && UpscaledPath != OriginalPath)
                        {
                            File.Delete(UpscaledPath);
                            Debug.WriteLine($"[OCR] アップスケール画像を削除: {UpscaledPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OCR] アップスケール画像削除エラー: {ex.Message}");
                    }
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// ROI を画像境界内にクランプして検証
        /// </summary>
        private static bool TryClampRoi(Rectangle rect, int imageWidth, int imageHeight, out OpenCvSharp.Rect clampedRect)
        {
            int x = Math.Max(0, rect.X);
            int y = Math.Max(0, rect.Y);
            int w = rect.Width;
            int h = rect.Height;

            if (w <= 0 || h <= 0)
            {
                Debug.WriteLine($"[OCR] 無効な ROI サイズ: {rect}");
                clampedRect = default;
                return false;
            }

            if (x >= imageWidth || y >= imageHeight)
            {
                Debug.WriteLine($"[OCR] ROI が画像範囲外: {rect} (image {imageWidth}x{imageHeight})");
                clampedRect = default;
                return false;
            }

            w = Math.Min(w, imageWidth - x);
            h = Math.Min(h, imageHeight - y);

            if (w <= 0 || h <= 0)
            {
                Debug.WriteLine($"[OCR] クランプ後の ROI が無効: ({x},{y},{w},{h})");
                clampedRect = default;
                return false;
            }

            clampedRect = new OpenCvSharp.Rect(x, y, w, h);
            return true;
        }

        /// <summary>
        /// Windows OCRエンジンを初期化（英語と日本語を個別に初期化）
        /// </summary>
        private void InitializeWindowsOcr()
        {
            try
            {
                var english = new Windows.Globalization.Language("en");
                if (Windows.Media.Ocr.OcrEngine.IsLanguageSupported(english))
                {
                    windowsOcrEngineEn = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(english);
                    Debug.WriteLine("[OCR] Windows OCR (英語) を初期化しました");
                }
                else
                {
                    Debug.WriteLine("[OCR] Windows OCR (英語) は利用できません");
                }

                var japanese = new Windows.Globalization.Language("ja");
                if (Windows.Media.Ocr.OcrEngine.IsLanguageSupported(japanese))
                {
                    windowsOcrEngineJa = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(japanese);
                    Debug.WriteLine("[OCR] Windows OCR (日本語) を初期化しました");
                }
                else
                {
                    Debug.WriteLine("[OCR] Windows OCR (日本語) は利用できません");
                }

                if (windowsOcrEngineEn is null && windowsOcrEngineJa is null)
                {
                    Debug.WriteLine("[OCR] Windows OCR が利用できません");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OCR] Windows OCR 初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 画像からOCRで文字を取得 (Windows OCR)
        /// 前処理は拡大のみ
        /// 変更: OcrResult を返すようにする
        /// </summary>
        private async Task<Windows.Media.Ocr.OcrResult?> ExtractTextFromImageAsync(string imagePath, bool usePreprocessing = true, OcrLang lang = OcrLang.Auto)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                Debug.WriteLine($"[OCR] ファイルが存在しません: {imagePath}");
                return null;
            }

            try
            {
                Debug.WriteLine($"[OCR] 開始: {imagePath}");

                if (windowsOcrEngineEn is null && windowsOcrEngineJa is null)
                    InitializeWindowsOcr();

                // select engine according to requested language
                Windows.Media.Ocr.OcrEngine? engineToUse = null;
                switch (lang)
                {
                    case OcrLang.English:
                        engineToUse = windowsOcrEngineEn;
                        break;
                    case OcrLang.Japanese:
                        engineToUse = windowsOcrEngineJa;
                        break;
                    case OcrLang.Auto:
                    default:
                        // prefer English, otherwise Japanese
                        engineToUse = windowsOcrEngineEn ?? windowsOcrEngineJa;
                        break;
                }

                if (engineToUse is null)
                {
                    Debug.WriteLine("[OCR] Windows OCRが利用できません");
                    return null;
                }

                string targetImagePath = imagePath;
                if (usePreprocessing)
                {
                    Debug.WriteLine("[OCR] 前処理: 画像を拡大します");
                    targetImagePath = UpscaleImageAsync(imagePath);
                }

                try
                {
                    var ocrResult = await PerformWindowsOcrAsync(targetImagePath, engineToUse);
                    if (ocrResult is null)
                    {
                        Debug.WriteLine("[OCR] OCR結果が null です");
                        return null;
                    }

                    Debug.WriteLine("[OCR] OCR処理が完了しました（OcrResult を返します）");
                    return ocrResult;
                }
                finally
                {
                    if (usePreprocessing && targetImagePath != imagePath && File.Exists(targetImagePath))
                    {
                        try { File.Delete(targetImagePath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OCR] エラー: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Windows OCR を実行
        /// （変更点: 文字列ではなく OcrResult を返す）
        /// </summary>
        private async Task<Windows.Media.Ocr.OcrResult?> PerformWindowsOcrAsync(string imagePath, Windows.Media.Ocr.OcrEngine engine)
        {
            if (engine is null)
                return null;

            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(imagePath);
                using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);

                Debug.WriteLine($"[OCR] 画像サイズ: {decoder.PixelWidth}x{decoder.PixelHeight}");

                var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                Debug.WriteLine("[OCR] Windows OCR 実行中...");
                var ocrResult = await engine.RecognizeAsync(softwareBitmap);

                Debug.WriteLine($"[OCR] 検出された行数: {ocrResult?.Lines.Count ?? 0}");
                return ocrResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OCR] Windows OCR 実行エラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// OcrResult の中から指定した文字列が現れる矩形領域を取得します。
        /// 戻り値は画像座標系の System.Drawing.Rectangle の配列です。
        /// 部分一致を許可し、複数のワードにまたがる一致も結合して矩形を返します。
        /// </summary>
        private static System.Drawing.Rectangle[] FindTextRectangles(Windows.Media.Ocr.OcrResult ocrResult, string target, bool caseSensitive = false)
        {
            var list = new System.Collections.Generic.List<System.Drawing.Rectangle>();
            if (ocrResult is null || string.IsNullOrEmpty(target))
                return list.ToArray();

            var targetNorm = caseSensitive ? target : target.ToLowerInvariant();

            foreach (var line in ocrResult.Lines)
            {
                var words = line.Words;
                int n = words.Count;
                for (int i = 0; i < n; i++)
                {
                    string concatNoSpace = string.Empty;
                    string concatWithSpace = string.Empty;
                    for (int j = i; j < n; j++)
                    {
                        var wtext = words[j].Text ?? string.Empty;
                        var wnorm = caseSensitive ? wtext : wtext.ToLowerInvariant();
                        concatNoSpace += wnorm;
                        concatWithSpace = (j == i) ? wnorm : (concatWithSpace + " " + wnorm);

                        // Check if target appears in either representation
                        if (concatNoSpace.Contains(targetNorm) || concatWithSpace.Contains(targetNorm))
                        {
                            // combine bounding rects from i..j
                            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                            for (int k = i; k <= j; k++)
                            {
                                var rx = words[k].BoundingRect; // Windows.Foundation.Rect (non-nullable)
                                minX = Math.Min(minX, rx.X);
                                minY = Math.Min(minY, rx.Y);
                                maxX = Math.Max(maxX, rx.X + rx.Width);
                                maxY = Math.Max(maxY, rx.Y + rx.Height);
                            }

                            if (minX <= maxX && minY <= maxY && minX != double.MaxValue)
                            {
                                int x = (int)Math.Floor(minX);
                                int y = (int)Math.Floor(minY);
                                int w = (int)Math.Ceiling(maxX - minX);
                                int h = (int)Math.Ceiling(maxY - minY);
                                list.Add(new System.Drawing.Rectangle(x, y, w, h));
                            }

                            // move to next start to avoid overlapping duplicates
                            break;
                        }
                    }
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// 画像を単純に拡大して一時ファイルに保存して返す
        /// </summary>
        private string UpscaleImageAsync(string imagePath, double scale = 4.0)
        {
            try
            {
                using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (src.Empty())
                    return imagePath;

                int newWidth = Math.Max(1, (int)Math.Round(src.Width * scale));
                int newHeight = Math.Max(1, (int)Math.Round(src.Height * scale));

                using var resized = new Mat();
                Cv2.Resize(src, resized, new OpenCvSharp.Size(newWidth, newHeight), 0, 0, InterpolationFlags.Cubic);
                string tempPath = Path.Combine(Path.GetTempPath(), $"ocr_upscaled_{DateTime.Now:yyyyMMddHHmmssffff}.png");

                Debug.WriteLine($"[OCR] 画像を拡大しました: {src.Width}x{src.Height} → {resized.Width}x{resized.Height}");
                Cv2.ImWrite(tempPath, resized);

                return tempPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OCR] 画像拡大エラー: {ex.Message}");
                return imagePath;
            }
        }

        /// <summary>
        /// 画像ファイルパス版の前処理オーバーロード
        /// - ファイル存在チェックを行い、OpenCvSharp で読み込んで既存の Mat ベースの前処理関数を呼び出します。
        /// - 読み込み失敗時は空の Mat を返します（呼び出し側で Dispose してください）。
        /// </summary>
        private static Mat PreprocessForOcr(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                Debug.WriteLine("[OCR] 画像パスが空です。");
                return new Mat();
            }

            if (!File.Exists(imagePath))
            {
                Debug.WriteLine($"[OCR] ファイルが存在しません: {imagePath}");
                return new Mat();
            }

            var src = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (src.Empty())
            {
                Debug.WriteLine($"[OCR] 画像の読み込みに失敗しました: {imagePath}");
                src.Dispose();
                return new Mat();
            }

            try
            {
                var processed = PreprocessForOcr(src);
                return processed;
            }
            finally
            {
                src.Dispose();
            }
        }

        /// <summary>
        /// 前処理: グレースケール化、平滑化、Otsu 二値化、3チャンネルに戻す
        /// 入力 Mat は破壊しません。呼び出し側で戻り値 Mat を破棄してください。
        /// </summary>
        private static Mat PreprocessForOcr(Mat input)
        {
            var gray = new Mat();
            Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);

            var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(3, 3), 0);

            var binary = new Mat();
            Cv2.Threshold(blurred, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            var outMat = new Mat();
            Cv2.CvtColor(binary, outMat, ColorConversionCodes.GRAY2BGR);

            gray.Dispose();
            blurred.Dispose();
            binary.Dispose();

            return outMat;
        }

        /// <summary>
        /// 矩形領域からOCRを行うユーティリティ（最適化版 - 一時ファイル削減）
        /// 戻り値は文字列のまま維持するため、内部で OcrResult を文字列化する
        /// </summary>
        private async Task<string?> ExtractTextFromRectangleAsync(string imagePath, Rectangle rect, OcrLang lang = OcrLang.Auto)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                Debug.WriteLine($"[OCR] ファイルが存在しません: {imagePath}");
                return null;
            }

            try
            {
                using var src = Cv2.ImRead(imagePath);
                if (src.Empty())
                {
                    Debug.WriteLine($"[OCR] 画像の読み込みに失敗: {imagePath}");
                    return null;
                }

                // ROI検証（共通化されたメソッド使用）
                if (!TryClampRoi(rect, src.Width, src.Height, out var cvRect))
                {
                    return null;
                }

                using var roi = new Mat(src, cvRect);

                if (roi.Empty())
                {
                    Debug.WriteLine("[OCR] ROI が空です。処理を中止します。");
                    return null;
                }

                // 二値化処理をメモリ内で実行（一時ファイル削減）
                using var gray = new Mat();
                Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);

                using var blurred = new Mat();
                Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(3, 3), 0);

                using var binary = new Mat();
                Cv2.Threshold(blurred, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                // OCR用に一時保存（1回のみ）
                string tempPath = Path.Combine(Path.GetTempPath(), $"ocr_roi_{DateTime.Now:yyyyMMddHHmmssffff}.png");
                Cv2.ImWrite(tempPath, binary);

                // 切り出した画像を拡大する
                string upscaledPath = UpscaleImageAsync(tempPath);

                try
                {
                    var ocrResult = await ExtractTextFromImageAsync(upscaledPath, usePreprocessing: false, lang: lang);
                    if (ocrResult is null)
                        return null;

                    var sb = new StringBuilder();
                    foreach (var line in ocrResult.Lines)
                        sb.AppendLine(line.Text);

                    return sb.ToString();
                }
                finally
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    try { if (File.Exists(upscaledPath)) File.Delete(upscaledPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OCR] エラー: {ex}");
                return null;
            }
        }

        /// <summary>
        /// OCRテスト（最適化版 - アップスケール画像再利用 + 並列処理）
        /// ExtractTextFromImageAsync が OcrResult を返すようになったため内部で文字列化する
        /// </summary>
        private async Task<bool> IsAoiOK(bool isTest = false)
        {
            if (string.IsNullOrEmpty(selectedImagePath))
            {
                MessageBox.Show("画像を選択してください。", "OCRテスト", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            try
            {
                Debug.WriteLine($"[OCR TEST] 開始: {selectedImagePath}");

                // アップスケール画像を1回だけ作成して再利用
                string upscaledPath = UpscaleImageAsync(selectedImagePath);
                using var upscaleCache = new UpscaledImageCache(selectedImagePath, upscaledPath);

                Rectangle rectangle = new();

                // 日本語と英語のOCRを並列実行
                var (ocrResultJp, ocrResultEn) = await Task.WhenAll(
                    ExtractTextFromImageAsync(upscaledPath, usePreprocessing: false, OcrLang.Japanese),
                    ExtractTextFromImageAsync(upscaledPath, usePreprocessing: false, OcrLang.English)
                ).ContinueWith(t => (t.Result[0], t.Result[1]));

                // 日本語OCR結果から「判定」の位置を取得
                if (ocrResultJp is not null)
                {
                    var rectJp = FindTextRectangles(ocrResultJp, "判定");
                    if (rectJp.Length > 0)
                    {
                        rectangle.X = rectJp[0].X - 10;
                        rectangle.Y = rectJp[0].Y + rectJp[0].Height;
                        rectangle.Width = rectJp[0].Width + 10;
                    }
                }

                // 英語OCR結果から「Total」の位置を取得
                if (ocrResultEn is not null)
                {
                    var rectEn = FindTextRectangles(ocrResultEn, "Total", true);
                    if (rectEn.Length > 0)
                    {
                        int top = rectangle.Y;
                        int bottom = rectEn[0].Y;
                        int computedH = bottom - top;

                        if (computedH <= 0)
                        {
                            computedH = (rectEn[0].Y + rectEn[0].Height) - top;
                        }

                        rectangle.Height = Math.Max(1, computedH);
                    }
                }

                // 最低限の検証: 幅・高さが有効か確認
                if (rectangle.Width <= 0 || rectangle.Height <= 0)
                {
                    Debug.WriteLine($"[OCR TEST] 無効な抽出領域が計算されました: {rectangle}");
                    if (isTest)
                        MessageBox.Show("抽出領域が不正です。OCR結果に基づく矩形が見つからなかった可能性があります。", "OCRテスト結果", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    
                    return true;
                }

                Debug.WriteLine($"[OCR TEST] 抽出領域: {rectangle}");

                // 同じアップスケール画像を使用して矩形領域からテキスト抽出
                string? extractedText = await ExtractTextFromRectangleAsync(upscaledPath, rectangle, OcrLang.English);
                Debug.WriteLine($"[OCR TEST] 抽出テキスト:\n{extractedText}");

                var result = new System.Text.StringBuilder();
                bool ret = true;

                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    bool hasNg = extractedText.IndexOf("NG", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (hasNg == true)
                    {
                        ret = !hasNg;
                    }

                    if (isTest)
                    {
                        // プレビューの生成: 最大500文字に切り詰め、末尾の改行を削除してから AppendLine を呼ぶ
                        var preview = extractedText.Length > 500 ? extractedText.Substring(0, 500) + "..." : extractedText;
                        result.AppendLine("----- 抽出テキスト プレビュー -----");
                        result.AppendLine(preview.TrimEnd('\r', '\n'));
                        result.AppendLine("--------------------------------");
                    }

                    if (hasNg)
                        result.AppendLine("判定: NG が検出されました（大文字小文字を区別しません）");
                    else
                        result.AppendLine("判定: NG は検出されませんでした");
                }
                else
                {
                    result.AppendLine("（テキストが抽出できませんでした）");
                }

                Debug.WriteLine("[OCR TEST] 完了");

                if (isTest)
                    MessageBox.Show(result.ToString(), "OCRテスト結果", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return ret;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OCR TEST] エラー: {ex.Message}");
                MessageBox.Show($"OCRテストでエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return false;
            }
        }

        /// <summary>
        /// 二値化処理（後方互換性のため残すが、ExtractTextFromRectangleAsync 内で統合済み）
        /// </summary>
        private string BinarizeAndSaveTempFile(string imagePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    Debug.WriteLine("[OCR] 画像パスが空です。");
                    return imagePath;
                }

                if (!File.Exists(imagePath))
                {
                    Debug.WriteLine($"[OCR] ファイルが存在しません: {imagePath}");
                    return imagePath;
                }

                using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (src.Empty())
                {
                    Debug.WriteLine($"[OCR] 画像の読み込みに失敗しました: {imagePath}");
                    return imagePath;
                }

                using var gray = new Mat();
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

                using var blurred = new Mat();
                Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(3, 3), 0);

                using var binary = new Mat();
                Cv2.Threshold(blurred, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                string tempPath = Path.Combine(Path.GetTempPath(), $"ocr_binarized_{DateTime.Now:yyyyMMddHHmmssffff}.png");
                Cv2.ImWrite(tempPath, binary);

                Debug.WriteLine($"[OCR] 二値化画像を保存しました: {tempPath}");
                return tempPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OCR] 二値化エラー: {ex.Message}");
                return imagePath;
            }
        }
    }
}