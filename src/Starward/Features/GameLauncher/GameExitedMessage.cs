using Starward.Core;

namespace Starward.Features.GameLauncher;

/// <summary>
/// 游戏进程结束时发送，用于“游戏退出后打开启动器”功能。
/// </summary>
internal class GameExitedMessage
{

    public GameBiz GameBiz { get; }

    public GameExitedMessage(GameBiz gameBiz)
    {
        GameBiz = gameBiz;
    }

}
