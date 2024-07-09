using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

public class Program
{
    /// <summary>
    /// 服务地址
    /// </summary>
    private const string IP = "localhost";
    private static string URL = $"http://{IP}:8888/";
    private static string URLIP = $"http://{GetLocalIPAddress()}:8888/";
    /// <summary>
    /// 资源根目录
    /// </summary>
    private static string fileRootPath = @"D:\UnityAssetsRootDir";

    /// <summary>
    /// 请求网站icon
    /// </summary>
    private const string ACTION_FAVICON = "favicon.ico";
    /// <summary>
    /// 请求上传文件
    /// </summary>
    private const string ACTION_UPLOAD = "/upload";

    static async Task Main(string[] args)
    {
        //从配置文件appsettings.json中读取文件服务器的指定目录
        IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
        var cfgPath = config.GetSection("AppConfig")["DirectoryPath"];
        if (cfgPath != null)
        {
            fileRootPath = cfgPath;
        }

        //如果路径不存在，则创建
        if (Directory.Exists(fileRootPath) == false)
        {
            Directory.CreateDirectory(fileRootPath);
        }

        //启动服务器`
        await StartHttpServer();
    }

    /// <summary>
    /// //启动web服务器
    /// </summary>
    private static async Task StartHttpServer()
    {
        try
        {
            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add(URLIP);
            httpListener.Prefixes.Add(URL);
            httpListener.Start();

            Console.WriteLine($"文件服务器已启动！\n\n文件根目录：{fileRootPath}。\n你也可以通过appsetting.json重新指定");
            Console.WriteLine($"\n你可以通以下地址进行文件访问：\n远程：{URLIP} \n本机：{URL}\n");
            Console.WriteLine($"\n文件上传地址示例：\n{URLIP}upload?type=(dddcz)&user=(tanyuiqng)&platform=(android)&version=(1.0)\n()标记的位置替换为实际使用信息，必填且不要括号\n\n");
            while (true)
            {
                var contest = await httpListener.GetContextAsync();
                var request = contest.Request;
                var response = contest.Response;

                Console.WriteLine($"收到{request.HttpMethod}请求：{request.Url}");

                if (request.HttpMethod == "GET")
                {
                    HandleGetRequest(request, response);
                }
                else if (request.HttpMethod == "POST")
                {
                    HandlePostRequest(request, response);
                }
            }
        }
        catch (Exception ex)
        {
            // 记录错误到控制台，可以改为记录到日志文件或其他日志管理工具
            Console.WriteLine($"{DateTime.Now}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }


    #region 处理Get请求
    /// <summary>
    /// 处理Get请求
    /// </summary>
    private static void HandleGetRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        string relativePath = request.Url.AbsolutePath.TrimStart('/');
        string localPath = Path.Combine(fileRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (relativePath == ACTION_FAVICON)
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            byte[] respBytes = Encoding.UTF8.GetBytes("no favicon");
            response.OutputStream.Write(respBytes, 0, respBytes.Length);
            return;
        }

        localPath = HttpUtility.UrlDecode(localPath);

        //是目录就返回目录列表
        if (Directory.Exists(localPath))
        {
            Console.WriteLine($"{localPath}是目录");
            // 返回目录内容的 HTML 列表
            string html = GenerateDirectoryListing(localPath, relativePath);
            byte[] buffer = Encoding.UTF8.GetBytes(html);

            response.ContentType = "text/html;charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        //是文件就返回文件内容
        else if (File.Exists(localPath))
        {
            Console.WriteLine($"{localPath}是文件");
            try
            {
                using (FileStream fs = File.OpenRead(localPath))
                {
                    byte[] buffer = new byte[64 * 1024]; // 64KB
                    int bytesRead;
                    while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        response.OutputStream.Write(buffer, 0, bytesRead);
                        response.OutputStream.Flush();
                    }
                }
                response.OutputStream.Close();
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine("HttpListenerException: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
        }
        //其它情况返回404
        else
        {
            Console.WriteLine($"{localPath}是非文件，非目录");

            // 返回404
            response.StatusCode = (int)HttpStatusCode.NotFound;
            byte[] errorBytes = Encoding.UTF8.GetBytes("File or directory not found");
            response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
            response.OutputStream.Close();
        }
    }

    /// <summary>
    /// 根据请求动态构建网页内容
    /// </summary>
    static string GenerateDirectoryListing(string directoryPath, string relativePath)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<html><body>");
        sb.AppendFormat("<h1>Index of /{0}</h1>", relativePath);

        // 添加父目录链接
        if (!string.IsNullOrEmpty(relativePath))
        {
            string parent = Path.GetDirectoryName(relativePath.TrimEnd('/'));
            if (!string.IsNullOrEmpty(parent))
            {
                parent = parent.Replace(Path.DirectorySeparatorChar, '/');
                sb.AppendFormat("<a href=\"/{0}\">[Parent Directory]</a><br/>", parent);
            }
            else
            {
                sb.Append("<a href=\"/\">[Parent Directory]</a><br/>");
            }
        }

        // 添加子目录链接
        foreach (string dir in Directory.GetDirectories(directoryPath))
        {
            string dirName = Path.GetFileName(dir);
            sb.AppendFormat("<a href=\"/{0}/\">{1}/</a><br/>", Path.Combine(relativePath, dirName).Replace(Path.DirectorySeparatorChar, '/'), dirName);
        }

        // 添加文件链接
        foreach (string file in Directory.GetFiles(directoryPath))
        {
            string fileName = Path.GetFileName(file);
            sb.AppendFormat("<a href=\"/{0}\">{1}</a><br/>", Path.Combine(relativePath, fileName).Replace(Path.DirectorySeparatorChar, '/'), fileName);
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// 判断文件类型
    /// </summary>
    static string GetContentType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        switch (extension)
        {
            case ".htm":
            case ".html":
                return "text/html";
            case ".jpg":
            case ".jpeg":
                return "image/jpeg";
            case ".png":
                return "image/png";
            case ".gif":
                return "image/gif";
            case ".css":
                return "text/css";
            case ".js":
                return "application/javascript";
            case ".json":
                return "application/json";
            case ".pdf":
                return "application/pdf";
            default:
                return "application/octet-stream";
        }
    }
    #endregion

    #region 处理Post请求
    /// <summary>
    /// 处理Post请求
    /// </summary>
    private static async void HandlePostRequest(HttpListenerRequest req, HttpListenerResponse resp)
    {
        if (!req.HasEntityBody)
        {
            Console.WriteLine("No request body found.");
            return;
        }

        // Get the boundary from the Content-Type header
        string boundary = GetBoundary(req.ContentType);
        if (boundary == null)
        {
            Console.WriteLine("Boundary not found.");
            return;
        }

        var output = resp.OutputStream;

        //上传文件请求
        if (req.Url.AbsolutePath.Equals(ACTION_UPLOAD))
        {
            //var resultStr = await ReceiveFile(req);
            var resultStr = "文件上传成功！";

            if (req.ContentType != null)
            {
                Console.WriteLine($"客户端数据类型，Client data content type:{req.ContentType}");
            }

            var type = req.QueryString["type"];
            var user = req.QueryString["user"];
            var platform = req.QueryString["platform"];
            var version = req.QueryString["version"];

            //文件保存目录
            var savePath = Path.Combine(fileRootPath, type, user, platform, version);

            //如果路径不存在，则创建
            if (Directory.Exists(savePath) == false)
            {
                Directory.CreateDirectory(savePath);
            }

            // Read the request body as a byte array
            using (Stream stream = req.InputStream)
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);

                byte[] content = memoryStream.ToArray();

                // Parse the multipart content
                byte[] boundaryBytes = Encoding.UTF8.GetBytes(boundary);
                int boundaryLength = boundaryBytes.Length;

                List<int> boundaryPositions = FindBoundaryPositions(content, boundaryBytes);

                for (int i = 0; i < boundaryPositions.Count - 1; i++)
                {
                    int start = boundaryPositions[i] + boundaryLength;
                    int end = boundaryPositions[i + 1] - 2; // subtract the CRLF before the boundary
                    if (start >= end) continue;

                    byte[] partBytes = new byte[end - start];
                    Array.Copy(content, start, partBytes, 0, partBytes.Length);

                    ProcessPart(partBytes, savePath);
                }
            }

            //给予返回
            var buffer = System.Text.Encoding.UTF8.GetBytes(resultStr);
            resp.ContentLength64 = buffer.Length;

            //向响应中写入返回
            await output.WriteAsync(buffer, 0, buffer.Length);
        }
        else
        {
            await output.WriteAsync(new byte[] { }, 0, 0);
        }
        output.Close();
    }

    /// <summary>
    /// 获取数据分隔
    /// </summary>
    static string GetBoundary(string contentType)
    {
        // Extract the boundary from the Content-Type header
        string boundary = null;
        string[] elements = contentType.Split(';');
        foreach (string element in elements)
        {
            if (element.Trim().StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
            {
                boundary = "--" + element.Trim().Substring("boundary=".Length);
                break;
            }
        }
        return boundary;
    }

    /// <summary>
    /// 提取文件名
    /// </summary>
    static string ExtractFileName(string contentDisposition)
    {
        string fileName = null;
        string[] elements = contentDisposition.Split(';');
        foreach (string element in elements)
        {
            if (element.Trim().StartsWith("filename=", StringComparison.OrdinalIgnoreCase))
            {
                fileName = element.Substring(element.IndexOf("=") + 1).Trim('"');
                break;
            }
        }
        // Decode the filename if it's UTF-8 encoded
        if (fileName != null && fileName.StartsWith("UTF-8''"))
        {
            fileName = HttpUtility.UrlDecode(fileName.Substring("UTF-8''".Length));
        }
        return fileName;
    }

    /// <summary>
    /// 查找多个文件数据的分界标识
    /// </summary>
    static List<int> FindBoundaryPositions(byte[] content, byte[] boundary)
    {
        List<int> positions = new List<int>();
        for (int i = 0; i <= content.Length - boundary.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < boundary.Length; j++)
            {
                if (content[i + j] != boundary[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                positions.Add(i);
            }
        }
        return positions;
    }

    /// <summary>
    /// 分文件读取数据并保存
    /// </summary>
    static void ProcessPart(byte[] part, string savePath)
    {
        int headerEndIndex = FindHeaderEndIndex(part);
        if (headerEndIndex == -1)
        {
            Console.WriteLine("Headers not found.");
            return;
        }

        string headers = Encoding.UTF8.GetString(part, 0, headerEndIndex);
        string contentDisposition = null;
        string contentType = null;

        using (StringReader reader = new StringReader(headers))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("Content-Disposition:", StringComparison.OrdinalIgnoreCase))
                {
                    contentDisposition = line;
                }
                else if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = line;
                }
            }
        }

        if (contentDisposition == null)
        {
            Console.WriteLine("Content-Disposition header not found.");
            return;
        }

        if (contentDisposition.Contains("filename="))
        {
            // Extract the file name
            string fileName = ExtractFileName(contentDisposition);

            // Extract the file data
            int contentStartIndex = headerEndIndex + 4; // skip the two CRLFs after headers
            byte[] fileData = new byte[part.Length - contentStartIndex];
            Array.Copy(part, contentStartIndex, fileData, 0, fileData.Length);

            // Save the file
            if (!string.IsNullOrEmpty(fileName))
            {
                //文件全路径名
                var fullFileName = Path.Combine(savePath, fileName);
                Console.WriteLine($"准备保存文件:{fullFileName}");

                //如果已存在，则删除
                if (File.Exists(fullFileName))
                {
                    File.Delete(fullFileName);
                }

                File.WriteAllBytes(fullFileName, fileData);
                Console.WriteLine($"文件 '{fileName}' 上传成功，位置：{fullFileName}");
            }
        }
        else
        {
            // Extract the value
            string name = ExtractName(contentDisposition);
            int contentStartIndex = headerEndIndex + 4; // skip the two CRLFs after headers
            string value = Encoding.UTF8.GetString(part, contentStartIndex, part.Length - contentStartIndex);

            // Process the key-value pair
            Console.WriteLine($"Key: {name}, Value: {value}");
        }
    }

    /// <summary>
    /// 从body中查找key
    /// </summary>
    static string ExtractName(string contentDisposition)
    {
        string name = null;
        string[] elements = contentDisposition.Split(';');
        foreach (string element in elements)
        {
            if (element.Trim().StartsWith("name=", StringComparison.OrdinalIgnoreCase))
            {
                name = element.Substring(element.IndexOf('=') + 1).Trim('"');
                break;
            }
        }
        return name;
    }

    /// <summary>
    /// 查找文件头结束标识的索引
    /// </summary>
    static int FindHeaderEndIndex(byte[] part)
    {
        for (int i = 0; i < part.Length - 3; i++)
        {
            //13为ascii的 CR（）,10为ascii的LF，其实就是检测CRLFCRLF，也就是\r\n\r\n
            if (part[i] == 13 && part[i + 1] == 10 && part[i + 2] == 13 && part[i + 3] == 10)
            {
                return i;
            }
        }
        return -1;
    }
    #endregion

    /// <summary>
    /// 获取本机Ip地址
    /// </summary>
    private static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }
}

