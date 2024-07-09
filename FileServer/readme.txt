这是一个c#编写的文件上传和查看服务器

启动文件：FileServer.exe

文件根目录通过appsetting.json进行配置

你可以通以下地址进行文件访问：
远程：http://x.x.x.x:8888/（x.x.x.x为你电脑的ipv4地址，程序启动后控制台也会显示出来）
本机：http://localhost:8888/


文件上传URL示例：
http://x.x.x.x:8888/upload?type=(dddcz)&user=(tanyuiqng)&platform=(android)&version=(1.0)
()标记的位置替换为实际使用信息，必填且不要括号

参数说明：
type：游戏名（应用名）
user：用户（谁上传的文件）
platform：游戏（应用）的平台（一般为android或ios）
version：资源版本

以上参数决定文件存储目录，比如上边的示例url会将上传的资源文件最后会放置在{root}\dddcz\tanyuqing\android\1.0\之下。
这里的{root}为appsetting.jso中配置的目录