using System;
using System.Windows.Forms;

namespace WendysDash
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e) {
                MessageBox.Show(e.Exception.Message, "Wendy's Dash", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            string folder = args.Length == 2 && args[0] == "--game-dir" ? args[1] : SteamLocator.FindGame();
            Application.Run(new MainForm(new PatchService(), folder));
        }
    }
}
