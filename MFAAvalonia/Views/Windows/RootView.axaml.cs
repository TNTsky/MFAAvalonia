using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MFAAvalonia.Configuration;
using MFAAvalonia.Extensions;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using MFAAvalonia.Helper.ValueType;
using MFAAvalonia.Views.UserControls;
using Newtonsoft.Json;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MFAAvalonia.Views.Windows;

public partial class RootView : SukiWindow
{
    public RootView()
    {
        // 先初始化组件
        InitializeComponent();
        
        // 设置事件处理
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
            {
                HandleWindowStateChange();
            }
        };
        
        // 为窗口大小变化添加监听，保存窗口大小
        SizeChanged += (s, e) => SaveWindowSize();
        
        // 先初始化组件和处理事件绑定，然后才恢复窗口大小和加载UI
        // 仅使用一个Loaded事件，统一处理初始化逻辑
        Loaded += (_, _) => 
        {
            LoggerHelper.Info("窗口Loaded事件触发，开始恢复窗口大小");
            
            // 确保在UI线程上执行
            Dispatcher.UIThread.Post(() =>
            {
                // 先恢复窗口大小
                RestoreWindowSize();
                
                // 然后加载UI
                LoadUI();
            });
        };
        
        MaaProcessor.Instance.InitializeData();
    }

    private void HandleWindowStateChange()
    {
        if (ConfigurationManager.Current.GetValue(ConfigurationKeys.ShouldMinimizeToTray, false))
        {
            Instances.RootViewModel.IsWindowVisible = WindowState != WindowState.Minimized;
        }
    }

    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = !ConfirmExit();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        ConfigurationManager.Current.SetValue(ConfigurationKeys.TaskItems, Instances.TaskQueueViewModel.TaskItemViewModels.ToList().Select(model => model.InterfaceItem));

        // 确保窗口大小被保存
        SaveWindowSize();

        MaaProcessor.Instance.SetTasker();
        GlobalHotkeyService.Shutdown();
        base.OnClosed(e);
    }

    public bool ConfirmExit()
    {
        if (!Instances.RootViewModel.IsRunning)
            return true;

        bool result = false;
        // var frame = new DispatcherFrame();
        var textBlock = new TextBlock
        {
            Text = "ConfirmExitText".ToLocalization()
        };
        Instances.DialogManager.CreateDialog()
            .WithTitle("ConfirmExitTitle".ToLocalization())
            .WithContent(textBlock).OfType(NotificationType.Warning)
            .WithActionButton("Yes".ToLocalization(), _ =>
            {
                result = true;
                Instances.ApplicationLifetime.Shutdown();
            }, dismissOnClick: true, "Flat", "Accent")
            .WithActionButton("No".ToLocalization(), _ =>
            {
                result = false;
            }, dismissOnClick: true).TryShow();
        return result;
    }

    private void ToggleWindowTopMost(object sender, RoutedEventArgs e)
    {
        Topmost = btnPin.IsChecked == true;
    }

    public static void AddLogByColor(string content,
        string brush = "Gray",
        string weight = "Regular",
        bool showTime = true)
        =>
            Instances.TaskQueueViewModel.AddLog(content, brush, weight, showTime);


    public static void AddLog(string content,
        IBrush? brush = null,
        string weight = "Regular",
        bool showTime = true)
        =>
            Instances.TaskQueueViewModel.AddLog(content, brush, weight, showTime);

    public static void AddLogByKey(string key, IBrush? brush = null, bool transformKey = true, params string[] formatArgsKeys)
        => Instances.TaskQueueViewModel.AddLogByKey(key, brush, transformKey, formatArgsKeys);

#pragma warning  disable CS4014 // 由于此调用不会等待，因此在此调用完成之前将会继续执行当前方法。请考虑将 "await" 运算符应用于调用结果。
    public void LoadUI()
    {
        
        DispatcherHelper.RunOnMainThread(async () =>
        {
            await Task.Delay(300);
            Instances.TaskQueueViewModel.CurrentController = (MaaProcessor.Interface?.Controller?.FirstOrDefault()?.Type).ToMaaControllerTypes(Instances.TaskQueueViewModel.CurrentController);
            if (!Convert.ToBoolean(GlobalConfiguration.GetValue(ConfigurationKeys.NoAutoStart, bool.FalseString))
                && ConfigurationManager.Current.GetValue(ConfigurationKeys.BeforeTask, "None").Contains("Startup", StringComparison.OrdinalIgnoreCase))
            {
                MaaProcessor.Instance.TaskQueue.Enqueue(new MFATask
                {
                    Name = "启动前",
                    Type = MFATask.MFATaskType.MFA,
                    Action = async () => await MaaProcessor.Instance.WaitSoftware(),
                });
                MaaProcessor.Instance.Start(!ConfigurationManager.Current.GetValue(ConfigurationKeys.BeforeTask, "None").Contains("And", StringComparison.OrdinalIgnoreCase), checkUpdate: true);
            }
            else
            {
                var isAdb = Instances.TaskQueueViewModel.CurrentController == MaaControllerTypes.Adb;

                AddLogByKey("ConnectingTo", null, true, isAdb ? "Emulator" : "Window");

                Instances.TaskQueueViewModel.TryReadAdbDeviceFromConfig();
                MaaProcessor.Instance.TaskQueue.Enqueue(new MFATask
                {
                    Name = "连接检测",
                    Type = MFATask.MFATaskType.MFA,
                    Action = async () => await MaaProcessor.Instance.TestConnecting(),
                });
                MaaProcessor.Instance.Start(true, checkUpdate: true);
            }
            try
            {
                var tempMFADir = Path.Combine(AppContext.BaseDirectory, "temp_mfa");
                if (Directory.Exists(tempMFADir)) 
                    Directory.Delete(tempMFADir, true);
                
                var tempMaaDir = Path.Combine(AppContext.BaseDirectory, "temp_maafw");
                if (Directory.Exists(tempMaaDir)) 
                    Directory.Delete(tempMaaDir, true);
                
                var tempResDir = Path.Combine(AppContext.BaseDirectory, "temp_res");
                if (Directory.Exists(tempResDir)) 
                    Directory.Delete(tempResDir, true);
            }
            catch (Exception e)
            {
                LoggerHelper.Error(e);
            }
            GlobalConfiguration.SetValue(ConfigurationKeys.NoAutoStart, bool.FalseString);

            Instances.RootViewModel.LockController = (MaaProcessor.Interface?.Controller?.Count ?? 0) < 2;
            ConfigurationManager.Current.SetValue(ConfigurationKeys.EnableEdit, ConfigurationManager.Current.GetValue(ConfigurationKeys.EnableEdit, false));
            foreach (var task in Instances.TaskQueueViewModel.TaskItemViewModels)
            {
                if (task.InterfaceItem?.Option is { Count: > 0 } || task.InterfaceItem?.Document != null || task.InterfaceItem?.Repeatable == true)
                {
                    task.EnableSetting = true;
                    break;
                }
            }
            if (!string.IsNullOrWhiteSpace(MaaProcessor.Interface?.Message))
            {
                ToastHelper.Info(MaaProcessor.Interface.Message);
            }

        });

        TaskManager.RunTaskAsync(async () =>
        {
            await Task.Delay(1000);
            DispatcherHelper.RunOnMainThread(() =>
            {
                Instances.AnnouncementViewModel.CheckAnnouncement();
                if (ConfigurationManager.Current.GetValue(ConfigurationKeys.AutoMinimize, false))
                {
                    WindowState = WindowState.Minimized;
                }
                if (ConfigurationManager.Current.GetValue(ConfigurationKeys.AutoHide, false))
                {
                    Hide();
                }
            });
        });
    }

    public void ClearTasks(Action? action = null)
    {
        DispatcherHelper.RunOnMainThread(() =>
        {
            Instances.TaskQueueViewModel.TaskItemViewModels = new();
            action?.Invoke();
        });
    }

    private void RestoreWindowSize()
    {
        try
        {
            var configName = ConfigurationManager.Current.FileName;
            var widthStr = ConfigurationManager.Current.GetValue(ConfigurationKeys.MainWindowWidth, "");
            var heightStr = ConfigurationManager.Current.GetValue(ConfigurationKeys.MainWindowHeight, "");
            
            LoggerHelper.Info($"正在恢复窗口大小: 宽度={widthStr}, 高度={heightStr}, 配置={configName}");
            
            if (!string.IsNullOrEmpty(widthStr) && !string.IsNullOrEmpty(heightStr))
            {
                if (double.TryParse(widthStr, out double width) && 
                    double.TryParse(heightStr, out double height))
                {
                    if (width > 100 && height > 100) // 确保有效的窗口大小
                    {
                        // 直接设置窗口大小，确保在UI线程上执行
                        Width = width;
                        Height = height;
                        LoggerHelper.Info($"窗口大小恢复成功: 宽度={width}, 高度={height}");
                        
                        // 再次保存以确保配置持久化
                        SaveWindowSize();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"恢复窗口大小失败: {ex.Message}");
        }
    }

    private void SaveWindowSize()
    {
        try
        {
            // 获取当前窗口大小
            double width = Width;
            double height = Height;
            if (width > 100 && height > 100) // 确保有效的窗口大小
            {
                // 保存窗口大小到配置并立即写入文件
                ConfigurationManager.Current.SetValue(ConfigurationKeys.MainWindowWidth, width.ToString());
                ConfigurationManager.Current.SetValue(ConfigurationKeys.MainWindowHeight, height.ToString());
                ConfigurationManager.SaveConfiguration(ConfigurationManager.Current.FileName);

                LoggerHelper.Info($"已保存窗口大小: 宽度={width}, 高度={height}, 配置={ConfigurationManager.Current.FileName}");
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"保存窗口大小失败: {ex.Message}");
        }
    }
}
