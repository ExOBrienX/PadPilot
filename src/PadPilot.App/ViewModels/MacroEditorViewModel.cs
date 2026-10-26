using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PadPilot.App.Services;
using PadPilot.Core.Services;

namespace PadPilot.App.ViewModels;

/// <summary>Un paso de la macro en el editor.</summary>
public sealed partial class MacroStepViewModel : ObservableObject
{
    private readonly MacroStep _step;
    private readonly Action _changed;

    public MacroStepViewModel(MacroStep step, Action changed, Action<MacroStepViewModel> remove)
    {
        _step = step;
        _changed = changed;
        RemoveCommand = new RelayCommand(() => remove(this));
        Action = ButtonActions.StepActions.FirstOrDefault(a => a.Id == step.Action) ?? ButtonActions.StepActions[0];
        HoldMs = step.HoldMs;
        DelayMs = step.DelayMs;
    }

    public MacroStep Step => _step;

    public IReadOnlyList<ButtonAction> Actions => ButtonActions.StepActions;

    public IRelayCommand RemoveCommand { get; }

    [ObservableProperty] public partial ButtonAction Action { get; set; }

    [ObservableProperty] public partial decimal? HoldMs { get; set; }

    [ObservableProperty] public partial decimal? DelayMs { get; set; }

    partial void OnActionChanged(ButtonAction value)
    {
        if (value is null) return;
        _step.Action = value.Id;
        _changed();
    }

    partial void OnHoldMsChanged(decimal? value)
    {
        _step.HoldMs = (int)Math.Clamp(value ?? 60, 1, 10000);
        _changed();
    }

    partial void OnDelayMsChanged(decimal? value)
    {
        _step.DelayMs = (int)Math.Clamp(value ?? 0, 0, 10000);
        _changed();
    }
}

/// <summary>Una macro en el editor.</summary>
public sealed partial class MacroItemViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly Action _renamed;

    public MacroItemViewModel(MacroDefinition definition, Action changed, Action renamed)
    {
        Definition = definition;
        _changed = changed;
        _renamed = renamed;
        Name = definition.Name;
        RepeatWhileHeld = definition.RepeatWhileHeld;
        foreach (var step in definition.Steps)
            Steps.Add(new MacroStepViewModel(step, changed, RemoveStep));
    }

    public MacroDefinition Definition { get; }

    public ObservableCollection<MacroStepViewModel> Steps { get; } = [];

    [ObservableProperty] public partial string Name { get; set; }

    [ObservableProperty] public partial bool RepeatWhileHeld { get; set; }

    partial void OnNameChanged(string value)
    {
        Definition.Name = string.IsNullOrWhiteSpace(value) ? "Macro" : value.Trim();
        _renamed();
    }

    partial void OnRepeatWhileHeldChanged(bool value)
    {
        Definition.RepeatWhileHeld = value;
        _changed();
    }

    [RelayCommand]
    private void AddStep()
    {
        var step = new MacroStep { Action = ButtonActions.StepActions[0].Id };
        Definition.Steps.Add(step);
        Steps.Add(new MacroStepViewModel(step, _changed, RemoveStep));
        _changed();
    }

    private void RemoveStep(MacroStepViewModel step)
    {
        Definition.Steps.Remove(step.Step);
        Steps.Remove(step);
        _changed();
    }
}

/// <summary>
/// Editor de macros (compartido por todos los mandos). Una macro es una secuencia de teclas y/o botones Xbox
/// con tiempos; se asigna a un botón desde la pestaña Botones.
/// </summary>
public sealed partial class MacroEditorViewModel : ObservableObject
{
    private readonly ProfileStore _profiles;

    public MacroEditorViewModel(ProfileStore profiles)
    {
        _profiles = profiles;
        foreach (var macro in profiles.Macros)
            Macros.Add(new MacroItemViewModel(macro, Save, Renamed));
        ButtonActions.SetMacros(profiles.Macros);
        SelectedMacro = Macros.FirstOrDefault();
    }

    /// <summary>Se crearon, renombraron o borraron macros: hay que refrescar los combos del remapeo.</summary>
    public event Action? CatalogChanged;

    public ObservableCollection<MacroItemViewModel> Macros { get; } = [];

    [ObservableProperty] public partial MacroItemViewModel? SelectedMacro { get; set; }

    public bool HasSelection => SelectedMacro is not null;

    partial void OnSelectedMacroChanged(MacroItemViewModel? value) => OnPropertyChanged(nameof(HasSelection));

    [RelayCommand]
    private void AddMacro()
    {
        var macro = new MacroDefinition
        {
            Name = Loc.T("macros.defaultName", Macros.Count + 1),
            Steps = { new MacroStep { Action = ButtonActions.StepActions[0].Id } },
        };
        _profiles.Macros.Add(macro);
        var vm = new MacroItemViewModel(macro, Save, Renamed);
        Macros.Add(vm);
        SelectedMacro = vm;
        Renamed();
    }

    [RelayCommand]
    private void DeleteMacro()
    {
        if (SelectedMacro is not { } vm) return;
        _profiles.Macros.Remove(vm.Definition);
        Macros.Remove(vm);
        SelectedMacro = Macros.FirstOrDefault();
        Renamed();
    }

    private void Save() => _profiles.SaveDeferred();

    private void Renamed()
    {
        ButtonActions.SetMacros(_profiles.Macros);
        Save();
        CatalogChanged?.Invoke();
    }
}
