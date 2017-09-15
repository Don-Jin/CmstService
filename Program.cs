using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Configuration;
using System.ServiceProcess;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using SuperSocket.Common;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketEngine;
using SuperSocket.SocketEngine.Configuration;

namespace CmstService
{
    static partial class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            // If this application run in Mono/Linux, change the control script to be executable
            if(Platform.IsMono && Path.DirectorySeparatorChar == '/')
                ChangeScriptExecutable();

            if ((!Platform.IsMono && !Environment.UserInteractive)  // Windows Service
                || (Platform.IsMono && !AppDomain.CurrentDomain.FriendlyName.Equals(Path.GetFileName(Assembly.GetEntryAssembly().CodeBase))))  // MonoService
            {
                RunAsService();

                // 系统服务默认与桌面不能交互
                return;
            }
            
            // 标题
            Console.Title = "CmstService";
            
            string exeArg = string.Empty;

            CheckCanSetConsoleColor();

            if (args == null || args.Length < 1)
            {

                ConsoleTextList list = new ConsoleTextList();
                list.AddRange(new ConsoleText[] {
                    new ConsoleText(CommonString.CommonLine),
                    new ConsoleText(CommonString.SpaceLine),
                    new ConsoleText("|                          ", ConsoleColor.White, false),
                    new ConsoleText("欢迎来到『中储洛阳』通信后台！", ConsoleColor.Cyan, false),
                    new ConsoleText("                     |", ConsoleColor.White),
                    new ConsoleText(CommonString.SpaceLine),
                    new ConsoleText(CommonString.CommonLine),
                    new ConsoleText(""),
                    new ConsoleText(CommonString.DevideLine, ConsoleColor.Green),
                    new ConsoleText(""),
                    new ConsoleText(CommonString.TableLine),
                    new ConsoleText("|   ", ConsoleColor.White, false),
                    new ConsoleText("命令", ConsoleColor.Cyan, false),
                    new ConsoleText("  |                               ", ConsoleColor.White, false),
                    new ConsoleText("描述", ConsoleColor.Cyan, false),
                    new ConsoleText("                                |", ConsoleColor.White),
                    new ConsoleText(CommonString.TableLine),
                    new ConsoleText("|    ", ConsoleColor.White, false),
                    new ConsoleText("r", ConsoleColor.Cyan, false),
                    new ConsoleText("    |  ", ConsoleColor.White, false),
                    new ConsoleText("以控制台模式运行系统", ConsoleColor.Cyan, false),
                    new ConsoleText("                                             |", ConsoleColor.White),
                    new ConsoleText(CommonString.TableLine),
                    new ConsoleText("|    ", ConsoleColor.White, false),
                    new ConsoleText("i", ConsoleColor.Cyan, false),
                    new ConsoleText("    |  ", ConsoleColor.White, false),
                    new ConsoleText("将应用安装为 Windows 服务", ConsoleColor.Cyan, false),
                    new ConsoleText("                                        |", ConsoleColor.White),
                    new ConsoleText(CommonString.TableLine),
                    new ConsoleText("|    ", ConsoleColor.White, false),
                    new ConsoleText("u", ConsoleColor.Cyan, false),
                    new ConsoleText("    |  ", ConsoleColor.White, false),
                    new ConsoleText("将应用从 Windows 服务卸载", ConsoleColor.Cyan, false),
                    new ConsoleText("                                        |", ConsoleColor.White),
                    new ConsoleText(CommonString.TableLine),
                    new ConsoleText(""),
                    new ConsoleText(""),
                    new ConsoleText("按任意键继续..."),
                });
                SetConsoleColor(list);

                while (true)
                {
                    exeArg = Console.ReadKey().KeyChar.ToString();
                    Console.WriteLine();

                    if (Run(exeArg, null))
                        break;
                }
            }
            else
            {
                exeArg = args[0];

                if (!string.IsNullOrEmpty(exeArg))
                    exeArg = exeArg.TrimStart('-');

                Run(exeArg, args);
            }
        }

        static void ChangeScriptExecutable()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CmstService.sh");

            try
            {
                if (!File.Exists(filePath))
                    return;

                File.SetAttributes(filePath, (FileAttributes)((uint)File.GetAttributes(filePath) | 0x80000000));
            }
            catch { }
        }

        private static bool Run(string exeArg, string[] startArgs)
        {
            switch (exeArg.ToLower())
            {
                case ("i"):
                    SelfInstaller.InstallMe();
                    return true;

                case ("u"):
                    SelfInstaller.UninstallMe();
                    return true;

                case ("r"):
                    RunAsConsole();
                    return true;

                case ("c"):
                    RunAsController(startArgs);
                    return true;

                default:
                    Console.WriteLine("无效参数！");
                    return false;
            }
        }

        private static bool setConsoleColor;

        private static void CheckCanSetConsoleColor()
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.ResetColor();
                setConsoleColor = true;
            }
            catch
            {
                setConsoleColor = false;
            }
        }

        private static void SetConsoleColor(ConsoleColor color)
        {
            if (setConsoleColor)
                Console.ForegroundColor = color;
        }

        private static void SetConsoleColor(ConsoleTextList list)
        {
            foreach (ConsoleText text in list.List)
            {
                if (setConsoleColor)
                    SetConsoleColor(text.Color);

                if (text.Wrap)
                    Console.WriteLine(text.Text);
                else
                    Console.Write(text.Text);
            }
        }

        private static Dictionary<string, ControlCommand> m_CommandHandlers = new Dictionary<string, ControlCommand>(StringComparer.OrdinalIgnoreCase);

        private static void AddCommand(string name, string description, Func<IBootstrap, string[], bool> handler)
        {
            var command = new ControlCommand
            {
                Name = name,
                Description = description,
                Handler = handler
            };

            m_CommandHandlers.Add(command.Name, command);
        }

        static void RunAsConsole()
        {
            Console.WriteLine("感谢使用 CmstService!");

            //CheckCanSetConsoleColor();

            Console.WriteLine("初始化中...");

            IBootstrap bootstrap = BootstrapFactory.CreateBootstrap();

            if (!bootstrap.Initialize())
            {
                SetConsoleColor(ConsoleColor.Red);

                Console.WriteLine("系统初始化失败！请检查错误日志以获取更多信息！");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("启动中...");

            var result = bootstrap.Start();

            Console.WriteLine(CommonString.HorizontalLine);

            foreach (var server in bootstrap.AppServers)
            {
                if (server.State == ServerState.Running)
                {
                    SetConsoleColor(ConsoleColor.Green);
                    Console.WriteLine("- {0} 启动成功", server.Name);
                }
                else
                {
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine("- {0} 启动失败", server.Name);
                }
            }

            Console.ResetColor();
            Console.WriteLine(CommonString.HorizontalLine);

            switch(result)
            {
                case(StartResult.None):
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine("未配置任何服务器项，请检查你的配置文件！");
                    Console.ReadKey();
                    return;

                case(StartResult.Success):
                    Console.WriteLine("CmstService 系统已成功启动！");
                    break;

                case (StartResult.Failed):
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine("系统初始化失败！请检查错误日志以获取更多信息！");
                    Console.ReadKey();
                    return;

                case (StartResult.PartialSuccess):
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine("部分服务器项启动成功，但其余全部启动失败！请检查错误日志以获取更多信息！");
                    break;
            }

            Console.ResetColor();
            Console.WriteLine("键入 \"quit\" 以终止系统运行。");

            RegisterCommands();

            ReadConsoleCommand(bootstrap);

            bootstrap.Stop();

            Console.WriteLine("CmstService 系统已成功终止！");
        }

        private static void RegisterCommands()
        {
            AddCommand("List", "列出所有服务器实例", ListCommand);
            AddCommand("Start", "启动一个服务器实例: Start {ServerName}", StartCommand);
            AddCommand("Stop", "终止一个服务器实例: Stop {ServerName}", StopCommand);
        }

        private static void RunAsController(string[] arguments)
        {
            if (arguments == null || arguments.Length < 2)
            {
                Console.WriteLine("无效参数！");
                return;
            }

            var config = ConfigurationManager.GetSection("socketServer") as IConfigurationSource;

            if (config == null)
            {
                Console.WriteLine("系统依赖配置文件启动服务，但并未找到有效配置信息！");
                return;
            }

            var clientChannel = new IpcClientChannel();
            ChannelServices.RegisterChannel(clientChannel, false);

            IBootstrap bootstrap = null;

            try
            {
                var remoteBootstrapUri = string.Format("ipc://SuperSocket.Bootstrap[{0}]/Bootstrap.rem", Math.Abs(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar).GetHashCode()));
                bootstrap = (IBootstrap)Activator.GetObject(typeof(IBootstrap), remoteBootstrapUri);
            }
            catch (RemotingException)
            {
                if (config.Isolation != IsolationMode.Process)
                {
                    Console.WriteLine("错误: CmstService 系统并未启动！");
                    return;
                }
            }

            RegisterCommands();

            var cmdName = arguments[1];

            ControlCommand cmd;

            if (!m_CommandHandlers.TryGetValue(cmdName, out cmd))
            {
                Console.WriteLine("未知命令！");
                return;
            }

            try
            {
                if (cmd.Handler(bootstrap, arguments.Skip(1).ToArray()))
                    Console.WriteLine("命令执行成功！");
            }
            catch (Exception e)
            {
                Console.WriteLine("命令执行失败！ " + e.Message);
            }
        }

        static bool ListCommand(IBootstrap bootstrap, string[] arguments)
        {
            foreach (var s in bootstrap.AppServers)
            {
                var processInfo = s as IProcessServer;

                if (processInfo != null && processInfo.ProcessId > 0)
                    Console.WriteLine("{0}[PID:{1}] - {2}", s.Name, processInfo.ProcessId, s.State);
                else
                    Console.WriteLine("{0} - {1}", s.Name, s.State);
            }

            return false;
        }

        static bool StopCommand(IBootstrap bootstrap, string[] arguments)
        {
            var name = arguments[1];

            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("服务器配置的 name 项是必须的！");
                return false;
            }

            var server = bootstrap.AppServers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (server == null)
            {
                Console.WriteLine("未检测到已配置的服务器实例！");
                return false;
            }

            server.Stop();

            return true;
        }

        static bool StartCommand(IBootstrap bootstrap, string[] arguments)
        {
            var name = arguments[1];

            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("服务器配置的 name 项是必须的！");
                return false;
            }

            var server = bootstrap.AppServers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (server == null)
            {
                Console.WriteLine("未检测到已配置的服务器实例！");
                return false;
            }

            server.Start();

            return true;
        }

        static void ReadConsoleCommand(IBootstrap bootstrap)
        {
            var line = Console.ReadLine();

            if (string.IsNullOrEmpty(line))
            {
                ReadConsoleCommand(bootstrap);
                return;
            }

            if ("quit".Equals(line, StringComparison.OrdinalIgnoreCase))
                return;

            var cmdArray = line.Split(' ');

            ControlCommand cmd;

            if (!m_CommandHandlers.TryGetValue(cmdArray[0], out cmd))
            {
                Console.WriteLine("未知命令！");
                ReadConsoleCommand(bootstrap);
                return;
            }

            try
            {
                if(cmd.Handler(bootstrap, cmdArray))
                    Console.WriteLine("命令执行成功！");
            }
            catch (Exception e)
            {
                Console.WriteLine("命令执行失败！ " + e.Message + Environment.NewLine + e.StackTrace);
            }

            ReadConsoleCommand(bootstrap);
        }

        static void RunAsService()
        {
            ServiceBase[] servicesToRun = new ServiceBase[] { new MainService() };

            ServiceBase.Run(servicesToRun);
        }
    }

    internal class ConsoleTextList
    {
        public ConsoleTextList() { }

        public ConsoleTextList(ConsoleText text) 
        {
            this.Add(text);
        }

        public ConsoleTextList(ConsoleText[] texts) 
        {
            this.AddRange(texts);
        }

        private List<ConsoleText> list = new List<ConsoleText>();

        public List<ConsoleText> List
        {
            get { return this.list; }
        }

        public void Add(ConsoleText text)
        {
            this.list.Add(text);
        }

        public void AddRange(ConsoleText[] texts)
        {
            foreach (ConsoleText text in texts)
            {
                this.Add(text);
            }
        }
    }

    internal class ConsoleText
    {
        public ConsoleText() { }

        public ConsoleText(string text, ConsoleColor color = ConsoleColor.Gray, bool wrap = true)
        {
            this.Text = text;
            this.Color = color;
            this.Wrap = wrap;
        }

        // 文本
        public string Text { get; set; }

        // 文本颜色
        public ConsoleColor Color { get; set; }

        // 换行，true 则用 Console.WriteLine()
        public bool Wrap { get; set; }
    }

    internal static class CommonString
    {
        public const string HorizontalLine = "-------------------------------------------------------------------------------";
        public const string CommonLine = "+-----------------------------------------------------------------------------+";
        public const string TableLine = "+---------+-------------------------------------------------------------------+";
        public const string DevideLine = "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~";
        public const string SpaceLine = "|                                                                             |";
    }
}