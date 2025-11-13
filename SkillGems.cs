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
        var (buttonElement, gemElement) = GetElementToClick();

        if (buttonElement == null) return;

        if (Settings.UseMagicInput.Value)
        {
            var gemDelay = Settings.DelayBetweenEachGemClick.Value;

            GameController.PluginBridge.GetMethod<Action<GemLevelUpElement>>("MagicInput.GemLevelUp")(gemElement);

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

            SetCursorPos(buttonElement);
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
        return GetElementToClick().buttonElement != null;
    }

    private (Element buttonElement, GemLevelUpElement gemElement) GetElementToClick()
    {
        var gemLvlUpPanel = GameController.IngameState.IngameUi?.GemLvlUpPanel;
        if (gemLvlUpPanel == null || !gemLvlUpPanel.IsVisible) return (null, null);

        // Path to Level All button: 4->(GemLvlUpPanel)1->0->0->0
        var levelAllButton = gemLvlUpPanel.GetChildAtIndex(0)?.GetChildAtIndex(0)?.GetChildAtIndex(0);
        if (levelAllButton != null && levelAllButton.IsVisible)
        {
            LogMessage("SkillGems: Level All button is visible. Targeting it.", 5);
            // For MagicInput, we need to return the parent GemLevelUpElement
            var levelAllGemElement = gemLvlUpPanel.GetChildAtIndex(0)?.GetChildAtIndex(0) as GemLevelUpElement;
            return (levelAllButton, levelAllGemElement);
        }

        // Path to the first gem container: 4->(GemLvlUpPanel)1->0->1->0->0
        var firstGemContainer = gemLvlUpPanel.GetChildAtIndex(0)?.GetChildAtIndex(1)?.GetChildAtIndex(0)?.GetChildAtIndex(0);
        if (firstGemContainer == null)
        {
            return (null, null);
        }

        // Text is at child index 3, Button is at child index 1
        var textElement = firstGemContainer.GetChildAtIndex(3);
        var buttonElement = firstGemContainer.GetChildAtIndex(1);

        if (textElement != null && buttonElement != null && textElement.Text != null && textElement.Text.Contains("Click to level"))
        {
            LogMessage($"SkillGems: Found levelable gem. Text: '{textElement.Text}'. Targeting button.", 5);
            // For MagicInput, we need to return the parent GemLevelUpElement (the first gem container's parent)
            var gemElement = gemLvlUpPanel.GetChildAtIndex(0)?.GetChildAtIndex(1)?.GetChildAtIndex(0) as GemLevelUpElement;
            return (buttonElement, gemElement);
        }
        
        return (null, null);
    }
}