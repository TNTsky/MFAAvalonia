﻿using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MFAAvalonia.Configuration;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using Semver;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using System;
using System.Reflection;

namespace MFAAvalonia.ViewModels.Windows;

public partial class RootViewModel : ViewModelBase
{
    [ObservableProperty] private bool _idle = true;
    [ObservableProperty] private bool _isWindowVisible = true;

    [ObservableProperty] private bool _isRunning;

    partial void OnIsRunningChanged(bool value)
    {
        Idle = !value;
    }
    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var major = version.Major;
            var minor = version.Minor >= 0 ? version.Minor : 0;
            var patch = version.Build >= 0 ? version.Build : 0;
            return SemVersion.Parse($"{major}.{minor}.{patch}").ToString();
        }
    }
    [ObservableProperty] private string? _resourceName;

    [ObservableProperty] private bool _isResourceNameVisible;

    [ObservableProperty] private string? _resourceVersion;

    [ObservableProperty] private string? _customTitle;

    [ObservableProperty] private bool _isCustomTitleVisible;

    [ObservableProperty] private bool _lockController;

    [ObservableProperty] private bool _isDebugMode = ConfigurationManager.Maa.GetValue(ConfigurationKeys.Recording, false)
        || ConfigurationManager.Maa.GetValue(ConfigurationKeys.SaveDraw, false)
        || ConfigurationManager.Maa.GetValue(ConfigurationKeys.ShowHitDraw, false);
    private bool _shouldTip = true;
    [ObservableProperty] private bool _isUpdating;

    public void SetUpdating(bool isUpdating)
    {
        IsUpdating = isUpdating;
    }

    partial void OnIsDebugModeChanged(bool value)
    {
        if (value && _shouldTip)
        {
            Instances.DialogManager.CreateDialog().OfType(NotificationType.Warning).WithContent("DebugModeWarning".ToLocalization()).WithActionButton("Ok".ToLocalization(), dialog => { }, true).TryShow();
            _shouldTip = false;
        }
    }

    public void ShowResourceName(string name)
    {
        ResourceName = name;
        IsResourceNameVisible = true;
    }

    public void ShowResourceVersion(string version)
    {
        ResourceVersion = version;
    }

    public void ShowCustomTitle(string title)
    {
        CustomTitle = title;
        IsCustomTitleVisible = true;
        IsResourceNameVisible = false;
    }
    [RelayCommand]
    public void ToggleVisible()
    {
        IsWindowVisible = !IsWindowVisible;
    }
}
