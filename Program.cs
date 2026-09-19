using System;
using System.Threading;
using System.Windows.Forms;

namespace EnvKeySender
{
    internal static class Program
    {
        private const string AppMutexName = "EnvKeySender_SingleInstance";

        [STAThread]
        static void Main()
        {
            bool createdNew;
            using var mutex = new Mutex(true, AppMutexName, out createdNew);

            if (!createdNew)
            {
                ErrorLogger.Write("Another instance is already running. Exiting.");
                return;
            }

            try
            {
                ErrorLogger.Write("Application started");
                ApplicationConfiguration.Initialize();
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                ErrorLogger.WriteException(ex, "Unhandled exception in Program.Main");
                throw;
            }
            finally
            {
                ErrorLogger.Write("Application exiting");
            }
        }
    }
}
