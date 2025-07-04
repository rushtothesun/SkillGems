using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;
using ExileCore.Shared.Attributes;

namespace SkillGems
{
    public class SkillGemsSettings : ISettings
    {
        //Mandatory setting to allow enabling/disabling your plugin
        public ToggleNode Enable { get; set; } = new ToggleNode(false);
        public ToggleNode AddPingIntoDelay { get; set; } = new ToggleNode(false);
        public ToggleNode UseMagicInput { get; set; } = new ToggleNode(false);
        [ConditionalDisplay(nameof(UseMagicInput))]
        public ToggleNode AutomaticLeveling { get; set; } = new ToggleNode(false);
        public HotkeyNode Run { get; set; } = new HotkeyNode(Keys.A);
        public RangeNode<int> DelayBetweenEachGemClick { get; set; } = new RangeNode<int>(20, 0, 1000);
        public RangeNode<int> DelayBetweenEachMouseEvent { get; set; } = new RangeNode<int>(20, 0, 1000);
    }
}