using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using Vector2 = System.Numerics.Vector2;

namespace SkillGems;

public class SkillGems : BaseSettingsPlugin<SkillGemsSettings>
{
    private CancellationTokenSource _gemLevelingCts;
    private Task _gemLevelingTask;
    private Vector2 _mousePosition;

    public override bool Initialise()
    {
        Input.RegisterKey(Settings.Run);
        return true;
    }

    public void Enable()
    {
        _gemLevelingCts = new CancellationTokenSource();
    }

    public void Disable()
    {
        _gemLevelingCts.Cancel();
    }

    private void SetCursorPos(Vector2 v)
    {
        Input.SetCursorPos(GameController.Window.GetWindowRectangleTimeCache.TopLeft.ToVector2Num() + v);
    }

    private void SetCursorPos(Element e)
    {
        SetCursorPos(e.GetClientRectCache.Center.ToVector2Num());
    }

    public override Job Tick()
    {
        if (Settings.UseMagicInput.Value && !Settings.AutomaticLeveling.Value && !Input.IsKeyDown(Settings.Run.Value) ||
            !Settings.UseMagicInput.Value && !Input.IsKeyDown(Settings.Run.Value) ||
            !PanelVisible())
        {
            _gemLevelingCts?.Cancel();
        }
        else if (CanTick() && IsPlayerAlive() && AnythingToLevel() && PanelVisible() && _gemLevelingTask == null)
        {
            _mousePosition = Input.MousePositionNum;
            _gemLevelingCts = new CancellationTokenSource();
            _gemLevelingTask = Task.FromResult(BeginGemLevel(_gemLevelingCts.Token)).Unwrap();
            _gemLevelingTask.ContinueWith(
                task =>
                {
                    _gemLevelingTask = null;
                    if (!Settings.UseMagicInput.Value)
                        SetCursorPos(_mousePosition);
                });
        }

        return null;
    }

    private async Task BeginGemLevel(CancellationToken cancellationToken)
    {
        var gemsToLvlUpElements = GetLevelableGems();

        if (!gemsToLvlUpElements.Any()) return;

        var elementToClick = gemsToLvlUpElements.ToList().FirstOrDefault();

        if (Settings.UseMagicInput.Value)
        {
            var gemDelay = Settings.DelayBetweenEachGemClick.Value;

            GameController.PluginBridge.GetMethod<Action<GemLevelUpElement>>("MagicInput.GemLevelUp")(elementToClick);

            if (Settings.AddPingIntoDelay.Value)
                gemDelay += GameController.IngameState.ServerData.Latency;

            await Task.Delay(gemDelay, cancellationToken);
        }
        else
        {
            var actionDelay = Settings.DelayBetweenEachMouseEvent.Value;
            var gemDelay = Settings.DelayBetweenEachGemClick.Value;

            if (Settings.AddPingIntoDelay.Value)
            {
                actionDelay += GameController.IngameState.ServerData.Latency;
                gemDelay += GameController.IngameState.ServerData.Latency;
            }

            SetCursorPos(elementToClick?.GetChildAtIndex(1));
            await Task.Delay(actionDelay, cancellationToken);
            Input.LeftDown();
            await Task.Delay(actionDelay, cancellationToken);
            Input.LeftUp();
            await Task.Delay(gemDelay, cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested) return;
    }

    private bool PanelVisible()
    {
        return !(GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible ||
            GameController.Game.IngameState.IngameUi.Atlas.IsVisible ||
            GameController.Game.IngameState.IngameUi.TreePanel.IsVisible ||
            GameController.Game.IngameState.IngameUi.SyndicatePanel.IsVisible ||
            GameController.Game.IngameState.IngameUi.OpenRightPanel.IsVisible ||
            GameController.Game.IngameState.IngameUi.ChatTitlePanel.IsVisible ||
            GameController.Game.IngameState.IngameUi.DelveWindow.IsVisible);
    }

    private bool CanTick()
    {
        return !GameController.IsLoading &&
            GameController.Game.IngameState.ServerData.IsInGame &&
            GameController.Player != null &&
            GameController.Player.Address != 0 &&
            GameController.Player.IsValid &&
            GameController.Window.IsForeground();
    }

    private bool IsPlayerAlive()
    {
        return GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Life>().CurHP > 0;
    }

    private bool AnythingToLevel()
    {
        return GetLevelableGems().Any();
    }

    private List<GemLevelUpElement> GetLevelableGems()
    {
        var gemsToLevelUp = new List<GemLevelUpElement>();

        var possibleGemsToLvlUpElements = GameController.IngameState.IngameUi?.GemLvlUpPanel?.GemsToLvlUp;

        if (possibleGemsToLvlUpElements == null || !possibleGemsToLvlUpElements.Any())
            return gemsToLevelUp;
        foreach (var possibleGemsToLvlUpElement in possibleGemsToLvlUpElements)
        {
            gemsToLevelUp.AddRange(
                from elem in possibleGemsToLvlUpElement.Children
                where elem.Text != null && elem.Text.Contains("Click to level")
                select possibleGemsToLvlUpElement);
        }

        return gemsToLevelUp;
    }
}