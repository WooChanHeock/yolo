using Alturos.Yolo.Model;
using Alturos.Yolo.TestUI.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alturos.Yolo.TestUI
{
    public partial class Main : Form
    {

        private YoloWrapper _yoloWrapper;
        private String filename;
        private String filepath;

        public Main()
        {
            this.InitializeComponent();

            this.buttonProcessImage.Enabled = false;
            this.buttonStartTracking.Enabled = false;

            this.menuStrip1.Visible = false;

            this.toolStripStatusLabelYoloInfo.Text = string.Empty;

            this.Text = $"Alturos Yolo TestUI {Application.ProductVersion}";
            this.dataGridViewFiles.AutoGenerateColumns = false;
            this.dataGridViewResult.AutoGenerateColumns = false;

            var imageInfos = new DirectoryImageReader().Analyze(@".\Images");
            //이미지의 정보를 저장할 리스트
            List<ImageInfo> list = imageInfos.ToList();
            //임시로 이미지를 저장할 리스트
            List<ImageInfo> list1 = imageInfos.ToList();
            DirectoryInfo Info = new DirectoryInfo(@".\Images");

            //1차 파일
            if (Info.Exists)
            {
                DirectoryInfo[] CInfo = Info.GetDirectories();

                foreach (DirectoryInfo info in CInfo)
                {
                    imageInfos = new DirectoryImageReader().Analyze(@".\Images\" + info.Name);
                    list1 = imageInfos.ToList();
                    list.AddRange(list1);
                    DirectoryInfo Info1 = new DirectoryInfo(@".\Images\" + info.Name);

                    //2차 파일
                    if (Info1.Exists)
                    {
                        CInfo = Info1.GetDirectories();

                        foreach (DirectoryInfo info1 in CInfo)
                        {
                            imageInfos = new DirectoryImageReader().Analyze(@".\Images\" + info.Name +"\\" + info1.Name);
                            list1 = imageInfos.ToList();
                            list.AddRange(list1);


                        }
                    }
                }
            }
            this.dataGridViewFiles.DataSource = list;





            Task.Run(() => this.Initialize("."));
            this.LoadAvailableConfigurations();
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            this._yoloWrapper?.Dispose();
        }

        private void LoadAvailableConfigurations()
        {
            var configPath = "config";

            if (!Directory.Exists(configPath))
            {
                return;
            }

            var configs = Directory.GetDirectories(configPath);
            if (configs.Length == 0)
            {
                return;
            }

            this.menuStrip1.Visible = true;

            foreach (var config in configs)
            {
                var menuItem = new ToolStripMenuItem();
                menuItem.Text = config;
                menuItem.Click += (object sender, EventArgs e) => { this.Initialize(config); };
                this.configurationToolStripMenuItem.DropDownItems.Add(menuItem);
            }
        }

        private ImageInfo GetCurrentImage()
        {
            var item = this.dataGridViewFiles.CurrentRow?.DataBoundItem as ImageInfo;
            return item;
        }

        private void dataGridViewFiles_SelectionChanged(object sender, EventArgs e)
        {
            var oldImage = this.pictureBox1.Image;
            var imageInfo = this.GetCurrentImage();
            this.pictureBox1.Image = Image.FromFile(imageInfo.Path);
            oldImage?.Dispose();

            this.dataGridViewResult.DataSource = null;
            this.groupBoxResult.Text = $"Result";
        }

        private void dataGridViewFiles_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                this.DetectSelectedImage();
            }
        }

        private void dataGridViewResult_SelectionChanged(object sender, EventArgs e)
        {
            if (!this.dataGridViewResult.Focused)
            {
                return;
            }

            var items = this.dataGridViewResult.DataSource as List<YoloItem>;
            var selectedItem = this.dataGridViewResult.CurrentRow?.DataBoundItem as YoloItem;
            this.DrawBoundingBoxes(items, selectedItem);
        }

        private void openFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialogResult = this.folderBrowserDialog1.ShowDialog();
            if (dialogResult != DialogResult.OK)
            {
                return;
            }

            var imageInfos = new DirectoryImageReader().Analyze(this.folderBrowserDialog1.SelectedPath);
            this.dataGridViewFiles.DataSource = imageInfos.ToList();
        }

        private void buttonProcessImage_Click(object sender, EventArgs e)
        {
            this.DetectSelectedImage();
        }

        private async void buttonStartTracking_Click(object sender, EventArgs e)
        {
            await this.StartTrackingAsync();
        }

        private async Task StartTrackingAsync()
        {
            this.buttonStartTracking.Enabled = false;

            var imageInfo = this.GetCurrentImage();

            var yoloTracking = new YoloTracking(imageInfo.Width, imageInfo.Height);
            var count = this.dataGridViewFiles.RowCount;
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    this.dataGridViewFiles.Rows[i - 1].Selected = false;
                }

                this.dataGridViewFiles.Rows[i].Selected = true;
                this.dataGridViewFiles.CurrentCell = this.dataGridViewFiles.Rows[i].Cells[0];

                var items = this.Detect();

                String line;
                String basicline;
                if (items.Any())
                {

                    basicline = filename + "," + filepath;
                    var result = from p in items select new { p.Type, p.Confidence, p.X, p.Y, p.Width, p.Height };
                    foreach (var p in result)
                    {
                        if ((p.Type.Equals("Text")&p.Confidence>=0.5)|(p.Type.Equals("Person")&p.Confidence >= 0.3)|(p.Type.Equals("Board")&p.Confidence >= 0.8)) {
                            line = basicline + "," + p.Type + "," + p.Confidence + "," + p.X + "," + p.Y + "," + p.Width + "," + p.Height;
                            using (StreamWriter outputFile = new StreamWriter(@"..\..\Black.txt", true))
                            {
                                outputFile.WriteLine(line);
                            }

                        }
                        
                    }
                    
                }

                var trackingItems = yoloTracking.Analyse(items);
                this.DrawBoundingBoxes(trackingItems);

                await Task.Delay(100);
            }

            this.buttonStartTracking.Enabled = true;
        }

        private void DrawBoundingBoxes(IEnumerable<YoloTrackingItem> items)
        {
            var imageInfo = this.GetCurrentImage();
            //Load the image(probably from your stream)
            var image = Image.FromFile(imageInfo.Path);

            using (var font = new Font(FontFamily.GenericSansSerif, 16))
            using (var canvas = Graphics.FromImage(image))
            {
                foreach (var item in items)
                {
                    var x = item.X;
                    var y = item.Y;
                    var width = item.Width;
                    var height = item.Height;

                    var brush = this.GetBrush(item.Confidence);
                    var penSize = image.Width / 100.0f;

                    using (var pen = new Pen(brush, penSize))
                    {
                        canvas.DrawRectangle(pen, x, y, width, height);
                        canvas.FillRectangle(brush, x - (penSize / 2), y - 15, width + penSize, 25);
                    }
                }

                foreach (var item in items)
                {
                    canvas.DrawString(item.ObjectId.ToString(), font, Brushes.White, item.X, item.Y - 12);
                }

                canvas.Flush();
            }

            var oldImage = this.pictureBox1.Image;
            this.pictureBox1.Image = image;
            oldImage?.Dispose();
        }

        private void DrawBoundingBoxes(List<YoloItem> items, YoloItem selectedItem = null)
        {
            var imageInfo = this.GetCurrentImage();
            //Load the image(probably from your stream)
            var image = Image.FromFile(imageInfo.Path);

            using (var canvas = Graphics.FromImage(image))
            {
                foreach (var item in items)
                {
                    var x = item.X;
                    var y = item.Y;
                    var width = item.Width;
                    var height = item.Height;

                    var brush = this.GetBrush(item.Confidence);
                    var penSize = image.Width / 100.0f;

                    using (var pen = new Pen(brush, penSize))
                    using (var overlayBrush = new SolidBrush(Color.FromArgb(150, 255, 255, 102)))
                    {
                        if (item.Equals(selectedItem))
                        {
                            canvas.FillRectangle(overlayBrush, x, y, width, height);
                        }

                        canvas.DrawRectangle(pen, x, y, width, height);
                    }
                }

                canvas.Flush();
            }

            var oldImage = this.pictureBox1.Image;
            this.pictureBox1.Image = image;
            oldImage?.Dispose();
        }

        private Brush GetBrush(double confidence)
        {
            if (confidence > 0.5)
            {
                return Brushes.GreenYellow;
            }
            else if (confidence > 0.2 && confidence <= 0.5)
            {
                return Brushes.Orange;
            }

            return Brushes.DarkRed;
        }

        private void Initialize(string path)
        {
            var configurationDetector = new YoloConfigurationDetector();
            try
            {
                var config = configurationDetector.Detect(path);
                if (config == null)
                {
                    return;
                }

                this.Initialize(config);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Cannot found a valid dataset {exception}", "No Dataset available", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }



        private void Initialize(YoloConfiguration config)
        {
            try
            {
                if (this._yoloWrapper != null)
                {
                    this._yoloWrapper.Dispose();
                }

                var gpuConfig = new GpuConfig();


                Console.WriteLine(gpuConfig.GpuIndex);


                this.toolStripStatusLabelYoloInfo.Text = $"Initialize...";

                var sw = new Stopwatch();
                sw.Start();
                this._yoloWrapper = new YoloWrapper(config.ConfigFile, config.WeightsFile, config.NamesFile, gpuConfig);
                sw.Stop();

                var action = new MethodInvoker(delegate ()
                {
                    var deviceName = this._yoloWrapper.GetGraphicDeviceName(gpuConfig);
                    this.toolStripStatusLabelYoloInfo.Text = $"Initialize Yolo in {sw.Elapsed.TotalMilliseconds:0} ms - Detection System:{this._yoloWrapper.DetectionSystem} {deviceName} Weights:{config.WeightsFile}";
                });

                this.statusStrip1.Invoke(action);
                this.buttonProcessImage.Invoke(new MethodInvoker(delegate () { this.buttonProcessImage.Enabled = true; }));
                this.buttonStartTracking.Invoke(new MethodInvoker(delegate () { this.buttonStartTracking.Enabled = true; }));
            }
            catch (Exception exception)
            {
                MessageBox.Show($"{nameof(Initialize)} - {exception}", "Error Initialize", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DetectSelectedImage()
        {
            var items = this.Detect();
            String line;
            this.dataGridViewResult.DataSource = items;
            this.DrawBoundingBoxes(items);
            if (items.Any())
            {
                Form1 dlg = new Form1();
                dlg.Show();
                line = filepath;
                var result = from p in items select new { p.Type, p.Confidence, p.X, p.Y, p.Width, p.Height };
                foreach (var p in result)
                {
                    line = line + ", " + p.Type + ", " + p.Confidence + ", " + p.X + ", " + p.Y + ", " + p.Width + ", " + p.Height;
                }
                using (StreamWriter outputFile = new StreamWriter(@"..\..\New_TEXT_File.txt", true))
                {
                    outputFile.WriteLine(line);
                }
            }
            // White 출력
            /*else {
                line = filename + ".White";
                using (StreamWriter outputFile = new StreamWriter(@"..\..\New_TEXT_File.txt", true))
                {
                    outputFile.WriteLine(line);
                }
            }*/
        }

        private List<YoloItem> Detect(bool memoryTransfer = true)
        {
            if (this._yoloWrapper == null)
            {
                return null;
            }

            var imageInfo = this.GetCurrentImage();
            filename = imageInfo.Name;
            filepath = imageInfo.Path;
            var imageData = File.ReadAllBytes(imageInfo.Path);

            var sw = new Stopwatch();
            sw.Start();
            List<YoloItem> items;
            if (memoryTransfer)
            {
                items = this._yoloWrapper.Detect(imageData).ToList();
            }
            else
            {
                items = this._yoloWrapper.Detect(imageInfo.Path).ToList();
            }
            sw.Stop();
            this.groupBoxResult.Text = $"Result [ processed in {sw.Elapsed.TotalMilliseconds:0} ms ]";

            return items;
        }

        private void gpuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.cpuToolStripMenuItem.Checked = !this.cpuToolStripMenuItem.Checked;
        }

        private async void downloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var repository = new YoloPreTrainedDatasetRepository();
            var datasets = await repository.GetDatasetsAsync();
            foreach (var dataset in datasets)
            {
                this.statusStrip1.Invoke(new MethodInvoker(delegate () { this.toolStripStatusLabelYoloInfo.Text = $"Start download for {dataset} dataset..."; }));
                await repository.DownloadDatasetAsync(dataset, $@"config\{dataset}");
            }

            this.LoadAvailableConfigurations();
            this.statusStrip1.Invoke(new MethodInvoker(delegate () { this.toolStripStatusLabelYoloInfo.Text = $"Download done"; }));
        }

        private void btnTake_Click(object sender, EventArgs e)
        {
            this.Hide();
            Thread.Sleep(500);
            pictureBox1.Image = ScreenShot.take();
            this.Show();
        }

        internal class API
        {
            [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
            public static extern IntPtr GetDC(IntPtr hWnd);

            [DllImport("user32.dll", ExactSpelling = true)]
            public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("gdi32.dll", ExactSpelling = true)]
            public static extern IntPtr BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

            [DllImport("user32.dll", EntryPoint = "GetDesktopWindow")]
            public static extern IntPtr GetDesktopWindow();
        }

        internal class ScreenShot
        {
            public static Bitmap take()
            {
                int screenWidth = Screen.PrimaryScreen.Bounds.Width;
                int screenHeight = Screen.PrimaryScreen.Bounds.Height;

                Bitmap screenBmp = new Bitmap(screenWidth, screenHeight);
                Graphics g = Graphics.FromImage(screenBmp);

                IntPtr dc1 = API.GetDC(API.GetDesktopWindow());
                IntPtr dc2 = g.GetHdc();

                //Main drawing, copies the screen to the bitmap
                API.BitBlt(dc2, 0, 0, screenWidth, screenHeight, dc1, 0, 0, 13369376); //last number is the copy constant

                //Clean up
                API.ReleaseDC(API.GetDesktopWindow(), dc1);
                g.ReleaseHdc(dc2);
                g.Dispose();

                return screenBmp;
            }

        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                pictureBox1.Image.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Bmp);
            }
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            var imageInfos = new DirectoryImageReader().Analyze(@".\Images");
            //이미지의 정보를 저장할 리스트
            List<ImageInfo> list = imageInfos.ToList();
            //임시로 이미지를 저장할 리스트
            List<ImageInfo> list1 = imageInfos.ToList();
            DirectoryInfo Info = new DirectoryInfo(@".\Images");

            //1차 파일
            if (Info.Exists)
            {
                DirectoryInfo[] CInfo = Info.GetDirectories();

                foreach (DirectoryInfo info in CInfo)
                {
                    imageInfos = new DirectoryImageReader().Analyze(@".\Images\" + info.Name);
                    list1 = imageInfos.ToList();
                    list.AddRange(list1);
                    DirectoryInfo Info1 = new DirectoryInfo(@".\Images\" + info.Name);

                    //2차 파일
                    if (Info1.Exists)
                    {
                        CInfo = Info1.GetDirectories();

                        foreach (DirectoryInfo info1 in CInfo)
                        {
                            imageInfos = new DirectoryImageReader().Analyze(@".\Images\" + info.Name + "\\" + info1.Name);
                            list1 = imageInfos.ToList();
                            list.AddRange(list1);


                        }
                    }
                }
            }
            this.dataGridViewFiles.DataSource = list;
            dataGridViewFiles.Update();
            dataGridViewFiles.Refresh();
        }

        private void btnOne_Click(object sender, EventArgs e)
        {
            string save_route = @"C:\Users\admin\Downloads\yolo-master\src\Alturos.Yolo.TestUI\bin\Debug\Images";

            this.Hide();
            Thread.Sleep(500);
            pictureBox1.Image = ScreenShot.take();
            pictureBox1.Image.Save(save_route + "\\1.png", System.Drawing.Imaging.ImageFormat.Png);
            this.Show();

            var imageInfos = new DirectoryImageReader().Analyze(@".\Images");
            this.dataGridViewFiles.DataSource = imageInfos.ToList();
            dataGridViewFiles.Update();
            dataGridViewFiles.Refresh();
            dataGridViewFiles.CurrentCell = dataGridViewFiles.Rows[0].Cells[0];
            this.DetectSelectedImage();


        }
    }
}
