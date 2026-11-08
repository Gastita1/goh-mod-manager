using System;
using System.Windows.Forms;

namespace GoHMM
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s2, System.Threading.ThreadExceptionEventArgs e2)
                {
                    AppConfig.Log("UI异常: " + e2.Exception);
                    MessageBox.Show(e2.Exception.ToString(), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                AppConfig.Log("崩溃: " + ex);
                MessageBox.Show(ex.ToString(), "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
