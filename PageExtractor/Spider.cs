using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using System.Data.OleDb;

namespace PageExtractor
{
    class Spider
    {

        private string taskName = "";
        private OleDbConnection conn=null;

        //用户定义的标签
        public string title1 = "";
        public string title2 = "";
        public string title3 = "";
        public string title4 = ""; 
        public string title5 = "";
        public string title6 = "";

        

        #region private type
        private class RequestState
        {
            private const int BUFFER_SIZE = 131072;
            private byte[] _data = new byte[BUFFER_SIZE];
            private StringBuilder _sb = new StringBuilder();
            
            public HttpWebRequest Req { get; private set; }
            public string Url { get; private set; }
            public int Depth { get; private set; }
            public int Index { get; private set; }
            public Stream ResStream { get; set; }
            public StringBuilder Html
            {
                get
                {
                    return _sb;
                }
            }
            
            public byte[] Data
            {
                get
                {
                    return _data;
                }
            }

            public int BufferSize
            {
                get
                {
                    return BUFFER_SIZE;
                }
            }

            public RequestState(HttpWebRequest req, string url, int depth, int index)
            {
                Req = req;
                Url = url;
                Depth = depth;
                Index = index;
            }
        }

        private class WorkingUnitCollection
        {
            private int _count;
            //private AutoResetEvent[] _works;
            private bool[] _busy;

            public WorkingUnitCollection(int count)
            {
                _count = count;
                //_works = new AutoResetEvent[count];
                _busy = new bool[count];

                for (int i = 0; i < count; i++)
                {
                    //_works[i] = new AutoResetEvent(true);
                    _busy[i] = true;
                }
            }

            public void StartWorking(int index)
            {
                if (!_busy[index])
                {
                    _busy[index] = true;
                    //_works[index].Reset();
                }
            }

            //线程工作完成
            public void FinishWorking(int index)
            {
                if (_busy[index])
                {
                    _busy[index] = false;
                    //_works[index].Set();
                }
            }

            public bool IsFinished()
            {
                bool notEnd = false;
                foreach (var b in _busy)
                {
                    notEnd |= b;
                }
                return !notEnd;
            }

            public void WaitAllFinished()
            {
                while (true)
                {
                    if (IsFinished())
                    {
                        break;
                    }
                    Thread.Sleep(1000);
                }
                //WaitHandle.WaitAll(_works);
            }

            public void AbortAllWork()
            {
                for (int i = 0; i < _count; i++)
                {
                    _busy[i] = false;
                }
            }
        }
        #endregion

        #region private fields
        //设置编码和get的request内容
        private static Encoding GB18030 = Encoding.GetEncoding("GB18030");   // GB18030兼容GBK和GB2312
        private static Encoding UTF8 = Encoding.UTF8;
        private string _userAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0)";
        private string _accept = "text/html";
        private string _method = "GET";
        private Encoding _encoding = GB18030;
        private Encodings _enc = Encodings.GB;
        private int _maxTime = 2 * 60 * 1000;

        private int _index;
        private string _path = null;
        private int _maxDepth = 2;
        private int _maxExternalDepth = 0;
        private string _rootUrl = null;
        private string _baseUrl = null;
        //下载和已经下载队列
        private Dictionary<string, int> _urlsLoaded = new Dictionary<string, int>();
        private Dictionary<string, int> _urlsUnload = new Dictionary<string, int>();

        private bool _stop = true;
        private Timer _checkTimer = null;
        private readonly object _locker = new object();
        private bool[] _reqsBusy = null;
        private int _reqCount = 4;
        private WorkingUnitCollection _workingSignals;

        //原始链接，www.xxx.com
        private string orignal_url = "";
        #endregion

        #region constructors
        /// <summary>
        /// 创建一个Spider实例
        /// </summary>
        public Spider()
        {
        }
        #endregion

        #region properties
        /// <summary>
        /// 任务名称
        /// </summary>
        public string TaskName
        {
            get
            {
                return taskName;
            }
            set
            {
                    taskName = value;
            }
        }

        //原始链接 www.xx.com
        public string OriginaUrl
        {
            get
            {
                return orignal_url;
            }
            set
            {
                orignal_url = value;
            }
        }

        //数据库连接对象
        public OleDbConnection SQLcon
        {
            get
            {
                return conn;
            }
            set
            {
                conn = value;
            }
        }

        //用户输入的地址
        public string RootUrl
        {
            get
            {
                return _rootUrl;
            }
            set
            {
                if (!value.Contains("http://"))
                {
                    _rootUrl = "http://" + value;
                }
                else
                {
                    _rootUrl = value;
                   
                } 
                //获取网站的域名
               _baseUrl = _rootUrl.Replace("www.", "");
               //_baseUrl = _rootUrl;
               _baseUrl = _baseUrl.Replace("http://", "");
               string []temp = _baseUrl.Split('/');

                if(temp.Length!=0)
                {
                    _baseUrl = temp[0];
                }
            }
        }


        /// <summary>
        /// 网页编码类型
        /// </summary>
        public Encodings PageEncoding
        {
            get
            {
                return _enc;
            }
            set
            {
                _enc = value;
                switch (value)
                {
                        //设置网页编码
                    case Encodings.GB:
                        _encoding = GB18030;
                        break;
                    case Encodings.UTF8:
                        _encoding = UTF8;
                        break;
                }
            }
        }

        /// <summary>
        /// 最大下载深度
        /// </summary>
        public int MaxDepth
        {
            get
            {
                return _maxDepth;
            }
            set
            {
                //反之用户输入非法字符
                _maxDepth = Math.Max(value, 1);
            }
        }

        /// <summary>
        /// 下载最大连接数
        /// </summary>
        public int MaxConnection
        {
            get
            {
                return _reqCount;
            }
            set
            {
                _reqCount = value;
            }
        }
        #endregion

        #region public type
        public delegate void ContentsSavedHandler(string path, string url);

        public delegate void DownloadFinishHandler(int count);

        public enum Encodings
        {
            UTF8,
            GB
        }
        #endregion

        #region events
        /// <summary>
        /// 正文内容被保存到本地后触发
        /// </summary>
        public event ContentsSavedHandler ContentsSaved = null;

        /// <summary>
        /// 全部链接下载分析完毕后触发
        /// </summary>
        public event DownloadFinishHandler DownloadFinish = null;
        #endregion

        #region public methods
        /// <summary>
        /// 开始下载
        /// </summary>
        /// <param name="path">保存本地文件的目录</param>
        public void Download(string path)
        {
            if (string.IsNullOrEmpty(RootUrl))
            {
                return;
            }
            _path = path;
            Init();
            StartDownload();
        }

        /// <summary>
        /// 终止下载
        /// </summary>
        public void Abort()
        {
            _stop = true;
            if (_workingSignals != null)
            {
                _workingSignals.AbortAllWork();
            }
        }
        #endregion

        #region private methods
        private void StartDownload()
        {
            _checkTimer = new Timer(new TimerCallback(CheckFinish), null, 0, 300);
            DispatchWork();
        }

        
        private void CheckFinish(object param)
        {
            if (_workingSignals.IsFinished())
            {
                _checkTimer.Dispose();
                _checkTimer = null;
                if (DownloadFinish != null)
                {
                    DownloadFinish(_index);
                }
            }
        }

        //调度工作
        private void DispatchWork()
        {
            //是否停止
            if (_stop)
            {
                return;
            }

            //选择空闲connection进行爬取
            for (int i = 0; i < _reqCount; i++)
            {
                if (!_reqsBusy[i])
                {
                    RequestResource(i);
                }
            }
        }

        private void Init()
        {
            //清空下载队列
            _urlsLoaded.Clear();
            _urlsUnload.Clear();
            AddUrls(new string[1] { RootUrl }, 0);
            _index = 0;
            _reqsBusy = new bool[_reqCount];
            _workingSignals = new WorkingUnitCollection(_reqCount);
            _stop = false;
        }


        //准备发送get请求
        private void RequestResource(int index)
        {
            int depth;
            string url = "";
            try
            {
                lock (_locker)
                {
                    if (_urlsUnload.Count <= 0)
                    {
                        //index线程工作完成
                        _workingSignals.FinishWorking(index);
                        return;
                    }
                    _reqsBusy[index] = true;
                    _workingSignals.StartWorking(index);
                    depth = _urlsUnload.First().Value;
                    url = _urlsUnload.First().Key;
                    _urlsLoaded.Add(url, depth);
                    _urlsUnload.Remove(url);
                }
                
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = _method; //请求方法
                req.Accept = _accept; //接受的内容
                req.UserAgent = _userAgent; //用户代理
                RequestState rs = new RequestState(req, url, depth, index);
                var result = req.BeginGetResponse(new AsyncCallback(ReceivedResource), rs);
                ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle,
                        TimeoutCallback, rs, _maxTime, true);
            }
            catch (WebException we)
            {
                MessageBox.Show("RequestResource " + we.Message + url + we.Status);
            }
        }


        //发送http请求 就收respons
        private void ReceivedResource(IAsyncResult ar)
        {
            RequestState rs = (RequestState)ar.AsyncState;
            HttpWebRequest req = rs.Req;
            string url = rs.Url;
            try
            {
                HttpWebResponse res = (HttpWebResponse)req.EndGetResponse(ar);
                if (_stop)
                {
                    res.Close();
                    req.Abort();
                    return;
                }
                //判断respons是否正确
                if (res != null && res.StatusCode == HttpStatusCode.OK)
                {
                    Stream resStream = res.GetResponseStream();
                    rs.ResStream = resStream;
                    var result = resStream.BeginRead(rs.Data, 0, rs.BufferSize,
                        new AsyncCallback(ReceivedData), rs);
                }
                else
                {
                    res.Close();
                    rs.Req.Abort();
                    _reqsBusy[rs.Index] = false;
                    DispatchWork();
                }
            }
            catch (WebException we)
            {
                MessageBox.Show("ReceivedResource " + we.Message + url + we.Status);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        //获取html
        private void ReceivedData(IAsyncResult ar)
        {
            RequestState rs = (RequestState)ar.AsyncState;
            HttpWebRequest req = rs.Req;
            Stream resStream = rs.ResStream;
            string url = rs.Url;
            int depth = rs.Depth;
            string html = null;
            int index = rs.Index;
            int read = 0;

            //读取html
            try
            {
                read = resStream.EndRead(ar);
                if (_stop)
                {
                    rs.ResStream.Close();
                    req.Abort();
                    return;
                }
                if (read > 0)
                {
                    MemoryStream ms = new MemoryStream(rs.Data, 0, read);
                    Encoding testUtf8 = UTF8;
                    StreamReader reader = new StreamReader(ms, testUtf8);
                    //StreamReader reader = new StreamReader(ms, _encoding);
                    string str = reader.ReadToEnd();
                    rs.Html.Append(str);
                    var result = resStream.BeginRead(rs.Data, 0, rs.BufferSize,
                        new AsyncCallback(ReceivedData), rs);
                    return;
                }
                html = rs.Html.ToString();
                SaveContents(html, url);
                string[] links = GetLinks(html);
                //添加urls 并且将深度增加1
                AddUrls(links, depth + 1);

                _reqsBusy[index] = false;
                DispatchWork();
            }
            catch (WebException we)
            {
                MessageBox.Show("ReceivedData Web " + we.Message + url + we.Status);
            }
            catch (Exception e)
            {
                Console.WriteLine("error this--------------");
                MessageBox.Show(e.GetType().ToString() + e.Message);
            }
        }


        //唤醒
        private void TimeoutCallback(object state, bool timedOut)
        {
            if (timedOut)
            {
                RequestState rs = state as RequestState;
                if (rs != null)
                {
                    rs.Req.Abort();
                }
                _reqsBusy[rs.Index] = false;
                //调度 connection
                DispatchWork();
            }
        }



        /// <summary>  
        /// 获取字符中指定标签的值  
        /// </summary>  
        /// <param name="str">字符串</param>  
        /// <param name="title">标签</param>  
        /// <param name="attrib">属性名</param>  
        /// <returns>属性</returns>  
        public static string[] GetTitleContent(string str, string title, string attrib)
        {

            string tmpStr = string.Format("<{0}[^>]*?{1}=(['\"\"]?)(?<url>[^'\"\"\\s>]+)\\1[^>]*>", title, attrib); //获取<title>之间内容  

            MatchCollection matchCollection = Regex.Matches(str, tmpStr, RegexOptions.IgnoreCase);
            string[] links = new string[matchCollection.Count];
            for (int i = 0; i < matchCollection.Count; i++)
            {
                links[i] = matchCollection[i].Groups["url"].Value;
            }
            return links;

            //Match TitleMatch = Regex.Match(str, tmpStr, RegexOptions.IgnoreCase);
            //string result = TitleMatch.Groups["url"].Value;
            //return result;
        }



        /// 获取字符中指定标签的值  
        /// </summary>  
        /// <param name="str">字符串</param>  
        /// <param name="title">标签</param>  
        /// <param name="attrib">属性名</param>
        /// 返回结果
        public static string GetUserContent(string str, string title)
        {

            //string tmpStr = string.Format("<{0}[^>]*?{1}=(['\"\"]?)(?<url>[^'\"\"\\s>]+)\\1[^>]*>", title, attrib); //获取<title>之间内容  
            string tmpStr = string.Format("<{0}[^>]*?>(?<Text>[^<]*)</{1}>", title, title); //获取<title>之间内容
            MatchCollection matchCollection = Regex.Matches(str, tmpStr, RegexOptions.IgnoreCase);
            string[] links = new string[matchCollection.Count];
            for (int i = 0; i < matchCollection.Count; i++)
            {
                links[i] = matchCollection[i].Groups["Text"].Value;
            }

            string result = string.Join("     \n\r", links);

            return result;

            //Match TitleMatch = Regex.Match(str, tmpStr, RegexOptions.IgnoreCase);
            //string result = TitleMatch.Groups["url"].Value;
            //return result;
        }


        //正则过滤urls
        private string[] GetLinks(string html)
        {
            string []test=GetTitleContent(html, "a", "href");
            //for (int i = 0; i < test.Length; i++)
            //{
            //    Console.WriteLine("get title:" + test[i]);
            //}
            const string pattern = @"http://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?";
            string result = string.Empty;

            //爬取所有完整url
            Regex r = new Regex(pattern, RegexOptions.IgnoreCase);


            MatchCollection m = r.Matches(html);
            string[] links = new string[m.Count];

            Console.WriteLine("root:" + RootUrl + "   " + _rootUrl);


            for (int i = 0; i < m.Count; i++)
            {
                links[i] = m[i].ToString();
            }
            //return links;
            return test;
             
        }



        //相对路径和绝对路径转换
        //public static string ConvertToAbsoluteUrls(string html)
        //{
        //    return Regex.Replace(html, "(href|src)=[\"'](?<url>.*?)[\"']", new MatchEvaluator(ComputeReplacement));
        //}
        //public static String ComputeReplacement(Match m)
        //{
        //    Uri absoluteUri = new Uri(baseUri, m.Groups["url"].ToString());
        //    return String.Format("{0}=\"{1}\"", m.Groups["1"].ToString(), absoluteUri.ToString());
        //}



        //url查重
        private bool UrlExists(string url)
        {
            bool result = _urlsUnload.ContainsKey(url);
            result |= _urlsLoaded.ContainsKey(url);
            return result;
        }

        //验证url 是否正确，排除各种附件和已经爬取的url
        private bool UrlAvailable(string url)
        {
           
            if (UrlExists(url))
            {
                return false;
            }
            if (url.Contains(".jpg") || url.Contains(".gif")
                || url.Contains(".png") || url.Contains(".css")
                || url.Contains(".js"))
            {
                return false;
            }
            return true;
        }

        private void AddUrls(string[] urls, int depth)
        {
            //深度超过定义的深度就放弃
            if (depth >= _maxDepth)
            {
                return;
            }
            foreach (string url in urls)
            {
                string cleanUrl = url.Trim();
                int end = cleanUrl.IndexOf(' ');
                if (end > 0)
                {
                    cleanUrl = cleanUrl.Substring(0, end);
                }
                cleanUrl = cleanUrl.TrimEnd('/');

                //如果爬取到的为相对路径 （没有www）
                if (!cleanUrl.Contains("http://"))
                {
                    Console.WriteLine(_baseUrl);
                    //cleanUrl = _baseUrl + cleanUrl;
                    //cleanUrl = "http://www.guet.cn" + cleanUrl;
                    cleanUrl = orignal_url + cleanUrl;
                    Console.WriteLine("更改链接:" + cleanUrl);
                }

                //判断url 是否可靠
                if (UrlAvailable(cleanUrl))
                {
                   // Console.WriteLine("判断可靠，无重复：" + cleanUrl);

                    if (cleanUrl.Contains(_baseUrl))
                    {
                      //  Console.WriteLine("添加下载队列：" + cleanUrl);
                        _urlsUnload.Add(cleanUrl, depth);
                    }
                    else
                    {
                        // 外链
                    }
                }
            }
        }


        //保存爬取的内容
        private void SaveContents(string html, string url)
        {
            if (string.IsNullOrEmpty(html))
            {
                return;
            }
            string path = "";
            lock (_locker)
            {
                //按照任务名和序号命名
                path = string.Format("{0}\\{1}.txt", _path, taskName+_index++);
                Console.WriteLine(url);
            }
            //存储到access
            //string sql = "insert into spiderStore (data,taskName,url,type,content) values("+DateTime.Now.ToLocalTime().ToString().Trim()+","+taskName+","+url+","+"all"+","+html+")";
           //string sql= "insert into spiderStore(data, taskName, url,content) values ('" + DateTime.Now.ToLocalTime().ToString() + "', '" + taskName + "', '" + url + "', '" + html.Trim()+ "')";
            //OleDbCommand comm = new OleDbCommand(sql, conn);
            //comm.ExecuteNonQuery(); 


            string userStr1="";
            string userStr2="";
            string userStr3="";
            string userStr4="";
            string userStr5 = "";
            string userStr6 = "";
            bool user = false;
            //使用用户定义的正则
            if (title1 != "")
            {
                user = true;
                 userStr1 = GetUserContent(html, title1);
            }
            if (title2 != "")
            {
                user = true;
                 userStr2 = GetUserContent(html, title2);
            }
            if (title3 != "")
            {
                user = true;
                 userStr3 = GetUserContent(html, title3);
            }
            if (title4 != "")
            {
                user = true;
                 userStr4 = GetUserContent(html, title4);
            }
            if (title5 != "")
            {
                user = true;
                userStr5 = GetUserContent(html, title5);
            }
            if (title6 != "")
            {
                user = true;
                userStr5 = GetUserContent(html, title6);
            }

            string finalUserStr = userStr1 + "\n\r" + userStr2 + "\n\r" + userStr3 + "\n\r" + userStr4 + "\n\r" + userStr5 + "\n\r" + userStr6;
            try
            {
                using (StreamWriter fs = new StreamWriter(path))
                {
                    //文件写入 
                    if (user)
                    {
                        fs.Write(finalUserStr);
                    }
                    else
                    {
                        fs.Write(html);
                    }
                }
            }
            catch (IOException ioe)
            {
                MessageBox.Show("SaveContents IO" + ioe.Message + " path=" + path);
            }
            
            if (ContentsSaved != null)
            {
                ContentsSaved(path, url);
            }
        }
        #endregion


  
    }
}
