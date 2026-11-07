using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Starward.Helpers.EndfieldLaunch;

/// <summary>
/// 以管理员权限运行的终末地启动辅助进程。游戏会校验直接父进程必须是官方签名的 Games.exe，
/// 这里把游戏目录中一个官方签名的文件复制为临时 Games.exe，以挂起状态作为父进程，
/// 再通过 PROC_THREAD_ATTRIBUTE_PARENT_PROCESS 创建游戏进程（参考 Xel-Launcher / EndfieldNoLauncher）。
/// </summary>
internal static class EndfieldLaunchWorker
{

    public const string Command = "--endfield-launch-worker";



    public static void Run(string[] args)
    {
        try
        {
            if (args.Length < 3)
            {
                throw new ArgumentException("Endfield launch worker arguments are invalid.");
            }
            string gamePath = Path.GetFullPath(args[1]);
            string arguments = args[2] ?? "";
            Log($"worker start: {gamePath} args={arguments}");
            RunGameAsync(gamePath, arguments).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log("worker error: " + ex);
            Environment.ExitCode = 1;
        }
    }



    private static async Task RunGameAsync(string gamePath, string arguments)
    {
        using GameDecoyParentBridge? bridge = GameDecoyParentBridge.TryCreate(gamePath);
        Process? game = null;
        if (bridge is not null)
        {
            try
            {
                game = bridge.StartGame(Path.Combine(gamePath, "Endfield.exe"), arguments, gamePath);
                Log($"game pid={game.Id}");
            }
            catch (Exception ex)
            {
                Log("parent bridge failed: " + ex);
                bridge.Dispose();
            }
        }
        game ??= StartDirectly(gamePath, arguments);
        using (game)
        {
            await game.WaitForExitAsync();
        }
    }



    private static Process StartDirectly(string gamePath, string arguments)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(gamePath, "Endfield.exe"),
            Arguments = arguments ?? "",
            WorkingDirectory = gamePath,
            UseShellExecute = true,
        }) ?? throw new InvalidOperationException("Failed to create Endfield process.");
    }



    private static void Log(string message)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "StarwardEndfieldLaunch.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { }
    }


}
