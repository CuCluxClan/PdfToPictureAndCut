using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using O2S.Components.PDFRender4NET.WPF;
using PdfSelectPartToPic.MVVM;

namespace PdfSelectPartToPic
{
    public class MainViewModel : TimerViewModelBase
    {
        public MainViewModel() : base("MainViewModel")
        {
            _pdfFilePath = new List<string>();
            _picFilePath = new List<string>();
            FileCount = 0;
            MainCommand = new DelegateCommand<string>(Execute);
            ToPicCommand = new DelegateCommand<string>(Execute);
            CutPicCommand = new DelegateCommand<string>(Execute);
        }


        protected override void Poll()
        {
        }

        #region Properties

        public ICommand MainCommand { get; }
        public ICommand ToPicCommand { get; }
        public ICommand CutPicCommand { get; }

        public int PageNumber { get; set; }
        public int CutLength { get; set; } = 350;

        public int StartCutLength { get; set; } = 350;

        public int ThreadNumber
        {
            get => _threadNumber;
            set
            {
                _threadNumber = value;
                InvokePropertyChanged(nameof(ThreadNumber));
            }
        }

        public int ProcessNumber
        {
            get => _processNumber;
            set
            {
                _processNumber = value;
                InvokePropertyChanged(nameof(ProcessNumber));
            }
        }

        public int FileCount
        {
            get => _fileCount;
            set
            {
                _fileCount = value;
                InvokePropertyChanged(nameof(FileCount));
            }
        }

        public List<string> PdfFilePath
        {
            get => _pdfFilePath;
            set
            {
                _pdfFilePath = value;
                InvokePropertyChanged(nameof(ToPicCommand));
                InvokePropertyChanged(nameof(PdfFilePath));
            }
        }

        public List<string> PicFilePath
        {
            get => _picFilePath;
            set
            {
                _picFilePath = value;
                InvokePropertyChanged(nameof(CutPicCommand));
                InvokePropertyChanged(nameof(PicFilePath));
            }
        }

        public string DisplayedImagePath
        {
            get => _displayImagePath;
            set
            {
                _displayImagePath = value;
                InvokePropertyChanged(nameof(DisplayedImagePath));
            }
        }

        #endregion

        #region Fields

        private static readonly object Lock = new object();
        private int _fileCount;
        private int _processNumber;
        private int _threadNumber;
        private List<string> _pdfFilePath;
        private List<string> _picFilePath;
        private string _displayImagePath;
        
        #endregion
        
        #region Methods

        private void Execute(string obj)
        {
            switch (obj)
            {
                case "ReadFile":
                    ReadFile();
                    break;
                case "ToPic":
                    ProcessNumber = 0;
                    ConvertToPic();
                    break;
                case "ReadPic":
                    ReadPic();
                    break;
                case "StartProcessing":
                    PicProcess();
                    break;
            }
        }

        private void ReadFile()
        {
            _pdfFilePath.Clear();
            FileCount = 0;
            var thisDialog = new OpenFileDialog
            {
                InitialDirectory = "d:\\",
                Filter = "pdf files (*.pdf)|*.pdf",
                FilterIndex = 2,
                RestoreDirectory = true,
                Multiselect = true,
                Title = "请选择需要转换的PDF文件!"
            };
            if (thisDialog.ShowDialog() == true)
                foreach (var file in thisDialog.FileNames)
                    try
                    {
                        _pdfFilePath.Add(file);
                    }

                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                    }

            FileCount = _pdfFilePath.Count;
        }

        private void ConvertToPic()
        {
            ThreadNumber = 0;
            lock (Lock)
            {
                var imageOutputPath = Path.GetDirectoryName(_pdfFilePath.FirstOrDefault()) + @"\Pictures";
                if (!Directory.Exists(imageOutputPath)) Directory.CreateDirectory(imageOutputPath);
                
                Task.Run(() =>
                {
                    for (var i = 0; i < _pdfFilePath.Count; i++)
                    {
                        if (ThreadNumber > 99)
                        {
                            i--;
                            continue;
                        }
                        var obj = new object[] {_pdfFilePath[i], imageOutputPath};
                        var t = new Thread(Save);
                        t.SetApartmentState(ApartmentState.STA);
                        t.Start(obj);
                        ThreadNumber++;
                    }
                });
            }
        }
        private void Save(object obj)
        {
            var path = ((object[]) obj)[0].ToString();
            var imageOutputPath = ((object[]) obj)[1];
            var imageName = Path.GetFileName(path).Replace(".pdf", "");
            var pdfFile = PDFFile.Open(path);
            if (PageNumber <= 0) PageNumber = 0;

            if (PageNumber > pdfFile.PageCount) PageNumber = pdfFile.PageCount;
            var pageImage = pdfFile.GetPageImage(PageNumber, 300, 300, PDFOutputImageFormat.BMP);
            var bmp = new Bitmap(pageImage);
            var qualityEncoder = Encoder.Quality;
            var quality = (long) 500;
            var ratio = new EncoderParameter(qualityEncoder, quality);
            var codecParams = new EncoderParameters(1) {Param = {[0] = ratio}};
            var jpegCodecInfo = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(x => x.FormatID == ImageFormat.Jpeg.Guid);
            if (jpegCodecInfo != null)
                bmp.Save($"{imageOutputPath}\\{imageName}.jpg", jpegCodecInfo, codecParams); // Save to JPG
            _picFilePath.Add($"{imageOutputPath}\\{imageName}.jpg");
            bmp.Dispose();
            ProcessNumber++;
            // GC.Collect();
            // GC.WaitForPendingFinalizers();
            // GC.Collect();
            ThreadNumber--;
            Thread.CurrentThread.Abort();
        }

        private void ReadPic()
        {
            var thisDialog = new OpenFileDialog
            {
                InitialDirectory = "d:\\",
                Filter = "jpg files (*.jpg)|*.jpg",
                FilterIndex = 2,
                RestoreDirectory = true,
                Multiselect = false,
                Title = "请选择任意一个转换出来的BMP图像!"
            };
            if (thisDialog.ShowDialog() == true)
            {
                DisplayedImagePath = thisDialog.FileNames.FirstOrDefault();
                var imgPath = Path.GetDirectoryName(DisplayedImagePath);
                if (imgPath != null)
                    foreach (var file in Directory.GetFiles(imgPath))
                        _picFilePath.Add(file);
            }
        }

        private void PicProcess()
        {
            ProcessNumber = 0;
            var imageOutputPath = Path.GetDirectoryName(_picFilePath.FirstOrDefault())
                ?.Replace(@"\Pictures", @"\CutResult");
            if (!Directory.Exists(imageOutputPath))
                if (imageOutputPath != null)
                    Directory.CreateDirectory(imageOutputPath);

            Task.Run(() =>
            {
                for (var i = 0; i < _picFilePath.Count; i++)
                {
                    if (ThreadNumber > 99)
                    {
                        i--;
                        continue;
                    }
                    var t = new Thread(CutPic);
                    t.SetApartmentState(ApartmentState.STA);
                    t.Start(_picFilePath[i]);
                    ThreadNumber++;
                }
            });
        }

        private void CutPic(object obj)
        {
            //图片路径
            var picPath = obj.ToString();
            var oldPath = picPath;
            var newPath = oldPath.Replace(@"\Pictures", @"\CutResult");
            var img = Image.FromStream(new MemoryStream(File.ReadAllBytes(oldPath)));
            //定义截取矩形
            var cropArea = new Rectangle(0, StartCutLength, img.Width, CutLength);
            //判断超出的位置否
            if (CutLength > img.Height) CutLength = img.Height;
            //定义Bitmap对象
            var bmpImage = new Bitmap(img);
            //进行裁剪
            var bmpCrop = bmpImage.Clone(cropArea, bmpImage.PixelFormat);
            var qualityEncoder = Encoder.Quality;
            var quality = (long) 500;
            var ratio = new EncoderParameter(qualityEncoder, quality);
            var codecParams = new EncoderParameters(1) {Param = {[0] = ratio}};
            var jpegCodecInfo = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(x => x.FormatID == ImageFormat.Jpeg.Guid);
            //保存成新文件
            if (jpegCodecInfo != null)
                bmpCrop.Save(newPath, jpegCodecInfo, codecParams); // Save to JPG
            //释放对象
            img.Dispose();
            bmpCrop.Dispose();
            ProcessNumber++;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            ThreadNumber--;
            Thread.CurrentThread.Abort();
        }

        #endregion
    }
}