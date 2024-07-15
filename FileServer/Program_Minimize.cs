using System.Diagnostics;
using System.Runtime.InteropServices;

public partial class Program
{
    #region 窗口最小化相关
    /// <summary>
    /// 获取当前控制台窗口的句柄
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
    /// <summary>
    /// 控制窗口的显示隐藏
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    /// <summary>
    /// 将窗口设置为活动窗口
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 窗口隐藏标识
    /// </summary>
    private const int SW_HIDE = 0;
    /// <summary>
    /// 激活窗口并将其显示为最小化窗口
    /// </summary>
    private const int SW_SHOWMINIMIZED = 2;
    /// <summary>
    /// 窗口显示标识 
    /// </summary>
    private const int SW_SHOW = 5;
    /// <summary>
    /// 窗口最小化标识，小化指定的窗口并激活 Z 顺序中的下一个顶层窗口。
    /// </summary>
    private const int SW_MINIMIZE = 6;
    /// <summary>
    /// 窗口恢复
    /// </summary>
    private const int SW_RESTORE = 9;


    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// 系统通知图标
    /// </summary>
    private static NotifyIcon notifyIcon;

    /// <summary>
    /// 当前控制台窗口的句柄
    /// </summary>
    private static IntPtr consoleWndHandle;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    private static long counter = 0;

    /// <summary>
    /// 窗口是否已隐藏
    /// </summary>
    private static bool isWndHide = false;

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public System.Drawing.Point ptMinPosition;
        public System.Drawing.Point ptMaxPosition;
        public System.Drawing.Rectangle rcNormalPosition;
    }

    /// <summary>
    /// 窗口最小化相关逻辑
    /// </summary>
    private static void MinimizeInit()
    {
        //保存当前窗口句柄
        consoleWndHandle = GetConsoleWindow();

        //创建托盘图标
        CreateTrayIcon();

        //启动后台任务
        var cts = new CancellationTokenSource();

        //设置Ctrl+C和Ctrl+Break的事件处理程序
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        //检查最小化事件
        CheckMinimizeEventByLoop();
    }

    /// <summary>
    /// 创建托盘图标
    /// </summary>
    private static void CreateTrayIcon()
    {
        //创建系统托盘图标
        notifyIcon = new NotifyIcon
        {
            Icon = new Icon("program.ico"),
            Text = "LT文件服务器",
            Visible = true,
        };

        //创建一个菜单
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开", null, (s, e) => ShowConsoleWindow());
        menu.Items.Add("退出", null, (s, e) => ExitApplication());

        // 注册点击事件，以防需要点击图标打开菜单
        notifyIcon.MouseUp += (sender, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                // 通过触发 ContextMenuStrip 的 Opening 事件来打开菜单
                menu.Show(Control.MousePosition);
            }
        };

        //将菜单赋给托盘图标
        notifyIcon.ContextMenuStrip = menu;
    }

    /// <summary>
    /// 检查最小化事件
    /// </summary>
    private static async void CheckMinimizeEventByLoop()
    {
        IntPtr hwnd = GetConsoleWindow();
        WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
        placement.length = Marshal.SizeOf(placement);

        while (true)
        {
            GetWindowPlacement(hwnd, ref placement);
            if (placement.showCmd == SW_SHOWMINIMIZED)
            {

                Debug.WriteLine($"控制台窗口已最小化，isWndHide:{isWndHide}");
                if (isWndHide == false)
                {
                    Debug.WriteLine($"执行隐藏窗口");
                    HideConsoleWindow();
                    isWndHide = true;
                }
            }
            else
            {
                isWndHide = false;
            }
            //让循环1秒执行1次
            await Task.Delay(1000);
            //Debug.WriteLine("counter:" + counter++);
        }
    }

    /// <summary>
    /// 显示并激活控制台窗口
    /// </summary>
    private static void ShowConsoleWindow()
    {
        ShowWindow(consoleWndHandle, SW_RESTORE);
        SetForegroundWindow(consoleWndHandle);
    }

    /// <summary>
    /// 隐藏控制台窗口
    /// </summary>
    private static void HideConsoleWindow()
    {
        ShowWindow(consoleWndHandle, SW_HIDE);
    }

    /// <summary>
    /// 退出当前程序
    /// </summary>
    private static void ExitApplication()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        Application.Exit();
        Environment.Exit(0);
    }

    #endregion
}

