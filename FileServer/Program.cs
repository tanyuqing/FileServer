using Microsoft.Extensions.Configuration;
using System.IO.Compression;
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
    private static string fileRootPath = @"E:\UnityAssetsRootPath";

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
            Console.WriteLine($"\n文件上传地址示例：\n{URLIP}upload?platform=(android)\n\n");

            while (true)
            {
                var contest = await httpListener.GetContextAsync();
                var request = contest.Request;
                var response = contest.Response;

                Console.WriteLine($"\n收到{request.HttpMethod}请求：{request.Url}");

                if (request.HttpMethod == "GET")
                {
                    HandleGetRequest(request, response);
                }
                else if (request.HttpMethod == "POST")
                {
                    HandlePostRequest(request, response);
                }
                else if (request.HttpMethod == "HEAD")
                {
                    HandleHeadRequest(request, response);
                }
                else
                {
                    HandleUnsupportedMethod(request, response);
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
    private static async Task HandleGetRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        Console.WriteLine($"HandleGetRequest triggered at {DateTime.Now}: {request.HttpMethod} {request.Url}");

        string relativePath = request.Url.AbsolutePath.TrimStart('/');
        string localPath = Path.Combine(fileRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (relativePath == ACTION_FAVICON)
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            byte[] respBytes = Encoding.UTF8.GetBytes("no favicon");
            await response.OutputStream.WriteAsync(respBytes, 0, respBytes.Length);
            response.OutputStream.Close();
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
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
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
                    while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await response.OutputStream.WriteAsync(buffer, 0, bytesRead);
                    }
                }
                response.OutputStream.Flush();
                response.OutputStream.Close();
                Console.WriteLine($"文件传输过结束");
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
            await response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
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
    private static async Task HandlePostRequest(HttpListenerRequest req, HttpListenerResponse resp)
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

        // Set read timeout for request stream
        //req.InputStream.ReadTimeout = 10 * 60 * 1000; // 10 minutes

        //上传文件请求
        if (req.Url.AbsolutePath.Equals(ACTION_UPLOAD))
        {
            //var resultStr = await ReceiveFile(req);
            var resultStr = "文件上传成功！";

            if (req.ContentType != null)
            {
                Console.WriteLine($"客户端数据类型，Client data content type:{req.ContentType}");
            }

            var platform = req.QueryString["platform"]?.Trim();
            var form = req.QueryString["form"]?.Trim();

            if (string.IsNullOrEmpty(platform))
            {
                //给予返回
                var bufferTmp = Encoding.UTF8.GetBytes($"url中{nameof(platform)}内容为空，请检查");
                resp.ContentLength64 = bufferTmp.Length;
                await resp.OutputStream.WriteAsync(bufferTmp, 0, bufferTmp.Length);
                return;
            }

            //文件保存目录
            var savePath = Path.Combine(fileRootPath, platform);
            //如果form存在，则目录中带上form
            if (string.IsNullOrEmpty(form) == false)
            {
                savePath = Path.Combine(fileRootPath, platform, form);
            }

            //如果目标目录不为空，则清除
            if (Directory.Exists(savePath))
            {
                DirectoryInfo dir = new(savePath);
                dir.Delete(true);
            }
            //创建目录
            Directory.CreateDirectory(savePath);
            Console.WriteLine($"创建目录：{savePath}");

            // Read the request body as a byte array

            //var str = new StreamReader(req.InputStream).ReadToEnd();
            using (Stream stream = req.InputStream)
            using (MemoryStream memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                byte[] content = memoryStream.ToArray();

                List<int> boundaryPositions = FindBoundaryPositions(content, boundary);

                for (int i = 0; i < boundaryPositions.Count - 1; i++)
                {
                    int start = boundaryPositions[i] + boundary.Length + 2; // +2 to skip CRLF
                    int end = boundaryPositions[i + 1] - 2; // subtract the CRLF before the boundary
                    if (start >= end) continue;

                    byte[] partBytes = new byte[end - start];
                    Array.Copy(content, start, partBytes, 0, partBytes.Length);

                    // Process each partBytes here, e.g., save to file
                    ProcessPartAsync(partBytes, savePath);
                }
            }

            //给予返回
            var buffer = System.Text.Encoding.UTF8.GetBytes(resultStr);
            resp.ContentLength64 = buffer.Length;

            //向响应中写入返回
            resp.OutputStream.Write(buffer, 0, buffer.Length);
        }
        else
        {
            resp.ContentLength64 = 0;
            resp.OutputStream.Write([], 0, 0);
        }
        resp.OutputStream.Flush();
        resp.OutputStream.Close();
        Console.WriteLine("上传请求已结束");
    }

    /// <summary>
    /// 获取数据分隔
    /// </summary>
    static string GetBoundary(string contentType)
    {
        var boundaryStartIndex = contentType.IndexOf("boundary=", StringComparison.InvariantCultureIgnoreCase);
        if (boundaryStartIndex == -1)
        {
            throw new ArgumentException("Boundary not found in Content-Type header");
        }
        var boundary = contentType.Substring(boundaryStartIndex + 9); // 9 is the length of "boundary="
        return boundary.Trim('"'); // Remove surrounding quotes if present
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
        else if (fileName.StartsWith("=?utf-8?B?"))
        {
            // Decode the Base64 encoded filename
            var base64EncodedFilename = fileName.Substring(10, fileName.Length - 12);
            byte[] base64EncodedBytes = Convert.FromBase64String(base64EncodedFilename);
            fileName = Encoding.UTF8.GetString(base64EncodedBytes);
        }
        return fileName;
    }

    /// <summary>
    /// 查找多个文件数据的分界标识
    /// </summary>
    static List<int> FindBoundaryPositions(byte[] content, string boundary)
    {
        var boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);

        List<int> positions = new List<int>();
        for (int i = 0; i <= content.Length - boundaryBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < boundaryBytes.Length; j++)
            {
                if (content[i + j] != boundaryBytes[j])
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
    static void ProcessPartAsync(byte[] part, string savePath)
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


                File.WriteAllBytes(fullFileName, fileData);
                Console.WriteLine($"文件 {fileName} 上传成功，位置：{fullFileName}");

                //如果是zip文件，则解压
                if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ZipFile.ExtractToDirectory(fullFileName, savePath);
                    Console.WriteLine($"文件 {fileName} 已解压到 {savePath}");

                    File.Delete(fullFileName);
                    Console.WriteLine($"文件 {fullFileName} 已删除！");
                }
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

    #region 处理Head请求
    private static void HandleHeadRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        string relativePath = request.Url.AbsolutePath.TrimStart('/');
        string localPath = Path.Combine(fileRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(localPath))
        {
            response.ContentType = GetContentType(localPath);
            response.ContentLength64 = new FileInfo(localPath).Length;
            response.StatusCode = (int)HttpStatusCode.OK;
        }
        else
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
        }
        response.OutputStream.Close();
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

    /// <summary>
    /// 处理非GET和POST请求
    /// </summary>
    private static async Task HandleUnsupportedMethod(HttpListenerRequest request, HttpListenerResponse response)
    {
        response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
        response.StatusDescription = "Method Not Allowed";
        byte[] buffer = Encoding.UTF8.GetBytes("Method Not Allowed");
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }
}

