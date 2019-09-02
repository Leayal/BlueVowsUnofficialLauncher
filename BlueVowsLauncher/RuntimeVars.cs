using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace BlueVowsLauncher
{
    static class RuntimeVars
    {
        static RuntimeVars()
        {
            var assembly = Assembly.GetEntryAssembly();
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                RootPath = Path.GetDirectoryName(new Uri(assembly.Location).LocalPath);
            }
            else if (!string.IsNullOrEmpty(assembly.CodeBase))
            {
                RootPath = assembly.CodeBase;
            }
            else
            {
                RootPath = Environment.CurrentDirectory;
            }
            // RootPath = @"E:\Entertainment\Blue Vows\苍蓝誓约";
        }

        internal static readonly string RootPath;
    }
}
