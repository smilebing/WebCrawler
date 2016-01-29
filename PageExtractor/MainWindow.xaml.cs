using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Data.OleDb;

namespace PageExtractor
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Spider _spider = null;
        private delegate void CSHandler(string arg0, string arg1);
        private delegate void DFHandler(int arg1);
        static string exePath = System.Environment.CurrentDirectory;//本程序所在路径
        //数据库连接
        OleDbConnection conn = new OleDbConnection("provider=Microsoft.Jet.OLEDB.4.0;data source=" + exePath + @"\spider1.mdb");







        public MainWindow()
        {
            InitializeComponent();
            _spider = new Spider();
            _spider.ContentsSaved += new Spider.ContentsSavedHandler(Spider_ContentsSaved);
            _spider.DownloadFinish += new Spider.DownloadFinishHandler(Spider_DownloadFinish);
            this.Closed += new EventHandler(MainWindow_Closed);
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            btnStop.IsEnabled = false;
        }

        //窗口加载
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            conn.Open();
            TextUrl.Text = "http://www.guet.cn/ExtGuetWeb/News?stype=1";
        }
        
        void Spider_DownloadFinish(int count)
        {
            DFHandler h = c =>
            {
                _spider.Abort();
                btnDownload.IsEnabled = true;
                btnDownload.Content = "Download";
                btnStop.IsEnabled = false;
                MessageBox.Show("Finished " + c.ToString());
            };
            Dispatcher.Invoke(h, count);
        }

        void MainWindow_Closed(object sender, EventArgs e)
        {
            conn.Close();
            //关闭窗口，关闭爬虫
            _spider.Abort();
        }


        //download
        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            _spider.title1 = userDefine1a.Text;
            _spider.title2 = userDefine2a.Text;
            _spider.title3 = userDefine3a.Text;
            _spider.title4 = userDefine4a.Text;
            _spider.title5 = userDefine5a.Text;
            _spider.title5 = userDefine6a.Text;

            

            _spider.SQLcon = conn;
            string temp = TextUrl.Text.Replace("http://", "");
            _spider.OriginaUrl = "http://"+ temp.Split('/')[0];

            _spider.RootUrl = TextUrl.Text;
            _spider.TaskName = textboxTaskName.Text;
            Thread thread = new Thread(new ParameterizedThreadStart(Download));
            thread.Start(TextPath.Text);
            btnDownload.IsEnabled = false;
            btnDownload.Content = "Downloading...";
            btnStop.IsEnabled = true;
        }

        //开始爬取
        private void Download(object param)
        {
            _spider.Download((string)param);
        }

        //保存内容
        void Spider_ContentsSaved(string path, string url)
        {
            CSHandler h = (p, u) =>
            {
                ListDownload.Items.Add(new { Url = u, File = p });
            };
            Dispatcher.Invoke(h, path, url);
        }

        //停止爬取
        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            _spider.Abort();
            btnDownload.IsEnabled = true;
            btnDownload.Content = "Download";
            btnStop.IsEnabled = false;
        }

        //寻去存储文件夹
        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog fdlg = new System.Windows.Forms.FolderBrowserDialog();
            fdlg.RootFolder = Environment.SpecialFolder.Desktop;
            fdlg.Description = "Contents Root Folder";
            var result = fdlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string path = fdlg.SelectedPath;
                TextPath.Text = path;
            }
        }

        //高级设置，设置深度和连接数
        private void PropertyButton_Click(object sender, RoutedEventArgs e)
        {
            PropertyWindow pw = new PropertyWindow()
            {
                MaxDepth = _spider.MaxDepth,
                MaxConnection = _spider.MaxConnection,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
            };
            if (pw.ShowDialog() == true)
            {
                //更新用户设置 的参数
                _spider.MaxDepth = pw.MaxDepth;
                _spider.MaxConnection = pw.MaxConnection;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Form1 f=new Form1();
            f.Show();
        }
    }
}
