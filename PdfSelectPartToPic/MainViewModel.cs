﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Apitron.PDF.Rasterizer;
using Apitron.PDF.Rasterizer.Configuration;
using Microsoft.Win32;
using PdfSelectPartToPic.MVVM;
using Rectangle = System.Drawing.Rectangle;
using RenderMode = Apitron.PDF.Rasterizer.Configuration.RenderMode;
using Size = System.Drawing.Size;

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
            DataPicFilePath = new Dictionary<string, string>();
        }


        protected override void Poll()
        {
            lock (Lock)
            {
                var second = 0.00;
                if (_deviceTimer != null)
                {
                    second = _deviceTimer.GetElapseTime() / 1000;
                    PassedTime = $"{second/60:N0} min, {second%60:N0} secs";
                    if (_isRunningConvert)
                    {
                        if (second > 0)
                        {
                            var restNumber = _pdfFilePath.Capacity - _processNumber;
                            var needSecond = (second / _processNumber) * restNumber;
                            NeededTime = $"{needSecond/60:N0} min, {needSecond%60:N0} secs";
                        }
                    }
                    else if (_isRunningPicCorp)
                    {
                        if (second > 0)
                        {
                            var restNumber = _picFilePath.Capacity - _processNumber;
                            var needSecond = (_processNumber / second) * restNumber;
                            NeededTime = $"{needSecond/60:N0} min, {needSecond%60:N0} secs";
                        }
                    }

                }
            }
        }

        #region Properties

        public ICommand MainCommand { get; }
        public ICommand ToPicCommand { get; }
        public ICommand CutPicCommand { get; }

        public int PageNumber { get; set; }
        public int CutLength { get; set; } = 420;

        public int StartCutLength { get; set; } = 80;

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
        
        public int CombineFileCount
        {
            get => _combineFileCount;
            set
            {
                _combineFileCount = value;
                InvokePropertyChanged(nameof(CombineFileCount));
            }
        }

        public List<string> PdfFilePath
        {
            get => _pdfFilePath;
            set
            {
                _pdfFilePath = value;
                InvokePropertyChanged(nameof(PdfFilePath));
            }
        }

        public List<string> PicFilePath
        {
            get => _picFilePath;
            set
            {
                _picFilePath = value;
                InvokePropertyChanged(nameof(PicFilePath));
            }
        }        
        public Dictionary<string,string> DataPicFilePath
        {
            get => _dataPicFilePath;
            set
            {
                _dataPicFilePath = value;
                InvokePropertyChanged(nameof(DataPicFilePath));
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

        public string PassedTime
        {
            get => _passedTime;
            set
            {
                _passedTime = value;
                InvokePropertyChanged(nameof(PassedTime));
            }
        }
        
        public string NeededTime
        {
            get => _neededTime;
            set
            {
                _neededTime = value;
                InvokePropertyChanged(nameof(NeededTime));
            }
        }

        #endregion

        #region Fields

        private static readonly object Lock = new object();
        private int _fileCount;
        private int _combineFileCount;
        private int _processNumber;
        private List<string> _pdfFilePath;
        private List<string> _picFilePath;
        private Dictionary<string,string> _dataPicFilePath;
        private string _displayImagePath;
        private DeviceTimer _deviceTimer;
        private string _passedTime;
        private string _neededTime;
        private bool _isRunningConvert;
        private bool _isRunningPicCorp;
        private bool _isOneKey;
        
        
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
                case "LoadCombinePic":
                    LoadCombinePicture();
                    break;
                case "StartProcessing":
                    ProcessNumber = 0;
                    PicProcess();
                    break;
                case "StartCombine":
                    ProcessNumber = 0;
                    CombineProcess();
                    break;
                case "OneKey":
                    ProcessNumber = 0;
                    ConvertToPic();
                    _isOneKey = true;
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
            lock (Lock)
            {
                var imageOutputPath = Path.GetDirectoryName(_pdfFilePath.FirstOrDefault()) + @"\Pictures";
                if (!Directory.Exists(imageOutputPath)) Directory.CreateDirectory(imageOutputPath);
                _deviceTimer = new DeviceTimer();
                _isRunningConvert = true;
                Task.Run(() =>
                {
                    Parallel.ForEach(_pdfFilePath, path=>
                    {
                        var obj = new object[] {path, imageOutputPath};
                        Save(obj);
                    });
                }).ContinueWith(task =>
                {
                    if (_isOneKey)
                    {
                        ProcessNumber = 0;
                        PicProcess();
                    }
                    else
                    {
                        _deviceTimer = null;
                        _isRunningConvert = false;
                        NeededTime = "0 min, 0 secs";
                        MessageBox.Show("干完咯弟弟");
                    }
                });
            }
        }
        private void Save(object obj)
        {
            var path = ((object[]) obj)[0].ToString();
            var imageOutputPath = ((object[]) obj)[1];
            var imageName = Path.GetFileName(path).Replace(".pdf", "");
            
            if (PageNumber <= 0) PageNumber = 0;

            using (var fs = new FileStream(path, FileMode.Open))
            {
                // this object represents a PDF document
                var document = new Document(fs);            
                if (PageNumber > document.Pages.Count)
                    PageNumber = document.Pages.Count;
                // process and save pages one by one
                var currentPage = document.Pages[PageNumber];
                // we use original page's width and height for image as well as default rendering settings
                var setting = new RenderingSettings {RenderMode = RenderMode.HighSpeed};
                using (var bitmap = currentPage.Render(3*(int)currentPage.Width, 3*(int)currentPage.Height, setting))
                {
                    bitmap.Save($"{imageOutputPath}\\{imageName}.jpg", ImageFormat.Jpeg);
                    
                }
            }
            lock (Lock)
            {
                _picFilePath.Add( $"{imageOutputPath}\\{imageName}.jpg");
                ProcessNumber++;
            }
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
            _isRunningPicCorp = true;
            _deviceTimer = new DeviceTimer();
            
            var imageOutputPath = Path.GetDirectoryName(_picFilePath.FirstOrDefault())
                ?.Replace(@"\Pictures", @"\CutResult");
            if (!Directory.Exists(imageOutputPath))
                if (imageOutputPath != null)
                    Directory.CreateDirectory(imageOutputPath);

            Task.Run(() =>
            {
                Parallel.ForEach(_picFilePath, CutPic);
            }).ContinueWith(task =>
            {
                
                if (_isOneKey)
                {
                    ProcessNumber = 0;
                    CombineProcess();
                }
                else
                {
                    _deviceTimer = null;
                    _isRunningPicCorp = false;
                    NeededTime = "0 min, 0 secs";
                    MessageBox.Show("干完咯弟弟");
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
            lock (Lock)
                ProcessNumber++;
            
        }

        private void LoadCombinePicture()
        {
            _dataPicFilePath.Clear();
            CombineFileCount = 0;
            var thisDialog = new OpenFileDialog
            {
                InitialDirectory = "d:\\",
                Filter = "png files (*.png)|*.png",
                FilterIndex = 2,
                RestoreDirectory = true,
                Multiselect = true,
                Title = "请选择需要转换的PNG文件!"
            };
            if (thisDialog.ShowDialog() == true)
                foreach (var file in thisDialog.FileNames)
                    try
                    {
                        _dataPicFilePath.Add(Path.GetFileNameWithoutExtension(file),file);
                    }

                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                    }

            if (FileCount == _dataPicFilePath.Count)
            {
                CombineFileCount = _dataPicFilePath.Count;
            }
            else
            {
                MessageBox.Show("图片数量与选定的PDF文件或剪裁后的JPG文件数量不一致!");
            }
            
        }
        
        private void CombineProcess()
        {
            ProcessNumber = 0;
            _isRunningPicCorp = true;
            _deviceTimer = new DeviceTimer();
            var imageOutputPath = Path.GetDirectoryName(_picFilePath.FirstOrDefault())
                ?.Replace(@"\Pictures", @"\CombineResult");
            if (!Directory.Exists(imageOutputPath))
                if (imageOutputPath != null)
                    Directory.CreateDirectory(imageOutputPath);

            Task.Run(() =>
            {
                Parallel.ForEach(_picFilePath, Combine);
            }).ContinueWith(task =>
            {
                _deviceTimer = null;
                _isRunningPicCorp = false;
                NeededTime = "0 min, 0 secs";
                MessageBox.Show("干完咯弟弟");
            });
        }

        private void Combine(object path)
        {
            var imageName = Path.GetFileNameWithoutExtension(path.ToString());
            if (_dataPicFilePath.ContainsKey(imageName))
            {
                var dataBitmap = new Bitmap(_dataPicFilePath[imageName]);
                var srcBitmap = new Bitmap(path.ToString().Replace(@"\Pictures", @"\CutResult"));
                dataBitmap = new Bitmap(dataBitmap,new Size(1579,245));
                var outputBitmap = new Bitmap(srcBitmap.Width,srcBitmap.Height);
                using (Graphics g = Graphics.FromImage(outputBitmap))
                {
                    g.DrawImage(srcBitmap, 0, 0);
                    g.DrawImage(dataBitmap, 120, 155);
                }
                outputBitmap.Save(path.ToString().Replace(@"\Pictures", @"\CombineResult"));
            }
        }
        #endregion
    }
}