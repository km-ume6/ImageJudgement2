using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Diagnostics;

namespace ImageJudgement2
{
    /// <summary>
    /// 長方形検出関連の処理
    /// </summary>
    public partial class Form1
    {
        private async Task DetectAndSaveRectanglesAsync()
        {
            if (currentImage == null || string.IsNullOrEmpty(selectedImagePath))
            {
                MessageBox.Show("画像が選択されていません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                //btnDetectRectangles.Enabled = false;
                //btnDetectRectangles.Text = "処理中...";

                var rectangles = await Task.Run(() => DetectRectangles(selectedImagePath));

                if (rectangles == null || rectangles.Length == 0)
                {
                    MessageBox.Show("長方形が検出されませんでした。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 最初に検出された長方形を右下パネルに表示
                var rectangle = rectangles[0];
                var croppedImage = await Task.Run(() => CropRectangleFromImage(selectedImagePath, rectangle));

                if (croppedImage != null)
                {
                    // UIスレッドで画像を更新
                    this.Invoke(() =>
                        {
                            detectedCircleImage?.Dispose();
                            detectedCircleImage = croppedImage;
                            rightBottomPanel?.Invalidate();
                        });

                    MessageBox.Show($"長方形領域を検出しました（{rectangles.Length}個中1個目を表示）", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("長方形領域の切り取りに失敗しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"長方形検出処理中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Debug.WriteLine($"DetectAndSaveRectanglesAsync error: {ex}");
            }
            finally
            {
                //btnDetectRectangles.Enabled = true;
                //btnDetectRectangles.Text = "長方形検出";
            }
        }

        // OpenCVで長方形を検出
        private OpenCvSharp.Rect[] DetectRectangles(string imagePath)
        {
            using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (src.Empty())
            {
                return [];
            }

            // グレースケール変換
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // ノイズ除去のためガウシアンブラー適用
            using var blurred = new Mat();
            Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);

            // UIからパラメータを取得
            int minArea = 1000;
            int maxArea = 0;
            int cannyThreshold1 = 50;
            int cannyThreshold2 = 150;

            if (nudMinArea?.InvokeRequired == true)
            {
                nudMinArea.Invoke(() =>
     {
         minArea = (int)nudMinArea.Value;
         maxArea = (int)nudMaxArea.Value;
         cannyThreshold1 = (int)nudCannyThreshold1.Value;
         cannyThreshold2 = (int)nudCannyThreshold2.Value;
     });
            }
            else if (nudMinArea != null)
            {
                minArea = (int)nudMinArea.Value;
                maxArea = (int)nudMaxArea.Value;
                cannyThreshold1 = (int)nudCannyThreshold1.Value;
                cannyThreshold2 = (int)nudCannyThreshold2.Value;
            }

            // Canny エッジ検出
            using var edges = new Mat();
            Cv2.Canny(blurred, edges, cannyThreshold1, cannyThreshold2);

            // 輪郭検出
            Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            var rectangles = new System.Collections.Generic.List<OpenCvSharp.Rect>();

            foreach (var contour in contours)
            {
                // 輪郭を近似
                var epsilon = 0.02 * Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, epsilon, true);

                // 4つの頂点を持つ（四角形）かつ、凸図形であるかチェック
                if (approx.Length == 4 && Cv2.IsContourConvex(approx))
                {
                    var rect = Cv2.BoundingRect(approx);
                    double area = rect.Width * rect.Height;

                    // 面積フィルタリング
                    if (area >= minArea && (maxArea == 0 || area <= maxArea))
                    {
                        rectangles.Add(rect);
                    }
                }
            }

            // 面積の大きい順にソート
            return rectangles.OrderByDescending(r => r.Width * r.Height).ToArray();
        }

        // 検出された長方形の部分を切り取る
        private Bitmap? CropRectangleFromImage(string imagePath, OpenCvSharp.Rect rectangle)
        {
            try
            {
                using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (src.Empty())
                {
                    return null;
                }

                // 画像境界内に収める
                int left = Math.Max(0, rectangle.X);
                int top = Math.Max(0, rectangle.Y);
                int width = Math.Min(src.Width - left, rectangle.Width);
                int height = Math.Min(src.Height - top, rectangle.Height);

                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                // 長方形の領域を切り取る
                var rect = new OpenCvSharp.Rect(left, top, width, height);
                using var cropped = new Mat(src, rect);

                // MatからBitmapに変換
                return BitmapConverter.ToBitmap(cropped);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CropRectangleFromImage error: {ex}");
                return null;
            }
        }
    }
}
