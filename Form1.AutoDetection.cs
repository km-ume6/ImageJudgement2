using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Diagnostics;

namespace ImageJudgement2
{
    /// <summary>
    /// 自動検出と画像合成処理
    /// </summary>
    public partial class Form1
    {
        /// <summary>
        /// 画像ファイルから円と矩形を検出し、合成画像を保存する
        /// </summary>
        /// <param name="imageFilePath">画像ファイルのフルパス</param>
        /// <param name="outputPath">保存先（空文字列=元画像と同じフォルダ、末尾が\=フォルダ名、それ以外=ファイル名）</param>
        /// <returns>保存されたファイルのパス。失敗時はnull</returns>
        public string? DetectAndSaveCompositeImage(string imageFilePath, string outputPath = "")
        {
            try
            {
                if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
                {
                    Debug.WriteLine($"ファイルが存在しません: {imageFilePath}");
                    return null;
                }

                // 円と矩形を検出
                var circles = DetectCircles(imageFilePath);
                var rectangles = DetectRectangles(imageFilePath);

                // 合成画像を作成
                var compositeImage = CreateCroppedCompositeImage(imageFilePath, circles, rectangles);

                if (compositeImage == null)
                {
                    Debug.WriteLine("検出結果がありません。");
                    return null;
                }

                string outputFilePath;

                if (string.IsNullOrEmpty(outputPath))
                {
                    // デフォルト: 元画像と同じフォルダに自動命名で保存
                    string directory = Path.GetDirectoryName(imageFilePath) ?? string.Empty;
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(imageFilePath);
                    string extension = Path.GetExtension(imageFilePath);
                    string outputFileName = $"{fileNameWithoutExt}_composite{extension}";
                    outputFilePath = Path.Combine(directory, outputFileName);

                    // 既に同名ファイルが存在する場合は連番を付ける
                    int counter = 1;
                    while (File.Exists(outputFilePath))
                    {
                        outputFileName = $"{fileNameWithoutExt}_composite_{counter}{extension}";
                        outputFilePath = Path.Combine(directory, outputFileName);
                        counter++;
                    }
                }
                else if (outputPath.EndsWith("\\") || outputPath.EndsWith("/"))
                {
                    // フォルダ名として処理
                    string directory = outputPath.TrimEnd('\\', '/');

                    // フォルダが存在しない場合は作成
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        Debug.WriteLine($"出力フォルダを作成しました: {directory}");
                    }

                    // ファイル名を自動生成
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(imageFilePath);
                    string extension = Path.GetExtension(imageFilePath);
                    string outputFileName = $"{fileNameWithoutExt}_composite{extension}";
                    outputFilePath = Path.Combine(directory, outputFileName);

                    // 既に同名ファイルが存在する場合は連番を付ける
                    int counter = 1;
                    while (File.Exists(outputFilePath))
                    {
                        outputFileName = $"{fileNameWithoutExt}_composite_{counter}{extension}";
                        outputFilePath = Path.Combine(directory, outputFileName);
                        counter++;
                    }
                }
                else
                {
                    // ファイル名として処理
                    outputFilePath = outputPath;

                    // ディレクトリが存在しない場合は作成
                    string? directory = Path.GetDirectoryName(outputFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        Debug.WriteLine($"出力フォルダを作成しました: {directory}");
                    }

                    // ファイルが既に存在する場合は上書き警告をデバッグ出力
                    if (File.Exists(outputFilePath))
                    {
                        Debug.WriteLine($"既存のファイルを上書きします: {outputFilePath}");
                    }
                }

                // 画像を保存（拡張子に対応した形式で保存）
                string ext = Path.GetExtension(outputFilePath).ToLowerInvariant();
                switch (ext)
                {
                    case ".jpg":
                    case ".jpeg":
                        compositeImage.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                        break;
                    case ".png":
                        compositeImage.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Png);
                        break;
                    case ".bmp":
                        compositeImage.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Bmp);
                        break;
                    case ".gif":
                        compositeImage.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Gif);
                        break;
                    case ".tiff":
                    case ".tif":
                        compositeImage.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Tiff);
                        break;
                    default:
                        // デフォルトはPNG形式
                        compositeImage.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Png);
                        break;
                }

                compositeImage.Dispose();

                Debug.WriteLine($"合成画像を保存しました: {outputFilePath}");
                return outputFilePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DetectAndSaveCompositeImage error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 画像ファイルから円と矩形を検出し、合成画像を保存する（非同期版）
        /// </summary>
        /// <param name="imageFilePath">画像ファイルのフルパス</param>
        /// <param name="outputPath">保存先（空文字列=元画像と同じフォルダ、末尾が\=フォルダ名、それ以外=ファイル名）</param>
        /// <returns>保存されたファイルのパス。失敗時はnull</returns>
        public async Task<string?> DetectAndSaveCompositeImageAsync(string imageFilePath, string outputPath = "")
        {
            return await Task.Run(() => DetectAndSaveCompositeImage(imageFilePath, outputPath));
        }

        /// <summary>
        /// 画像ファイルから円を検出し、切り抜いた画像を保存する
        /// </summary>
        /// <param name="imageFilePath">画像ファイルのフルパス</param>
        /// <param name="outputPath">保存先（空文字列=元画像と同じフォルダ、末尾が\=フォルダ名、それ以外=ファイル名）</param>
        /// <returns>保存されたファイルのパス。失敗時はnull</returns>
        public string? DetectAndSaveCircleImage(string imageFilePath, string outputPath = "")
        {
            try
            {
                if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
                {
                    Debug.WriteLine($"ファイルが存在しません: {imageFilePath}");
                    return null;
                }

                // 円を検出
                var circles = DetectCircles(imageFilePath);

                if (circles == null || circles.Length == 0)
                {
                    Debug.WriteLine("円が検出されませんでした。");
                    return null;
                }

                // 最初の円を切り抜く
                using var src = Cv2.ImRead(imageFilePath, ImreadModes.Color);
                if (src.Empty())
                {
                    Debug.WriteLine("画像の読み込みに失敗しました。");
                    return null;
                }

                var croppedCircle = CropCircleToMat(src, circles[0]);
                if (croppedCircle == null)
                {
                    Debug.WriteLine("円の切り抜きに失敗しました。");
                    return null;
                }

                using var circleImage = BitmapConverter.ToBitmap(croppedCircle);
                croppedCircle.Dispose();

                // 出力パスを決定
                string outputFilePath = DetermineOutputPath(imageFilePath, outputPath, "_circle");

                // 画像を保存
                SaveImageByExtension(circleImage, outputFilePath);

                Debug.WriteLine($"円画像を保存しました: {outputFilePath}");
                return outputFilePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DetectAndSaveCircleImage error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 画像ファイルから円を検出し、切り抜いた画像を保存する（非同期版）
        /// </summary>
        /// <param name="imageFilePath">画像ファイルのフルパス</param>
        /// <param name="outputPath">保存先（空文字列=元画像と同じフォルダ、末尾が\=フォルダ名、それ以外=ファイル名）</param>
        /// <returns>保存されたファイルのパス。失敗時はnull</returns>
        public async Task<string?> DetectAndSaveCircleImageAsync(string imageFilePath, string outputPath = "")
        {
            return await Task.Run(() => DetectAndSaveCircleImage(imageFilePath, outputPath));
        }

        /// <summary>
        /// 画像ファイルから矩形を検出し、切り抜いた画像を保存する
        /// </summary>
        /// <param name="imageFilePath">画像ファイルのフルパス</param>
        /// <param name="outputPath">保存先（空文字列=元画像と同じフォルダ、末尾が\=フォルダ名、それ以外=ファイル名）</param>
        /// <returns>保存されたファイルのパス。失敗時はnull</returns>
        public string? DetectAndSaveRectangleImage(string imageFilePath, string outputPath = "")
        {
            try
            {
                if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
                {
                    Debug.WriteLine($"ファイルが存在しません: {imageFilePath}");
                    return null;
                }

                // 矩形を検出
                var rectangles = DetectRectangles(imageFilePath);

                if (rectangles == null || rectangles.Length == 0)
                {
                    Debug.WriteLine("矩形が検出されませんでした。");
                    return null;
                }

                // 最初の矩形を切り抜く
                using var src = Cv2.ImRead(imageFilePath, ImreadModes.Color);
                if (src.Empty())
                {
                    Debug.WriteLine("画像の読み込みに失敗しました。");
                    return null;
                }

                var croppedRect = CropRectangleToMat(src, rectangles[0]);
                if (croppedRect == null)
                {
                    Debug.WriteLine("矩形の切り抜きに失敗しました。");
                    return null;
                }

                using var rectangleImage = BitmapConverter.ToBitmap(croppedRect);
                croppedRect.Dispose();

                // 出力パスを決定
                string outputFilePath = DetermineOutputPath(imageFilePath, outputPath, "_rectangle");

                // 画像を保存
                SaveImageByExtension(rectangleImage, outputFilePath);

                Debug.WriteLine($"矩形画像を保存しました: {outputFilePath}");
                return outputFilePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DetectAndSaveRectangleImage error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 画像ファイルから矩形を検出し、切り抜いた画像を保存する（非同期版）
        /// </summary>
        /// <param name="imageFilePath">画像ファイルのフルパス</param>
        /// <param name="outputPath">保存先（空文字列=元画像と同じフォルダ、末尾が\=フォルダ名、それ以外=ファイル名）</param>
        /// <returns>保存されたファイルのパス。失敗時はnull</returns>
        public async Task<string?> DetectAndSaveRectangleImageAsync(string imageFilePath, string outputPath = "")
        {
            return await Task.Run(() => DetectAndSaveRectangleImage(imageFilePath, outputPath));
        }

        /// <summary>
        /// 出力ファイルパスを決定する
        /// </summary>
        private string DetermineOutputPath(string imageFilePath, string outputPath, string suffix)
        {
            string outputFilePath;

            if (string.IsNullOrEmpty(outputPath))
            {
                // デフォルト: 元画像と同じフォルダに自動命名で保存
                string directory = Path.GetDirectoryName(imageFilePath) ?? string.Empty;
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(imageFilePath);
                string extension = Path.GetExtension(imageFilePath);
                string outputFileName = $"{fileNameWithoutExt}{suffix}{extension}";
                outputFilePath = Path.Combine(directory, outputFileName);

                // 既に同名ファイルが存在する場合は連番を付ける
                int counter = 1;
                while (File.Exists(outputFilePath))
                {
                    outputFileName = $"{fileNameWithoutExt}{suffix}_{counter}{extension}";
                    outputFilePath = Path.Combine(directory, outputFileName);
                    counter++;
                }
            }
            else if (outputPath.EndsWith("\\") || outputPath.EndsWith("/"))
            {
                // フォルダ名として処理
                string directory = outputPath.TrimEnd('\\', '/');

                // フォルダが存在しない場合は作成
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Debug.WriteLine($"出力フォルダを作成しました: {directory}");
                }

                // ファイル名を自動生成
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(imageFilePath);
                string extension = Path.GetExtension(imageFilePath);
                string outputFileName = $"{fileNameWithoutExt}{suffix}{extension}";
                outputFilePath = Path.Combine(directory, outputFileName);

                // 既に同名ファイルが存在する場合は連番を付ける
                int counter = 1;
                while (File.Exists(outputFilePath))
                {
                    outputFileName = $"{fileNameWithoutExt}{suffix}_{counter}{extension}";
                    outputFilePath = Path.Combine(directory, outputFileName);
                    counter++;
                }
            }
            else
            {
                // ファイル名として処理
                outputFilePath = outputPath;

                // ディレクトリが存在しない場合は作成
                string? directory = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Debug.WriteLine($"出力フォルダを作成しました: {directory}");
                }

                // ファイルが既に存在する場合は上書き警告をデバッグ出力
                if (File.Exists(outputFilePath))
                {
                    Debug.WriteLine($"既存のファイルを上書きします: {outputFilePath}");
                }
            }

            return outputFilePath;
        }

        /// <summary>
        /// 拡張子に応じて画像を保存する
        /// </summary>
        private void SaveImageByExtension(Bitmap image, string outputFilePath)
        {
            string ext = Path.GetExtension(outputFilePath).ToLowerInvariant();
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    image.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                    break;
                case ".png":
                    image.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Png);
                    break;
                case ".bmp":
                    image.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Bmp);
                    break;
                case ".gif":
                    image.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Gif);
                    break;
                case ".tiff":
                case ".tif":
                    image.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Tiff);
                    break;
                default:
                    // デフォルトはPNG形式
                    image.Save(outputFilePath, System.Drawing.Imaging.ImageFormat.Png);
                    break;
            }
        }

        private async Task AutoDetectAndCompositeAsync()
        {
            if (currentImage == null || string.IsNullOrEmpty(selectedImagePath))
            {
                return;
            }

            try
            {
                // 円と矩形を同時に検出
                var (circles, rectangles) = await Task.Run(() =>
                         {
                             var detectedCircles = DetectCircles(selectedImagePath);
                             var detectedRectangles = DetectRectangles(selectedImagePath);
                             return (detectedCircles, detectedRectangles);
                         });

                // 切り抜いた画像を合成（円1個、矩形1個）
                var compositeImage = await Task.Run(() => CreateCroppedCompositeImage(selectedImagePath, circles, rectangles));

                if (compositeImage != null)
                {
                    this.Invoke(() =>
                             {
                                 detectedCircleImage?.Dispose();
                                 detectedCircleImage = null;
                                 detectedCompositeImage?.Dispose();
                                 detectedCompositeImage = compositeImage;
                                 rightBottomPanel?.Invalidate();
                             });
                }
                else
                {
                    // 検出結果がない場合はクリア
                    this.Invoke(() =>
               {
                   detectedCircleImage?.Dispose();
                   detectedCircleImage = null;
                   detectedCompositeImage?.Dispose();
                   detectedCompositeImage = null;
                   rightBottomPanel?.Invalidate();
               });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutoDetectAndCompositeAsync error: {ex}");
            }
        }

        private Bitmap? CreateCroppedCompositeImage(string imagePath, CircleSegment[] circles, OpenCvSharp.Rect[] rectangles)
        {
            try
            {
                using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (src.Empty())
                {
                    return null;
                }

                var croppedImages = new System.Collections.Generic.List<Mat>();

                // 円を切り抜く（1個のみ）
                if (circles != null && circles.Length > 0)
                {
                    var croppedCircle = CropCircleToMat(src, circles[0]);
                    if (croppedCircle != null)
                    {
                        croppedImages.Add(croppedCircle);
                    }
                }

                // 矩形を切り抜く（1個のみ）
                if (rectangles != null && rectangles.Length > 0)
                {
                    var croppedRect = CropRectangleToMat(src, rectangles[0]);
                    if (croppedRect != null)
                    {
                        croppedImages.Add(croppedRect);
                    }
                }

                if (croppedImages.Count == 0)
                {
                    return null;
                }

                // 画像を横に並べて合成（ラベルなし）
                var result = CombineImagesHorizontally(croppedImages);

                // Matを解放
                foreach (var mat in croppedImages)
                {
                    mat.Dispose();
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateCroppedCompositeImage error: {ex}");
                return null;
            }
        }

        private Mat? CropCircleToMat(Mat src, CircleSegment circle)
        {
            try
            {
                int x = (int)circle.Center.X;
                int y = (int)circle.Center.Y;
                int radius = (int)circle.Radius;

                // 画像境界内に収める
                int left = Math.Max(0, x - radius);
                int top = Math.Max(0, y - radius);
                int width = Math.Min(src.Width - left, radius * 2);
                int height = Math.Min(src.Height - top, radius * 2);

                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                // 円の領域を切り取る
                var rect = new OpenCvSharp.Rect(left, top, width, height);
                var cropped = new Mat(src, rect);

                // マスクを作成（円形部分のみを残す）
                using var mask = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
                var centerInCrop = new OpenCvSharp.Point(x - left, y - top);
                Cv2.Circle(mask, centerInCrop, radius, Scalar.White, -1);

                // マスクを適用
                var result = new Mat();
                cropped.CopyTo(result, mask);
                cropped.Dispose();

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CropCircleToMat error: {ex}");
                return null;
            }
        }

        private Mat? CropRectangleToMat(Mat src, OpenCvSharp.Rect rectangle)
        {
            try
            {
                // 画像境界内に収める
                int left = Math.Max(0, rectangle.X);
                int top = Math.Max(0, rectangle.Y);
                int width = Math.Min(src.Width - left, rectangle.Width);
                int height = Math.Min(src.Height - top, rectangle.Height);

                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                // 矩形の領域を切り取る
                var rect = new OpenCvSharp.Rect(left, top, width, height);
                return new Mat(src, rect);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CropRectangleToMat error: {ex}");
                return null;
            }
        }

        private Bitmap CombineImagesHorizontally(System.Collections.Generic.List<Mat> images)
        {
            if (images.Count == 0)
            {
                throw new ArgumentException("画像リストが空です。");
            }

            // 最大の高さを取得
            int maxHeight = images.Max(img => img.Height);
            int totalWidth = images.Sum(img => img.Width);
            int padding = 10; // 画像間の余白

            // 合成画像を作成
            var combined = new Mat(maxHeight, totalWidth + (images.Count - 1) * padding, MatType.CV_8UC3, Scalar.White);

            int currentX = 0;
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];

                // 画像を中央揃えで配置
                int yOffset = (maxHeight - img.Height) / 2;
                var roi = new OpenCvSharp.Rect(currentX, yOffset, img.Width, img.Height);
                var roiMat = new Mat(combined, roi);
                img.CopyTo(roiMat);

                currentX += img.Width + padding;
            }

            return BitmapConverter.ToBitmap(combined);
        }

        // Helper: send image to AI, store to currentAiResult and compute judge using detectionMode
        private async Task<string> AnalyzeImageWithAiAsync(string imageFilePath)
        {
            if (string.IsNullOrEmpty(imageFilePath)) return "Unknown";

            try
            {
                // 共有クライアントを使用（usingを削除してソケット枯渇を防止）
                var aiClient = GetOrCreateSharedAiClient();

                if (aiClient == null)
                {
                    Debug.WriteLine("AI判定APIの設定が不完全です。");
                    return "Unknown";
                }

                var aiResult = await aiClient.PredictImageAsync(imageFilePath).ConfigureAwait(false);
                currentAiResult = aiResult;

                // use detectionMode from settings
                string judge = aiResult?.Judge(detectionMode) ?? "Unknown";

                return judge;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AnalyzeImageWithAiAsync error: {ex.Message}");
                return "Unknown";
            }
        }
    }
}
