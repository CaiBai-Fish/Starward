using Grpc.Core;
using Microsoft.Extensions.Logging;
using Starward.Core.HoYoPlay;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.RPC.GameInstall;

internal class GameInstallController : GameInstaller.GameInstallerBase
{

    private readonly ILogger<GameInstallController> _logger;

    private readonly GameInstallService _gameInstallService;

    private readonly GameInstallHelper _gameInstallHelper;

    private readonly GameUninstallService _gameUninstallService;


    public GameInstallController(ILogger<GameInstallController> logger, GameInstallService gameInstallService, GameInstallHelper gameInstallHelper, GameUninstallService gameUninstallService)
    {
        _logger = logger;
        _gameInstallService = gameInstallService;
        _gameInstallHelper = gameInstallHelper;
        _gameUninstallService = gameUninstallService;
    }






    public override Task<GameInstallContextDTO> StartOrContinueTask(GameInstallRequest request, ServerCallContext context)
    {
        return Task.FromResult(_gameInstallService.StartOrContinueTask(request));
    }




    public override Task<GameInstallContextDTO> PauseTask(GameInstallRequest request, ServerCallContext context)
    {
        return Task.FromResult(_gameInstallService.PauseTask(request));
    }




    public override Task<GameInstallContextDTO> StopTask(GameInstallRequest request, ServerCallContext context)
    {
        return Task.FromResult(_gameInstallService.StopTask(request));
    }




    public override Task<RateLimiterMessage> SetRateLimiter(RateLimiterMessage request, ServerCallContext context)
    {
        int limit = _gameInstallHelper.SetRateLimiter(request.BytesPerSecond);
        return Task.FromResult(new RateLimiterMessage { BytesPerSecond = limit });
    }




    public override async Task<UninstallGameResponse> UninstallGame(UninstallGameRequest request, ServerCallContext context)
    {
        try
        {
            GameId gameId = new GameId { GameBiz = request.GameBiz, Id = request.GameId };
            if (_gameInstallService.TryGetTask(gameId, out GameInstallContext? task))
            {
                // 停止正在进行的安装任务
                task.Cancel(GameInstallState.Stop);
                await Task.Delay(3000);
            }
            await _gameUninstallService.UninstallGameAsync(request, context.CancellationToken);
            return new UninstallGameResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstall game ({gameBiz}): {path}", request.GameBiz, request.InstallPath);
            return new UninstallGameResponse { Success = false, ErrorMessage = ex.Message };
        }
    }




    #region GetTaskProgress



    private IServerStreamWriter<GameInstallContextDTO>? _taskProgressStream;

    private GameInstallContext? _lastTaskContext;


    public override async Task GetTaskProgress(EmptyMessage request, IServerStreamWriter<GameInstallContextDTO> responseStream, ServerCallContext context)
    {
        try
        {
            _taskProgressStream = responseStream;
            _lastTaskContext = _gameInstallService.CurrentTask;
            _gameInstallService.TaskStateChanged += _gameInstallService_TaskStateChanged;
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (await timer.WaitForNextTickAsync(context.CancellationToken))
            {
                GameInstallContext? task = _gameInstallService.CurrentTask;
                if (task is null)
                {
                    // 任务结束后 CurrentTask 会被立即置空，终态帧可能来不及送达就关闭了流，
                    // 客户端会一直停留在最后收到的下载中进度（卡在 100%），这里补发一次终态。
                    if (_lastTaskContext is { } last && last.State is GameInstallState.Stop or GameInstallState.Finish or GameInstallState.Error)
                    {
                        await responseStream.WriteAsync(GameInstallContextDTO.FromTask(last));
                    }
                    return;
                }
                _lastTaskContext = task;
                task.RefreshSpeed();
                await responseStream.WriteAsync(GameInstallContextDTO.FromTask(task));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report game install task progress");
        }
        finally
        {
            _gameInstallService.TaskStateChanged -= _gameInstallService_TaskStateChanged;
            _taskProgressStream = null;
        }
    }







    private async void _gameInstallService_TaskStateChanged(object? sender, GameInstallContext e)
    {
        if (_taskProgressStream is not null)
        {
            await _taskProgressStream.WriteAsync(GameInstallContextDTO.FromTask(e));
        }
    }


    #endregion





}
