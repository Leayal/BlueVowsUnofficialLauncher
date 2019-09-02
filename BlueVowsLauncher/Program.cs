using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Loader;
using Leayal.ApplicationController;
using System.Xml;

namespace BlueVowsLauncher
{
    static class Program
    {
        const string ProgramID = "DLeayal-BlueVowsUnofficialLauncher==";

        [STAThread]
        static void Main(string[] args)
        {
            Controller controller = new Controller();
            controller.Run(args);
        }

        class Controller : ApplicationBase
        {
            private App app;
            public Controller() : base(ProgramID) { }

            protected override void OnStartup(StartupEventArgs eventArgs)
            {
                this.app = new App();
                this.app.Run();
            }

            protected override void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
            {
                if (this.app != null)
                {
                    this.app.MainWindow.Activate();
                }
                base.OnStartupNextInstance(eventArgs);
            }
        }
    }
}
