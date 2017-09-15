using System;
using System.IO;
using System.Timers;
using System.ServiceProcess;
using SuperSocket.SocketBase;
using SuperSocket.SocketEngine;

namespace CmstService
{
    partial class MainService : ServiceBase
    {
        private IBootstrap m_Bootstrap;

        public MainService()
        {
            InitializeComponent();
            
            // 创建 Socket 服务
            m_Bootstrap = BootstrapFactory.CreateBootstrap();
        }

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            
            // 延时对象
            Timer timerDelay = new Timer(15000);
            timerDelay.Elapsed += new ElapsedEventHandler(delegate(object sender, ElapsedEventArgs e) {
                // 初始化并启动服务
                if (m_Bootstrap.Initialize() && m_Bootstrap.Start() == StartResult.Success)
                {
                    timerDelay.Enabled = false;
                    timerDelay.Close();
                }
            });
            timerDelay.Start();
        }

        protected override void OnStop()
        {
            m_Bootstrap.Stop();
            base.OnStop();
        }

        protected override void OnShutdown()
        {
            m_Bootstrap.Stop();
            base.OnShutdown();
        }
    }
}
