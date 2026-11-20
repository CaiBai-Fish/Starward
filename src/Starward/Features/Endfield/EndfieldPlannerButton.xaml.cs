using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Core;
using Starward.Core.HoYoPlay;
using System;

namespace Starward.Features.Endfield;

/// <summary>
/// 终末地启动器页面右边栏的终末地规划器（CEP）入口。
/// </summary>
[INotifyPropertyChanged]
public sealed partial class EndfieldPlannerButton : UserControl
{

    private readonly ILogger<EndfieldPlannerButton> _logger = AppConfig.GetLogger<EndfieldPlannerButton>();


    private static WeakReference<EndfieldPlannerWindow>? _plannerWindow;


    public EndfieldPlannerButton()
    {
        this.InitializeComponent();
        this.Visibility = Visibility.Collapsed;
    }



    public GameId? CurrentGameId
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                UpdateVisibility();
            }
        }
    }



    private bool IsEndfieldEnabled => CurrentGameId?.GameBiz.Game is GameBiz.endfield;



    private void UpdateVisibility()
    {
        this.Visibility = IsEndfieldEnabled ? Visibility.Visible : Visibility.Collapsed;
    }



    private void Button_EndfieldPlanner_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_plannerWindow is not null && _plannerWindow.TryGetTarget(out EndfieldPlannerWindow? window))
            {
                window.Activate();
                return;
            }
            window = new EndfieldPlannerWindow();
            _plannerWindow = new WeakReference<EndfieldPlannerWindow>(window);
            window.Closed += (_, _) => _plannerWindow = null;
            window.Activate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open Endfield planner window");
        }
    }



}
