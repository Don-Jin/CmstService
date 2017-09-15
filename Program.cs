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

                // ϵͳ����Ĭ�������治�ܽ���
                return;
            }
            
            // ����
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
                    new ConsoleText("��ӭ�������д�������ͨ�ź�̨��", ConsoleColor.Cyan, false),
                    new ConsoleText("                     |", ConsoleColor.White),
                    new ConsoleText(CommonString.SpaceLine),
                    new ConsoleText(CommonString.CommonLine),
                    new ConsoleText(""),
                    new ConsoleText(CommonString.DevideLine, ConsoleColor.Green),
                    new ConsoleText(""),
                    new ConsoleText(CommonString.TableLine),
                    new ConsoleText("|   ", ConsoleColor.White, false),
                    new ConsoleText("����", ConsoleColor.Cyan, false),
                    new ConsoleText("  |                               ", ConsoleColor.White, false),
                    new ConsoleText("����", ConsoleColor.Cyan, false),
                    new ConsoleText("                                |", ConsoleColor.White),
                    new ConsoleText(CommonString.TableLine),
                    new ConsoleText("|    ", ConsoleColor.White, false),
                    new ConsoleText("r", ConsoleColor.Cyan, false),
                    new ConsoleText("    |  ", ConsoleColor.White, false),
                    new ConsoleText("�Կ���̨ģʽ����ϵͳ", ConsoleColor.Cyan, false),
                    new ConsoleText("                                             |", ConsoleColor.White),
                    new ConsoleText(CommonString.TableLine),
                    new ConsoleText("|    ", ConsoleColor.White, false),
                    new ConsoleText("i", ConsoleColor.Cyan, false),
                    new ConsoleText("    |  ", ConsoleColor.White, false),
                    new ConsoleText("��Ӧ�ð�װΪ Windows ����", ConsoleColor.Cyan, false),
                    new ConsoleText("                                        |", ConsoleColor.White),
                    new ConsoleText(CommonString.TableLine),
                    new ConsoleText("|    ", ConsoleColor.White, false),
                    new ConsoleText("u", ConsoleColor.Cyan, false),
                    new ConsoleText("    |  ", ConsoleColor.White, false),
                    new ConsoleText("��Ӧ�ô� Windows ����ж��", ConsoleColor.Cyan, false),
                    new ConsoleText("                                        |", ConsoleColor.White),
                    new ConsoleText(CommonString.TableLine),
                    new ConsoleText(""),
                    new ConsoleText(""),
                    new ConsoleText("�����������..."),
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
                    Console.WriteLine("��Ч������");
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
            Console.WriteLine("��лʹ�� CmstService!");

            //CheckCanSetConsoleColor();

            Console.WriteLine("��ʼ����...");

            IBootstrap bootstrap = BootstrapFactory.CreateBootstrap();

            if (!bootstrap.Initialize())
            {
                SetConsoleColor(ConsoleColor.Red);

                Console.WriteLine("ϵͳ��ʼ��ʧ�ܣ����������־�Ի�ȡ������Ϣ��");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("������...");

            var result = bootstrap.Start();

            Console.WriteLine(CommonString.HorizontalLine);

            foreach (var server in bootstrap.AppServers)
            {
                if (server.State == ServerState.Running)
                {
                    SetConsoleColor(ConsoleColor.Green);
                    Console.WriteLine("- {0} �����ɹ�", server.Name);
                }
                else
                {
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine("- {0} ����ʧ��", server.Name);
                }
            }

            Console.ResetColor();
            Console.WriteLine(CommonString.HorizontalLine);

            switch(result)
            {
                case(StartResult.None):
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine("δ�����κη������������������ļ���");
                    Console.ReadKey();
                    return;

                case(StartResult.Success):
                    Console.WriteLine("CmstService ϵͳ�ѳɹ�������");
                    break;

                case (StartResult.Failed):
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine("ϵͳ��ʼ��ʧ�ܣ����������־�Ի�ȡ������Ϣ��");
                    Console.ReadKey();
                    return;

                case (StartResult.PartialSuccess):
                    SetConsoleColor(ConsoleColor.Red);
                    Console.WriteLine("���ַ������������ɹ���������ȫ������ʧ�ܣ����������־�Ի�ȡ������Ϣ��");
                    break;
            }

            Console.ResetColor();
            Console.WriteLine("���� \"quit\" ����ֹϵͳ���С�");

            RegisterCommands();

            ReadConsoleCommand(bootstrap);

            bootstrap.Stop();

            Console.WriteLine("CmstService ϵͳ�ѳɹ���ֹ��");
        }

        private static void RegisterCommands()
        {
            AddCommand("List", "�г����з�����ʵ��", ListCommand);
            AddCommand("Start", "����һ��������ʵ��: Start {ServerName}", StartCommand);
            AddCommand("Stop", "��ֹһ��������ʵ��: Stop {ServerName}", StopCommand);
        }

        private static void RunAsController(string[] arguments)
        {
            if (arguments == null || arguments.Length < 2)
            {
                Console.WriteLine("��Ч������");
                return;
            }

            var config = ConfigurationManager.GetSection("socketServer") as IConfigurationSource;

            if (config == null)
            {
                Console.WriteLine("ϵͳ���������ļ��������񣬵���δ�ҵ���Ч������Ϣ��");
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
                    Console.WriteLine("����: CmstService ϵͳ��δ������");
                    return;
                }
            }

            RegisterCommands();

            var cmdName = arguments[1];

            ControlCommand cmd;

            if (!m_CommandHandlers.TryGetValue(cmdName, out cmd))
            {
                Console.WriteLine("δ֪���");
                return;
            }

            try
            {
                if (cmd.Handler(bootstrap, arguments.Skip(1).ToArray()))
                    Console.WriteLine("����ִ�гɹ���");
            }
            catch (Exception e)
            {
                Console.WriteLine("����ִ��ʧ�ܣ� " + e.Message);
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
                Console.WriteLine("���������õ� name ���Ǳ���ģ�");
                return false;
            }

            var server = bootstrap.AppServers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (server == null)
            {
                Console.WriteLine("δ��⵽�����õķ�����ʵ����");
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
                Console.WriteLine("���������õ� name ���Ǳ���ģ�");
                return false;
            }

            var server = bootstrap.AppServers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (server == null)
            {
                Console.WriteLine("δ��⵽�����õķ�����ʵ����");
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
                Console.WriteLine("δ֪���");
                ReadConsoleCommand(bootstrap);
                return;
            }

            try
            {
                if(cmd.Handler(bootstrap, cmdArray))
                    Console.WriteLine("����ִ�гɹ���");
            }
            catch (Exception e)
            {
                Console.WriteLine("����ִ��ʧ�ܣ� " + e.Message + Environment.NewLine + e.StackTrace);
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

        // �ı�
        public string Text { get; set; }

        // �ı���ɫ
        public ConsoleColor Color { get; set; }

        // ���У�true ���� Console.WriteLine()
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